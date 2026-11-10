using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The glass material shared by <see cref="GlassCard"/>, <see cref="GlassMenu"/>, <see cref="GlassSegmented"/> and
/// <see cref="GlassTabBar"/>: how strongly the backdrop is blurred, the tints laid over it, the edge light and shade, the
/// pointer sheen and the selection lens. Every color is a neutral white or the neutral surface at some alpha, so the
/// material does not change with the accent. Corner radii, shadows and the opaque fallback come from the ArcDesign tokens
/// in the themes.
/// </summary>
[ControlDesignToken]
internal sealed class GlassPaneToken : AbstractControlDesignToken
{
    /// <summary>Blur radius of the backdrop seen through the glass, in px.</summary>
    public double BlurRadius { get; set; }

    /// <summary>Tint over the blurred backdrop: the surface color, translucent.</summary>
    public Color Tint { get; set; }

    /// <summary>Tint when there is no backdrop to blur: more opaque, since what shows through is sharp.</summary>
    public Color FallbackTint { get; set; }

    /// <summary>The bright side of the 1px edge light (top left).</summary>
    public Color EdgeLight { get; set; }

    /// <summary>The far side of the edge (bottom right): a faint shade that separates the glass from a light backdrop.</summary>
    public Color EdgeShade { get; set; }

    /// <summary>The soft glaze across the top of the glass, fading downwards.</summary>
    public Color Glaze { get; set; }

    /// <summary>The center of the pointer sheen; it fades to nothing at its radius.</summary>
    public Color Sheen { get; set; }

    /// <summary>Radius of the pointer sheen, in px.</summary>
    public double SheenRadius { get; set; }

    /// <summary>Fill of the selection lens and of a menu's highlight.</summary>
    public Color LensFill { get; set; }

    /// <summary>The bright rim along the top of the selection lens.</summary>
    public Color LensRim { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        BlurRadius = 22;
        Tint = Oklch.WithAlpha(n.Surface, isDarkMode ? 0.52 : 0.46);
        FallbackTint = Oklch.WithAlpha(n.Surface, isDarkMode ? 0.86 : 0.84);
        EdgeLight = Color.FromArgb(isDarkMode ? (byte)66 : (byte)230, 255, 255, 255);
        EdgeShade = Oklch.WithAlpha(isDarkMode ? n.Background : n.Foreground, isDarkMode ? 0.5 : 0.1);
        Glaze = Color.FromArgb(isDarkMode ? (byte)18 : (byte)82, 255, 255, 255);
        Sheen = Color.FromArgb(isDarkMode ? (byte)34 : (byte)120, 255, 255, 255);
        SheenRadius = 180;
        LensFill = Color.FromArgb(isDarkMode ? (byte)30 : (byte)150, 255, 255, 255);
        LensRim = Color.FromArgb(isDarkMode ? (byte)72 : (byte)255, 255, 255, 255);
    }
}
