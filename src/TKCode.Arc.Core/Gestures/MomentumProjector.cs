using TKCode.Arc.Motion;

namespace TKCode.Arc.Gestures;

/// <summary>Options for <see cref="MomentumProjector.SelectSnapPoint"/>.</summary>
public readonly record struct SnapProjection
{
    /// <summary>Linear projection horizon: the release coasts this long at its velocity (Arc bottom sheet: 0.2s).</summary>
    public TimeSpan Horizon { get; init; }

    /// <summary>Used instead of <see cref="Horizon"/> when positive: exponential decay per millisecond (0.99 / 0.995).</summary>
    public double DecayRate { get; init; }

    /// <summary>Index of the snap point the gesture started from, or −1.</summary>
    public int FromIndex { get; init; }

    /// <summary>
    /// A release faster than this (px/s) always moves at least one snap point in its direction even when the projection
    /// lands on <see cref="FromIndex"/> again (bottom sheet 320, carousel 360, swipe actions 900). Zero or less disables the rule.
    /// </summary>
    public double FlickVelocity { get; init; }

    /// <summary>Linear projection over <paramref name="horizon"/>.</summary>
    public static SnapProjection Linear(TimeSpan horizon, int fromIndex = -1, double flickVelocity = 0)
        => new() { Horizon = horizon, FromIndex = fromIndex, FlickVelocity = flickVelocity };

    /// <summary>Exponential decay projection (<see cref="MomentumProjector.Project(double, double)"/>).</summary>
    public static SnapProjection Decay(double rate = MomentumProjector.DefaultDecayRate, int fromIndex = -1,
                                       double flickVelocity = 0)
        => new() { DecayRate = rate, FromIndex = fromIndex, FlickVelocity = flickVelocity };
}

/// <summary>
/// Release physics (S10): where a flick would come to rest, which snap point it picks, and a hand-off to a
/// <see cref="MotionValue{T}"/> spring that keeps the release velocity.
/// </summary>
public static class MomentumProjector
{
    /// <summary>Default per-millisecond decay (about 0.1s of travel).</summary>
    public const double DefaultDecayRate = 0.99;

    /// <summary>
    /// Distance travelled by <paramref name="velocity"/> (px/s) decaying exponentially at <paramref name="rate"/> per
    /// millisecond: <c>v/1000 × rate/(1 − rate)</c> (0.99 ≈ 0.1s of travel, 0.995 ≈ 0.2s).
    /// </summary>
    public static double Project(double velocity, double rate = DefaultDecayRate)
    {
        if (rate <= 0 || rate >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "The decay rate must be in (0, 1).");
        }
        return velocity / 1000 * rate / (1 - rate);
    }

    /// <summary>Linear projection: <c>velocity × horizon</c>.</summary>
    public static double Project(double velocity, TimeSpan horizon) => velocity * horizon.TotalSeconds;

    /// <summary>Resting position of a release at <paramref name="position"/> with <paramref name="velocity"/>.</summary>
    public static double ProjectedPosition(double position, double velocity, in SnapProjection options)
        => position + (options.DecayRate > 0 ? Project(velocity, options.DecayRate) : Project(velocity, options.Horizon));

    /// <summary>Index of the point closest to <paramref name="position"/> (−1 when empty).</summary>
    public static int NearestIndex(ReadOnlySpan<double> points, double position)
    {
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < points.Length; i++)
        {
            var distance = Math.Abs(points[i] - position);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// Picks the snap point a release settles on: the point nearest to the projected position. A flick faster than
    /// <see cref="SnapProjection.FlickVelocity"/> that would land back on <see cref="SnapProjection.FromIndex"/> moves to
    /// the neighbouring point in the direction of the velocity instead. Points need not be sorted.
    /// </summary>
    /// <returns>The chosen index, or −1 when <paramref name="points"/> is empty.</returns>
    public static int SelectSnapPoint(ReadOnlySpan<double> points, double position, double velocity, in SnapProjection options)
    {
        var projected = ProjectedPosition(position, velocity, options);
        var target = NearestIndex(points, projected);
        var from = options.FromIndex;
        if (target < 0 || target != from || from < 0 || from >= points.Length || options.FlickVelocity <= 0 ||
            Math.Abs(velocity) <= options.FlickVelocity)
        {
            return target;
        }
        // The neighbour in the direction of travel: the closest point strictly beyond the starting point.
        var origin = points[from];
        var next = from;
        var nextDistance = double.PositiveInfinity;
        for (var i = 0; i < points.Length; i++)
        {
            var delta = (points[i] - origin) * Math.Sign(velocity);
            if (delta > 0 && delta < nextDistance)
            {
                next = i;
                nextDistance = delta;
            }
        }
        return next;
    }

    /// <summary>
    /// Hands a release off to <paramref name="value"/>: springs to <paramref name="target"/> starting with
    /// <paramref name="velocity"/> (px/s, in the value's units), so the motion continues the gesture without a seam.
    /// Respects the motion level of the value's owner (Reduced and Off jump).
    /// </summary>
    public static Task<bool> SettleTo(MotionValue<double> value, double target, double velocity, SpringSpec spring)
        => value.AnimateTo(target, MotionSpec.Spring(spring), velocity);

    /// <inheritdoc cref="SettleTo(MotionValue{double}, double, double, SpringSpec)"/>
    public static Task<bool> SettleTo(MotionValue<double> value, double target, double velocity, ArcSpring spring = ArcSpring.Smooth)
        => value.AnimateTo(target, MotionSpec.Spring(spring), velocity);
}
