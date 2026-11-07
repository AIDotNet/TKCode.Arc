using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Geometry of <see cref="VoiceOrb"/> (Arc Voice orb, design reference). The orb's colors come from the accent (or the
/// orb's own tints) in the theme.
/// </summary>
[ControlDesignToken]
internal sealed class VoiceOrbToken : AbstractControlDesignToken
{
    /// <summary>The default width and height: 208 (the body takes about 60 %, the rest is halo and ripples).</summary>
    public double DefaultSize { get; set; }

    /// <summary>The ripple ring's stroke at size 200 (it scales with the size).</summary>
    public double RingThickness { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        DefaultSize = 208;
        RingThickness = 2.2;
    }
}
