using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// One self-drawn panel of a <see cref="SmallMultiples"/> (Arc Small multiples, design reference): the name and the
/// value at the crosshair (or the latest), the trend line, the y ticks in a right gutter, the ghost of the total, the
/// area, the line and its end dot, and the synced crosshair. Its hairline dividers (right and bottom) are drawn outside
/// the dimming, so dimming a flat trend fades only the content.
/// </summary>
/// <remarks>
/// Motion is code-driven because the line, the area, the end dot, the crosshair dot and the ticks all derive from the
/// same two animated domain values every frame; a ControlTheme transition cannot reshape self-drawn geometry.
/// </remarks>
internal sealed class SmallMultiplesPanel : Control
{
    // Metrics (Arc Small multiples, design reference).
    internal const double PadLeft = 20;
    internal const double PadTop = 16;
    internal const double PadRight = 20;
    internal const double PadBottom = 18;
    internal const double NameRow = 20;
    internal const double PhoneValueRow = 22;
    internal const double TrendGap = 2;
    internal const double TrendRow = 17;
    internal const double PlotGap = 14;
    internal const double TickGutter = 40;
    internal const double PhoneTickGutter = 34;
    internal const double TickLabelGap = 6;
    internal const double CrossOverhang = 6;
    internal const double DimOpacity = 0.38;
    private const double NameSize = 14;
    private const double PhoneValueSize = 16;
    private const double TrendSize = 12;
    private const double TickSize = 11;
    private const double IconSize = 14;
    private const double IconGap = 4;
    private const double LineThickness = 1.75;
    private const double EndDotRadius = 3;
    private const double CrossDotRadius = 4.5;
    private const double CrossLineAlpha = 0.28;
    private const double AreaAlpha = 0.07;
    private const double GhostAlpha = 0.7;

    private readonly SmallMultiplesGrid _grid;
    private readonly MotionValue<double> _min;
    private readonly MotionValue<double> _max;
    private readonly MotionValue<double> _dim;
    private readonly MotionValue<double> _independentTicks;
    private readonly MotionValue<double> _sharedTicks;
    private readonly MotionValue<Vector> _flip;
    private readonly TranslateTransform _translate = new();

    private SmallMultiplesRow _row;
    private double[] _independentTickValues = [];
    private double[] _sharedTickValues = [];

    private StreamGeometry? _line;
    private StreamGeometry? _area;
    private StreamGeometry? _ghost;
    private (double Min, double Max, Size Size, double PlotHeight, double Gutter) _lineKey;
    private (Size Size, double PlotHeight, double Gutter, object? Totals) _ghostKey;

    private bool _colorsKnown;
    private IImmutableBrush _surface = Brushes.White;
    private IImmutableBrush _foreground = Brushes.Black;
    private IImmutableBrush _secondary = Brushes.Gray;
    private IImmutableBrush _muted = Brushes.Gray;
    private IImmutableBrush _series = Brushes.SteelBlue;
    private IImmutableBrush _areaBrush = Brushes.Transparent;
    private IPen _dividerPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _tickPen = new ImmutablePen(Brushes.LightGray, 1);
    private IPen _linePen = new ImmutablePen(Brushes.SteelBlue, LineThickness);
    private IPen _ghostPen = new ImmutablePen(Brushes.Gray, 1);
    private IPen _crossPen = new ImmutablePen(Brushes.Gray, 1);
    private IPen _crossDotPen = new ImmutablePen(Brushes.SteelBlue, 2);
    private IPen _iconPen = new ImmutablePen(Brushes.Gray, 1.75);

    public SmallMultiplesPanel(SmallMultiplesGrid grid, SmallMultiplesRow row)
    {
        _grid = grid;
        _row = row;
        _min = new MotionValue<double>(this, row.Independent.Min, MotionVectors.Double);
        _max = new MotionValue<double>(this, row.Independent.Max, MotionVectors.Double);
        _dim = new MotionValue<double>(this, 1, MotionVectors.Double);
        _independentTicks = new MotionValue<double>(this, 1, MotionVectors.Double);
        _sharedTicks = new MotionValue<double>(this, 0, MotionVectors.Double);
        _flip = new MotionValue<Vector>(this, default, MotionVectors.Vector);
        _min.Changed += _ => InvalidateVisual();
        _max.Changed += _ => InvalidateVisual();
        _dim.Changed += _ => InvalidateVisual();
        _independentTicks.Changed += _ => InvalidateVisual();
        _sharedTicks.Changed += _ => InvalidateVisual();
        _flip.Changed += v =>
        {
            _translate.X = v.X;
            _translate.Y = v.Y;
        };
        RenderTransform = _translate;
        ClipToBounds = false;
        ActualThemeVariantChanged += (_, _) => OnThemeChanged();
        ResourcesChanged += (_, _) => OnThemeChanged();
        UpdateAutomationName();
    }

    // Theme and accent switches: colors are resolved again on the next render.
    private void OnThemeChanged()
    {
        _colorsKnown = false;
        InvalidateVisual();
    }

    public SmallMultiplesRow Row => _row;

    /// <summary>The domain the panel is moving toward (tests).</summary>
    internal (double Min, double Max) TargetDomain => (_min.Target, _max.Target);

    /// <summary>The domain drawn right now.</summary>
    internal (double Min, double Max) CurrentDomain => (_min.Value, _max.Value);

    /// <summary>The content's opacity right now (1, or <see cref="DimOpacity"/> when dimmed).</summary>
    internal double ContentOpacity => _dim.Value;

    /// <summary>The FLIP offset right now.</summary>
    internal Vector FlipOffset => _flip.Value;

    internal bool IsDimmed => _dim.Target < 1;

    /// <summary>The plot's rectangle inside the panel.</summary>
    internal Rect PlotRect
    {
        get
        {
            var top = PadTop + NameRow + (_grid.IsPhone ? PhoneValueRow : 0) + TrendGap + TrendRow + PlotGap;
            var gutter = _grid.IsPhone ? PhoneTickGutter : TickGutter;
            var width = Math.Max(1, Bounds.Width - PadLeft - PadRight - gutter);
            return new Rect(PadLeft, top, width, _grid.PlotHeight);
        }
    }

    /// <summary>The panel's height for the grid's current metrics.</summary>
    internal static double HeightFor(bool phone, double plotHeight) =>
        PadTop + NameRow + (phone ? PhoneValueRow : 0) + TrendGap + TrendRow + PlotGap + plotHeight + PadBottom;

    public void SetRow(SmallMultiplesRow row)
    {
        _row = row;
        _lineKey = default;
        UpdateAutomationName();
        InvalidateVisual();
    }

    public void SetTicks(double[] independent, double[] shared)
    {
        _independentTickValues = independent;
        _sharedTickValues = shared;
        InvalidateVisual();
    }

    /// <summary>Moves the y domain, on the smooth spring when <paramref name="animate"/> (Reduced and Off land at once).</summary>
    public void SetDomain(double min, double max, bool animate)
    {
        if (!animate)
        {
            _min.Jump(min);
            _max.Jump(max);
            return;
        }
        _ = _min.AnimateTo(min, MotionSpec.Spring(ArcMotionPresets.Smooth));
        _ = _max.AnimateTo(max, MotionSpec.Spring(ArcMotionPresets.Smooth));
    }

    /// <summary>Shows the ticks of <paramref name="scale"/> and fades the other set (240ms).</summary>
    public void SetScale(SmallMultiplesScale scale, bool animate)
    {
        var shared = scale == SmallMultiplesScale.Shared;
        Fade(_independentTicks, shared ? 0 : 1, animate);
        Fade(_sharedTicks, shared ? 1 : 0, animate);
    }

    public void SetDimmed(bool dimmed, bool animate) => Fade(_dim, dimmed ? DimOpacity : 1, animate);

    private static void Fade(MotionValue<double> value, double target, bool animate)
    {
        if (!animate)
        {
            value.Jump(target);
            return;
        }
        _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }

    /// <summary>Starts a FLIP: the panel appears at <paramref name="offset"/> from its new slot and springs into it.</summary>
    public void Flip(Vector offset)
    {
        var start = _flip.Value + offset;
        if (Math.Abs(start.X) < 0.5 && Math.Abs(start.Y) < 0.5)
        {
            _flip.Jump(default);
            return;
        }
        _flip.Jump(start);
        _ = _flip.AnimateTo(default, MotionSpec.Spring(ArcMotionPresets.Smooth));
    }

    public void StopFlip() => _flip.Jump(default);

    internal void UpdateAutomationName()
    {
        var index = _grid.CrosshairIndex;
        var value = index is { } i ? _row.ValueAt(i) : _row.Latest;
        AutomationProperties.SetName(this, $"{_row.Name}, {_grid.FormatValue(value)}, {_grid.TrendWords(_row)}");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 240;
        return new Size(width, HeightFor(_grid.IsPhone, _grid.PlotHeight));
    }

    // Rendering ---------------------------------------------------------------------------------------------------------

    private void EnsureColors()
    {
        if (_colorsKnown)
        {
            return;
        }
        var foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var series = ArcTokens.GetColor(this, ArcDesignTokenKind.Series1);
        var muted = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        var borderSubtle = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle);
        _surface = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.Surface));
        _foreground = new ImmutableSolidColorBrush(foreground);
        _secondary = new ImmutableSolidColorBrush(ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary));
        _muted = new ImmutableSolidColorBrush(muted);
        _series = new ImmutableSolidColorBrush(series);
        _areaBrush = new ImmutableSolidColorBrush(series, AreaAlpha);
        _dividerPen = new ImmutablePen(new ImmutableSolidColorBrush(borderSubtle), 1);
        _tickPen = _dividerPen;
        _linePen = new ImmutablePen(_series, LineThickness, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);
        // Avalonia dash lengths are multiples of the thickness: 3px on, 3px off at 1px.
        _ghostPen = new ImmutablePen(new ImmutableSolidColorBrush(muted, GhostAlpha), 1, new ImmutableDashStyle([3, 3], 0),
            lineJoin: PenLineJoin.Round);
        _crossPen = new ImmutablePen(new ImmutableSolidColorBrush(foreground, CrossLineAlpha), 1);
        _crossDotPen = new ImmutablePen(_series, 2);
        _iconPen = new ImmutablePen(_secondary, 1.75, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round);
        _grid.Text.Family = TextElement.GetFontFamily(this);
        _colorsKnown = true;
    }

    public override void Render(DrawingContext context)
    {
        EnsureColors();
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        context.FillRectangle(_surface, new Rect(size));
        // Hairline dividers on the right and bottom edges (the inset -1px -1px shadow of the reference).
        context.DrawLine(_dividerPen, new Point(size.Width - 0.5, 0), new Point(size.Width - 0.5, size.Height));
        context.DrawLine(_dividerPen, new Point(0, size.Height - 0.5), new Point(size.Width, size.Height - 0.5));

        using (context.PushOpacity(_dim.Value))
        {
            DrawHeading(context, size);
            DrawPlot(context, size);
        }
    }

    private void DrawHeading(DrawingContext context, Size size)
    {
        var text = _grid.Text;
        var index = _grid.CrosshairIndex;
        var value = index is { } i ? _row.ValueAt(i) : _row.Latest;
        var name = text.Get(_row.Name, NameSize, FontWeight.Medium);
        var valueText = _grid.FormatValue(value);
        var right = size.Width - PadRight;
        double trendTop;
        if (_grid.IsPhone)
        {
            name.DrawCentered(context, _foreground, PadLeft, PadTop + NameRow / 2);
            var big = text.Get(valueText, PhoneValueSize, FontWeight.Medium);
            big.DrawCentered(context, _foreground, PadLeft, PadTop + NameRow + PhoneValueRow / 2);
            trendTop = PadTop + NameRow + PhoneValueRow + TrendGap;
        }
        else
        {
            var v = text.Get(valueText, NameSize);
            // The name gives way to the value: it is clipped short of it rather than overlapping.
            var nameRoom = right - v.Width - 12 - PadLeft;
            using (context.PushClip(new Rect(PadLeft, PadTop, Math.Max(0, nameRoom), NameRow)))
            {
                name.DrawCentered(context, _foreground, PadLeft, PadTop + NameRow / 2);
            }
            v.DrawCentered(context, _foreground, right - v.Width, PadTop + NameRow / 2);
            trendTop = PadTop + NameRow + TrendGap;
        }

        // Trend: arrow (or a dash when flat), the signed growth, then "vs <comparison period>".
        var center = trendTop + TrendRow / 2;
        var x = PadLeft;
        var glyph = _row.IsFlat || !double.IsFinite(_row.Growth) ? ArcIcons.Minus
            : _row.Growth >= 0 ? ArcIcons.ArrowUpRight : LineChartsGlyphs.ArrowDownRight;
        using (context.PushTransform(Matrix.CreateScale(IconSize / 24, IconSize / 24) *
                                     Matrix.CreateTranslation(x, center - IconSize / 2)))
        {
            context.DrawGeometry(null, _iconPen, glyph);
        }
        x += IconSize + IconGap;
        if (!double.IsFinite(_row.Growth))
        {
            return;
        }
        var growth = LineChartsFormat.SignedPercent(_row.Growth);
        if (_row.IsFlat)
        {
            var flat = text.Get(_grid.Owner.FlatText + " " + growth, TrendSize);
            flat.DrawCentered(context, _secondary, x, center);
            return;
        }
        var g = text.Get(growth, TrendSize);
        g.DrawCentered(context, _secondary, x, center);
        x += g.Width + IconGap;
        var compare = text.Get(_grid.CompareText(_row), TrendSize);
        using (context.PushClip(new Rect(x, trendTop, Math.Max(0, right - x), TrendRow)))
        {
            compare.DrawCentered(context, _muted, x, center);
        }
    }

    private void DrawPlot(DrawingContext context, Size size)
    {
        var plot = PlotRect;
        var min = _min.Value;
        var max = _max.Value;
        var span = max - min;
        if (!(span > 0) || !double.IsFinite(span))
        {
            return;
        }
        double Y(double v) => plot.Bottom - (v - min) / span * plot.Height;

        DrawTicks(context, plot, _independentTickValues, _independentTicks.Value, Y);
        DrawTicks(context, plot, _sharedTickValues, _sharedTicks.Value, Y);

        var count = _row.Values.Length;
        if (count == 0)
        {
            return;
        }
        double X(double i) => count > 1 ? plot.X + i / (count - 1) * plot.Width : plot.X + plot.Width;

        EnsureGhost(plot, size);
        if (_ghost is not null)
        {
            context.DrawGeometry(null, _ghostPen, _ghost);
        }

        EnsureLine(plot, size, min, max);
        using (context.PushClip(new Rect(plot.X - 8, plot.Y - 8, plot.Width + 16, plot.Height + 8)))
        {
            if (_area is not null)
            {
                context.DrawGeometry(_areaBrush, null, _area);
            }
            if (_line is not null)
            {
                context.DrawGeometry(null, _linePen, _line);
            }
        }

        var last = _row.Values[^1];
        if (double.IsFinite(last))
        {
            var p = new Point(X(count - 1), Math.Clamp(Y(last), plot.Y, plot.Bottom));
            context.DrawEllipse(_series, null, p, EndDotRadius, EndDotRadius);
        }

        var cross = _grid.CrosshairPosition;
        if (double.IsFinite(cross))
        {
            var cx = X(cross);
            context.DrawLine(_crossPen, new Point(Math.Round(cx) + 0.5, plot.Y - CrossOverhang),
                new Point(Math.Round(cx) + 0.5, plot.Bottom + CrossOverhang));
            var cv = SmallMultiplesMath.Interpolate(_row.Values, cross);
            if (double.IsFinite(cv))
            {
                var p = new Point(cx, Math.Clamp(Y(cv), plot.Y, plot.Bottom));
                context.DrawEllipse(_surface, _crossDotPen, p, CrossDotRadius - 1, CrossDotRadius - 1);
            }
        }
    }

    private void DrawTicks(DrawingContext context, Rect plot, double[] ticks, double opacity, Func<double, double> y)
    {
        if (opacity <= 0.01 || ticks.Length == 0)
        {
            return;
        }
        var text = _grid.Text;
        using (context.PushOpacity(opacity))
        {
            foreach (var tick in ticks)
            {
                var ty = y(tick);
                // Ticks travel with the domain; those outside the plot (mid-rescale) are not drawn.
                if (ty < plot.Y - 0.5 || ty > plot.Bottom + 0.5)
                {
                    continue;
                }
                var line = Math.Round(ty) + 0.5;
                context.DrawLine(_tickPen, new Point(plot.X, line), new Point(plot.Right, line));
                var label = text.Get(_grid.FormatTick(tick), TickSize);
                label.DrawCentered(context, _muted, plot.Right + TickLabelGap, ty);
            }
        }
    }

    private void EnsureLine(Rect plot, Size size, double min, double max)
    {
        var key = (min, max, size, plot.Height, plot.Width);
        if (_line is not null && key.Equals(_lineKey))
        {
            return;
        }
        _lineKey = key;
        _line = BuildPath(_row.Values, plot, min, max, closeArea: false);
        _area = BuildPath(_row.Values, plot, min, max, closeArea: true);
    }

    private void EnsureGhost(Rect plot, Size size)
    {
        var totals = _grid.Totals;
        var key = (size, plot.Height, plot.Width, (object?)totals);
        if (_ghost is not null && key.Equals(_ghostKey))
        {
            return;
        }
        _ghostKey = key;
        var (gmin, gmax) = _grid.GhostDomain;
        _ghost = totals.Length > 1 ? BuildPath(totals, plot, gmin, gmax, closeArea: false) : null;
    }

    private static StreamGeometry? BuildPath(IReadOnlyList<double> values, Rect plot, double min, double max, bool closeArea)
    {
        var count = values.Count;
        var span = max - min;
        if (count == 0 || !(span > 0))
        {
            return null;
        }
        var geometry = new StreamGeometry();
        using var g = geometry.Open();
        var open = false;
        var firstX = 0.0;
        var lastX = 0.0;
        for (var i = 0; i < count; i++)
        {
            var v = values[i];
            var x = count > 1 ? plot.X + (double)i / (count - 1) * plot.Width : plot.Right;
            if (!double.IsFinite(v))
            {
                if (open)
                {
                    EndFigure(g, closeArea, firstX, lastX, plot.Bottom);
                    open = false;
                }
                continue;
            }
            var p = new Point(x, plot.Bottom - (v - min) / span * plot.Height);
            if (!open)
            {
                if (closeArea)
                {
                    g.BeginFigure(new Point(x, plot.Bottom), true);
                    g.LineTo(p);
                }
                else
                {
                    g.BeginFigure(p, false);
                }
                open = true;
                firstX = x;
            }
            else
            {
                g.LineTo(p);
            }
            lastX = x;
        }
        if (open)
        {
            EndFigure(g, closeArea, firstX, lastX, plot.Bottom);
        }
        return geometry;
    }

    private static void EndFigure(StreamGeometryContext g, bool closeArea, double firstX, double lastX, double bottom)
    {
        if (closeArea)
        {
            g.LineTo(new Point(lastX, bottom));
            g.LineTo(new Point(firstX, bottom));
            g.EndFigure(true);
        }
        else
        {
            g.EndFigure(false);
        }
    }
}
