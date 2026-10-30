using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and paper colors of <see cref="StickerPeel"/> (Arc Sticker peel, design reference). The surface, border and
/// focus ring colors come from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class StickerPeelToken : AbstractControlDesignToken
{
    /// <summary>The surface is at least 280 high, so a few stickers have room.</summary>
    public double SurfaceMinHeight { get; set; }

    /// <summary>The white die cut border around a sticker's printed face.</summary>
    public Thickness DieCutPadding { get; set; }

    /// <summary>Color of the die cut border.</summary>
    public Color DieCutColor { get; set; }

    /// <summary>Color of the plain paper backing a folded corner shows.</summary>
    public Color BackingColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        SurfaceMinHeight = 280;
        DieCutPadding = new Thickness(6);
        DieCutColor = isDarkMode ? Color.FromRgb(0xF3, 0xF1, 0xEC) : Colors.White;
        BackingColor = isDarkMode ? Color.FromRgb(0xCF, 0xCB, 0xC2) : Color.FromRgb(0xEC, 0xE9, 0xE2);
    }
}
