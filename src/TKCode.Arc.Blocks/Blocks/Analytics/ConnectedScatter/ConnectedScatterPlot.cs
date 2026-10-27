using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>What the plot needs from its block besides the points: the axes, the target line, the texts and the formats.</summary>
internal sealed class ScatterPlotOptions
{
    public double XMin { get; set; }

    public double XMax { get; set; } = 1;

    public double XStep { get; set; } = 0.2;

    public double YMin { get; set; }

    public double YMax { get; set; } = 1;

    public double YStep { get; set; } = 0.2;

    /// <summary>The target line's slope; NaN leaves out the line, the zone and their labels.</summary>
    public double TargetRatio { get; set; } = double.NaN;

    public string XName { get; set; } = "X";

    public string YName { get; set; } = "Y";

    public string RatioName { get; set; } = "Ratio";

    public string TargetLabel { get; set; } = string.Empty;

    public string ZoneLabel { get; set; } = string.Empty;

    public Func<double, string> Value { get; set; } = ConnectedScatterEngine.DefaultValue;

    public Func<double, string> Ratio { get; set; } = ConnectedScatterEngine.DefaultRatio;

    public Func<double, string>? XTick { get; set; }

    public Func<double, string>? YTick { get; set; }
}

/// <summary>A label written beside a point (the first month, the last month or a note).</summary>
internal readonly record struct ScatterNoteSource(int Index, string Text, ScatterLabelSide Side);

/// <summary>
/// The chart of a <see cref="ConnectedScatter"/> drawn by one control: the healthy zone, grid and ticks, the target line,
/// the guides to the axes, the dashed future path (ghost) and the solid trail, the direction arrows, the monthly dots,
/// the labels, the head point and the hover card. The block owns the head position (<see cref="SetPosition"/>); the plot
/// owns the point morph between series (one spring per coordinate), the fades and the card.
/// </summary>
/// <remarks>
/// Code-driven motion: every moving part is a coordinate inside one geometry (the curve is rebuilt from the morphing
/// points each frame, the trail is cut at the head, the dots and arrows follow the curve), so a ControlTheme transition
/// cannot express it. One <see cref="LiveLoop"/> advances the fades and the card; the morph runs on a spring array.
/// </remarks>
internal sealed class ConnectedScatterPlot : Control
{
    #region Geometry properties (from the token)

    public static readonly StyledProperty<Thickness> PlotInsetProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, Thickness>(nameof(PlotInset), new Thickness(44, 14, 16, 30));
    public static readonly StyledProperty<double> WideHeightProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, double>(nameof(WideHeight), 380);
    public static readonly StyledProperty<double> MediumHeightProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, double>(nameof(MediumHeight), 330);
    public static readonly StyledProperty<double> CompactHeightProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, double>(nameof(CompactHeight), 300);
    public static readonly StyledProperty<double> CardWidthProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, double>(nameof(CardWidth), 176);
    public static readonly StyledProperty<double> CardGapProperty =
        AvaloniaProperty.Register<ConnectedScatterPlot, double>(nameof(CardGap), 14);

    public Thickness PlotInset { get => GetValue(PlotInsetProperty); set => SetValue(PlotInsetProperty, value); }
    public double WideHeight { get => GetValue(WideHeightProperty); set => SetValue(WideHeightProperty, value); }
    public double MediumHeight { get => GetValue(MediumHeightProperty); set => SetValue(MediumHeightProperty, value); }
    public double CompactHeight { get => GetValue(CompactHeightProperty); set => SetValue(CompactHeightProperty, value); }
    public double CardWidth { get => GetValue(CardWidthProperty); set => SetValue(CardWidthProperty, value); }
    public double CardGap { get => GetValue(CardGapProperty); set => SetValue(CardGapProperty, value); }

    #endregion

    // Measured marks (px): they do not change with the theme.
    private const double FutureDot = 3;
    private const double PastDot = 3.25;
    private const double DotStroke = 1.5;
    private const double DotHalo = 11;
    private const double HeadDot = 6;
    private const double HeadStroke = 2.5;
    private const double HeadHalo = 13;
    private const double HeadHaloGrown = 1.25;
    private const double GhostWidth = 1.5;
    private const double TrailWidth = 2.25;
    private const double TargetWidth = 1.25;
    private const double LabelSize = 12;
    private const double HaloStroke = 2;
    private const int Samples = 24;

    private static readonly StreamGeometry s_arrow = BuildArrow();

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _text = new(512);
    private readonly LiveLoop _loop;
    private readonly AnalyticsSpringArray _morph;
    private readonly AnalyticsCatmullRom _curve = new();
    private readonly ScatterCard _card = new();
    // A StreamGeometry is written once (re-opening one keeps drawing its old figures): a changed path gets a new object.
    private StreamGeometry _ghostGeometry = new();
    private StreamGeometry _trailGeometry = new();
    private StreamGeometry _zoneGeometry = new();

    private ScatterPlotOptions _options = new();
    private string[] _labels = [];
    private double[] _targetX = [];
    private double[] _targetY = [];
    private Point[] _pixels = [];
    private Point[] _targetPixels = [];
    private bool[] _hasArrow = [];
    private int _count;
    private ScatterNoteSource[] _notes = [];
    private ScatterAnnotation[] _annotations = [];
    private ScatterAnnotation[] _previousAnnotations = [];
    private double[] _xTicks = [];
    private double[] _yTicks = [];
    private double _u;
    private int _hover = -1;
    private AnalyticsTween[] _halo = [];
    private AnalyticsTween _reveal = new(1);
    private AnalyticsTween _labelIn = new(1);
    private AnalyticsTween _labelOut = new(0);
    private AnalyticsTween _headScale = new(1);
    private bool _touchHold;
    private bool _geometryDirty = true;
    private bool _trailDirty = true;
    private bool _annotationsDirty = true;
    private double _trailU = double.NaN;
    private Size _geometrySize;
    private int _paletteVersion = -1;
    private bool _dark;
    private Point _zoneFrom;
    private Point _zoneTo;
    private bool _hasTarget;
    private double[] _scratch = [];

    static ConnectedScatterPlot()
    {
        AffectsMeasure<ConnectedScatterPlot>(WideHeightProperty, MediumHeightProperty, CompactHeightProperty);
        AffectsRender<ConnectedScatterPlot>(PlotInsetProperty, CardWidthProperty, CardGapProperty);
        FocusableProperty.OverrideDefaultValue<ConnectedScatterPlot>(false);
    }

    public ConnectedScatterPlot()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        _morph = new AnalyticsSpringArray(this, AnalyticsSprings.Smooth) { RestDelta = 0.0005, RestSpeed = 0.01 };
        _morph.Changed += OnMorphChanged;
        ActualThemeVariantChanged += (_, _) => InvalidatePalette();
        ResourcesChanged += (_, _) => InvalidatePalette();
    }

    /// <summary>Raised after every frame in which the points moved (the block refreshes the readouts).</summary>
    public event Action? DisplayChanged;

    /// <summary>Raised when a point is clicked or tapped, with its index.</summary>
    public event Action<int>? PointInvoked;

    /// <summary>Months drawn.</summary>
    public int Count => _count;

    /// <summary>True while the points morph or anything fades (tests).</summary>
    internal bool IsMoving => _morph.IsMoving || _loop.IsActive;

    /// <summary>True while the points morph between two series (tests).</summary>
    internal bool IsMorphing => _morph.IsMoving;

    /// <summary>The hovered point, or −1.</summary>
    internal int HoverIndex => _hover;

    /// <summary>The card (tests).</summary>
    internal ScatterCard Card => _card;

    /// <summary>The head's month position.</summary>
    internal double Position => _u;

    /// <summary>The fade of the future path, dots, arrows and labels (0 before the draw-in lands; tests).</summary>
    internal double Reveal => _reveal.Value;

    /// <summary>The scale of the head's halo (tests).</summary>
    internal double HeadHaloScale => _headScale.Value;

    /// <summary>The annotations of the current series (tests).</summary>
    internal IReadOnlyList<ScatterAnnotation> Annotations
    {
        get
        {
            EnsureAnnotations();
            return _annotations;
        }
    }

    /// <summary>The arrows of the current series: true for segment i when it has one (tests).</summary>
    internal IReadOnlyList<bool> Arrows
    {
        get
        {
            EnsureGeometry();
            return _hasArrow;
        }
    }

    /// <summary>The plot rectangle at the current size.</summary>
    internal Rect PlotRect
    {
        get
        {
            var inset = PlotInset;
            return new Rect(inset.Left, inset.Top, Math.Max(0, Bounds.Width - inset.Left - inset.Right),
                Math.Max(0, Bounds.Height - inset.Top - inset.Bottom));
        }
    }

    /// <summary>The displayed x of month <paramref name="index"/> (data units, mid-morph values included).</summary>
    public double DisplayedX(int index) => index >= 0 && index < _count ? _morph[index] : double.NaN;

    /// <summary>The displayed y of month <paramref name="index"/>.</summary>
    public double DisplayedY(int index) => index >= 0 && index < _count ? _morph[_count + index] : double.NaN;

    /// <summary>The x and y at month position <paramref name="u"/>: linear between the two neighbouring displayed months.</summary>
    public (double X, double Y) ValuesAt(double u)
    {
        if (_count == 0)
        {
            return (double.NaN, double.NaN);
        }
        var values = _morph.Values;
        return (ConnectedScatterEngine.Interpolate(values[.._count], u), ConnectedScatterEngine.Interpolate(values[_count..(2 * _count)], u));
    }

    /// <summary>Where month <paramref name="index"/> is drawn now (tests, hit tests).</summary>
    internal Point PixelOf(int index)
    {
        EnsureGeometry();
        return index >= 0 && index < _count ? _pixels[index] : default;
    }

    /// <summary>The head point's position on the curve now.</summary>
    internal Point HeadPixel
    {
        get
        {
            EnsureGeometry();
            return _curve.PointAt(_u);
        }
    }

    #region Data

    /// <summary>Sets the axes, the target line, the texts and the formats; geometry and labels rebuild.</summary>
    public void SetOptions(ScatterPlotOptions options)
    {
        _options = options;
        _xTicks = ConnectedScatterEngine.Ticks(options.XMin, options.XMax, options.XStep);
        _yTicks = ConnectedScatterEngine.Ticks(options.YMin, options.YMax, options.YStep);
        _geometryDirty = true;
        _annotationsDirty = true;
        _text.Clear();
        _card.Invalidate();
        RefreshCard();
        InvalidateVisual();
    }

    /// <summary>
    /// Shows a series. With <paramref name="morph"/> (and the same number of months) every point springs to its new
    /// place at the same time on the Smooth spring and the label groups cross-fade one after the other (old out 180ms,
    /// new in 240ms); otherwise everything is replaced at once.
    /// </summary>
    public void SetSeries(string[] labels, double[] x, double[] y, IReadOnlyList<ScatterNoteSource> notes, bool morph)
    {
        var count = Math.Min(labels.Length, Math.Min(x.Length, y.Length));
        var sameShape = count == _count && count > 0;
        _labels = labels;
        _targetX = x;
        _targetY = y;
        if (_scratch.Length < count * 2)
        {
            _scratch = new double[count * 2];
        }
        x.AsSpan(0, count).CopyTo(_scratch);
        y.AsSpan(0, count).CopyTo(_scratch.AsSpan(count));
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = morph && sameShape && level == ArcMotionLevel.Full && this.IsAttachedToVisualTree();
        var oldAnnotations = _annotations;
        var hadLabels = !_annotationsDirty && _count > 0;
        _count = count;
        _notes = notes.ToArray();
        if (_halo.Length != count)
        {
            _halo = new AnalyticsTween[count];
        }
        if (_hover >= count)
        {
            _hover = -1;
        }
        if (animate)
        {
            _morph.SetTargets(_scratch.AsSpan(0, count * 2));
            _previousAnnotations = hadLabels ? oldAnnotations : [];
            _annotationsDirty = true;
            EnsureAnnotations();
            _labelOut.Jump(1);
            _labelOut.Start(0, ArcMotionPresets.Exit, ArcEasings.Exit);
            _labelIn.Jump(0);
            _labelIn.Start(1, ArcMotionPresets.Standard, ArcEasings.Standard, ArcMotionPresets.Exit);
            _loop.Start();
        }
        else
        {
            _morph.Jump(_scratch.AsSpan(0, count * 2));
            _previousAnnotations = [];
            _annotationsDirty = true;
            _labelOut.Jump(0);
            _labelIn.Jump(_reveal.Target >= 1 ? 1 : 0);
        }
        _geometryDirty = true;
        _trailDirty = true;
        RefreshCard();
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>The head's month position (written every frame while it moves).</summary>
    public void SetPosition(double u)
    {
        if (Math.Abs(u - _u) < 1e-9)
        {
            return;
        }
        _u = u;
        _trailDirty = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Hides (false) or shows (true) the layer that appears when the draw-in lands: the dashed future path, the hollow
    /// dots, the future arrows and the labels. With <paramref name="animate"/> they fade in over 240ms.
    /// </summary>
    public void SetReveal(bool revealed, bool animate)
    {
        var target = revealed ? 1.0 : 0.0;
        if (animate && revealed && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            _reveal.Start(1, ArcMotionPresets.Standard, ArcEasings.Standard);
            _labelIn.Start(1, ArcMotionPresets.Standard, ArcEasings.Standard);
            _loop.Start();
        }
        else
        {
            _reveal.Jump(target);
            _labelIn.Jump(target);
        }
        InvalidateVisual();
    }

    /// <summary>The head's halo grows to 1.25 while the head is played or dragged (580ms CSS spring; Full motion only, otherwise it stays at 1).</summary>
    public void SetEmphasis(bool emphasized)
    {
        var target = emphasized ? HeadHaloGrown : 1;
        if (ArcMotion.IsFull(this))
        {
            _headScale.Start(target, TimeSpan.FromMilliseconds(580), ArcEasings.CssSpring);
            _loop.Start();
        }
        else
        {
            // Reduced motion drops scale effects: the halo stays at its resting size.
            _headScale.Jump(1);
        }
        InvalidateVisual();
    }

    /// <summary>Hides the card and clears the hover.</summary>
    public void ClearHover()
    {
        SetHover(-1);
    }

    /// <summary>Recomputes the card's content and position (after the data, the head or the texts changed).</summary>
    public void RefreshCard()
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_hover < 0 || _hover >= _count || Bounds.Width <= 0)
        {
            _card.Hide(level);
            StartLoopIfNeeded();
            return;
        }
        EnsureGeometry();
        _card.SetContent(CardContentFor(_hover), CardWidth);
        _card.Show(CardPositionFor(_hover), this.IsAttachedToVisualTree() ? level : ArcMotionLevel.Off);
        StartLoopIfNeeded();
    }

    private void StartLoopIfNeeded()
    {
        if (_card.IsMoving)
        {
            _loop.Start();
        }
    }

    private ScatterCardContent CardContentFor(int index)
    {
        var x = DisplayedX(index);
        var y = DisplayedY(index);
        var ratio = ConnectedScatterEngine.Ratio(x, y);
        var healthy = double.IsNaN(_options.TargetRatio) || ConnectedScatterEngine.IsHealthy(x, y, _options.TargetRatio);
        return new ScatterCardContent(index < _labels.Length ? _labels[index] : string.Empty, _options.XName, _options.Value(x),
            _options.YName, _options.Value(y), _options.RatioName, _options.Ratio(ratio), healthy);
    }

    private Point CardPositionFor(int index)
    {
        var size = _card.Measure(_text, _palette, CardWidth);
        return AnalyticsReadout.AboveOrBelow(_pixels[index], size, new Rect(0, 0, Bounds.Width, Bounds.Height), CardGap);
    }

    #endregion

    #region Geometry

    private void OnMorphChanged()
    {
        _geometryDirty = true;
        _trailDirty = true;
        if (_hover >= 0)
        {
            RefreshCard();
        }
        InvalidateVisual();
        DisplayChanged?.Invoke();
    }

    private double PlotXPx(double value)
    {
        var rect = PlotRect;
        var span = _options.XMax - _options.XMin;
        return span > 0 ? rect.Left + (value - _options.XMin) / span * rect.Width : rect.Left;
    }

    private double PlotYPx(double value)
    {
        var rect = PlotRect;
        var span = _options.YMax - _options.YMin;
        return span > 0 ? rect.Bottom - (value - _options.YMin) / span * rect.Height : rect.Bottom;
    }

    private void EnsureGeometry()
    {
        var size = Bounds.Size;
        if (!_geometryDirty && size == _geometrySize)
        {
            return;
        }
        _geometrySize = size;
        _geometryDirty = false;
        _trailDirty = true;
        _trailU = double.NaN;
        if (_pixels.Length < _count)
        {
            _pixels = new Point[_count];
            _targetPixels = new Point[_count];
            _hasArrow = new bool[Math.Max(1, _count)];
        }
        for (var i = 0; i < _count; i++)
        {
            _pixels[i] = new Point(PlotXPx(_morph[i]), PlotYPx(_morph[_count + i]));
            _targetPixels[i] = new Point(PlotXPx(_targetX[i]), PlotYPx(_targetY[i]));
        }
        for (var i = 0; i < _count - 1; i++)
        {
            // Which segments have an arrow follows the series being shown (the target), not the points mid-morph.
            _hasArrow[i] = ConnectedScatterEngine.HasArrow(Distance(_targetPixels[i], _targetPixels[i + 1]));
        }
        _curve.SetPoints(_pixels.AsSpan(0, _count));
        _ghostGeometry = new StreamGeometry();
        using (var context = _ghostGeometry.Open())
        {
            if (_count > 0)
            {
                _curve.Write(context);
            }
        }
        BuildZone();
        _annotationsDirty = true;
    }

    private void BuildZone()
    {
        var o = _options;
        _zoneGeometry = new StreamGeometry();
        Point from = default;
        Point to = default;
        _hasTarget = !double.IsNaN(o.TargetRatio) &&
                     ConnectedScatterEngine.TryTargetSegment(o.TargetRatio, o.XMin, o.XMax, o.YMin, o.YMax, out from, out to);
        if (!_hasTarget)
        {
            using var empty = _zoneGeometry.Open();
            return;
        }
        _zoneFrom = new Point(PlotXPx(from.X), PlotYPx(from.Y));
        _zoneTo = new Point(PlotXPx(to.X), PlotYPx(to.Y));
        var polygon = ConnectedScatterEngine.HealthyPolygon(o.TargetRatio, o.XMin, o.XMax, o.YMin, o.YMax);
        using var context = _zoneGeometry.Open();
        if (polygon.Length < 3)
        {
            return;
        }
        context.BeginFigure(new Point(PlotXPx(polygon[0].X), PlotYPx(polygon[0].Y)), true);
        for (var i = 1; i < polygon.Length; i++)
        {
            context.LineTo(new Point(PlotXPx(polygon[i].X), PlotYPx(polygon[i].Y)));
        }
        context.EndFigure(true);
    }

    private void EnsureAnnotations()
    {
        if (!_annotationsDirty)
        {
            return;
        }
        EnsureGeometry();
        _annotationsDirty = false;
        var list = new List<ScatterAnnotation>(_notes.Length + 2);
        var compact = new ScatterBreakpoints(Bounds.Width).IsCompact;
        if (_count > 0)
        {
            var plotWidth = PlotRect.Width;
            var first = _count > 1 ? ConnectedScatterEngine.FirstSide(_targetPixels[0], _targetPixels[1]) : ScatterLabelSide.Left;
            list.Add(new ScatterAnnotation(0, _labels[0], first, false));
            if (_count > 1)
            {
                var last = ConnectedScatterEngine.LastSide(_targetPixels[_count - 2], _targetPixels[_count - 1], plotWidth);
                list.Add(new ScatterAnnotation(_count - 1, _labels[_count - 1], last, true));
            }
        }
        if (!compact)
        {
            foreach (var note in _notes)
            {
                if (note.Index > 0 && note.Index < _count - 1 && !string.IsNullOrEmpty(note.Text))
                {
                    list.Add(new ScatterAnnotation(note.Index, note.Text, note.Side, false));
                }
            }
        }
        _annotations = list.ToArray();
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static StreamGeometry BuildArrow()
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(3.5, 0), true);
        context.LineTo(new Point(-3.5, -3.5));
        context.LineTo(new Point(-3.5, 3.5));
        context.EndFigure(true);
        return geometry;
    }

    #endregion

    #region Frame loop

    private bool Advance(double dt)
    {
        var moving = false;
        moving |= _reveal.Advance(dt);
        moving |= _labelIn.Advance(dt);
        moving |= _labelOut.Advance(dt);
        moving |= _headScale.Advance(dt);
        for (var i = 0; i < _halo.Length; i++)
        {
            moving |= _halo[i].Advance(dt);
        }
        moving |= _card.Advance(dt);
        if (!_labelOut.IsRunning && _previousAnnotations.Length > 0 && _labelOut.Value <= 0.001)
        {
            _previousAnnotations = [];
        }
        InvalidateVisual();
        return moving;
    }

    #endregion

    #region Input

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_touchHold && e.Pointer.Type != PointerType.Touch)
        {
            return;
        }
        SetHover(HitTest(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_touchHold)
        {
            SetHover(-1);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var index = HitTest(e.GetPosition(this));
        if (e.Pointer.Type == PointerType.Touch)
        {
            // A finger has no hover: the card shows while the finger is down.
            _touchHold = true;
            SetHover(index);
        }
        if (index >= 0)
        {
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var wasTouch = _touchHold;
        var captured = ReferenceEquals(e.Pointer.Captured, this);
        if (captured)
        {
            e.Pointer.Capture(null);
        }
        var index = HitTest(e.GetPosition(this));
        if (wasTouch)
        {
            _touchHold = false;
            SetHover(-1);
        }
        if (captured && index >= 0)
        {
            PointInvoked?.Invoke(index);
        }
    }

    private int HitTest(Point position)
    {
        if (_count == 0)
        {
            return -1;
        }
        EnsureGeometry();
        return ConnectedScatterEngine.NearestPoint(_pixels.AsSpan(0, _count), position);
    }

    private void SetHover(int index)
    {
        if (index == _hover)
        {
            return;
        }
        var old = _hover;
        _hover = index;
        if (old >= 0 && old < _halo.Length)
        {
            Fade(ref _halo[old], 0);
        }
        if (index >= 0 && index < _halo.Length)
        {
            Fade(ref _halo[index], 1);
        }
        RefreshCard();
        InvalidateVisual();
    }

    private void Fade(ref AnalyticsTween tween, double target)
    {
        if (ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            tween.Jump(target);
        }
        else
        {
            tween.Start(target, ArcMotionPresets.Fast, ArcEasings.Standard);
            _loop.Start();
        }
    }

    #endregion

    #region Measure and render

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 700 : availableSize.Width;
        var breakpoints = new ScatterBreakpoints(width);
        var height = breakpoints.HasFourColumns ? WideHeight : breakpoints.IsCompact ? CompactHeight : MediumHeight;
        return new Size(width, height);
    }

    private void InvalidatePalette()
    {
        _paletteVersion = -1;
        _text.Clear();
        _card.Invalidate();
        InvalidateVisual();
    }

    private bool EnsurePalette()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return _palette.IsReady;
        }
        if (_paletteVersion >= 0 && _palette.IsReady)
        {
            return true;
        }
        _palette.Refresh(this);
        _paletteVersion = _palette.Version;
        _dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        return _palette.IsReady;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.Transparent, bounds);
        if (!EnsurePalette())
        {
            return;
        }
        EnsureGeometry();
        EnsureAnnotations();
        var p = _palette;
        var plot = PlotRect;
        var body = new Typeface(p.BodyFont);
        var medium = new Typeface(p.BodyFont, FontStyle.Normal, p.Medium);
        var compact = new ScatterBreakpoints(Bounds.Width).IsCompact;
        var reveal = Math.Clamp(_reveal.Value, 0, 1);

        // Healthy zone, grid and ticks.
        using (context.PushClip(plot))
        {
            if (_hasTarget)
            {
                context.DrawGeometry(p.Brush(p.Success, 0.06), null, _zoneGeometry);
            }
        }
        DrawGridAndTicks(context, plot, body);

        // Target line and its two labels.
        if (_hasTarget)
        {
            using (context.PushClip(plot))
            {
                context.DrawLine(p.Pen(p.TextMuted, TargetWidth, 1, 4, 4, PenLineCap.Flat), _zoneFrom, _zoneTo);
            }
            if (!compact && !string.IsNullOrEmpty(_options.TargetLabel))
            {
                DrawTargetLabel(context, body);
            }
            if (!compact && !string.IsNullOrEmpty(_options.ZoneLabel))
            {
                var zone = _text.Get(_options.ZoneLabel, body, LabelSize);
                DrawHaloText(context, zone, p.Success, 1, plot.Left + 12, plot.Top + 18, anchorEnd: false);
            }
        }

        if (_count > 0)
        {
            var head = _curve.PointAt(_u);
            using (context.PushClip(plot))
            {
                // Guides from the head to the axes.
                var guide = p.Brush(p.BorderStrong, 0.7);
                context.FillRectangle(guide, new Rect(head.X - 0.5, head.Y, 1, Math.Max(0, plot.Bottom - head.Y)));
                context.FillRectangle(guide, new Rect(plot.Left, head.Y - 0.5, Math.Max(0, head.X - plot.Left), 1));

                if (_count > 1)
                {
                    // The ghost is the whole path; the trail covers it up to the head.
                    if (reveal > 0.001)
                    {
                        context.DrawGeometry(null, p.Pen(p.BorderStrong, GhostWidth, Quantize(reveal), 2, 4), _ghostGeometry);
                    }
                    EnsureTrail();
                    context.DrawGeometry(null, p.Pen(p.Accent, TrailWidth), _trailGeometry);
                }
            }
            DrawArrows(context, reveal);
            DrawDots(context, reveal);
            DrawAnnotations(context, body, medium, compact);
            DrawHead(context, head);
        }
        _card.Draw(context, _text, _palette, CardWidth);
    }

    private static double Quantize(double alpha) => Math.Round(Math.Clamp(alpha, 0, 1) * 16) / 16;

    private void EnsureTrail()
    {
        if (!_trailDirty && _trailU.Equals(_u))
        {
            return;
        }
        _trailDirty = false;
        _trailU = _u;
        _trailGeometry = new StreamGeometry();
        using var context = _trailGeometry.Open();
        _curve.Write(context, _u);
    }

    private void DrawGridAndTicks(DrawingContext context, Rect plot, Typeface body)
    {
        var p = _palette;
        var grid = p.Brush(p.BorderSubtle);
        var muted = p.Brush(p.TextMuted);
        var yFormat = _options.YTick ?? (v => ConnectedScatterEngine.DefaultTick(v, _options.YStep, _options.Value));
        var xFormat = _options.XTick ?? (v => ConnectedScatterEngine.DefaultTick(v, _options.XStep, _options.Value));
        foreach (var tick in _yTicks)
        {
            var y = Math.Round(PlotYPx(tick)) + 0.5;
            if (y < plot.Top - 0.5 || y > plot.Bottom + 1)
            {
                continue;
            }
            context.FillRectangle(grid, new Rect(plot.Left, y - 0.5, plot.Width, 1));
            var label = _text.Get(yFormat(tick), body, LabelSize, true);
            label.DrawAtBaseline(context, muted, plot.Left - 8 - label.Width, PlotYPx(tick) + 0.32 * LabelSize + 0.5);
        }
        var compact = new ScatterBreakpoints(Bounds.Width).IsCompact;
        var indexes = ConnectedScatterEngine.XTickIndexes(_xTicks.Length, compact);
        foreach (var i in indexes)
        {
            var label = _text.Get(xFormat(_xTicks[i]), body, LabelSize, true);
            var x = PlotXPx(_xTicks[i]);
            double left;
            if (i == 0 && !compact)
            {
                left = x;
            }
            else if (i == _xTicks.Length - 1 && !compact)
            {
                left = x - label.Width;
            }
            else
            {
                left = x - label.Width / 2;
            }
            label.DrawAtBaseline(context, muted, left, plot.Bottom + 19);
        }
    }

    private void DrawTargetLabel(DrawingContext context, Typeface body)
    {
        var p = _palette;
        // The label sits 94% along the visible part of the line, 7px above it, turned with the line.
        var at = new Point(_zoneFrom.X + (_zoneTo.X - _zoneFrom.X) * 0.94, _zoneFrom.Y + (_zoneTo.Y - _zoneFrom.Y) * 0.94);
        var angle = Math.Atan2(_zoneTo.Y - _zoneFrom.Y, _zoneTo.X - _zoneFrom.X);
        var text = _text.Get(_options.TargetLabel, body, LabelSize);
        using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(at.X, at.Y)))
        {
            DrawHaloText(context, text, p.TextSecondary, 1, 0, -7, anchorEnd: true);
        }
    }

    // Text with a 4px Surface outline under it (paint-order: stroke): the text drawn in Surface around, then in colour.
    private static readonly Vector[] s_halo =
    [
        new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-0.7071, -0.7071), new(0.7071, -0.7071), new(-0.7071, 0.7071), new(0.7071, 0.7071)
    ];

    private void DrawHaloText(DrawingContext context, AnalyticsText text, Color color, double alpha, double x, double baseline, bool anchorEnd,
        bool centered = false)
    {
        var left = anchorEnd ? x - text.Width : centered ? x - text.Width / 2 : x;
        var p = _palette;
        var halo = _text.Brush(p.Surface, alpha);
        foreach (var v in s_halo)
        {
            text.DrawAtBaseline(context, halo, left + v.X * HaloStroke, baseline + v.Y * HaloStroke);
        }
        text.DrawAtBaseline(context, _text.Brush(color, alpha), left, baseline);
    }

    private void DrawArrows(DrawingContext context, double reveal)
    {
        if (_count < 2)
        {
            return;
        }
        var p = _palette;
        var past = p.Brush(p.Accent);
        for (var i = 0; i < _count - 1; i++)
        {
            if (!_hasArrow[i])
            {
                continue;
            }
            var isPast = _u >= i + 0.5;
            if (!isPast && reveal < 0.001)
            {
                continue;
            }
            var at = _curve.PointAt(i + 0.5);
            var tangent = _curve.TangentAt(i + 0.5);
            var angle = Math.Atan2(tangent.Y, tangent.X);
            using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(at.X, at.Y)))
            {
                context.DrawGeometry(isPast ? past : p.Brush(p.TextMuted, Quantize(reveal)), null, s_arrow);
            }
        }
    }

    private void DrawDots(DrawingContext context, double reveal)
    {
        var p = _palette;
        var accent = p.Brush(p.Accent);
        var surface = p.Brush(p.Surface);
        var pastPen = p.Pen(p.Surface, DotStroke);
        for (var i = 0; i < _count; i++)
        {
            var c = _pixels[i];
            if (i < _halo.Length && _halo[i].Value > 0.001)
            {
                context.DrawEllipse(p.Brush(p.Accent, Quantize(_halo[i].Value) * (_dark ? 0.13 : 0.10)), null, c, DotHalo, DotHalo);
            }
            if (_u >= i - 1e-9)
            {
                context.DrawEllipse(accent, pastPen, c, PastDot, PastDot);
            }
            else if (reveal > 0.001)
            {
                var edge = _dark ? p.TextMuted : p.BorderStrong;
                context.DrawEllipse(p.Brush(p.Surface, Quantize(reveal)), p.Pen(edge, DotStroke, Quantize(reveal)), c, FutureDot, FutureDot);
            }
        }
    }

    private void DrawAnnotations(DrawingContext context, Typeface body, Typeface medium, bool compact)
    {
        var inAlpha = Math.Clamp(_labelIn.Value, 0, 1);
        if (_previousAnnotations.Length > 0 && _labelOut.Value > 0.001)
        {
            DrawAnnotationSet(context, _previousAnnotations, body, medium, Math.Clamp(_labelOut.Value, 0, 1));
        }
        if (inAlpha > 0.001)
        {
            DrawAnnotationSet(context, _annotations, body, medium, inAlpha);
        }
    }

    private void DrawAnnotationSet(DrawingContext context, ScatterAnnotation[] set, Typeface body, Typeface medium, double alpha)
    {
        var p = _palette;
        foreach (var note in set)
        {
            if (note.Index >= _count)
            {
                continue;
            }
            var c = _pixels[note.Index];
            var text = _text.Get(note.Text, note.IsLast ? medium : body, LabelSize);
            var color = note.IsLast ? p.Foreground : p.TextSecondary;
            var a = Quantize(alpha);
            // A label that would leave the plot's area (over the axis labels, past the right edge) goes to the other side.
            var side = note.Side;
            var plotLeft = PlotRect.Left;
            if (side == ScatterLabelSide.Left && c.X - 10 - text.Width < plotLeft)
            {
                side = ScatterLabelSide.Right;
            }
            else if (side == ScatterLabelSide.Right && c.X + 10 + text.Width > Bounds.Width - 2)
            {
                side = ScatterLabelSide.Left;
            }
            switch (side)
            {
                case ScatterLabelSide.Left:
                    DrawHaloText(context, text, color, a, c.X - 10, c.Y + 4, anchorEnd: true);
                    break;
                case ScatterLabelSide.Right:
                    DrawHaloText(context, text, color, a, c.X + 10, c.Y + 4, anchorEnd: false);
                    break;
                case ScatterLabelSide.Top:
                    DrawHaloText(context, text, color, a, c.X, c.Y - 11, anchorEnd: false, centered: true);
                    break;
                default:
                    DrawHaloText(context, text, color, a, c.X, c.Y + 19, anchorEnd: false, centered: true);
                    break;
            }
        }
    }

    private void DrawHead(DrawingContext context, Point head)
    {
        var p = _palette;
        var radius = HeadHalo * _headScale.Value;
        context.DrawEllipse(p.Brush(p.Accent, _dark ? 0.13 : 0.10), null, head, radius, radius);
        context.DrawEllipse(p.Brush(p.Accent), p.Pen(p.Surface, HeadStroke), head, HeadDot, HeadDot);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new ConnectedScatterPlotPeer(this);
}

/// <summary>The plot for assistive technology: a chart group; the data is in the block's table.</summary>
internal sealed class ConnectedScatterPlotPeer(ConnectedScatterPlot owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string GetLocalizedControlTypeCore() => "chart";

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
