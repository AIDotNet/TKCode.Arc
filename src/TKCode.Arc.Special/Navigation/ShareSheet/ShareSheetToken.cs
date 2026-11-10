using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="ShareSheet"/>'s panel: its width as a popover, the target tiles and the action rows. Colors,
/// radii and shadows come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ShareSheetToken : AbstractControlDesignToken
{
    /// <summary>Width of the panel when it opens as a popover.</summary>
    public double PopoverWidth { get; set; }

    /// <summary>Gap between the panel's groups.</summary>
    public double GroupSpacing { get; set; }

    /// <summary>Width of a target tile, its name included.</summary>
    public double TileWidth { get; set; }

    /// <summary>Height of a target tile, its name included.</summary>
    public double TileHeight { get; set; }

    /// <summary>Diameter of a person's circle and size of a destination's tile.</summary>
    public double AvatarSize { get; set; }

    /// <summary>Size of a destination's glyph.</summary>
    public double TileGlyphSize { get; set; }

    /// <summary>Gap between the avatar and the name under it.</summary>
    public double NameGap { get; set; }

    /// <summary>Opacity of a person's soft circle over the panel.</summary>
    public double AvatarTintOpacity { get; set; }

    /// <summary>Height of an action row.</summary>
    public double RowHeight { get; set; }

    /// <summary>Space between an action row's edge and its glyph.</summary>
    public double RowInset { get; set; }

    /// <summary>Tint of a tile or row under the pointer.</summary>
    public Color HoverColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        PopoverWidth = 320;
        GroupSpacing = 14;
        TileWidth = 68;
        TileHeight = 82;
        AvatarSize = 48;
        TileGlyphSize = 20;
        NameGap = 6;
        AvatarTintOpacity = isDarkMode ? 0.26 : 0.16;
        RowHeight = 40;
        RowInset = 10;
        HoverColor = Oklch.WithAlpha(ArcPalette.Neutrals(isDarkMode).Foreground, 0.06);
    }
}
