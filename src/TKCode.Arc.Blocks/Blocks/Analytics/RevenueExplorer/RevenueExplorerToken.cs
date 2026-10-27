using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="RevenueExplorer"/>, measured from the Analytics group's revenue explorer block (design reference
/// only, ADR 0024). Colours, the type ramp and the fonts come from ArcDesign tokens in the theme and the palette; the
/// layer fills are accent-independent opacities.
/// </summary>
[ControlDesignToken]
internal sealed class RevenueExplorerToken : AbstractControlDesignToken
{
    /// <summary>Block padding (20, 16, 20, 20).</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Block padding under 560 wide (16).</summary>
    public Thickness NarrowBlockPadding { get; set; }

    /// <summary>Space between the header, the numbers, the toolbar, the chart and the overview (20).</summary>
    public double SectionGap { get; set; }

    /// <summary>The same under 560 (16).</summary>
    public double NarrowSectionGap { get; set; }

    /// <summary>Below this block width paddings drop to 16 and the headline numbers tighten (560).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Below this block width the header wraps, the numbers stack and the chips shrink (440).</summary>
    public double StackedWidth { get; set; }

    /// <summary>Title line height (16 on 17.6, letter spacing −0.48).</summary>
    public double TitleLineHeight { get; set; }

    /// <summary>Title letter spacing.</summary>
    public double TitleLetterSpacing { get; set; }

    /// <summary>Space between the title and the subtitle (2).</summary>
    public double SubtitleGap { get; set; }

    /// <summary>Body line height (14 on 19.6).</summary>
    public double BodyLineHeight { get; set; }

    /// <summary>Gap between the titles and the presets (24).</summary>
    public double HeaderColumnGap { get; set; }

    /// <summary>Gap between them when they wrap (12).</summary>
    public double HeaderRowGap { get; set; }

    /// <summary>Gap between the chips and the compare switch (16).</summary>
    public double ToolbarColumnGap { get; set; }

    /// <summary>Gap between them when they wrap (8).</summary>
    public double ToolbarRowGap { get; set; }

    /// <summary>Gap between the series chips (6).</summary>
    public double ChipSpacing { get; set; }

    /// <summary>Headline value size: 3% of the block's width, clamped to 22–28.</summary>
    public double MetricValueMinSize { get; set; }

    /// <summary>Largest headline value size.</summary>
    public double MetricValueMaxSize { get; set; }

    /// <summary>Headline value size per px of block width.</summary>
    public double MetricValueScale { get; set; }

    /// <summary>Headline number padding beside a divider (20; narrow 12).</summary>
    public double MetricPadding { get; set; }

    /// <summary>Headline number padding under 560.</summary>
    public double NarrowMetricPadding { get; set; }

    /// <summary>Vertical padding of stacked headline numbers (10).</summary>
    public double StackedMetricPadding { get; set; }

    /// <summary>Chart height (256).</summary>
    public double PlotHeight { get; set; }

    /// <summary>Chart height under 600 wide (216).</summary>
    public double CompactPlotHeight { get; set; }

    /// <summary>Below this chart width the chart is compact (600).</summary>
    public double CompactPlotWidth { get; set; }

    /// <summary>Width of the y labels column (48); the plot and the overview start here.</summary>
    public double PlotLeftInset { get; set; }

    /// <summary>Space under the plot for the month labels (28).</summary>
    public double PlotBottomInset { get; set; }

    /// <summary>Radius of the keyboard focus ring around the plot (12).</summary>
    public double PlotFocusRadius { get; set; }

    /// <summary>Distance of the readout panel from the crosshair (14).</summary>
    public double PanelOffset { get; set; }

    /// <summary>Top of the readout panel (14).</summary>
    public double PanelTop { get; set; }

    /// <summary>Radius of the crosshair dots (4).</summary>
    public double DotRadius { get; set; }

    /// <summary>Top of the crosshair line (8).</summary>
    public double CrosshairTop { get; set; }

    /// <summary>Month labels stay this far right of the plot's left edge (14).</summary>
    public double LabelLeftInset { get; set; }

    /// <summary>Month labels stay this far left of the right edge (16).</summary>
    public double LabelRightInset { get; set; }

    /// <summary>Fill of the new layer (accent at 20%).</summary>
    public double AreaOpacityNew { get; set; }

    /// <summary>Fill of the expansion layer (7.5%).</summary>
    public double AreaOpacityExpansion { get; set; }

    /// <summary>Fill of the churn layer (danger at 12%).</summary>
    public double AreaOpacityChurn { get; set; }

    /// <summary>Stroke of the new line (2).</summary>
    public double LineWidthPrimary { get; set; }

    /// <summary>Stroke of the expansion, churn and previous-period lines (1.5).</summary>
    public double LineWidthSecondary { get; set; }

    /// <summary>Height of the overview strip (62).</summary>
    public double OverviewHeight { get; set; }

    /// <summary>Height of the window and its dim sides (44).</summary>
    public double BrushHeight { get; set; }

    /// <summary>Width of a handle's hit area (16).</summary>
    public double HandleWidth { get; set; }

    /// <summary>Width of a handle's grip (6).</summary>
    public double GripWidth { get; set; }

    /// <summary>Height of a handle's grip (20).</summary>
    public double GripHeight { get; set; }

    /// <summary>Corner radius of the window (10).</summary>
    public double WindowRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(20, 16, 20, 20);
        NarrowBlockPadding = new Thickness(16);
        SectionGap = 20;
        NarrowSectionGap = 16;
        NarrowWidth = 560;
        StackedWidth = 440;
        TitleLineHeight = 17.6;
        TitleLetterSpacing = -0.48;
        SubtitleGap = 2;
        BodyLineHeight = 19.6;
        HeaderColumnGap = 24;
        HeaderRowGap = 12;
        ToolbarColumnGap = 16;
        ToolbarRowGap = 8;
        ChipSpacing = 6;
        MetricValueMinSize = 22;
        MetricValueMaxSize = 28;
        MetricValueScale = 0.03;
        MetricPadding = 20;
        NarrowMetricPadding = 12;
        StackedMetricPadding = 10;
        PlotHeight = 256;
        CompactPlotHeight = 216;
        CompactPlotWidth = 600;
        PlotLeftInset = 48;
        PlotBottomInset = 28;
        PlotFocusRadius = 12;
        PanelOffset = 14;
        PanelTop = 14;
        DotRadius = 4;
        CrosshairTop = 8;
        LabelLeftInset = 14;
        LabelRightInset = 16;
        AreaOpacityNew = 0.2;
        AreaOpacityExpansion = 0.075;
        AreaOpacityChurn = 0.12;
        LineWidthPrimary = 2;
        LineWidthSecondary = 1.5;
        OverviewHeight = 62;
        BrushHeight = 44;
        HandleWidth = 16;
        GripWidth = 6;
        GripHeight = 20;
        WindowRadius = 10;
    }
}
