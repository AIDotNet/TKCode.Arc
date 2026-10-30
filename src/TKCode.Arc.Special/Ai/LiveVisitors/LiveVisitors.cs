using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A realtime visitors card (Arc Live visitors, design reference): a pulsing live status, the number of people on the site
/// rolling digit by digit, a dotted world map where new arrivals ping in and fade, the top pages right now with bars that
/// reorder as counts change, and a ticker of signups and upgrades with the country and how long ago. A pause button holds
/// the last snapshot.
/// <para>
/// Feed it from your realtime source: set <see cref="Count"/>, <see cref="Pages"/>, <see cref="Visitors"/> and
/// <see cref="Events"/> to new snapshots as they arrive (the card works out what moved from the ids), or push single
/// arrivals and conversions with <see cref="PushVisitor(LiveVisitorsPing)"/> and <see cref="PushEvent"/>. Reflect the
/// socket's state in <see cref="Connection"/>. Without <see cref="Count"/> the card plays its <see cref="Simulation"/>, a
/// <see cref="LiveVisitorsSimulation"/> of a site you describe; with neither, it holds placeholders and says there is no
/// data.
/// </para>
/// </summary>
[TemplatePart(StatusDotPart, typeof(LiveVisitorsStatusDot))]
[TemplatePart(StatusBoxPart, typeof(Panel))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(PausePart, typeof(LiveVisitorsPauseButton))]
[TemplatePart(CountBoxPart, typeof(Panel))]
[TemplatePart(DigitsPart, typeof(RollingDigits))]
[TemplatePart(CountPlaceholderPart, typeof(LiveVisitorsPlaceholder))]
[TemplatePart(CountLabelPart, typeof(TextBlock))]
[TemplatePart(MapPart, typeof(LiveVisitorsMap))]
[TemplatePart(PagesHeadingPart, typeof(TextBlock))]
[TemplatePart(PagesPart, typeof(LiveVisitorsRowList))]
[TemplatePart(EventsHeadingPart, typeof(TextBlock))]
[TemplatePart(EventsPart, typeof(LiveVisitorsRowList))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(PausedPseudoClass, ConnectingPseudoClass, ReconnectingPseudoClass, NarrowPseudoClass, SimulatedPseudoClass, EmptyPseudoClass)]
public class LiveVisitors : TemplatedControl
{
    internal const string StatusDotPart = "PART_StatusDot";
    internal const string StatusBoxPart = "PART_StatusBox";
    internal const string StatusPart = "PART_Status";
    internal const string PausePart = "PART_Pause";
    internal const string CountBoxPart = "PART_CountBox";
    internal const string DigitsPart = "PART_Digits";
    internal const string CountPlaceholderPart = "PART_CountPlaceholder";
    internal const string CountLabelPart = "PART_CountLabel";
    internal const string MapPart = "PART_Map";
    internal const string PagesHeadingPart = "PART_PagesHeading";
    internal const string PagesPart = "PART_Pages";
    internal const string EventsHeadingPart = "PART_EventsHeading";
    internal const string EventsPart = "PART_Events";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string PausedPseudoClass = ":paused";
    internal const string ConnectingPseudoClass = ":connecting";
    internal const string ReconnectingPseudoClass = ":reconnecting";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string SimulatedPseudoClass = ":simulated";
    internal const string EmptyPseudoClass = ":nodata";

    /// <summary>How long a pushed arrival stays on the map (s).</summary>
    internal const double PushedVisitorLifetime = 6;

    /// <summary>People on the site right now. Leave it null to play the <see cref="Simulation"/>; once set, the card shows only the data you pass.</summary>
    public static readonly StyledProperty<int?> CountProperty =
        AvaloniaProperty.Register<LiveVisitors, int?>(nameof(Count));

    /// <summary>Pages with people on them. The card sorts them and lists the busiest few with bars scaled to the busiest page; rows slide when the order changes.</summary>
    public static readonly StyledProperty<IReadOnlyList<LiveVisitorsPage>?> PagesProperty =
        AvaloniaProperty.Register<LiveVisitors, IReadOnlyList<LiveVisitorsPage>?>(nameof(Pages));

    /// <summary>Recent arrivals as map dots. New ids ping in, ids no longer passed fade out; keep it to the last few seconds.</summary>
    public static readonly StyledProperty<IReadOnlyList<LiveVisitorsPing>?> VisitorsProperty =
        AvaloniaProperty.Register<LiveVisitors, IReadOnlyList<LiveVisitorsPing>?>(nameof(Visitors));

    /// <summary>Recent conversions. The card lists the newest few; with a position the newest is labelled on the map for 2s.</summary>
    public static readonly StyledProperty<IReadOnlyList<LiveVisitorsEvent>?> EventsProperty =
        AvaloniaProperty.Register<LiveVisitors, IReadOnlyList<LiveVisitorsEvent>?>(nameof(Events));

    /// <summary>The stream's state: Connecting shows placeholders in the same space, Reconnecting keeps the last data with a warning dot.</summary>
    public static readonly StyledProperty<LiveVisitorsConnection> ConnectionProperty =
        AvaloniaProperty.Register<LiveVisitors, LiveVisitorsConnection>(nameof(Connection));

    /// <summary>Whether updates are paused (two-way). While paused the card holds its last snapshot, whatever you keep passing.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<LiveVisitors, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the pause button.</summary>
    public static readonly StyledProperty<bool> IsPauseVisibleProperty =
        AvaloniaProperty.Register<LiveVisitors, bool>(nameof(IsPauseVisible), true);

    /// <summary>How many pages to list.</summary>
    public static readonly StyledProperty<int> MaxPagesProperty =
        AvaloniaProperty.Register<LiveVisitors, int>(nameof(MaxPages), 4, coerce: (_, v) => Math.Clamp(v, 1, 12));

    /// <summary>How many conversions to list.</summary>
    public static readonly StyledProperty<int> MaxEventsProperty =
        AvaloniaProperty.Register<LiveVisitors, int>(nameof(MaxEvents), 4, coerce: (_, v) => Math.Clamp(v, 1, 12));

    /// <summary>
    /// A simulated stream to play while <see cref="Count"/> is null: its count, pages, arrivals and conversions, advanced
    /// by the card's clock while it is on screen, live and not paused. Null (the default) leaves the card empty: placeholders
    /// and a "No data" status. Give each card its own simulation.
    /// </summary>
    public static readonly StyledProperty<LiveVisitorsSimulation?> SimulationProperty =
        AvaloniaProperty.Register<LiveVisitors, LiveVisitorsSimulation?>(nameof(Simulation));

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<LiveVisitorsLabels> LabelsProperty =
        AvaloniaProperty.Register<LiveVisitors, LiveVisitorsLabels>(nameof(Labels), LiveVisitorsLabels.Default);

    /// <summary>The card width below which the two lists stack (set by the theme from the token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<LiveVisitors, double>(nameof(NarrowWidth), 520);

    /// <summary>Raised when the person presses the pause button. Bubbles. Pause or close your own subscription to match.</summary>
    public static readonly RoutedEvent<LiveVisitorsPausedChangedEventArgs> PausedChangedEvent =
        RoutedEvent.Register<LiveVisitors, LiveVisitorsPausedChangedEventArgs>(nameof(PausedChanged), RoutingStrategies.Bubble);

    private readonly List<(LiveVisitorsPing Ping, double Born)> _pushedVisitors = [];
    private readonly List<LiveVisitorsEvent> _pushedEvents = [];
    private readonly LiveLoop _clock;
    /// <summary>Each digit of the count rolls on its own strip in 280ms on CSS ease-out.</summary>
    internal static readonly MotionSpec CountRoll = MotionSpec.Tween(TimeSpan.FromMilliseconds(280), AiHeightBox.EaseOut);

    private LiveVisitorsStatusDot? _statusDot;
    private Panel? _statusBox;
    private ArcContentSwap? _status;
    private LiveVisitorsPauseButton? _pause;
    private Panel? _countBox;
    private RollingDigits? _digits;
    private LiveVisitorsPlaceholder? _countPlaceholder;
    private TextBlock? _countLabel;
    private LiveVisitorsMap? _map;
    private TextBlock? _pagesHeading;
    private LiveVisitorsRowList? _pages;
    private TextBlock? _eventsHeading;
    private LiveVisitorsRowList? _events;
    private ExplainerAnnouncer? _announcer;
    private readonly Dictionary<string, LiveVisitorsEvent> _shownEvents = new(StringComparer.Ordinal);
    private string? _shownStatus;
    private double _time;
    private double _sinceTimes;
    private int _serial;
    private bool _shown;
    private bool _announcedSimulation;
    private bool _syncingPause;

    /// <summary>Creates the card.</summary>
    public LiveVisitors()
    {
        // The clock plays the simulation, ages the ticker's times and expires pushed arrivals. It runs at every motion
        // level (these are information) but only while the card is on screen and not paused.
        _clock = new LiveLoop(this, Tick) { MinimumLevel = ArcMotionLevel.Off };
    }

    /// <inheritdoc cref="CountProperty"/>
    public int? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <inheritdoc cref="PagesProperty"/>
    public IReadOnlyList<LiveVisitorsPage>? Pages { get => GetValue(PagesProperty); set => SetValue(PagesProperty, value); }

    /// <inheritdoc cref="VisitorsProperty"/>
    public IReadOnlyList<LiveVisitorsPing>? Visitors { get => GetValue(VisitorsProperty); set => SetValue(VisitorsProperty, value); }

    /// <inheritdoc cref="EventsProperty"/>
    public IReadOnlyList<LiveVisitorsEvent>? Events { get => GetValue(EventsProperty); set => SetValue(EventsProperty, value); }

    /// <inheritdoc cref="ConnectionProperty"/>
    public LiveVisitorsConnection Connection { get => GetValue(ConnectionProperty); set => SetValue(ConnectionProperty, value); }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused { get => GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }

    /// <inheritdoc cref="IsPauseVisibleProperty"/>
    public bool IsPauseVisible { get => GetValue(IsPauseVisibleProperty); set => SetValue(IsPauseVisibleProperty, value); }

    /// <inheritdoc cref="MaxPagesProperty"/>
    public int MaxPages { get => GetValue(MaxPagesProperty); set => SetValue(MaxPagesProperty, value); }

    /// <inheritdoc cref="MaxEventsProperty"/>
    public int MaxEvents { get => GetValue(MaxEventsProperty); set => SetValue(MaxEventsProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public LiveVisitorsSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public LiveVisitorsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <summary>Whether the card plays its <see cref="Simulation"/> (no <see cref="Count"/> was set, and a simulation was).</summary>
    public bool IsSimulated => Count is null && Simulation is not null;

    /// <summary>Whether the card has nothing to show: no <see cref="Count"/> and no simulation.</summary>
    internal bool IsEmpty => Count is null && Simulation is null;

    /// <inheritdoc cref="PausedChangedEvent"/>
    public event EventHandler<LiveVisitorsPausedChangedEventArgs>? PausedChanged
    {
        add => AddHandler(PausedChangedEvent, value);
        remove => RemoveHandler(PausedChangedEvent, value);
    }

    /// <summary>The clock the ticker's times and the simulated events are read from (tests replace it).</summary>
    internal Func<DateTimeOffset> Now { get; set; } = () => DateTimeOffset.UtcNow;

    internal LiveVisitorsMap? Map => _map;

    internal LiveVisitorsRowList? PageList => _pages;

    internal LiveVisitorsRowList? EventList => _events;

    internal RollingDigits? Digits => _digits;

    internal LiveVisitorsPauseButton? PauseButton => _pause;

    internal LiveVisitorsStatusDot? StatusDot => _statusDot;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal TextBlock? CountLabel => _countLabel;

    internal Panel? CountBox => _countBox;

    /// <summary>The status word on screen.</summary>
    internal string? StatusText => _shownStatus;

    /// <summary>Whether the clock is wanted (it may still be held off screen).</summary>
    internal bool IsClockActive => _clock.IsActive;

    /// <summary>
    /// Adds one arrival to the map. It pings in now and fades out after six seconds, alongside the arrivals in
    /// <see cref="Visitors"/>. An empty id gets one of its own.
    /// </summary>
    public void PushVisitor(LiveVisitorsPing ping)
    {
        ArgumentNullException.ThrowIfNull(ping);
        if (string.IsNullOrEmpty(ping.Id))
        {
            ping.Id = "pushed-" + (++_serial).ToString(CultureInfo.InvariantCulture);
        }
        _pushedVisitors.RemoveAll(p => p.Ping.Id == ping.Id);
        _pushedVisitors.Add((ping, _time));
        UpdateClock();
        Refresh(instant: false);
    }

    /// <summary>Adds one arrival at a latitude and longitude to the map; it fades out after six seconds.</summary>
    public void PushVisitor(double latitude, double longitude, LiveVisitorsPingKind kind = LiveVisitorsPingKind.Visit) =>
        PushVisitor(new LiveVisitorsPing(string.Empty, latitude, longitude, kind));

    /// <summary>
    /// Adds one conversion to the ticker, alongside those in <see cref="Events"/>. With a position it also pings on the map
    /// with a stronger ring and labels its city for 2s.
    /// </summary>
    public void PushEvent(LiveVisitorsEvent conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);
        if (string.IsNullOrEmpty(conversion.Id))
        {
            conversion.Id = "pushed-" + (++_serial).ToString(CultureInfo.InvariantCulture);
        }
        _pushedEvents.RemoveAll(e => e.Id == conversion.Id);
        _pushedEvents.Insert(0, conversion);
        if (_pushedEvents.Count > 24)
        {
            _pushedEvents.RemoveAt(_pushedEvents.Count - 1);
        }
        if (conversion.Latitude is { } lat && conversion.Longitude is { } lon)
        {
            _pushedVisitors.Add((new LiveVisitorsPing(conversion.Id, lat, lon, LiveVisitorsPingKind.Event), _time));
        }
        UpdateClock();
        Refresh(instant: false);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pause is not null)
        {
            _pause.IsCheckedChanged -= OnPauseChanged;
        }
        _statusDot = e.NameScope.Find<LiveVisitorsStatusDot>(StatusDotPart);
        _statusBox = e.NameScope.Find<Panel>(StatusBoxPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _pause = e.NameScope.Find<LiveVisitorsPauseButton>(PausePart);
        _countBox = e.NameScope.Find<Panel>(CountBoxPart);
        _digits = e.NameScope.Find<RollingDigits>(DigitsPart);
        if (_digits is not null)
        {
            _digits.WheelMotion = CountRoll;
        }
        _countPlaceholder = e.NameScope.Find<LiveVisitorsPlaceholder>(CountPlaceholderPart);
        _countLabel = e.NameScope.Find<TextBlock>(CountLabelPart);
        _map = e.NameScope.Find<LiveVisitorsMap>(MapPart);
        _pagesHeading = e.NameScope.Find<TextBlock>(PagesHeadingPart);
        _pages = e.NameScope.Find<LiveVisitorsRowList>(PagesPart);
        _eventsHeading = e.NameScope.Find<TextBlock>(EventsHeadingPart);
        _events = e.NameScope.Find<LiveVisitorsRowList>(EventsPart);
        _announcer = e.NameScope.Find<ExplainerAnnouncer>(AnnouncerPart);
        if (_pause is not null)
        {
            _pause.IsChecked = IsPaused;
            _pause.IsCheckedChanged += OnPauseChanged;
        }
        _shownStatus = null;
        _shownEvents.Clear();
        _shown = false;
        UpdatePseudoClasses();
        ApplyLabels();
        Refresh(instant: true);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPausedProperty)
        {
            UpdatePseudoClasses();
            if (_pause is not null && _pause.IsChecked != IsPaused)
            {
                _syncingPause = true;
                _pause.IsChecked = IsPaused;
                _syncingPause = false;
            }
            _announcer?.Announce(IsPaused ? Labels.PausedAnnouncement : Labels.ResumedAnnouncement);
            ApplyLabels();
            UpdateClock();
            if (!IsPaused)
            {
                Refresh(instant: false);
            }
        }
        else if (change.Property == ConnectionProperty)
        {
            UpdatePseudoClasses();
            UpdateClock();
            Refresh(instant: false);
        }
        else if (change.Property == CountProperty || change.Property == SimulationProperty)
        {
            UpdatePseudoClasses();
            UpdateClock();
            Refresh(instant: false);
        }
        else if (change.Property == PagesProperty || change.Property == VisitorsProperty || change.Property == EventsProperty
                 || change.Property == MaxPagesProperty || change.Property == MaxEventsProperty)
        {
            UpdateClock();
            Refresh(instant: false);
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            _shownEvents.Clear();
            Refresh(instant: false);
        }
        else if (change.Property == IsPauseVisibleProperty)
        {
            ApplyLabels();
        }
        else if (change.Property == NarrowWidthProperty)
        {
            UpdateNarrow(Bounds.Width);
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateNarrow(e.NewSize.Width);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateClock();
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // The live region says once that what it shows is not real traffic.
        if (IsSimulated && !_announcedSimulation && _announcer is not null)
        {
            _announcedSimulation = true;
            _announcer.Announce(Labels.SimulatedAnnouncement);
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new LiveVisitorsPeer(this);

    private void UpdateNarrow(double width)
    {
        if (width > 0)
        {
            PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(PausedPseudoClass, IsPaused);
        PseudoClasses.Set(ConnectingPseudoClass, Connection == LiveVisitorsConnection.Connecting);
        PseudoClasses.Set(ReconnectingPseudoClass, Connection == LiveVisitorsConnection.Reconnecting);
        PseudoClasses.Set(SimulatedPseudoClass, IsSimulated);
        PseudoClasses.Set(EmptyPseudoClass, IsEmpty);
        if (_statusDot is not null)
        {
            _statusDot.IsPulsing = Connection == LiveVisitorsConnection.Live && !IsPaused && !IsEmpty;
        }
    }

    private void OnPauseChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingPause || _pause is null)
        {
            return;
        }
        var paused = _pause.IsChecked == true;
        if (paused == IsPaused)
        {
            return;
        }
        SetCurrentValue(IsPausedProperty, paused);
        RaiseEvent(new LiveVisitorsPausedChangedEventArgs(PausedChangedEvent, paused));
    }

    /// <summary>The clock runs while not paused and there is something to play: the simulation, times to age, arrivals to expire.</summary>
    private void UpdateClock()
    {
        var wanted = !IsPaused && Connection != LiveVisitorsConnection.Connecting
                     && (IsSimulated || _pushedVisitors.Count > 0 || _pushedEvents.Count > 0 || Events is { Count: > 0 });
        if (wanted)
        {
            _clock.Start();
        }
        else
        {
            _clock.Stop();
        }
    }

    private bool Tick(double dt)
    {
        _time += dt;
        _sinceTimes += dt;
        var changed = false;
        // Reconnecting keeps the last data, so the simulated stream holds too (the ticker's times still age).
        if (IsSimulated && Connection == LiveVisitorsConnection.Live)
        {
            changed |= Simulation!.Advance(TimeSpan.FromSeconds(dt));
        }
        var before = _pushedVisitors.Count;
        _pushedVisitors.RemoveAll(p => _time - p.Born > PushedVisitorLifetime);
        changed |= before != _pushedVisitors.Count;
        if (changed)
        {
            Refresh(instant: false);
        }
        if (_sinceTimes >= 1)
        {
            _sinceTimes = 0;
            RefreshTimes();
        }
        return true;
    }

    /// <summary>Plays <paramref name="seconds"/> of the card's clock, as if frames had passed (tests).</summary>
    internal void Advance(double seconds)
    {
        const double frame = 1.0 / 60;
        var left = seconds;
        while (left > 1e-9)
        {
            var step = Math.Min(frame * 6, left);
            Tick(step);
            _map?.Advance(step);
            left -= step;
        }
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.Title);
        if (_pagesHeading is not null)
        {
            _pagesHeading.Text = labels.PagesHeading;
        }
        if (_eventsHeading is not null)
        {
            _eventsHeading.Text = labels.EventsHeading;
        }
        if (_pages is not null)
        {
            _pages.EmptyText = labels.NoPages;
        }
        if (_events is not null)
        {
            _events.EmptyText = labels.NoEvents;
        }
        if (_pause is not null)
        {
            var name = IsPaused ? labels.Resume : labels.Pause;
            AutomationProperties.SetName(_pause, name);
            Tooltip.SetTip(_pause, name);
            _pause.IsVisible = IsPauseVisible;
        }
        if (_statusBox is not null)
        {
            // Every status word sits invisibly under the visible one, so the box keeps the widest word's width and the
            // crossfade never moves anything.
            var words = new[] { labels.Live, labels.Paused, labels.Connecting, labels.Reconnecting, labels.NoData };
            for (var i = _statusBox.Children.Count - 1; i >= 0; i--)
            {
                if (_statusBox.Children[i] is TextBlock { Tag: "reserve" })
                {
                    _statusBox.Children.RemoveAt(i);
                }
            }
            foreach (var word in words)
            {
                var reserve = new TextBlock { Text = word, Opacity = 0, Tag = "reserve", IsHitTestVisible = false };
                AutomationProperties.SetAccessibilityView(reserve, AccessibilityView.Raw);
                _statusBox.Children.Insert(0, reserve);
            }
        }
        _shownStatus = null;
        UpdateStatus();
    }

    /// <summary>The status word: Paused while paused, No data without data or a simulation, otherwise the stream's state.</summary>
    internal string DisplayedStatus => IsPaused
        ? Labels.Paused
        : IsEmpty
        ? Labels.NoData
        : Connection switch
        {
            LiveVisitorsConnection.Connecting => Labels.Connecting,
            LiveVisitorsConnection.Reconnecting => Labels.Reconnecting,
            _ => Labels.Live,
        };

    private void UpdateStatus()
    {
        var word = DisplayedStatus;
        if (_status is null || _shownStatus == word)
        {
            return;
        }
        _shownStatus = word;
        _status.Content = new TextBlock { Text = word };
    }

    /// <summary>The pages the card lists: busiest first (ties by path), at most <see cref="MaxPages"/>.</summary>
    internal IReadOnlyList<LiveVisitorsPage> DisplayedPages()
    {
        var source = IsSimulated ? Simulation!.Pages : Pages ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pages = new List<LiveVisitorsPage>(source.Count);
        foreach (var page in source)
        {
            if (page is not null && page.Visitors > 0 && seen.Add(page.Path ?? string.Empty))
            {
                pages.Add(page);
            }
        }
        pages.Sort((a, b) => b.Visitors != a.Visitors ? b.Visitors.CompareTo(a.Visitors) : string.CompareOrdinal(a.Path, b.Path));
        return pages.Count > MaxPages ? pages.GetRange(0, MaxPages) : pages;
    }

    /// <summary>The conversions the card lists: newest first, each id once, at most <see cref="MaxEvents"/>.</summary>
    internal IReadOnlyList<LiveVisitorsEvent> DisplayedEvents()
    {
        var source = IsSimulated ? Simulation!.Events : Events ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<LiveVisitorsEvent>(source.Count + _pushedEvents.Count);
        foreach (var conversion in _pushedEvents.Concat(source))
        {
            if (conversion is not null && seen.Add(conversion.Id ?? string.Empty))
            {
                events.Add(conversion);
            }
        }
        // A stable sort, so events with the same time keep the order they were given in.
        var ordered = events.Select((e, i) => (e, i)).OrderByDescending(p => p.e.At).ThenBy(p => p.i).Select(p => p.e).ToList();
        return ordered.Count > MaxEvents ? ordered.GetRange(0, MaxEvents) : ordered;
    }

    /// <summary>The arrivals on the map: those passed or simulated, and those pushed in the last six seconds.</summary>
    internal IReadOnlyList<LiveVisitorsPing> DisplayedVisitors()
    {
        var source = IsSimulated ? Simulation!.Visitors : Visitors ?? [];
        var pings = new List<LiveVisitorsPing>(source.Count + _pushedVisitors.Count);
        foreach (var ping in source)
        {
            if (ping is not null)
            {
                pings.Add(ping);
            }
        }
        foreach (var (ping, _) in _pushedVisitors)
        {
            pings.Add(ping);
        }
        return pings;
    }

    /// <summary>The number the card shows.</summary>
    internal int DisplayedCount => Math.Max(0, IsSimulated ? Simulation!.Count : Count ?? 0);

    /// <summary>
    /// Brings every part in line with the data. While paused the card keeps its last snapshot: only the first frame is
    /// drawn, so a card that starts paused still shows something.
    /// </summary>
    private void Refresh(bool instant)
    {
        UpdateStatus();
        if (_pages is null || _events is null)
        {
            return;
        }
        if (IsPaused && _shown)
        {
            return;
        }
        var first = !_shown;
        _shown = true;
        instant |= first;
        var labels = Labels;
        // With nothing to show (no data and no simulation) the card holds the same placeholders as while connecting.
        var connecting = Connection == LiveVisitorsConnection.Connecting || IsEmpty;

        var count = DisplayedCount;
        if (_digits is not null)
        {
            _digits.IsVisible = !connecting;
            _digits.IsInstant = instant;
            if (_digits.Value != count)
            {
                _digits.Value = count;
            }
        }
        if (_countPlaceholder is not null)
        {
            _countPlaceholder.IsVisible = connecting;
        }
        var words = labels.People(count);
        if (_countLabel is not null && _countLabel.Text != words)
        {
            _countLabel.Text = words;
        }
        if (_countBox is not null)
        {
            // The count is read once as plain text; the rolling digits are hidden from screen readers.
            AutomationProperties.SetName(_countBox,
                connecting ? DisplayedStatus : count.ToString("N0", CultureInfo.InvariantCulture) + " " + words);
        }

        _pages.IsPlaceholder = connecting;
        _events.IsPlaceholder = connecting;
        if (connecting)
        {
            _map?.Clear();
            return;
        }

        var pages = DisplayedPages();
        var busiest = pages.Count > 0 ? Math.Max(1, pages[0].Visitors) : 1;
        var byPath = pages.ToDictionary(p => p.Path ?? string.Empty, StringComparer.Ordinal);
        _pages.Capacity = MaxPages;
        _pages.Sync(pages.Select(p => p.Path ?? string.Empty).ToArray(), key => new LiveVisitorsPageRow { Tag = key }, (view, _) =>
        {
            if (view is LiveVisitorsPageRow row && byPath.TryGetValue(row.Tag as string ?? string.Empty, out var page))
            {
                row.Show(page.Path ?? string.Empty, page.Visitors, (double)page.Visitors / busiest, !instant);
                AutomationProperties.SetName(row, labels.PageSentence(page.Path ?? string.Empty, page.Visitors));
            }
        }, instant);
        var events = DisplayedEvents();
        _shownEvents.Clear();
        foreach (var conversion in events)
        {
            _shownEvents[conversion.Id ?? string.Empty] = conversion;
        }
        _events.Capacity = MaxEvents;
        _events.Sync(events.Select(e => e.Id ?? string.Empty).ToArray(), key => new LiveVisitorsEventRow { Tag = key },
            (view, _) => ShowEvent(view), instant);

        if (_map is not null)
        {
            (string, string, double, double)? label = null;
            foreach (var conversion in events)
            {
                if (conversion.Latitude is { } lat && conversion.Longitude is { } lon)
                {
                    label = (conversion.Id ?? string.Empty, conversion.City ?? string.Empty, lat, lon);
                }
                break;
            }
            _map.Sync(DisplayedVisitors(), label);
        }
    }

    private void ShowEvent(Control view)
    {
        if (view is not LiveVisitorsEventRow row || row.Tag is not string key || !_shownEvents.TryGetValue(key, out var conversion))
        {
            return;
        }
        var labels = Labels;
        var ago = Now() - conversion.At;
        row.Show(conversion.Label ?? string.Empty, conversion.City ?? string.Empty, conversion.Country ?? string.Empty,
            conversion.Flag, labels.Ago(ago));
        AutomationProperties.SetName(row, labels.EventSentence(conversion.Label ?? string.Empty, conversion.City ?? string.Empty,
            labels.CountryName(conversion.Country ?? string.Empty), labels.AgoSpoken(ago)));
    }

    /// <summary>Ages the ticker's times once a second (only the time column and the row's name change).</summary>
    private void RefreshTimes()
    {
        if (_events is null || IsPaused)
        {
            return;
        }
        foreach (var row in _events.Rows)
        {
            ShowEvent(row);
        }
    }

    private sealed class LiveVisitorsPeer(LiveVisitors owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}
