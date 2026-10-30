namespace TKCode.Arc;

/// <summary>
/// The pure math of <see cref="GhostMascot"/>: each status's pose (a small vector of values the drawing reads), the
/// 400ms pose blend, the two traveling hem waves, the swirl's draw-and-erase window, the writing cycle, the done sparkle,
/// the error wobble and the eyes' critically damped spring. Nothing here allocates.
/// </summary>
internal static class GhostMascotPose
{
    /// <summary>Vertical offset of the whole ghost in drawing units (negative is up).</summary>
    public const int Lift = 0;

    /// <summary>Horizontal offset of the ghost in drawing units (writing leans toward the line).</summary>
    public const int Shift = 1;

    /// <summary>Tilt of the ghost in degrees (clockwise).</summary>
    public const int Tilt = 2;

    /// <summary>Scale of the ghost (listening comes forward).</summary>
    public const int Scale = 3;

    /// <summary>Opacity of the sheet (thinking turns translucent).</summary>
    public const int Opacity = 4;

    /// <summary>How open the eyes are (1 at rest).</summary>
    public const int EyeOpen = 5;

    /// <summary>The eyes' size (wide while listening).</summary>
    public const int EyeSize = 6;

    /// <summary>Happy eyes (0 ovals, 1 arcs).</summary>
    public const int Happy = 7;

    /// <summary>Droopy eyes (error).</summary>
    public const int Droop = 8;

    /// <summary>The small smile (done).</summary>
    public const int Smile = 9;

    /// <summary>Visibility of the thinking swirl.</summary>
    public const int Swirl = 10;

    /// <summary>Visibility of the writing pen and line.</summary>
    public const int Pen = 11;

    /// <summary>Visibility of the done sparkle.</summary>
    public const int Sparkle = 12;

    /// <summary>Amplitude of the float (1 is the idle float).</summary>
    public const int FloatAmp = 13;

    /// <summary>Rate of the float in cycles per second.</summary>
    public const int FloatRate = 14;

    /// <summary>Amplitude of the hem waves (1 is the idle ripple).</summary>
    public const int HemAmp = 15;

    /// <summary>Rate of the hem waves in cycles per second.</summary>
    public const int HemRate = 16;

    /// <summary>Rate of the swirl cycle in cycles per second.</summary>
    public const int SwirlRate = 17;

    /// <summary>Rate of the writing cycle in cycles per second.</summary>
    public const int WriteRate = 18;

    /// <summary>How much the ghost blinks (idle 1, listening less, otherwise 0).</summary>
    public const int Blink = 19;

    /// <summary>How much the eyes glance around (idle).</summary>
    public const int Glance = 20;

    /// <summary>The eye spring's natural frequency (rad/s): quick saccades, a slow drift up, tight pen tracking.</summary>
    public const int GazeStiffness = 21;

    /// <summary>The number of values in a pose.</summary>
    public const int Count = 22;

    /// <summary>The status blend: every pose value moves from the frame on screen to the new pose over 400ms.</summary>
    public const double BlendDuration = 0.4;

    /// <summary>The done sparkle grows in 320ms (ease out) with a quarter turn.</summary>
    public const double SparkleGrowDuration = 0.32;

    /// <summary>The error wobble decays to rest in about 1.2s.</summary>
    public const double WobbleDuration = 1.2;

    /// <summary>A blink lasts 160ms.</summary>
    public const double BlinkDuration = 0.16;

    /// <summary>The share of the writing cycle spent writing; the rest erases while the pen glides back.</summary>
    public const double WriteShare = 0.68;

    /// <summary>The words of the writing line as spans of the line (0 to 1); the pen lifts between them.</summary>
    public static readonly double[] WordStarts = [0, 0.34, 0.74];

    /// <summary>The ends of the words (see <see cref="WordStarts"/>).</summary>
    public static readonly double[] WordEnds = [0.26, 0.66, 1];

    // Idle glance targets, cycled with pseudo-random dwell times.
    private static readonly double[] s_glanceX = [0, -0.75, 0.7, 0.1, 0.55, -0.45, 0];
    private static readonly double[] s_glanceY = [0, 0.15, -0.25, 0.35, 0.3, -0.3, -0.05];

    /// <summary>Writes the target pose of <paramref name="status"/> into <paramref name="pose"/>.</summary>
    public static void Write(GhostMascotStatus status, Span<double> pose)
    {
        // Idle: the base pose every status starts from.
        pose[Lift] = 0;
        pose[Shift] = 0;
        pose[Tilt] = 0;
        pose[Scale] = 1;
        pose[Opacity] = 1;
        pose[EyeOpen] = 1;
        pose[EyeSize] = 1;
        pose[Happy] = 0;
        pose[Droop] = 0;
        pose[Smile] = 0;
        pose[Swirl] = 0;
        pose[Pen] = 0;
        pose[Sparkle] = 0;
        pose[FloatAmp] = 1;
        pose[FloatRate] = 0.36;
        pose[HemAmp] = 1;
        pose[HemRate] = 0.55;
        pose[SwirlRate] = 0.42;
        pose[WriteRate] = 0.32;
        pose[Blink] = 1;
        pose[Glance] = 1;
        pose[GazeStiffness] = 26;
        switch (status)
        {
            case GhostMascotStatus.Listening:
                pose[Lift] = -1.5;
                pose[Tilt] = -5;
                pose[Scale] = 1.07;
                pose[EyeOpen] = 1.16;
                pose[EyeSize] = 1.14;
                pose[FloatAmp] = 0.6;
                pose[FloatRate] = 0.3;
                pose[HemAmp] = 0.8;
                pose[HemRate] = 0.45;
                pose[Blink] = 0.6;
                pose[Glance] = 0;
                pose[GazeStiffness] = 16;
                break;
            case GhostMascotStatus.Thinking:
                pose[Lift] = -4;
                pose[Scale] = 0.98;
                pose[Opacity] = 0.68;
                pose[EyeOpen] = 0.92;
                pose[EyeSize] = 0.95;
                pose[Swirl] = 1;
                pose[FloatAmp] = 0.7;
                pose[FloatRate] = 0.26;
                pose[HemAmp] = 1.1;
                pose[HemRate] = 0.4;
                pose[Blink] = 0;
                pose[Glance] = 0;
                pose[GazeStiffness] = 4.5;
                break;
            case GhostMascotStatus.Writing:
                pose[Lift] = -1;
                pose[Shift] = -2;
                pose[Tilt] = 7;
                pose[EyeOpen] = 0.9;
                pose[Pen] = 1;
                pose[FloatAmp] = 0.45;
                pose[FloatRate] = 0.4;
                pose[HemAmp] = 0.9;
                pose[HemRate] = 0.7;
                pose[Blink] = 0;
                pose[Glance] = 0;
                pose[GazeStiffness] = 34;
                break;
            case GhostMascotStatus.Done:
                pose[Lift] = -5;
                pose[Scale] = 1.03;
                pose[Happy] = 1;
                pose[Smile] = 1;
                pose[Sparkle] = 1;
                pose[FloatAmp] = 0.8;
                pose[FloatRate] = 0.42;
                pose[HemAmp] = 1.2;
                pose[HemRate] = 0.8;
                pose[Blink] = 0;
                pose[Glance] = 0;
                pose[GazeStiffness] = 14;
                break;
            case GhostMascotStatus.Error:
                pose[Lift] = 4;
                pose[Scale] = 0.97;
                pose[EyeOpen] = 0.7;
                pose[Droop] = 1;
                pose[FloatAmp] = 0.3;
                pose[FloatRate] = 0.22;
                pose[HemAmp] = 0.5;
                pose[HemRate] = 0.3;
                pose[Blink] = 0;
                pose[Glance] = 0;
                pose[GazeStiffness] = 10;
                break;
        }
    }

    /// <summary>
    /// Where the eyes look for <paramref name="status"/> (x, y in -1 to 1; y down), apart from the idle glances and the
    /// pen tip, which the caller adds.
    /// </summary>
    public static (double X, double Y) Gaze(GhostMascotStatus status) => status switch
    {
        GhostMascotStatus.Listening => (0, 0.15),
        GhostMascotStatus.Thinking => (0.4, -1),
        GhostMascotStatus.Done => (0, -0.15),
        GhostMascotStatus.Error => (-0.1, 0.7),
        _ => (0, 0)
    };

    /// <summary>The idle glance target number <paramref name="index"/> (wrapping).</summary>
    public static (double X, double Y) GlanceAt(int index)
    {
        var i = ((index % s_glanceX.Length) + s_glanceX.Length) % s_glanceX.Length;
        return (s_glanceX[i], s_glanceY[i]);
    }

    /// <summary>A repeatable pseudo-random value in 0 to 1 for <paramref name="n"/> (dwell times and blink gaps).</summary>
    public static double Hash(int n)
    {
        var x = Math.Sin(n * 12.9898 + 78.233) * 43758.5453;
        return x - Math.Floor(x);
    }

    /// <summary>
    /// The pose blend at progress <paramref name="p"/> (0 to 1) of value <paramref name="x0"/> moving at
    /// <paramref name="v0"/> per second toward <paramref name="x1"/>: a cubic Hermite over
    /// <see cref="BlendDuration"/> that starts with the value's own velocity and ends at rest. From rest it is the
    /// classic ease in and out (smoothstep). Returns the value and its velocity per second.
    /// </summary>
    public static (double X, double V) Blend(double x0, double v0, double x1, double p)
    {
        if (p >= 1)
        {
            return (x1, 0);
        }
        if (p <= 0)
        {
            return (x0, v0);
        }
        var t = BlendDuration;
        var p2 = p * p;
        var p3 = p2 * p;
        var h00 = 2 * p3 - 3 * p2 + 1;
        var h10 = p3 - 2 * p2 + p;
        var h01 = -2 * p3 + 3 * p2;
        var x = h00 * x0 + h10 * t * v0 + h01 * x1;
        var d00 = 6 * p2 - 6 * p;
        var d10 = 3 * p2 - 4 * p + 1;
        var d01 = -6 * p2 + 6 * p;
        var v = (d00 * x0 + d10 * t * v0 + d01 * x1) / t;
        return (x, v);
    }

    /// <summary>
    /// The hem at <paramref name="x"/>: two traveling sine waves (one moving right, one left at 1.37 times the rate)
    /// around <paramref name="baseline"/>, scaled by <paramref name="amp"/>. Returns the height and the slope dy/dx.
    /// </summary>
    public static (double Y, double Slope) Hem(double x, double baseline, double amp, double phase)
    {
        const double a1 = 2.4;
        const double a2 = 1.1;
        const double k1 = 2 * Math.PI / 17;
        const double k2 = 2 * Math.PI / 29;
        var p1 = k1 * x - 2 * Math.PI * phase;
        var p2 = k2 * x + 2 * Math.PI * phase * 1.37 + 1.1;
        var y = baseline + amp * (a1 * Math.Sin(p1) + a2 * Math.Sin(p2));
        var slope = amp * (a1 * k1 * Math.Cos(p1) + a2 * k2 * Math.Cos(p2));
        return (y, slope);
    }

    /// <summary>
    /// The drawn part of the swirl at cycle phase <paramref name="phase"/>, as a window along the spiral from its outer end
    /// (0) to its center (1): the head draws in first, the tail erases behind it, and both meet at the wrap (empty).
    /// </summary>
    public static (double Tail, double Head) SwirlWindow(double phase)
    {
        var s = Frac(phase);
        var head = Smoothstep(Math.Clamp(s / 0.55, 0, 1));
        var tail = Smoothstep(Math.Clamp((s - 0.45) / 0.55, 0, 1));
        return (tail, head);
    }

    /// <summary>
    /// The writing cycle at phase <paramref name="phase"/>: how much of the line is written (0 to 1), where the pen tip is
    /// along it and how high it is lifted (drawing units). The pen writes word by word, lifting between words, then glides
    /// back while the line erases in front of it; the line is empty when the cycle wraps.
    /// </summary>
    public static (double Written, double Pen, double PenLift) Writing(double phase)
    {
        const double lift = 2.4;
        var s = Frac(phase);
        if (s < WriteShare)
        {
            var p = s / WriteShare;
            var up = 0.0;
            for (var i = 0; i < WordStarts.Length - 1; i++)
            {
                var gapStart = WordEnds[i];
                var gapEnd = WordStarts[i + 1];
                if (p > gapStart && p < gapEnd)
                {
                    up = Math.Sin(Math.PI * (p - gapStart) / (gapEnd - gapStart));
                }
            }
            return (p, p, lift * up);
        }
        var q = Smoothstep((s - WriteShare) / (1 - WriteShare));
        return (1 - q, 1 - q, lift * Math.Sin(Math.PI * q));
    }

    /// <summary>The still writing pose for reduced motion: half the line written, the pen down in the middle word.</summary>
    public static double StillWritingPhase => WriteShare * 0.5;

    /// <summary>The sparkle's growth at <paramref name="tau"/> s after done began: 0 to 1 over 320ms on an ease out.</summary>
    public static double SparkleGrow(double tau) => EaseOut(tau / SparkleGrowDuration);

    /// <summary>
    /// The error wobble in degrees at <paramref name="tau"/> s after the error began: one swing that decays to rest in
    /// about 1.2s, softened at the start so it never kicks.
    /// </summary>
    public static double Wobble(double tau)
    {
        if (tau <= 0 || tau >= WobbleDuration)
        {
            return 0;
        }
        var soften = 1 - Math.Exp(-tau / 0.06);
        var fade = 1 - Smoothstep(Math.Clamp((tau - 0.9) / 0.3, 0, 1));
        return 9 * Math.Sin(2 * Math.PI * 2.2 * tau) * Math.Exp(-tau / 0.3) * soften * fade;
    }

    /// <summary>How closed a blink is at <paramref name="tau"/> s after it began (0 open, 1 shut).</summary>
    public static double BlinkClosure(double tau) =>
        tau <= 0 || tau >= BlinkDuration ? 0 : Math.Sin(Math.PI * tau / BlinkDuration);

    /// <summary>
    /// Steps a critically damped spring (natural frequency <paramref name="omega"/>) toward <paramref name="target"/> over
    /// <paramref name="dt"/> with the exact solution, so it is stable at any frame time and never overshoots from rest.
    /// </summary>
    public static (double X, double V) Spring(double x, double v, double target, double omega, double dt)
    {
        var d = x - target;
        var e = Math.Exp(-omega * dt);
        var c = v + omega * d;
        var nd = (d + c * dt) * e;
        var nv = (v - omega * c * dt) * e;
        return (target + nd, nv);
    }

    /// <summary>The standard ease out (cubic).</summary>
    public static double EaseOut(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var q = 1 - p;
        return 1 - q * q * q;
    }

    /// <summary>Smoothstep (ease in and out) on 0 to 1.</summary>
    public static double Smoothstep(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return p * p * (3 - 2 * p);
    }

    internal static double Frac(double x) => x - Math.Floor(x);
}
