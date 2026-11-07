using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="HoloCard"/> (Arc Holo card, design reference): the card's corners, its default width limit
/// and the inner edge light. The foil, glitter and glare are decoration drawn from fixed recipes; the face color, the
/// focus ring and the floating shadow's color come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class HoloCardToken : AbstractControlDesignToken
{
    /// <summary>Corner radius of the card and its faces (22, measured; not one of the panel radii).</summary>
    public CornerRadius CardCornerRadius { get; set; }

    /// <summary>The card is at most 300 wide unless a width is set (5:7, so 300 × 420).</summary>
    public double CardMaxWidth { get; set; }

    /// <summary>The 1 px light along the inside of each face's edge: white at 14 % on both themes.</summary>
    public Color CardEdgeLight { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardCornerRadius = new CornerRadius(22);
        CardMaxWidth = 300;
        CardEdgeLight = Color.FromArgb(36, 255, 255, 255);
    }
}
