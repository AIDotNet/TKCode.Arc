using AtomUI.Theme.DesignTokens;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="GravityWell"/> (Arc Gravity well, design reference). The ring, focus and tile colors come from
/// the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class GravityWellToken : AbstractControlDesignToken
{
    /// <summary>The shockwave ring: 2px of accent just outside the tile.</summary>
    public Thickness RingThickness { get; set; }

    public Thickness RingMargin { get; set; }

    /// <summary>The keyboard focus ring sits 4px outside the grid.</summary>
    public Thickness FocusRingMargin { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        RingThickness = new Thickness(2);
        RingMargin = new Thickness(-2);
        FocusRingMargin = new Thickness(-4);
    }
}
