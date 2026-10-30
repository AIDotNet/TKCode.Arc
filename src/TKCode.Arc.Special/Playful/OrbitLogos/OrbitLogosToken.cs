using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="OrbitLogos"/> (Arc Orbit logos, design reference). Colors come from the ArcDesign tokens in
/// the theme: the raised surface for chips and the card, the accent for the connector and the selected chip.
/// </summary>
[ControlDesignToken]
internal sealed class OrbitLogosToken : AbstractControlDesignToken
{
    /// <summary>The detail column is at least this tall, so opening a card does not move the page.</summary>
    public double DetailMinHeight { get; set; }

    /// <summary>Inner padding of the detail card.</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>Inner padding of a category chip.</summary>
    public Thickness CategoryPadding { get; set; }

    /// <summary>Inner padding of the name tag.</summary>
    public Thickness TagPadding { get; set; }

    /// <summary>Height of a category chip.</summary>
    public double CategoryHeight { get; set; }

    /// <summary>Diameter of the dot that marks a connected chip.</summary>
    public double DotSize { get; set; }

    /// <summary>The focus ring sits this far outside a chip.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DetailMinHeight = 232;
        CardPadding = new Thickness(20);
        CategoryPadding = new Thickness(14, 0);
        TagPadding = new Thickness(10, 4);
        CategoryHeight = 34;
        DotSize = 10;
        FocusRingMargin = new Thickness(-4);
    }
}
