using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The accent-independent sprite colors of <see cref="PixelMascot"/>, derived from the theme neutrals: the outline, the
/// shell and its shade, the screen (always dark, so the eyes glow in light and dark themes) and the floor shadow. The
/// accent (antenna, chest light, typed bar, sparkles and the eyes' glow) and the danger color come from the ArcDesign
/// tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class PixelMascotToken : AbstractControlDesignToken
{
    /// <summary>The outline around the head, body, arms, bubble and keyboard.</summary>
    public Color OutlineColor { get; set; }

    /// <summary>The shell of the head and body, the thought bubble and the keys.</summary>
    public Color ShellColor { get; set; }

    /// <summary>The shaded underside of the shell, the chest panel, unlit dots and the keyboard.</summary>
    public Color ShadeColor { get; set; }

    /// <summary>The face screen and the lit dots of the thought bubble.</summary>
    public Color ScreenColor { get; set; }

    /// <summary>The shadow on the floor under the feet.</summary>
    public Color ShadowColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        if (isDarkMode)
        {
            OutlineColor = Oklch.MixOklab(n.Background, Colors.Black, 0.55);
            ShellColor = Oklch.MixOklab(n.Foreground, n.Background, 0.2);
            ShadeColor = Oklch.MixOklab(n.Foreground, n.Background, 0.45);
            ScreenColor = Oklch.MixOklab(n.Background, Colors.Black, 0.35);
            ShadowColor = n.Border;
        }
        else
        {
            OutlineColor = n.Foreground;
            ShellColor = n.SurfaceRaised;
            ShadeColor = n.ControlTrack;
            ScreenColor = Oklch.MixOklab(n.Foreground, n.Surface, 0.08);
            ShadowColor = n.Border;
        }
    }
}
