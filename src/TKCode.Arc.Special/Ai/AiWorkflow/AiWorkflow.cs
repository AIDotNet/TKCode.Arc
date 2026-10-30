using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A compact automation builder card with an AI step (Arc AI workflow, design reference). Steps are 32px rows on one 1px
/// hairline rail: a trigger, an AI step, a branch whose paths switch with small tabs, and actions. Test run glides a small
/// accent dot down each connector while the line behind it fills; each step crossfades to Running with a shimmering
/// activity line, then shows its duration and an inline output preview in small muted text (the email, the label with a
/// streamed reason, the posted message, the created issue). The path the run does not take is marked Skipped. A failing
/// step shows its error and Retry, which resumes the run from that step. Steps are added from a compact command menu
/// (search, rows with app marks and number shortcuts) and removed with a small button.
/// </summary>
/// <remarks>
/// <para>
/// Set <see cref="Steps"/> to show your own flow (an empty list shows the empty state, which lists the catalog's
/// triggers). With no steps of its own the card plays its <see cref="Simulation"/>, a flow you supply, and with
/// <see cref="AutoPlay"/> test-runs it while it is on screen, saying the run is simulated; with neither it shows the empty
/// state. Adding and removing steps puts a new list in <see cref="Steps"/> (two-way) and raises
/// <see cref="StepsChanged"/>. Set <see cref="RunStep"/> to run each step for real; without it a run is simulated from each
/// step's <see cref="AiWorkflowStep.Output"/> and <see cref="AiWorkflowStep.Duration"/>.
/// </para>
/// </remarks>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(HeaderPart, typeof(Border))]
[TemplatePart(HeaderGridPart, typeof(Grid))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(ActionsPart, typeof(StackPanel))]
[TemplatePart(SimulatedPart, typeof(Border))]
[TemplatePart(RunButtonPart, typeof(Button))]
[TemplatePart(BodyPart, typeof(Border))]
[TemplatePart(RailPart, typeof(AiWorkflowRail))]
[TemplatePart(EmptyPart, typeof(StackPanel))]
[TemplatePart(SkeletonPart, typeof(StackPanel))]
[TemplatePart(FooterPart, typeof(Border))]
[TemplatePart(AddButtonPart, typeof(Button))]
[TemplatePart(SummaryPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(RunningPseudoClass, NarrowPseudoClass, LoadingPseudoClass, EmptyPseudoClass, EditablePseudoClass)]
public partial class AiWorkflow : TemplatedControl, IAiWorkflowRunHost
{
    internal const string FramePart = "PART_Frame";
    internal const string HeaderPart = "PART_Header";
    internal const string HeaderGridPart = "PART_HeaderGrid";
    internal const string TitlePart = "PART_Title";
    internal const string DescriptionPart = "PART_Description";
    internal const string ActionsPart = "PART_Actions";
    internal const string SimulatedPart = "PART_Simulated";
    internal const string RunButtonPart = "PART_RunButton";
    internal const string BodyPart = "PART_Body";
    internal const string RailPart = "PART_Rail";
    internal const string EmptyPart = "PART_Empty";
    internal const string SkeletonPart = "PART_Skeleton";
    internal const string FooterPart = "PART_Footer";
    internal const string AddButtonPart = "PART_AddButton";
    internal const string SummaryPart = "PART_Summary";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string RunningPseudoClass = ":running";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string LoadingPseudoClass = ":loading";
    internal const string EmptyPseudoClass = ":empty";
    internal const string EditablePseudoClass = ":editable";

    /// <summary>The workflow's name in the header. Null shows the simulation's name while its flow is shown.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiWorkflow, string?>(nameof(Title));

    /// <summary>One line of context under the name. Null shows the simulation's line while its flow is shown.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<AiWorkflow, string?>(nameof(Description));

    /// <summary>
    /// The steps, in order (two-way). A branch carries its paths and must be the last top-level step. Null plays the
    /// <see cref="Simulation"/>'s flow, or shows the empty state without one; an empty list shows the empty state. Adding
    /// or removing a step puts a new list here.
    /// </summary>
    public static readonly StyledProperty<IList<AiWorkflowStep>?> StepsProperty =
        AvaloniaProperty.Register<AiWorkflow, IList<AiWorkflowStep>?>(nameof(Steps), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The entries of the add step menu. Triggers are offered in the empty state, everything else in the menu. Null uses
    /// the simulation's catalog while its flow is shown, and no entries otherwise; an empty list hides the menu.
    /// </summary>
    public static readonly StyledProperty<IList<AiWorkflowCatalogItem>?> CatalogProperty =
        AvaloniaProperty.Register<AiWorkflow, IList<AiWorkflowCatalogItem>?>(nameof(Catalog));

    /// <summary>
    /// Runs one step for real and returns its output; throw to fail the step with the message. Null simulates each step
    /// from its <see cref="AiWorkflowStep.Output"/> and <see cref="AiWorkflowStep.Duration"/> (the card then says
    /// "Simulated").
    /// </summary>
    public static readonly StyledProperty<AiWorkflowStepRunner?> RunStepProperty =
        AvaloniaProperty.Register<AiWorkflow, AiWorkflowStepRunner?>(nameof(RunStep));

    /// <summary>Shows three skeleton steps while your flow loads; Test run and the add menu are unavailable.</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<AiWorkflow, bool>(nameof(IsLoading));

    /// <summary>
    /// Plays a test run once the card is on screen and repeats it after a short pause, until someone presses or types inside
    /// the card. Nothing is scheduled while the card is off screen. Keep it off in real products.
    /// </summary>
    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<AiWorkflow, bool>(nameof(AutoPlay), true);

    /// <summary>Shows the add step menu and the remove buttons. Triggers cannot be removed.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<AiWorkflow, bool>(nameof(IsEditable), true);

    /// <summary>
    /// The flow the card shows and test-runs on its own while <see cref="Steps"/> is null: your name, line, steps and
    /// catalog, played exactly like a flow of your own (autoplay, pausing off screen, stopping for good after a press).
    /// Null shows the empty state instead.
    /// </summary>
    public static readonly StyledProperty<AiWorkflowSimulation?> SimulationProperty =
        AvaloniaProperty.Register<AiWorkflow, AiWorkflowSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<AiWorkflowLabels> LabelsProperty =
        AvaloniaProperty.Register<AiWorkflow, AiWorkflowLabels>(nameof(Labels), AiWorkflowLabels.Default);

    /// <summary>True while a test run is in progress.</summary>
    public static readonly DirectProperty<AiWorkflow, bool> IsRunningProperty =
        AvaloniaProperty.RegisterDirect<AiWorkflow, bool>(nameof(IsRunning), o => o.IsRunning);

    /// <summary>Raised after a step is added or removed, with the new list (already in <see cref="Steps"/>). Bubbles.</summary>
    public static readonly RoutedEvent<AiWorkflowStepsChangedEventArgs> StepsChangedEvent =
        RoutedEvent.Register<AiWorkflow, AiWorkflowStepsChangedEventArgs>(nameof(StepsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a test run passes or stops on a failed step (not after Stop). Bubbles.</summary>
    public static readonly RoutedEvent<AiWorkflowRunCompletedEventArgs> RunCompletedEvent =
        RoutedEvent.Register<AiWorkflow, AiWorkflowRunCompletedEventArgs>(nameof(RunCompleted), RoutingStrategies.Bubble);

    private readonly AiWorkflowRunner _runner;
    private bool _isRunning;
    private bool _fromSimulation;
    private bool _committing;

    static AiWorkflow()
    {
        SpecialFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<AiWorkflow>(false);
    }

    /// <summary>Creates the card.</summary>
    public AiWorkflow()
    {
        _runner = new AiWorkflowRunner(this, new AgentPlanDispatcherScheduler());
        AddHandler(PointerPressedEvent, OnAnyPress, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        UpdatePseudoClasses();
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="StepsProperty"/>
    public IList<AiWorkflowStep>? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <inheritdoc cref="CatalogProperty"/>
    public IList<AiWorkflowCatalogItem>? Catalog { get => GetValue(CatalogProperty); set => SetValue(CatalogProperty, value); }

    /// <inheritdoc cref="RunStepProperty"/>
    public AiWorkflowStepRunner? RunStep { get => GetValue(RunStepProperty); set => SetValue(RunStepProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="AutoPlayProperty"/>
    public bool AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public AiWorkflowSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AiWorkflowLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => _isRunning; private set => SetAndRaise(IsRunningProperty, ref _isRunning, value); }

    /// <inheritdoc cref="StepsChangedEvent"/>
    public event EventHandler<AiWorkflowStepsChangedEventArgs>? StepsChanged
    {
        add => AddHandler(StepsChangedEvent, value);
        remove => RemoveHandler(StepsChangedEvent, value);
    }

    /// <inheritdoc cref="RunCompletedEvent"/>
    public event EventHandler<AiWorkflowRunCompletedEventArgs>? RunCompleted
    {
        add => AddHandler(RunCompletedEvent, value);
        remove => RemoveHandler(RunCompletedEvent, value);
    }

    /// <summary>The steps on screen: <see cref="Steps"/>, else the simulation's, else none (the empty state).</summary>
    internal IReadOnlyList<AiWorkflowStep> EffectiveSteps => (Steps ?? Simulation?.Steps) is { } steps
        ? steps as List<AiWorkflowStep> ?? [.. steps]
        : [];

    /// <summary>The add step menu's entries: <see cref="Catalog"/>, else the simulation's while its flow is shown.</summary>
    internal IReadOnlyList<AiWorkflowCatalogItem> EffectiveCatalog =>
        (Catalog ?? (FromSimulation ? Simulation!.Catalog : null)) is { } catalog ? [.. catalog] : [];

    /// <summary>True while the simulation's flow is shown (the app has set no steps).</summary>
    internal bool ShowsSimulation => Steps is null && Simulation is not null;

    /// <summary>
    /// True while the flow on screen is the simulation's, or was made from it by adding or removing steps: its name, line
    /// and catalog still apply.
    /// </summary>
    private bool FromSimulation => Simulation is not null && (Steps is null || _fromSimulation);

    internal string DisplayedTitle => Title ?? (FromSimulation ? Simulation!.Title ?? string.Empty : string.Empty);

    internal string DisplayedDescription => Description ?? (FromSimulation ? Simulation!.Description ?? string.Empty : string.Empty);

    internal AiWorkflowRunner Runner => _runner;

    /// <summary>The timers of runs and autoplay (tests swap in a manual one).</summary>
    internal IAgentPlanScheduler Scheduler
    {
        get => _runner.Scheduler;
        set => _runner.Scheduler = value;
    }

    // Actions ---------------------------------------------------------------------------------------------------------------

    /// <summary>Starts a test run from the first step (a run in progress starts over). Does nothing while loading.</summary>
    public void StartRun()
    {
        if (IsLoading || EffectiveSteps.Count == 0)
        {
            return;
        }
        _autoRun = false;
        _runner.Start();
    }

    /// <summary>Stops the test run in progress; the running step's <see cref="AiWorkflowRunStepContext.Cancellation"/> is cancelled.</summary>
    public void StopRun()
    {
        _autoRun = false;
        _runner.Stop();
    }

    /// <summary>Runs the failed step again and resumes the run from it.</summary>
    public void Retry() => _runner.Retry();

    /// <summary>The status of a step in the current or last run.</summary>
    public AiWorkflowStepStatus GetStepStatus(string stepId) => _runner.StatusOf(stepId);

    /// <summary>The output of a step in the current or last run, or null.</summary>
    public AiWorkflowOutput? GetStepOutput(string stepId) => _runner.Outputs.TryGetValue(stepId, out var output) ? output : null;

    /// <summary>
    /// Adds a step made from <paramref name="item"/>: a trigger goes first, anything else at the end of the flow, or at the
    /// end of the visible path when the flow ends in a branch. Raises <see cref="StepsChanged"/>.
    /// </summary>
    public AiWorkflowStep AddStep(AiWorkflowCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var steps = EffectiveSteps;
        var step = item.ToStep(AiWorkflowFormat.NewId(steps, item.Kind));
        var updated = AiWorkflowFormat.Add(steps, step, SelectedPathId);
        Commit(updated, AiWorkflowChangeKind.Added, step);
        return step;
    }

    /// <summary>Removes the step with <paramref name="stepId"/> (never a trigger, not during a run). Raises <see cref="StepsChanged"/>.</summary>
    public bool RemoveStep(string stepId)
    {
        if (IsRunning)
        {
            return false;
        }
        var steps = EffectiveSteps;
        var step = AiWorkflowFormat.All(steps).FirstOrDefault(s => s.Id == stepId);
        if (step is null || step.Kind == AiWorkflowStepKind.Trigger)
        {
            return false;
        }
        var updated = AiWorkflowFormat.Remove(steps, stepId);
        foreach (var gone in AiWorkflowFormat.All([step]))
        {
            _runner.Forget(gone.Id);
        }
        Commit(updated, AiWorkflowChangeKind.Removed, step);
        return true;
    }

    private void Commit(List<AiWorkflowStep> updated, AiWorkflowChangeKind kind, AiWorkflowStep step)
    {
        _animateNextSync = true;
        _fromSimulation = FromSimulation;
        _committing = true;
        try
        {
            SetCurrentValue(StepsProperty, updated);
        }
        finally
        {
            _committing = false;
        }
        Announce(kind == AiWorkflowChangeKind.Added ? Labels.AnnounceAdded(step.Title) : Labels.AnnounceRemoved(step.Title));
        RaiseEvent(new AiWorkflowStepsChangedEventArgs(StepsChangedEvent, updated, kind, step));
    }

    /// <summary>Opens the add step menu (when the card is editable, not loading, and the catalog has entries for it).</summary>
    public void OpenAddMenu()
    {
        if (!CanOpenMenu || _addButton is null)
        {
            return;
        }
        _menu ??= CreateMenu();
        _menu.Open(_addButton, EffectiveCatalog, AiWorkflowFormat.Branch(EffectiveSteps) is null, Labels);
    }

    /// <summary>Closes the add step menu.</summary>
    public void CloseAddMenu() => _menu?.Close(returnFocus: false);

    /// <summary>Shows the branch path with <paramref name="pathId"/> (as its tab does).</summary>
    public void SelectPath(string pathId) => SelectPathCore(pathId, animate: true);

    // Run host ----------------------------------------------------------------------------------------------------------------

    IReadOnlyList<AiWorkflowStep> IAiWorkflowRunHost.RunSteps => EffectiveSteps;

    AiWorkflowStepRunner? IAiWorkflowRunHost.Runner => RunStep;

    void IAiWorkflowRunHost.OnTravel(AiWorkflowStep step) => _rail?.Travel(step.Id, AiWorkflowRunner.TravelDuration);

    void IAiWorkflowRunHost.OnStepChanged(AiWorkflowStep step)
    {
        if (_rows.TryGetValue(step.Id, out var row))
        {
            if (_rail?.TravelingTo == step.Id && _runner.StatusOf(step.Id) != AiWorkflowStepStatus.Idle)
            {
                _rail.EndTravel();
            }
            ApplyRow(row, animate: true);
        }
        _rail?.InvalidateVisual();
    }

    void IAiWorkflowRunHost.OnRouted(AiWorkflowStep branch, string pathId) => SelectPathCore(pathId, animate: true);

    void IAiWorkflowRunHost.OnRunChanged()
    {
        IsRunning = _runner.IsRunning;
        if (!IsRunning && _rail?.TravelingTo is not null)
        {
            _rail.EndTravel();
        }
        UpdatePseudoClasses();
        RefreshChrome();
        foreach (var row in _rows.Values)
        {
            ApplyRow(row, animate: true);
        }
    }

    void IAiWorkflowRunHost.Announce(string text) => Announce(text);

    void IAiWorkflowRunHost.OnCompleted(AiWorkflowRunStatus status, TimeSpan duration, IReadOnlyDictionary<string, AiWorkflowOutput> outputs,
                                        string? failedStepId)
    {
        RaiseEvent(new AiWorkflowRunCompletedEventArgs(RunCompletedEvent, status, duration, outputs, failedStepId));
        UpdateAutoPlay();
    }

    internal void Announce(string text) => _announcer?.Announce(text);

    // Autoplay ------------------------------------------------------------------------------------------------------------------

    /// <summary>The wait before the first autoplay run once the card is on screen.</summary>
    internal static readonly TimeSpan AutoPlayDelay = TimeSpan.FromMilliseconds(600);

    /// <summary>The pause between autoplay runs.</summary>
    internal static readonly TimeSpan AutoPlayPause = TimeSpan.FromMilliseconds(2800);

    private ViewportVisibility? _viewport;
    private bool _onScreen;
    private bool _interacted;
    private bool _autoRun;
    private IDisposable? _autoTimer;

    /// <summary>True once someone pressed or typed inside the card (autoplay has stopped for good).</summary>
    internal bool HasInteracted => _interacted;

    internal bool IsAutoPlayScheduled => _autoTimer is not null;

    /// <summary>Tells the card whether it is on screen (the viewport watcher calls it; tests may too).</summary>
    internal void SetOnScreen(bool onScreen)
    {
        _onScreen = onScreen;
        UpdateAutoPlay();
    }

    private bool WantsAutoPlay => AutoPlay && !_interacted && !IsLoading && _onScreen && IsAttachedToVisualTree() && EffectiveSteps.Count > 0;

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    private void UpdateAutoPlay()
    {
        if (!WantsAutoPlay)
        {
            _autoTimer?.Dispose();
            _autoTimer = null;
            if (!_onScreen && _autoRun && _runner.IsRunning)
            {
                // Off screen: the autoplay run waits from the start instead of playing unseen.
                _autoRun = false;
                _runner.Reset();
            }
            return;
        }
        if (_runner.IsRunning || _autoTimer is not null)
        {
            return;
        }
        var delay = _runner.HasRun ? AutoPlayPause : AutoPlayDelay;
        _autoTimer = Scheduler.Schedule(delay, () =>
        {
            _autoTimer = null;
            if (WantsAutoPlay && !_runner.IsRunning)
            {
                _autoRun = true;
                _runner.Start();
            }
        });
    }

    private void OnAnyPress(object? sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch && !_touch)
        {
            _touch = true;
            foreach (var row in _rows.Values)
            {
                row.SetTouch(true);
            }
        }
        StopAutoPlay();
    }

    private void OnAnyKey(object? sender, KeyEventArgs e) => StopAutoPlay();

    private void StopAutoPlay()
    {
        if (_interacted)
        {
            return;
        }
        _interacted = true;
        _autoRun = false;
        _autoTimer?.Dispose();
        _autoTimer = null;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AiWorkflowPeer(this);

    private sealed class AiWorkflowPeer(AiWorkflow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
        {
            var owner = (AiWorkflow)Owner;
            return owner.IsLoading ? owner.Labels.Loading : owner.DisplayedTitle;
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}
