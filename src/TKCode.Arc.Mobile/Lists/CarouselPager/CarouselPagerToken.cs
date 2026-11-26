namespace TKCode.Arc.Mobile;

/// <summary>Geometry of <see cref="CarouselPager"/>: page indicator dot dimensions, spacing and strip height.</summary>
[ControlDesignToken]
internal sealed class CarouselPagerToken : AbstractControlDesignToken
{
    /// <summary>Diameter of an inactive page indicator dot: 7.</summary>
    public double IndicatorSize { get; set; }

    /// <summary>Width of the active page indicator pill: 18.</summary>
    public double ActiveIndicatorWidth { get; set; }

    /// <summary>Gap between adjacent page indicators: 6.</summary>
    public double IndicatorSpacing { get; set; }

    /// <summary>Minimum height of the indicator strip below the carousel viewport: 32.</summary>
    public double IndicatorStripHeight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        IndicatorSize = 7;
        ActiveIndicatorWidth = 18;
        IndicatorSpacing = 6;
        IndicatorStripHeight = 32;
    }
}
