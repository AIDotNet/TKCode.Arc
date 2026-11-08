namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class RadarChartToken : AbstractControlDesignToken
{
    /// <summary>Space between the outer ring and an axis label.</summary>
    public double LabelGap { get; set; }

    /// <summary>The same space below 360 wide, where the label ring tightens.</summary>
    public double CompactLabelGap { get; set; }

    /// <summary>Widest an axis label may be before it is trimmed.</summary>
    public double LabelWidth { get; set; }

    /// <summary>Widest an axis label may be below 360 wide, where labels wrap.</summary>
    public double CompactLabelWidth { get; set; }

    /// <summary>Thickness of a profile's outline and its line key.</summary>
    public double StrokeThickness { get; set; }

    /// <summary>Radius of the vertex markers on the active axis.</summary>
    public double MarkerRadius { get; set; }

    /// <summary>Length of the line key in the legend and the tooltip.</summary>
    public double LineKeyWidth { get; set; }

    /// <summary>Space between the web and the legend.</summary>
    public double LegendGap { get; set; }

    /// <summary>Space between an axis label and the tooltip.</summary>
    public double TooltipGap { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        LabelGap = 12;
        CompactLabelGap = 6;
        LabelWidth = 112;
        CompactLabelWidth = 68;
        StrokeThickness = 2;
        MarkerRadius = 4;
        LineKeyWidth = 22;
        LegendGap = 14;
        TooltipGap = 10;
    }
}
