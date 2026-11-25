// Ported from vscode (src/vs/editor/contrib/hover/browser/hoverOperation.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the timers (RunOnceScheduler in the Debouncer) run on an IdleScheduler's RunAfter, which the creator hands in (the
//    control's dispatcher timer; a manual clock in tests);
//  - the hover time is a function (upstream reads EditorOption.hover.delay);
//  - computeAsync returns an IAsyncEnumerable (upstream: AsyncIterableProducer) and the async computation is cancelled
//    with a CancellationTokenSource; results that arrive after a cancel are dropped (upstream's cancelable producer
//    stops yielding);
//  - errors go to UnexpectedErrors.Report; cancellation is not an error.

namespace TKCode.Arc.Editor.Contrib.Hover;

/// <summary>Computes hover results, synchronously and/or asynchronously (upstream's IHoverComputer).</summary>
internal interface IHoverComputer<TArgs, TResult>
{
    /// <summary>Called after half the hover time; null when the computer has nothing asynchronous.</summary>
    IAsyncEnumerable<TResult>? ComputeAsync(TArgs args, CancellationToken token);

    /// <summary>Called after the whole hover time.</summary>
    IReadOnlyList<TResult> ComputeSync(TArgs args);
}

/// <summary>A hover operation's result so far (upstream's HoverResult).</summary>
internal sealed record HoverResult<TArgs, TResult>(IReadOnlyList<TResult> Value, bool IsComplete, bool HasLoadingMessage, TArgs Options);

/// <summary>
/// Runs a hover computation with upstream's timing: nothing for half the hover time, then the asynchronous part, then
/// after the whole hover time the synchronous part and the first result; a loading message after three hover times
/// (upstream's HoverOperation).
/// </summary>
internal sealed class HoverOperation<TArgs, TResult> : IDisposable
    where TArgs : class
{
    private enum HoverOperationState
    {
        Idle,
        FirstWait,
        SecondWait,
        WaitingForAsync = 3,
        WaitingForAsyncShowingLoading = 4,
    }

    private readonly IHoverComputer<TArgs, TResult> _computer;
    private readonly Func<int> _hoverTime;
    private readonly Debouncer _asyncComputationScheduler;
    private readonly Debouncer _syncComputationScheduler;
    private readonly Debouncer _loadingMessageScheduler;

    private HoverOperationState _state = HoverOperationState.Idle;
    private CancellationTokenSource? _asyncIterable;
    private bool _asyncIterableDone;
    private List<TResult> _result = [];
    private TArgs? _options;
    private bool _disposed;

    public HoverOperation(IdleScheduler scheduler, IHoverComputer<TArgs, TResult> computer, Func<int> hoverTime)
    {
        _computer = computer;
        _hoverTime = hoverTime;
        _asyncComputationScheduler = new Debouncer(scheduler, TriggerAsyncComputation);
        _syncComputationScheduler = new Debouncer(scheduler, TriggerSyncComputation);
        _loadingMessageScheduler = new Debouncer(scheduler, TriggerLoadingMessage);
    }

    /// <summary>Raised with the results so far (upstream's onResult).</summary>
    public event Action<HoverResult<TArgs, TResult>>? OnResult;

    /// <summary>The options of the running operation, or null when idle.</summary>
    public TArgs? Options => _options;

    private int HoverTime => _hoverTime();

    private int FirstWaitTime => HoverTime / 2;

    private int SecondWaitTime => HoverTime - FirstWaitTime;

    private int LoadingMessageTime => 3 * HoverTime;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _asyncIterable?.Cancel();
        _asyncIterable = null;
        _options = null;
        _asyncComputationScheduler.Cancel();
        _syncComputationScheduler.Cancel();
        _loadingMessageScheduler.Cancel();
        OnResult = null;
    }

    private void SetState(HoverOperationState state, TArgs options)
    {
        _options = options;
        _state = state;
        FireResult(options);
    }

    private void TriggerAsyncComputation(TArgs options)
    {
        SetState(HoverOperationState.SecondWait, options);
        _syncComputationScheduler.Schedule(options, SecondWaitTime);

        var tokenSource = new CancellationTokenSource();
        var items = _computer.ComputeAsync(options, tokenSource.Token);
        if (items is not null)
        {
            _asyncIterableDone = false;
            _asyncIterable = tokenSource;
            _ = RunAsyncComputation(items, options, tokenSource);
        }
        else
        {
            tokenSource.Dispose();
            _asyncIterableDone = true;
        }
    }

    private async Task RunAsyncComputation(IAsyncEnumerable<TResult> items, TArgs options, CancellationTokenSource tokenSource)
    {
        try
        {
            await foreach (var item in items.WithCancellation(tokenSource.Token))
            {
                if (tokenSource.IsCancellationRequested || !ReferenceEquals(_asyncIterable, tokenSource))
                {
                    return;
                }

                if (item is not null)
                {
                    _result.Add(item);
                    FireResult(options);
                }
            }

            if (tokenSource.IsCancellationRequested || !ReferenceEquals(_asyncIterable, tokenSource))
            {
                return;
            }

            _asyncIterableDone = true;
            if (_state is HoverOperationState.WaitingForAsync or HoverOperationState.WaitingForAsyncShowingLoading)
            {
                SetState(HoverOperationState.Idle, options);
            }
        }
        catch (OperationCanceledException)
        {
            // cancelled: a newer operation runs
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
        }
    }

    private void TriggerSyncComputation(TArgs options)
    {
        _result = [.. _result, .. _computer.ComputeSync(options)];
        SetState(_asyncIterableDone ? HoverOperationState.Idle : HoverOperationState.WaitingForAsync, options);
    }

    private void TriggerLoadingMessage(TArgs options)
    {
        if (_state == HoverOperationState.WaitingForAsync)
        {
            SetState(HoverOperationState.WaitingForAsyncShowingLoading, options);
        }
    }

    private void FireResult(TArgs options)
    {
        if (_state is HoverOperationState.FirstWait or HoverOperationState.SecondWait)
        {
            // do not send out results before the hover time
            return;
        }

        var isComplete = _state == HoverOperationState.Idle;
        var hasLoadingMessage = _state == HoverOperationState.WaitingForAsyncShowingLoading;
        OnResult?.Invoke(new HoverResult<TArgs, TResult>([.. _result], isComplete, hasLoadingMessage, options));
    }

    /// <summary>Starts computing (upstream's start): after the hover delay, or at once.</summary>
    public void Start(HoverStartMode mode, TArgs options)
    {
        if (mode == HoverStartMode.Delayed)
        {
            if (_state == HoverOperationState.Idle)
            {
                SetState(HoverOperationState.FirstWait, options);
                _asyncComputationScheduler.Schedule(options, FirstWaitTime);
                _loadingMessageScheduler.Schedule(options, LoadingMessageTime);
            }
        }
        else
        {
            switch (_state)
            {
                case HoverOperationState.Idle:
                    TriggerAsyncComputation(options);
                    _syncComputationScheduler.Cancel();
                    TriggerSyncComputation(options);
                    break;
                case HoverOperationState.SecondWait:
                    _syncComputationScheduler.Cancel();
                    TriggerSyncComputation(options);
                    break;
            }
        }
    }

    /// <summary>Stops the operation and forgets its results (upstream's cancel).</summary>
    public void Cancel()
    {
        _asyncComputationScheduler.Cancel();
        _syncComputationScheduler.Cancel();
        _loadingMessageScheduler.Cancel();
        if (_asyncIterable is not null)
        {
            _asyncIterable.Cancel();
            _asyncIterable = null;
        }

        _result = [];
        _options = null;
        _state = HoverOperationState.Idle;
    }

    // upstream's Debouncer: a RunOnceScheduler that remembers the options of the last schedule
    private sealed class Debouncer
    {
        private readonly RunOnceScheduler _scheduler;
        private TArgs? _options;

        public Debouncer(IdleScheduler scheduler, Action<TArgs> runner) =>
            _scheduler = new RunOnceScheduler(scheduler, () => runner(_options!));

        public void Schedule(TArgs options, int debounceTimeMs)
        {
            _options = options;
            _scheduler.Schedule(debounceTimeMs);
        }

        public void Cancel() => _scheduler.Cancel();
    }
}
