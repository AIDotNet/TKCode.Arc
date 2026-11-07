using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The composer's own clock for the waits it plays (the simulated fetch, the autoplay delay, the sent confirmation): one
/// <see cref="LiveLoop"/> that runs at every motion level and off screen while a wait is pending, so time follows the
/// frame clock (tests step it with a <c>ManualFrameClock</c>) and nothing runs while the composer is not shown.
/// </summary>
internal sealed class LinkUnfurlScheduler : IDisposable
{
    private readonly LiveLoop _loop;
    private readonly List<Wait> _waits = new();
    private double _now;

    private sealed class Wait
    {
        public double Due;
        public TaskCompletionSource<bool> Completion = null!;
        public CancellationTokenRegistration Registration;
    }

    public LinkUnfurlScheduler(Visual owner)
    {
        _loop = new LiveLoop(owner, Step) { MinimumLevel = ArcMotionLevel.Off, TracksViewport = false };
    }

    /// <summary>Seconds this clock has run.</summary>
    public double Now => _now;

    /// <summary>Waits pending.</summary>
    public int Pending => _waits.Count;

    /// <summary>Completes after <paramref name="delay"/> of frame time; cancelling ends it with a cancellation.</summary>
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }
        var wait = new Wait
        {
            Due = _now + Math.Max(0, delay.TotalSeconds),
            Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        if (cancellationToken.CanBeCanceled)
        {
            wait.Registration = cancellationToken.Register(() =>
            {
                _waits.Remove(wait);
                wait.Completion.TrySetCanceled(cancellationToken);
            });
        }
        _waits.Add(wait);
        _loop.Start();
        return wait.Completion.Task;
    }

    /// <summary>Moves the clock on by <paramref name="seconds"/> and completes the waits that came due (tests).</summary>
    internal void Advance(double seconds) => Step(seconds);

    private bool Step(double dt)
    {
        _now += dt;
        for (var i = 0; i < _waits.Count; i++)
        {
            var wait = _waits[i];
            if (wait.Due <= _now + 1e-9)
            {
                _waits.RemoveAt(i);
                i--;
                wait.Registration.Dispose();
                wait.Completion.TrySetResult(true);
            }
        }
        return _waits.Count > 0;
    }

    public void Dispose()
    {
        foreach (var wait in _waits)
        {
            wait.Registration.Dispose();
            wait.Completion.TrySetCanceled();
        }
        _waits.Clear();
        _loop.Dispose();
    }
}
