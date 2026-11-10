using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// One AI agent run shown live: the goal, a timeline of steps (thinking, tool calls with their input and result, files
/// touched, messages), the elapsed time, the cost and the tokens, and Stop / Retry. Give it your run through
/// <see cref="Goal"/>, <see cref="Steps"/>, <see cref="State"/>, <see cref="Cost"/> and <see cref="Tokens"/>; the clock
/// runs on its own while <see cref="State"/> is Running. Without <see cref="Steps"/> it plays a built-in sample run.
/// Stop and Retry raise <see cref="StopRequested"/> and <see cref="RetryRequested"/> for your backend; Stop also marks the
/// run stopped at once.
/// </summary>
/// <example><code>
/// &lt;arc:AgentRun Goal="{Binding Goal}" Steps="{Binding Steps}" State="{Binding State}" Cost="{Binding Cost}" /&gt;
/// </code></example>
[TemplatePart(StepsPart, typeof(ItemsControl))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(ActionPart, typeof(Button))]
[TemplatePart(ProgressPart, typeof(AgentRunProgressBar))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(RunningPseudoClass, CompletedPseudoClass, StoppedPseudoClass, FailedPseudoClass, IdlePseudoClass, CompactPseudoClass, EmptyPseudoClass)]
public partial class AgentRun : TemplatedControl
{
    public const string StepsPart = "PART_Steps";
    public const string ScrollPart = "PART_Scroll";
    public const string ActionPart = "PART_Action";
    public const string ProgressPart = "PART_Progress";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the run works.</summary>
    public const string RunningPseudoClass = ":running";

    /// <summary>Set once the run finished.</summary>
    public const string CompletedPseudoClass = ":completed";

    /// <summary>Set after the person stopped the run.</summary>
    public const string StoppedPseudoClass = ":stopped";

    /// <summary>Set after the run failed.</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>Set before the run starts.</summary>
    public const string IdlePseudoClass = ":idle";

    /// <summary>Set while the run is at most 480 wide: the facts move under the state line.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the timeline has no step.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>What the agent was asked to do. Default: the sample goal.</summary>
    public static readonly StyledProperty<string?> GoalProperty =
        AvaloniaProperty.Register<AgentRun, string?>(nameof(Goal), AgentRunSamples.Goal);

    /// <summary>The agent's name, shown next to the state. Default: the sample agent.</summary>
    public static readonly StyledProperty<string?> AgentNameProperty =
        AvaloniaProperty.Register<AgentRun, string?>(nameof(AgentName), AgentRunSamples.AgentName);

    /// <summary>
    /// The steps in the order they happened. A collection that raises change notifications (such as an
    /// <see cref="ObservableCollection{T}"/>) is followed as steps arrive; each step's own changes are followed too.
    /// Null (the default) plays the built-in sample run.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<AgentRunStep>?> StepsProperty =
        AvaloniaProperty.Register<AgentRun, IReadOnlyList<AgentRunStep>?>(nameof(Steps));

    /// <summary>Where the run stands (two-way: Stop sets Stopped, and the sample run sets every state).</summary>
    public static readonly StyledProperty<AgentRunState> StateProperty =
        AvaloniaProperty.Register<AgentRun, AgentRunState>(nameof(State), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The time the run has taken (two-way: it grows on its own while <see cref="State"/> is Running).</summary>
    public static readonly StyledProperty<TimeSpan> ElapsedProperty =
        AvaloniaProperty.Register<AgentRun, TimeSpan>(nameof(Elapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What the run has cost so far, in <see cref="CurrencySymbol"/>.</summary>
    public static readonly StyledProperty<decimal> CostProperty =
        AvaloniaProperty.Register<AgentRun, decimal>(nameof(Cost));

    /// <summary>The currency symbol in front of the cost ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<AgentRun, string>(nameof(CurrencySymbol), "$");

    /// <summary>The tokens the run has used so far; zero hides the count.</summary>
    public static readonly StyledProperty<long> TokensProperty =
        AvaloniaProperty.Register<AgentRun, long>(nameof(Tokens));

    /// <summary>Why a failed run failed, shown under the timeline while <see cref="State"/> is Failed.</summary>
    public static readonly StyledProperty<string?> FailureMessageProperty =
        AvaloniaProperty.Register<AgentRun, string?>(nameof(FailureMessage));

    /// <summary>Whether the built-in sample run starts by itself when the control is shown (true).</summary>
    public static readonly StyledProperty<bool> AutoStartProperty =
        AvaloniaProperty.Register<AgentRun, bool>(nameof(AutoStart), true);

    /// <summary>Every word the run shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<AgentRunLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentRun, AgentRunLabels>(nameof(Labels), new AgentRunLabels());

    /// <summary>Whether the built-in sample run is shown (no <see cref="Steps"/> given).</summary>
    public static readonly DirectProperty<AgentRun, bool> IsSimulatedProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, bool>(nameof(IsSimulated), o => o.IsSimulated);

    /// <summary>True while the run is at most 480 wide.</summary>
    public static readonly DirectProperty<AgentRun, bool> IsCompactProperty =
        AvaloniaProperty.RegisterDirect<AgentRun, bool>(nameof(IsCompact), o => o.IsCompact);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentRun, double>(nameof(CompactWidth), 480);

    internal static readonly StyledProperty<TimeSpan> ClockIntervalProperty =
        AvaloniaProperty.Register<AgentRun, TimeSpan>(nameof(ClockInterval), TimeSpan.FromMilliseconds(250));

    /// <summary>Raised when the person presses Stop (before the run is marked stopped). Cancel your agent here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<AgentRun, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Retry or Run again. Start a new run and reset the properties here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RetryRequestedEvent =
        RoutedEvent.Register<AgentRun, RoutedEventArgs>(nameof(RetryRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when a step's detail opens or closes.</summary>
    public static readonly RoutedEvent<AgentRunStepEventArgs> StepToggledEvent =
        RoutedEvent.Register<AgentRun, AgentRunStepEventArgs>(nameof(StepToggled), RoutingStrategies.Bubble);

    /// <inheritdoc cref="GoalProperty"/>
    public string? Goal { get => GetValue(GoalProperty); set => SetValue(GoalProperty, value); }

    /// <inheritdoc cref="AgentNameProperty"/>
    public string? AgentName { get => GetValue(AgentNameProperty); set => SetValue(AgentNameProperty, value); }

    /// <inheritdoc cref="StepsProperty"/>
    public IReadOnlyList<AgentRunStep>? Steps { get => GetValue(StepsProperty); set => SetValue(StepsProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public AgentRunState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="CostProperty"/>
    public decimal Cost { get => GetValue(CostProperty); set => SetValue(CostProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="TokensProperty"/>
    public long Tokens { get => GetValue(TokensProperty); set => SetValue(TokensProperty, value); }

    /// <inheritdoc cref="FailureMessageProperty"/>
    public string? FailureMessage { get => GetValue(FailureMessageProperty); set => SetValue(FailureMessageProperty, value); }

    /// <inheritdoc cref="AutoStartProperty"/>
    public bool AutoStart { get => GetValue(AutoStartProperty); set => SetValue(AutoStartProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentRunLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsSimulatedProperty"/>
    public bool IsSimulated { get => _isSimulated; private set => SetAndRaise(IsSimulatedProperty, ref _isSimulated, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => _isCompact; private set => SetAndRaise(IsCompactProperty, ref _isCompact, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal TimeSpan ClockInterval { get => GetValue(ClockIntervalProperty); set => SetValue(ClockIntervalProperty, value); }

    /// <inheritdoc cref="StopRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? StopRequested
    {
        add => AddHandler(StopRequestedEvent, value);
        remove => RemoveHandler(StopRequestedEvent, value);
    }

    /// <inheritdoc cref="RetryRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? RetryRequested
    {
        add => AddHandler(RetryRequestedEvent, value);
        remove => RemoveHandler(RetryRequestedEvent, value);
    }

    /// <inheritdoc cref="StepToggledEvent"/>
    public event EventHandler<AgentRunStepEventArgs>? StepToggled
    {
        add => AddHandler(StepToggledEvent, value);
        remove => RemoveHandler(StepToggledEvent, value);
    }

    #endregion

    private readonly ObservableCollection<AgentRunStep> _simSteps = new();
    private readonly HashSet<AgentRunStep> _expanded = new();
    private readonly HashSet<AgentRunStep> _seen = new();
    private readonly List<AgentRunStep> _observed = new();
    private INotifyCollectionChanged? _observedCollection;
    private IReadOnlyList<AgentRunStep> _shown;
    private ItemsControl? _stepsHost;
    private ScrollViewer? _scroll;
    private Button? _action;
    private AgentRunProgressBar? _progress;
    private AiAnnouncer? _announcer;
    private bool _isSimulated = true;
    private bool _isCompact;
    private bool _attached;
    private bool _followTail = true;
    private bool _stateFromInside;

    // The clock.
    private DispatcherTimer? _clock;
    private long _runningSince;
    private TimeSpan _elapsedAtStart;

    // The sample run.
    private IReadOnlyList<AgentRunSamples.Beat>? _script;
    private int _beat = -1;
    private IDisposable? _beatTimer;
    private int _generation;

    static AgentRun()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<AgentRun>(AutomationLandmarkType.Region);
    }

    public AgentRun()
    {
        _shown = _simSteps;
        UpdatePseudoClasses();
    }

    /// <summary>
    /// Schedules a one-shot callback: (delay, callback) → a handle that cancels it. Tests replace it to step the sample run;
    /// the default is a <see cref="DispatcherTimer"/>.
    /// </summary>
    internal Func<TimeSpan, Action, IDisposable> Schedule { get; set; } =
        static (delay, callback) => DispatcherTimer.RunOnce(callback, delay);

    /// <summary>A monotonic clock for <see cref="Elapsed"/>; tests replace it.</summary>
    internal Func<TimeSpan> Now { get; set; } = static () => Stopwatch.GetElapsedTime(0);

    /// <summary>The steps on screen (the app's or the sample's).</summary>
    internal IReadOnlyList<AgentRunStep> ShownSteps => _shown;

    internal AiAnnouncer? Announcer => _announcer;

    internal Button? ActionButton => _action;

    internal AgentRunProgressBar? ProgressBar => _progress;

    #region Public actions

    /// <summary>
    /// Stops the run: raises <see cref="StopRequested"/>, then marks the run and its running steps stopped. Does nothing
    /// unless the run is working.
    /// </summary>
    public void Stop()
    {
        if (State != AgentRunState.Running)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        CancelBeat();
        foreach (var step in _shown)
        {
            if (step.Status is AgentRunStepStatus.Running or AgentRunStepStatus.Pending)
            {
                step.Status = AgentRunStepStatus.Stopped;
            }
        }
        SetStateFromInside(AgentRunState.Stopped);
    }

    /// <summary>
    /// Starts again: raises <see cref="RetryRequested"/>; the sample run also clears itself and plays from the start. Does
    /// nothing while the run works.
    /// </summary>
    public void Retry()
    {
        if (State == AgentRunState.Running)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(RetryRequestedEvent, this));
        if (IsSimulated)
        {
            StartSample();
        }
    }

    /// <summary>Opens or closes the detail of <paramref name="step"/>.</summary>
    public void SetStepExpanded(AgentRunStep step, bool expanded)
    {
        if (expanded ? !_expanded.Add(step) : !_expanded.Remove(step))
        {
            return;
        }
        if (FindView(step) is { } view)
        {
            view.IsExpanded = expanded;
        }
        RaiseEvent(new AgentRunStepEventArgs(StepToggledEvent, step, expanded));
    }

    /// <summary>Whether the detail of <paramref name="step"/> is open.</summary>
    public bool IsStepExpanded(AgentRunStep step) => _expanded.Contains(step);

    #endregion

    #region Sample run

    private void StartSample()
    {
        CancelBeat();
        _script ??= AgentRunSamples.Script();
        _simSteps.Clear();
        _expanded.Clear();
        _seen.Clear();
        _followTail = true;
        SetCurrentValue(CostProperty, 0m);
        SetCurrentValue(TokensProperty, 0L);
        SetCurrentValue(ElapsedProperty, TimeSpan.Zero);
        SetCurrentValue(FailureMessageProperty, null);
        _beat = -1;
        SetStateFromInside(AgentRunState.Running);
        NextBeat();
    }

    private void NextBeat()
    {
        _beat++;
        if (_script is null || _beat >= _script.Count)
        {
            SetStateFromInside(AgentRunState.Completed);
            return;
        }
        var beat = _script[_beat];
        var step = beat.Create();
        var output = beat.Output;
        step.Status = AgentRunStepStatus.Running;
        if (step.Kind == AgentRunStepKind.Message)
        {
            _expanded.Add(step);
        }
        _simSteps.Add(step);
        var generation = _generation;
        _beatTimer = Schedule(beat.Duration, () =>
        {
            if (generation != _generation)
            {
                return;
            }
            if (output is not null)
            {
                step.Output = output;
            }
            step.Duration = beat.Duration;
            step.Status = AgentRunStepStatus.Done;
            SetCurrentValue(CostProperty, Cost + beat.Cost);
            SetCurrentValue(TokensProperty, Tokens + beat.Tokens);
            _beatTimer = Schedule(TimeSpan.FromMilliseconds(420), () =>
            {
                if (generation == _generation)
                {
                    NextBeat();
                }
            });
        });
    }

    private void CancelBeat()
    {
        _generation++;
        _beatTimer?.Dispose();
        _beatTimer = null;
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        if (_stepsHost is not null)
        {
            _stepsHost.ItemsSource = null;
        }
        base.OnApplyTemplate(e);
        _stepsHost = e.NameScope.Find<ItemsControl>(StepsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _action = e.NameScope.Find<Button>(ActionPart);
        _progress = e.NameScope.Find<AgentRunProgressBar>(ProgressPart);
        _announcer = e.NameScope.Find<AiAnnouncer>(AnnouncerPart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
        if (_stepsHost is not null)
        {
            _stepsHost.ItemTemplate = new FuncDataTemplate<AgentRunStep>((step, _) => step is null ? null : CreateView(step));
            _stepsHost.ItemsSource = _shown;
        }
        UpdateAll();
    }

    private AgentRunStepView CreateView(AgentRunStep step)
    {
        var view = new AgentRunStepView { Owner = this };
        view.Bind(step, _expanded.Contains(step), isNew: _attached && _seen.Add(step) && IsLastShown(step));
        return view;
    }

    private bool IsLastShown(AgentRunStep step) => _shown.Count > 0 && ReferenceEquals(_shown[^1], step);

    internal AgentRunStepView? FindView(AgentRunStep step)
    {
        var index = IndexOf(step);
        return index < 0 || _stepsHost is null ? null : ViewOf(_stepsHost.ContainerFromIndex(index));
    }

    private static AgentRunStepView? ViewOf(Control? container) =>
        container as AgentRunStepView ?? (container is ContentPresenter { Child: AgentRunStepView view } ? view : null);

    private int IndexOf(AgentRunStep step)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (ReferenceEquals(_shown[i], step))
            {
                return i;
            }
        }
        return -1;
    }

    internal bool IsLast(AgentRunStep step) => IsLastShown(step);

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (State == AgentRunState.Running)
        {
            Stop();
        }
        else if (State == AgentRunState.Idle && IsSimulated)
        {
            StartSample();
        }
        else
        {
            Retry();
        }
    }

    /// <summary>Up / Down move between the step headers; Home / End jump to the first and the last.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not AgentRunStepHeader header || header.Step is not { } step)
        {
            return;
        }
        var index = IndexOf(step);
        var target = e.Key switch
        {
            Key.Down => index + 1,
            Key.Up => index - 1,
            Key.Home => 0,
            Key.End => _shown.Count - 1,
            _ => -1
        };
        if (e.Key is not (Key.Down or Key.Up or Key.Home or Key.End) || target < 0 || target >= _shown.Count || target == index)
        {
            return;
        }
        e.Handled = true;
        FocusStep(target);
    }

    private void FocusStep(int index)
    {
        if (_stepsHost is null)
        {
            return;
        }
        _stepsHost.ScrollIntoView(index);
        _stepsHost.UpdateLayout();
        if (_stepsHost.ContainerFromIndex(index) is { } container && ViewOf(container)?.Header is { } header)
        {
            header.Focus(NavigationMethod.Directional);
        }
    }

    #endregion

    #region Data

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepsProperty)
        {
            var steps = change.GetNewValue<IReadOnlyList<AgentRunStep>?>();
            if (steps is not null)
            {
                CancelBeat();
            }
            IsSimulated = steps is null;
            _shown = steps ?? _simSteps;
            _expanded.Clear();
            _seen.Clear();
            foreach (var step in _shown)
            {
                _seen.Add(step);
                if (step.Kind == AgentRunStepKind.Message)
                {
                    _expanded.Add(step);
                }
            }
            ObserveCollection();
            if (_stepsHost is not null)
            {
                _stepsHost.ItemsSource = _shown;
            }
            UpdateAll();
        }
        else if (change.Property == StateProperty)
        {
            OnStateChanged(change.GetOldValue<AgentRunState>(), change.GetNewValue<AgentRunState>());
        }
        else if (change.Property == LabelsProperty || change.Property == FailureMessageProperty)
        {
            UpdateAll();
            RefreshViews();
        }
        else if (change.Property == ElapsedProperty || change.Property == CostProperty || change.Property == TokensProperty ||
                 change.Property == CurrencySymbolProperty)
        {
            UpdateTemplateTexts();
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    private void SetStateFromInside(AgentRunState state)
    {
        _stateFromInside = true;
        try
        {
            SetCurrentValue(StateProperty, state);
        }
        finally
        {
            _stateFromInside = false;
        }
    }

    private void OnStateChanged(AgentRunState old, AgentRunState state)
    {
        if (!_stateFromInside && IsSimulated && state != AgentRunState.Running)
        {
            // The app took over the sample's state: the sample stops where it is.
            CancelBeat();
        }
        if (state == AgentRunState.Running)
        {
            StartClock();
        }
        else
        {
            StopClock();
        }
        UpdatePseudoClasses();
        UpdateAll();
        if (old == AgentRunState.Running && state is AgentRunState.Completed or AgentRunState.Stopped or AgentRunState.Failed)
        {
            _announcer?.Announce(Labels.RunEnded(Labels.State(state), Labels.Elapsed(Elapsed)));
        }
    }

    private void ObserveCollection()
    {
        if (_observedCollection is not null)
        {
            _observedCollection.CollectionChanged -= OnStepsChanged;
            _observedCollection = null;
        }
        foreach (var step in _observed)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
        }
        _observed.Clear();
        if (!_attached)
        {
            return;
        }
        if (_shown is INotifyCollectionChanged notify)
        {
            _observedCollection = notify;
            notify.CollectionChanged += OnStepsChanged;
        }
        foreach (var step in _shown)
        {
            step.PropertyChanged += OnStepPropertyChanged;
            _observed.Add(step);
        }
    }

    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Re-follow every step: cheap for a run's few dozen steps and always right, whatever the change was.
        foreach (var step in _observed)
        {
            step.PropertyChanged -= OnStepPropertyChanged;
        }
        _observed.Clear();
        foreach (var step in _shown)
        {
            step.PropertyChanged += OnStepPropertyChanged;
            _observed.Add(step);
        }
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is { Count: > 0 } added && added[^1] is AgentRunStep last
            && last.Status == AgentRunStepStatus.Running)
        {
            _announcer?.Announce(Labels.StepStarted(last.Title));
        }
        // The step that was last is no longer last: its rail now continues to the new one.
        RefreshViews();
        UpdateAll();
        if (_followTail && e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(() => _scroll?.ScrollToEnd(), DispatcherPriority.Background);
        }
    }

    private void OnStepPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AgentRunStep step)
        {
            return;
        }
        if (e.Property == AgentRunStep.StatusProperty)
        {
            UpdateAll();
            if (step.Status == AgentRunStepStatus.Running)
            {
                _announcer?.Announce(Labels.StepStarted(step.Title));
            }
        }
        FindView(step)?.Refresh();
    }

    private void RefreshViews()
    {
        foreach (var step in _shown)
        {
            FindView(step)?.Refresh();
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null)
        {
            return;
        }
        // Only a move the person made decides whether the timeline keeps following new steps.
        if (e.OffsetDelta.Y != 0 && e.ExtentDelta.Y == 0)
        {
            _followTail = _scroll.Offset.Y + _scroll.Viewport.Height >= _scroll.Extent.Height - 24;
        }
    }

    #endregion

    #region Clock

    private void StartClock()
    {
        if (!_attached || _clock is not null)
        {
            return;
        }
        _elapsedAtStart = Elapsed;
        _runningSince = Now().Ticks;
        _clock = new DispatcherTimer(ClockInterval, DispatcherPriority.Background, (_, _) => Tick());
        _clock.Start();
    }

    private void StopClock()
    {
        if (_clock is null)
        {
            return;
        }
        Tick();
        _clock.Stop();
        _clock = null;
    }

    /// <summary>Advances <see cref="Elapsed"/> by the real time since the clock started (tests call it directly).</summary>
    internal void Tick()
    {
        if (_clock is null)
        {
            return;
        }
        var elapsed = _elapsedAtStart + TimeSpan.FromTicks(Now().Ticks - _runningSince);
        // Whole seconds only: the text changes once a second and nothing else needs to redraw in between.
        var shown = TimeSpan.FromSeconds(Math.Floor(elapsed.TotalSeconds));
        if (shown != Elapsed)
        {
            SetCurrentValue(ElapsedProperty, shown);
        }
    }

    #endregion

    #region View state

    /// <summary>The text of the action button for the current state; null hides it.</summary>
    internal string? ActionText => State switch
    {
        AgentRunState.Running => Labels.Stop,
        AgentRunState.Completed => Labels.RunAgain,
        AgentRunState.Idle => IsSimulated ? Labels.RunAgain : null,
        _ => Labels.Retry
    };

    /// <summary>The note under the timeline: why a run failed, or that it was stopped.</summary>
    internal string? NoteText => State switch
    {
        AgentRunState.Failed => string.IsNullOrWhiteSpace(FailureMessage) ? Labels.FailedNote : $"{Labels.FailedNote} {FailureMessage}",
        AgentRunState.Stopped => Labels.StoppedNote,
        _ => null
    };

    /// <summary>The progress line ("4 of 6 steps").</summary>
    internal string ProgressText
    {
        get
        {
            var done = 0;
            foreach (var step in _shown)
            {
                if (step.Status == AgentRunStepStatus.Done)
                {
                    done++;
                }
            }
            return Labels.Progress(done, _shown.Count);
        }
    }

    private void UpdateAll()
    {
        PseudoClasses.Set(EmptyPseudoClass, _shown.Count == 0);
        if (_action is not null)
        {
            var text = ActionText;
            _action.IsVisible = text is not null && (IsSimulated || State != AgentRunState.Idle);
            _action.Content = text;
            _action.Variant = State == AgentRunState.Running ? ButtonVariant.Secondary : ButtonVariant.Primary;
            AutomationProperties.SetName(_action, text);
        }
        _progress?.SetSteps(_shown);
        UpdateTemplateTexts();
    }

    private void UpdatePseudoClasses()
    {
        var state = State;
        PseudoClasses.Set(IdlePseudoClass, state == AgentRunState.Idle);
        PseudoClasses.Set(RunningPseudoClass, state == AgentRunState.Running);
        PseudoClasses.Set(CompletedPseudoClass, state == AgentRunState.Completed);
        PseudoClasses.Set(StoppedPseudoClass, state == AgentRunState.Stopped);
        PseudoClasses.Set(FailedPseudoClass, state == AgentRunState.Failed);
    }

    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width <= CompactWidth + 0.01;
        IsCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    #endregion

    #region Lifetime

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveCollection();
        if (State == AgentRunState.Running)
        {
            StartClock();
        }
        if (IsSimulated && AutoStart && State == AgentRunState.Idle && _simSteps.Count == 0)
        {
            StartSample();
        }
        else if (IsSimulated && State == AgentRunState.Running && _beatTimer is null)
        {
            // Shown again while the sample was mid-run: carry on from the next step.
            NextBeat();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        StopClock();
        if (IsSimulated && _beatTimer is not null)
        {
            // The sample pauses with the control; the running step is played again from its start when it returns.
            CancelBeat();
            if (_simSteps.Count > 0 && _simSteps[^1].Status == AgentRunStepStatus.Running)
            {
                _simSteps.RemoveAt(_simSteps.Count - 1);
                _beat--;
            }
        }
        ObserveCollection();
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new AgentRunAutomationPeer(this);

    private sealed class AgentRunAutomationPeer(AgentRun owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() =>
            base.GetNameCore() is { Length: > 0 } name ? name : ((AgentRun)Owner).Labels.Region;
    }
}
