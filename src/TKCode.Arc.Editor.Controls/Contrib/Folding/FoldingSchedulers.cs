// Ported from vscode (src/vs/base/common/async.ts: Delayer and RunOnceScheduler, commit b57d5c9e), MIT license; only
// what the folding controller uses.
// Differences:
//  - both run on the Avalonia UI thread: a DispatcherTimer for a delay, a Background-priority post for no delay;
//  - Delayer.Flush runs a pending task at once (the folding actions use it instead of waiting for the delay);
//  - in TKCode.Arc.Contrib.Folding, so that another feature's port of the same helpers does not collide;
//  - Delayer.Cancel completes the pending task with a cancellation (upstream rejects with CancellationError).

using Avalonia.Threading;

namespace TKCode.Arc.Contrib.Folding;

/// <summary>Runs the last task it was given once no new one came for a delay (upstream's Delayer).</summary>
internal sealed class Delayer<T> : IDisposable
{
    private Func<Task<T>>? _task;
    private TaskCompletionSource<T>? _completion;
    private DispatcherTimer? _timer;
    private int _generation;
    private bool _disposed;

    public Delayer(TimeSpan defaultDelay)
    {
        DefaultDelay = defaultDelay;
    }

    /// <summary>The delay of <see cref="Trigger"/>.</summary>
    public TimeSpan DefaultDelay { get; set; }

    /// <summary>Whether a task waits for its delay.</summary>
    public bool IsTriggered => _completion is not null;

    /// <summary>
    /// Schedules <paramref name="task"/> after <paramref name="delay"/> (default <see cref="DefaultDelay"/>), replacing a
    /// task that still waits; the result completes with the task that runs.
    /// </summary>
    public Task<T> Trigger(Func<Task<T>> task, TimeSpan? delay = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _task = task;
        StopTimer();
        _completion ??= new TaskCompletionSource<T>();
        var completion = _completion;
        var wait = delay ?? DefaultDelay;
        var generation = ++_generation;
        if (wait <= TimeSpan.Zero)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (generation == _generation)
                {
                    Run();
                }
            }, DispatcherPriority.Background);
        }
        else
        {
            _timer = new DispatcherTimer(wait, DispatcherPriority.Background, (_, _) => Run());
            _timer.Start();
        }

        return completion.Task;
    }

    /// <summary>Runs a waiting task now.</summary>
    public void Flush()
    {
        if (_completion is not null)
        {
            Run();
        }
    }

    /// <summary>Drops a waiting task; its result completes as cancelled.</summary>
    public void Cancel()
    {
        StopTimer();
        _generation++;
        _task = null;
        var completion = _completion;
        _completion = null;
        completion?.TrySetCanceled();
    }

    private async void Run()
    {
        StopTimer();
        _generation++;
        var task = _task;
        var completion = _completion;
        _task = null;
        _completion = null;
        if (task is null || completion is null)
        {
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

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void Dispose()
    {
        Cancel();
        _disposed = true;
    }
}

/// <summary>Runs an action once after a delay; scheduling again restarts the delay (upstream's RunOnceScheduler).</summary>
internal sealed class RunOnceScheduler : IDisposable
{
    private readonly Action _runner;
    private readonly TimeSpan _delay;
    private DispatcherTimer? _timer;

    public RunOnceScheduler(Action runner, TimeSpan delay)
    {
        _runner = runner;
        _delay = delay;
    }

    /// <summary>Whether a run waits for its delay.</summary>
    public bool IsScheduled() => _timer is not null;

    /// <summary>Schedules a run after the delay, dropping one that waits.</summary>
    public void Schedule()
    {
        Cancel();
        _timer = new DispatcherTimer(_delay, DispatcherPriority.Background, (_, _) =>
        {
            Cancel();
            _runner();
        });
        _timer.Start();
    }

    /// <summary>Runs a waiting run now (tests).</summary>
    public void Flush()
    {
        if (_timer is not null)
        {
            Cancel();
            _runner();
        }
    }

    /// <summary>Drops a waiting run.</summary>
    public void Cancel()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void Dispose() => Cancel();
}
