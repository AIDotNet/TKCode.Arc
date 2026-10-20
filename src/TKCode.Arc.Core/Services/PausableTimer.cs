using Avalonia.Threading;

namespace TKCode.Arc.Primitives;

/// <summary>
/// S12: a one-shot timer that can pause and resume (toasts pause on hover, carousels while hidden).
/// Callbacks run on the UI thread.
/// </summary>
public sealed class PausableTimer : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Action _elapsed;
    private TimeSpan _duration;
    private TimeSpan _remaining;
    private DateTime _startedAt;
    private bool _running;

    public PausableTimer(TimeSpan duration, Action elapsed)
    {
        _duration = duration;
        _remaining = duration;
        _elapsed = elapsed;
        _timer = new DispatcherTimer(DispatcherPriority.Normal);
        _timer.Tick += OnTick;
    }

    public bool IsRunning => _running;

    public TimeSpan Remaining => _running ? _remaining - (DateTime.UtcNow - _startedAt) : _remaining;

    /// <summary>0 at start, 1 when elapsed.</summary>
    public double Progress => _duration <= TimeSpan.Zero ? 1 : Math.Clamp(1 - Remaining.TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);

    public void Start()
    {
        _remaining = _duration;
        Resume();
    }

    public void Restart(TimeSpan duration)
    {
        _duration = duration;
        Start();
    }

    public void Pause()
    {
        if (!_running)
        {
            return;
        }
        _remaining -= DateTime.UtcNow - _startedAt;
        _running = false;
        _timer.Stop();
    }

    public void Resume()
    {
        if (_running)
        {
            return;
        }
        if (_remaining <= TimeSpan.Zero)
        {
            _elapsed();
            return;
        }
        _startedAt = DateTime.UtcNow;
        _running = true;
        _timer.Interval = _remaining;
        _timer.Start();
    }

    public void Stop()
    {
        _running = false;
        _timer.Stop();
        _remaining = _duration;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        _running = false;
        _remaining = TimeSpan.Zero;
        _elapsed();
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
