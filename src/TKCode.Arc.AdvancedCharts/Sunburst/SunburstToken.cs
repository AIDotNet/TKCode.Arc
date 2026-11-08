
namespace TKCode.Arc;

[ControlDesignToken]
internal sealed class SunburstToken : AbstractControlDesignToken
{
    /// <summary>Space between the breadcrumb and the ring, and between the ring and a legend below it.</summary>
    public double FigureGap { get; set; }

    /// <summary>Size of the centre hole as a fraction of the ring's radius.</summary>
    public double HoleRatio { get; set; }

    /// <summary>Pixels between neighbouring segments, along and across the rings.</summary>
    public double SegmentGap { get; set; }

    /// <summary>Corner radius of a segment.</summary>
    public double SegmentCorner { get; set; }

    /// <summary>Space between the ring and a legend beside it.</summary>
    public double LegendGap { get; set; }

    /// <summary>The legend moves below the ring when less than this width is left for it.</summary>
    public double LegendMinWidth { get; set; }

    /// <summary>Widest a legend beside the ring grows.</summary>
    public double LegendMaxWidth { get; set; }

    /// <summary>Height of a legend row.</summary>
    public double LegendRowHeight { get; set; }

    /// <summary>Corner radius of a legend row's hover and focus marks.</summary>
    public double LegendRowRadius { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        FigureGap = 12;
        HoleRatio = 0.3;
        SegmentGap = 2;
        SegmentCorner = 3;
        LegendGap = 24;
        LegendMinWidth = 200;
        LegendMaxWidth = 300;
        LegendRowHeight = 36;
        LegendRowRadius = 8;
    }
}
