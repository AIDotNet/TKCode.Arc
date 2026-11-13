namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class WindRoseChartToken : AbstractControlDesignToken
{
    /// <summary>Space between the outer ring and a direction label.</summary>
    public double LabelGap { get; set; }

    /// <summary>The same space below 360 wide.</summary>
    public double CompactLabelGap { get; set; }

    /// <summary>Radius of the calm circle in the centre, as a fraction of the rose's radius.</summary>
    public double HoleRatio { get; set; }

    /// <summary>Fraction of its sector's angle a petal fills; the rest is the gap between petals.</summary>
    public double SectorFill { get; set; }

    /// <summary>Pixels between the stacked speed bins of a petal.</summary>
    public double SegmentGap { get; set; }

    /// <summary>Corner radius of a petal segment.</summary>
    public double SegmentCorner { get; set; }

    /// <summary>Space between the rose and the legend.</summary>
    public double LegendGap { get; set; }

    /// <summary>Space between a petal and its tooltip.</summary>
    public double TooltipGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelGap = 10;
        CompactLabelGap = 6;
        HoleRatio = 0.18;
        SectorFill = 0.84;
        SegmentGap = 2;
        SegmentCorner = 2;
        LegendGap = 14;
        TooltipGap = 12;
    }
}
