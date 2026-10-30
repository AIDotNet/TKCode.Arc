using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>What <see cref="AgentPlanSimulation"/> drives: the live steps, the shown status and the planning state.</summary>
internal interface IAgentPlanSimulationHost
{
    /// <summary>The plan's live steps.</summary>
    AvaloniaList<AgentPlanStep> Steps { get; }

    /// <summary>Shows <paramref name="status"/> as the plan's status.</summary>
    void SetSimulatedStatus(AgentPlanStatus status);

    /// <summary>Shows whether Keep planning is waiting for a suggestion and whether it can be pressed again.</summary>
    void SetPlanning(bool busy, bool canKeepPlanning);

    /// <summary>Inserts a suggested step at <paramref name="index"/> and streams its title in.</summary>
    void InsertSuggestion(AgentPlanStep step, int index);
}

/// <summary>
/// A simulated agent for an <see cref="AgentPlan"/> that has no <see cref="AgentPlan.Status"/>: give it to
/// <see cref="AgentPlan.Simulation"/> and it answers the plan's buttons (Approve plan, Keep planning, Pause, Resume, Retry,
/// Skip, Start over) and plays a scripted run, one step after the other, with the beats measured on the reference. Your app
/// writes the content: the draft (<see cref="Steps"/>), what Keep planning suggests (<see cref="Suggestions"/>), the goal,
/// the model and the summary; nothing ships with the package. Scripts are found by step id; a step without one runs
/// <see cref="GenericScript"/>. It is event-driven: exactly one timer waits per state change, nothing runs while the plan
/// is idle, and Pause keeps the remaining time of that timer so Resume continues where the run stopped. The data is read
/// when the plan attaches the simulation; create one per plan, since it keeps the run's state.
/// </summary>
public sealed class AgentPlanSimulation
{
    // Beats (ms), notes 2b.2.
    internal static readonly TimeSpan ApproveToFirst = TimeSpan.FromMilliseconds(280);
    internal static readonly TimeSpan ActiveToFirstLine = TimeSpan.FromMilliseconds(372);
    internal static readonly TimeSpan LineInterval = TimeSpan.FromMilliseconds(540);
    internal static readonly TimeSpan LastLineToDone = TimeSpan.FromMilliseconds(470);
    internal static readonly TimeSpan DoneToNext = TimeSpan.FromMilliseconds(290);
    internal static readonly TimeSpan LineToInsert = TimeSpan.FromMilliseconds(417);
    internal static readonly TimeSpan InsertToNextLine = TimeSpan.FromMilliseconds(578);
    internal static readonly TimeSpan LastLineToFailure = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan LastDoneToFinish = TimeSpan.FromMilliseconds(420);
    internal static readonly TimeSpan RetryToFirstLine = TimeSpan.FromMilliseconds(555);
    internal static readonly TimeSpan RetryLineInterval = TimeSpan.FromMilliseconds(577);
    internal static readonly TimeSpan RetryLastLineToDone = TimeSpan.FromMilliseconds(465);
    internal static readonly TimeSpan SkipToNext = TimeSpan.FromMilliseconds(277);
    internal static readonly TimeSpan PlanningWait = TimeSpan.FromMilliseconds(720);

    private readonly IAgentPlanScheduler _scheduler;
    private readonly Dictionary<string, AgentPlanStepScript> _scripts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failedOnce = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inserted = new(StringComparer.Ordinal);
    private readonly List<AgentPlanStep> _initial = new();
    private IAgentPlanSimulationHost? _host;
    private WeakReference<IAgentPlanSimulationHost>? _heldHost;
    private IDisposable? _timer;
    private Action? _pending;
    private TimeSpan _due;
    private TimeSpan _remaining;
    private bool _suspended;
    private int _nextSuggestion;
    private bool _planning;

    /// <summary>Creates a simulation with no steps; set its data (in XAML too) before the plan attaches it.</summary>
    public AgentPlanSimulation()
        : this(new AgentPlanDispatcherScheduler())
    {
    }

    internal AgentPlanSimulation(IAgentPlanScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>The scripted steps: the draft a plan with no steps starts from, and the scripts of those ids. Empty by default.</summary>
    public IReadOnlyList<AgentPlanScriptedStep> Steps { get; set; } = [];

    /// <summary>What Keep planning suggests, in order (each goes in before the last step). Empty by default.</summary>
    public IReadOnlyList<AgentPlanScriptedStep> Suggestions { get; set; } = [];

    /// <summary>
    /// The script of a step that has none (a step you added); null (the default) finishes such a step without activity lines
    /// or a time.
    /// </summary>
    public AgentPlanStepScript? GenericScript { get; set; }

    /// <summary>The goal shown while the simulation runs, unless the plan sets its own <see cref="AgentPlan.Goal"/>.</summary>
    public string? Goal { get; set; }

    /// <summary>The model shown while the simulation runs, unless the plan sets its own <see cref="AgentPlan.Model"/>.</summary>
    public AgentModel? Model { get; set; }

    /// <summary>The summary shown when the run is done, unless the plan sets its own <see cref="AgentPlan.Summary"/>.</summary>
    public string? Summary { get; set; }

    /// <summary>The status the simulation shows.</summary>
    public AgentPlanStatus Status { get; private set; }

    /// <summary>True while it drives a plan.</summary>
    public bool IsAttached => Host is not null;

    /// <summary>True when Keep planning has suggestions left.</summary>
    public bool CanKeepPlanning => _nextSuggestion < Suggestions.Count;

    /// <summary>True while a timer waits (a step is being worked on, or a suggestion is on its way).</summary>
    public bool IsWaiting => _pending is not null;

    internal TimeSpan RemainingWhilePaused => _remaining;

    /// <summary>The plan it drives; held weakly while the plan is off screen (see <see cref="Suspend"/>).</summary>
    private IAgentPlanSimulationHost? Host => _host ?? (_heldHost is { } held && held.TryGetTarget(out var host) ? host : null);

    /// <summary>
    /// Starts driving <paramref name="host"/>: a plan with no steps gets the scripted draft; the steps it has now are what
    /// Start over goes back to. Returns true when it filled in the scripted draft.
    /// </summary>
    internal bool Attach(IAgentPlanSimulationHost host)
    {
        Detach();
        _host = host;
        IndexScripts();
        var filled = false;
        if (host.Steps.Count == 0)
        {
            foreach (var step in Steps)
            {
                host.Steps.Add(step.CreateStep());
            }
            filled = true;
        }
        _initial.Clear();
        foreach (var step in host.Steps)
        {
            _initial.Add(step.CloneAsPending());
        }
        Status = AgentPlanStatus.Draft;
        _nextSuggestion = 0;
        _planning = false;
        host.SetSimulatedStatus(Status);
        host.SetPlanning(false, CanKeepPlanning);
        return filled;
    }

    /// <summary>Stops driving the plan; nothing waits afterwards.</summary>
    internal void Detach()
    {
        Cancel();
        _host = null;
        _heldHost = null;
        _suspended = false;
    }

    /// <summary>
    /// Holds the waiting timer (the plan left the screen); <see cref="Wake"/> continues it. Meanwhile the plan is held
    /// weakly, so a simulation that outlives it (a view model's) does not keep it alive.
    /// </summary>
    internal void Suspend()
    {
        if (_host is not null)
        {
            _heldHost = new WeakReference<IAgentPlanSimulationHost>(_host);
            _host = null;
        }
        if (_suspended || _pending is null || _timer is null)
        {
            _suspended = true;
            return;
        }
        _suspended = true;
        _remaining = Max(TimeSpan.Zero, _due - _scheduler.Now);
        _timer.Dispose();
        _timer = null;
    }

    /// <summary>Continues a timer held by <see cref="Suspend"/>.</summary>
    internal void Wake()
    {
        if (_heldHost is not null)
        {
            _host = Host;
            _heldHost = null;
        }
        if (!_suspended)
        {
            return;
        }
        _suspended = false;
        if (_pending is { } action && _timer is null && Status != AgentPlanStatus.Paused)
        {
            Schedule(_remaining, action);
        }
    }

    private void IndexScripts()
    {
        _scripts.Clear();
        foreach (var step in Steps)
        {
            Index(step);
        }
        foreach (var step in Suggestions)
        {
            Index(step);
        }
    }

    private void Index(AgentPlanScriptedStep step)
    {
        _scripts[step.Id] = step.Script;
        if (step.Script.Inserts is { } inserted)
        {
            Index(inserted);
        }
    }

    /// <summary>A step without a script and no <see cref="GenericScript"/>: no lines, no time.</summary>
    private static readonly AgentPlanStepScript s_noScript = new([], TimeSpan.Zero);

    private AgentPlanStepScript ScriptOf(AgentPlanStep step) =>
        _scripts.TryGetValue(step.Id, out var script) ? script : GenericScript ?? s_noScript;

    // The plan's buttons ----------------------------------------------------------------------------------------------

    /// <summary>Approve plan: every step starts pending and the first one starts after 280ms.</summary>
    internal void OnApproved()
    {
        if (Host is not { } host || Status != AgentPlanStatus.Draft)
        {
            return;
        }
        Cancel();
        _planning = false;
        host.SetPlanning(false, CanKeepPlanning);
        _failedOnce.Clear();
        _inserted.Clear();
        foreach (var step in host.Steps)
        {
            step.Activities.Clear();
            step.Error = null;
            step.Duration = null;
            step.State = AgentPlanStepState.Pending;
        }
        SetStatus(AgentPlanStatus.Running);
        Schedule(ApproveToFirst, ActivateNext);
    }

    /// <summary>Pause (true) keeps the waiting timer's remaining time; Resume (false) continues it.</summary>
    internal void OnPauseChanged(bool paused)
    {
        if (Host is null)
        {
            return;
        }
        if (paused && Status == AgentPlanStatus.Running)
        {
            if (_pending is not null && _timer is not null)
            {
                _remaining = Max(TimeSpan.Zero, _due - _scheduler.Now);
                _timer.Dispose();
                _timer = null;
            }
            SetStatus(AgentPlanStatus.Paused);
        }
        else if (!paused && Status == AgentPlanStatus.Paused)
        {
            SetStatus(AgentPlanStatus.Running);
            if (_pending is { } action && !_suspended)
            {
                Schedule(_remaining, action);
            }
        }
    }

    /// <summary>Retry runs the failed step again (its lines stay, the retry's lines follow); Skip moves on.</summary>
    internal void OnStepAction(AgentPlanStepActionKind kind, AgentPlanStep step)
    {
        if (Host is null || step.State != AgentPlanStepState.Failed || Status is AgentPlanStatus.Draft or AgentPlanStatus.Done)
        {
            return;
        }
        Cancel();
        if (Status == AgentPlanStatus.Paused)
        {
            SetStatus(AgentPlanStatus.Running);
        }
        if (kind == AgentPlanStepActionKind.Retry)
        {
            step.Error = null;
            step.State = AgentPlanStepState.Active;
            var script = ScriptOf(step);
            Schedule(script.RetryActivities.Count > 0 ? RetryToFirstLine : RetryLastLineToDone,
                () => RetryLine(step, script, 0));
            return;
        }
        step.State = AgentPlanStepState.Skipped;
        Schedule(HasPending() ? SkipToNext : LastDoneToFinish, ActivateNextOrFinish);
    }

    /// <summary>Keep planning: after a short wait the next suggestion streams in before the last step.</summary>
    internal void OnKeepPlanning()
    {
        if (Host is not { } host || Status != AgentPlanStatus.Draft || _planning || !CanKeepPlanning)
        {
            return;
        }
        _planning = true;
        host.SetPlanning(true, CanKeepPlanning);
        // The waiting action finds the plan when it runs: a captured one would stay alive while the timer is held.
        Schedule(PlanningWait, () =>
        {
            _planning = false;
            var plan = Host!;
            var suggestion = Suggestions[_nextSuggestion++];
            var index = Math.Max(0, plan.Steps.Count - 1);
            plan.InsertSuggestion(suggestion.CreateStep(AgentPlanNoteKind.Suggested), index);
            plan.SetPlanning(false, CanKeepPlanning);
        });
    }

    /// <summary>Start over: the draft the simulation started from, every suggestion available again.</summary>
    internal void OnStartOver()
    {
        if (Host is not { } host)
        {
            return;
        }
        Cancel();
        _planning = false;
        _nextSuggestion = 0;
        _failedOnce.Clear();
        _inserted.Clear();
        host.Steps.Clear();
        foreach (var step in _initial)
        {
            host.Steps.Add(step.CloneAsPending());
        }
        SetStatus(AgentPlanStatus.Draft);
        host.SetPlanning(false, CanKeepPlanning);
    }

    // The run ---------------------------------------------------------------------------------------------------------

    private bool HasPending()
    {
        foreach (var step in Host!.Steps)
        {
            if (step.State == AgentPlanStepState.Pending)
            {
                return true;
            }
        }
        return false;
    }

    private void ActivateNextOrFinish()
    {
        if (HasPending())
        {
            ActivateNext();
        }
        else
        {
            Finish();
        }
    }

    private void ActivateNext()
    {
        var host = Host!;
        AgentPlanStep? next = null;
        foreach (var step in host.Steps)
        {
            if (step.State == AgentPlanStepState.Pending)
            {
                next = step;
                break;
            }
        }
        if (next is null)
        {
            Finish();
            return;
        }
        next.State = AgentPlanStepState.Active;
        var script = ScriptOf(next);
        if (script.Activities.Count == 0)
        {
            Schedule(LastLineToDone, () => AfterLastLine(next, script));
            return;
        }
        Schedule(ActiveToFirstLine, () => Line(next, script, 0));
    }

    private void Line(AgentPlanStep step, AgentPlanStepScript script, int index)
    {
        step.Activities.Add(script.Activities[index]);
        if (script.Inserts is { } inserted && script.InsertAfterActivity == index && !_inserted.Contains(inserted.Id))
        {
            Schedule(LineToInsert, () =>
            {
                Insert(step, inserted);
                if (index + 1 < script.Activities.Count)
                {
                    Schedule(InsertToNextLine, () => Line(step, script, index + 1));
                }
                else
                {
                    Schedule(LastLineToDone, () => AfterLastLine(step, script));
                }
            });
            return;
        }
        if (index + 1 < script.Activities.Count)
        {
            Schedule(LineInterval, () => Line(step, script, index + 1));
            return;
        }
        var fails = script.FailureReason is not null && !_failedOnce.Contains(step.Id);
        Schedule(fails ? LastLineToFailure : LastLineToDone, () => AfterLastLine(step, script));
    }

    private void Insert(AgentPlanStep after, AgentPlanScriptedStep inserted)
    {
        var host = Host!;
        _inserted.Add(inserted.Id);
        var at = host.Steps.IndexOf(after);
        var step = inserted.CreateStep(AgentPlanNoteKind.AddedByAgent);
        host.Steps.Insert(at < 0 ? host.Steps.Count : at + 1, step);
    }

    private void AfterLastLine(AgentPlanStep step, AgentPlanStepScript script)
    {
        if (script.FailureReason is { } reason && !_failedOnce.Contains(step.Id))
        {
            _failedOnce.Add(step.Id);
            step.Error = reason;
            step.State = AgentPlanStepState.Failed;
            // Waits for Retry or Skip: no timer.
            return;
        }
        Complete(step, ReferenceEquals(script, s_noScript) ? null : script.Duration);
    }

    private void RetryLine(AgentPlanStep step, AgentPlanStepScript script, int index)
    {
        if (index < script.RetryActivities.Count)
        {
            step.Activities.Add(script.RetryActivities[index]);
            Schedule(index + 1 < script.RetryActivities.Count ? RetryLineInterval : RetryLastLineToDone,
                () => RetryLine(step, script, index + 1));
            return;
        }
        Complete(step, script.RetryDuration ?? script.Duration);
    }

    private void Complete(AgentPlanStep step, TimeSpan? duration)
    {
        step.Duration = duration;
        step.State = AgentPlanStepState.Done;
        Schedule(HasPending() ? DoneToNext : LastDoneToFinish, ActivateNextOrFinish);
    }

    private void Finish()
    {
        _pending = null;
        SetStatus(AgentPlanStatus.Done);
    }

    // Timer -----------------------------------------------------------------------------------------------------------

    private void SetStatus(AgentPlanStatus status)
    {
        Status = status;
        Host?.SetSimulatedStatus(status);
    }

    private void Schedule(TimeSpan delay, Action action)
    {
        _timer?.Dispose();
        _pending = action;
        _due = _scheduler.Now + delay;
        _remaining = delay;
        if (_suspended || Status == AgentPlanStatus.Paused)
        {
            _timer = null;
            return;
        }
        _timer = _scheduler.Schedule(delay, () =>
        {
            _timer = null;
            if (!ReferenceEquals(_pending, action))
            {
                return;
            }
            _pending = null;
            action();
        });
    }

    private void Cancel()
    {
        _timer?.Dispose();
        _timer = null;
        _pending = null;
        _remaining = TimeSpan.Zero;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
