using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// One frame of a card's unfurl, all in the lane's coordinates: the visible rectangle and its corner radius, the inset
/// of that rectangle inside the card (the clip), the card's offset from its slot, and the opacities and scales that ride
/// on the same progress.
/// </summary>
internal readonly record struct LinkUnfurlMorphFrame(
    Rect Visible,
    double Radius,
    double ClipLeft,
    double ClipTop,
    Vector Offset,
    double Opacity,
    double Lift,
    double BodyOpacity,
    double ImageScale,
    double SlotFactor);

/// <summary>
/// The unfurl of Arc Link unfurl (design reference), fitted frame by frame (notes 6.2): one spring progress p (0 = the
/// link, 1 = the card) drives everything. Width and x lead (pw = 1 − (1 − p)^1.6), height and y follow (ph = p^1.35);
/// the corner radius goes from the link's 13 to the card's 18 linearly; the card is clipped to the visible rectangle,
/// with its favicon kept on the link's favicon (an inset of 10·(1 − pw) by 10·(1 − ph)); the plate's shadow lifts only
/// in flight (sin πp); the card fades in over the first tenth, the body over p 0.27–0.77, and the image settles from
/// 1.08. The lane's slot is (card height + gap)·ph tall, so the toolbar glides with the card.
/// </summary>
internal static class LinkUnfurlMorph
{
    /// <summary>The link's corner radius (half its 26px height) and the settled card's.</summary>
    public const double LinkRadius = 13;

    public const double CardRadius = 18;

    /// <summary>The link's height.</summary>
    public const double LinkHeight = 26;

    /// <summary>How far the card's favicon sits inside the card beyond the link's (15 − 5 on both axes).</summary>
    public const double FaviconOffset = 10;

    /// <summary>The opening spring (k 110, c 23; ζ ≈ 1.10, settles in about a second) and the quicker fold (k 224, c 34; ζ ≈ 1.14).</summary>
    public static readonly SpringSpec Open = SpringSpec.FromPhysical(110, 23);

    public static readonly SpringSpec Fold = SpringSpec.FromPhysical(224, 34);

    /// <summary>The same spring played <paramref name="speed"/> times as fast (time scaled: k·s², c·s).</summary>
    public static SpringSpec Scaled(SpringSpec spec, double speed)
    {
        var s = Math.Clamp(speed, 0.05, 20);
        return Math.Abs(s - 1) < 1e-9 ? spec : SpringSpec.FromPhysical(spec.Stiffness * s * s, spec.Damping * s, spec.Mass);
    }

    public static double Wide(double p) => 1 - Math.Pow(1 - Math.Clamp(p, 0, 1), 1.6);

    public static double Deep(double p) => Math.Pow(Math.Clamp(p, 0, 1), 1.35);

    /// <summary>
    /// The frame at progress <paramref name="p"/> between the link's box <paramref name="link"/> (26 high) and the card's
    /// settled box <paramref name="slot"/>.
    /// </summary>
    public static LinkUnfurlMorphFrame Compute(Rect link, Rect slot, double p)
    {
        p = Math.Clamp(p, 0, 1);
        var pw = Wide(p);
        var ph = Deep(p);
        var x = Lerp(link.X, slot.X, pw);
        var w = Lerp(link.Width, slot.Width, pw);
        var y = Lerp(link.Y, slot.Y, ph);
        var h = Lerp(LinkHeight, slot.Height, ph);
        var radius = Lerp(LinkRadius, CardRadius, p);
        var left = FaviconOffset * (1 - pw);
        var top = FaviconOffset * (1 - ph);
        var offset = new Vector(x - left - slot.X, y - top - slot.Y);
        var opacity = Math.Min(1, 10 * p);
        var lift = Math.Sin(Math.PI * p);
        var body = Math.Clamp((p - 0.27) / 0.5, 0, 1);
        var scale = 1 + 0.08 * Math.Pow(1 - p, 0.8);
        return new LinkUnfurlMorphFrame(new Rect(x, y, Math.Max(0, w), Math.Max(0, h)), radius, left, top, offset, opacity, lift, body,
            scale, ph);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

/// <summary>
/// Drag a card's header (or, with a mouse, the card) up to fold it (Arc Link unfurl, design reference). The card follows
/// the pointer: an upward drag of 80% of the card's height folds it all the way (80px up folds a 344px card by 0.29, as
/// measured); a drag down holds it open. On release it folds past 40% or after an upward flick, and otherwise springs
/// back open with the release speed.
/// </summary>
internal static class LinkUnfurlDragFold
{
    /// <summary>The share of the card's height a drag must travel to fold it completely.</summary>
    public const double TravelShare = 0.8;

    /// <summary>The fold past which a release folds the card (Arc's "about 40%").</summary>
    public const double FoldThreshold = 0.4;

    /// <summary>An upward release faster than this (px/s) folds the card whatever its fold; a faster downward one keeps it open.</summary>
    public const double FlickSpeed = 400;

    /// <summary>The open progress after dragging <paramref name="dy"/> (negative = up) from <paramref name="start"/>.</summary>
    public static double Progress(double start, double dy, double cardHeight) =>
        Math.Clamp(start + dy / Travel(cardHeight), 0, 1);

    /// <summary>The progress speed (per second) of a pointer moving at <paramref name="velocityY"/> px/s.</summary>
    public static double ProgressVelocity(double velocityY, double cardHeight) => velocityY / Travel(cardHeight);

    /// <summary>Whether a release at <paramref name="progress"/> moving at <paramref name="velocityY"/> px/s folds the card.</summary>
    public static bool ShouldFold(double progress, double velocityY)
    {
        if (velocityY <= -FlickSpeed)
        {
            return true;
        }
        if (velocityY >= FlickSpeed)
        {
            return false;
        }
        return 1 - progress >= FoldThreshold;
    }

    private static double Travel(double cardHeight) => Math.Max(40, TravelShare * cardHeight);
}
