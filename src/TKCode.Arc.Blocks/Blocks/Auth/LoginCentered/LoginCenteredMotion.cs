// Ported from Arc's free login-centered block (MIT, Copyright (c) 2026 Elia Kuratli): the motion constants of login-centered.tsx.
using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The motion constants of <see cref="LoginCentered"/>, written as the source has them.</summary>
internal static class LoginCenteredMotion
{
    /// <summary>motion's default curve for a tween that names no ease ("easeOut", cubic-bezier(0, 0, .58, 1)).</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);

    /// <summary><c>motionTokens.spring.smooth</c>: visual duration .4, no bounce (steps rising, the card's height).</summary>
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);

    /// <summary><c>motionTokens.spring.snappy</c>: visual duration .26, no bounce (the rolling seconds).</summary>
    public static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0);

    /// <summary>The backdrop's pointer drift: <c>useSpring(…, { visualDuration: .9, bounce: 0 })</c>.</summary>
    public static readonly SpringSpec Drift = SpringSpec.FromVisual(0.9, 0);

    public static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan Considered = TimeSpan.FromMilliseconds(480);

    /// <summary>
    /// Steps (<c>stepMotion</c>): they rise into place out of a 4px blur, 12px in the direction of travel on the smooth
    /// spring, fading in over 240ms on the enter curve after 60ms (the blur clears after 40ms); the leaving step falls 8px
    /// the other way into the same blur over 180ms on the standard curve. Reduced: a 120ms fade in, the old step goes at once.
    /// </summary>
    public static readonly LoginCenteredPresenceStyle Step = new(
        EnterBlur: 4,
        EnterOffset: MotionSpec.Spring(Smooth),
        EnterOpacity: MotionSpec.Tween(Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(60)),
        EnterBlurSpec: MotionSpec.Tween(Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(40)),
        ExitBlur: 4,
        ExitOffset: MotionSpec.Tween(Exit, ArcEasings.Standard),
        ExitOpacity: MotionSpec.Tween(Exit, ArcEasings.Standard),
        ExitBlurSpec: MotionSpec.Tween(Exit, ArcEasings.Standard),
        ReducedEnter: MotionSpec.Tween(Instant, EaseOut));

    /// <summary>
    /// Line swaps (<c>swap</c>: the status line and the resend label): the new line rises 6px out of a 4px blur over 240ms
    /// on the enter curve; the old one rises 4px into a 2px blur over 160ms. Reduced: a 120ms fade in, the old line goes at once.
    /// </summary>
    public static readonly LoginCenteredPresenceStyle Swap = new(
        EnterBlur: 4,
        EnterOffset: MotionSpec.Tween(Standard, ArcEasings.Enter),
        EnterOpacity: MotionSpec.Tween(Standard, ArcEasings.Enter),
        EnterBlurSpec: MotionSpec.Tween(Standard, ArcEasings.Enter),
        ExitBlur: 2,
        ExitOffset: MotionSpec.Tween(Fast, EaseOut),
        ExitOpacity: MotionSpec.Tween(Fast, EaseOut),
        ExitBlurSpec: MotionSpec.Tween(Fast, EaseOut),
        ReducedEnter: MotionSpec.Tween(Instant, EaseOut));

    /// <summary>
    /// Rolling seconds (<c>roll</c>): each changed character slides .7em on the snappy spring (down while the timer runs,
    /// up when a new code restarts it) through a 2px blur, fading over 160ms. Reduced: no motion at all.
    /// </summary>
    public static readonly LoginCenteredPresenceStyle Roll = new(
        EnterBlur: 2,
        EnterOffset: MotionSpec.Spring(Snappy),
        EnterOpacity: MotionSpec.Tween(Fast, EaseOut),
        EnterBlurSpec: MotionSpec.Tween(Fast, EaseOut),
        ExitBlur: 2,
        ExitOffset: MotionSpec.Spring(Snappy),
        ExitOpacity: MotionSpec.Tween(Fast, EaseOut),
        ExitBlurSpec: MotionSpec.Tween(Fast, EaseOut),
        ReducedEnter: null);
}

/// <summary>
/// How a <see cref="LoginCenteredPresence"/> brings content in and takes it out: blur radii and the motion of each channel.
/// Offsets are given per swap (they depend on the direction). <paramref name="ReducedEnter"/> null means no fade under reduced motion.
/// </summary>
internal sealed record LoginCenteredPresenceStyle(
    double EnterBlur,
    MotionSpec EnterOffset,
    MotionSpec EnterOpacity,
    MotionSpec EnterBlurSpec,
    double ExitBlur,
    MotionSpec ExitOffset,
    MotionSpec ExitOpacity,
    MotionSpec ExitBlurSpec,
    MotionSpec? ReducedEnter);
