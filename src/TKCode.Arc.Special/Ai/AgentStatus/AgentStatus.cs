using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A compact live status pill for a running agent (Arc Agent status, design reference): the agent's mark inside a thin live
/// ring, the current activity with a matching icon, and an elapsed timer in tabular figures. A new activity rolls in
/// from below while the pill's width follows on a spring, and the label fades at its right edge while it is wider than
/// its room. Pressing the pill opens it in place into a small floating panel with the latest steps (icon, text,
/// duration), a segmented step progress bar and Pause, Stop and Open run; Escape or a press outside closes it and
/// returns focus to the pill. It covers idle (Ready), working, needs input (an accent dot and inline Approve and Deny),
/// paused, done (the ring closes and the mark becomes a drawn check, then it settles back to Ready), failed (with Retry)
/// and stopped.
/// <para>
/// Drive it from your agent's events: set <see cref="Status"/>, keep <see cref="Activity"/> to the latest tool call in the
/// present tense, add <see cref="Steps"/>, give a fixed <see cref="StartedAt"/> (the timer leaves out paused and failed
/// time itself) and handle the action events; <see cref="Actions"/> chooses which buttons are offered, the counterpart of
/// showing a control only when its handler exists. With nothing set, or with a <see cref="Simulation"/>, it plays an
/// invented sample run on its own.
/// </para>
/// </summary>
[TemplatePart(SlotPart, typeof(Border))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(IdlePseudoClass, WorkingPseudoClass, NeedsInputPseudoClass, PausedPseudoClass, DonePseudoClass,
    FailedPseudoClass, StoppedPseudoClass, ExpandedPseudoClass, CompactPseudoClass)]
public sealed partial class AgentStatus : TemplatedControl
{
    internal const string SlotPart = "PART_Slot";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string IdlePseudoClass = ":idle";
    internal const string WorkingPseudoClass = ":working";
    internal const string NeedsInputPseudoClass = ":needs-input";
    internal const string PausedPseudoClass = ":paused";
    internal const string DonePseudoClass = ":done";
    internal const string FailedPseudoClass = ":failed";
    internal const string StoppedPseudoClass = ":stopped";
    internal const string ExpandedPseudoClass = ":expanded";
    internal const string CompactPseudoClass = ":compact";

    /// <summary>Where the run is. Ignored while a <see cref="Simulation"/> drives the pill.</summary>
    public static readonly StyledProperty<AgentStatusRunState> StatusProperty =
        AvaloniaProperty.Register<AgentStatus, AgentStatusRunState>(nameof(Status));

    /// <summary>
    /// What the agent does now, in the present tense. While input is needed it is the request ("Needs your approval to
    /// run a command"), when done it is the outcome that follows "Done in …", and after a failure it is the failure.
    /// </summary>
    public static readonly StyledProperty<AgentStatusActivity?> ActivityProperty =
        AvaloniaProperty.Register<AgentStatus, AgentStatusActivity?>(nameof(Activity));

    /// <summary>When the run started: the timer counts from here and leaves out time spent paused or failed.</summary>
    public static readonly StyledProperty<DateTimeOffset?> StartedAtProperty =
        AvaloniaProperty.Register<AgentStatus, DateTimeOffset?>(nameof(StartedAt));

    /// <summary>When the run ended; by default the moment the status turned done or stopped.</summary>
    public static readonly StyledProperty<DateTimeOffset?> EndedAtProperty =
        AvaloniaProperty.Register<AgentStatus, DateTimeOffset?>(nameof(EndedAt));

    /// <summary>The agent and its mark (drawn at 12px inside the ring); null is a plain "Agent" with a default mark.</summary>
    public static readonly StyledProperty<AgentModel?> AgentProperty =
        AvaloniaProperty.Register<AgentStatus, AgentModel?>(nameof(Agent));

    /// <summary>How long after done or stopped the pill settles back to Ready (4 seconds); null keeps the result on screen.</summary>
    public static readonly StyledProperty<TimeSpan?> SettleAfterProperty =
        AvaloniaProperty.Register<AgentStatus, TimeSpan?>(nameof(SettleAfter), TimeSpan.FromSeconds(4));

    /// <summary>Which edge of the pill the panel grows from.</summary>
    public static readonly StyledProperty<AgentStatusPanelAlignment> PanelAlignmentProperty =
        AvaloniaProperty.Register<AgentStatus, AgentStatusPanelAlignment>(nameof(PanelAlignment), AgentStatusPanelAlignment.Center);

    /// <summary>Whether the panel is open. Two-way: pressing the pill, Escape and a press outside change it.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AgentStatus, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How many of the latest steps the panel lists (5); older ones slide out at the top.</summary>
    public static readonly StyledProperty<int> MaxStepsProperty =
        AvaloniaProperty.Register<AgentStatus, int>(nameof(MaxSteps), 5, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>A one-off message for screen readers, such as the result of an action; it joins the status announcement.</summary>
    public static readonly StyledProperty<string?> NoticeProperty =
        AvaloniaProperty.Register<AgentStatus, string?>(nameof(Notice));

    /// <summary>Which action buttons may show (all by default); a button also needs the right state.</summary>
    public static readonly StyledProperty<AgentStatusActions> ActionsProperty =
        AvaloniaProperty.Register<AgentStatus, AgentStatusActions>(nameof(Actions), AgentStatusActions.All);

    /// <summary>The words and number formats; replace to translate or reword.</summary>
    public static readonly StyledProperty<AgentStatusLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentStatus, AgentStatusLabels>(nameof(Labels), new AgentStatusLabels());

    /// <summary>
    /// A simulated run that drives the pill (its status, activity, steps and times; its buttons are routed back to it).
    /// With no data set at all the pill plays the first sample run on its own.
    /// </summary>
    public static readonly StyledProperty<AgentRunSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AgentStatus, AgentRunSimulation?>(nameof(Simulation));

    /// <summary>The clock for the timer and the settle delay (the system clock by default; tests pass a manual one).</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<AgentStatus, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>The steps list: every step of the run, pending ones included, so the progress bar knows the total.</summary>
    public static readonly DirectProperty<AgentStatus, AvaloniaList<AgentStatusStep>> StepsProperty =
        AvaloniaProperty.RegisterDirect<AgentStatus, AvaloniaList<AgentStatusStep>>(nameof(Steps), o => o._steps);

    /// <summary>Raised when Pause is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> PauseRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(PauseRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Resume is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> ResumeRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(ResumeRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Stop is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> StopRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Approve is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> ApproveRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(ApproveRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Deny is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> DenyRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(DenyRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Retry is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Open run is pressed.</summary>
    public static readonly RoutedEvent<AgentStatusActionEventArgs> OpenRunRequestedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusActionEventArgs>(nameof(OpenRunRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the panel opens or closes.</summary>
    public static readonly RoutedEvent<AgentStatusExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<AgentStatus, AgentStatusExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a finished or stopped run settles back to Ready.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> SettledEvent =
        RoutedEvent.Register<AgentStatus, RoutedEventArgs>(nameof(Settled), RoutingStrategies.Bubble);

    private readonly AvaloniaList<AgentStatusStep> _steps = new();

    static AgentStatus()
    {
        IsTabStopProperty.OverrideDefaultValue<AgentStatus>(false);
        FocusableProperty.OverrideDefaultValue<AgentStatus>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<AgentStatus>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<AgentStatus>(VerticalAlignment.Top);
    }

    /// <inheritdoc cref="StatusProperty"/>
    public AgentStatusRunState Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="ActivityProperty"/>
    public AgentStatusActivity? Activity { get => GetValue(ActivityProperty); set => SetValue(ActivityProperty, value); }

    /// <inheritdoc cref="StepsProperty"/>
    [Content]
    public AvaloniaList<AgentStatusStep> Steps => _steps;

    /// <inheritdoc cref="StartedAtProperty"/>
    public DateTimeOffset? StartedAt { get => GetValue(StartedAtProperty); set => SetValue(StartedAtProperty, value); }

    /// <inheritdoc cref="EndedAtProperty"/>
    public DateTimeOffset? EndedAt { get => GetValue(EndedAtProperty); set => SetValue(EndedAtProperty, value); }

    /// <inheritdoc cref="AgentProperty"/>
    public AgentModel? Agent { get => GetValue(AgentProperty); set => SetValue(AgentProperty, value); }

    /// <inheritdoc cref="SettleAfterProperty"/>
    public TimeSpan? SettleAfter { get => GetValue(SettleAfterProperty); set => SetValue(SettleAfterProperty, value); }

    /// <inheritdoc cref="PanelAlignmentProperty"/>
    public AgentStatusPanelAlignment PanelAlignment { get => GetValue(PanelAlignmentProperty); set => SetValue(PanelAlignmentProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="MaxStepsProperty"/>
    public int MaxSteps { get => GetValue(MaxStepsProperty); set => SetValue(MaxStepsProperty, value); }

    /// <inheritdoc cref="NoticeProperty"/>
    public string? Notice { get => GetValue(NoticeProperty); set => SetValue(NoticeProperty, value); }

    /// <inheritdoc cref="ActionsProperty"/>
    public AgentStatusActions Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentStatusLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AgentRunSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="TimeProviderProperty"/>
    public TimeProvider TimeProvider { get => GetValue(TimeProviderProperty); set => SetValue(TimeProviderProperty, value); }

    /// <inheritdoc cref="PauseRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? PauseRequested { add => AddHandler(PauseRequestedEvent, value); remove => RemoveHandler(PauseRequestedEvent, value); }

    /// <inheritdoc cref="ResumeRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? ResumeRequested { add => AddHandler(ResumeRequestedEvent, value); remove => RemoveHandler(ResumeRequestedEvent, value); }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? StopRequested { add => AddHandler(StopRequestedEvent, value); remove => RemoveHandler(StopRequestedEvent, value); }

    /// <inheritdoc cref="ApproveRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? ApproveRequested { add => AddHandler(ApproveRequestedEvent, value); remove => RemoveHandler(ApproveRequestedEvent, value); }

    /// <inheritdoc cref="DenyRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? DenyRequested { add => AddHandler(DenyRequestedEvent, value); remove => RemoveHandler(DenyRequestedEvent, value); }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? RetryRequested { add => AddHandler(RetryRequestedEvent, value); remove => RemoveHandler(RetryRequestedEvent, value); }

    /// <inheritdoc cref="OpenRunRequestedEvent"/>
    public event EventHandler<AgentStatusActionEventArgs>? OpenRunRequested { add => AddHandler(OpenRunRequestedEvent, value); remove => RemoveHandler(OpenRunRequestedEvent, value); }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<AgentStatusExpandedChangedEventArgs>? ExpandedChanged { add => AddHandler(ExpandedChangedEvent, value); remove => RemoveHandler(ExpandedChangedEvent, value); }

    /// <inheritdoc cref="SettledEvent"/>
    public event EventHandler<RoutedEventArgs>? Settled { add => AddHandler(SettledEvent, value); remove => RemoveHandler(SettledEvent, value); }

    /// <summary>Opens the panel.</summary>
    public void Expand() => IsExpanded = true;

    /// <summary>Closes the panel.</summary>
    public void Collapse() => IsExpanded = false;

    /// <summary>Opens the panel when it is closed and closes it when it is open.</summary>
    public void Toggle() => IsExpanded = !IsExpanded;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == IsExpandedProperty)
        {
            OnExpandedChanged(change.GetNewValue<bool>());
        }
        else if (property == SimulationProperty)
        {
            OnSimulationReplaced(change.GetOldValue<AgentRunSimulation?>(), change.GetNewValue<AgentRunSimulation?>());
        }
        else if (property == ArcMotion.LevelProperty)
        {
            ApplyLevel();
        }
        else if (property == StatusProperty || property == ActivityProperty || property == StartedAtProperty || property == EndedAtProperty
                 || property == AgentProperty || property == SettleAfterProperty || property == MaxStepsProperty
                 || property == NoticeProperty || property == ActionsProperty || property == LabelsProperty || property == TimeProviderProperty
                 || property == PanelAlignmentProperty)
        {
            Refresh();
        }
    }
}
