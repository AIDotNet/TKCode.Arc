using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Geometry and the theme-dependent grays of <see cref="BlackHole"/> (Arc Black hole, design reference: measured on the
/// public preview). The core is pure black in both themes; the photon ring and the shockwave are grays that follow the
/// foreground (dark on light, light on dark), so they carry no accent.
/// </summary>
[ControlDesignToken]
internal sealed class BlackHoleToken : AbstractControlDesignToken
{
    /// <summary>How much the core grows while it eats (0.4: 5.5 → 7.7 DIP).</summary>
    public double CoreGrowth { get; set; }

    /// <summary>Einstein radius of the lens as a multiple of the core radius (measured 3.92–4.09).</summary>
    public double EinsteinRatio { get; set; }

    /// <summary>Distance over which the dark halo fades by 1/e (DIP, at the resting core radius).</summary>
    public double HaloDecay { get; set; }

    /// <summary>Opacity of the dark halo 4 DIP outside the core.</summary>
    public double HaloOpacity { get; set; }

    /// <summary>Width (FWHM) of the photon ring (DIP, at the resting core radius).</summary>
    public double PhotonRingWidth { get; set; }

    /// <summary>Color and opacity of the photon ring just outside the core.</summary>
    public Color PhotonRingColor { get; set; }

    /// <summary>Width (FWHM) of the shockwave ring (DIP).</summary>
    public double ShockWidth { get; set; }

    /// <summary>Opacity of the shockwave ring when it starts.</summary>
    public double ShockOpacity { get; set; }

    /// <summary>Color of the shockwave ring: darkens on light surfaces, lightens on dark ones.</summary>
    public Color ShockColor { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CoreGrowth = 0.4;
        EinsteinRatio = 4.0;
        HaloDecay = 10.5;
        HaloOpacity = isDarkMode ? 0.11 : 0.16;
        PhotonRingWidth = 2;
        PhotonRingColor = isDarkMode ? Color.FromArgb(173, 0xB0, 0xB2, 0xB2) : Color.FromArgb(145, 0x2A, 0x2A, 0x2A);
        ShockWidth = 6;
        ShockOpacity = 0.6;
        ShockColor = isDarkMode ? Color.FromRgb(0xBB, 0xBB, 0xBB) : Color.FromRgb(0x34, 0x34, 0x34);
    }
}
