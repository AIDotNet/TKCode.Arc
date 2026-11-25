// Ported from vscode (src/vs/base/common/async.ts: RunOnceScheduler, commit b57d5c9e), MIT license.
// Differences from upstream: the timer is an IdleScheduler's RunAfter (the owner's dispatcher timer, a manual clock in
// tests); a timer that fires after it was cancelled or rescheduled does nothing (a fake scheduler may fire a disposed
// timer). Kept in the hover feature's namespace so that it does not clash with another feature's copy.

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>Runs a callback once after a delay; scheduling again restarts the delay (upstream's RunOnceScheduler).</summary>
internal sealed class RunOnceScheduler(IdleScheduler scheduler, Action runner, int defaultDelay = 0) : IDisposable
{
    private IDisposable? _timer;
    private object? _pending;

    /// <summary>Whether a run is pending.</summary>
    public bool IsScheduled() => _pending is not null;

    /// <summary>Runs the callback after <paramref name="delay"/> milliseconds (the default delay when null), cancelling a pending run.</summary>
    public void Schedule(int? delay = null)
    {
        Cancel();
        var pending = new object();
        _pending = pending;
        var timer = scheduler.RunAfter(TimeSpan.FromMilliseconds(Math.Max(0, delay ?? defaultDelay)), () =>
        {
            if (!ReferenceEquals(_pending, pending))
            {
                return;
            }

            _pending = null;
            _timer = null;
            runner();
        });

        // a scheduler that ran the callback at once already cleared the pending run
        if (ReferenceEquals(_pending, pending))
        {
            _timer = timer;
        }
    }

    /// <summary>Cancels a pending run.</summary>
    public void Cancel()
    {
        _pending = null;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Cancel();
}
