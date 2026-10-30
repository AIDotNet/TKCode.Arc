using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The dotted world map of a <see cref="LiveVisitors"/> card, drawn by one control: the land is one cached geometry of
/// round dots in grid units drawn under a scale transform (built once for the whole app), and the pings are a handful of
/// circles. A new arrival fades in with one ring that expands and fades over 900ms (a conversion gets a larger dot and a
/// stronger ring); an arrival that is no longer passed fades out over 400ms; the newest conversion with a position gets a
/// small city label for 2s. Its clock is a <see cref="LiveLoop"/> that runs only while something moves and pauses off
/// screen. Reduced motion keeps the fades and the label but drops the rings; Off changes everything at once. Decorative:
/// screen readers skip it, the same information is in the lists.
/// </summary>
internal sealed class LiveVisitorsMap : Control
{
    /// <summary>The ring's life (s).</summary>
    internal const double RingDuration = 0.9;

    /// <summary>A dot's fade in (s).</summary>
    internal const double EnterDuration = 0.16;

    /// <summary>An expired dot's fade out (s).</summary>
    internal const double ExitDuration = 0.4;

    /// <summary>How long a conversion's city label stays (s).</summary>
    internal const double LabelDuration = 2;

    /// <summary>The label's fades (s).</summary>
    internal const double LabelFade = 0.16;

    /// <summary>The width the token sizes (dots, rings, the label) are given for; they scale with the map below it.</summary>
    internal const double ReferenceWidth = 600;

    public static readonly StyledProperty<IBrush?> LandBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, IBrush?>(nameof(LandBrush));

    public static readonly StyledProperty<IBrush?> PingBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, IBrush?>(nameof(PingBrush));

    public static readonly StyledProperty<IBrush?> LabelBackgroundProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, IBrush?>(nameof(LabelBackground));

    public static readonly StyledProperty<IBrush?> LabelBorderBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, IBrush?>(nameof(LabelBorderBrush));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LiveVisitorsMap>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<LiveVisitorsMap>();

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(LabelFontSize), 11);

    public static readonly StyledProperty<double> PingDotProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(PingDot), 2.5);

    public static readonly StyledProperty<double> EventPingDotProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(EventPingDot), 3.5);

    public static readonly StyledProperty<double> RingReachProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(RingReach), 12);

    public static readonly StyledProperty<double> EventRingReachProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(EventRingReach), 18);

    public static readonly StyledProperty<double> RingThicknessProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(RingThickness), 1.25);

    public static readonly StyledProperty<double> EventRingThicknessProperty =
        AvaloniaProperty.Register<LiveVisitorsMap, double>(nameof(EventRingThickness), 2);

    private readonly List<Ping> _pings = [];
    private readonly Dictionary<string, Ping> _byId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly LiveLoop _loop;
    private Pen? _ringPen;
    private Pen? _eventRingPen;
    private Pen? _labelPen;
    private TextLayout? _labelText;
    private string? _labelId;
    private Point _labelAt;
    private double _labelBorn = double.NegativeInfinity;
    private double _time;
    private bool _synced;

    static LiveVisitorsMap()
    {
        AffectsRender<LiveVisitorsMap>(LandBrushProperty, PingBrushProperty, LabelBackgroundProperty, LabelBorderBrushProperty,
            ForegroundProperty, PingDotProperty, EventPingDotProperty, RingReachProperty, EventRingReachProperty);
    }

    public LiveVisitorsMap()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        // Off still needs the clock: the 2s label and the removal of expired dots are information, not decoration.
        _loop = new LiveLoop(this, Step) { MinimumLevel = ArcMotionLevel.Off };
    }

    public IBrush? LandBrush { get => GetValue(LandBrushProperty); set => SetValue(LandBrushProperty, value); }

    public IBrush? PingBrush { get => GetValue(PingBrushProperty); set => SetValue(PingBrushProperty, value); }

    public IBrush? LabelBackground { get => GetValue(LabelBackgroundProperty); set => SetValue(LabelBackgroundProperty, value); }

    public IBrush? LabelBorderBrush { get => GetValue(LabelBorderBrushProperty); set => SetValue(LabelBorderBrushProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }

    public double PingDot { get => GetValue(PingDotProperty); set => SetValue(PingDotProperty, value); }

    public double EventPingDot { get => GetValue(EventPingDotProperty); set => SetValue(EventPingDotProperty, value); }

    public double RingReach { get => GetValue(RingReachProperty); set => SetValue(RingReachProperty, value); }

    public double EventRingReach { get => GetValue(EventRingReachProperty); set => SetValue(EventRingReachProperty, value); }

    public double RingThickness { get => GetValue(RingThicknessProperty); set => SetValue(RingThicknessProperty, value); }

    public double EventRingThickness { get => GetValue(EventRingThicknessProperty); set => SetValue(EventRingThicknessProperty, value); }

    /// <summary>The ids of the dots on the map, leaving ones included (tests).</summary>
    internal IReadOnlyList<string> PingIds => _pings.Select(p => p.Id).ToArray();

    /// <summary>The ids of the dots that are fading out (tests).</summary>
    internal IReadOnlyList<string> LeavingIds => _pings.Where(p => p.Left is not null).Select(p => p.Id).ToArray();

    /// <summary>The city the map labels right now, or null (tests).</summary>
    internal string? LabelText => _labelText is not null && _time - _labelBorn < LabelDuration ? _labelCity : null;

    /// <summary>Whether the map's clock is delivering frames (tests).</summary>
    internal bool IsClockRunning => _loop.IsRunning;

    private string? _labelCity;

    /// <summary>
    /// Brings the dots in line with <paramref name="pings"/>: new ids ping in, missing ids fade out. With
    /// <paramref name="label"/> (the newest conversion with a position) the map labels its city for 2s, once per id.
    /// The first sync shows everything at rest.
    /// </summary>
    public void Sync(IReadOnlyList<LiveVisitorsPing> pings, (string Id, string City, double Lat, double Lon)? label)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        var instant = !_synced || !IsLoaded || level == ArcMotionLevel.Off;
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ping in pings)
        {
            if (string.IsNullOrEmpty(ping.Id) || !wanted.Add(ping.Id))
            {
                continue;
            }
            var at = LiveVisitorsLand.Project(ping.Latitude, ping.Longitude);
            if (_byId.TryGetValue(ping.Id, out var known))
            {
                known.At = at;
                known.Kind = ping.Kind;
                known.Left = null;
                continue;
            }
            var fresh = new Ping(ping.Id, at, ping.Kind, instant ? double.NegativeInfinity : _time);
            _pings.Add(fresh);
            _byId[ping.Id] = fresh;
        }
        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            var ping = _pings[i];
            if (wanted.Contains(ping.Id) || ping.Left is not null)
            {
                continue;
            }
            if (instant)
            {
                _pings.RemoveAt(i);
                _byId.Remove(ping.Id);
            }
            else
            {
                ping.Left = _time;
            }
        }
        if (label is { } l && _seen.Add(l.Id))
        {
            if (_synced)
            {
                ShowLabel(l.City, LiveVisitorsLand.Project(l.Lat, l.Lon), l.Id);
            }
            if (_seen.Count > 256)
            {
                _seen.Clear();
                _seen.Add(l.Id);
            }
        }
        _synced = true;
        Wake();
        InvalidateVisual();
    }

    /// <summary>Clears the dots and the label (placeholders while connecting).</summary>
    public void Clear()
    {
        _pings.Clear();
        _byId.Clear();
        _labelText?.Dispose();
        _labelText = null;
        _labelCity = null;
        _labelId = null;
        _synced = false;
        InvalidateVisual();
    }

    /// <summary>Plays <paramref name="seconds"/> of the map's clock (tests drive it this way).</summary>
    internal void Advance(double seconds) => Step(seconds);

    private void ShowLabel(string city, Point at, string id)
    {
        if (string.IsNullOrEmpty(city))
        {
            return;
        }
        _labelText?.Dispose();
        _labelText = new TextLayout(city, new Typeface(FontFamily, FontStyle.Normal, FontWeight.Medium), LabelFontSize, Foreground);
        _labelCity = city;
        _labelId = id;
        _labelAt = at;
        _labelBorn = _time;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PingBrushProperty || change.Property == RingThicknessProperty || change.Property == EventRingThicknessProperty)
        {
            _ringPen = null;
            _eventRingPen = null;
        }
        else if (change.Property == LabelBorderBrushProperty)
        {
            _labelPen = null;
        }
        else if (change.Property == ForegroundProperty && _labelText is not null && _labelCity is not null && _labelId is not null)
        {
            var born = _labelBorn;
            ShowLabel(_labelCity, _labelAt, _labelId);
            _labelBorn = born;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Wake();
    }

    private void Wake()
    {
        if (IsBusy())
        {
            _loop.Start();
        }
    }

    private bool IsBusy()
    {
        if (_labelText is not null && _time - _labelBorn < LabelDuration)
        {
            return true;
        }
        foreach (var ping in _pings)
        {
            if (ping.Left is not null || _time - ping.Born < RingDuration)
            {
                return true;
            }
        }
        return false;
    }

    private bool Step(double dt)
    {
        _time += dt;
        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            if (_pings[i].Left is { } left && _time - left >= ExitDuration)
            {
                _byId.Remove(_pings[i].Id);
                _pings.RemoveAt(i);
            }
        }
        if (_labelText is not null && _time - _labelBorn >= LabelDuration)
        {
            _labelText.Dispose();
            _labelText = null;
            _labelCity = null;
        }
        InvalidateVisual();
        return IsBusy();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : ReferenceWidth;
        return new Size(width, width * LiveVisitorsLand.Aspect);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        var scale = Math.Min(bounds.Width / LiveVisitorsLand.Columns, bounds.Height / LiveVisitorsLand.RowCount);
        var offset = new Point((bounds.Width - scale * LiveVisitorsLand.Columns) / 2, (bounds.Height - scale * LiveVisitorsLand.RowCount) / 2);
        if (LandBrush is { } land)
        {
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset.X, offset.Y)))
            {
                context.DrawGeometry(land, null, LiveVisitorsLand.Geometry);
            }
        }
        if (PingBrush is not { } brush || _pings.Count == 0 && _labelText is null)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var size = Math.Clamp(bounds.Width / ReferenceWidth, 0.55, 1.2);
        _ringPen ??= new Pen(brush, RingThickness);
        _eventRingPen ??= new Pen(brush, EventRingThickness);
        foreach (var ping in _pings)
        {
            var center = new Point(offset.X + ping.At.X * scale, offset.Y + ping.At.Y * scale);
            var age = _time - ping.Born;
            var isEvent = ping.Kind == LiveVisitorsPingKind.Event;
            var opacity = level == ArcMotionLevel.Off ? 1 : Math.Clamp(age / EnterDuration, 0, 1);
            if (ping.Left is { } left)
            {
                opacity *= level == ArcMotionLevel.Off ? 0 : 1 - Math.Clamp((_time - left) / ExitDuration, 0, 1);
            }
            if (opacity <= 0)
            {
                continue;
            }
            var dot = (isEvent ? EventPingDot : PingDot) * size;
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(brush, null, center, dot, dot);
            }
            if (level == ArcMotionLevel.Full && age >= 0 && age < RingDuration)
            {
                var t = age / RingDuration;
                var eased = 1 - Math.Pow(1 - t, 3);
                var reach = dot + ((isEvent ? EventRingReach : RingReach) * size - dot) * eased;
                using (context.PushOpacity((isEvent ? 0.85 : 0.55) * (1 - t) * opacity))
                {
                    context.DrawEllipse(null, isEvent ? _eventRingPen : _ringPen, center, reach, reach);
                }
            }
        }
        DrawLabel(context, offset, scale, size, level, bounds);
    }

    private void DrawLabel(DrawingContext context, Point offset, double scale, double size, ArcMotionLevel level, Rect bounds)
    {
        if (_labelText is not { } text)
        {
            return;
        }
        var age = _time - _labelBorn;
        if (age >= LabelDuration)
        {
            return;
        }
        var opacity = level == ArcMotionLevel.Off
            ? 1
            : Math.Clamp(Math.Min(age / LabelFade, (LabelDuration - age) / LabelFade), 0, 1);
        var anchor = new Point(offset.X + _labelAt.X * scale, offset.Y + _labelAt.Y * scale);
        var padX = 6.0;
        var padY = 2.0;
        var width = text.Width + padX * 2;
        var height = text.Height + padY * 2;
        var gap = EventPingDot * size + 6;
        var x = Math.Clamp(anchor.X - width / 2, 0, Math.Max(0, bounds.Width - width));
        var y = anchor.Y - gap - height;
        if (y < 0)
        {
            y = anchor.Y + gap;
        }
        var rect = new Rect(x, y, width, height);
        _labelPen ??= LabelBorderBrush is { } border ? new Pen(border, 1) : null;
        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(LabelBackground, _labelPen, new RoundedRect(rect, height / 2));
            text.Draw(context, new Point(x + padX, y + padY));
        }
    }

    private sealed class Ping(string id, Point at, LiveVisitorsPingKind kind, double born)
    {
        public string Id { get; } = id;

        public Point At { get; set; } = at;

        public LiveVisitorsPingKind Kind { get; set; } = kind;

        public double Born { get; } = born;

        public double? Left { get; set; }
    }
}
