namespace TKCode.Arc;

/// <summary>
/// The pure math of <see cref="CubeMascot"/>: each status is a function of time written into a small pose vector (lift,
/// roll, nod, sway, gaze, eye shape, brows, mouth, sparkle and the printed lines), plus the timing of the thinking quarter
/// turns, the writing lines, the blinks and the one-shot gestures (the done dip and the error shake). Nothing here
/// allocates.
/// </summary>
internal static class CubeMascotPose
{
    /// <summary>The number of values in a pose.</summary>
    public const int Count = 12;

    /// <summary>Vertical lift of the cube in cube units (up is positive).</summary>
    public const int Lift = 0;

    /// <summary>Roll (lean) in degrees, about the view axis.</summary>
    public const int Roll = 1;

    /// <summary>Pitch (nod) in degrees; positive tips the face down.</summary>
    public const int Pitch = 2;

    /// <summary>Sway: a yaw offset in degrees added to the quarter-turn yaw.</summary>
    public const int Sway = 3;

    /// <summary>Gaze across the face in face units (right is positive).</summary>
    public const int GazeX = 4;

    /// <summary>Gaze up and down the face in face units (down is positive).</summary>
    public const int GazeY = 5;

    /// <summary>How open the eyes are (1 = open pills, 0 = a closed line).</summary>
    public const int EyeOpen = 6;

    /// <summary>How far the eyes are bent into the happy squint (0 = none, 1 = full).</summary>
    public const int Squint = 7;

    /// <summary>The worried brows (0 = hidden, 1 = shown).</summary>
    public const int Brow = 8;

    /// <summary>The mouth's curve: positive smiles, negative frowns.</summary>
    public const int Mouth = 9;

    /// <summary>The sparkle's size (0 = hidden, 1 = full).</summary>
    public const int Sparkle = 10;

    /// <summary>The printed lines' visibility (0 = hidden, 1 = shown).</summary>
    public const int Ink = 11;

    /// <summary>The fixed camera pitch (degrees) that shows the top face.</summary>
    public const double CameraPitch = 32;

    /// <summary>The yaw at rest (degrees): the face side turned slightly left, the shaded side on the right.</summary>
    public const double HomeYaw = -30;

    /// <summary>Thinking holds 0.5s, then turns a quarter turn over 0.8s on a cubic ease in and out.</summary>
    public const double TurnHold = 0.5;

    public const double TurnDuration = 0.8;

    /// <summary>Writing prints a line every 360ms; each line travels for 1.44s (four lines in flight, a nod per four).</summary>
    public const double LineInterval = 0.36;

    public const double LineLife = 1.44;

    /// <summary>The status blend (420ms, 240ms into error), on an ease in and out.</summary>
    public const double Blend = 0.42;

    public const double ErrorBlend = 0.24;

    /// <summary>The error shake: 3.5 swings in 560ms under a squared envelope that ends at rest with zero velocity.</summary>
    public const double ShakeDuration = 0.56;

    public const double ShakeSwings = 3.5;

    /// <summary>The done dip: one dip and settle over 600ms.</summary>
    public const double DipDuration = 0.6;

    /// <summary>A blink takes 170ms; blinks come every 2.6 to 4.9s, sometimes twice.</summary>
    public const double BlinkDuration = 0.17;

    public const double BlinkMin = 2.6;

    public const double BlinkMax = 4.9;

    /// <summary>The sparkle grows and turns in over 550ms when done.</summary>
    public const double SparkleGrow = 0.55;

    /// <summary>Writes the rest pose: eyes open, a gentle smile, nothing else.</summary>
    public static void Rest(Span<double> pose)
    {
        pose.Clear();
        pose[EyeOpen] = 1;
        pose[Mouth] = 0.35;
    }

    /// <summary>
    /// Writes the target pose of <paramref name="status"/>. <paramref name="t"/> is the free-running idle clock (s),
    /// <paramref name="since"/> the time spent in the status (s) and <paramref name="turn"/> the progress of the current
    /// quarter turn (0 to 1, 0 between turns). <paramref name="still"/> gives the status's still pose (reduced motion).
    /// </summary>
    public static void Write(CubeMascotStatus status, bool still, double t, double since, double turn, Span<double> pose)
    {
        Rest(pose);
        switch (status)
        {
            case CubeMascotStatus.Idle:
                if (!still)
                {
                    WriteIdle(t, pose);
                }
                break;
            case CubeMascotStatus.Thinking:
            {
                var lean = still ? 0 : Math.Sin(Math.PI * Math.Clamp(turn, 0, 1));
                pose[Lift] = 0.02 + 0.045 * lean;
                pose[Roll] = -6 * lean;
                pose[GazeX] = 0.04;
                pose[GazeY] = -0.1;
                pose[Mouth] = 0;
                break;
            }
            case CubeMascotStatus.Writing:
            {
                var u = still ? 0 : Frac(since / LineLife);
                var sweep = still ? 0 : Math.Sin(2 * Math.PI * since / (2 * LineLife));
                // One nod per four lines: a smooth bump that starts and ends at rest.
                pose[Pitch] = 9 + 4 * (0.5 - 0.5 * Math.Cos(2 * Math.PI * u));
                pose[Roll] = 1.5 * sweep;
                pose[GazeX] = 0.07 * sweep;
                pose[GazeY] = 0.1;
                pose[EyeOpen] = 0.85;
                pose[Mouth] = 0.12;
                pose[Ink] = 1;
                break;
            }
            case CubeMascotStatus.Done:
                pose[EyeOpen] = 0;
                pose[Squint] = 1;
                pose[Mouth] = 0.8;
                pose[Sparkle] = still ? 1 : EaseOutCubic(since / SparkleGrow);
                pose[Lift] = still ? 0 : 0.015 * Math.Sin(2 * Math.PI * since / 3.4);
                break;
            case CubeMascotStatus.Error:
                pose[EyeOpen] = 0.8;
                pose[GazeY] = 0.03;
                pose[Brow] = 1;
                pose[Mouth] = -0.6;
                pose[Lift] = -0.02;
                break;
        }
    }

    // Slow sines with incommensurate periods, so the idle never repeats or restarts.
    private static void WriteIdle(double t, Span<double> pose)
    {
        pose[Lift] = 0.03 * Wave(t, 3.7, 0) + 0.012 * Wave(t, 2.3, 1);
        pose[Roll] = 2.2 * Wave(t, 7.3, 0) + 0.8 * Wave(t, 3.1, 0.5);
        pose[Pitch] = 1.5 * Wave(t, 5.9, 2);
        pose[Sway] = 6 * Wave(t, 9.7, 0) + 2 * Wave(t, 4.3, 1.3);
        pose[GazeX] = 0.06 * Wave(t, 6.1, 0.7) + 0.02 * Wave(t, 2.9, 0);
        pose[GazeY] = 0.025 * Wave(t, 5.3, 2.1);
    }

    private static double Wave(double t, double period, double phase) => Math.Sin(2 * Math.PI * t / period + phase);

    /// <summary>The error shake's sideways offset in cube units at <paramref name="tau"/> seconds; 0 outside.</summary>
    public static double Shake(double tau)
    {
        if (tau <= 0 || tau >= ShakeDuration)
        {
            return 0;
        }
        var p = tau / ShakeDuration;
        var envelope = (1 - p) * (1 - p);
        return 0.09 * Math.Sin(2 * Math.PI * ShakeSwings * p) * envelope;
    }

    /// <summary>The done dip in cube units at <paramref name="tau"/> seconds (negative is down); 0 outside.</summary>
    public static double Dip(double tau)
    {
        if (tau <= 0 || tau >= DipDuration)
        {
            return 0;
        }
        var p = tau / DipDuration;
        // Down fast, back up slowly, with zero velocity at both ends and no overshoot.
        var s = Math.Sin(Math.PI * Math.Pow(p, 0.7));
        return -0.07 * s * s;
    }

    /// <summary>How closed a blink is at <paramref name="tau"/> seconds into it (0 open, 1 closed); 0 outside.</summary>
    public static double Blink(double tau)
    {
        if (tau <= 0 || tau >= BlinkDuration)
        {
            return 0;
        }
        return Math.Sin(Math.PI * tau / BlinkDuration);
    }

    /// <summary>The yaw progress of a quarter turn at <paramref name="p"/> (cubic ease in and out).</summary>
    public static double EaseInOutCubic(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return p < 0.5 ? 4 * p * p * p : 1 - Math.Pow(-2 * p + 2, 3) / 2;
    }

    /// <summary>The ease out used for the sparkle's growth.</summary>
    public static double EaseOutCubic(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var q = 1 - p;
        return 1 - q * q * q;
    }

    /// <summary>
    /// A printed line at <paramref name="age"/> seconds of its 1.44s life: how far it has slid out (0 to 1) and its
    /// opacity (fades in over the first fifth, out over the last third, invisible at both ends).
    /// </summary>
    public static (double Travel, double Opacity) Line(double age)
    {
        if (age <= 0 || age >= LineLife)
        {
            return (Math.Clamp(age / LineLife, 0, 1), 0);
        }
        var u = age / LineLife;
        var fadeIn = SmoothStep(0, 0.2, u);
        var fadeOut = 1 - SmoothStep(0.66, 1, u);
        return (u, fadeIn * fadeOut);
    }

    /// <summary>The length of printed line <paramref name="index"/> in cube units (0.3 to 0.72), fixed per line.</summary>
    public static double LineLength(long index)
    {
        var h = unchecked((ulong)index * 0x9E3779B97F4A7C15UL);
        h ^= h >> 29;
        h = unchecked(h * 0xBF58476D1CE4E5B9UL);
        h ^= h >> 32;
        var r = (h & 0xFFFF) / 65535.0;
        // Every fourth line is short, like the end of a paragraph.
        return index % 4 == 3 ? 0.3 + 0.12 * r : 0.5 + 0.22 * r;
    }

    /// <summary>
    /// The face's visibility on a side at <paramref name="angle"/> degrees from the front (0 at the front, positive past
    /// it toward the shaded side, negative on the side that arrives): the leaving face fades as it turns edge on, the
    /// arriving one fades in as it reaches the front.
    /// </summary>
    public static double FaceVisibility(double angle)
    {
        angle = WrapSigned(angle);
        return angle >= 0 ? 1 - SmoothStep(10, 48, angle) : SmoothStep(-40, -10, angle);
    }

    /// <summary>How open the eyes of a face arriving at <paramref name="angle"/> degrees are (they open as it reaches the front).</summary>
    public static double FaceEyes(double angle)
    {
        angle = WrapSigned(angle);
        return angle >= 0 ? 1 : SmoothStep(-34, -12, angle);
    }

    /// <summary>Wraps an angle into (-180, 180].</summary>
    public static double WrapSigned(double angle)
    {
        angle %= 360;
        if (angle > 180)
        {
            angle -= 360;
        }
        else if (angle <= -180)
        {
            angle += 360;
        }
        return angle;
    }

    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var p = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return p * p * (3 - 2 * p);
    }

    public static double Frac(double x) => x - Math.Floor(x);
}
