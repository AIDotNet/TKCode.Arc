using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Draws every edge of a <see cref="WorkflowCanvas"/> in one pass (Arc HeroRelay <c>.edges</c>): a quiet wire for each
/// connection, dashed and fainter when the run skipped it, and on top the data drawn in the accent from the source
/// toward the target. One control for all edges keeps a large workflow cheap; each edge caches its curve.
/// </summary>
/// <remarks>
/// Code-driven motion: the drawn length is a share of a curve inside one self-drawn layer, which no control property or
/// ControlTheme transition can express.
/// </remarks>
internal sealed class WorkflowEdgeLayer : Control
{
    public static readonly StyledProperty<double> WireThicknessProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(WireThickness), 1.25);

    public static readonly StyledProperty<IBrush?> WireBrushProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, IBrush?>(nameof(WireBrush));

    public static readonly StyledProperty<double> FlowThicknessProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(FlowThickness), 1.75);

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, IBrush?>(nameof(AccentSubtleBrush));

    public static readonly StyledProperty<double> SkippedDashLengthProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(SkippedDashLength), 3);

    public static readonly StyledProperty<double> SkippedGapLengthProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(SkippedGapLength), 4);

    public static readonly StyledProperty<double> SkippedWireOpacityProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(SkippedWireOpacity), 0.7);

    public static readonly StyledProperty<double> BezierMinHandleProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(BezierMinHandle), 40);

    public static readonly StyledProperty<double> StepCornerRadiusProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, double>(nameof(StepCornerRadius), 8);

    public static readonly StyledProperty<TimeSpan> FlowDurationProperty =
        AvaloniaProperty.Register<WorkflowEdgeLayer, TimeSpan>(nameof(FlowDuration), TimeSpan.FromMilliseconds(400));

    private IPen? _wirePen;
    private IPen? _skippedPen;
    private IPen? _flowPen;
    private IPen? _haloPen;

    static WorkflowEdgeLayer()
    {
        // Decorative: the card's name says it all.
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WorkflowEdgeLayer>(AccessibilityView.Raw);
        AffectsRender<WorkflowEdgeLayer>(WireThicknessProperty, WireBrushProperty, FlowThicknessProperty, AccentBrushProperty,
            SkippedDashLengthProperty, SkippedGapLengthProperty, SkippedWireOpacityProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<WorkflowEdgeLayer>(false);
        ClipToBoundsProperty.OverrideDefaultValue<WorkflowEdgeLayer>(false);
    }

    public double WireThickness
    {
        get => GetValue(WireThicknessProperty);
        set => SetValue(WireThicknessProperty, value);
    }

    public IBrush? WireBrush
    {
        get => GetValue(WireBrushProperty);
        set => SetValue(WireBrushProperty, value);
    }

    public double FlowThickness
    {
        get => GetValue(FlowThicknessProperty);
        set => SetValue(FlowThicknessProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? AccentSubtleBrush
    {
        get => GetValue(AccentSubtleBrushProperty);
        set => SetValue(AccentSubtleBrushProperty, value);
    }

    public double SkippedDashLength
    {
        get => GetValue(SkippedDashLengthProperty);
        set => SetValue(SkippedDashLengthProperty, value);
    }

    public double SkippedGapLength
    {
        get => GetValue(SkippedGapLengthProperty);
        set => SetValue(SkippedGapLengthProperty, value);
    }

    public double SkippedWireOpacity
    {
        get => GetValue(SkippedWireOpacityProperty);
        set => SetValue(SkippedWireOpacityProperty, value);
    }

    public double BezierMinHandle
    {
        get => GetValue(BezierMinHandleProperty);
        set => SetValue(BezierMinHandleProperty, value);
    }

    public double StepCornerRadius
    {
        get => GetValue(StepCornerRadiusProperty);
        set => SetValue(StepCornerRadiusProperty, value);
    }

    public TimeSpan FlowDuration
    {
        get => GetValue(FlowDurationProperty);
        set => SetValue(FlowDurationProperty, value);
    }

    /// <summary>The edges to draw, in drawing order (the canvas owns the list).</summary>
    public List<WorkflowEdgeVisual> Edges { get; } = new();

    /// <summary>The world rectangle this layer covers; drawing happens in world coordinates.</summary>
    public Rect Extent { get; set; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WireThicknessProperty || change.Property == WireBrushProperty ||
            change.Property == SkippedDashLengthProperty || change.Property == SkippedGapLengthProperty ||
            change.Property == SkippedWireOpacityProperty)
        {
            _wirePen = null;
            _skippedPen = null;
        }
        else if (change.Property == FlowThicknessProperty || change.Property == AccentBrushProperty)
        {
            _flowPen = null;
        }
        else if (change.Property == AccentSubtleBrushProperty)
        {
            _haloPen = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Edges.Count == 0 || WireBrush is not ISolidColorBrush wire)
        {
            return;
        }
        var thickness = WireThickness;
        var wireColor = wire.Color;
        _wirePen ??= new ImmutablePen(new ImmutableSolidColorBrush(wireColor), thickness);
        // Avalonia measures dashes in stroke widths; Arc's are in pixels.
        _skippedPen ??= new ImmutablePen(
            new ImmutableSolidColorBrush(Oklch.WithAlpha(wireColor, wireColor.A / 255.0 * wire.Opacity * SkippedWireOpacity)), thickness,
            new ImmutableDashStyle([SkippedDashLength / thickness, SkippedGapLength / thickness], 0));
        using var _ = context.PushTransform(Matrix.CreateTranslation(-Extent.X, -Extent.Y));
        foreach (var edge in Edges)
        {
            if (edge.Wire is { } curve)
            {
                context.DrawGeometry(null, edge.Skipped ? _skippedPen : _wirePen, curve);
            }
        }
        if (AccentBrush is not ISolidColorBrush accent)
        {
            return;
        }
        _flowPen ??= new ImmutablePen(new ImmutableSolidColorBrush(accent.Color), FlowThickness, lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round);
        // Selected edges: a 6px halo in the subtle accent under the full accent line.
        _haloPen ??= AccentSubtleBrush is { } subtle ? new ImmutablePen(subtle.ToImmutable(), 6, lineCap: PenLineCap.Round,
            lineJoin: PenLineJoin.Round) : null;
        foreach (var edge in Edges)
        {
            if (edge.Selected && edge.Wire is { } selectedWire)
            {
                if (_haloPen is not null)
                {
                    context.DrawGeometry(null, _haloPen, selectedWire);
                }
                context.DrawGeometry(null, _flowPen, selectedWire);
            }
        }
        foreach (var edge in Edges)
        {
            if (edge.Skipped || edge.Wire is null || edge.Selected)
            {
                continue;
            }
            var progress = edge.Progress?.Value ?? 0;
            if (progress <= 0.001)
            {
                continue;
            }
            context.DrawGeometry(null, _flowPen, progress >= 0.999 ? edge.Wire : edge.PartialWire(progress));
        }
    }
}

/// <summary>
/// One edge as the canvas draws it: its shape, whether the run took or skipped it, and how far the data has drawn.
/// A curve is kept as an exact cubic; a step or straight edge as a polyline (the step's rounded corners flattened), and
/// both carry a length table so the data draws at an even speed.
/// </summary>
internal sealed class WorkflowEdgeVisual
{
    private const int Samples = 24;
    private const int CornerSegments = 6;

    private readonly double[] _lengths = new double[Samples + 1];
    private readonly Point[] _samples = new Point[Samples + 1];
    private Point _p0, _p1, _p2, _p3;
    private Point[]? _polyline;
    private double[]? _polylineLengths;
    private WorkflowEdgeStyle _style;

    public WorkflowEdgeVisual(WorkflowEdge edge) => Edge = edge;

    public WorkflowEdge Edge { get; }

    /// <summary>Data went along the edge (it draws in the accent).</summary>
    public bool Lit { get; set; }

    /// <summary>The run did not take the edge (dashed).</summary>
    public bool Skipped { get; set; }

    /// <summary>The edge is selected (accent with a soft halo).</summary>
    public bool Selected { get; set; }

    /// <summary>How much of the edge is drawn, 0–1; created by the canvas.</summary>
    public MotionValue<double>? Progress { get; set; }

    /// <summary>The whole edge, or null before the canvas has placed both cards.</summary>
    public StreamGeometry? Wire { get; private set; }

    /// <summary>The edge's bounding box (curve control points included).</summary>
    public Rect Bounds { get; private set; }

    /// <summary>
    /// Shapes the edge from the source card's rectangle to the target card's: out of the source's bottom center into the
    /// target's top center (left to right: right middle into left middle). A curve has Arc's handles of half the gap,
    /// longer for short, flat or backward edges so they still leave and enter straight; a step turns halfway with
    /// rounded corners; a straight edge is one line.
    /// </summary>
    public void Place(Point p0, Point p3, WorkflowLayoutDirection direction, WorkflowEdgeStyle style, double minHandle,
                      double cornerRadius)
    {
        var vertical = direction == WorkflowLayoutDirection.TopToBottom;
        Point p1, p2;
        if (style == WorkflowEdgeStyle.Bezier)
        {
            var m = Handle(vertical ? p3.Y - p0.Y : p3.X - p0.X, minHandle);
            p1 = vertical ? new Point(p0.X, p0.Y + m) : new Point(p0.X + m, p0.Y);
            p2 = vertical ? new Point(p3.X, p3.Y - m) : new Point(p3.X - m, p3.Y);
        }
        else
        {
            // The middle of a step (the line where it turns); a straight edge ignores it.
            var middle = vertical ? (p0.Y + p3.Y) / 2 : (p0.X + p3.X) / 2;
            p1 = vertical ? new Point(p0.X, middle) : new Point(middle, p0.Y);
            p2 = vertical ? new Point(p3.X, middle) : new Point(middle, p3.Y);
        }
        if (Wire is not null && style == _style && p0 == _p0 && p1 == _p1 && p2 == _p2 && p3 == _p3)
        {
            return;
        }
        (_p0, _p1, _p2, _p3, _style) = (p0, p1, p2, p3, style);
        if (style == WorkflowEdgeStyle.Bezier)
        {
            _polyline = null;
            _polylineLengths = null;
            Wire = Cubic(p0, p1, p2, p3);
            Bounds = BoundsOf([p0, p1, p2, p3]);
            // Arc length table, so the data draws at an even speed along the curve; the samples also serve hit tests.
            var previous = p0;
            _lengths[0] = 0;
            _samples[0] = p0;
            for (var i = 1; i <= Samples; i++)
            {
                var point = At((double)i / Samples);
                _samples[i] = point;
                _lengths[i] = _lengths[i - 1] + Distance(previous, point);
                previous = point;
            }
            return;
        }
        _polyline = style == WorkflowEdgeStyle.Straight ? [p0, p3] : Step(p0, p1, p2, p3, cornerRadius);
        _polylineLengths = new double[_polyline.Length];
        for (var i = 1; i < _polyline.Length; i++)
        {
            _polylineLengths[i] = _polylineLengths[i - 1] + Distance(_polyline[i - 1], _polyline[i]);
        }
        Wire = Polyline(_polyline, _polyline.Length - 1, _polyline[^1]);
        Bounds = BoundsOf(_polyline);
    }

    /// <summary>The shortest distance from <paramref name="point"/> to the edge (along its samples or polyline).</summary>
    public double DistanceTo(Point point)
    {
        if (Wire is null)
        {
            return double.PositiveInfinity;
        }
        IReadOnlyList<Point> line = _polyline ?? (IReadOnlyList<Point>)_samples;
        var best = double.PositiveInfinity;
        for (var i = 1; i < line.Count; i++)
        {
            best = Math.Min(best, SegmentDistance(point, line[i - 1], line[i]));
        }
        return best;
    }

    private static double SegmentDistance(Point p, Point a, Point b)
    {
        var ab = b - a;
        var length = ab.X * ab.X + ab.Y * ab.Y;
        var t = length <= 0 ? 0 : Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / length, 0, 1);
        return Distance(p, new Point(a.X + ab.X * t, a.Y + ab.Y * t));
    }

    /// <summary>Arc's handle (<c>mid = (y2 − y1) / 2</c>), lengthened toward <paramref name="minHandle"/> as the gap closes.</summary>
    internal static double Handle(double gap, double minHandle) => Math.Max(gap / 2, minHandle - gap);

    /// <summary>The first <paramref name="progress"/> of the edge by length.</summary>
    public StreamGeometry PartialWire(double progress)
    {
        if (_polyline is { } line && _polylineLengths is { } lengths)
        {
            var goal = lengths[^1] * progress;
            var k = 1;
            while (k < line.Length - 1 && lengths[k] < goal)
            {
                k++;
            }
            var piece = lengths[k] - lengths[k - 1];
            var share = piece <= 0 ? 0 : (goal - lengths[k - 1]) / piece;
            return Polyline(line, k - 1, Lerp(line[k - 1], line[k], Math.Clamp(share, 0, 1)));
        }
        // A curve splits exactly (de Casteljau) at the parameter of the wanted length.
        var target = _lengths[Samples] * progress;
        var i = 1;
        while (i < Samples && _lengths[i] < target)
        {
            i++;
        }
        var span = _lengths[i] - _lengths[i - 1];
        var local = span <= 0 ? 0 : (target - _lengths[i - 1]) / span;
        var t = (i - 1 + local) / Samples;
        var a = Lerp(_p0, _p1, t);
        var b = Lerp(_p1, _p2, t);
        var c = Lerp(_p2, _p3, t);
        var ab = Lerp(a, b, t);
        var bc = Lerp(b, c, t);
        return Cubic(_p0, a, ab, Lerp(ab, bc, t));
    }

    /// <summary>The curve's points (tests): ends, and the handles of a curve or the turning points of a step.</summary>
    internal (Point Start, Point C1, Point C2, Point End) Points => (_p0, _p1, _p2, _p3);

    /// <summary>The polyline of a step or straight edge (tests); null for a curve.</summary>
    internal IReadOnlyList<Point>? PolylinePoints => _polyline;

    /// <summary>
    /// A step through the turning points <paramref name="b"/> and <paramref name="c"/>, each corner rounded with up to
    /// <paramref name="radius"/> (less when the legs are short) and flattened into a few segments.
    /// </summary>
    private static Point[] Step(Point a, Point b, Point c, Point d, double radius)
    {
        if (Distance(b, c) < 0.5)
        {
            return [a, d];
        }
        var r = Math.Min(radius, Math.Min(Distance(b, c) / 2, Math.Min(Distance(a, b), Distance(c, d))));
        var points = new List<Point>(4 + 2 * CornerSegments) { a };
        AddCorner(points, a, b, c, r);
        AddCorner(points, b, c, d, r);
        points.Add(d);
        return points.ToArray();
    }

    /// <summary>Rounds the corner at <paramref name="corner"/> between the legs from <paramref name="from"/> and to <paramref name="to"/>.</summary>
    private static void AddCorner(List<Point> points, Point from, Point corner, Point to, double r)
    {
        if (r <= 0.01)
        {
            points.Add(corner);
            return;
        }
        var start = corner + Unit(corner, from) * r;
        var end = corner + Unit(corner, to) * r;
        // A quadratic through the corner approximates the quarter circle closely enough at these radii.
        for (var i = 0; i <= CornerSegments; i++)
        {
            var t = (double)i / CornerSegments;
            var u = 1 - t;
            points.Add(new Point(u * u * start.X + 2 * u * t * corner.X + t * t * end.X,
                u * u * start.Y + 2 * u * t * corner.Y + t * t * end.Y));
        }
    }

    private static Vector Unit(Point from, Point to)
    {
        var length = Distance(from, to);
        return length <= 0 ? default : new Vector((to.X - from.X) / length, (to.Y - from.Y) / length);
    }

    private Point At(double t)
    {
        var u = 1 - t;
        return _p0 * (u * u * u) + _p1 * (3 * u * u * t) + _p2 * (3 * u * t * t) + _p3 * (t * t * t);
    }

    private static Rect BoundsOf(IReadOnlyList<Point> points)
    {
        double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
        foreach (var p in points)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }
        return new Rect(left, top, right - left, bottom - top);
    }

    private static StreamGeometry Cubic(Point p0, Point p1, Point p2, Point p3)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(p0, false);
        g.CubicBezierTo(p1, p2, p3);
        g.EndFigure(false);
        return geometry;
    }

    /// <summary>The polyline's points up to index <paramref name="last"/>, then a line to <paramref name="end"/>.</summary>
    private static StreamGeometry Polyline(Point[] points, int last, Point end)
    {
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        g.BeginFigure(points[0], false);
        for (var i = 1; i <= last; i++)
        {
            g.LineTo(points[i]);
        }
        g.LineTo(end);
        g.EndFigure(false);
        return geometry;
    }

    private static Point Lerp(Point a, Point b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static double Distance(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
