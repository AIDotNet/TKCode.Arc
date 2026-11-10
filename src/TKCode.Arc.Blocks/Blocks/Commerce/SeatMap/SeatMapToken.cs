using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="SeatMap"/>.</summary>
[ControlDesignToken]
internal sealed class SeatMapToken : AbstractControlDesignToken
{
    /// <summary>Space inside the block's card.</summary>
    public Thickness BlockPadding { get; set; }

    /// <summary>Width of the summary beside the map.</summary>
    public double SummaryWidth { get; set; }

    /// <summary>Space around the summary beside the map (the gap is on its left).</summary>
    public Thickness SummaryMargin { get; set; }

    /// <summary>Space around the summary when it sits under the map.</summary>
    public Thickness SummaryStackedMargin { get; set; }

    /// <summary>Block width below which the summary sits under the map.</summary>
    public double CompactThreshold { get; set; }

    /// <summary>Diameter of a legend dot.</summary>
    public double LegendDot { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BlockPadding = new Thickness(24);
        SummaryWidth = 300;
        SummaryMargin = new Thickness(28, 0, 0, 0);
        SummaryStackedMargin = new Thickness(0, 20, 0, 0);
        CompactThreshold = 780;
        LegendDot = 12;
    }
}
