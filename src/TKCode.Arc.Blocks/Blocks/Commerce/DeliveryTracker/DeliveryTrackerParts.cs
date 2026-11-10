using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The status steps, drawn in one control: a track through one dot per stage, the done part filled up to <see cref="Fill"/>
/// (a stage index that springs), done dots filled with a check, the current dot ringed, and each stage's name under its dot.
/// </summary>
internal sealed class DeliveryTrackerSteps : Control
{
    public static readonly StyledProperty<double> FillProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, double>(nameof(Fill));

    public static readonly StyledProperty<int> CurrentProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, int>(nameof(Current));

    public static readonly StyledProperty<IBrush?> TrackBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(TrackBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> GlyphBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(GlyphBrush));

    public static readonly StyledProperty<IBrush?> SurfaceBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(SurfaceBrush));

    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<double> LabelFontSizeProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, double>(nameof(LabelFontSize), 12);

    public static readonly StyledProperty<FontFamily?> LabelFontFamilyProperty =
        AvaloniaProperty.Register<DeliveryTrackerSteps, FontFamily?>(nameof(LabelFontFamily));

    private const double Dot = 18;
    private const double Track = 4;
    private static readonly Geometry s_check = ArcIcons.Check;

    private string[] _names = [];
    private FormattedText?[] _texts = [];
    private FormattedText?[] _strong = [];
    private ImmutablePen? _checkPen;
    private ImmutablePen? _ringPen;

    static DeliveryTrackerSteps()
    {
        AffectsRender<DeliveryTrackerSteps>(FillProperty, CurrentProperty, TrackBrushProperty, FillBrushProperty, GlyphBrushProperty,
            SurfaceBrushProperty, TextBrushProperty, MutedBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<DeliveryTrackerSteps>(false);
    }

    public double Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public int Current { get => GetValue(CurrentProperty); set => SetValue(CurrentProperty, value); }
    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public IBrush? GlyphBrush { get => GetValue(GlyphBrushProperty); set => SetValue(GlyphBrushProperty, value); }
    public IBrush? SurfaceBrush { get => GetValue(SurfaceBrushProperty); set => SetValue(SurfaceBrushProperty, value); }
    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    public double LabelFontSize { get => GetValue(LabelFontSizeProperty); set => SetValue(LabelFontSizeProperty, value); }
    public FontFamily? LabelFontFamily { get => GetValue(LabelFontFamilyProperty); set => SetValue(LabelFontFamilyProperty, value); }

    /// <summary>The stage names under the dots.</summary>
    public void SetNames(IReadOnlyList<string> names)
    {
        _names = names.ToArray();
        ResetText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextBrushProperty || change.Property == MutedBrushProperty || change.Property == LabelFontSizeProperty
            || change.Property == LabelFontFamilyProperty)
        {
            ResetText();
        }
        else if (change.Property == GlyphBrushProperty || change.Property == TrackBrushProperty)
        {
            _checkPen = _ringPen = null;
        }
    }

    private void ResetText()
    {
        _texts = new FormattedText?[_names.Length];
        _strong = new FormattedText?[_names.Length];
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Dot + 8 + LabelFontSize * 2.6);

    public override void Render(DrawingContext context)
    {
        var count = _names.Length;
        var width = Bounds.Width;
        if (count < 2 || width <= 0)
        {
            return;
        }
        var step = (width - Dot) / (count - 1);
        var cy = Dot / 2;
        double X(double index) => Dot / 2 + index * step;
        context.DrawRectangle(TrackBrush, null, new Rect(X(0), cy - Track / 2, X(count - 1) - X(0), Track), Track / 2, Track / 2);
        var fill = Math.Clamp(Fill, 0, count - 1);
        if (fill > 0)
        {
            context.DrawRectangle(FillBrush, null, new Rect(X(0), cy - Track / 2, X(fill) - X(0), Track), Track / 2, Track / 2);
        }
        var typeface = new Typeface(LabelFontFamily ?? FontFamily.Default);
        for (var i = 0; i < count; i++)
        {
            var center = new Point(X(i), cy);
            var done = i <= Current && i <= fill + 0.01;
            if (done)
            {
                context.DrawEllipse(FillBrush, null, center, Dot / 2, Dot / 2);
                if (i < Current || Current == count - 1)
                {
                    using (context.PushTransform(Matrix.CreateScale(0.5, 0.5) * Matrix.CreateTranslation(center.X - 6, center.Y - 6)))
                    {
                        _checkPen ??= new ImmutablePen((GlyphBrush ?? Brushes.White).ToImmutable(), 3, lineCap: PenLineCap.Round,
                            lineJoin: PenLineJoin.Round);
                        context.DrawGeometry(null, _checkPen, s_check);
                    }
                }
                else
                {
                    context.DrawEllipse(SurfaceBrush, null, center, 3.5, 3.5);
                }
            }
            else
            {
                _ringPen ??= new ImmutablePen((TrackBrush ?? Brushes.Gray).ToImmutable(), 2);
                context.DrawEllipse(SurfaceBrush, _ringPen, center, Dot / 2 - 1, Dot / 2 - 1);
            }
            var current = i == Current;
            var cache = current ? _strong : _texts;
            var text = cache[i] ??= new FormattedText(_names[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                current ? new Typeface(typeface.FontFamily, FontStyle.Normal, FontWeight.Medium) : typeface, LabelFontSize,
                i <= Current ? TextBrush : MutedBrush)
            {
                MaxTextWidth = Math.Max(20, step - 4),
                TextAlignment = TextAlignment.Center
            };
            var tx = Math.Clamp(center.X - text.MaxTextWidth / 2, 0, Math.Max(0, width - text.MaxTextWidth));
            if (i == 0)
            {
                tx = 0;
                text.TextAlignment = TextAlignment.Left;
            }
            else if (i == count - 1)
            {
                tx = width - text.MaxTextWidth;
                text.TextAlignment = TextAlignment.Right;
            }
            context.DrawText(text, new Point(tx, Dot + 8));
        }
    }
}

/// <summary>
/// The route drawing: a quiet street grid generated from a seed, the route from the hub to the address as a smooth curve
/// (dashed ahead, solid accent behind), pins at both ends and the van at <see cref="Progress"/>, which pulses while it is
/// out for delivery (a <see cref="LiveLoop"/>, full motion only). Travelled segments are drawn line by line from cached
/// points, so a moving van allocates nothing per frame.
/// </summary>
internal sealed class DeliveryTrackerRoute : Control
{
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, double>(nameof(Progress));

    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, int>(nameof(Seed), 7);

    public static readonly StyledProperty<bool> IsPulsingProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, bool>(nameof(IsPulsing));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<IBrush?> GroundBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(GroundBrush));

    public static readonly StyledProperty<IBrush?> StreetBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(StreetBrush));

    public static readonly StyledProperty<IBrush?> RouteBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(RouteBrush));

    public static readonly StyledProperty<IBrush?> AheadBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(AheadBrush));

    public static readonly StyledProperty<IBrush?> MarkerForegroundProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(MarkerForeground));

    public static readonly StyledProperty<IBrush?> EndBrushProperty =
        AvaloniaProperty.Register<DeliveryTrackerRoute, IBrush?>(nameof(EndBrush));

    // Lucide "truck" (ISC).
    private static readonly Geometry s_truck = ArcIcons.Parse(
        "M14 18V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v11a1 1 0 0 0 1 1h2 M15 18H9 M19 18h2a1 1 0 0 0 1-1v-3.65a1 1 0 0 0-.22-.624l-3.48-4.35A1 1 0 0 0 17.52 8H14 M15 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M5 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");

    private readonly LiveLoop _loop;
    private Point[] _points = [];
    private double[] _lengths = [];
    private Geometry? _streets;
    private Geometry? _route;
    private Size _builtFor;
    private double _phase;
    private ImmutablePen? _streetPen;
    private ImmutablePen? _routePen;
    private ImmutablePen? _aheadPen;
    private ImmutablePen? _glyphPen;

    static DeliveryTrackerRoute()
    {
        AffectsRender<DeliveryTrackerRoute>(ProgressProperty, CornerRadiusProperty, GroundBrushProperty, StreetBrushProperty, RouteBrushProperty,
            AheadBrushProperty, MarkerForegroundProperty, EndBrushProperty);
    }

    public DeliveryTrackerRoute()
    {
        _loop = new LiveLoop(this, dt =>
        {
            _phase = (_phase + dt / 1.6) % 1;
            InvalidateVisual();
            return true;
        });
        _loop.RunningChanged += _ => InvalidateVisual();
    }

    public double Progress { get => GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public int Seed { get => GetValue(SeedProperty); set => SetValue(SeedProperty, value); }
    public bool IsPulsing { get => GetValue(IsPulsingProperty); set => SetValue(IsPulsingProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public IBrush? GroundBrush { get => GetValue(GroundBrushProperty); set => SetValue(GroundBrushProperty, value); }
    public IBrush? StreetBrush { get => GetValue(StreetBrushProperty); set => SetValue(StreetBrushProperty, value); }
    public IBrush? RouteBrush { get => GetValue(RouteBrushProperty); set => SetValue(RouteBrushProperty, value); }
    public IBrush? AheadBrush { get => GetValue(AheadBrushProperty); set => SetValue(AheadBrushProperty, value); }
    public IBrush? MarkerForeground { get => GetValue(MarkerForegroundProperty); set => SetValue(MarkerForegroundProperty, value); }
    public IBrush? EndBrush { get => GetValue(EndBrushProperty); set => SetValue(EndBrushProperty, value); }

    /// <summary>Whether the pulse loop is running (tests).</summary>
    internal bool IsLoopRunning => _loop.IsRunning;

    /// <summary>The van's position for a progress of 0 to 1 along the route (tests).</summary>
    internal Point PointAt(double progress)
    {
        EnsureGeometry(Bounds.Size);
        if (_points.Length < 2)
        {
            return default;
        }
        var total = _lengths[^1];
        var target = Math.Clamp(progress, 0, 1) * total;
        var i = Array.BinarySearch(_lengths, target);
        if (i >= 0)
        {
            return _points[i];
        }
        i = ~i;
        if (i <= 0)
        {
            return _points[0];
        }
        if (i >= _points.Length)
        {
            return _points[^1];
        }
        var t = (target - _lengths[i - 1]) / Math.Max(1e-6, _lengths[i] - _lengths[i - 1]);
        return _points[i - 1] + (_points[i] - _points[i - 1]) * t;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPulsingProperty)
        {
            if (IsPulsing)
            {
                _loop.Start();
            }
            else
            {
                _loop.Stop();
            }
        }
        else if (change.Property == SeedProperty)
        {
            _builtFor = default;
            InvalidateVisual();
        }
        else if (change.Property == StreetBrushProperty || change.Property == RouteBrushProperty || change.Property == AheadBrushProperty
                 || change.Property == MarkerForegroundProperty)
        {
            _streetPen = _routePen = _aheadPen = _glyphPen = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, 200);

    private void EnsureGeometry(Size size)
    {
        if (size == _builtFor || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        _builtFor = size;
        var random = new Random(Seed);
        var w = size.Width;
        var h = size.Height;

        // Streets: a slightly irregular grid with one diagonal avenue.
        var streets = new StreamGeometry();
        using (var g = streets.Open())
        {
            for (var x = 18 + random.NextDouble() * 20; x < w; x += 46 + random.NextDouble() * 30)
            {
                g.BeginFigure(new Point(x, 0), false);
                g.LineTo(new Point(x + random.NextDouble() * 14 - 7, h));
                g.EndFigure(false);
            }
            for (var y = 14 + random.NextDouble() * 16; y < h; y += 38 + random.NextDouble() * 24)
            {
                g.BeginFigure(new Point(0, y), false);
                g.LineTo(new Point(w, y + random.NextDouble() * 12 - 6));
                g.EndFigure(false);
            }
            g.BeginFigure(new Point(0, h * (0.2 + random.NextDouble() * 0.2)), false);
            g.LineTo(new Point(w, h * (0.7 + random.NextDouble() * 0.2)));
            g.EndFigure(false);
        }
        _streets = streets;

        // Route: from the hub (lower left) to the address (upper right) through two bends.
        var start = new Point(w * 0.1, h * 0.78);
        var end = new Point(w * 0.88, h * 0.24);
        var c1 = new Point(w * (0.3 + random.NextDouble() * 0.15), h * (0.9 - random.NextDouble() * 0.2));
        var c2 = new Point(w * (0.55 + random.NextDouble() * 0.15), h * (0.05 + random.NextDouble() * 0.25));
        const int samples = 64;
        _points = new Point[samples + 1];
        _lengths = new double[samples + 1];
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var u = 1 - t;
            _points[i] = new Point(
                u * u * u * start.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                u * u * u * start.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y);
            _lengths[i] = i == 0 ? 0 : _lengths[i - 1] + Distance(_points[i - 1], _points[i]);
        }
        var route = new StreamGeometry();
        using (var g = route.Open())
        {
            g.BeginFigure(start, false);
            g.CubicBezierTo(c1, c2, end);
            g.EndFigure(false);
        }
        _route = route;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        EnsureGeometry(size);
        var radius = CornerRadius;
        var shape = new RoundedRect(new Rect(size), radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft);
        using (context.PushClip(shape))
        {
            context.DrawRectangle(GroundBrush, null, new Rect(size));
            _streetPen ??= new ImmutablePen((StreetBrush ?? Brushes.LightGray).ToImmutable(), 5, lineCap: PenLineCap.Round);
            _aheadPen ??= new ImmutablePen((AheadBrush ?? Brushes.Gray).ToImmutable(), 3, new ImmutableDashStyle([1.5, 2], 0), PenLineCap.Round);
            _routePen ??= new ImmutablePen((RouteBrush ?? Brushes.Blue).ToImmutable(), 4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _glyphPen ??= new ImmutablePen((MarkerForeground ?? Brushes.White).ToImmutable(), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            if (_streets is not null)
            {
                context.DrawGeometry(null, _streetPen, _streets);
            }
            if (_route is not null)
            {
                context.DrawGeometry(null, _aheadPen, _route);
            }

            var progress = Math.Clamp(Progress, 0, 1);
            var travelled = progress * _lengths[^1];
            for (var i = 1; i < _points.Length && _lengths[i - 1] < travelled; i++)
            {
                var to = _lengths[i] <= travelled ? _points[i] : PointAt(progress);
                context.DrawLine(_routePen, _points[i - 1], to);
            }

            // Ends: the hub as a small square dot, the address as a ringed dot.
            var start = _points[0];
            var end = _points[^1];
            context.DrawRectangle(RouteBrush, null, new Rect(start.X - 5, start.Y - 5, 10, 10), 2, 2);
            context.DrawEllipse(EndBrush, null, end, 9, 9);
            context.DrawEllipse(RouteBrush, null, end, 4.5, 4.5);

            // The van.
            var van = PointAt(progress);
            if (_loop.IsRunning)
            {
                var ring = 16 + _phase * 14;
                using (context.PushOpacity(0.35 * (1 - _phase)))
                {
                    context.DrawEllipse(RouteBrush, null, van, ring, ring);
                }
            }
            context.DrawEllipse(RouteBrush, null, van, 15, 15);
            using (context.PushTransform(Matrix.CreateScale(0.75, 0.75) * Matrix.CreateTranslation(van.X - 9, van.Y - 9)))
            {
                context.DrawGeometry(null, _glyphPen, s_truck);
            }
        }
    }
}
