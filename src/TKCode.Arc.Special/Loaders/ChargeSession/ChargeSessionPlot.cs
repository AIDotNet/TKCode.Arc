using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The charge curve plot of <see cref="ChargeSession"/> (Arc Charge session, design reference): a 1000 × 300 unit box
/// stretched to the plot (x = 10 × percent, y = 288 − 244 × share), drawn in pixels so every stroke keeps its width:
/// grid lines at full and half power, the axis, a tint beyond the limit, the whole curve in the strong border color, the
/// travelled part (start charge to charge) in the accent over a fading accent area, the dashed limit and a 12px live dot
/// that rides the curve and drops to the baseline with availability. The curve's 200-segment polyline is rebuilt only when
/// the size or the curve changes.
/// </summary>
internal sealed class ChargeSessionPlot : Control
{
    public const double UnitHeight = 300;
    public const double FullY = 44;
    public const double HalfY = 166;
    public const double BaseY = 288;
    public const double Span = 244;
    public const int Segments = 200;

    public static readonly StyledProperty<IBrush?> AccentInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(AccentInk));
    public static readonly StyledProperty<IBrush?> ForegroundInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(ForegroundInk));
    public static readonly StyledProperty<IBrush?> SurfaceInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(SurfaceInk));
    public static readonly StyledProperty<IBrush?> GridInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(GridInk));
    public static readonly StyledProperty<IBrush?> AxisInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(AxisInk));
    public static readonly StyledProperty<IBrush?> CurveInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(CurveInk));
    public static readonly StyledProperty<IBrush?> BeyondInkProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, IBrush?>(nameof(BeyondInk));

    /// <summary>Plot height as a share of its width (0.40 beside the stats, 0.50 stacked).</summary>
    public static readonly StyledProperty<double> AspectProperty =
        AvaloniaProperty.Register<ChargeSessionPlot, double>(nameof(Aspect), 0.40);

    private static readonly AvaloniaProperty[] s_inks =
    {
        AccentInkProperty, ForegroundInkProperty, SurfaceInkProperty, GridInkProperty, AxisInkProperty, CurveInkProperty,
        BeyondInkProperty,
    };

    private readonly Point[] _points = new Point[Segments + 1];
    private ChargeSessionModel? _model;
    private StreamGeometry? _curve;
    private StreamGeometry? _area;
    private Size _builtFor;
    private bool _paletteDirty = true;
    private ImmutableSolidColorBrush _accent = new(Colors.Black);
    private IBrush _beyond = Brushes.Transparent;
    private IBrush _areaFill = Brushes.Transparent;
    private IPen _gridPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _axisPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _curvePen = new ImmutablePen(Brushes.Gray, 1.5);
    private IPen _donePen = new ImmutablePen(Brushes.Black, 2);
    private IPen _limitPen = new ImmutablePen(Brushes.Black, 1);
    private IPen _dotPen = new ImmutablePen(Brushes.White, 2);

    static ChargeSessionPlot()
    {
        AffectsMeasure<ChargeSessionPlot>(AspectProperty);
    }

    public IBrush? AccentInk { get => GetValue(AccentInkProperty); set => SetValue(AccentInkProperty, value); }

    public IBrush? ForegroundInk { get => GetValue(ForegroundInkProperty); set => SetValue(ForegroundInkProperty, value); }

    public IBrush? SurfaceInk { get => GetValue(SurfaceInkProperty); set => SetValue(SurfaceInkProperty, value); }

    public IBrush? GridInk { get => GetValue(GridInkProperty); set => SetValue(GridInkProperty, value); }

    public IBrush? AxisInk { get => GetValue(AxisInkProperty); set => SetValue(AxisInkProperty, value); }

    public IBrush? CurveInk { get => GetValue(CurveInkProperty); set => SetValue(CurveInkProperty, value); }

    public IBrush? BeyondInk { get => GetValue(BeyondInkProperty); set => SetValue(BeyondInkProperty, value); }

    public double Aspect { get => GetValue(AspectProperty); set => SetValue(AspectProperty, value); }

    /// <summary>The live dot's center in plot pixels (tests read it).</summary>
    internal Point DotCenter { get; private set; }

    /// <summary>Connects the session to draw.</summary>
    public void Attach(ChargeSessionModel model)
    {
        _model = model;
        InvalidateCurve();
    }

    /// <summary>Redraws when the charge, availability or limit moved since the last call (the card calls it every step).</summary>
    public void Update()
    {
        if (_model is not { } model)
        {
            return;
        }
        if (model.Charge != _shownCharge || model.Availability != _shownAvailability || model.Limit != _shownLimit)
        {
            _shownCharge = model.Charge;
            _shownAvailability = model.Availability;
            _shownLimit = model.Limit;
            InvalidateVisual();
        }
    }

    private double _shownCharge = double.NaN;
    private double _shownAvailability = double.NaN;
    private double _shownLimit = double.NaN;

    /// <summary>Rebuilds the curve on the next frame (its shape changed: taper, curve function).</summary>
    public void InvalidateCurve()
    {
        _builtFor = default;
        InvalidateVisual();
    }

    /// <summary>The limit for a pointer at plot x (not yet rounded or clamped).</summary>
    public double LimitAt(double x) => Bounds.Width > 0 ? x / Bounds.Width * 100 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 480;
        return new Size(width, width * Math.Max(0.05, Aspect));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (Array.IndexOf(s_inks, change.Property) >= 0)
        {
            _paletteDirty = true;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0 || _model is not { } model)
        {
            return;
        }
        EnsurePalette();
        EnsureCurve(size);
        var w = size.Width;
        var sy = size.Height / UnitHeight;
        // Transparent fill: the whole plot takes the press that moves the limit.
        context.FillRectangle(Brushes.Transparent, new Rect(size));

        var limitX = w * Math.Clamp(model.Limit, 0, 100) / 100;
        if (limitX < w)
        {
            context.FillRectangle(_beyond, new Rect(limitX, 0, w - limitX, BaseY * sy));
        }
        context.DrawLine(_gridPen, new Point(0, Snap(FullY * sy)), new Point(w, Snap(FullY * sy)));
        context.DrawLine(_gridPen, new Point(0, Snap(HalfY * sy)), new Point(w, Snap(HalfY * sy)));
        context.DrawLine(_axisPen, new Point(0, Snap(BaseY * sy)), new Point(w, Snap(BaseY * sy)));

        // The travelled part: area and accent line clipped to [start charge, charge].
        var from = w * Math.Clamp(Math.Min(model.StartCharge, model.Charge), 0, 100) / 100;
        var to = w * Math.Clamp(model.Charge, 0, 100) / 100;
        if (to > from)
        {
            using (context.PushClip(new Rect(from, 0, to - from, size.Height)))
            {
                context.DrawGeometry(_areaFill, null, _area!);
            }
        }
        context.DrawGeometry(null, _curvePen, _curve!);
        if (to > from)
        {
            using (context.PushClip(new Rect(from, -2, to - from, size.Height + 4)))
            {
                context.DrawGeometry(null, _donePen, _curve!);
            }
        }

        context.DrawLine(_limitPen, new Point(Snap(limitX), 14 * sy), new Point(Snap(limitX), BaseY * sy));

        // The live dot: 12px, accent with a 2px surface ring, riding the curve times availability.
        var share = model.Share(model.Charge) * model.Availability;
        DotCenter = new Point(to, (BaseY - Span * share) * sy);
        context.DrawEllipse(_accent, _dotPen, DotCenter, 5, 5);
    }

    private static double Snap(double y) => Math.Round(y - 0.5) + 0.5;

    private void EnsureCurve(Size size)
    {
        if (_curve is not null && _builtFor == size)
        {
            return;
        }
        _builtFor = size;
        var model = _model!;
        var sy = size.Height / UnitHeight;
        for (var i = 0; i <= Segments; i++)
        {
            var percent = 100.0 * i / Segments;
            _points[i] = new Point(size.Width * percent / 100, (BaseY - Span * model.Share(percent)) * sy);
        }
        _curve = new StreamGeometry();
        using (var ctx = _curve.Open())
        {
            ctx.BeginFigure(_points[0], false);
            for (var i = 1; i <= Segments; i++)
            {
                ctx.LineTo(_points[i]);
            }
            ctx.EndFigure(false);
        }
        _area = new StreamGeometry();
        using (var ctx = _area.Open())
        {
            ctx.BeginFigure(new Point(0, BaseY * sy), true);
            for (var i = 0; i <= Segments; i++)
            {
                ctx.LineTo(_points[i]);
            }
            ctx.LineTo(new Point(size.Width, BaseY * sy));
            ctx.EndFigure(true);
        }
        // The area's gradient runs over the curve's box: accent at 20% on top (full power) to 2% at the baseline.
        _areaFill = new ImmutableLinearGradientBrush(
            new[] { new ImmutableGradientStop(0, WithAlpha(_accentColor, 0.20)), new ImmutableGradientStop(1, WithAlpha(_accentColor, 0.02)) },
            1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, FullY * sy, RelativeUnit.Absolute), new RelativePoint(0, BaseY * sy, RelativeUnit.Absolute));
    }

    private Color _accentColor = Colors.Black;

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        _accentColor = ColorOf(AccentInk, Colors.Black);
        _accent = new ImmutableSolidColorBrush(_accentColor);
        _beyond = new ImmutableSolidColorBrush(ColorOf(BeyondInk, Colors.Transparent));
        _gridPen = new ImmutablePen(new ImmutableSolidColorBrush(ColorOf(GridInk, Colors.LightGray)), 1);
        _axisPen = new ImmutablePen(new ImmutableSolidColorBrush(ColorOf(AxisInk, Colors.LightGray)), 1);
        _curvePen = new ImmutablePen(new ImmutableSolidColorBrush(ColorOf(CurveInk, Colors.Gray)), 1.5,
            lineJoin: PenLineJoin.Round);
        _donePen = new ImmutablePen(_accent, 2, lineJoin: PenLineJoin.Round);
        _limitPen = new ImmutablePen(new ImmutableSolidColorBrush(WithAlpha(ColorOf(ForegroundInk, Colors.Black), 0.5)), 1,
            new ImmutableDashStyle(new[] { 4.0, 4.0 }, 0));
        _dotPen = new ImmutablePen(new ImmutableSolidColorBrush(ColorOf(SurfaceInk, Colors.White)), 2);
        _builtFor = default;
    }

    private static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(color.A * alpha), color.R, color.G, color.B);

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid ? solid.Color : fallback;

}
