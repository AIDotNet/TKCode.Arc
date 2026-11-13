namespace TKCode.Arc;

/// <summary>Geometry of <see cref="RadialMenu"/>: the ring's radii, the gap between sectors and the plate around them.</summary>
[ControlDesignToken]
internal sealed class RadialMenuToken : AbstractControlDesignToken
{
    /// <summary>Radius of the centre (the label area and the gesture's dead zone): 44.</summary>
    public double InnerRadius { get; set; }

    /// <summary>Radius of the sectors' outer edge: 124.</summary>
    public double OuterRadius { get; set; }

    /// <summary>Gap between neighbouring sectors: 4.</summary>
    public double SectorGap { get; set; }

    /// <summary>How far the plate reaches past the sectors: 6.</summary>
    public double PlatePadding { get; set; }

    /// <summary>The least distance kept between the ring and the edge of the viewport when it is shifted into view: 8.</summary>
    public double ViewportMargin { get; set; }

    /// <summary>Distance between the centre label and the edge of the centre: 10.</summary>
    public double CenterLabelPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        InnerRadius = 44;
        OuterRadius = 124;
        SectorGap = 4;
        PlatePadding = 6;
        ViewportMargin = 8;
        CenterLabelPadding = 10;
    }
}
