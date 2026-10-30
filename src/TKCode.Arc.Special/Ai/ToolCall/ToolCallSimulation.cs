using Avalonia;
using Avalonia.Collections;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>How a scripted call ends in a <see cref="ToolCallSimulation"/>.</summary>
public enum ToolCallSimulationOutcome
{
    /// <summary>The call runs, then succeeds.</summary>
    Succeeds,

    /// <summary>The call runs, then fails; the run waits for Retry, after which the call runs again and succeeds.</summary>
    FailsUntilRetried,

    /// <summary>The call appears waiting for approval; Allow runs it to success, Deny marks it denied and the run goes on.</summary>
    NeedsApproval
}

/// <summary>Where a <see cref="ToolCallSimulation"/> stands.</summary>
public enum ToolCallSimulationState
{
    /// <summary>Not started, or stopped.</summary>
    Idle,

    /// <summary>Calls are being appended and run.</summary>
    Working,

    /// <summary>A failed call waits for <see cref="ToolCallSimulation.Retry"/>.</summary>
    WaitingForRetry,

    /// <summary>A call waits for <see cref="ToolCallSimulation.Approve"/> or <see cref="ToolCallSimulation.Deny"/>.</summary>
    WaitingForApproval,

    /// <summary>Every scripted call has settled.</summary>
    Finished
}

/// <summary>What just happened in a <see cref="ToolCallSimulation"/>.</summary>
public enum ToolCallSimulationPhase
{
    /// <summary>The run started (the list was cleared).</summary>
    Started,

    /// <summary>A call was appended (running, or waiting for approval).</summary>
    CallAppended,

    /// <summary>A call succeeded.</summary>
    CallSucceeded,

    /// <summary>A call failed; the run waits for Retry.</summary>
    CallFailed,

    /// <summary>A call waits for approval.</summary>
    WaitingForApproval,

    /// <summary>A failed call was retried.</summary>
    Retried,

    /// <summary>A waiting call was allowed.</summary>
    Allowed,

    /// <summary>A waiting call was denied.</summary>
    Denied,

    /// <summary>Every scripted call settled.</summary>
    Finished
}

/// <summary>Data of <see cref="ToolCallSimulation.Changed"/>.</summary>
public sealed class ToolCallSimulationEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public ToolCallSimulationEventArgs(ToolCallSimulationPhase phase, ToolCallItem? item)
    {
        Phase = phase;
        Item = item;
    }

    /// <summary>What happened.</summary>
    public ToolCallSimulationPhase Phase { get; }

    /// <summary>The call it happened to; null for <see cref="ToolCallSimulationPhase.Started"/> and
    /// <see cref="ToolCallSimulationPhase.Finished"/>.</summary>
    public ToolCallItem? Item { get; }
}

/// <summary>One scripted call of a <see cref="ToolCallSimulation"/>.</summary>
public sealed class ToolCallSimulationStep
{
    /// <summary>Creates a step.</summary>
    /// <param name="call">The call's content (titles, input, output, summary, message, duration); each run appends a copy.</param>
    /// <param name="outcome">How the call ends.</param>
    public ToolCallSimulationStep(ToolCallItem call, ToolCallSimulationOutcome outcome = ToolCallSimulationOutcome.Succeeds)
    {
        Call = call;
        Outcome = outcome;
    }

    /// <summary>The call's content; each run appends a fresh copy.</summary>
    public ToolCallItem Call { get; }

    /// <summary>How the call ends.</summary>
    public ToolCallSimulationOutcome Outcome { get; }

    /// <summary>The wait before the call is appended: after the start for the first step, after the previous call settled
    /// for the others (225ms by default, as measured between calls).</summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(225);

    /// <summary>How long the call runs before it settles (or fails); null uses the call's duration, or one second.</summary>
    public TimeSpan? RunFor { get; init; }

    /// <summary>How long a retried call runs before it succeeds (700ms by default; the duration it shows stays the call's
    /// own).</summary>
    public TimeSpan RetryRunFor { get; init; } = TimeSpan.FromMilliseconds(700);

    internal TimeSpan RunTime => RunFor ?? Call.Duration ?? TimeSpan.FromSeconds(1);
}

/// <summary>
/// Plays a scripted agent turn into <see cref="Items"/> on a clock: calls are appended one after another, run for their
/// time and settle; a failing call waits for <see cref="Retry"/> and a call that needs approval waits for
/// <see cref="Approve"/> or <see cref="Deny"/>. Your app writes the <see cref="Script"/>; nothing ships with the package.
/// Give it to <see cref="ToolCallGroup.Simulation"/> (the group shows <see cref="Items"/>, follows <see cref="IsWorking"/>,
/// starts the run when it is on screen and stops it when it leaves), or bind a group's <c>ItemsSource</c> to
/// <see cref="Items"/> and its <c>IsActive</c> to <see cref="IsWorking"/> yourself. Allow, Deny and Retry on the rows reach
/// the simulation when nothing else handles them. Time comes from a <see cref="TimeProvider"/>, so tests can step it;
/// callbacks run on the UI thread. Create one per group: it keeps the playback state.
/// </summary>
public sealed class ToolCallSimulation : AvaloniaObject, IDisposable
{
    /// <summary>Where the run stands.</summary>
    public static readonly DirectProperty<ToolCallSimulation, ToolCallSimulationState> StateProperty =
        AvaloniaProperty.RegisterDirect<ToolCallSimulation, ToolCallSimulationState>(nameof(State), s => s.State);

    /// <summary>Whether the agent is working (appending or running calls): bind a group's <c>IsActive</c> to it, so its
    /// header keeps saying "Using" between calls.</summary>
    public static readonly DirectProperty<ToolCallSimulation, bool> IsWorkingProperty =
        AvaloniaProperty.RegisterDirect<ToolCallSimulation, bool>(nameof(IsWorking), s => s.IsWorking);

    private readonly TimeProvider _time;
    private IReadOnlyList<ToolCallSimulationStep> _script;
    private ITimer? _timer;
    private int _generation;
    private int _next;
    private ToolCallItem? _waiting;
    private ToolCallSimulationStep? _waitingStep;
    private ToolCallSimulationState _state;
    private bool _isWorking;

    /// <summary>Creates a simulation with no calls on the system clock; set <see cref="Script"/> (in XAML too).</summary>
    public ToolCallSimulation()
        : this([])
    {
    }

    /// <summary>Creates a simulation of <paramref name="script"/>.</summary>
    /// <param name="script">The calls to play, in order (null: none).</param>
    /// <param name="timeProvider">The clock (null: the system clock).</param>
    public ToolCallSimulation(IEnumerable<ToolCallSimulationStep>? script, TimeProvider? timeProvider = null)
    {
        _script = (script ?? []).ToList();
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>The calls of the run so far, appended and changed in place as it plays.</summary>
    public AvaloniaList<ToolCallItem> Items { get; } = new();

    /// <summary>
    /// The scripted calls, in order, written by your app (each run appends copies of their calls). Setting it while a run
    /// plays starts again from the top; null is no calls.
    /// </summary>
    public IReadOnlyList<ToolCallSimulationStep> Script
    {
        get => _script;
        set
        {
            _script = (value ?? []).ToList();
            if (State != ToolCallSimulationState.Idle)
            {
                Start();
            }
        }
    }

    /// <inheritdoc cref="StateProperty"/>
    public ToolCallSimulationState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="IsWorkingProperty"/>
    public bool IsWorking
    {
        get => _isWorking;
        private set => SetAndRaise(IsWorkingProperty, ref _isWorking, value);
    }

    /// <summary>Whether a call was denied in this run.</summary>
    public bool WasDenied { get; private set; }

    /// <summary>The call waiting for Retry or approval, if any.</summary>
    public ToolCallItem? WaitingCall => _waiting;

    /// <summary>Raised on the UI thread at every beat of the run (a call appended, settled, failed, retried, allowed,
    /// denied; the start and the end).</summary>
    public event EventHandler<ToolCallSimulationEventArgs>? Changed;

    /// <summary>Starts the run from the top: the list is cleared and the first call is appended after its delay. Calling
    /// it again restarts.</summary>
    public void Start()
    {
        Cancel();
        foreach (var item in Items)
        {
            item.Simulation = null;
        }
        Items.Clear();
        _next = 0;
        _waiting = null;
        _waitingStep = null;
        WasDenied = false;
        State = ToolCallSimulationState.Working;
        IsWorking = true;
        Raise(ToolCallSimulationPhase.Started, null);
        ScheduleNext();
    }

    /// <summary>Stops the clock; the calls stay as they are.</summary>
    public void Stop()
    {
        Cancel();
        _waiting = null;
        _waitingStep = null;
        State = ToolCallSimulationState.Idle;
        IsWorking = false;
    }

    /// <summary>Retries the failed call the run waits on; false when <paramref name="item"/> is not that call.</summary>
    public bool Retry(ToolCallItem item)
    {
        if (State != ToolCallSimulationState.WaitingForRetry || !ReferenceEquals(item, _waiting) || _waitingStep is not { } step)
        {
            return false;
        }
        Resume();
        item.Status = ToolCallStatus.Running;
        Raise(ToolCallSimulationPhase.Retried, item);
        After(step.RetryRunFor, () => Succeed(item));
        return true;
    }

    /// <summary>Allows the call the run waits on: it runs, then succeeds. False when <paramref name="item"/> is not that call.</summary>
    public bool Approve(ToolCallItem item)
    {
        if (State != ToolCallSimulationState.WaitingForApproval || !ReferenceEquals(item, _waiting) || _waitingStep is not { } step)
        {
            return false;
        }
        Resume();
        item.Status = ToolCallStatus.Running;
        Raise(ToolCallSimulationPhase.Allowed, item);
        After(step.RunTime, () => Succeed(item));
        return true;
    }

    /// <summary>Denies the call the run waits on: it is marked denied and the run goes on. False when
    /// <paramref name="item"/> is not that call.</summary>
    public bool Deny(ToolCallItem item)
    {
        if (State != ToolCallSimulationState.WaitingForApproval || !ReferenceEquals(item, _waiting))
        {
            return false;
        }
        Resume();
        WasDenied = true;
        item.Status = ToolCallStatus.Denied;
        Raise(ToolCallSimulationPhase.Denied, item);
        Settled();
        return true;
    }

    /// <summary>Stops the clock.</summary>
    public void Dispose() => Stop();

    private void Resume()
    {
        _waiting = null;
        _waitingStep = null;
        State = ToolCallSimulationState.Working;
        IsWorking = true;
    }

    private void ScheduleNext()
    {
        if (_next >= _script.Count)
        {
            State = ToolCallSimulationState.Finished;
            IsWorking = false;
            Raise(ToolCallSimulationPhase.Finished, null);
            return;
        }
        var step = _script[_next++];
        After(step.Delay, () => Append(step));
    }

    private void Append(ToolCallSimulationStep step)
    {
        var item = step.Call.Copy();
        item.Simulation = this;
        if (step.Outcome == ToolCallSimulationOutcome.NeedsApproval)
        {
            item.Status = ToolCallStatus.Approval;
            Items.Add(item);
            Raise(ToolCallSimulationPhase.CallAppended, item);
            Wait(item, step, ToolCallSimulationState.WaitingForApproval);
            Raise(ToolCallSimulationPhase.WaitingForApproval, item);
            return;
        }
        item.Status = ToolCallStatus.Running;
        Items.Add(item);
        Raise(ToolCallSimulationPhase.CallAppended, item);
        if (step.Outcome == ToolCallSimulationOutcome.FailsUntilRetried)
        {
            After(step.RunTime, () =>
            {
                item.Status = ToolCallStatus.Error;
                Wait(item, step, ToolCallSimulationState.WaitingForRetry);
                Raise(ToolCallSimulationPhase.CallFailed, item);
            });
            return;
        }
        After(step.RunTime, () => Succeed(item));
    }

    private void Wait(ToolCallItem item, ToolCallSimulationStep step, ToolCallSimulationState state)
    {
        _waiting = item;
        _waitingStep = step;
        State = state;
        IsWorking = false;
    }

    private void Succeed(ToolCallItem item)
    {
        item.Status = ToolCallStatus.Success;
        Raise(ToolCallSimulationPhase.CallSucceeded, item);
        Settled();
    }

    /// <summary>A call settled: the next one follows after its delay, or the run finishes.</summary>
    private void Settled() => ScheduleNext();

    private void Raise(ToolCallSimulationPhase phase, ToolCallItem? item) =>
        Changed?.Invoke(this, new ToolCallSimulationEventArgs(phase, item));

    /// <summary>Runs <paramref name="action"/> on the UI thread after <paramref name="delay"/> unless the run was restarted
    /// or stopped meanwhile. One beat is pending at a time.</summary>
    private void After(TimeSpan delay, Action action)
    {
        Cancel();
        var generation = _generation;
        _timer = ToolCallTimer.After(_time, delay, () =>
        {
            if (generation == _generation)
            {
                _timer = null;
                action();
            }
        });
    }

    private void Cancel()
    {
        _generation++;
        _timer?.Dispose();
        _timer = null;
    }
}

/// <summary>One-shot timers on a <see cref="TimeProvider"/> whose callbacks run on the UI thread.</summary>
internal static class ToolCallTimer
{
    /// <summary>Runs <paramref name="action"/> on the UI thread after <paramref name="delay"/>; dispose to cancel.</summary>
    public static ITimer After(TimeProvider time, TimeSpan delay, Action action)
    {
        var cancelled = new StrongBox<bool>();
        var timer = time.CreateTimer(_ =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Run();
            }
            else
            {
                Dispatcher.UIThread.Post(Run);
            }
        }, null, delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
        return new Handle(timer, cancelled);

        void Run()
        {
            if (!cancelled.Value)
            {
                cancelled.Value = true;
                action();
            }
        }
    }

    private sealed class StrongBox<T>
    {
        public T? Value;
    }

    /// <summary>Disposing marks the callback cancelled too, so one already posted to the UI thread does nothing.</summary>
    private sealed class Handle(ITimer timer, StrongBox<bool> cancelled) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => timer.Change(dueTime, period);

        public void Dispose()
        {
            cancelled.Value = true;
            timer.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
