using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The motion the Special › AI status marks and layouts share where the measured numbers are the same: the settle spring
/// (critically damped, ω ≈ 20 rad/s, no overshoot), which Arc uses for every size, position and scale change of these
/// components. Per-component durations (the check draw, the swap fades, the spin period) stay with their component.
/// </summary>
internal static class AiGlyphMotion
{
    /// <summary>The settle spring: visual duration 0.26 s, no bounce.</summary>
    public static readonly SpringSpec Settle = SpringSpec.FromVisual(0.26, 0);

    /// <summary>Animates a stroke's draw progress to 1 on the standard curve after <paramref name="delay"/>.</summary>
    public static Task<bool> DrawCheck(MotionValue<double> progress, TimeSpan duration, TimeSpan delay) =>
        progress.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Standard, delay));
}
