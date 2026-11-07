using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The player's few timed reactions (control bar hide, click-vs-double-click wait, delayed spinner, drag-seek throttle,
/// buffering announcement), kept as due times on one <see cref="TimeProvider"/> and run by one dispatcher timer set for
/// the nearest. Because the times come from the clock, tests on a fake clock advance it and call <see cref="RunDue"/>.
/// </summary>
internal sealed class VideoDeadlines
{
    private readonly Func<TimeProvider> _clock;
    private readonly Dictionary<string, (long Due, Action Action)> _pending = new(StringComparer.Ordinal);
    private DispatcherTimer? _timer;

    public VideoDeadlines(Func<TimeProvider> clock)
    {
        _clock = clock;
    }

    /// <summary>Whether <paramref name="key"/> is waiting.</summary>
    public bool IsPending(string key) => _pending.ContainsKey(key);

    /// <summary>Runs <paramref name="action"/> once <paramref name="delay"/> has passed, replacing a pending one with the same key.</summary>
    public void Schedule(string key, TimeSpan delay, Action action)
    {
        var clock = _clock();
        var due = clock.GetTimestamp() + (long)(Math.Max(0, delay.TotalSeconds) * clock.TimestampFrequency);
        _pending[key] = (due, action);
        Arm();
    }

    /// <summary>Drops a pending action.</summary>
    public void Cancel(string key)
    {
        if (_pending.Remove(key) && _pending.Count == 0)
        {
            _timer?.Stop();
        }
    }

    /// <summary>Drops every pending action.</summary>
    public void Clear()
    {
        _pending.Clear();
        _timer?.Stop();
    }

    /// <summary>Runs the actions whose time has come, in due order.</summary>
    public void RunDue()
    {
        var now = _clock().GetTimestamp();
        while (true)
        {
            string? next = null;
            long nextDue = long.MaxValue;
            foreach (var (key, entry) in _pending)
            {
                if (entry.Due <= now && entry.Due < nextDue)
                {
                    next = key;
                    nextDue = entry.Due;
                }
            }
            if (next is null)
            {
                break;
            }
            var action = _pending[next].Action;
            _pending.Remove(next);
            action();
        }
        Arm();
    }

    private void Arm()
    {
        if (_pending.Count == 0)
        {
            _timer?.Stop();
            return;
        }
        var clock = _clock();
        var now = clock.GetTimestamp();
        var due = _pending.Values.Min(static e => e.Due);
        var wait = TimeSpan.FromSeconds(Math.Max(0, due - now) / (double)clock.TimestampFrequency);
        if (wait < TimeSpan.FromMilliseconds(1))
        {
            wait = TimeSpan.FromMilliseconds(1);
        }
        _timer ??= new DispatcherTimer(DispatcherPriority.Background);
        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Interval = wait;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        RunDue();
    }
}
