namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="SplitBar"/>: the bar's height, the gap between parts and how far a highlighted part grows, the
/// legend's spacing and dot, and the opacity of parts that are not highlighted. Colors, radii and type come from the
/// ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class SplitBarToken : AbstractControlDesignToken
{
    /// <summary>The bar's height (12).</summary>
    public double BarHeight { get; set; }

    /// <summary>The gap between two parts (3).</summary>
    public double SegmentGap { get; set; }

    /// <summary>How far a highlighted part grows above and below the bar (2).</summary>
    public double HighlightLift { get; set; }

    /// <summary>The opacity of the parts that are not highlighted (0.35).</summary>
    public double DimOpacity { get; set; }

    /// <summary>Space between the header, the bar and the legend (12).</summary>
    public double SectionGap { get; set; }

    /// <summary>Space between legend entries across (20) and between their lines (8).</summary>
    public double LegendItemGap { get; set; }

    public double LegendLineGap { get; set; }

    /// <summary>The legend dot (8).</summary>
    public double DotSize { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BarHeight = 12;
        SegmentGap = 3;
        HighlightLift = 2;
        DimOpacity = 0.35;
        SectionGap = 12;
        LegendItemGap = 20;
        LegendLineGap = 8;
        DotSize = 8;
    }
}
