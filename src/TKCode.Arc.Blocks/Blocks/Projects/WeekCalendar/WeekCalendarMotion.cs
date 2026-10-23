using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Motion of the week calendar, fitted from Arc's frames (notes §6). Kept here rather than in the token: springs are not
/// token types, and these values belong to this block only.
/// </summary>
internal static class WeekCalendarMotion
{
    /// <summary>
    /// The range slide (previous / next, Today, a day in the month, the arrow keys past an edge): the header row, the all-day
    /// row and the grid travel one pane width together, critically damped (k ≈ 130, c ≈ 23; Arc fit k 114–140, c 20.5–24),
    /// 10% left after about 340ms, at rest after about a second. Interrupted slides keep their speed.
    /// </summary>
    public static readonly SpringSpec RangeSlide = SpringSpec.FromVisual(0.46, 0);

    /// <summary>The old view fades out in 160ms on the standard curve when the view changes.</summary>
    public static readonly TimeSpan ViewFadeOut = ArcMotionPresets.Fast;

    /// <summary>The new view fades in over 240ms on the enter curve, starting with the old one's fade.</summary>
    public static readonly TimeSpan ViewFadeIn = ArcMotionPresets.Standard;

    /// <summary>The all-day row grows or folds in 240ms on the standard curve.</summary>
    public static readonly TimeSpan AllDayResize = ArcMotionPresets.Standard;

    /// <summary>The curve of the old view's fade.</summary>
    public static Easing FadeOutEasing => ArcEasings.Standard;

    /// <summary>The curve of the new view's fade.</summary>
    public static Easing FadeInEasing => ArcEasings.Enter;

    /// <summary>The fade out of a pane that leaves in place.</summary>
    public static MotionSpec FadeOut() => MotionSpec.Tween(ViewFadeOut, FadeOutEasing);

    /// <summary>The fade in of a pane that arrives in place.</summary>
    public static MotionSpec FadeIn() => MotionSpec.Tween(ViewFadeIn, FadeInEasing);

    // Editing (notes B §6) ----------------------------------------------------------------------------------------------

    /// <summary>
    /// A dragged event hopping between 15-minute slots and day columns: critically damped, ω ≈ 32.5 (Arc fit k ≈ 1056,
    /// c ≈ 65; ≈ 330ms to rest), restarting from where it is drawn at every slot.
    /// </summary>
    public static readonly SpringSpec Hop = SpringSpec.FromVisual(0.16, 0);

    /// <summary>
    /// Events settling into their places after a drop, an edit, a keyboard move, an undo, a cancelled drag, a delete or a
    /// restore (Arc fit ω ≈ 13.75, ζ ≈ 1; Smooth).
    /// </summary>
    public static readonly SpringSpec Relayout = ArcMotionPresets.Smooth;

    /// <summary>The popovers' scale from 0.96 (Arc fit ω ≈ 12.75, ζ ≈ 0.98; Smooth) and the toast's rise from 12px.</summary>
    public static readonly SpringSpec PopoverScale = ArcMotionPresets.Smooth;

    /// <summary>The popovers' opacity: 160ms ease-out (CSS <c>ease-out</c>, not Arc's enter curve).</summary>
    public static MotionSpec PopoverFadeIn() => MotionSpec.Tween(ArcMotionPresets.Fast, EaseOut);

    /// <summary>The popovers leave in 120ms on the standard curve, shrinking to 0.98.</summary>
    public static MotionSpec PopoverExit() => MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);

    /// <summary>The toast leaves in 160ms on the standard curve, dropping 8px.</summary>
    public static MotionSpec ToastExit() => MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);

    /// <summary>Reduced motion: the toast fades in over 160ms (no rise).</summary>
    public static MotionSpec ToastFadeIn() => MotionSpec.Tween(ArcMotionPresets.Fast, new LinearEasing());

    /// <summary>CSS <c>ease-out</c>.</summary>
    public static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
}
