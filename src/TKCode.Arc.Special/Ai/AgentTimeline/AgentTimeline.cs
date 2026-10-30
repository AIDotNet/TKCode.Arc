using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A compact timeline of one orchestrated multi-agent run: the orchestrator and its subagents share a time axis, one row
/// each (color dot, name, model). Spans show thinking (a tint of the agent's color), tool calls (solid) and waiting
/// (hatched); thin elbow connectors show work handed down to a subagent and results coming back up, with a dot where they
/// meet the upper row. The run replays: a playhead sweeps, the active span grows with a square leading edge and a short
/// shine, connectors draw in as handoffs happen, and the header counts time, tokens and cost until it settles into a
/// summary with the parallel speedup. Hover or focus a span for a card with what it did; click it (or press Enter) to pin
/// it and dim everything off its handoff path. Play, pause, restart, a speed control and scrubbing on the axis drive the
/// replay; live mode follows a run in progress.
/// </summary>
/// <remarks>
/// Given a run it opens finished, ready to replay; with <see cref="AutoPlay"/> it replays the run the first time it is
/// about a fifth in view. With no data it is empty: the default title ("Agent run") over an empty axis, with nothing to
/// play. Under reduced motion it always opens finished; playback still works, without the shine, connector draw-in and
/// card glide. To show a finished run as if it were happening now, feed it through an <see cref="AgentTimelineLiveDriver"/>.
/// </remarks>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(StatusPart, typeof(AgentTimelineStatusLine))]
[TemplatePart(LabelsPart, typeof(AgentTimelineLabelColumn))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(PlotPart, typeof(AgentTimelinePlot))]
[TemplatePart(PlayButtonPart, typeof(AvaloniaButton))]
[TemplatePart(RestartButtonPart, typeof(AvaloniaButton))]
[TemplatePart(SpeedPart, typeof(SegmentedControl))]
[TemplatePart(LegendPart, typeof(Panel))]
[TemplatePart(CardLayerPart, typeof(Canvas))]
[TemplatePart(CardPart, typeof(AgentTimelineCard))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(ReadyPseudoClass, RunningPseudoClass, PausedPseudoClass, FinishedPseudoClass, LivePseudoClass, NarrowPseudoClass,
    ScrollablePseudoClass, ScrolledPseudoClass, PinnedPseudoClass, SingleSpeedPseudoClass)]
public class AgentTimeline : TemplatedControl
{
    /// <summary>The heading part.</summary>
    public const string TitlePart = "PART_Title";

    /// <summary>The status line part.</summary>
    public const string StatusPart = "PART_Status";

    /// <summary>The agent label column part.</summary>
    public const string LabelsPart = "PART_Labels";

    /// <summary>The horizontal scroller around the plot.</summary>
    public const string ScrollerPart = "PART_Scroller";

    /// <summary>The drawn plot part.</summary>
    public const string PlotPart = "PART_Plot";

    /// <summary>The play / pause / replay button part.</summary>
    public const string PlayButtonPart = "PART_PlayButton";

    /// <summary>The restart button part.</summary>
    public const string RestartButtonPart = "PART_RestartButton";

    /// <summary>The speed control part.</summary>
    public const string SpeedPart = "PART_Speed";

    /// <summary>The legend part.</summary>
    public const string LegendPart = "PART_Legend";

    /// <summary>The layer the span card moves in.</summary>
    public const string CardLayerPart = "PART_CardLayer";

    /// <summary>The span card part.</summary>
    public const string CardPart = "PART_HoverCard";

    /// <summary>The polite live region part.</summary>
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set at the start of the run while paused.</summary>
    public const string ReadyPseudoClass = ":ready";

    /// <summary>Set while the replay plays.</summary>
    public const string RunningPseudoClass = ":running";

    /// <summary>Set while paused part way.</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set at the end of the run.</summary>
    public const string FinishedPseudoClass = ":finished";

    /// <summary>Set in live mode (the playback controls hide).</summary>
    public const string LivePseudoClass = ":live";

    /// <summary>Set below a 520 px wide timeline.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set when the plot is wider than its room and scrolls.</summary>
    public const string ScrollablePseudoClass = ":scrollable";

    /// <summary>Set while the plot is scrolled away from its start (the label column shows its divider).</summary>
    public const string ScrolledPseudoClass = ":scrolled";

    /// <summary>Set while a span is pinned.</summary>
    public const string PinnedPseudoClass = ":pinned";

    /// <summary>Set when there is at most one speed to offer (the speed control hides).</summary>
    public const string SingleSpeedPseudoClass = ":single-speed";

    /// <summary>Defines the <see cref="Run"/> property.</summary>
    public static readonly StyledProperty<AgentTimelineRun?> RunProperty =
        AvaloniaProperty.Register<AgentTimeline, AgentTimelineRun?>(nameof(Run));

    /// <summary>Defines the <see cref="Agents"/> property.</summary>
    public static readonly StyledProperty<IList<AgentTimelineAgent>?> AgentsProperty =
        AvaloniaProperty.Register<AgentTimeline, IList<AgentTimelineAgent>?>(nameof(Agents));

    /// <summary>Defines the <see cref="Spans"/> property.</summary>
    public static readonly StyledProperty<IList<AgentTimelineSpan>?> SpansProperty =
        AvaloniaProperty.Register<AgentTimeline, IList<AgentTimelineSpan>?>(nameof(Spans));

    /// <summary>Defines the <see cref="Handoffs"/> property.</summary>
    public static readonly StyledProperty<IList<AgentTimelineHandoff>?> HandoffsProperty =
        AvaloniaProperty.Register<AgentTimeline, IList<AgentTimelineHandoff>?>(nameof(Handoffs));

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AgentTimeline, string?>(nameof(Title));

    /// <summary>Defines the <see cref="AutoPlay"/> property.</summary>
    public static readonly StyledProperty<bool?> AutoPlayProperty =
        AvaloniaProperty.Register<AgentTimeline, bool?>(nameof(AutoPlay));

    /// <summary>Defines the <see cref="Speed"/> property.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AgentTimeline, double>(nameof(Speed), 1.0, coerce: CoerceSpeed, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Speeds"/> property.</summary>
    public static readonly StyledProperty<IList<double>?> SpeedsProperty =
        AvaloniaProperty.Register<AgentTimeline, IList<double>?>(nameof(Speeds));

    /// <summary>Defines the <see cref="IsLive"/> property.</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<AgentTimeline, bool>(nameof(IsLive));

    /// <summary>Defines the <see cref="IsPlaying"/> property.</summary>
    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<AgentTimeline, bool>(nameof(IsPlaying), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="PinnedSpan"/> property.</summary>
    public static readonly StyledProperty<AgentTimelineSpan?> PinnedSpanProperty =
        AvaloniaProperty.Register<AgentTimeline, AgentTimelineSpan?>(nameof(PinnedSpan), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="Labels"/> property.</summary>
    public static readonly StyledProperty<AgentTimelineLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentTimeline, AgentTimelineLabels>(nameof(Labels), new AgentTimelineLabels());

    /// <summary>Defines the <see cref="CurrentTime"/> property.</summary>
    public static readonly DirectProperty<AgentTimeline, double> CurrentTimeProperty =
        AvaloniaProperty.RegisterDirect<AgentTimeline, double>(nameof(CurrentTime), o => o.CurrentTime);

    /// <summary>Defines the <see cref="Duration"/> property.</summary>
    public static readonly DirectProperty<AgentTimeline, double> DurationProperty =
        AvaloniaProperty.RegisterDirect<AgentTimeline, double>(nameof(Duration), o => o.Duration);

    /// <summary>Defines the <see cref="Phase"/> property.</summary>
    public static readonly DirectProperty<AgentTimeline, AgentTimelinePhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<AgentTimeline, AgentTimelinePhase>(nameof(Phase), o => o.Phase);

    /// <summary>Defines the <see cref="Summary"/> property.</summary>
    public static readonly DirectProperty<AgentTimeline, AgentTimelineSummary> SummaryProperty =
        AvaloniaProperty.RegisterDirect<AgentTimeline, AgentTimelineSummary>(nameof(Summary), o => o.Summary);

    /// <summary>Defines the <see cref="ActualTitle"/> property.</summary>
    public static readonly DirectProperty<AgentTimeline, string> ActualTitleProperty =
        AvaloniaProperty.RegisterDirect<AgentTimeline, string>(nameof(ActualTitle), o => o.ActualTitle);

    /// <summary>Defines the <see cref="SpanSelected"/> routed event.</summary>
    public static readonly RoutedEvent<AgentTimelineSpanSelectedEventArgs> SpanSelectedEvent =
        RoutedEvent.Register<AgentTimeline, AgentTimelineSpanSelectedEventArgs>(nameof(SpanSelected), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="Completed"/> routed event.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CompletedEvent =
        RoutedEvent.Register<AgentTimeline, RoutedEventArgs>(nameof(Completed), RoutingStrategies.Bubble);

    /// <summary>Defines the <see cref="PlaybackChanged"/> routed event.</summary>
    public static readonly RoutedEvent<AgentTimelinePlaybackChangedEventArgs> PlaybackChangedEvent =
        RoutedEvent.Register<AgentTimeline, AgentTimelinePlaybackChangedEventArgs>(nameof(PlaybackChanged), RoutingStrategies.Bubble);

    // Ink properties: the theme sets them from the design tokens; the drawn parts read their colors from them.
    internal static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(SurfaceInk));
    internal static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(ForegroundInk));
    internal static readonly StyledProperty<IBrush?> NeutralInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(NeutralInk));
    internal static readonly StyledProperty<IBrush?> MutedInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(MutedInk));
    internal static readonly StyledProperty<IBrush?> BorderInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(BorderInk));
    internal static readonly StyledProperty<IBrush?> GridInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(GridInk));
    internal static readonly StyledProperty<IBrush?> DangerInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(DangerInk));
    internal static readonly StyledProperty<IBrush?> SuccessInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(SuccessInk));
    internal static readonly StyledProperty<IBrush?> FocusInkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(FocusInk));
    internal static readonly StyledProperty<IBrush?> Series1InkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(Series1Ink));
    internal static readonly StyledProperty<IBrush?> Series2InkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(Series2Ink));
    internal static readonly StyledProperty<IBrush?> Series3InkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(Series3Ink));
    internal static readonly StyledProperty<IBrush?> Series4InkProperty =
        AvaloniaProperty.Register<AgentTimeline, IBrush?>(nameof(Series4Ink));

    internal static readonly StyledProperty<double> ThinkingTintProperty =
        AvaloniaProperty.Register<AgentTimeline, double>(nameof(ThinkingTint), 0.24);

    internal static readonly StyledProperty<double> ThinkingHoverTintProperty =
        AvaloniaProperty.Register<AgentTimeline, double>(nameof(ThinkingHoverTint), 0.34);

    private static readonly double[] s_defaultSpeeds = [1, 2];

    private readonly AiPlaybackClock _clock;
    private readonly MotionValue<double> _axisValue;
    private readonly List<INotifyCollectionChanged> _observedLists = new();
    private readonly List<AgentTimelineSpan> _observedSpans = new();
    private AgentTimelineModel _model = AgentTimelineModel.Empty;
    private AgentTimelinePalette? _palette;
    private ViewportVisibility? _visibility;
    private TextBlock? _title;
    private AgentTimelineStatusLine? _status;
    private AgentTimelineLabelColumn? _labels;
    private ScrollViewer? _scroller;
    private AgentTimelinePlot? _plot;
    private AvaloniaButton? _playButton;
    private AvaloniaButton? _restartButton;
    private SegmentedControl? _speed;
    private Panel? _legend;
    private Canvas? _cardLayer;
    private AgentTimelineCard? _card;
    private ExplainerAnnouncer? _announcer;
    private ArcContentSwap? _playGlyph;
    private string? _playGlyphKey;
    private double _currentTime;
    private double _duration;
    private AgentTimelinePhase _phase = AgentTimelinePhase.Finished;
    private AgentTimelineSummary _summary;
    private string _actualTitle = string.Empty;
    private double _liveAxis;
    private double _liveEnd;
    private double _countsWall = double.NegativeInfinity;
    private double _userScrollWall = double.NegativeInfinity;
    private double _followOffset = double.NaN;
    private double _width = double.NaN;
    private int _hovered = -1;
    private int _pinnedIndex = -1;
    private bool _started;
    private bool _userDriven;
    private bool _syncingPlaying;
    private bool _syncingSpeed;
    private bool _paletteDirty = true;
    private bool _narrow;
    private bool _scrollable;
    private bool _attached;

    static AgentTimeline()
    {
        FocusableProperty.OverrideDefaultValue<AgentTimeline>(false);
    }

    /// <summary>Creates a timeline (empty until it is given data).</summary>
    public AgentTimeline()
        : this(null)
    {
    }

    /// <summary>Creates a timeline whose live clock reads <paramref name="time"/> (tests).</summary>
    internal AgentTimeline(TimeProvider? time)
    {
        _clock = new AiPlaybackClock(this, time);
        _clock.Ticked += OnClockTicked;
        _clock.ReachedEnd += OnReachedEnd;
        _axisValue = new MotionValue<double>(this, 1, MotionVectors.Double);
        _axisValue.Changed += value => _plot?.SetAxis(value, _duration);
        RebuildModel();
        UpdatePseudoClasses();
    }

    /// <summary>
    /// The run to show: its agents (rows, the orchestrator first), spans and handoffs. Null with no <see cref="Agents"/> and
    /// <see cref="Spans"/> shows the empty timeline.
    /// </summary>
    public AgentTimelineRun? Run { get => GetValue(RunProperty); set => SetValue(RunProperty, value); }

    /// <summary>Overrides the run's agents. Observed when the list raises collection changes.</summary>
    public IList<AgentTimelineAgent>? Agents { get => GetValue(AgentsProperty); set => SetValue(AgentsProperty, value); }

    /// <summary>Overrides the run's spans. Observed when the list raises collection changes.</summary>
    public IList<AgentTimelineSpan>? Spans { get => GetValue(SpansProperty); set => SetValue(SpansProperty, value); }

    /// <summary>Overrides the run's handoffs. Observed when the list raises collection changes.</summary>
    public IList<AgentTimelineHandoff>? Handoffs { get => GetValue(HandoffsProperty); set => SetValue(HandoffsProperty, value); }

    /// <summary>The heading (also the timeline's accessible name). Null uses the run's title, then "Agent run".</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>
    /// Replays from zero the first time the timeline is about a fifth in view when true. Null or false (the default) opens
    /// the run finished, ready to replay. Reduced motion always opens finished.
    /// </summary>
    public bool? AutoPlay { get => GetValue(AutoPlayProperty); set => SetValue(AutoPlayProperty, value); }

    /// <summary>The playback speed (1 is real time); the speed control writes it.</summary>
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }

    /// <summary>The options of the speed control (default 1x and 2x); with one option or none the control hides.</summary>
    public IList<double>? Speeds { get => GetValue(SpeedsProperty); set => SetValue(SpeedsProperty, value); }

    /// <summary>
    /// Follows a run in progress: the clock runs from the run's <see cref="AgentTimelineRun.StartedAt"/> (or from when this
    /// turns on), open spans grow to now, the axis grows in calm steps and the playback controls hide. Set it to false when
    /// the run ends; the timeline settles on the summary.
    /// </summary>
    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    /// <summary>True while the replay plays; set it to play or pause.</summary>
    public bool IsPlaying { get => GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }

    /// <summary>The pinned span (clicking a span or Enter pins it; again or Escape unpins); null when none is pinned.</summary>
    public AgentTimelineSpan? PinnedSpan { get => GetValue(PinnedSpanProperty); set => SetValue(PinnedSpanProperty, value); }

    /// <summary>Every word the timeline shows or speaks.</summary>
    public AgentTimelineLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <summary>The replay time in seconds (updated about twenty times a second while it plays, at once on a seek).</summary>
    public double CurrentTime { get => _currentTime; private set => SetAndRaise(CurrentTimeProperty, ref _currentTime, value); }

    /// <summary>The run's length in seconds (in a live run, the time so far).</summary>
    public double Duration { get => _duration; private set => SetAndRaise(DurationProperty, ref _duration, value); }

    /// <summary>Ready, Running, Paused, Finished or Live.</summary>
    public AgentTimelinePhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <summary>The run's totals (agents, length, speedup, tokens, cost) as the finished summary shows them.</summary>
    public AgentTimelineSummary Summary { get => _summary; private set => SetAndRaise(SummaryProperty, ref _summary, value); }

    /// <summary>The heading shown: <see cref="Title"/>, else the run's title, else the default.</summary>
    public string ActualTitle { get => _actualTitle; private set => SetAndRaise(ActualTitleProperty, ref _actualTitle, value); }

    /// <summary>Raised when a span is pinned (with the span) or unpinned (with null).</summary>
    public event EventHandler<AgentTimelineSpanSelectedEventArgs>? SpanSelected
    {
        add => AddHandler(SpanSelectedEvent, value);
        remove => RemoveHandler(SpanSelectedEvent, value);
    }

    /// <summary>Raised once each time a replay reaches the end or a live run ends.</summary>
    public event EventHandler<RoutedEventArgs>? Completed
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    /// <summary>Raised when a person plays, pauses, restarts, scrubs or changes the speed.</summary>
    public event EventHandler<AgentTimelinePlaybackChangedEventArgs>? PlaybackChanged
    {
        add => AddHandler(PlaybackChangedEvent, value);
        remove => RemoveHandler(PlaybackChangedEvent, value);
    }

    internal IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }
    internal IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }
    internal IBrush? NeutralInk { get => GetValue(NeutralInkProperty); set => SetValue(NeutralInkProperty, value); }
    internal IBrush? MutedInk { get => GetValue(MutedInkProperty); set => SetValue(MutedInkProperty, value); }
    internal IBrush? BorderInk { get => GetValue(BorderInkProperty); set => SetValue(BorderInkProperty, value); }
    internal IBrush? GridInk { get => GetValue(GridInkProperty); set => SetValue(GridInkProperty, value); }
    internal IBrush? DangerInk { get => GetValue(DangerInkProperty); set => SetValue(DangerInkProperty, value); }
    internal IBrush? SuccessInk { get => GetValue(SuccessInkProperty); set => SetValue(SuccessInkProperty, value); }
    internal IBrush? FocusInk { get => GetValue(FocusInkProperty); set => SetValue(FocusInkProperty, value); }
    internal IBrush? Series1Ink { get => GetValue(Series1InkProperty); set => SetValue(Series1InkProperty, value); }
    internal IBrush? Series2Ink { get => GetValue(Series2InkProperty); set => SetValue(Series2InkProperty, value); }
    internal IBrush? Series3Ink { get => GetValue(Series3InkProperty); set => SetValue(Series3InkProperty, value); }
    internal IBrush? Series4Ink { get => GetValue(Series4InkProperty); set => SetValue(Series4InkProperty, value); }
    internal double ThinkingTint { get => GetValue(ThinkingTintProperty); set => SetValue(ThinkingTintProperty, value); }
    internal double ThinkingHoverTint { get => GetValue(ThinkingHoverTintProperty); set => SetValue(ThinkingHoverTintProperty, value); }

    /// <summary>The story time right now (the clock's, not the 20 Hz <see cref="CurrentTime"/>).</summary>
    internal double Now => _clock.Time;

    /// <summary>True when the timeline has no data (no run, agents or spans): it shows its empty state.</summary>
    internal bool IsEmpty => Run is null && Agents is null && Spans is null;

    internal AgentTimelineModel Model => _model;

    internal AiPlaybackClock Clock => _clock;

    internal AgentTimelinePlot? Plot => _plot;

    internal AgentTimelineCard? Card => _card;

    internal AgentTimelineStatusLine? StatusLine => _status;

    internal AgentTimelineLabelColumn? LabelColumn => _labels;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal ScrollViewer? Scroller => _scroller;

    internal AvaloniaButton? PlayButton => _playButton;

    internal SegmentedControl? SpeedControl => _speed;

    internal double LiveAxis => _liveAxis;

    private static double CoerceSpeed(AvaloniaObject owner, double value)
        => double.IsFinite(value) && value > 0 ? Math.Min(value, 16) : 1;

    // ---- Public actions ----------------------------------------------------------------------------------------------

    /// <summary>Plays from the current time (from zero once finished). Does nothing in live mode.</summary>
    public void Play() => PlayCore(announce: false, raise: false);

    /// <summary>Pauses the replay where it is.</summary>
    public void Pause() => PauseCore(announce: false, raise: false);

    /// <summary>Goes back to zero and plays, keeping the speed.</summary>
    public void Restart() => RestartCore(announce: false, raise: false);

    /// <summary>Moves the replay to <paramref name="seconds"/> (clamped to the run) and pauses there.</summary>
    public void Seek(double seconds)
    {
        if (_clock.IsLive)
        {
            return;
        }
        _started = true;
        _clock.Seek(seconds);
        AfterJump();
    }

    /// <summary>Pins <paramref name="span"/> (null unpins), as clicking it does.</summary>
    public void Pin(AgentTimelineSpan? span) => SetCurrentValue(PinnedSpanProperty, span);

    /// <summary>Removes the pin.</summary>
    public void Unpin() => SetCurrentValue(PinnedSpanProperty, null);

    // ---- Template ----------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayClick;
        }
        if (_restartButton is not null)
        {
            _restartButton.Click -= OnRestartClick;
        }
        if (_speed is not null)
        {
            _speed.SelectionChanged -= OnSpeedSelectionChanged;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_plot is not null)
        {
            _plot.Host = null;
        }

        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _status = e.NameScope.Find<AgentTimelineStatusLine>(StatusPart);
        _labels = e.NameScope.Find<AgentTimelineLabelColumn>(LabelsPart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _plot = e.NameScope.Find<AgentTimelinePlot>(PlotPart);
        _playButton = e.NameScope.Find<AvaloniaButton>(PlayButtonPart);
        _restartButton = e.NameScope.Find<AvaloniaButton>(RestartButtonPart);
        _speed = e.NameScope.Find<SegmentedControl>(SpeedPart);
        _legend = e.NameScope.Find<Panel>(LegendPart);
        _cardLayer = e.NameScope.Find<Canvas>(CardLayerPart);
        _card = e.NameScope.Find<AgentTimelineCard>(CardPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_plot is not null)
        {
            _plot.Host = this;
        }
        if (_playButton is not null)
        {
            _playButton.Click += OnPlayClick;
            _playGlyph = new ArcContentSwap { Mode = ContentSwapMode.Crossfade, HorizontalContentAlignment = HorizontalAlignment.Center };
            _playButton.Content = _playGlyph;
            _playGlyphKey = null;
        }
        if (_restartButton is not null)
        {
            _restartButton.Click += OnRestartClick;
        }
        UpdatePlayable();
        if (_speed is not null)
        {
            _speed.SelectionChanged += OnSpeedSelectionChanged;
        }
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        _paletteDirty = true;
        EnsurePalette();
        ApplyModel();
        UpdateTexts();
        UpdateSpeedControl();
        SyncView(animate: false);
        UpdatePseudoClasses();
        UpdateButtons();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        RebuildModel();
        _visibility?.Dispose();
        _visibility = null;
        if (!_started)
        {
            _visibility = new ViewportVisibility(this, 0.2, once: true);
            _visibility.VisibilityChanged += OnFirstSeen;
        }
        if (IsLive && !_clock.IsLive)
        {
            StartLive();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _visibility?.Dispose();
        _visibility = null;
        Unobserve();
    }

    private void OnFirstSeen(object? sender, bool visible)
    {
        if (!visible || _started)
        {
            return;
        }
        _started = true;
        if (_clock.IsLive || AutoPlay != true || !ArcMotion.IsFull(this) || _model.Duration <= 0)
        {
            return;
        }
        // The first replay starts quietly: the live region keeps its finished sentence until the reader acts.
        _clock.PlayFrom(0);
        AfterJump();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == RunProperty || property == AgentsProperty || property == SpansProperty || property == HandoffsProperty)
        {
            RebuildModel();
        }
        else if (property == TitleProperty || property == LabelsProperty)
        {
            UpdateTexts();
            UpdateCounters(force: true);
            UpdateSummary();
            _plot?.UpdateProxies();
            UpdateButtons();
            UpdateSpeedControl();
            BuildLegend();
        }
        else if (property == SpeedProperty)
        {
            _clock.Speed = Speed;
            UpdateSpeedControl();
        }
        else if (property == SpeedsProperty)
        {
            UpdateSpeedControl();
        }
        else if (property == IsLiveProperty)
        {
            if (IsLive)
            {
                StartLive();
            }
            else
            {
                StopLive();
            }
        }
        else if (property == IsPlayingProperty)
        {
            if (!_syncingPlaying)
            {
                if (IsPlaying)
                {
                    PlayCore(announce: false, raise: false);
                }
                else
                {
                    PauseCore(announce: false, raise: false);
                }
            }
        }
        else if (property == PinnedSpanProperty)
        {
            OnPinnedChanged(change.GetOldValue<AgentTimelineSpan?>(), change.GetNewValue<AgentTimelineSpan?>());
        }
        else if (property == AutoPlayProperty)
        {
            // Only the first sight reads it.
        }
        else if (property == ArcMotion.LevelProperty)
        {
            SyncView(animate: false);
        }
        else if (property.Name.EndsWith("Ink", StringComparison.Ordinal) || property == ThinkingTintProperty || property == ThinkingHoverTintProperty)
        {
            _paletteDirty = true;
            InvalidateArrange();
        }
    }

    // ---- Data --------------------------------------------------------------------------------------------------------

    private void RebuildModel()
    {
        Unobserve();
        var run = Run;
        IList<AgentTimelineAgent> agents = Agents ?? (IList<AgentTimelineAgent>?)run?.Agents ?? [];
        IList<AgentTimelineSpan> spans = Spans ?? (IList<AgentTimelineSpan>?)run?.Spans ?? [];
        IList<AgentTimelineHandoff> handoffs = Handoffs ?? (IList<AgentTimelineHandoff>?)run?.Handoffs ?? [];
        if (_attached)
        {
            Observe(agents);
            Observe(spans);
            Observe(handoffs);
            foreach (var span in spans)
            {
                if (span is not null)
                {
                    span.PropertyChanged += OnSpanPropertyChanged;
                    _observedSpans.Add(span);
                }
            }
        }
        _model = AgentTimelineModel.Build(agents.ToArray(), spans.ToArray(), handoffs.ToArray(), run?.Duration);
        if (!_clock.IsLive)
        {
            var wasAtEnd = _clock.IsAtEnd || !_started;
            _clock.Duration = _model.Duration;
            if (wasAtEnd)
            {
                _clock.Seek(_model.Duration);
            }
            _axisValue.Jump(Math.Max(_model.Duration, 1e-3));
        }
        Duration = _clock.IsLive ? Math.Max(_clock.Time, _model.DataEnd) : _model.Duration;
        ApplyModel();
        UpdateTexts();
        UpdatePlayable();
        SyncView(animate: false);
    }

    /// <summary>Without agents there is nothing to replay: play and restart are disabled until data arrives.</summary>
    private void UpdatePlayable()
    {
        var playable = _model.Rows.Length > 0 || _clock.IsLive;
        if (_playButton is not null)
        {
            _playButton.IsEnabled = playable;
        }
        if (_restartButton is not null)
        {
            _restartButton.IsEnabled = playable;
        }
    }

    private void Observe<T>(IList<T> list)
    {
        if (list is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnDataCollectionChanged;
            _observedLists.Add(observable);
        }
    }

    private void Unobserve()
    {
        foreach (var list in _observedLists)
        {
            list.CollectionChanged -= OnDataCollectionChanged;
        }
        _observedLists.Clear();
        foreach (var span in _observedSpans)
        {
            span.PropertyChanged -= OnSpanPropertyChanged;
        }
        _observedSpans.Clear();
    }

    private void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildModel();

    private void OnSpanPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => RebuildModel();

    /// <summary>Hands the model to the parts and works out the totals.</summary>
    private void ApplyModel()
    {
        var pinned = PinnedSpan;
        _pinnedIndex = _model.IndexOf(pinned);
        if (_hovered >= _model.Spans.Length)
        {
            _hovered = -1;
        }
        if (_plot is not null)
        {
            var axis = _clock.IsLive ? Math.Max(_liveAxis, 1e-3) : Math.Max(_model.Duration, 1e-3);
            _plot.SetModel(_model);
            _plot.SetAxis(axis, _clock.IsLive ? _clock.Time : _model.Duration);
            _plot.SetPinned(_pinnedIndex, animate: false);
            _plot.SetHovered(_hovered, animate: false);
        }
        _labels?.SetModel(_model, row => _plot?.RowPalette(row)?.Solid);
        UpdateSummary();
        InvalidateMeasure();
    }

    private double OpenEnd => _clock.IsLive ? _clock.Time : Math.Max(_clock.Duration, _model.DataEnd);

    private void UpdateSummary()
    {
        var length = _clock.IsLive ? Math.Max(_clock.Time, _model.DataEnd) : _model.Duration;
        Summary = _model.Summary(length, OpenEnd);
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        var s = Summary;
        var agents = labels.Agents(s.AgentCount);
        var time = labels.Time(s.Duration);
        var speedup = labels.Speedup(s.Speedup);
        var cost = labels.Cost(s.Cost);
        _status.SetSummary(agents, time, speedup, cost, $"{labels.FinishedPrefix} {agents}, {time}, {speedup}, {cost}");
        if (_clock.IsLive)
        {
            _status.SetFinals(string.Empty, string.Empty, string.Empty);
        }
        else
        {
            _status.SetFinals(labels.Time(_model.Duration), AgentTimelineMath.Tokens(s.Tokens), labels.Cost(s.Cost));
        }
    }

    private void UpdateTexts()
    {
        var run = Run;
        ActualTitle = Title ?? run?.Title ?? Labels.DefaultTitle;
        if (_title is not null)
        {
            _title.Text = ActualTitle;
        }
        AutomationProperties.SetName(this, ActualTitle);
        _status?.SetWords(Labels);
        if (_plot is not null)
        {
            AutomationProperties.SetName(_plot, Labels.Spans);
        }
        if (_plot?.Scrubber is { } scrubber)
        {
            AutomationProperties.SetName(scrubber, Labels.RunTime);
        }
        if (_restartButton is not null)
        {
            AutomationProperties.SetName(_restartButton, Labels.Restart);
        }
        if (_speed is not null)
        {
            AutomationProperties.SetName(_speed, Labels.PlaybackSpeed);
        }
        if (_legend is not null)
        {
            AutomationProperties.SetName(_legend, Labels.Legend);
        }
        UpdateScrubberText();
    }

    // ---- Palette -----------------------------------------------------------------------------------------------------

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        static Color C(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;
        var neutrals = ArcPalette.Neutrals(false);
        _palette = new AgentTimelinePalette(
            C(SurfaceInk, neutrals.Surface), C(ForegroundInk, neutrals.Foreground), C(NeutralInk, neutrals.TextSecondary),
            C(MutedInk, neutrals.TextMuted), C(BorderInk, neutrals.Border), C(GridInk, neutrals.BorderSubtle),
            C(DangerInk, neutrals.Danger), C(SuccessInk, neutrals.Success), C(FocusInk, Color.FromArgb(184, 11, 11, 11)),
            [
                C(Series1Ink, Color.Parse("#1F7DCF")), C(Series2Ink, Color.Parse("#7128A5")),
                C(Series3Ink, Color.Parse("#E58F00")), C(Series4Ink, Color.Parse("#009E84"))
            ],
            ThinkingTint, ThinkingHoverTint);
        _plot?.SetPalette(_palette);
        _labels?.SetModel(_model, row => _plot?.RowPalette(row)?.Solid);
        BuildLegend();
        UpdateCard();
    }

    private void BuildLegend()
    {
        if (_legend is null || _palette is not { } palette)
        {
            return;
        }
        var labels = Labels;
        _legend.Children.Clear();
        _legend.Children.Add(LegendItem(new AgentTimelineLegendSwatch(AgentTimelineSpanKind.Thinking, palette), labels.Thinking, palette));
        _legend.Children.Add(LegendItem(new AgentTimelineLegendSwatch(AgentTimelineSpanKind.Tool, palette), labels.ToolCall, palette));
        _legend.Children.Add(LegendItem(new AgentTimelineLegendSwatch(AgentTimelineSpanKind.Waiting, palette), labels.Waiting, palette));
        _legend.Children.Add(LegendItem(new ArcGlyph { Data = ArcIcons.CornerDownRight, Size = 13, Foreground = palette.MutedBrush }, labels.Handoff, palette));
        _legend.Children.Add(LegendItem(new ArcGlyph { Data = ArcIcons.CircleX, Size = 13, Foreground = palette.DangerBrush }, labels.Failed, palette));
    }

    private static Control LegendItem(Control mark, string text, AgentTimelinePalette palette)
    {
        mark.VerticalAlignment = VerticalAlignment.Center;
        var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        item.Children.Add(mark);
        item.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 12,
            LineHeight = 16.8,
            Foreground = palette.NeutralBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        return item;
    }

    // ---- Layout ------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (!double.IsFinite(width))
        {
            width = double.IsFinite(MaxWidth) ? MaxWidth : 744;
        }
        ApplyWidth(Math.Min(width, MaxWidth));
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsurePalette();
        if (Math.Abs(finalSize.Width - _width) > 0.5)
        {
            ApplyWidth(finalSize.Width);
        }
        var result = base.ArrangeOverride(finalSize);
        UpdateScrollState();
        return result;
    }

    private void ApplyWidth(double width)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            return;
        }
        _width = width;
        var narrow = width < AgentTimelineLayout.NarrowWidth;
        var inner = width - BorderThickness.Left - BorderThickness.Right;
        var available = AgentTimelineLayout.AvailablePlotWidth(inner, narrow);
        var scrollable = available < AgentTimelineLayout.MinPlotWidth - 0.5;
        if (narrow != _narrow)
        {
            _narrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
        }
        if (_labels is not null)
        {
            _labels.IsNarrow = narrow;
        }
        if (scrollable != _scrollable)
        {
            _scrollable = scrollable;
            PseudoClasses.Set(ScrollablePseudoClass, scrollable);
        }
        _plot?.SetPlotWidth(AgentTimelineLayout.PlotWidth(inner, narrow));
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroller is null)
        {
            return;
        }
        var offset = _scroller.Offset.X;
        if (double.IsNaN(_followOffset) || Math.Abs(offset - _followOffset) > 0.5)
        {
            // The person scrolled: stop following the playhead for a while.
            _userScrollWall = _clock.WallTime;
        }
        _followOffset = double.NaN;
        UpdateScrollState();
        if (_card is { IsShown: true })
        {
            UpdateCard();
        }
    }

    private void UpdateScrollState() => PseudoClasses.Set(ScrolledPseudoClass, _scroller is { } s && s.Offset.X > 0.5);

    /// <summary>While a scrolled replay plays, keeps the playhead 80 px inside the right edge, unless the person just scrolled.</summary>
    private void Follow()
    {
        if (!_scrollable || _scroller is not { } scroller || _plot is not { } plot || !(_clock.IsPlaying || _clock.IsLive))
        {
            return;
        }
        if (_clock.WallTime - _userScrollWall < AgentTimelineLayout.FollowCooldown)
        {
            return;
        }
        var x = AgentTimelineLayout.TrackLeftPadding + _clock.Time * plot.Scale;
        var viewport = scroller.Viewport.Width;
        var max = Math.Max(0, scroller.Extent.Width - viewport);
        var wanted = Math.Clamp(x - (viewport - AgentTimelineLayout.FollowMargin), 0, max);
        if (wanted > scroller.Offset.X + 0.5 || (wanted < scroller.Offset.X - 0.5 && x < scroller.Offset.X))
        {
            _followOffset = wanted;
            scroller.Offset = new Vector(wanted, scroller.Offset.Y);
        }
    }

    // ---- The clock ---------------------------------------------------------------------------------------------------

    private void OnClockTicked(double dt)
    {
        if (_clock.IsLive)
        {
            UpdateLiveAxis(animate: true);
        }
        var full = ArcMotion.IsFull(this);
        _plot?.Advance(_clock.Time, _clock.WallTime, OpenEnd, _clock.IsLive, full, animate: _clock.IsPlaying || _clock.IsLive);
        if (_clock.WallTime - _countsWall >= 0.05)
        {
            UpdateCounters(force: false);
        }
        UpdatePhase(animate: full);
        Follow();
        if (_card is { IsShown: true } card && card.Span >= 0 && !_model.IsMounted(card.Span, _clock.Time))
        {
            UpdateCard();
        }
        UpdateFrames();
    }

    private void UpdateFrames()
    {
        if (_plot is { } plot)
        {
            _clock.KeepsRunning = plot.HasShine || plot.IsAnimating();
        }
    }

    internal void RequestFrames(double seconds) => _clock.RequestFrames(seconds);

    private void OnReachedEnd()
    {
        SyncView(animate: ArcMotion.IsFull(this));
        RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
        if (_userDriven)
        {
            _announcer?.Announce(Labels.RunComplete(Summary));
        }
    }

    /// <summary>After a seek or a restart: everything jumps to the new time (no transitions), then the state updates.</summary>
    private void AfterJump()
    {
        _plot?.Advance(_clock.Time, _clock.WallTime, OpenEnd, _clock.IsLive, ArcMotion.IsFull(this), animate: false);
        SyncView(animate: ArcMotion.IsFull(this));
    }

    private void SyncView(bool animate)
    {
        UpdateCounters(force: true);
        UpdatePhase(animate);
        UpdateCard();
        UpdateFrames();
    }

    private void UpdateCounters(bool force)
    {
        _countsWall = _clock.WallTime;
        var t = _clock.Time;
        CurrentTime = t;
        if (_clock.IsLive)
        {
            Duration = Math.Max(t, _model.DataEnd);
        }
        UpdateScrubberText();
        if (_status is null)
        {
            return;
        }
        var labels = Labels;
        var counts = _model.CountsAt(t, OpenEnd);
        var time = labels.Time(t);
        var tokens = AgentTimelineMath.Tokens(counts.Tokens);
        var cost = labels.Cost(counts.Cost);
        var word = Phase switch
        {
            AgentTimelinePhase.Running => labels.Running,
            AgentTimelinePhase.Paused => labels.Paused,
            AgentTimelinePhase.Live => labels.Live,
            _ => labels.Ready
        };
        var name = $"{word}, {time}, {tokens} {labels.TokensUnit}, {cost}" + (counts.Working > 0 ? labels.AgentsWorking(counts.Working) : string.Empty);
        _status.SetCounters(time, tokens, cost, name);
        _ = force;
    }

    private void UpdateScrubberText()
    {
        if (_plot?.Scrubber is { } scrubber)
        {
            AutomationProperties.SetHelpText(scrubber, Labels.ScrubberValue(_clock.Time, _clock.IsLive ? Duration : _model.Duration));
        }
    }

    private void UpdatePhase(bool animate)
    {
        var phase = _clock.IsLive ? AgentTimelinePhase.Live
            : _clock.IsAtEnd ? AgentTimelinePhase.Finished
            : _clock.IsPlaying ? AgentTimelinePhase.Running
            : _clock.Time <= 1e-9 ? AgentTimelinePhase.Ready
            : AgentTimelinePhase.Paused;
        var changed = phase != Phase;
        Phase = phase;
        if (_clock.IsPlaying != IsPlaying)
        {
            _syncingPlaying = true;
            try
            {
                SetCurrentValue(IsPlayingProperty, _clock.IsPlaying);
            }
            finally
            {
                _syncingPlaying = false;
            }
        }
        _status?.SetPhase(phase, animate && changed);
        _plot?.SetPlayheadVisible(phase != AgentTimelinePhase.Finished, animate && changed);
        _plot?.SetKnobVisible(phase != AgentTimelinePhase.Live);
        if (changed)
        {
            UpdatePseudoClasses();
            UpdateButtons();
            if (phase == AgentTimelinePhase.Finished && _status is not null)
            {
                UpdateCounters(force: true);
            }
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(ReadyPseudoClass, Phase == AgentTimelinePhase.Ready);
        PseudoClasses.Set(RunningPseudoClass, Phase == AgentTimelinePhase.Running);
        PseudoClasses.Set(PausedPseudoClass, Phase == AgentTimelinePhase.Paused);
        PseudoClasses.Set(FinishedPseudoClass, Phase == AgentTimelinePhase.Finished);
        PseudoClasses.Set(LivePseudoClass, Phase == AgentTimelinePhase.Live);
        PseudoClasses.Set(PinnedPseudoClass, _pinnedIndex >= 0);
    }

    private void UpdateButtons()
    {
        var labels = Labels;
        if (_playButton is null || _playGlyph is null)
        {
            return;
        }
        var (key, name) = Phase == AgentTimelinePhase.Running
            ? ("pause", labels.Pause)
            : Phase == AgentTimelinePhase.Finished ? ("replay", labels.Replay) : ("play", labels.Play);
        if (key != _playGlyphKey)
        {
            var first = _playGlyphKey is null;
            _playGlyphKey = key;
            var glyph = new AgentTimelineSolidGlyph(key == "pause" ? ArcIcons.Pause : ArcIcons.Play);
            if (first || !ArcMotion.IsFull(this))
            {
                var mode = _playGlyph.Mode;
                _playGlyph.Mode = ContentSwapMode.None;
                _playGlyph.Content = glyph;
                _playGlyph.Mode = mode;
            }
            else
            {
                _playGlyph.Content = glyph;
            }
        }
        AutomationProperties.SetName(_playButton, name);
    }

    // ---- Playback actions --------------------------------------------------------------------------------------------

    private void PlayCore(bool announce, bool raise)
    {
        if (_clock.IsLive)
        {
            return;
        }
        _started = true;
        if (_clock.IsAtEnd)
        {
            RestartCore(announce, raise);
            return;
        }
        _clock.Play();
        AfterJump();
        if (announce)
        {
            _announcer?.Announce(Labels.PlayingAt(Speed));
        }
        if (raise)
        {
            RaisePlayback(AgentTimelinePlaybackAction.Play);
        }
    }

    private void PauseCore(bool announce, bool raise)
    {
        if (_clock.IsLive)
        {
            return;
        }
        _started = true;
        _clock.Pause();
        SyncView(animate: ArcMotion.IsFull(this));
        if (announce)
        {
            _announcer?.Announce(Labels.PausedAt(_clock.Time));
        }
        if (raise)
        {
            RaisePlayback(AgentTimelinePlaybackAction.Pause);
        }
    }

    private void RestartCore(bool announce, bool raise)
    {
        if (_clock.IsLive)
        {
            return;
        }
        _started = true;
        _clock.PlayFrom(0);
        AfterJump();
        if (announce)
        {
            _announcer?.Announce(Labels.PlayingAt(Speed));
        }
        if (raise)
        {
            RaisePlayback(AgentTimelinePlaybackAction.Restart);
        }
    }

    /// <summary>The scrubber moved the replay (pointer or keys): seek there and pause; keys also announce the new position.</summary>
    internal void ScrubTo(double seconds, AgentTimelinePlaybackAction action, bool announce)
    {
        if (_clock.IsLive)
        {
            return;
        }
        _started = true;
        _userDriven = true;
        _clock.Seek(seconds);
        AfterJump();
        if (announce)
        {
            AnnouncePosition();
        }
        RaisePlayback(action);
    }

    /// <summary>Speaks where the replay stands: the summary at the end, else the paused time.</summary>
    internal void AnnouncePosition()
    {
        _announcer?.Announce(_clock.IsAtEnd ? Labels.RunComplete(Summary) : Labels.PausedAt(_clock.Time));
    }

    private void RaisePlayback(AgentTimelinePlaybackAction action)
        => RaiseEvent(new AgentTimelinePlaybackChangedEventArgs(PlaybackChangedEvent, action, _clock.Time, Speed));

    private void OnPlayClick(object? sender, RoutedEventArgs e)
    {
        _userDriven = true;
        if (Phase == AgentTimelinePhase.Running)
        {
            PauseCore(announce: true, raise: true);
        }
        else
        {
            PlayCore(announce: true, raise: true);
        }
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        _userDriven = true;
        RestartCore(announce: true, raise: true);
    }

    private IList<double> EffectiveSpeeds => Speeds is { Count: > 0 } speeds ? speeds : s_defaultSpeeds;

    private void UpdateSpeedControl()
    {
        var speeds = EffectiveSpeeds;
        PseudoClasses.Set(SingleSpeedPseudoClass, speeds.Count <= 1);
        if (_speed is null)
        {
            return;
        }
        _syncingSpeed = true;
        try
        {
            var options = new SegmentedOption[speeds.Count];
            var selected = -1;
            for (var i = 0; i < speeds.Count; i++)
            {
                options[i] = new SegmentedOption(speeds[i], Labels.SpeedOption(speeds[i]));
                if (Math.Abs(speeds[i] - Speed) < 1e-9)
                {
                    selected = i;
                }
            }
            if (_speed.ItemsSource is not SegmentedOption[] current || !SameOptions(current, options))
            {
                _speed.ItemsSource = options;
            }
            _speed.SelectedIndex = selected;
            _speed.IsVisible = speeds.Count > 1;
        }
        finally
        {
            _syncingSpeed = false;
        }
    }

    private static bool SameOptions(SegmentedOption[] a, SegmentedOption[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!Equals(a[i].Value, b[i].Value) || a[i].Label != b[i].Label)
            {
                return false;
            }
        }
        return true;
    }

    private void OnSpeedSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncingSpeed || _speed?.SelectedItem is not SegmentedOption { Value: double value })
        {
            return;
        }
        if (Math.Abs(value - Speed) < 1e-9)
        {
            return;
        }
        _userDriven = true;
        SetCurrentValue(SpeedProperty, value);
        if (_clock.IsPlaying)
        {
            _announcer?.Announce(Labels.PlayingAt(value));
        }
        RaisePlayback(AgentTimelinePlaybackAction.Speed);
    }

    // ---- Live mode ---------------------------------------------------------------------------------------------------

    private void StartLive()
    {
        if (!_attached && Run is null)
        {
            return;
        }
        _started = true;
        var run = Run;
        _liveAxis = 0;
        _clock.StartLive(run?.StartedAt);
        UpdatePlayable();
        UpdateLiveAxis(animate: false);
        UpdateSummary();
        AfterJump();
    }

    private void StopLive()
    {
        if (!_clock.IsLive)
        {
            return;
        }
        _liveEnd = _clock.Time;
        var length = _model.HasOpenSpans ? Math.Max(_model.Duration, _liveEnd) : _model.Duration;
        _clock.Duration = length;
        _clock.StopLive();
        UpdatePlayable();
        Duration = length;
        _plot?.SetAxis(_axisValue.Value, length);
        if (ArcMotion.IsFull(this))
        {
            _ = _axisValue.AnimateTo(Math.Max(length, 1e-3), MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _axisValue.Jump(Math.Max(length, 1e-3));
        }
        UpdateSummary();
        AfterJump();
        RaiseEvent(new RoutedEventArgs(CompletedEvent, this));
        _announcer?.Announce(Labels.RunComplete(Summary));
    }

    private void UpdateLiveAxis(bool animate)
    {
        var extent = Math.Max(_clock.Time, _model.DataEnd);
        var target = AgentTimelineMath.LiveAxis(extent, _liveAxis);
        if (Math.Abs(target - _liveAxis) < 1e-9)
        {
            return;
        }
        var first = _liveAxis <= 0;
        _liveAxis = target;
        if (!first && animate && ArcMotion.IsFull(this))
        {
            _ = _axisValue.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else
        {
            _axisValue.Jump(target);
            _plot?.SetAxis(target, extent);
        }
    }

    // ---- Spans: hover, focus, pin --------------------------------------------------------------------------------------

    internal void OnPlotHover(int span)
    {
        if (span == _hovered)
        {
            return;
        }
        _hovered = span;
        _plot?.SetHovered(span, ArcMotion.IsFull(this));
        RequestFrames(0.2);
        UpdateCard();
    }

    internal void OnSpanFocusChanged() => UpdateCard();

    internal void OnSpanPressed(int span)
    {
        _plot?.SetRoving(span, focus: true, NavigationMethod.Pointer);
        TogglePin(span);
    }

    internal void TogglePin(int span)
    {
        if (span < 0 || span >= _model.Spans.Length)
        {
            return;
        }
        Pin(_pinnedIndex == span ? null : _model.Spans[span].Span);
    }

    internal void MoveRoving(AgentTimelineKeyMove move)
    {
        if (_plot is not { } plot)
        {
            return;
        }
        var from = plot.EnsureRoving();
        var to = _model.Navigate(from, move, _clock.Time, OpenEnd);
        if (to >= 0 && to != from)
        {
            plot.SetRoving(to, focus: true, NavigationMethod.Directional);
            plot.ActiveProxy.BringIntoView();
            UpdateCard();
        }
    }

    private void OnPinnedChanged(AgentTimelineSpan? old, AgentTimelineSpan? span)
    {
        var index = _model.IndexOf(span);
        _pinnedIndex = index;
        _plot?.SetPinned(index, ArcMotion.IsFull(this));
        RequestFrames(0.25);
        UpdatePseudoClasses();
        UpdateCard();
        if (!ReferenceEquals(old, span))
        {
            RaiseEvent(new AgentTimelineSpanSelectedEventArgs(SpanSelectedEvent, span));
        }
    }

    /// <summary>
    /// Shows the card for the span under the pointer, else the focused span, else the pinned one; hides it (after the
    /// grace) when there is none.
    /// </summary>
    private void UpdateCard()
    {
        if (_card is not { } card || _plot is not { } plot || _cardLayer is null)
        {
            return;
        }
        var span = _hovered >= 0 ? _hovered : plot.HasSpanFocus ? plot.Roving : _pinnedIndex;
        if (span < 0 || span >= _model.Spans.Length || !_model.IsMounted(span, _clock.Time))
        {
            card.Hide();
            return;
        }
        card.Show(CardInfo(span), height => Place(span, height));
    }

    private Point Place(int span, double height)
    {
        if (_plot is not { } plot || _cardLayer is null || _card is null)
        {
            return default;
        }
        var rect = plot.FullRect(span);
        var origin = plot.TranslatePoint(new Point(rect.X, rect.Y), _cardLayer) ?? new Point(rect.X, rect.Y);
        var layerWidth = _cardLayer.Bounds.Width;
        var width = _card.CardWidth;
        var x = origin.X - AgentTimelineLayout.CardOffset;
        x = Math.Max(AgentTimelineLayout.CardMargin, Math.Min(x, layerWidth - width - AgentTimelineLayout.CardMargin));
        var above = origin.Y - AgentTimelineLayout.CardGap - height;
        var y = above >= 0 ? above : origin.Y + AgentTimelineLayout.BarHeight + AgentTimelineLayout.CardGap;
        return new Point(Math.Round(x, 2), Math.Round(y, 2));
    }

    private AgentTimelineCardInfo CardInfo(int index)
    {
        var labels = Labels;
        var info = _model.Spans[index];
        var span = info.Span;
        var agent = _model.Rows[info.Row].Agent;
        var end = info.EndAt(OpenEnd);
        var notes = new List<AgentTimelineCardNote>();
        if (info.IsFailed)
        {
            notes.Add(new AgentTimelineCardNote(ArcIcons.CircleX, labels.FailedNote(span.Detail), true));
        }
        else if (info.IsRetry)
        {
            notes.Add(new AgentTimelineCardNote(ArcIcons.RotateCw, span.Detail ?? labels.RetryNote, false));
        }
        if (info.Handoffs.Count >= 2)
        {
            notes.Add(new AgentTimelineCardNote(ArcIcons.CornerDownRight, labels.HandoffsDuring(info.Handoffs.Count), false));
        }
        else if (info.Handoffs.Count == 1)
        {
            var handoff = _model.Handoffs[info.Handoffs[0]];
            if (handoff.FromRow == info.Row)
            {
                var to = _model.Rows[handoff.ToRow].Agent.Name;
                notes.Add(new AgentTimelineCardNote(AgentTimelineIcons.CornerUpLeft, labels.HandoffTo(to, handoff.Handoff.Label), false));
            }
            else
            {
                var from = _model.Rows[handoff.FromRow].Agent.Name;
                notes.Add(new AgentTimelineCardNote(ArcIcons.CornerDownRight, labels.HandoffFrom(from, handoff.Handoff.Label), false));
            }
        }
        var isTool = info.Kind == AgentTimelineSpanKind.Tool;
        var duration = labels.Time(Math.Max(0, end - info.Start));
        var tokens = info.Kind == AgentTimelineSpanKind.Waiting || span.Tokens is null ? null : labels.Tokens(info.Tokens);
        var key = string.Join("|", agent.Name, span.Label, duration, tokens, span.Tool, string.Join("/", notes.Select(n => n.Text)));
        return new AgentTimelineCardInfo
        {
            Span = index,
            Key = key,
            Dot = _plot?.RowPalette(info.Row)?.Solid,
            Agent = agent.Name,
            Model = agent.Model?.Name,
            Label = span.Label,
            Tool = isTool ? span.Tool ?? labels.ToolCall : null,
            ToolMark = span.ToolMark,
            Kind = labels.KindOf(info.Kind),
            Duration = duration,
            Tokens = tokens,
            Notes = notes
        };
    }
}
