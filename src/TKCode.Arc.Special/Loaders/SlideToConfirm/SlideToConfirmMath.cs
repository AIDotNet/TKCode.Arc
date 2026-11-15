using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The numbers behind <see cref="SlideToConfirm"/>'s motion (Arc Slide to confirm, design reference; fitted from
/// per-frame samples, mass 1, px): the springs, the rubber band, the liquid's neck, the track's swell, the thumb's
/// stretch, the label's fade and the confirm choreography keyed to the track's retreat. Pure functions, no state.
/// </summary>
internal static class SlideToConfirmMath
{
    /// <summary>The thumb follows the pointer (k 2000, c 92.5).</summary>
    public static readonly SpringSpec Follow = SpringSpec.FromPhysical(2000, 92.5);

    /// <summary>The liquid's tail follows the thumb (k 383, c 33.8).</summary>
    public static readonly SpringSpec Tail = SpringSpec.FromPhysical(383, 33.8);

    /// <summary>An early release, Home and the return after a reset (k 470, c 31.5; keeps the release velocity).</summary>
    public static readonly SpringSpec Release = SpringSpec.FromPhysical(470, 31.5);

    /// <summary>Arrow and Page keys (k 318, c 21.9: about 9% overshoot).</summary>
    public static readonly SpringSpec Key = SpringSpec.FromPhysical(318, 21.9);

    /// <summary>The thumb to the end on confirm and on End (k 348, c 38.9).</summary>
    public static readonly SpringSpec Commit = SpringSpec.FromPhysical(348, 38.9);

    /// <summary>The track drawing into the thumb and expanding back (k 180, c 27.6; about 600 ms).</summary>
    public static readonly SpringSpec Retreat = SpringSpec.FromPhysical(180, 27.6);

    /// <summary>The press swell of the thumb, the liquid's head and the track (k 533, c 15: 34% overshoot, a wobble).</summary>
    public static readonly SpringSpec Press = SpringSpec.FromPhysical(533, 15);

    /// <summary>Travel per second of a held Space or Enter, in track lengths (1.15).</summary>
    public const double GlideSpeed = 1.15;

    /// <summary>The thumb radius the measured lengths refer to (23, on the 56 px track).</summary>
    public const double ReferenceRadius = 23;

    /// <summary>Gap between the thumb and the track edge (5).</summary>
    public const double Inset = 5;

    /// <summary>
    /// Where the thumb's centre sits for a pointer target past the end: the end plus a rubber band that approaches
    /// 26.7 px (scaled with the thumb).
    /// </summary>
    public static double RubberThumb(double over, double scale)
        => over <= 0 ? over : RubberBand.Apply(over, 26.7 * scale, 0.505);

    /// <summary>How far the track's right end stretches for a thumb <paramref name="overshoot"/> past the end.</summary>
    public static double TrackStretch(double overshoot)
        => overshoot <= 0 ? 0 : (0.75 * overshoot) + (0.8 * (1 - Math.Exp(-overshoot / 2)));

    /// <summary>Half the thickness of the liquid's neck at its waist for a gap <paramref name="gap"/> between the tail and the thumb.</summary>
    public static double NeckWaist(double gap, double radius)
        => Math.Clamp(radius * (1 - (0.0065 * gap)), radius * 0.16, radius);

    /// <summary>The track's outward swell next to the thumb: 2.4 px when pressed plus up to 3.4 px with speed.</summary>
    public static double SwellAmplitude(double press, double speed, double scale)
        => scale * ((2.4 * press) + (Math.Clamp(press, 0, 1) * 3.4 * (1 - Math.Exp(-Math.Abs(speed) / 400))));

    /// <summary>The swell's width (σ of the bell around the thumb), 22 px on the 56 px track.</summary>
    public static double SwellSigma(double scale) => 22 * scale;

    /// <summary>Horizontal stretch of the thumb at <paramref name="speed"/> px/s: rx/ry, capped at 1.364.</summary>
    public static double StretchRatio(double speed) => 1 + Math.Min(0.00034 * Math.Abs(speed), 0.364);

    /// <summary>The label's opacity for the slide progress: 1 at the start, 0.3 at the end.</summary>
    public static double LabelOpacity(double progress) => 1 - (0.7 * Math.Clamp(progress, 0, 1));

    /// <summary>How far the label drifts right for the slide progress (12 px over the full slide).</summary>
    public static double LabelDrift(double progress) => 12 * Math.Clamp(progress, 0, 1);

    /// <summary>The label while the track retreats into the thumb: gone at a third of the way.</summary>
    public static double LabelRetreatCap(double retreat) => 0.3 * Math.Clamp(1 - (retreat / 0.32), 0, 1);

    /// <summary>The pending or confirmed text, fading in over the second half of the retreat.</summary>
    public static double StatusOpacity(double retreat) => Math.Clamp((retreat - 0.55) / 0.40, 0, 1);

    /// <summary>The spinner, fading in near the end of the retreat (the chevron fades out by as much).</summary>
    public static double SpinnerOpacity(double retreat) => Math.Clamp((retreat - 0.65) / 0.35, 0, 1);

    /// <summary>
    /// The next keyboard value for a Page step: a quarter of the track, then the nearest tenth (halves round up), so
    /// 0 → 0.3 → 0.6 and 0.6 → 0.4.
    /// </summary>
    public static double PageStep(double value, int direction)
        => Math.Clamp(Math.Floor(((value + (0.25 * direction)) * 10) + 0.5 + 1e-9) / 10, 0, 1);

    /// <summary>The next keyboard value for an arrow step (a tenth, landing on tenths).</summary>
    public static double ArrowStep(double value, int direction)
        => Math.Clamp(Math.Round((value * 10) + direction, MidpointRounding.AwayFromZero) / 10, 0, 1);

    // The idle nudge, sampled from Arc every 33 ms (px on the 56 px track): out to 18.2 px in about 440 ms and back.
    private static readonly double[] s_hint =
    [
        0, 0.01, 0.10, 0.44, 1.20, 2.47, 4.25, 6.44, 8.89, 11.4, 13.76, 15.76, 17.25, 18.1, 18.23, 17.65, 16.39, 14.55,
        12.3, 9.77, 7.2, 4.76, 2.64, 1.03, 0.0, -0.15, -0.19, -0.16, -0.11, -0.06, -0.02, 0.02, 0.05, 0.03, 0.01, 0
    ];

    /// <summary>The nudge's step between samples (ms) and its length.</summary>
    public const double HintStep = 1000.0 / 30;

    public static double HintDuration => (s_hint.Length - 1) * HintStep;

    /// <summary>The nudge's offset (px on the reference track) <paramref name="ms"/> after it starts; 0 after it ends.</summary>
    public static double HintOffset(double ms)
    {
        if (ms <= 0 || ms >= HintDuration)
        {
            return 0;
        }
        var f = ms / HintStep;
        var i = (int)f;
        var t = f - i;
        // Catmull-Rom between the samples keeps the velocity (and so the stretch and the tail) smooth.
        var p0 = s_hint[Math.Max(0, i - 1)];
        var p1 = s_hint[i];
        var p2 = s_hint[Math.Min(s_hint.Length - 1, i + 1)];
        var p3 = s_hint[Math.Min(s_hint.Length - 1, i + 2)];
        return 0.5 * ((2 * p1) + ((-p0 + p2) * t) + (((2 * p0) - (5 * p1) + (4 * p2) - p3) * t * t)
                      + ((-p0 + (3 * p1) - (3 * p2) + p3) * t * t * t));
    }

    /// <summary>Arc's standard curve (0.22, 1, 0.36, 1) as an ease-out for the short fades, clamped to 0..1.</summary>
    public static double EaseOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return ArcEasings.Standard.Ease(t);
    }

    /// <summary>A symmetric ease for the chevron-to-check morph.</summary>
    public static double EaseInOut(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - (2 * t));
    }

    /// <summary>Advances a spring toward <paramref name="target"/> by <paramref name="dt"/> seconds (closed form).</summary>
    public static void Step(in SpringSpec spec, ref double value, ref double velocity, double target, double dt)
    {
        var (x, v) = SpringSolver.Evaluate(spec, value - target, velocity, dt);
        value = target + x;
        velocity = v;
    }

    /// <summary>True when a spring is close enough to its target to stop.</summary>
    public static bool IsSettled(double value, double velocity, double target, double epsilon = 0.01)
        => Math.Abs(value - target) < epsilon && Math.Abs(velocity) < epsilon * 10;

    /// <summary>The chevron's three points (px around the thumb's centre).</summary>
    public static readonly (double X, double Y)[] Chevron = [(-2.5, -6.5), (3.5, 0), (-2.5, 6.5)];

    /// <summary>The check's three points: a short arm down to a low corner left of centre, a long arm up to the right.</summary>
    public static readonly (double X, double Y)[] Check = [(-7, 0.5), (-2.5, 5.5), (7.5, -5.5)];
}
