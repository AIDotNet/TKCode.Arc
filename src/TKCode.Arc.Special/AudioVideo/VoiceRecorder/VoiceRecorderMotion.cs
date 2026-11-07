using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The motion constants fitted frame by frame from Arc Voice recorder (design reference, notes 6.1–6.6).
/// </summary>
internal static class VoiceRecorderMotion
{
    /// <summary>
    /// The layout morph (mic to bar, bar to mic, bar to bubble): ω 12.4 rad/s, ζ 1.00 (k ≈ 153.8, c ≈ 24.8), 99% at about
    /// 530ms, at rest by about 950ms.
    /// </summary>
    public static readonly SpringSpec Layout = SpringSpec.FromPhysical(12.4 * 12.4, 2 * 12.4);

    /// <summary>The trash, stop and send buttons popping in: ω 20.1, ζ 1.00 (Arc's 500ms linear() curve).</summary>
    public static readonly SpringSpec PopIn = SpringSpec.FromPhysical(20.1 * 20.1, 2 * 20.1);

    /// <summary>The mic coming back after a send: ω 12.5, ζ 1.00 (Arc's 700ms linear() curve).</summary>
    public static readonly SpringSpec MicReturn = SpringSpec.FromPhysical(12.5 * 12.5, 2 * 12.5);

    /// <summary>Under reduced motion the pop-ins keep their opacity over about the same time (springs jump at that level).</summary>
    public static readonly TimeSpan ReducedPopIn = TimeSpan.FromMilliseconds(500);

    public static readonly TimeSpan ReducedMicReturn = TimeSpan.FromMilliseconds(700);

    /// <summary>The scale the trash, stop and send buttons pop in from (and leave to).</summary>
    public const double SideFromScale = 0.7;

    /// <summary>The scale the mic pops back in from after a send.</summary>
    public const double MicFromScale = 0.8;

    /// <summary>The list's glide to the bottom after a send (Chrome's native smooth scroll measured about 134ms, in-out).</summary>
    public static readonly TimeSpan Scroll = TimeSpan.FromMilliseconds(134);

    /// <summary>The record dot breathes 1 → 0.35 → 1 every 1.2 seconds, each half ease-in-out.</summary>
    public const double DotPeriod = 1.2;

    public const double DotLow = 0.35;

    /// <summary>The record dot's opacity <paramref name="seconds"/> into the take.</summary>
    public static double DotOpacity(double seconds)
    {
        var phase = (seconds % DotPeriod) / DotPeriod;
        var half = phase < 0.5 ? phase * 2 : (1 - phase) * 2;
        var eased = half < 0.5 ? 2 * half * half : 1 - Math.Pow(-2 * half + 2, 2) / 2;
        return 1 - (1 - DotLow) * eased;
    }
}
