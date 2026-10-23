using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A live payments map: a dotted globe on which
/// every payment flies as an arc from its city to the headquarters, a day timeline under it (payments per 15 minutes,
/// the playhead, replay and live controls), and a panel with today's revenue, the regions against their forecast and
/// the top cities. Drag the globe to spin it (it glides and drifts back and forth when idle), hover a city for its
/// figures, pick a city or a region to fly there, replay the day at 1500× or drag the playhead to any moment; every
/// figure follows the playhead.
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="Cities"/> (with <see cref="Headquarters"/>, where arcs land), <see cref="Regions"/> and
/// <see cref="Payments"/> in time order. Totals at any moment come from prefix sums (O(log n)), so a whole day of
/// payments replays smoothly. Payments after <see cref="Now"/> wait: with <see cref="ClockMode"/> Internal the globe moves
/// <see cref="Now"/> on with the real clock, so a day generated in advance plays out live; with External your app
/// sets <see cref="Now"/> and appends payments (<see cref="AddPayment"/> or a collection that raises change events).
/// </para>
/// <para>
/// Keys: on the globe, arrows turn it and Escape clears the selection; on the timeline ← / → move 5 minutes (Shift: an
/// hour), PageUp / PageDown an hour, Home goes to 00:00 and End goes live. Under reduced motion the globe holds still
/// (a snapshot of the latest arcs), Replay and Pause are hidden and the pill reads "Now".
/// </para>
/// </remarks>
[TemplatePart(CanvasPart, typeof(RevenueGlobeCanvas))]
[TemplatePart(TipPart, typeof(ShowpieceHoverCard))]
[TemplatePart(StagePart, typeof(RevenueGlobeStagePanel))]
[TemplatePart(TimelinePart, typeof(RevenueGlobeTimeline))]
[TemplatePart(ControlsPart, typeof(RevenueGlobeControlsPanel))]
[TemplatePart(ReplayPart, typeof(AvaloniaButton))]
[TemplatePart(PausePart, typeof(AvaloniaButton))]
[TemplatePart(LivePart, typeof(AvaloniaButton))]
[TemplatePart(LiveTextPart, typeof(TextBlock))]
[TemplatePart(ClockPart, typeof(TextBlock))]
[TemplatePart(ZonePart, typeof(TextBlock))]
[TemplatePart(HoursPart, typeof(Panel))]
[TemplatePart(PanelsPart, typeof(RevenueGlobePanelsLayout))]
[TemplatePart(RevenuePart, typeof(RevenueGlobeTicker))]
[TemplatePart(PaymentsPart, typeof(RevenueGlobeTicker))]
[TemplatePart(LastHourPart, typeof(RevenueGlobeTicker))]
[TemplatePart(RegionRowsPart, typeof(Panel))]
[TemplatePart(CityRowsPart, typeof(Panel))]
[TemplatePart(AnnouncerPart, typeof(ShowpieceLiveRegion))]
[TemplatePart(TablePart, typeof(Panel))]
[PseudoClasses(LivePseudoClass, PausedPseudoClass, ReplayingPseudoClass, ScrubbingPseudoClass, CitySelectedPseudoClass,
    RegionSelectedPseudoClass, WidePseudoClass, StackedPseudoClass, NarrowPseudoClass, CompactPseudoClass, ReducedPseudoClass)]
public partial class RevenueGlobe : TemplatedControl
{
    public const string CanvasPart = "PART_Canvas";
    public const string TipPart = "PART_Tip";
    public const string StagePart = "PART_Stage";
    public const string TimelinePart = "PART_Timeline";
    public const string ControlsPart = "PART_Controls";
    public const string ReplayPart = "PART_Replay";
    public const string PausePart = "PART_Pause";
    public const string LivePart = "PART_Live";
    public const string LiveTextPart = "PART_LiveText";
    public const string ClockPart = "PART_Clock";
    public const string ZonePart = "PART_Zone";
    public const string HoursPart = "PART_Hours";
    public const string PanelsPart = "PART_Panels";
    public const string RevenuePart = "PART_Revenue";
    public const string PaymentsPart = "PART_Payments";
    public const string LastHourPart = "PART_LastHour";
    public const string RegionRowsPart = "PART_RegionRows";
    public const string CityRowsPart = "PART_CityRows";
    public const string AnnouncerPart = "PART_Announcer";
    public const string TablePart = "PART_Table";

    /// <summary>Set while the playhead follows the present.</summary>
    public const string LivePseudoClass = ":live";

    /// <summary>Set while paused.</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set while replaying (also during a Go live fast-forward).</summary>
    public const string ReplayingPseudoClass = ":replaying";

    /// <summary>Set while the timeline is dragged.</summary>
    public const string ScrubbingPseudoClass = ":scrubbing";

    /// <summary>Set while a city is selected.</summary>
    public const string CitySelectedPseudoClass = ":city-selected";

    /// <summary>Set while a region is selected.</summary>
    public const string RegionSelectedPseudoClass = ":region-selected";

    /// <summary>Set at 880px and wider: the panel sits beside the globe.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set between 600 and 880px: the panel sits under the globe in two columns.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set below 600px: one column, a smaller revenue figure.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set below 400px: the clock takes a row of its own.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set under reduced (or no) motion: a still snapshot, no replay or pause.</summary>
    public const string ReducedPseudoClass = ":reduced";

    #region 公共属性定义

    /// <summary>The block's title (text or any content).</summary>
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<RevenueGlobe, object?>(nameof(Title));

    /// <summary>The line under the title (text or any content).</summary>
    public static readonly StyledProperty<object?> DescriptionProperty =
        AvaloniaProperty.Register<RevenueGlobe, object?>(nameof(Description));

    /// <summary>The date at the top right, formatted by the app ("Thursday, 22 October").</summary>
    public static readonly StyledProperty<string?> DateTextProperty =
        AvaloniaProperty.Register<RevenueGlobe, string?>(nameof(DateText));

    /// <summary>Where the arcs land: drawn as a haloed dot with its name. Its own payments count but draw no arc.</summary>
    public static readonly StyledProperty<RevenueCity?> HeadquartersProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueCity?>(nameof(Headquarters));

    /// <summary>Regions the cities count toward; each gets a row with its revenue against its forecast.</summary>
    public static readonly DirectProperty<RevenueGlobe, AvaloniaList<RevenueRegion>> RegionsProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, AvaloniaList<RevenueRegion>>(nameof(Regions), o => o.Regions, (o, v) => o.Regions = v);

    /// <summary>
    /// Payments in time order. Collections that raise change events are followed (appending at the end is cheap);
    /// payments after <see cref="Now"/> wait for it.
    /// </summary>
    public static readonly StyledProperty<IEnumerable<RevenuePayment>?> PaymentsProperty =
        AvaloniaProperty.Register<RevenueGlobe, IEnumerable<RevenuePayment>?>(nameof(Payments));

    /// <summary>Midnight of the day shown; the timeline spans 24 hours from it. Default: 00:00 UTC of <see cref="Now"/>.</summary>
    public static readonly StyledProperty<DateTimeOffset> DayStartProperty =
        AvaloniaProperty.Register<RevenueGlobe, DateTimeOffset>(nameof(DayStart));

    /// <summary>
    /// The present: payments up to here have happened. With <see cref="ClockMode"/> Internal the globe moves it on in
    /// real time from the value you set (default: the current time).
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset> NowProperty =
        AvaloniaProperty.Register<RevenueGlobe, DateTimeOffset>(nameof(Now), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The playhead (two-way). Setting it jumps there; it follows <see cref="Now"/> while live.</summary>
    public static readonly StyledProperty<DateTimeOffset> TimeProperty =
        AvaloniaProperty.Register<RevenueGlobe, DateTimeOffset>(nameof(Time), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time stands still and no new arcs start (two-way); the globe keeps drifting and arcs in flight land.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<RevenueGlobe, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Simulated seconds per second while replaying. Default 1500 (an hour in 2.4 s).</summary>
    public static readonly StyledProperty<double> ReplaySpeedProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(ReplaySpeed), 1500);

    /// <summary>The selected city (two-way): the camera flies there and holds; clears <see cref="SelectedRegion"/>.</summary>
    public static readonly StyledProperty<RevenueCity?> SelectedCityProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueCity?>(nameof(SelectedCity), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected region (two-way): the camera flies to its center and other cities fade; clears <see cref="SelectedCity"/>.</summary>
    public static readonly StyledProperty<RevenueRegion?> SelectedRegionProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueRegion?>(nameof(SelectedRegion), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the globe moves <see cref="Now"/> on by itself (Internal, default) or the app does (External).</summary>
    public static readonly StyledProperty<RevenueGlobeClockMode> ClockModeProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueGlobeClockMode>(nameof(ClockMode));

    /// <summary>The tilt the camera returns to, in degrees of latitude it looks from. Default 22.</summary>
    public static readonly StyledProperty<double> HomeTiltProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(HomeTilt), 22);

    /// <summary>Idle drift speed in degrees per second (0 turns the drift off). Default 4.</summary>
    public static readonly StyledProperty<double> DriftSpeedProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(DriftSpeed), 4);

    /// <summary>Center of the drift range: degrees east of the headquarters' longitude. Default 40.</summary>
    public static readonly StyledProperty<double> DriftCenterOffsetProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(DriftCenterOffset), 40);

    /// <summary>Half width of the drift range in degrees. Default 43.</summary>
    public static readonly StyledProperty<double> DriftHalfRangeProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(DriftHalfRange), 43);

    /// <summary>Zoom the camera flies in to for a selected city. Default 1.30.</summary>
    public static readonly StyledProperty<double> CityZoomProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(CityZoom), 1.30);

    /// <summary>Zoom the camera flies in to for a selected region. Default 1.08.</summary>
    public static readonly StyledProperty<double> RegionZoomProperty =
        AvaloniaProperty.Register<RevenueGlobe, double>(nameof(RegionZoom), 1.08);

    /// <summary>The land and sea dots; null uses <see cref="RevenueGlobeLand.Default"/> (Natural Earth 1:110m).</summary>
    public static readonly StyledProperty<RevenueGlobeLand?> LandProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueGlobeLand?>(nameof(Land));

    /// <summary>Symbol in front of amounts. Default "$"; amounts show as whole numbers with group separators.</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<RevenueGlobe, string>(nameof(CurrencySymbol), "$");

    /// <summary>Culture for numbers (separators); null uses en-US.</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<RevenueGlobe, CultureInfo?>(nameof(Culture));

    /// <summary>Format of the clock. Default "HH:mm:ss".</summary>
    public static readonly StyledProperty<string> TimeFormatProperty =
        AvaloniaProperty.Register<RevenueGlobe, string>(nameof(TimeFormat), "HH:mm:ss");

    /// <summary>Every word the block shows or announces (English by default).</summary>
    public static readonly StyledProperty<RevenueGlobeLabels> LabelsProperty =
        AvaloniaProperty.Register<RevenueGlobe, RevenueGlobeLabels>(nameof(Labels), new RevenueGlobeLabels());

    /// <summary>The playhead follows the present.</summary>
    public static readonly DirectProperty<RevenueGlobe, bool> IsLiveProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, bool>(nameof(IsLive), o => o.IsLive);

    /// <summary>What the timeline is doing: live, paused, replaying or scrubbing.</summary>
    public static readonly DirectProperty<RevenueGlobe, RevenueGlobePlaybackState> PlaybackStateProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, RevenueGlobePlaybackState>(nameof(PlaybackState), o => o.PlaybackState);

    /// <summary>Longitude at the center of the view, in degrees (follows the camera).</summary>
    public static readonly DirectProperty<RevenueGlobe, double> CenterLongitudeProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, double>(nameof(CenterLongitude), o => o.CenterLongitude);

    /// <summary>Latitude the camera looks from, in degrees (follows the camera).</summary>
    public static readonly DirectProperty<RevenueGlobe, double> CenterLatitudeProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, double>(nameof(CenterLatitude), o => o.CenterLatitude);

    /// <summary>The camera zoom (1 = the globe fills 82% of the smaller side).</summary>
    public static readonly DirectProperty<RevenueGlobe, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<RevenueGlobe, double>(nameof(Zoom), o => o.Zoom);

    /// <summary>Raised when the user or the app changes the selected city or region.</summary>
    public static readonly RoutedEvent<RevenueGlobeSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<RevenueGlobe, RevenueGlobeSelectionChangedEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised whenever the playhead moves (every frame while replaying: throttle what you do with it).</summary>
    public static readonly RoutedEvent<RevenueGlobeTimeChangedEventArgs> TimeChangedEvent =
        RoutedEvent.Register<RevenueGlobe, RevenueGlobeTimeChangedEventArgs>(nameof(TimeChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a city dot or a top-city row is clicked (also when it was already selected).</summary>
    public static readonly RoutedEvent<RevenueGlobeCityEventArgs> CityInvokedEvent =
        RoutedEvent.Register<RevenueGlobe, RevenueGlobeCityEventArgs>(nameof(CityInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public object? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public object? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="DateTextProperty"/>
    public string? DateText { get => GetValue(DateTextProperty); set => SetValue(DateTextProperty, value); }

    /// <inheritdoc cref="HeadquartersProperty"/>
    public RevenueCity? Headquarters { get => GetValue(HeadquartersProperty); set => SetValue(HeadquartersProperty, value); }

    /// <summary>The cities payments come from (inline content).</summary>
    [Content]
    public AvaloniaList<RevenueCity> Cities { get; } = new();

    /// <inheritdoc cref="RegionsProperty"/>
    public AvaloniaList<RevenueRegion> Regions
    {
        get => _regionsList;
        set
        {
            var old = _regionsList;
            if (SetAndRaise(RegionsProperty, ref _regionsList, value ?? new AvaloniaList<RevenueRegion>()))
            {
                old.CollectionChanged -= OnRegionsChanged;
                _regionsList.CollectionChanged += OnRegionsChanged;
                RebuildScene();
            }
        }
    }

    /// <inheritdoc cref="PaymentsProperty"/>
    public IEnumerable<RevenuePayment>? Payments { get => GetValue(PaymentsProperty); set => SetValue(PaymentsProperty, value); }

    /// <inheritdoc cref="DayStartProperty"/>
    public DateTimeOffset DayStart { get => GetValue(DayStartProperty); set => SetValue(DayStartProperty, value); }

    /// <inheritdoc cref="NowProperty"/>
    public DateTimeOffset Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="TimeProperty"/>
    public DateTimeOffset Time { get => GetValue(TimeProperty); set => SetValue(TimeProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="ReplaySpeedProperty"/>
    public double ReplaySpeed { get => GetValue(ReplaySpeedProperty); set => SetValue(ReplaySpeedProperty, value); }

    /// <inheritdoc cref="SelectedCityProperty"/>
    public RevenueCity? SelectedCity { get => GetValue(SelectedCityProperty); set => SetValue(SelectedCityProperty, value); }

    /// <inheritdoc cref="SelectedRegionProperty"/>
    public RevenueRegion? SelectedRegion { get => GetValue(SelectedRegionProperty); set => SetValue(SelectedRegionProperty, value); }

    /// <inheritdoc cref="ClockModeProperty"/>
    public RevenueGlobeClockMode ClockMode { get => GetValue(ClockModeProperty); set => SetValue(ClockModeProperty, value); }

    /// <inheritdoc cref="HomeTiltProperty"/>
    public double HomeTilt { get => GetValue(HomeTiltProperty); set => SetValue(HomeTiltProperty, value); }

    /// <inheritdoc cref="DriftSpeedProperty"/>
    public double DriftSpeed { get => GetValue(DriftSpeedProperty); set => SetValue(DriftSpeedProperty, value); }

    /// <inheritdoc cref="DriftCenterOffsetProperty"/>
    public double DriftCenterOffset { get => GetValue(DriftCenterOffsetProperty); set => SetValue(DriftCenterOffsetProperty, value); }

    /// <inheritdoc cref="DriftHalfRangeProperty"/>
    public double DriftHalfRange { get => GetValue(DriftHalfRangeProperty); set => SetValue(DriftHalfRangeProperty, value); }

    /// <inheritdoc cref="CityZoomProperty"/>
    public double CityZoom { get => GetValue(CityZoomProperty); set => SetValue(CityZoomProperty, value); }

    /// <inheritdoc cref="RegionZoomProperty"/>
    public double RegionZoom { get => GetValue(RegionZoomProperty); set => SetValue(RegionZoomProperty, value); }

    /// <inheritdoc cref="LandProperty"/>
    public RevenueGlobeLand? Land { get => GetValue(LandProperty); set => SetValue(LandProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="TimeFormatProperty"/>
    public string TimeFormat { get => GetValue(TimeFormatProperty); set => SetValue(TimeFormatProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public RevenueGlobeLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive
    {
        get => _isLive;
        private set => SetAndRaise(IsLiveProperty, ref _isLive, value);
    }

    /// <inheritdoc cref="PlaybackStateProperty"/>
    public RevenueGlobePlaybackState PlaybackState
    {
        get => _playbackState;
        private set => SetAndRaise(PlaybackStateProperty, ref _playbackState, value);
    }

    /// <inheritdoc cref="CenterLongitudeProperty"/>
    public double CenterLongitude
    {
        get => _centerLongitude;
        private set => SetAndRaise(CenterLongitudeProperty, ref _centerLongitude, value);
    }

    /// <inheritdoc cref="CenterLatitudeProperty"/>
    public double CenterLatitude
    {
        get => _centerLatitude;
        private set => SetAndRaise(CenterLatitudeProperty, ref _centerLatitude, value);
    }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom
    {
        get => _zoom;
        private set => SetAndRaise(ZoomProperty, ref _zoom, value);
    }

    /// <summary>Starts the day over from 00:00 at <see cref="ReplaySpeed"/> (bind a button to it).</summary>
    public ICommand ReplayCommand { get; }

    /// <summary>Back to the present (bind a button to it).</summary>
    public ICommand GoLiveCommand { get; }

    /// <summary>Pauses, or resumes after a pause (bind a button to it).</summary>
    public ICommand TogglePauseCommand { get; }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<RevenueGlobeSelectionChangedEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="TimeChangedEvent"/>
    public event EventHandler<RevenueGlobeTimeChangedEventArgs>? TimeChanged
    {
        add => AddHandler(TimeChangedEvent, value);
        remove => RemoveHandler(TimeChangedEvent, value);
    }

    /// <inheritdoc cref="CityInvokedEvent"/>
    public event EventHandler<RevenueGlobeCityEventArgs>? CityInvoked
    {
        add => AddHandler(CityInvokedEvent, value);
        remove => RemoveHandler(CityInvokedEvent, value);
    }

    #endregion

    private AvaloniaList<RevenueRegion> _regionsList = new();
    private bool _isLive = true;
    private RevenueGlobePlaybackState _playbackState = RevenueGlobePlaybackState.Live;
    private double _centerLongitude;
    private double _centerLatitude;
    private double _zoom = 1;

    private RevenueGlobeCanvas? _canvas;
    private ShowpieceHoverCard? _tip;
    private readonly RevenueGlobeTip _tipContent = new();
    private RevenueGlobeStagePanel? _stage;
    private RevenueGlobeTimeline? _timeline;
    private RevenueGlobeControlsPanel? _controls;
    private AvaloniaButton? _replayButton;
    private AvaloniaButton? _pauseButton;
    private AvaloniaButton? _liveButton;
    private TextBlock? _liveText;
    private TextBlock? _clockText;
    private TextBlock? _zoneText;
    private Panel? _hours;
    private RevenueGlobePanelsLayout? _panels;
    private RevenueGlobeTicker? _revenueTicker;
    private RevenueGlobeTicker? _paymentsTicker;
    private RevenueGlobeTicker? _lastHourTicker;
    private Panel? _regionRows;
    private Panel? _cityRows;
    private ShowpieceLiveRegion? _announcer;
    private Panel? _table;

    static RevenueGlobe()
    {
        FocusableProperty.OverrideDefaultValue<RevenueGlobe>(false);
    }

    public RevenueGlobe()
    {
        ReplayCommand = new RevenueGlobeCommand(Replay);
        GoLiveCommand = new RevenueGlobeCommand(GoLive);
        TogglePauseCommand = new RevenueGlobeCommand(TogglePause);
        Cities.CollectionChanged += OnCitiesChanged;
        _regionsList.CollectionChanged += OnRegionsChanged;
        _clockLoop = new LiveLoop(this, OnClockFrame);
        _ledger = new RevenueGlobeLedger(0);
        InitializeTime();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _canvas = e.NameScope.Find<RevenueGlobeCanvas>(CanvasPart);
        _tip = e.NameScope.Find<ShowpieceHoverCard>(TipPart);
        _stage = e.NameScope.Find<RevenueGlobeStagePanel>(StagePart);
        _timeline = e.NameScope.Find<RevenueGlobeTimeline>(TimelinePart);
        _controls = e.NameScope.Find<RevenueGlobeControlsPanel>(ControlsPart);
        _replayButton = e.NameScope.Find<AvaloniaButton>(ReplayPart);
        _pauseButton = e.NameScope.Find<AvaloniaButton>(PausePart);
        _liveButton = e.NameScope.Find<AvaloniaButton>(LivePart);
        _liveText = e.NameScope.Find<TextBlock>(LiveTextPart);
        _clockText = e.NameScope.Find<TextBlock>(ClockPart);
        _zoneText = e.NameScope.Find<TextBlock>(ZonePart);
        _hours = e.NameScope.Find<Panel>(HoursPart);
        _panels = e.NameScope.Find<RevenueGlobePanelsLayout>(PanelsPart);
        _revenueTicker = e.NameScope.Find<RevenueGlobeTicker>(RevenuePart);
        _paymentsTicker = e.NameScope.Find<RevenueGlobeTicker>(PaymentsPart);
        _lastHourTicker = e.NameScope.Find<RevenueGlobeTicker>(LastHourPart);
        _regionRows = e.NameScope.Find<Panel>(RegionRowsPart);
        _cityRows = e.NameScope.Find<Panel>(CityRowsPart);
        _announcer = e.NameScope.Find<ShowpieceLiveRegion>(AnnouncerPart);
        _table = e.NameScope.Find<Panel>(TablePart);

        if (_canvas is not null)
        {
            _canvas.CityTapped += OnCanvasCityTapped;
            _canvas.BackgroundTapped += OnCanvasBackgroundTapped;
            _canvas.HoverCityChanged += OnCanvasHoverChanged;
            _canvas.EscapePressed += OnCanvasEscape;
            _canvas.DragBegan += OnCanvasDragBegan;
            _canvas.ViewChanged += OnCanvasViewChanged;
            _canvas.ArcLanded += OnArcLanded;
            _canvas.MotionLevelChangedEvent += OnMotionLevelChanged;
            _canvas.Land = Land ?? RevenueGlobeLand.Default;
            _canvas.Scene = _scene;
        }
        if (_tip is not null)
        {
            _tip.Content = _tipContent;
            _tip.Placement = ShowpieceCardPlacement.Globe;
            _tip.PositionSpring = ArcMotionPresets.Snappy;
        }
        if (_timeline is not null)
        {
            _timeline.Owner = this;
        }
        if (_replayButton is not null)
        {
            _replayButton.Click += OnReplayClick;
        }
        if (_pauseButton is not null)
        {
            _pauseButton.Click += OnPauseClick;
        }
        if (_liveButton is not null)
        {
            _liveButton.Click += OnLiveClick;
        }
        BuildHourMarks();
        BuildRows();
        _placedCenter = double.NaN;
        ConfigureCamera();
        ApplyLabels();
        UpdateLayoutMode(Bounds.Width);
        UpdateMotionLevel();
        UpdateTimeline(force: true);
        UpdateFigures();
        _clockSecond = long.MinValue;
        UpdateClockText();
        SyncState();
        ApplySelectionToParts(animate: false);
    }

    private void DetachParts()
    {
        if (_canvas is not null)
        {
            _canvas.CityTapped -= OnCanvasCityTapped;
            _canvas.BackgroundTapped -= OnCanvasBackgroundTapped;
            _canvas.HoverCityChanged -= OnCanvasHoverChanged;
            _canvas.EscapePressed -= OnCanvasEscape;
            _canvas.DragBegan -= OnCanvasDragBegan;
            _canvas.ViewChanged -= OnCanvasViewChanged;
            _canvas.ArcLanded -= OnArcLanded;
            _canvas.MotionLevelChangedEvent -= OnMotionLevelChanged;
        }
        if (_tip is not null)
        {
            _tip.Content = null;
        }
        if (_timeline is not null)
        {
            _timeline.Owner = null;
        }
        if (_replayButton is not null)
        {
            _replayButton.Click -= OnReplayClick;
        }
        if (_pauseButton is not null)
        {
            _pauseButton.Click -= OnPauseClick;
        }
        if (_liveButton is not null)
        {
            _liveButton.Click -= OnLiveClick;
        }
        _regionRows?.Children.Clear();
        _cityRows?.Children.Clear();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == HeadquartersProperty)
        {
            RebuildScene();
        }
        else if (property == PaymentsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnPaymentsCollectionChanged;
            }
            if (change.NewValue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnPaymentsCollectionChanged;
            }
            _extraPayments.Clear();
            RebuildLedger();
        }
        else if (property == DayStartProperty)
        {
            OnDayStartChanged();
        }
        else if (property == NowProperty)
        {
            OnNowPropertyChanged();
        }
        else if (property == TimeProperty)
        {
            OnTimePropertyChanged();
        }
        else if (property == IsPausedProperty)
        {
            OnIsPausedChanged();
        }
        else if (property == ReplaySpeedProperty)
        {
            _playback.ReplaySpeed = Math.Max(1, ReplaySpeed);
        }
        else if (property == ClockModeProperty)
        {
            AnchorClock();
            RefreshClockLoop();
        }
        else if (property == SelectedCityProperty || property == SelectedRegionProperty)
        {
            OnSelectionPropertyChanged(change);
        }
        else if (property == HomeTiltProperty || property == DriftSpeedProperty || property == DriftCenterOffsetProperty ||
                 property == DriftHalfRangeProperty)
        {
            ConfigureCamera();
        }
        else if (property == LandProperty)
        {
            if (_canvas is not null)
            {
                _canvas.Land = Land ?? RevenueGlobeLand.Default;
            }
        }
        else if (property == CurrencySymbolProperty || property == CultureProperty)
        {
            RefreshFormatters();
        }
        else if (property == TimeFormatProperty)
        {
            _clockSecond = long.MinValue;
            UpdateClockText();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (property == BoundsProperty)
        {
            UpdateLayoutMode(Bounds.Width);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AnchorClock();
        UpdateMotionLevel();
        RefreshClockLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clockLoop.Stop();
    }

    // Layout --------------------------------------------------------------------------------------------------------------

    private RevenueGlobeLayout _layout = RevenueGlobeLayout.Stacked;

    /// <summary>The layout for the current width (tests).</summary>
    internal RevenueGlobeLayout CurrentLayout => _layout;

    private void UpdateLayoutMode(double width)
    {
        if (width <= 0)
        {
            return;
        }
        // Container width = the block's content box (inside its 1px border), like Arc's container queries.
        var content = Math.Max(0, width - 2);
        _layout = RevenueGlobeStagePanel.LayoutFor(content);
        PseudoClasses.Set(WidePseudoClass, _layout == RevenueGlobeLayout.Wide);
        PseudoClasses.Set(StackedPseudoClass, _layout == RevenueGlobeLayout.Stacked);
        PseudoClasses.Set(NarrowPseudoClass, _layout == RevenueGlobeLayout.Narrow);
        PseudoClasses.Set(CompactPseudoClass, content < 400);
        if (_stage is not null)
        {
            _stage.Layout = _layout;
        }
        if (_panels is not null)
        {
            _panels.TwoColumns = _layout == RevenueGlobeLayout.Stacked;
        }
        if (_controls is not null)
        {
            _controls.ClockOnOwnRow = content < 400;
        }
    }

    // Labels ---------------------------------------------------------------------------------------------------------------

    private void ApplyLabels()
    {
        var labels = Labels;
        _tipContent.TodayLabel = labels.Today;
        _tipContent.PaymentsLabel = labels.Payments;
        _tipContent.ShareLabel = labels.Share;
        if (_canvas is not null)
        {
            AutomationProperties.SetName(_canvas, $"{labels.Globe}. {labels.GlobeHelp}");
            _canvas.RoleDescription = "interactive globe";
        }
        if (_timeline is not null)
        {
            AutomationProperties.SetName(_timeline, labels.Timeline);
        }
        if (_replayButton is not null)
        {
            AutomationProperties.SetName(_replayButton, labels.Replay);
        }
        if (_zoneText is not null)
        {
            _zoneText.Text = labels.TimeZone;
        }
        BuildHourMarks();
        UpdatePlaybackVisuals();
        UpdateFigures();
    }

    private void BuildHourMarks()
    {
        if (_hours is null)
        {
            return;
        }
        _hours.Children.Clear();
        foreach (var mark in Labels.HourMarks)
        {
            _hours.Children.Add(new TextBlock { Text = mark, Classes = { "hour" } });
        }
    }

    private sealed class RevenueGlobeCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
