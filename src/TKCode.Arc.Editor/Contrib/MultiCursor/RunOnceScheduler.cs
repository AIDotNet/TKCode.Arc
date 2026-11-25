// Ported from vscode (src/vs/base/common/async.ts: RunOnceScheduler, commit b57d5c9e), MIT license.
// Difference: the timer is the creator's runAfter hook (the control posts a dispatcher timer; tests run it by hand),
// since the engine has no event loop.

namespace TKCode.Arc.Editor.Contrib.MultiCursor;

/// <summary>Runs a callback once after a delay; scheduling again restarts the delay (upstream's RunOnceScheduler).</summary>
internal sealed class RunOnceScheduler : IDisposable
{
    private readonly Action _runner;
    private readonly Func<TimeSpan, Action, IDisposable> _runAfter;
    private readonly TimeSpan _defaultDelay;
    private IDisposable? _pending;
    private int _version;

    /// <param name="runner">The callback.</param>
    /// <param name="delay">The default delay.</param>
    /// <param name="runAfter">Runs an action once after a delay on the owning thread; disposing the result cancels it.</param>
    public RunOnceScheduler(Action runner, TimeSpan delay, Func<TimeSpan, Action, IDisposable> runAfter)
    {
        _runner = runner;
        _defaultDelay = delay;
        _runAfter = runAfter;
    }

    /// <summary>Whether a run is pending.</summary>
    public bool IsScheduled => _pending is not null;

    /// <summary>Schedules a run after <paramref name="delay"/> (the default delay when null), cancelling a pending one.</summary>
    public void Schedule(TimeSpan? delay = null)
    {
        Cancel();
        var version = ++_version;
        var handle = _runAfter(delay ?? _defaultDelay, () =>
        {
            if (version != _version)
            {
                return;
            }

            _version++;
            _pending = null;
            _runner();
        });
        if (version == _version)
        {
            _pending = handle;
        }
    }

    /// <summary>Cancels a pending run.</summary>
    public void Cancel()
    {
        _version++;
        _pending?.Dispose();
        _pending = null;
    }

    public void Dispose() => Cancel();
}
