using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and key colors of <see cref="OnScreenKeyboard"/>. The key colors are neutral mixes (independent of the
/// accent): white keys on a light grey tray in light mode, lighter keys on a dark tray in dark mode, function keys a
/// step darker than the character keys.
/// </summary>
[ControlDesignToken]
internal sealed class OnScreenKeyboardToken : AbstractControlDesignToken
{
    /// <summary>Height of a row of keys when the keyboard is not given a height (touch target: at least 44).</summary>
    public double KeyHeight { get; set; }

    /// <summary>Width of one key unit (a standard key and its gap) when the keyboard is not given a width.</summary>
    public double KeyUnitWidth { get; set; }

    /// <summary>Space between two keys of a row.</summary>
    public double KeyGap { get; set; }

    /// <summary>Space between two rows.</summary>
    public double RowGap { get; set; }

    /// <summary>Space between the tray edge and the keys.</summary>
    public Thickness TrayPadding { get; set; }

    /// <summary>Thickness of a key's bottom lip (the key cap's darker bottom edge); pressed keys drop to 1.</summary>
    public double KeyLipThickness { get; set; }

    /// <summary>Size of the glyphs of Backspace, Enter, Shift and the arrows.</summary>
    public double GlyphSize { get; set; }

    /// <summary>Opacity of a disabled key.</summary>
    public double DisabledOpacity { get; set; }

    /// <summary>Opacity of the Enter key while pressed (as the primary button).</summary>
    public double PrimaryPressedOpacity { get; set; }

    /// <summary>The tray behind the keys.</summary>
    public Color TrayBackground { get; set; }

    /// <summary>A character key at rest.</summary>
    public Color KeyBackground { get; set; }

    /// <summary>A character key under the mouse pointer.</summary>
    public Color KeyHoverBackground { get; set; }

    /// <summary>A character key while pressed.</summary>
    public Color KeyPressedBackground { get; set; }

    /// <summary>A function key (Shift, Backspace, page keys, arrows) at rest.</summary>
    public Color FunctionKeyBackground { get; set; }

    /// <summary>A function key under the mouse pointer.</summary>
    public Color FunctionKeyHoverBackground { get; set; }

    /// <summary>The 1px edge around a key.</summary>
    public Color KeyEdge { get; set; }

    /// <summary>The key cap's bottom lip.</summary>
    public Color KeyLip { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        KeyHeight = 46;
        KeyUnitWidth = 52;
        KeyGap = 6;
        RowGap = 8;
        TrayPadding = new Thickness(8);
        KeyLipThickness = 2;
        GlyphSize = 24;
        DisabledOpacity = 0.45;
        PrimaryPressedOpacity = 0.84;
        if (isDarkMode)
        {
            TrayBackground = n.Surface;
            KeyBackground = Oklch.MixOklab(n.SurfaceMuted, n.BorderStrong, 0.42);
            KeyHoverBackground = Oklch.MixOklab(n.SurfaceMuted, n.BorderStrong, 0.58);
            KeyPressedBackground = Oklch.MixOklab(n.SurfaceMuted, n.BorderStrong, 0.12);
            FunctionKeyBackground = Oklch.MixOklab(n.SurfaceMuted, n.Border, 0.6);
            FunctionKeyHoverBackground = Oklch.MixOklab(n.SurfaceMuted, n.BorderStrong, 0.3);
            KeyEdge = Oklch.MixOklab(n.Border, n.BorderStrong, 0.35);
            KeyLip = n.Background;
        }
        else
        {
            TrayBackground = Oklch.MixOklab(n.SurfaceMuted, n.Border, 0.55);
            KeyBackground = n.SurfaceRaised;
            KeyHoverBackground = Oklch.MixOklab(n.SurfaceRaised, n.SurfaceMuted, 0.9);
            KeyPressedBackground = Oklch.MixOklab(n.Border, n.BorderStrong, 0.3);
            FunctionKeyBackground = Oklch.MixOklab(n.Border, n.BorderStrong, 0.22);
            FunctionKeyHoverBackground = Oklch.MixOklab(n.Border, n.BorderStrong, 0.36);
            KeyEdge = n.Border;
            KeyLip = n.BorderStrong;
        }
    }
}
