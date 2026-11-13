using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The plot of a <see cref="WindowRange"/>: the faded chart, the full-color chart clipped to the window, the frame (top and
/// bottom lines, two edge handles with grips), the press tint, the live dot and the focus rings. It takes every press on
/// the chart and hands it to the owner. The three focus stops are its (invisible) children.
/// </summary>
internal sealed class WindowRangePlot : Control, ICustomHitTest
{
    public static readonly StyledProperty<double> EdgeWidthProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(EdgeWidth), 12);
    public static readonly StyledProperty<double> EdgeRadiusProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(EdgeRadius), 9);
    public static readonly StyledProperty<double> BandThicknessProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(BandThickness), 2);
    public static readonly StyledProperty<double> GripWidthProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(GripWidth), 2.5);
    public static readonly StyledProperty<double> GripHeightProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(GripHeight), 16);
    public static readonly StyledProperty<double> GripOpacityProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(GripOpacity), 0.85);
    public static readonly StyledProperty<double> ChartTopInsetProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(ChartTopInset), 10);
    public static readonly StyledProperty<double> ChartBottomInsetProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(ChartBottomInset), 2);
    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(LineThickness), 1.75);
    public static readonly StyledProperty<double> MutedOpacityProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(MutedOpacity), 0.32);
    public static readonly StyledProperty<double> AreaOpacityProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(AreaOpacity), 0.14);
    public static readonly StyledProperty<double> MutedAreaOpacityProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(MutedAreaOpacity), 0.10);
    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(TintOpacity), 0.06);
    public static readonly StyledProperty<double> DotDiameterProperty =
        AvaloniaProperty.Register<WindowRangePlot, double>(nameof(DotDiameter), 8);
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<WindowRangePlot, CornerRadius>(nameof(CornerRadius), new CornerRadius(12));
    public static readonly StyledProperty<IBrush?> FrameBrushProperty =
        AvaloniaProperty.Register<WindowRangePlot, IBrush?>(nameof(FrameBrush));
    public static readonly StyledProperty<IBrush?> GripBrushProperty =
        AvaloniaProperty.Register<WindowRangePlot, IBrush?>(nameof(GripBrush));
    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<WindowRangePlot, IBrush?>(nameof(LineBrush));
    public static readonly StyledProperty<IBrush?> FocusBrushProperty =
        AvaloniaProperty.Register<WindowRangePlot, IBrush?>(nameof(FocusBrush));

    /// <summary>How far an edge's press target reaches outward past the handle (Arc: a 14px <c>::after</c>).</summary>
    internal const double EdgeReachOutside = 14;

    /// <summary>How far an edge's press target reaches into the window.</summary>
    internal const double EdgeReachInside = 2;

    /// <summary>Gap between a handle and its focus ring (the ring is 2px wide, 2px out).</summary>
    private const double RingOffset = 3;

    private static readonly Cursor s_resize = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);

    private WindowRange _owner = null!;
    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private int _builtVersion = -1;
    private Size _builtSize;
    private Pen? _linePen;
    private Pen? _ringPen;
    private Color _lineColor;
    private IBrush? _lineBrush;
    private IBrush? _areaBrush;
    private IBrush? _mutedAreaBrush;
    private IBrush? _frameBrush;

    static WindowRangePlot()
    {
        ClipToBoundsProperty.OverrideDefaultValue<WindowRangePlot>(false);
        AffectsRender<WindowRangePlot>(EdgeWidthProperty, EdgeRadiusProperty, BandThicknessProperty, GripWidthProperty,
            GripHeightProperty, GripOpacityProperty, ChartTopInsetProperty, ChartBottomInsetProperty, LineThicknessProperty,
            MutedOpacityProperty, AreaOpacityProperty, MutedAreaOpacityProperty, TintOpacityProperty, DotDiameterProperty,
            CornerRadiusProperty, FrameBrushProperty, GripBrushProperty, LineBrushProperty, FocusBrushProperty);
    }

    public WindowRangePlot()
    {
        Cursor = s_hand;
    }

    /// <summary>Connects the plot to its control and creates the three focus stops (once).</summary>
    internal void Attach(WindowRange owner)
    {
        if (_owner == owner)
        {
            return;
        }
        if (_owner is not null)
        {
            foreach (var handle in Handles)
            {
                VisualChildren.Remove(handle);
                LogicalChildren.Remove(handle);
            }
        }
        _owner = owner;
        StartHandle = new WindowRangeHandle(owner, WindowRangePart.Start);
        WindowHandle = new WindowRangeHandle(owner, WindowRangePart.Window);
        EndHandle = new WindowRangeHandle(owner, WindowRangePart.End);
        foreach (var handle in Handles)
        {
            VisualChildren.Add(handle);
            LogicalChildren.Add(handle);
        }
        _builtVersion = -1;
        InvalidateVisual();
    }

    internal bool IsAttached => _owner is not null;

    public double EdgeWidth { get => GetValue(EdgeWidthProperty); set => SetValue(EdgeWidthProperty, value); }
    public double EdgeRadius { get => GetValue(EdgeRadiusProperty); set => SetValue(EdgeRadiusProperty, value); }
    public double BandThickness { get => GetValue(BandThicknessProperty); set => SetValue(BandThicknessProperty, value); }
    public double GripWidth { get => GetValue(GripWidthProperty); set => SetValue(GripWidthProperty, value); }
    public double GripHeight { get => GetValue(GripHeightProperty); set => SetValue(GripHeightProperty, value); }
    public double GripOpacity { get => GetValue(GripOpacityProperty); set => SetValue(GripOpacityProperty, value); }
    public double ChartTopInset { get => GetValue(ChartTopInsetProperty); set => SetValue(ChartTopInsetProperty, value); }
    public double ChartBottomInset { get => GetValue(ChartBottomInsetProperty); set => SetValue(ChartBottomInsetProperty, value); }
    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
    public double MutedOpacity { get => GetValue(MutedOpacityProperty); set => SetValue(MutedOpacityProperty, value); }
    public double AreaOpacity { get => GetValue(AreaOpacityProperty); set => SetValue(AreaOpacityProperty, value); }
    public double MutedAreaOpacity { get => GetValue(MutedAreaOpacityProperty); set => SetValue(MutedAreaOpacityProperty, value); }
    public double TintOpacity { get => GetValue(TintOpacityProperty); set => SetValue(TintOpacityProperty, value); }
    public double DotDiameter { get => GetValue(DotDiameterProperty); set => SetValue(DotDiameterProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public IBrush? FrameBrush { get => GetValue(FrameBrushProperty); set => SetValue(FrameBrushProperty, value); }
    public IBrush? GripBrush { get => GetValue(GripBrushProperty); set => SetValue(GripBrushProperty, value); }
    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public IBrush? FocusBrush { get => GetValue(FocusBrushProperty); set => SetValue(FocusBrushProperty, value); }

    public WindowRangeHandle StartHandle { get; private set; } = null!;
    public WindowRangeHandle WindowHandle { get; private set; } = null!;
    public WindowRangeHandle EndHandle { get; private set; } = null!;

    public IEnumerable<WindowRangeHandle> Handles
    {
        get
        {
            if (_owner is null)
            {
                yield break;
            }
            yield return StartHandle;
            yield return WindowHandle;
            yield return EndHandle;
        }
    }

    public WindowRangeHandle HandleOf(WindowRangePart part) => part switch
    {
        WindowRangePart.Start => StartHandle,
        WindowRangePart.End => EndHandle,
        _ => WindowHandle
    };

    #region Geometry

    /// <summary>Width of the chart between the two edge insets.</summary>
    public double ChartWidth => Math.Max(1, Bounds.Width - 2 * EdgeWidth);

    /// <summary>The x of a position on the domain (0..1000).</summary>
    public double XOf(double units) => EdgeWidth + units / WindowRange.Units * ChartWidth;

    /// <summary>The domain position (0..1000) of an x.</summary>
    public double UnitsOf(double x) => (x - EdgeWidth) / ChartWidth * WindowRange.Units;

    /// <summary>Pixels per domain unit.</summary>
    public double PixelsPerUnit => ChartWidth / WindowRange.Units;

    /// <summary>The part a press at <paramref name="x"/> takes: an edge (with its reach), the window, or nothing.</summary>
    public WindowRangePart PartAt(double x)
    {
        var start = XOf(_owner.ShownStart);
        var end = XOf(_owner.ShownEnd);
        var edge = EdgeWidth;
        var onStart = x >= start - edge - EdgeReachOutside && x <= start + EdgeReachInside;
        var onEnd = x >= end - EdgeReachInside && x <= end + edge + EdgeReachOutside;
        if (onStart && onEnd)
        {
            // A narrow window: the nearer handle wins.
            return Math.Abs(x - (start - edge / 2)) <= Math.Abs(x - (end + edge / 2)) ? WindowRangePart.Start : WindowRangePart.End;
        }
        if (onStart)
        {
            return WindowRangePart.Start;
        }
        if (onEnd)
        {
            return WindowRangePart.End;
        }
        return x > start && x < end ? WindowRangePart.Window : WindowRangePart.None;
    }

    #endregion

    #region Layout and input

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var handle in Handles)
        {
            handle.Measure(availableSize);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The handles carry focus and automation only; they span the plot so a focused one scrolls the whole plot into view.
        foreach (var handle in Handles)
        {
            handle.Arrange(new Rect(finalSize));
        }
        return finalSize;
    }

    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _owner?.OnPlotPressed(this, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _owner?.OnPlotMoved(this, e);
        if (_owner is not null)
        {
            UpdateCursor(e.GetPosition(this).X);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _owner?.OnPlotReleased(this, e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _owner?.OnPlotCaptureLost();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner?.SetHover(WindowRangePart.None);
    }

    private void UpdateCursor(double x)
    {
        var part = _owner.ActivePart is var active and not WindowRangePart.None ? active : PartAt(x);
        Cursor = part is WindowRangePart.Start or WindowRangePart.End ? s_resize : s_hand;
    }

    #endregion

    #region Render

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LineBrushProperty || change.Property == LineThicknessProperty || change.Property == AreaOpacityProperty ||
            change.Property == MutedAreaOpacityProperty || change.Property == FrameBrushProperty)
        {
            _lineBrush = null;
        }
        else if (change.Property == FocusBrushProperty)
        {
            _ringPen = null;
        }
        else if (change.Property == ChartTopInsetProperty || change.Property == ChartBottomInsetProperty ||
                 change.Property == EdgeWidthProperty)
        {
            _builtVersion = -1;
        }
    }

    /// <summary>The accent (or the theme's series color) and the brushes derived from it.</summary>
    private void EnsureBrushes()
    {
        var accent = _owner.Accent;
        var line = accent ?? LineBrush;
        var frame = accent ?? FrameBrush;
        if (_lineBrush == line && _frameBrush == frame && _linePen is not null)
        {
            return;
        }
        _lineBrush = line;
        _frameBrush = frame;
        _lineColor = line is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
        _linePen = new Pen(line, LineThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        _areaBrush = new ImmutableSolidColorBrush(_lineColor, AreaOpacity);
        _mutedAreaBrush = new ImmutableSolidColorBrush(_lineColor, MutedAreaOpacity);
    }

    private void EnsureGeometry(Size size, double top, double bottom)
    {
        if (_builtVersion == _owner.ShapeVersion && _builtSize == size && _line is not null)
        {
            return;
        }
        _builtVersion = _owner.ShapeVersion;
        _builtSize = size;
        var shown = _owner.ShownData;
        (_line, _area) = WindowRangeChart.Build(shown, _owner.ShownScaleTop, EdgeWidth, size.Width - EdgeWidth, top, bottom);
    }

    /// <summary>The chart's top and bottom (tests).</summary>
    internal (double Top, double Bottom) ChartBand => (ChartTopInset, Bounds.Height - ChartBottomInset);

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (_owner is null || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureBrushes();
        var edge = EdgeWidth;
        var height = size.Height;
        var top = ChartTopInset;
        var bottom = height - ChartBottomInset;
        EnsureGeometry(size, top, bottom);
        var start = XOf(_owner.ShownStart);
        var end = Math.Max(start, XOf(_owner.ShownEnd));
        var plot = new Rect(size);
        // The whole plot takes the press.
        context.FillRectangle(Brushes.Transparent, plot);
        using (context.PushClip(new RoundedRect(plot, CornerRadius)))
        {
            if (_line is { } line && _area is { } area)
            {
                using (context.PushOpacity(MutedOpacity))
                {
                    context.DrawGeometry(_mutedAreaBrush, null, area);
                    context.DrawGeometry(null, _linePen, line);
                }
                if (end > start)
                {
                    using (context.PushClip(new Rect(start, 0, end - start, height)))
                    {
                        context.DrawGeometry(_areaBrush, null, area);
                        context.DrawGeometry(null, _linePen, line);
                    }
                }
            }
            DrawFrame(context, start, end, height, edge);
            DrawDot(context, top, bottom);
        }
        DrawWindowRing(context, start, end, height, edge);
    }

    private void DrawFrame(DrawingContext context, double start, double end, double height, double edge)
    {
        var frame = _frameBrush;
        var band = BandThickness;
        if (end > start)
        {
            context.FillRectangle(frame!, new Rect(start, 0, end - start, band));
            context.FillRectangle(frame!, new Rect(start, height - band, end - start, band));
            var tint = TintOpacity * _owner.PanPress;
            if (tint > 0.001)
            {
                using (context.PushOpacity(Math.Min(1, tint)))
                {
                    context.FillRectangle(frame!, new Rect(start, band, end - start, Math.Max(0, height - 2 * band)));
                }
            }
        }
        var r = EdgeRadius;
        var startRect = new Rect(start - edge, 0, edge, height);
        var endRect = new Rect(end, 0, edge, height);
        context.DrawRectangle(frame, null, new RoundedRect(startRect, r, 0, 0, r));
        context.DrawRectangle(frame, null, new RoundedRect(endRect, 0, r, r, 0));
        DrawGrip(context, startRect, WindowRangePart.Start);
        DrawGrip(context, endRect, WindowRangePart.End);
        var ring = RingPen;
        if (ring is not null && StartHandle.IsRingVisible)
        {
            var o = RingOffset;
            context.DrawRectangle(null, ring, new RoundedRect(startRect.Inflate(o), r + o, 0, 0, r + o));
        }
        if (ring is not null && EndHandle.IsRingVisible)
        {
            var o = RingOffset;
            context.DrawRectangle(null, ring, new RoundedRect(endRect.Inflate(o), 0, r + o, r + o, 0));
        }
    }

    private Pen? RingPen => FocusBrush is { } focus ? _ringPen ??= new Pen(focus, 2) : null;

    private void DrawGrip(DrawingContext context, Rect edgeRect, WindowRangePart part)
    {
        if (GripBrush is not { } grip)
        {
            return;
        }
        var scale = _owner.GripScaleOf(part);
        var w = GripWidth;
        var h = GripHeight * scale;
        var rect = new Rect(edgeRect.Center.X - w / 2, edgeRect.Center.Y - h / 2, w, h);
        using (context.PushOpacity(_owner.HoverPart == part ? 1 : GripOpacity))
        {
            context.DrawRectangle(grip, null, new RoundedRect(rect, w / 2));
        }
    }

    private void DrawDot(DrawingContext context, double top, double bottom)
    {
        if (!_owner.IsLive || _owner.ShownData.Length == 0 || _lineBrush is not { } brush)
        {
            return;
        }
        var data = _owner.ShownData;
        var center = new Point(XOf(WindowRange.Units), WindowRangeChart.YOf(data[^1], _owner.ShownScaleTop, top, bottom));
        var radius = DotDiameter / 2;
        var (scale, opacity) = _owner.Pulse;
        if (opacity > 0.001)
        {
            using (context.PushOpacity(opacity))
            {
                context.DrawEllipse(brush, null, center, radius * scale, radius * scale);
            }
        }
        context.DrawEllipse(brush, null, center, radius, radius);
    }

    /// <summary>
    /// The window's keyboard ring goes around the whole frame, outside the plot's clip. Arc draws it on the window body,
    /// where the plot clips it away; a visible ring is our accessibility rule.
    /// </summary>
    private void DrawWindowRing(DrawingContext context, double start, double end, double height, double edge)
    {
        if (!WindowHandle.IsRingVisible || RingPen is not { } ring)
        {
            return;
        }
        var o = RingOffset;
        var rect = new Rect(start - edge, 0, end - start + 2 * edge, height).Inflate(o);
        context.DrawRectangle(null, ring, new RoundedRect(rect, EdgeRadius + o));
    }

    #endregion
}
