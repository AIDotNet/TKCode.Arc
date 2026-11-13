using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="MagnifyDock"/>. The shelf, icon tile, label and dot colors, the corner radii, the shadows and
/// the focus ring come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class MagnifyDockToken : AbstractControlDesignToken
{
    /// <summary>The keyboard focus ring sits 3px outside an icon (it grows with the icon).</summary>
    public Thickness ItemFocusRingMargin { get; set; }

    /// <summary>The opacity of an icon while it is pressed.</summary>
    public double PressedOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ItemFocusRingMargin = new Thickness(-3);
        PressedOpacity = 0.8;
    }
}
