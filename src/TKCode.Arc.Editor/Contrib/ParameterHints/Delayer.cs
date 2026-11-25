// Ported from vscode (src/vs/base/common/async.ts: Delayer, commit b57d5c9e), MIT license.
// Differences from upstream: the timeout is an IdleScheduler's RunAfter; cancel completes the pending task as
// cancelled (upstream rejects it with a CancellationError); the microtask delay is not ported. Kept in the parameter
// hints namespace so that it does not clash with another feature's copy.

using TKCode.Arc.Editor.Contrib.Hover;

namespace TKCode.Arc.Editor.Contrib.ParameterHints;

/// <summary>
/// Runs the last task given to <see cref="Trigger"/> once the delay passed without another trigger (upstream's Delayer):
/// a burst of triggers runs one task.
/// </summary>
internal sealed class Delayer<T> : IDisposable
{
    private readonly RunOnceScheduler _timeout;
    private TaskCompletionSource<T?>? _completion;
    private Func<Task<T>>? _task;

    public Delayer(IdleScheduler scheduler, int defaultDelay)
    {
        DefaultDelay = defaultDelay;
        _timeout = new RunOnceScheduler(scheduler, OnTimeout);
    }

    public int DefaultDelay { get; }

    /// <summary>Runs <paramref name="task"/> after the delay unless triggered again; the result is the task's.</summary>
    public Task<T?> Trigger(Func<Task<T>> task, int? delay = null)
    {
        _task = task;
        _completion ??= new TaskCompletionSource<T?>();
        var completion = _completion.Task;
        _timeout.Schedule(delay ?? DefaultDelay);
        return completion;
    }

    /// <summary>Whether a task waits for its delay.</summary>
    public bool IsTriggered() => _timeout.IsScheduled();

    /// <summary>Drops the pending task; its <see cref="Trigger"/> result completes as cancelled.</summary>
    public void Cancel()
    {
        _timeout.Cancel();
        if (_completion is { } completion)
        {
            _completion = null;
            _task = null;
            completion.TrySetCanceled();
        }
    }

    public void Dispose() => Cancel();

    private async void OnTimeout()
    {
        var completion = _completion;
        var task = _task;
        _completion = null;
        _task = null;
        if (completion is null)
        {
            return;
        }

        if (task is null)
        {
            completion.TrySetResult(default);
            return;
        }

        try
        {
            completion.TrySetResult(await task().ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled();
        }
        catch (Exception e)
        {
            completion.TrySetException(e);
        }
    }
}
