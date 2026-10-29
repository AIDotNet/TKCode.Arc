using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="CohortRetention"/>, measured from the Analytics group's cohort retention block (design reference
/// only, ADR 0024). Colours, the type ramp and the fonts come from ArcDesign tokens in the theme; the heat ramp is mixed
/// from Accent and Surface in code. "Compact" values apply while the plot is narrower than <see cref="CompactPlotWidth"/>;
/// "Narrow" ones while the block is narrower than <see cref="NarrowWidth"/>.
/// </summary>
[ControlDesignToken]
internal sealed class CohortRetentionToken : AbstractControlDesignToken
{
    /// <summary>Block padding (20, 16, 20, 20) and its narrow form (16).</summary>
    public Thickness BlockPadding { get; set; }

    public Thickness NarrowBlockPadding { get; set; }

    /// <summary>Space between the header, the metrics, the toolbar and the plot (20; narrow 16).</summary>
    public double SectionGap { get; set; }

    public double NarrowSectionGap { get; set; }

    /// <summary>Below this block width the header wraps, metric paddings tighten and paddings drop to 16 (560).</summary>
    public double NarrowWidth { get; set; }

    /// <summary>Below this block width the metrics stack in one column and the view switch goes above the legend (440).</summary>
    public double StackedWidth { get; set; }

    /// <summary>Title: 16 on a 17.6 line, letter spacing −0.48; 2 between it and the subtitle (14 on 19.6).</summary>
    public double TitleLineHeight { get; set; }

    public double TitleLetterSpacing { get; set; }

    public double SubtitleGap { get; set; }

    public double BodyLineHeight { get; set; }

    /// <summary>Header gaps: 24 between the titles and the segment switch, 12 between wrapped lines.</summary>
    public double HeaderColumnGap { get; set; }

    public double HeaderRowGap { get; set; }

    /// <summary>Toolbar gaps: 16 between the legend and the view switch, 12 between wrapped lines; −4 below it.</summary>
    public double ToolbarColumnGap { get; set; }

    public double ToolbarRowGap { get; set; }

    public Thickness ToolbarMargin { get; set; }

    /// <summary>Metric value size: 3% of the block's width, clamped to 22–28.</summary>
    public double MetricValueMinSize { get; set; }

    public double MetricValueMaxSize { get; set; }

    public double MetricValueScale { get; set; }

    /// <summary>Metric inline padding (20; narrow 12) and the stacked items' vertical padding (10).</summary>
    public double MetricPadding { get; set; }

    public double NarrowMetricPadding { get; set; }

    public double StackedMetricPadding { get; set; }

    /// <summary>The plot switches to its compact geometry below this width (600).</summary>
    public double CompactPlotWidth { get; set; }

    /// <summary>The label column (136; compact 56), the size column's right edge from it (14).</summary>
    public double LabelColumnWidth { get; set; }

    public double CompactLabelColumnWidth { get; set; }

    public double SizeColumnInset { get; set; }

    /// <summary>Row pitch (30; compact 24), cell height (28; 22), cell radius (5; 3), gap above the average row (10; 8).</summary>
    public double RowPitch { get; set; }

    public double CompactRowPitch { get; set; }

    public double CellHeight { get; set; }

    public double CompactCellHeight { get; set; }

    public double CellRadius { get; set; }

    public double CompactCellRadius { get; set; }

    public double AverageRowGap { get; set; }

    public double CompactAverageRowGap { get; set; }

    /// <summary>Numbers inside cells hide when a column is narrower than this (32).</summary>
    public double CellNumberMinWidth { get; set; }

    /// <summary>Curves points: cohorts 5, average 7; the cursor ring around them 10.5 and 12.5.</summary>
    public double PointDiameter { get; set; }

    public double AveragePointDiameter { get; set; }

    public double RingDiameter { get; set; }

    public double AverageRingDiameter { get; set; }

    /// <summary>The hover card: 200 wide, 9 from the cursor box.</summary>
    public double CardWidth { get; set; }

    public double CardGap { get; set; }

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
        ToolbarRowGap = 12;
        ToolbarMargin = new Thickness(0, 0, 0, -4);
        MetricValueMinSize = 22;
        MetricValueMaxSize = 28;
        MetricValueScale = 0.03;
        MetricPadding = 20;
        NarrowMetricPadding = 12;
        StackedMetricPadding = 10;
        CompactPlotWidth = 600;
        LabelColumnWidth = 136;
        CompactLabelColumnWidth = 56;
        SizeColumnInset = 14;
        RowPitch = 30;
        CompactRowPitch = 24;
        CellHeight = 28;
        CompactCellHeight = 22;
        CellRadius = 5;
        CompactCellRadius = 3;
        AverageRowGap = 10;
        CompactAverageRowGap = 8;
        CellNumberMinWidth = 32;
        PointDiameter = 5;
        AveragePointDiameter = 7;
        RingDiameter = 10.5;
        AverageRingDiameter = 12.5;
        CardWidth = 200;
        CardGap = 9;
    }
}
