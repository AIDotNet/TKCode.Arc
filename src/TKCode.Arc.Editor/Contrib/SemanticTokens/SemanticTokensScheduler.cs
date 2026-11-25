// Adaptation of vscode (src/vs/base/common/async.ts: RunOnceScheduler, commit b57d5c9e), MIT license, on the model's
// IdleScheduler (its RunAfter is the timer). Named for this feature so that it does not collide with other features'
// schedulers.

namespace TKCode.Arc.Editor.Contrib.SemanticTokens;

/// <summary>Runs a callback once after a delay; scheduling again restarts the delay (upstream's RunOnceScheduler).</summary>
internal sealed class SemanticTokensScheduler(IdleScheduler scheduler, Action runner, int defaultDelay) : IDisposable
{
    private IDisposable? _timer;
    private int _generation;
    private bool _scheduled;
    private bool _disposed;

    public bool IsScheduled() => _scheduled;

    public void Schedule(int? delay = null)
    {
        Cancel();
        if (_disposed)
        {
            return;
        }

        var generation = ++_generation;
        _scheduled = true;
        var timer = scheduler.RunAfter(TimeSpan.FromMilliseconds(delay ?? defaultDelay), () =>
        {
            if (generation != _generation || !_scheduled)
            {
                return;
            }

            _scheduled = false;
            _timer = null;
            runner();
        });
        if (generation == _generation && _scheduled)
        {
            _timer = timer;
        }
    }

    public void Cancel()
    {
        _generation++;
        _scheduled = false;
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel();
    }
}
