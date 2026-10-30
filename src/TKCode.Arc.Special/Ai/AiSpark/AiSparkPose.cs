using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The pure math of <see cref="AiSpark"/>: every status is a periodic function of time and of the ray's position around
/// the ring, written into a small pose vector (the four primary ray lengths, the four diagonal lengths, the diagonals'
/// tone, the core radius and the scale of the whole mark), plus the one-shot gestures (the done gather and the error
/// shake), the spin speeds and the optical weight hint. Nothing here allocates.
/// </summary>
internal static class AiSparkPose
{
    /// <summary>The number of values in a pose.</summary>
    public const int Count = 11;

    /// <summary>Index of the first primary ray length (four values, clockwise from 12 o'clock).</summary>
    public const int Primary = 0;

    /// <summary>Index of the first diagonal ray length (four values, clockwise from 1:30).</summary>
    public const int Diagonal = 4;

    /// <summary>Index of the diagonals' tone: 1 is the usual lighter tone, lower is dimmer.</summary>
    public const int DiagonalTone = 8;

    /// <summary>Index of the core radius (1 at rest).</summary>
    public const int Core = 9;

    /// <summary>Index of the whole mark's scale (1 at rest).</summary>
    public const int Scale = 10;

    /// <summary>The idle breath (6.4s), the thinking ripple (1.9s per turn) and the writing heartbeat (1.04s).</summary>
    public const double IdlePeriod = 6.4;

    public const double ThinkingPeriod = 1.9;

    public const double BeatPeriod = 1.04;

    /// <summary>Spin while thinking (26 degrees per second) and while writing (46 degrees per second).</summary>
    public const double ThinkingSpin = 26;

    public const double GeneratingSpin = 46;

    /// <summary>The done gather: in over 170ms, back to rest over 510ms with no overshoot.</summary>
    public const double GatherIn = 0.17;

    public const double GatherOut = 0.51;

    /// <summary>The error shake (460ms) and the error tone change (320ms).</summary>
    public const double ShakeDuration = 0.46;

    public const double ErrorToneDuration = 0.32;

    /// <summary>The blend between still poses (and the color change) under reduced motion: 200ms.</summary>
    public const double ReducedBlend = 0.2;

    /// <summary>The smallest rendered size (px) that shows the two orbiting particles while writing.</summary>
    public const double ParticleMinSize = 40;

    /// <summary>Under this rendered size (px) the diagonals are drawn as dots.</summary>
    public const double DotSize = 18;

    /// <summary>
    /// The status blend: a critically damped spring of visual duration 0.48s (it reads as a 420 to 560ms blend), started
    /// from the pose and velocity the mark is in, so an interrupted blend is the next blend's starting point.
    /// </summary>
    public static readonly SpringSpec Blend = SpringSpec.FromVisual(0.48, 0);

    /// <summary>How long the blend runs before the mark counts as settled (s).</summary>
    public static readonly double BlendSettle = Blend.EstimateSettle(0.0005, 0.002).TotalSeconds;

    private static readonly double[] s_primaryJitter = [0, 0.06, -0.04, 0.05];
    private static readonly double[] s_diagonalJitter = [0.03, -0.05, 0.04, -0.02];

    /// <summary>Writes the rest pose (every value 1).</summary>
    public static void Rest(Span<double> pose)
    {
        for (var i = 0; i < Count; i++)
        {
            pose[i] = 1;
        }
    }

    /// <summary>
    /// Writes the target pose of <paramref name="status"/> at loop time <paramref name="t"/> (s). <paramref name="still"/>
    /// gives the status's still pose (reduced or no motion); <paramref name="twinkle"/> false makes idle fully still.
    /// </summary>
    public static void Write(AiSparkStatus status, bool twinkle, bool still, double t, Span<double> pose)
    {
        Rest(pose);
        switch (status)
        {
            case AiSparkStatus.Idle:
                if (twinkle && !still)
                {
                    WriteIdle(t, pose);
                }
                break;
            case AiSparkStatus.Thinking:
                WriteThinking(still ? 0 : t, pose);
                break;
            case AiSparkStatus.Generating:
                if (still)
                {
                    for (var i = 0; i < 4; i++)
                    {
                        pose[Primary + i] = 1.06;
                        pose[Diagonal + i] = 0.96;
                    }
                    pose[Core] = 1.06;
                }
                else
                {
                    WriteGenerating(t, pose);
                }
                break;
            case AiSparkStatus.Error:
                for (var i = 0; i < 4; i++)
                {
                    pose[Primary + i] = 0.74;
                    pose[Diagonal + i] = 0.7;
                }
                pose[DiagonalTone] = 0.45;
                pose[Core] = 0.88;
                pose[Scale] = 0.96;
                break;
        }
    }

    /// <summary>Whether <paramref name="status"/> moves on its own (a loop that never settles).</summary>
    public static bool Loops(AiSparkStatus status, bool twinkle) =>
        status is AiSparkStatus.Thinking or AiSparkStatus.Generating || (status == AiSparkStatus.Idle && twinkle);

    /// <summary>The spin speed of <paramref name="status"/> in degrees per second (before the speed multiplier).</summary>
    public static double SpinSpeed(AiSparkStatus status) => status switch
    {
        AiSparkStatus.Thinking => ThinkingSpin,
        AiSparkStatus.Generating => GeneratingSpin,
        _ => 0
    };

    // A 6.4s breath of the whole mark while one diagonal at a time (clockwise, a quarter of the period each) reaches out.
    private static void WriteIdle(double t, Span<double> pose)
    {
        var u = Frac(t / IdlePeriod);
        var breath = 0.5 - 0.5 * Math.Cos(2 * Math.PI * u);
        pose[Scale] = 0.975 + 0.05 * breath;
        pose[Core] = 0.96 + 0.08 * breath;
        for (var i = 0; i < 4; i++)
        {
            var phase = Frac(u - i * 0.25);
            var reach = phase < 0.25 ? Square(Math.Sin(Math.PI * phase / 0.25)) : 0;
            pose[Diagonal + i] = 1 + 0.24 * reach;
        }
    }

    // One crest travels clockwise around the ring every 1.9s; each ray rises as it passes, with a small offset of its own.
    private static void WriteThinking(double t, Span<double> pose)
    {
        var crest = 2 * Math.PI * t / ThinkingPeriod;
        for (var i = 0; i < 4; i++)
        {
            pose[Primary + i] = 0.9 + 0.2 * Crest(crest - i * Math.PI / 2 - s_primaryJitter[i]);
            pose[Diagonal + i] = 0.84 + 0.36 * Crest(crest - Math.PI / 4 - i * Math.PI / 2 - s_diagonalJitter[i]);
        }
        pose[DiagonalTone] = 0.92;
        pose[Core] = 0.92;
        pose[Scale] = 0.97;
    }

    // A lub dub every 1.04s: the vertical axis fires, the horizontal axis 50ms later, the diagonals answer 110ms after it.
    private static void WriteGenerating(double t, Span<double> pose)
    {
        var u = t / BeatPeriod;
        var vertical = Beat(u, 0);
        var horizontal = Beat(u, 0.05);
        var diagonal = 0.75 * Beat(u, 0.11);
        for (var i = 0; i < 4; i++)
        {
            pose[Primary + i] = 0.93 + 0.2 * (i % 2 == 0 ? vertical : horizontal);
            pose[Diagonal + i] = 0.86 + 0.3 * diagonal;
        }
        pose[Core] = 0.96 + 0.14 * vertical;
        pose[Scale] = 1 + 0.025 * vertical;
    }

    /// <summary>The lub (full) and the dub (55%, 170ms later) of the heartbeat at beat phase <paramref name="u"/>.</summary>
    public static double Beat(double u, double offset) => Pulse(u - offset) + 0.55 * Pulse(u - offset - 0.17);

    private static double Pulse(double x)
    {
        var d = Frac(x + 0.5) - 0.5;
        return Math.Exp(-Square(d / 0.055));
    }

    private static double Crest(double angle)
    {
        var c = 0.5 + 0.5 * Math.Cos(angle);
        return c * c * c;
    }

    /// <summary>
    /// The done gather at <paramref name="tau"/> seconds after it began: 0 → 1 over 170ms (smoothstep), then back to 0 over
    /// 510ms on an ease-out (no overshoot); 0 outside.
    /// </summary>
    public static double Gather(double tau)
    {
        if (tau <= 0 || tau >= GatherIn + GatherOut)
        {
            return 0;
        }
        if (tau < GatherIn)
        {
            var p = tau / GatherIn;
            return p * p * (3 - 2 * p);
        }
        var q = 1 - (tau - GatherIn) / GatherOut;
        return q * q * q;
    }

    /// <summary>The error shake's horizontal offset in viewBox units at <paramref name="tau"/> seconds: 2.5 swings that decay to 0.</summary>
    public static double Shake(double tau)
    {
        if (tau <= 0 || tau >= ShakeDuration)
        {
            return 0;
        }
        var p = tau / ShakeDuration;
        return 3.2 * Math.Sin(2 * Math.PI * 2.5 * p) * Square(1 - p);
    }

    /// <summary>
    /// The optical weight of the rays at a rendered size of <paramref name="px"/>: hinted on a log scale, 1 at 32px, heavier
    /// below (1.3 at 16px) and slightly lighter above, within 0.92 to 1.5.
    /// </summary>
    public static double Weight(double px)
    {
        if (!(px > 0))
        {
            return 1;
        }
        return Math.Clamp(1 + 0.3 * Math.Log2(32 / px), 0.92, 1.5);
    }

    /// <summary>The standard ease-out used for the reduced blend and the color change.</summary>
    public static double EaseOut(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var q = 1 - p;
        return 1 - q * q * q;
    }

    internal static double Frac(double x) => x - Math.Floor(x);

    private static double Square(double x) => x * x;
}
