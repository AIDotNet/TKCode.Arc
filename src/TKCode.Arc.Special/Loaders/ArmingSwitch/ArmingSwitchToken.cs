using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and accent-independent inks of <see cref="ArmingSwitch"/> (Arc Arming switch, design reference): the card's
/// breakpoints, the plate's widest size, the engraving's smallest font size, and the colors of the brushed aluminium plate,
/// the chrome parts, the engraving, the lens, the lamp's three lights and the hold ring's track, per theme. The cover
/// and the ring follow the accent; those come from ArcDesign tokens in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class ArmingSwitchToken : AbstractControlDesignToken
{
    /// <summary>From this card width on (720) the title, status and hint sit in a column beside the panel; below it they stack.</summary>
    public double WideFromWidth { get; set; }

    /// <summary>Below this card width (420) the stacked card tightens its padding and gaps.</summary>
    public double TightBelowWidth { get; set; }

    /// <summary>The widest the plate gets (560).</summary>
    public double PlateMaxWidth { get; set; }

    /// <summary>The engraving never gets smaller than this (9).</summary>
    public double EngraveMinFontSize { get; set; }

    /// <summary>The plate's brushed gradient: bright, middle and dark aluminium.</summary>
    public Color PlateLight { get; set; }

    public Color PlateMid { get; set; }

    public Color PlateDark { get; set; }

    /// <summary>The dark rounded base under the cover, top and bottom.</summary>
    public Color BaseTop { get; set; }

    public Color BaseBottom { get; set; }

    /// <summary>The base's top bevel.</summary>
    public Color BaseHighlight { get; set; }

    /// <summary>The plate's bevel: the light top edge and the dark bottom edge.</summary>
    public Color BevelHighlight { get; set; }

    public Color BevelShadow { get; set; }

    /// <summary>The diagonal chamfer sheen over the plate: light at the top left, dark at the bottom right.</summary>
    public Color ChamferLight { get; set; }

    public Color ChamferShadow { get; set; }

    /// <summary>The 1 px ring around the plate.</summary>
    public Color PlateEdge { get; set; }

    /// <summary>The plate's drop shadow.</summary>
    public Color PlateDrop { get; set; }

    /// <summary>Chrome: highlight, middle, low and edge tones of the hinge, nut, bat, ball and bezel.</summary>
    public Color ChromeHigh { get; set; }

    public Color ChromeMid { get; set; }

    public Color ChromeLow { get; set; }

    public Color ChromeEdge { get; set; }

    /// <summary>The engraved labels and their light and dark edges.</summary>
    public Color Engrave { get; set; }

    public Color EngraveHighlight { get; set; }

    public Color EngraveShadow { get; set; }

    /// <summary>The room light on the plate: the soft glow at the top left and the brushed streaks.</summary>
    public Color Glow { get; set; }

    public Color Streak { get; set; }

    /// <summary>The screw heads, light and dark.</summary>
    public Color ScrewLight { get; set; }

    public Color ScrewDark { get; set; }

    /// <summary>The unlit lens, centre and rim.</summary>
    public Color LensLight { get; set; }

    public Color LensDark { get; set; }

    /// <summary>The ready light when off.</summary>
    public Color LedOff { get; set; }

    /// <summary>The hold ring's track and its highlight.</summary>
    public Color RingTrack { get; set; }

    public Color RingTrackHighlight { get; set; }

    /// <summary>The contact shadows of the parts.</summary>
    public Color PartShadow { get; set; }

    /// <summary>The lamp's lights: armed amber, success green and failure red (the same in both themes).</summary>
    public Color LampAmber { get; set; }

    public Color LampGreen { get; set; }

    public Color LampRed { get; set; }

    /// <summary>How strongly the brushed grain shows (0.24 dark, 0.34 light).</summary>
    public double BrushOpacity { get; set; }

    /// <summary>How dark the cover's tinted shadow is (0.72 dark, 0.5 light).</summary>
    public double CoverShadowOpacity { get; set; }

    /// <summary>The cover walls' opacity (0.76 dark, 0.70 light).</summary>
    public double CoverWallOpacity { get; set; }

    /// <summary>The cover's OKLCH lightness is kept between these (0.54 to 0.64 dark, 0.50 to 0.60 light).</summary>
    public double CoverLightnessMin { get; set; }

    public double CoverLightnessMax { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        WideFromWidth = 720;
        TightBelowWidth = 420;
        PlateMaxWidth = 560;
        EngraveMinFontSize = 9;
        LampAmber = Oklch.ToColor(0.84, 0.165, 76);
        LampGreen = Oklch.ToColor(0.81, 0.19, 150);
        LampRed = Oklch.ToColor(0.67, 0.23, 27);
        if (isDarkMode)
        {
            PlateLight = Oklch.ToColor(0.41, 0.004, 250);
            PlateMid = Oklch.ToColor(0.34, 0.004, 250);
            PlateDark = Oklch.ToColor(0.285, 0.004, 250);
            BaseTop = Oklch.ToColor(0.24, 0.004, 250);
            BaseBottom = Oklch.ToColor(0.165, 0.004, 250);
            BaseHighlight = Oklch.ToColor(1, 0, 0, 0.12);
            BevelHighlight = Oklch.ToColor(1, 0, 0, 0.2);
            BevelShadow = Oklch.ToColor(0, 0, 0, 0.5);
            ChamferLight = Oklch.ToColor(1, 0, 0, 0.3);
            ChamferShadow = Oklch.ToColor(0, 0, 0, 0.45);
            PlateEdge = Oklch.ToColor(1, 0, 0, 0.07);
            PlateDrop = Oklch.ToColor(0, 0, 0, 0.5);
            ChromeHigh = Oklch.ToColor(0.95, 0, 0);
            ChromeMid = Oklch.ToColor(0.64, 0.004, 250);
            ChromeLow = Oklch.ToColor(0.31, 0.005, 250);
            ChromeEdge = Oklch.ToColor(0.20, 0.005, 250);
            Engrave = Oklch.ToColor(0.92, 0.004, 250, 0.84);
            EngraveHighlight = Oklch.ToColor(0, 0, 0, 0.4);
            EngraveShadow = Oklch.ToColor(0, 0, 0, 0.5);
            Glow = Oklch.ToColor(1, 0, 0, 0.13);
            Streak = Oklch.ToColor(1, 0, 0, 0.17);
            ScrewLight = Oklch.ToColor(0.72, 0.003, 250);
            ScrewDark = Oklch.ToColor(0.38, 0.004, 250);
            LensLight = Oklch.ToColor(0.36, 0.045, 70);
            LensDark = Oklch.ToColor(0.17, 0.015, 70);
            LedOff = Oklch.ToColor(0.32, 0.02, 150);
            RingTrack = Oklch.ToColor(0, 0, 0, 0.42);
            RingTrackHighlight = Oklch.ToColor(1, 0, 0, 0.09);
            PartShadow = Oklch.ToColor(0, 0, 0, 0.55);
            BrushOpacity = 0.24;
            CoverShadowOpacity = 0.72;
            CoverWallOpacity = 0.76;
            CoverLightnessMin = 0.54;
            CoverLightnessMax = 0.64;
        }
        else
        {
            PlateLight = Oklch.ToColor(0.955, 0.002, 250);
            PlateMid = Oklch.ToColor(0.89, 0.003, 250);
            PlateDark = Oklch.ToColor(0.83, 0.004, 250);
            BaseTop = Oklch.ToColor(0.31, 0.004, 250);
            BaseBottom = Oklch.ToColor(0.21, 0.004, 250);
            BaseHighlight = Oklch.ToColor(1, 0, 0, 0.22);
            BevelHighlight = Oklch.ToColor(1, 0, 0, 0.9);
            BevelShadow = Oklch.ToColor(0.25, 0.01, 250, 0.2);
            ChamferLight = Oklch.ToColor(1, 0, 0, 0.95);
            ChamferShadow = Oklch.ToColor(0.45, 0.006, 250, 0.38);
            PlateEdge = Oklch.ToColor(0.25, 0.01, 250, 0.16);
            PlateDrop = Oklch.ToColor(0.20, 0.01, 250, 0.2);
            ChromeHigh = Oklch.ToColor(0.995, 0, 0);
            ChromeMid = Oklch.ToColor(0.81, 0.004, 250);
            ChromeLow = Oklch.ToColor(0.46, 0.006, 250);
            ChromeEdge = Oklch.ToColor(0.30, 0.006, 250);
            Engrave = Oklch.ToColor(0.33, 0.006, 250, 0.84);
            EngraveHighlight = Oklch.ToColor(1, 0, 0, 0.78);
            EngraveShadow = Oklch.ToColor(0.20, 0.01, 250, 0.14);
            Glow = Oklch.ToColor(1, 0, 0, 0.55);
            Streak = Oklch.ToColor(1, 0, 0, 0.62);
            ScrewLight = Oklch.ToColor(0.97, 0.002, 250);
            ScrewDark = Oklch.ToColor(0.74, 0.004, 250);
            LensLight = Oklch.ToColor(0.44, 0.05, 70);
            LensDark = Oklch.ToColor(0.23, 0.02, 70);
            LedOff = Oklch.ToColor(0.42, 0.02, 150);
            RingTrack = Oklch.ToColor(0.20, 0.01, 250, 0.14);
            RingTrackHighlight = Oklch.ToColor(1, 0, 0, 0.7);
            PartShadow = Oklch.ToColor(0.20, 0.01, 250, 0.34);
            BrushOpacity = 0.34;
            CoverShadowOpacity = 0.5;
            CoverWallOpacity = 0.70;
            CoverLightnessMin = 0.50;
            CoverLightnessMax = 0.60;
        }
    }
}
