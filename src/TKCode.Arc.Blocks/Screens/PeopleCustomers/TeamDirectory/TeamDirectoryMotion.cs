using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Motion measured on Arc Team directory (design reference, fitted from frame captures): one critically damped spring
/// (ω ≈ 12.4, no overshoot) moves the team highlight, the selected row's highlight and the rows that close or open a gap;
/// rows leaving and entering fade in place on an ease-out curve; the profile cross-fades, the new one rising 4px.
/// Reduced motion: everything is instant (Arc turns its layout animations and fades off).
/// </summary>
internal static class TeamDirectoryMotion
{
    /// <summary>The layout spring: visual duration .42, no bounce (ω ≈ 12.5, ζ 1).</summary>
    public static readonly SpringSpec Layout = SpringSpec.FromVisual(0.42, 0);

    /// <summary>CSS ease-out, cubic-bezier(0, 0, .58, 1): rows leaving and entering.</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary>A row leaving fades out in place (fitted 170ms).</summary>
    public static readonly TimeSpan RowExit = TimeSpan.FromMilliseconds(170);

    /// <summary>A row entering fades in in place (fitted 150ms).</summary>
    public static readonly TimeSpan RowEnter = TimeSpan.FromMilliseconds(150);

    /// <summary>The outgoing profile fades out on the enter curve (175–180ms).</summary>
    public static readonly TimeSpan PaneExit = TimeSpan.FromMilliseconds(180);

    /// <summary>The incoming profile fades in on the enter curve (210–220ms).</summary>
    public static readonly TimeSpan PaneEnter = TimeSpan.FromMilliseconds(220);

    /// <summary>The incoming profile rises on the standard curve (230ms).</summary>
    public static readonly TimeSpan PaneRise = TimeSpan.FromMilliseconds(230);

    public static MotionSpec RowExitSpec => MotionSpec.Tween(RowExit, EaseOut);

    public static MotionSpec RowEnterSpec => MotionSpec.Tween(RowEnter, EaseOut);

    public static MotionSpec PaneExitSpec => MotionSpec.Tween(PaneExit, ArcEasings.Enter);

    public static MotionSpec PaneEnterSpec => MotionSpec.Tween(PaneEnter, ArcEasings.Enter);

    public static MotionSpec PaneRiseSpec => MotionSpec.Tween(PaneRise, ArcEasings.Standard);
}
