using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SplitBar"/>: the bar, the gap and cap of each part, how labels fit inside a part or move to the
/// lane under the bar, how far a highlighted part widens, the legend and the readout, and the type sizes. Colors, radii
/// and fonts come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SplitBarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height (44).</summary>
    public double BarHeight { get; set; }

    /// <summary>The gap between two parts (2).</summary>
    public double SegmentGap { get; set; }

    /// <summary>How far a highlighted part grows above and below the bar (0: it widens instead).</summary>
    public double HighlightLift { get; set; }

    /// <summary>The opacity of the parts that are not highlighted (0.5).</summary>
    public double DimOpacity { get; set; }

    /// <summary>Space between the header, the bar and the legend (12).</summary>
    public double SectionGap { get; set; }

    /// <summary>Space between legend entries across (4).</summary>
    public double LegendItemGap { get; set; }

    /// <summary>Space between legend lines (2).</summary>
    public double LegendLineGap { get; set; }

    /// <summary>The legend swatch (12).</summary>
    public double DotSize { get; set; }

    /// <summary>The solid strip at the end of each part and of each legend swatch (3).</summary>
    public double CapWidth { get; set; }

    /// <summary>How much of the series color a part's fill takes over the surface (0.42).</summary>
    public double FillMix { get; set; }

    /// <summary>The same for the highlighted part (0.62).</summary>
    public double HighlightFillMix { get; set; }

    /// <summary>How much of the bar a highlighted part gains at least (0.045).</summary>
    public double HighlightGrow { get; set; }

    /// <summary>Space from a part's start to its inside label (10).</summary>
    public double LabelInset { get; set; }

    /// <summary>Room a part needs around its inside label to show it (24).</summary>
    public double LabelFitPadding { get; set; }

    /// <summary>Space between a label's name and its share (5 inside, 4 in the lane).</summary>
    public double LabelNameGap { get; set; }

    public double LaneNameGap { get; set; }

    /// <summary>The height of the lane under the bar that holds labels too wide for their part (40).</summary>
    public double LaneHeight { get; set; }

    /// <summary>Where a lane label's top sits under the bar (18) and where its leader line ends (15).</summary>
    public double LaneLabelTop { get; set; }

    public double LeaderDepth { get; set; }

    /// <summary>The least space between two lane labels (16).</summary>
    public double LaneLabelGap { get; set; }

    /// <summary>Padding of a legend entry (8,6,10,6).</summary>
    public Thickness LegendItemPadding { get; set; }

    /// <summary>The swatch before the highlighted part in the readout (8).</summary>
    public double ReadoutSwatchSize { get; set; }

    /// <summary>Gaps in the header: between title and readout (16), between rows when the readout wraps (8), title to description (2).</summary>
    public double HeadColumnGap { get; set; }

    public double HeadRowGap { get; set; }

    public double TitleGap { get; set; }

    /// <summary>Type sizes: title (15), description, readout, legend (13), readout figure (17), labels on the bar (12).</summary>
    public double TitleFontSize { get; set; }

    public double BodyFontSize { get; set; }

    public double ReadoutFontSize { get; set; }

    public double LabelFontSize { get; set; }

    /// <summary>Line heights: title (21), description, readout and legend (18.2), readout figure (20.4).</summary>
    public double TitleLineHeight { get; set; }

    public double BodyLineHeight { get; set; }

    public double ReadoutLineHeight { get; set; }

    /// <summary>The least width of a legend share, so entries keep their place while digits roll (33.8).</summary>
    public double LegendShareMinWidth { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 44;
        SegmentGap = 2;
        HighlightLift = 0;
        DimOpacity = 0.5;
        SectionGap = 12;
        LegendItemGap = 4;
        LegendLineGap = 2;
        DotSize = 12;
        CapWidth = 3;
        FillMix = 0.42;
        HighlightFillMix = 0.62;
        HighlightGrow = 0.045;
        LabelInset = 10;
        LabelFitPadding = 24;
        LabelNameGap = 5;
        LaneNameGap = 4;
        LaneHeight = 40;
        LaneLabelTop = 18;
        LeaderDepth = 15;
        LaneLabelGap = 16;
        LegendItemPadding = new Thickness(8, 6, 10, 6);
        ReadoutSwatchSize = 8;
        HeadColumnGap = 16;
        HeadRowGap = 8;
        TitleGap = 2;
        TitleFontSize = 15;
        BodyFontSize = 13;
        ReadoutFontSize = 17;
        LabelFontSize = 12;
        TitleLineHeight = 21;
        BodyLineHeight = 18.2;
        ReadoutLineHeight = 20.4;
        LegendShareMinWidth = 33.8;
    }
}
