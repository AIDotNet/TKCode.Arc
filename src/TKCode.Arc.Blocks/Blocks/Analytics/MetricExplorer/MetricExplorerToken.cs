using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MetricExplorer"/>, measured from the Analytics group's metric explorer (design reference only,
/// ADR 0024). Colours, type ramp and fonts come from ArcDesign tokens; the code reads these values through the theme.
/// "Narrow" applies below <see cref="NarrowWidth"/>, "compact" at <see cref="CompactWidth"/> and below.
/// </summary>
[ControlDesignToken]
internal sealed class MetricExplorerToken : AbstractControlDesignToken
{
    /// <summary>Block width under which the open chart narrows: paddings 16, no tab values, range switch under the readout (640).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Block width at and under which the layout is compact (440).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Block width under which the collapsed cards take two columns (760).</summary>
    public double TwoColumnWidth { get; set; }

    /// <summary>Surface radius of the cards and the open chart (16).</summary>
    public double SurfaceRadius { get; set; }

    /// <summary>Space between the header and the cards (12).</summary>
    public double HeaderGap { get; set; }

    /// <summary>Space between cards (12; compact 8).</summary>
    public double CardGap { get; set; }

    public double CompactCardGap { get; set; }

    /// <summary>Card padding (16 / 20; compact 14 / 16).</summary>
    public Thickness CardPadding { get; set; }

    public Thickness CompactCardPadding { get; set; }

    /// <summary>Sparkline height inside a card (28).</summary>
    public double SparklineHeight { get; set; }

    /// <summary>Open chart padding (16 / 20 / 20; narrow 16; compact 12 / 14 / 16).</summary>
    public Thickness PanelPadding { get; set; }

    public Thickness NarrowPanelPadding { get; set; }

    public Thickness CompactPanelPadding { get; set; }

    /// <summary>Height of the tab row (32) and of the close button (32; compact 26 wide).</summary>
    public double TabRowHeight { get; set; }

    /// <summary>Space under the tab row (20) and between the readout and the range switch when stacked (16).</summary>
    public double RowGap { get; set; }

    public double StackedGap { get; set; }

    /// <summary>The chart's height limits (180 / 240), its plot insets (left 52, right 6, top 8, bottom 28) and the y label column (40).</summary>
    public double ChartMinHeight { get; set; }

    public double ChartMaxHeight { get; set; }

    public Thickness PlotInset { get; set; }

    public double AxisLabelWidth { get; set; }

    /// <summary>Space above the legend (20), under it (8), and above the description (12).</summary>
    public double LegendTopGap { get; set; }

    public double LegendBottomGap { get; set; }

    public double DescriptionGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        NarrowWidth = 640;
        CompactWidth = 440;
        TwoColumnWidth = 760;
        SurfaceRadius = 16;
        HeaderGap = 12;
        CardGap = 12;
        CompactCardGap = 8;
        CardPadding = new Thickness(20, 16);
        CompactCardPadding = new Thickness(16, 14);
        SparklineHeight = 28;
        PanelPadding = new Thickness(20, 16, 20, 20);
        NarrowPanelPadding = new Thickness(16);
        CompactPanelPadding = new Thickness(14, 12, 14, 16);
        TabRowHeight = 32;
        RowGap = 20;
        StackedGap = 16;
        ChartMinHeight = 180;
        ChartMaxHeight = 240;
        PlotInset = new Thickness(52, 8, 6, 28);
        AxisLabelWidth = 40;
        LegendTopGap = 20;
        LegendBottomGap = 8;
        DescriptionGap = 12;
    }
}
