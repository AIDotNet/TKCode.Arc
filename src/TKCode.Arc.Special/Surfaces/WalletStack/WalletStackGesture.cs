using TKCode.Arc.Gestures;

namespace TKCode.Arc;

/// <summary>
/// The drag physics of the wallet stack (Arc Wallet stack, design reference), as pure functions: a vertical drag cycles
/// the cards 1:1 with iOS rubber banding past the ends, a release lands on the card a 0.2s linear projection reaches
/// (never more than one card from where the drag started), and pulling the open card down closes it when the projected
/// open amount falls under one half.
/// </summary>
internal static class WalletStackGesture
{
    /// <summary>Movement before a press becomes a drag (6px still opened the card in Arc, 9px dragged).</summary>
    public const double Threshold = 8;

    /// <summary>The release coasts this long at its velocity before snapping (fitted between 0.19 and 0.206s).</summary>
    public static readonly TimeSpan Projection = TimeSpan.FromSeconds(0.2);

    /// <summary>Arc's rubber-band constant (fits the measured stretch to 0.02px).</summary>
    public const double RubberFactor = 0.55;

    /// <summary>
    /// Stretch limit of an open card pulled upward, as a fraction of the card width (one sample: a 140px pull lifted the
    /// open amount to 1.16 at 296px).
    /// </summary>
    public const double OpenRubberFraction = 0.213;

    /// <summary>
    /// The stack position during a drag: <paramref name="start"/> plus the offset in cards, with rubber banding past the
    /// first and the last card.
    /// </summary>
    public static double DragPosition(double start, double offset, double distance, int count)
    {
        if (distance <= 0 || count <= 0)
        {
            return start;
        }
        var raw = start + offset / distance;
        var last = count - 1;
        if (raw < 0)
        {
            return RubberBand.Apply(raw * distance, distance, RubberFactor) / distance;
        }
        if (raw > last)
        {
            return last + RubberBand.Apply((raw - last) * distance, distance, RubberFactor) / distance;
        }
        return raw;
    }

    /// <summary>
    /// The card a release settles on: <c>round(pos + v·0.2s)</c> (v in cards per second), then at most one card from
    /// <paramref name="startIndex"/> and inside the stack.
    /// </summary>
    public static int ReleaseTarget(double position, double velocity, double distance, int startIndex, int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        var cardsPerSecond = distance > 0 ? velocity / distance : 0;
        var projected = MomentumProjector.ProjectedPosition(position, cardsPerSecond, SnapProjection.Linear(Projection));
        var target = (int)Math.Floor(projected + 0.5);
        target = Math.Clamp(target, startIndex - 1, startIndex + 1);
        return Math.Clamp(target, 0, count - 1);
    }

    /// <summary>
    /// The open amount while the open card is pulled: down by <paramref name="offset"/> px it falls as
    /// <c>o = start − offset/distance</c> (never below 0); pulled up past 1 it stretches with resistance.
    /// </summary>
    public static double OpenAmount(double start, double offset, double distance, double cardWidth)
    {
        if (distance <= 0)
        {
            return start;
        }
        var raw = start - offset / distance;
        if (raw <= 0)
        {
            return 0;
        }
        if (raw > 1)
        {
            var limit = OpenRubberFraction * cardWidth;
            return 1 + RubberBand.Apply((raw - 1) * distance, limit, RubberFactor) / distance;
        }
        return raw;
    }

    /// <summary>
    /// Whether releasing the pulled open card closes it: the open amount projected 0.2s ahead at the release velocity
    /// (px/s, positive downward) falls under one half.
    /// </summary>
    public static bool ReleaseCloses(double open, double velocity, double distance)
    {
        var rate = distance > 0 ? -velocity / distance : 0;
        return MomentumProjector.ProjectedPosition(open, rate, SnapProjection.Linear(Projection)) < 0.5;
    }
}
