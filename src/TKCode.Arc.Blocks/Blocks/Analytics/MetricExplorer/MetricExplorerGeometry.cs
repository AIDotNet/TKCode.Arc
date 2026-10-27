using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The measured sizes the parts of a <see cref="MetricExplorer"/> share (the values of <see cref="MetricExplorerToken"/>,
/// copied in by the theme). Defaults equal the token, so a control without a theme still lays out.
/// </summary>
internal sealed class MetricExplorerGeometry
{
    public double NarrowWidth { get; init; } = 640;

    public double CompactWidth { get; init; } = 440;

    public double TwoColumnWidth { get; init; } = 760;

    public double SurfaceRadius { get; init; } = 16;

    public double CardGap { get; init; } = 12;

    public double CompactCardGap { get; init; } = 8;

    public Thickness CardPadding { get; init; } = new(20, 16);

    public Thickness CompactCardPadding { get; init; } = new(16, 14);

    public double SparklineHeight { get; init; } = 28;

    public Thickness PanelPadding { get; init; } = new(20, 16, 20, 20);

    public Thickness NarrowPanelPadding { get; init; } = new(16);

    public Thickness CompactPanelPadding { get; init; } = new(14, 12, 14, 16);

    public double TabRowHeight { get; init; } = 32;

    public double RowGap { get; init; } = 20;

    public double StackedGap { get; init; } = 16;

    public double ChartMinHeight { get; init; } = 180;

    public double ChartMaxHeight { get; init; } = 240;

    public Thickness PlotInset { get; init; } = new(52, 8, 6, 28);

    public double AxisLabelWidth { get; init; } = 40;

    public double LegendTopGap { get; init; } = 20;

    public double LegendBottomGap { get; init; } = 8;

    public double DescriptionGap { get; init; } = 12;

    public bool IsNarrow(double width) => width < NarrowWidth;

    public bool IsCompact(double width) => width <= CompactWidth;

    public Thickness PaddingFor(double width) => IsCompact(width) ? CompactPanelPadding : IsNarrow(width) ? NarrowPanelPadding : PanelPadding;

    /// <summary>The columns of the collapsed grid: 4, or 2 under <see cref="TwoColumnWidth"/>.</summary>
    public int ColumnsFor(double width, int count) => Math.Max(1, Math.Min(count, width < TwoColumnWidth ? 2 : 4));
}
