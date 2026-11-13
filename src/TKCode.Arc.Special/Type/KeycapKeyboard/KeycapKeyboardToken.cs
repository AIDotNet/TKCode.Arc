using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent colors of <see cref="KeycapKeyboard"/>. Every keycap measure scales from one key unit
/// (the side of a one-unit key), which grows with the width up to <see cref="MaxKeyUnit"/>; the accent key, the glow, the
/// press tint and the caret take the accent from the ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class KeycapKeyboardToken : AbstractControlDesignToken
{
    /// <summary>Kept for theme overrides written before 1.2.0; no longer read (the key unit is capped by <see cref="MaxKeyUnit"/>).</summary>
    public double KeySize { get; set; }

    /// <summary>Kept for theme overrides written before 1.2.0; no longer read (the gap is 0.14 of the key unit).</summary>
    public double KeyGap { get; set; }

    /// <summary>Kept for theme overrides written before 1.2.0; no longer read (the side is 0.085 of the key unit).</summary>
    public double KeyDepth { get; set; }

    /// <summary>Kept for theme overrides written before 1.2.0; no longer read (the plate padding is 2.2% of the width, 6 to 16 px).</summary>
    public Thickness PlatePadding { get; set; }

    /// <summary>The housing under each keycap: the dark side that shows below the cap and that the cap sinks into.</summary>
    public Color KeySkirtColor { get; set; }

    /// <summary>Largest key unit in px (the side of a one-unit key) when <see cref="KeycapKeyboard.KeySize"/> is not set.</summary>
    public double MaxKeyUnit { get; set; }

    /// <summary>Widest the keyboard grows, text area included.</summary>
    public double MaxBoardWidth { get; set; }

    /// <summary>Space between the text area and the plate.</summary>
    public double TextGap { get; set; }

    /// <summary>The plate the keys sit in.</summary>
    public Color PlateColor { get; set; }

    /// <summary>The hairline around the plate.</summary>
    public Color PlateBorderColor { get; set; }

    /// <summary>The plate's soft drop shadow.</summary>
    public Color PlateShadowColor { get; set; }

    /// <summary>The two drop shadows under each housing.</summary>
    public Color HousingShadowColor { get; set; }

    /// <summary>Top of the keycap's side gradient.</summary>
    public Color CapTopColor { get; set; }

    /// <summary>Bottom of the keycap's side gradient.</summary>
    public Color CapBottomColor { get; set; }

    /// <summary>The 1 px inner ring around each keycap.</summary>
    public Color CapRingColor { get; set; }

    /// <summary>The darker lower lip inside each keycap.</summary>
    public Color CapLipColor { get; set; }

    /// <summary>Top of the dished face gradient.</summary>
    public Color FaceTopColor { get; set; }

    /// <summary>Bottom of the dished face gradient.</summary>
    public Color FaceBottomColor { get; set; }

    /// <summary>The inner shade along the top of the face.</summary>
    public Color FaceShadeColor { get; set; }

    /// <summary>The inner light along the bottom of the face.</summary>
    public Color FaceLightColor { get; set; }

    /// <summary>Character legends at rest.</summary>
    public Color LegendColor { get; set; }

    /// <summary>Corner legends (tab, caps, modifiers) and icons at rest.</summary>
    public Color CornerLegendColor { get; set; }

    /// <summary>Opacity of the accent at the center of the glow under a pressed key.</summary>
    public double GlowOpacity { get; set; }

    /// <summary>Opacity of the accent tint over a pressed keycap's rim.</summary>
    public double PressTintOpacity { get; set; }

    /// <summary>Opacity of the white tint over the pressed accent keycap's rim.</summary>
    public double AccentPressTintOpacity { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        var n = ArcPalette.Neutrals(isDarkMode);
        KeySize = 44;
        KeyGap = 5;
        KeyDepth = 3;
        PlatePadding = new Thickness(10);
        MaxKeyUnit = 54;
        MaxBoardWidth = 760;
        TextGap = 20;
        if (isDarkMode)
        {
            KeySkirtColor = Shade(n, 0.11);
            PlateColor = Shade(n, 0.175);
            PlateBorderColor = Oklch.WithAlpha(Colors.White, 0.07);
            PlateShadowColor = Oklch.WithAlpha(Colors.Black, 0.18);
            HousingShadowColor = Oklch.WithAlpha(Colors.Black, 0.4);
            CapTopColor = Shade(n, 0.29);
            CapBottomColor = Shade(n, 0.255);
            CapRingColor = Oklch.WithAlpha(Colors.White, 0.07);
            CapLipColor = Oklch.WithAlpha(Colors.Black, 0.4);
            FaceTopColor = Shade(n, 0.265);
            FaceBottomColor = Shade(n, 0.295);
            FaceShadeColor = Oklch.WithAlpha(Colors.Black, 0.35);
            FaceLightColor = Oklch.WithAlpha(Colors.White, 0.05);
            LegendColor = Shade(n, 0.86);
            CornerLegendColor = Shade(n, 0.66);
            GlowOpacity = 0.34;
            PressTintOpacity = 0.14;
        }
        else
        {
            KeySkirtColor = Shade(n, 0.84);
            PlateColor = Shade(n, 0.97);
            PlateBorderColor = Oklch.WithAlpha(Colors.Black, 0.07);
            PlateShadowColor = Oklch.WithAlpha(Colors.Black, 0.035);
            HousingShadowColor = Oklch.WithAlpha(Colors.Black, 0.1);
            CapTopColor = n.Background;
            CapBottomColor = Shade(n, 0.965);
            CapRingColor = Oklch.WithAlpha(Colors.Black, 0.09);
            CapLipColor = Oklch.WithAlpha(Colors.Black, 0.1);
            FaceTopColor = Shade(n, 0.968);
            FaceBottomColor = Shade(n, 0.996);
            FaceShadeColor = Oklch.WithAlpha(Colors.Black, 0.1);
            FaceLightColor = Oklch.WithAlpha(Colors.White, 0.5);
            LegendColor = Shade(n, 0.4);
            CornerLegendColor = Shade(n, 0.56);
            GlowOpacity = 0.3;
            PressTintOpacity = 0.12;
        }
        AccentPressTintOpacity = 0.08;
    }

    /// <summary>A gray of OKLCH lightness <paramref name="lightness"/> on the line from the background to the foreground.</summary>
    private static Color Shade(ArcNeutralColors n, double lightness)
    {
        var background = Oklch.FromColor(n.Background).L;
        var foreground = Oklch.FromColor(n.Foreground).L;
        var span = foreground - background;
        return Math.Abs(span) < 1e-6 ? n.Background : Oklch.MixOklab(n.Background, n.Foreground, (lightness - background) / span);
    }
}
