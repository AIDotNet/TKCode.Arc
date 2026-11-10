using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A deep-research run: the question, the plan, the sources being read with their progress, the notes taken from them,
/// and the final report with numbered citations. Give it your run through <see cref="Question"/>, <see cref="Phase"/>,
/// <see cref="Plan"/>, <see cref="Sources"/>, <see cref="Notes"/> and <see cref="Report"/>; the clock runs on its own
/// until the report is ready. Without <see cref="Sources"/> it plays a built-in sample run. Once the report is ready the
/// process folds into one line that opens again on demand. Citations and source rows raise <see cref="SourceInvoked"/>;
/// when nobody handles it the source's address opens.
/// </summary>
/// <example><code>
/// &lt;arc:ResearchRun Question="{Binding Question}" Phase="{Binding Phase}" Plan="{Binding Plan}"
///                  Sources="{Binding Sources}" Notes="{Binding Notes}" Report="{Binding Report}" /&gt;
/// </code></example>
[TemplatePart(StagesPart, typeof(Grid))]
[TemplatePart(PlanPart, typeof(StackPanel))]
[TemplatePart(SourcesPart, typeof(StackPanel))]
[TemplatePart(NotesPart, typeof(StackPanel))]
[TemplatePart(ReportPart, typeof(StackPanel))]
[TemplatePart(ProcessPart, typeof(AiCollapsible))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(ToggleProcessPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(WorkingPseudoClass, DonePseudoClass, StoppedPseudoClass, CompactPseudoClass)]
public partial class ResearchRun : TemplatedControl
{
    public const string StagesPart = "PART_Stages";
    public const string PlanPart = "PART_Plan";
    public const string SourcesPart = "PART_Sources";
    public const string NotesPart = "PART_Notes";
    public const string ReportPart = "PART_Report";
    public const string ProcessPart = "PART_Process";
    public const string ActionPart = "PART_Action";
    public const string ToggleProcessPart = "PART_ToggleProcess";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the run plans, reads or writes.</summary>
    public const string WorkingPseudoClass = ":working";

    /// <summary>Set once the report is ready.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>Set after a stop.</summary>
    public const string StoppedPseudoClass = ":stopped";

    /// <summary>Set while the run is at most 640 wide: the plan and the sources stack.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>The question being researched. Default: the sample question.</summary>
    public static readonly StyledProperty<string?> QuestionProperty =
        AvaloniaProperty.Register<ResearchRun, string?>(nameof(Question), ResearchRunSamples.Question);

    /// <summary>Where the run stands (two-way: Stop sets Stopped, and the sample run sets every phase).</summary>
    public static readonly StyledProperty<ResearchRunPhase> PhaseProperty =
        AvaloniaProperty.Register<ResearchRun, ResearchRunPhase>(nameof(Phase), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The plan's steps in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> PlanProperty =
        AvaloniaProperty.Register<ResearchRun, IReadOnlyList<string>?>(nameof(Plan));

    /// <summary>How many plan steps are done; the next one shows as current while the run works.</summary>
    public static readonly StyledProperty<int> CompletedPlanStepsProperty =
        AvaloniaProperty.Register<ResearchRun, int>(nameof(CompletedPlanSteps));

    /// <summary>
    /// The sources found, numbered by <see cref="AiSource.Index"/>. A collection that raises change notifications is followed
    /// as sources arrive; each source's status and progress are followed too. Null (the default) plays the sample run.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<ResearchRunSource>?> SourcesProperty =
        AvaloniaProperty.Register<ResearchRun, IReadOnlyList<ResearchRunSource>?>(nameof(Sources));

    /// <summary>The notes taken so far, oldest first; a collection that raises change notifications is followed.</summary>
    public static readonly StyledProperty<IReadOnlyList<ResearchRunNote>?> NotesProperty =
        AvaloniaProperty.Register<ResearchRun, IReadOnlyList<ResearchRunNote>?>(nameof(Notes));

    /// <summary>
    /// The report: paragraphs separated by a blank line, citing sources by number in brackets ("[3]"). Set it as it is
    /// written; null or empty shows the report's place.
    /// </summary>
    public static readonly StyledProperty<string?> ReportProperty =
        AvaloniaProperty.Register<ResearchRun, string?>(nameof(Report));

    /// <summary>The time the research has taken (two-way: it grows on its own while the run plans, reads or writes).</summary>
    public static readonly StyledProperty<TimeSpan> ElapsedProperty =
        AvaloniaProperty.Register<ResearchRun, TimeSpan>(nameof(Elapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the plan, sources and notes are open (two-way; they fold when the report is ready).</summary>
    public static readonly StyledProperty<bool> IsProcessExpandedProperty =
        AvaloniaProperty.Register<ResearchRun, bool>(nameof(IsProcessExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the built-in sample run starts by itself when the control is shown (true).</summary>
    public static readonly StyledProperty<bool> AutoStartProperty =
        AvaloniaProperty.Register<ResearchRun, bool>(nameof(AutoStart), true);

    /// <summary>Every word the run shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<ResearchRunLabels> LabelsProperty =
        AvaloniaProperty.Register<ResearchRun, ResearchRunLabels>(nameof(Labels), new ResearchRunLabels());

    /// <summary>Whether the built-in sample run is shown (no <see cref="Sources"/> given).</summary>
    public static readonly DirectProperty<ResearchRun, bool> IsSimulatedProperty =
        AvaloniaProperty.RegisterDirect<ResearchRun, bool>(nameof(IsSimulated), o => o.IsSimulated);

    /// <summary>True while the run is at most 640 wide.</summary>
    public static readonly DirectProperty<ResearchRun, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<ResearchRun, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<ResearchRun, double>(nameof(CompactWidth), 640);

    /// <summary>Raised when the person presses Stop (before the run is marked stopped). Cancel your research here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<ResearchRun, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Research again. Start a new run and reset the properties here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RestartRequestedEvent =
        RoutedEvent.Register<ResearchRun, RoutedEventArgs>(nameof(RestartRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person opens a source from its row or a citation. Mark it handled to stop the address opening.</summary>
    public static readonly RoutedEvent<ResearchRunSourceEventArgs> SourceInvokedEvent =
        RoutedEvent.Register<ResearchRun, ResearchRunSourceEventArgs>(nameof(SourceInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="QuestionProperty"/>
    public string? Question { get => GetValue(QuestionProperty); set => SetValue(QuestionProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public ResearchRunPhase Phase { get => GetValue(PhaseProperty); set => SetValue(PhaseProperty, value); }

    /// <inheritdoc cref="PlanProperty"/>
    public IReadOnlyList<string>? Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }

    /// <inheritdoc cref="CompletedPlanStepsProperty"/>
    public int CompletedPlanSteps { get => GetValue(CompletedPlanStepsProperty); set => SetValue(CompletedPlanStepsProperty, value); }

    /// <inheritdoc cref="SourcesProperty"/>
    public IReadOnlyList<ResearchRunSource>? Sources { get => GetValue(SourcesProperty); set => SetValue(SourcesProperty, value); }

    /// <inheritdoc cref="NotesProperty"/>
    public IReadOnlyList<ResearchRunNote>? Notes { get => GetValue(NotesProperty); set => SetValue(NotesProperty, value); }

    /// <inheritdoc cref="ReportProperty"/>
    public string? Report { get => GetValue(ReportProperty); set => SetValue(ReportProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="IsProcessExpandedProperty"/>
    public bool IsProcessExpanded { get => GetValue(IsProcessExpandedProperty); set => SetValue(IsProcessExpandedProperty, value); }

    /// <inheritdoc cref="AutoStartProperty"/>
    public bool AutoStart { get => GetValue(AutoStartProperty); set => SetValue(AutoStartProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ResearchRunLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool IsSimulated { get => _isSimulated; private set => SetAndRaise(IsSimulatedProperty, ref _isSimulated, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested
    {
        add => AddHandler(StopRequestedEvent, value);
        remove => RemoveHandler(StopRequestedEvent, value);
    }

    /// <inheritdoc cref="RestartRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RestartRequested
    {
        add => AddHandler(RestartRequestedEvent, value);
        remove => RemoveHandler(RestartRequestedEvent, value);
    }

    /// <inheritdoc cref="SourceInvokedEvent"/>
    public event EventHandler<ResearchRunSourceEventArgs>? SourceInvoked
    {
        add => AddHandler(SourceInvokedEvent, value);
        remove => RemoveHandler(SourceInvokedEvent, value);
    }

    #endregion

    private bool _isSimulated = true;
    private bool _isCompact;
    private bool _attached;
    private bool _phaseFromInside;

    static ResearchRun()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<ResearchRun>(AutomationLandmarkType.Region);
    }

    public ResearchRun()
    {
        UpdatePseudoClasses();
    }

    /// <summary>Schedules a one-shot callback; tests replace it to step the sample run.</summary>
    internal Func<TimeSpan, Action, IDisposable> Schedule { get; set; } =
        static (delay, callback) => DispatcherTimer.RunOnce(callback, delay);

    /// <summary>A monotonic clock for <see cref="Elapsed"/>; tests replace it.</summary>
    internal Func<TimeSpan> Now { get; set; } = static () => Stopwatch.GetElapsedTime(0);

    internal static bool IsWorking(ResearchRunPhase phase) =>
        phase is ResearchRunPhase.Planning or ResearchRunPhase.Reading or ResearchRunPhase.Writing;

    #region Public actions

    /// <summary>Stops the research: raises <see cref="StopRequested"/>, then marks the run stopped. Does nothing unless it works.</summary>
    public void Stop()
    {
        if (!IsWorking(Phase))
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        CancelSimulation();
        foreach (var source in ShownSources)
        {
            if (source.Status == ResearchRunSourceStatus.Reading)
            {
                source.Status = ResearchRunSourceStatus.Queued;
            }
        }
        SetPhaseFromInside(ResearchRunPhase.Stopped);
    }

    /// <summary>Starts again: raises <see cref="RestartRequested"/>; the sample run also clears itself and plays from the start.</summary>
    public void Restart()
    {
        if (IsWorking(Phase))
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(RestartRequestedEvent, this));
        if (IsSimulated)
        {
            StartSimulation();
        }
    }

    /// <summary>Opens <paramref name="source"/>: raises <see cref="SourceInvoked"/>, and when nobody handles it opens its address.</summary>
    public void OpenSource(ResearchRunSource source)
    {
        var args = new ResearchRunSourceEventArgs(SourceInvokedEvent, source);
        RaiseEvent(args);
        if (!args.Handled && source.Url is { IsAbsoluteUri: true } url && url.Scheme is "http" or "https"
            && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(url);
        }
    }

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourcesProperty)
        {
            var sources = change.GetNewValue<IReadOnlyList<ResearchRunSource>?>();
            if (sources is not null)
            {
                CancelSimulation();
            }
            IsSimulated = sources is null;
            Observe();
            RebuildAll();
        }
        else if (change.Property == NotesProperty)
        {
            Observe();
            RebuildNotes();
            UpdateTexts();
        }
        else if (change.Property == PlanProperty || change.Property == CompletedPlanStepsProperty)
        {
            RebuildPlan();
        }
        else if (change.Property == ReportProperty)
        {
            RebuildReport();
        }
        else if (change.Property == PhaseProperty)
        {
            OnPhaseChanged(change.GetOldValue<ResearchRunPhase>(), change.GetNewValue<ResearchRunPhase>());
        }
        else if (change.Property == ElapsedProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == IsProcessExpandedProperty)
        {
            UpdateProcess();
        }
        else if (change.Property == LabelsProperty)
        {
            RebuildAll();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    private void SetPhaseFromInside(ResearchRunPhase phase)
    {
        _phaseFromInside = true;
        try
        {
            SetCurrentValue(PhaseProperty, phase);
        }
        finally
        {
            _phaseFromInside = false;
        }
    }

    private void OnPhaseChanged(ResearchRunPhase old, ResearchRunPhase phase)
    {
        if (!_phaseFromInside && IsSimulated && !IsWorking(phase))
        {
            CancelSimulation();
        }
        if (IsWorking(phase))
        {
            StartClock();
        }
        else
        {
            StopClock();
        }
        if (phase == ResearchRunPhase.Done && old != ResearchRunPhase.Done)
        {
            // The finished report takes the stage; the process folds into one line.
            SetCurrentValue(IsProcessExpandedProperty, false);
        }
        else if (IsWorking(phase) && !IsWorking(old))
        {
            SetCurrentValue(IsProcessExpandedProperty, true);
        }
        UpdatePseudoClasses();
        RebuildPlan();
        RebuildReport();
        UpdateTexts();
        if (old != phase)
        {
            _announcer?.Announce(Labels.PhaseAnnouncement(phase));
        }
    }

    private void UpdatePseudoClasses()
    {
        var phase = Phase;
        PseudoClasses.Set(WorkingPseudoClass, IsWorking(phase));
        PseudoClasses.Set(DonePseudoClass, phase == ResearchRunPhase.Done);
        PseudoClasses.Set(StoppedPseudoClass, phase == ResearchRunPhase.Stopped);
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        if (compact == _isCompact)
        {
            return;
        }
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        LayoutProcess();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    #region Following the data

    private INotifyCollectionChanged? _observedSources;
    private INotifyCollectionChanged? _observedNotes;
    private readonly List<ResearchRunSource> _observedSourceItems = new();

    private void Observe()
    {
        if (_observedSources is not null)
        {
            _observedSources.CollectionChanged -= OnSourcesChanged;
            _observedSources = null;
        }
        if (_observedNotes is not null)
        {
            _observedNotes.CollectionChanged -= OnNotesChanged;
            _observedNotes = null;
        }
        foreach (var source in _observedSourceItems)
        {
            source.PropertyChanged -= OnSourcePropertyChanged;
        }
        _observedSourceItems.Clear();
        if (!_attached)
        {
            return;
        }
        if (ShownSources is INotifyCollectionChanged sources)
        {
            _observedSources = sources;
            sources.CollectionChanged += OnSourcesChanged;
        }
        if (ShownNotes is INotifyCollectionChanged notes)
        {
            _observedNotes = notes;
            notes.CollectionChanged += OnNotesChanged;
        }
        foreach (var source in ShownSources)
        {
            source.PropertyChanged += OnSourcePropertyChanged;
            _observedSourceItems.Add(source);
        }
    }

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var source in _observedSourceItems)
        {
            source.PropertyChanged -= OnSourcePropertyChanged;
        }
        _observedSourceItems.Clear();
        foreach (var source in ShownSources)
        {
            source.PropertyChanged += OnSourcePropertyChanged;
            _observedSourceItems.Add(source);
        }
        RebuildSources();
        UpdateTexts();
    }

    private void OnNotesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildNotes();
        UpdateTexts();
    }

    private void OnSourcePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ResearchRunSource source)
        {
            return;
        }
        if (e.Property == ResearchRunSource.StatusProperty || e.Property == ResearchRunSource.ProgressProperty ||
            e.Property == AiSource.TitleProperty || e.Property == AiSource.DomainProperty)
        {
            RowOf(source)?.Refresh();
            if (e.Property == ResearchRunSource.StatusProperty)
            {
                UpdateTexts();
            }
        }
    }

    #endregion

    #region Lifetime

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Observe();
        if (IsWorking(Phase))
        {
            StartClock();
        }
        if (IsSimulated && AutoStart && Phase == ResearchRunPhase.Idle && _simSources.Count == 0)
        {
            StartSimulation();
        }
        else if (IsSimulated && IsWorking(Phase) && _simTimer is null)
        {
            ResumeSimulation();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        StopClock();
        PauseSimulation();
        Observe();
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ResearchRunAutomationPeer(this);

    private sealed class ResearchRunAutomationPeer(ResearchRun owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((ResearchRun)Owner).Labels.Region;
    }
}
