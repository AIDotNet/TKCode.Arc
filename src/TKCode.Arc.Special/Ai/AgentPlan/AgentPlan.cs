using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Metadata;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The plan an agent writes before it acts, then works through as a live checklist (Arc Agent plan, design reference). A
/// header shows the model's mark, "Plan · 6 steps" (the number rolls) and the goal. While <see cref="CurrentStatus"/> is
/// <see cref="AgentPlanStatus.Draft"/> every step can be renamed in place (Enter saves, Escape undoes, Backspace in an empty
/// title removes the step), dragged by its grip or moved with Alt and the arrow keys, removed or added to; Keep planning
/// asks the agent for one more step, which streams in before the last one. Approve plan locks the plan: the grips become
/// status marks, the header counts "3 of 6" over a thin progress line, the current step shimmers beside a spinning arc with
/// its activity lines underneath, a finished step draws its check and settles to secondary text with its time, the agent can
/// insert a step (a faint accent wash), a failed step offers Retry and Skip, the run can be paused, and the footer ends with
/// the total time, a streamed one-line summary and Start over.
/// </summary>
/// <remarks>
/// <para>
/// With no <see cref="Status"/> the plan runs the built-in <see cref="AgentPlanSimulation"/> (an invented sample when there
/// are no <see cref="Steps"/>), as the Arc preview does. With a <see cref="Status"/> the plan is controlled: the control never
/// changes the status or a step's state, activity, error or time; every action is reported through its events
/// (<see cref="Approved"/>, <see cref="Changed"/>, <see cref="StepAction"/>, <see cref="KeepPlanningRequested"/>,
/// <see cref="PauseChanged"/>, <see cref="StartOverRequested"/>) and you update <see cref="Status"/> and the steps. Draft
/// edits (rename, move, remove, add) are made in <see cref="Steps"/> itself and reported through <see cref="Changed"/>.
/// </para>
/// <para>Set a step's <see cref="AgentPlanStep.Error"/> before its state becomes Failed so the announcement can read it.</para>
/// </remarks>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(HeaderPart, typeof(Border))]
[TemplatePart(HeadRowPart, typeof(Panel))]
[TemplatePart(GoalPart, typeof(TextBlock))]
[TemplatePart(ProgressPart, typeof(Control))]
[TemplatePart(ListHostPart, typeof(Panel))]
[TemplatePart(FooterPart, typeof(Border))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(DraftPseudoClass, RunningPseudoClass, PausedPseudoClass, DonePseudoClass, FailedPseudoClass, NarrowPseudoClass, EmptyPseudoClass)]
public partial class AgentPlan : TemplatedControl, IAgentPlanSimulationHost
{
    public const string FramePart = "PART_Frame";
    public const string HeaderPart = "PART_Header";
    public const string HeadRowPart = "PART_HeadRow";
    public const string GoalPart = "PART_Goal";
    public const string ProgressPart = "PART_Progress";
    public const string ListHostPart = "PART_ListHost";
    public const string FooterPart = "PART_Footer";
    public const string AnnouncerPart = "PART_Announcer";

    public const string DraftPseudoClass = ":draft";
    public const string RunningPseudoClass = ":running";
    public const string PausedPseudoClass = ":paused";
    public const string DonePseudoClass = ":done";
    public const string FailedPseudoClass = ":failed";
    public const string NarrowPseudoClass = ":narrow";
    public const string EmptyPseudoClass = ":empty";

    /// <summary>The card is tighter and the draft's buttons share the row below this width.</summary>
    public const double NarrowWidth = 420;

    /// <summary>Defines the <see cref="Steps"/> property.</summary>
    public static readonly StyledProperty<AvaloniaList<AgentPlanStep>> StepsProperty =
        AvaloniaProperty.Register<AgentPlan, AvaloniaList<AgentPlanStep>>(nameof(Steps));

    /// <summary>Defines the <see cref="Status"/> property.</summary>
    public static readonly StyledProperty<AgentPlanStatus?> StatusProperty =
        AvaloniaProperty.Register<AgentPlan, AgentPlanStatus?>(nameof(Status));

    /// <summary>Defines the <see cref="Goal"/> property.</summary>
    public static readonly StyledProperty<string?> GoalProperty =
        AvaloniaProperty.Register<AgentPlan, string?>(nameof(Goal));

    /// <summary>Defines the <see cref="Model"/> property.</summary>
    public static readonly StyledProperty<AgentModel?> ModelProperty =
        AvaloniaProperty.Register<AgentPlan, AgentModel?>(nameof(Model));

    /// <summary>Defines the <see cref="Summary"/> property.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<AgentPlan, string?>(nameof(Summary));

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<AgentPlanLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentPlan, AgentPlanLabels>(nameof(Labels), new AgentPlanLabels());

    /// <summary>Defines the <see cref="Simulation"/> property.</summary>
    public static readonly StyledProperty<AgentPlanSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AgentPlan, AgentPlanSimulation?>(nameof(Simulation));

    /// <summary>Defines the <see cref="IsPlanning"/> property.</summary>
    public static readonly StyledProperty<bool> IsPlanningProperty =
        AvaloniaProperty.Register<AgentPlan, bool>(nameof(IsPlanning));

    /// <summary>Defines the <see cref="CanKeepPlanning"/> property.</summary>
    public static readonly StyledProperty<bool> CanKeepPlanningProperty =
        AvaloniaProperty.Register<AgentPlan, bool>(nameof(CanKeepPlanning), true);

    /// <summary>Defines the <see cref="CurrentStatus"/> property.</summary>
    public static readonly DirectProperty<AgentPlan, AgentPlanStatus> CurrentStatusProperty =
        AvaloniaProperty.RegisterDirect<AgentPlan, AgentPlanStatus>(nameof(CurrentStatus), o => o.CurrentStatus);

    internal static readonly StyledProperty<IBrush?> ForegroundInkProperty = AvaloniaProperty.Register<AgentPlan, IBrush?>("ForegroundInk");
    internal static readonly StyledProperty<IBrush?> SecondaryInkProperty = AvaloniaProperty.Register<AgentPlan, IBrush?>("SecondaryInk");
    internal static readonly StyledProperty<IBrush?> MutedInkProperty = AvaloniaProperty.Register<AgentPlan, IBrush?>("MutedInk");

    /// <summary>Defines the <see cref="Approved"/> event.</summary>
    public static readonly RoutedEvent<AgentPlanApprovedEventArgs> ApprovedEvent =
        RoutedEvent.Register<AgentPlan, AgentPlanApprovedEventArgs>(nameof(Approved), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Changed"/> event.</summary>
    public static readonly RoutedEvent<AgentPlanChangedEventArgs> ChangedEvent =
        RoutedEvent.Register<AgentPlan, AgentPlanChangedEventArgs>(nameof(Changed), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="StepAction"/> event.</summary>
    public static readonly RoutedEvent<AgentPlanStepActionEventArgs> StepActionEvent =
        RoutedEvent.Register<AgentPlan, AgentPlanStepActionEventArgs>(nameof(StepAction), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="KeepPlanningRequested"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> KeepPlanningRequestedEvent =
        RoutedEvent.Register<AgentPlan, RoutedEventArgs>(nameof(KeepPlanningRequested), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="PauseChanged"/> event.</summary>
    public static readonly RoutedEvent<AgentPlanPauseChangedEventArgs> PauseChangedEvent =
        RoutedEvent.Register<AgentPlan, AgentPlanPauseChangedEventArgs>(nameof(PauseChanged), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="StartOverRequested"/> event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StartOverRequestedEvent =
        RoutedEvent.Register<AgentPlan, RoutedEventArgs>(nameof(StartOverRequested), RoutingStrategies.Bubble);

    static AgentPlan()
    {
        FocusableProperty.OverrideDefaultValue<AgentPlan>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<AgentPlan>(AutomationControlType.Group);
    }

    /// <summary>Creates a plan with an empty list of steps.</summary>
    public AgentPlan()
    {
        SetCurrentValue(StepsProperty, new AvaloniaList<AgentPlanStep>());
        UpdatePseudoClasses();
    }

    /// <summary>The steps, in order. Replacing the list replaces the plan on screen; with no steps and no <see cref="Status"/> the invented sample is shown.</summary>
    [Content]
    public AvaloniaList<AgentPlanStep> Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    /// <summary>
    /// Where the plan stands. Null (default) runs the built-in simulation; a value makes the plan controlled: the control never
    /// changes the status or the steps' states and reports every action through its events.
    /// </summary>
    public AgentPlanStatus? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>What the plan is for, under the header and in the plan's accessible name. Null shows the sample's goal while the sample runs.</summary>
    public string? Goal
    {
        get => GetValue(GoalProperty);
        set => SetValue(GoalProperty, value);
    }

    /// <summary>The model that wrote the plan: its mark leads the header and the agent's notes, its name shows in the header while the plan is a draft. Null shows the sample's model while the sample runs, otherwise none.</summary>
    public AgentModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>One line shown after "Done in …" when the plan finishes; it streams in. Null uses the simulation's summary.</summary>
    public string? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    /// <summary>Every word the plan shows or announces.</summary>
    public AgentPlanLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>The script of the built-in simulation (used only while <see cref="Status"/> is null); null runs the invented sample.</summary>
    public AgentPlanSimulation? Simulation
    {
        get => GetValue(SimulationProperty);
        set => SetValue(SimulationProperty, value);
    }

    /// <summary>True while a suggestion requested with Keep planning is on its way: the button shows a spinner. The simulation sets it itself.</summary>
    public bool IsPlanning
    {
        get => GetValue(IsPlanningProperty);
        set => SetValue(IsPlanningProperty, value);
    }

    /// <summary>False disables Keep planning (the agent has nothing more to suggest). The simulation sets it itself.</summary>
    public bool CanKeepPlanning
    {
        get => GetValue(CanKeepPlanningProperty);
        set => SetValue(CanKeepPlanningProperty, value);
    }

    /// <summary>The status shown: <see cref="Status"/>, or the simulation's while <see cref="Status"/> is null.</summary>
    public AgentPlanStatus CurrentStatus => Status ?? _simulatedStatus;

    /// <summary>Done and skipped steps over all steps (0 for an empty plan).</summary>
    public double Progress => AgentPlanFormat.Progress(Steps);

    /// <summary>The sum of the done steps' durations.</summary>
    public TimeSpan TotalDuration => AgentPlanFormat.Total(Steps);

    /// <summary>Raised by Approve plan with the steps in their final order, titles trimmed and empty steps removed.</summary>
    public event EventHandler<AgentPlanApprovedEventArgs>? Approved
    {
        add => AddHandler(ApprovedEvent, value);
        remove => RemoveHandler(ApprovedEvent, value);
    }

    /// <summary>Raised after each draft edit: a rename, a move, a removal, an added step, or a suggestion that finished streaming.</summary>
    public event EventHandler<AgentPlanChangedEventArgs>? Changed
    {
        add => AddHandler(ChangedEvent, value);
        remove => RemoveHandler(ChangedEvent, value);
    }

    /// <summary>Raised when someone retries or skips a failed step.</summary>
    public event EventHandler<AgentPlanStepActionEventArgs>? StepAction
    {
        add => AddHandler(StepActionEvent, value);
        remove => RemoveHandler(StepActionEvent, value);
    }

    /// <summary>Raised by Keep planning: ask the agent for one more step and pass it to <see cref="StreamSuggestion"/>.</summary>
    public event EventHandler<RoutedEventArgs>? KeepPlanningRequested
    {
        add => AddHandler(KeepPlanningRequestedEvent, value);
        remove => RemoveHandler(KeepPlanningRequestedEvent, value);
    }

    /// <summary>Raised by Pause (true) and Resume (false).</summary>
    public event EventHandler<AgentPlanPauseChangedEventArgs>? PauseChanged
    {
        add => AddHandler(PauseChangedEvent, value);
        remove => RemoveHandler(PauseChangedEvent, value);
    }

    /// <summary>Raised by Start over once the plan is done; set <see cref="Status"/> back to Draft and reset the steps.</summary>
    public event EventHandler<RoutedEventArgs>? StartOverRequested
    {
        add => AddHandler(StartOverRequestedEvent, value);
        remove => RemoveHandler(StartOverRequestedEvent, value);
    }

    internal IBrush? ForegroundInk => GetValue(ForegroundInkProperty);

    internal IBrush? SecondaryInk => GetValue(SecondaryInkProperty);

    internal IBrush? MutedInk => GetValue(MutedInkProperty);

    // Actions (the buttons call these; hosts and tests may too) ------------------------------------------------------------

    /// <summary>
    /// Approve plan: saves the field being edited, trims titles, drops empty steps, raises <see cref="Approved"/> and, in
    /// simulation, starts the run. Does nothing unless the plan is a draft with at least one step.
    /// </summary>
    public void Approve()
    {
        if (CurrentStatus != AgentPlanStatus.Draft)
        {
            return;
        }
        foreach (var row in _rows.Values.ToList())
        {
            if (row.TitleBox.IsKeyboardFocusWithin)
            {
                row.Commit(focusLeaving: false);
            }
        }
        _inBatch = true;
        try
        {
            foreach (var step in Steps.ToList())
            {
                var trimmed = step.Title.Trim();
                if (trimmed.Length == 0)
                {
                    Steps.Remove(step);
                }
                else if (trimmed != step.Title)
                {
                    step.Title = trimmed;
                }
            }
        }
        finally
        {
            _inBatch = false;
        }
        if (Steps.Count == 0)
        {
            RefreshAll(animate: false);
            return;
        }
        RaiseEvent(new AgentPlanApprovedEventArgs(ApprovedEvent, Steps.ToList()));
        ActiveSimulation?.OnApproved();
        if (Status is not null)
        {
            // Controlled: the host sets Status; focus follows when it does.
            _hadFocus = true;
        }
    }

    /// <summary>Pause: raises <see cref="PauseChanged"/> (true) and, in simulation, pauses the run.</summary>
    public void Pause() => SetPaused(true);

    /// <summary>Resume: raises <see cref="PauseChanged"/> (false) and, in simulation, continues the run.</summary>
    public void Resume() => SetPaused(false);

    private void SetPaused(bool paused)
    {
        var status = CurrentStatus;
        if ((paused && status != AgentPlanStatus.Running) || (!paused && status != AgentPlanStatus.Paused))
        {
            return;
        }
        _hadFocus = IsKeyboardFocusWithin;
        RaiseEvent(new AgentPlanPauseChangedEventArgs(PauseChangedEvent, paused));
        ActiveSimulation?.OnPauseChanged(paused);
    }

    /// <summary>Retry on a failed step: raises <see cref="StepAction"/> and, in simulation, runs the step again.</summary>
    public void Retry(AgentPlanStep step) => RequestStepAction(AgentPlanStepActionKind.Retry, step);

    /// <summary>Skip on a failed step: raises <see cref="StepAction"/> and, in simulation, moves on.</summary>
    public void Skip(AgentPlanStep step) => RequestStepAction(AgentPlanStepActionKind.Skip, step);

    internal void RequestStepAction(AgentPlanStepActionKind kind, AgentPlanStep step)
    {
        if (step.State != AgentPlanStepState.Failed)
        {
            return;
        }
        _hadFocus = true;
        RaiseEvent(new AgentPlanStepActionEventArgs(StepActionEvent, kind, step));
        ActiveSimulation?.OnStepAction(kind, step);
    }

    /// <summary>Keep planning: raises <see cref="KeepPlanningRequested"/> and, in simulation, streams the next suggestion in.</summary>
    public void KeepPlanning()
    {
        if (CurrentStatus != AgentPlanStatus.Draft || IsPlanning || !CanKeepPlanning)
        {
            return;
        }
        Announce(Labels.AnnouncePlanning);
        RaiseEvent(new RoutedEventArgs(KeepPlanningRequestedEvent));
        ActiveSimulation?.OnKeepPlanning();
    }

    /// <summary>Start over: raises <see cref="StartOverRequested"/> and, in simulation, goes back to the original draft.</summary>
    public void StartOver()
    {
        if (CurrentStatus != AgentPlanStatus.Done)
        {
            return;
        }
        _hadFocus = true;
        RaiseEvent(new RoutedEventArgs(StartOverRequestedEvent));
        ActiveSimulation?.OnStartOver();
    }

    /// <summary>
    /// Inserts a suggested step (before the last one unless <paramref name="index"/> says otherwise), streams its title in
    /// chunks of a few words, then makes it editable and raises <see cref="Changed"/> with
    /// <see cref="AgentPlanChangeKind.SuggestionStreamed"/>. Call it from <see cref="KeepPlanningRequested"/> when the plan is
    /// controlled.
    /// </summary>
    public void StreamSuggestion(AgentPlanStep step, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (step.NoteKind == AgentPlanNoteKind.None)
        {
            step.NoteKind = AgentPlanNoteKind.Suggested;
        }
        _streamStep = step;
        Steps.Insert(Math.Clamp(index ?? Math.Max(0, Steps.Count - 1), 0, Steps.Count), step);
    }

    /// <summary>Focuses the first control that makes sense for the plan's state (the first grip, Pause or Retry, or Start over).</summary>
    public bool FocusEntry()
    {
        ApplyTemplate();
        return FocusForState() || (_rows.Values.FirstOrDefault()?.Grip.Focus() ?? false);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AgentPlanPeer(this);

    private sealed class AgentPlanPeer(AgentPlan owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => (Owner as AgentPlan)?.AccessibleName ?? base.GetNameCore();

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
