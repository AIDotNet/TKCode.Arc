using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The chart and the control row of a <see cref="RangeHistogramSlider"/>, one press target. All bars are one cached geometry
/// drawn twice: once in the resting color and once lit under the clip between the thumbs, so a frame costs two draws and no
/// allocation however many bars there are, and a bar a thumb stops in is cut exactly there. The geometry is rebuilt only when
/// the counts or the size change.
/// </summary>
internal sealed class RangeHistogramRail : SliderRailBase
{
    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<RangeHistogramRail, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<IBrush?> GuideBrushProperty =
        AvaloniaProperty.Register<RangeHistogramRail, IBrush?>(nameof(GuideBrush));

    public static readonly StyledProperty<double> ChartHeightProperty =
        AvaloniaProperty.Register<RangeHistogramRail, double>(nameof(ChartHeight), 76);

    public static readonly StyledProperty<double> BarGapProperty =
        AvaloniaProperty.Register<RangeHistogramRail, double>(nameof(BarGap), 2);

    public static readonly StyledProperty<double> GuideOverhangProperty =
        AvaloniaProperty.Register<RangeHistogramRail, double>(nameof(GuideOverhang), 2);

    public static readonly StyledProperty<CornerRadius> BarTopRadiusProperty =
        AvaloniaProperty.Register<RangeHistogramRail, CornerRadius>(nameof(BarTopRadius));

    public static readonly StyledProperty<CornerRadius> BarBottomRadiusProperty =
        AvaloniaProperty.Register<RangeHistogramRail, CornerRadius>(nameof(BarBottomRadius));

    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }
    public IBrush? GuideBrush { get => GetValue(GuideBrushProperty); set => SetValue(GuideBrushProperty, value); }
    public double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }
    public double BarGap { get => GetValue(BarGapProperty); set => SetValue(BarGapProperty, value); }
    public double GuideOverhang { get => GetValue(GuideOverhangProperty); set => SetValue(GuideOverhangProperty, value); }
    public CornerRadius BarTopRadius { get => GetValue(BarTopRadiusProperty); set => SetValue(BarTopRadiusProperty, value); }
    public CornerRadius BarBottomRadius { get => GetValue(BarBottomRadiusProperty); set => SetValue(BarBottomRadiusProperty, value); }

    private StreamGeometry? _bars;
    private Size _barsSize;

    static RangeHistogramRail()
    {
        AffectsMeasure<RangeHistogramRail>(ChartHeightProperty);
        AffectsRender<RangeHistogramRail>(BarBrushProperty, GuideBrushProperty, GuideOverhangProperty);
    }

    internal RangeHistogramSlider? Slider { get; set; }

    /// <summary>The chart's height after the control row took its share (a short parent squeezes the chart, not the row).</summary>
    internal double ChartAreaHeight => Math.Max(0, RailSize.Height - RowHeight);

    /// <summary>The vertical center of the track and the thumbs.</summary>
    internal double TrackCenterY => ChartAreaHeight + (RailSize.Height - ChartAreaHeight) / 2;

    // Read by tests: the same instance means the frame reused the geometry.
    internal StreamGeometry? BarsGeometry => _bars;

    internal void InvalidateBars()
    {
        _bars = null;
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BarGapProperty || change.Property == BarTopRadiusProperty || change.Property == BarBottomRadiusProperty)
        {
            InvalidateBars();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height + ChartHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // The base centers the thumbs in the whole rail; here they belong in the control row under the chart.
        var d = ThumbDiameter;
        var top = TrackCenterY - d / 2;
        foreach (var thumb in Thumbs)
        {
            thumb.Arrange(new Rect(0, top, d, d));
        }
        return size;
    }

    private StreamGeometry Bars(RangeHistogramSlider slider, double width, double height)
    {
        var size = new Size(width, height);
        if (_bars is { } cached && _barsSize == size)
        {
            return cached;
        }
        var geometry = new StreamGeometry();
        var counts = slider.Counts;
        var tallest = (double)slider.Tallest;
        var gap = BarGap;
        var barWidth = (width - gap * (counts.Count - 1)) / counts.Count;
        using (var context = geometry.Open())
        {
            for (var i = 0; barWidth > 0 && i < counts.Count; i++)
            {
                if (counts[i] > 0)
                {
                    var barHeight = Math.Max(RangeHistogramSlider.MinBarShare, counts[i] / tallest) * height;
                    AddBar(context, TrackLeft + i * (barWidth + gap), height, barWidth, barHeight);
                }
            }
        }
        _bars = geometry;
        _barsSize = size;
        return geometry;
    }

    private void AddBar(StreamGeometryContext context, double x, double bottom, double width, double height)
    {
        var top = bottom - height;
        var rt = BarTopRadius.TopLeft;
        var rb = BarBottomRadius.BottomLeft;
        // Corners shrink together when they do not fit, as CSS border radii do.
        var fit = Math.Min(1, Math.Min(rt + rt > 0 ? width / (rt + rt) : 1, rt + rb > 0 ? height / (rt + rb) : 1));
        rt *= fit;
        rb *= fit;
        var right = x + width;
        context.BeginFigure(new Point(x, bottom - rb), true);
        context.LineTo(new Point(x, top + rt));
        Corner(context, new Point(x + rt, top), rt);
        context.LineTo(new Point(right - rt, top));
        Corner(context, new Point(right, top + rt), rt);
        context.LineTo(new Point(right, bottom - rb));
        Corner(context, new Point(right - rb, bottom), rb);
        context.LineTo(new Point(x + rb, bottom));
        Corner(context, new Point(x, bottom - rb), rb);
        context.EndFigure(true);
    }

    private static void Corner(StreamGeometryContext context, Point to, double radius)
    {
        if (radius > 0)
        {
            context.ArcTo(to, new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Slider is not { } slider)
        {
            return;
        }
        var chart = ChartAreaHeight;
        var cy = TrackCenterY;
        var thickness = TrackThickness;
        var bars = Bars(slider, TrackWidth, chart);
        context.DrawGeometry(BarBrush, null, bars);
        var track = new RoundedRect(new Rect(TrackLeft, cy - thickness / 2, TrackWidth, thickness), thickness / 2);
        context.DrawRectangle(TrackBrush, null, track);

        // One clip reveals the lit bars and the track fill, so both cut at the thumb on the same frame.
        var (from, to) = slider.LitSpan();
        var x0 = ToPixel(from);
        var x1 = ToPixel(to);
        if (x1 > x0)
        {
            using (context.PushClip(new Rect(x0, 0, x1 - x0, RailSize.Height)))
            {
                context.DrawGeometry(FillBrush, null, bars);
                context.DrawRectangle(FillBrush, null, track);
            }
        }
        DrawGuide(context, slider, 0, cy);
        DrawGuide(context, slider, 1, cy);
    }

    private void DrawGuide(DrawingContext context, RangeHistogramSlider slider, int index, double bottom)
    {
        var (position, opacity, scale) = slider.GuideOf(index);
        if (opacity <= 0.001 || GuideBrush is not { } brush)
        {
            return;
        }
        var height = (bottom + GuideOverhang) * scale;
        using (context.PushOpacity(opacity))
        {
            context.FillRectangle(brush, new Rect(ToPixel(position) - 0.5, bottom - height, 1, height));
        }
    }
}
