using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="KeycapKeyboard"/> and the keycap's side color. The plate, faces, text, highlight and focus ring
/// take their colors, radii and fonts from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class KeycapKeyboardToken : AbstractControlDesignToken
{
    /// <summary>Largest width of one key unit.</summary>
    public double KeySize { get; set; }

    /// <summary>Space between two keys.</summary>
    public double KeyGap { get; set; }

    /// <summary>How tall the side of a keycap shows under its face; a pressed key sinks by this much.</summary>
    public double KeyDepth { get; set; }

    /// <summary>Space between the plate's edge and the keys.</summary>
    public Thickness PlatePadding { get; set; }

    /// <summary>The side of a keycap, a step darker than its face.</summary>
    public Color KeySkirtColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var neutrals = ArcPalette.Neutrals(isDarkMode);
        KeySize = 44;
        KeyGap = 5;
        KeyDepth = 3;
        PlatePadding = new Thickness(10);
        KeySkirtColor = Oklch.MixOklab(neutrals.Border, neutrals.Foreground, isDarkMode ? 0.04 : 0.12);
    }
}
