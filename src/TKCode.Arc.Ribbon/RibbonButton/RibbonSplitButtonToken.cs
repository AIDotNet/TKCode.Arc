using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of a <see cref="RibbonSplitButton"/> on top of <see cref="RibbonButtonToken"/>: the divider between the action and
/// the menu part, and the fill of the part under the pointer.
/// </summary>
[ControlDesignToken]
internal sealed class RibbonSplitButtonToken : AbstractControlDesignToken
{
    /// <summary>The divider before the menu part of a medium or small button, shown while the pointer is over it: 1.</summary>
    public Thickness DividerThickness { get; set; }

    /// <summary>The divider above the menu part of a large button: 1.</summary>
    public Thickness LargeDividerThickness { get; set; }

    /// <summary>Width of the menu part at the end of a medium or small button (the chevron and the padding after it): 20.</summary>
    public double MenuPartWidth { get; set; }

    /// <summary>Fill of the part under the pointer: the foreground mixed 8% (dark: 11%) into the muted surface.</summary>
    public Color PartHoverBackground { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        DividerThickness = new Thickness(1, 0, 0, 0);
        LargeDividerThickness = new Thickness(0, 1, 0, 0);
        MenuPartWidth = 20;
        PartHoverBackground = Oklch.MixOklab(neutrals.SurfaceMuted, neutrals.Foreground, isDarkMode ? 0.11 : 0.08);
    }
}
