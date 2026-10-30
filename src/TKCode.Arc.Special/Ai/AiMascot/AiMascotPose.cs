using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// How the drawing is framed: the regular frame (56px and up) shows the whole 100 × 100 design box with the ground; the
/// compact frame crops tighter around the body and enlarges the eyes, dot and sparkle so they read at 24px.
/// </summary>
internal readonly record struct AiMascotFrame(Rect Crop, double EyeScale, double DotScale, double SparkleScale, double StrokeScale,
    double PenY, double OrbitY, bool Compact)
{
    public static readonly AiMascotFrame Regular = new(new Rect(0, 0, 100, 100), 1, 1, 1, 1, 90.5, 27, false);

    public static readonly AiMascotFrame Small = new(new Rect(10, 12, 80, 80), 1.24, 1.5, 1.4, 1.3, 87.5, 25, true);
}

/// <summary>
/// One frame of the mascot in design units (a 100 × 100 box; the body's anchor is its bottom center at 50, 82). Each
/// state is a function of the loop's time (sums of slow sines, an orbit angle, a writing cycle whose start and end meet
/// at zero length), so no loop has a seam; the drawn frame is the weighted sum of the states' frames.
/// </summary>
internal struct AiMascotPose
{
    public const double AnchorX = 50;
    public const double AnchorY = 82;
    public const double EyeY = 55;
    public const double EyeSpread = 10;
    public const double EyeWidth = 6.4;
    public const double EyeHeight = 12.4;
    public const double GazeRangeX = 4.2;
    public const double GazeRangeY = 3.4;
    public const double DotRadius = 3.6;
    public const double LineLeft = 32;
    public const double LineRight = 68;
    public const double LineY = 92;
    public const double WriteCycle = 2.5;
    public const double WriteShare = 0.72;
    public const double OrbitPeriod = 2.6;
    public const double OrbitRadiusX = 32;
    public const double OrbitRadiusY = 7;
    public const double SparkleX = 77;
    public const double SparkleY = 27;

    // Body: offset, scale about the anchor and tilt in degrees.
    public double BodyX, BodyY, ScaleX, ScaleY, Tilt;

    // Gaze target (−1..1 each way) and how stiff the eye spring is (rad/s).
    public double GazeX, GazeY, GazeStiffness;

    // Eyes: height and width factors, the happy squint (0..1) and the worried brows (0..1).
    public double EyeOpen, EyeWide, Squint, Brows;

    // The dot: center, radius, opacity, how much of it is behind the body and how much it fades into the eye color.
    public double DotX, DotY, DotR, DotOpacity, DotBack, DotInk;

    // The done sparkle: scale (0..1) and turn in degrees.
    public double Sparkle, SparkleTurn;

    // The written line under the mascot (x from and to) and its opacity.
    public double LineFrom, LineTo, LineOpacity;

    /// <summary>The pose of <paramref name="status"/> at loop time <paramref name="t"/>.</summary>
    /// <param name="status">The state.</param>
    /// <param name="t">The loop clock in seconds.</param>
    /// <param name="since">Seconds since the state was entered (the error shake).</param>
    /// <param name="live">False for the still, representative pose (first paint, reduced motion).</param>
    /// <param name="glanceX">The idle glance target.</param>
    /// <param name="glanceY">The idle glance target.</param>
    /// <param name="frame">The framing (pen height, orbit height).</param>
    public static AiMascotPose Of(AiMascotStatus status, double t, double since, bool live, double glanceX, double glanceY, in AiMascotFrame frame)
    {
        var p = new AiMascotPose
        {
            ScaleX = 1, ScaleY = 1, EyeOpen = 1, EyeWide = 1, DotR = DotRadius, DotOpacity = 1,
            GazeStiffness = 22, LineFrom = LineLeft, LineTo = LineLeft
        };
        var a = live ? 1.0 : 0.0;
        switch (status)
        {
            case AiMascotStatus.Idle:
            {
                var breath = a * Math.Sin(Tau * t / 3.6);
                p.ScaleY = 1 + 0.024 * breath;
                p.ScaleX = 1 - 0.012 * breath;
                p.BodyY = -a * (1.4 * Math.Sin(Tau * t / 5.2) + 0.35 * Math.Sin(Tau * t / 2.9 + 1.1));
                p.GazeX = a * glanceX;
                p.GazeY = a * glanceY;
                p.GazeStiffness = 34;
                p.DotX = 78 + a * 2.2 * Math.Sin(Tau * t / 4.4);
                p.DotY = 30 + a * (2.4 * Math.Sin(Tau * t / 3.1 + 0.6)) + p.BodyY * 0.5;
                break;
            }
            case AiMascotStatus.Listening:
            {
                var breath = a * Math.Sin(Tau * t / 2.8);
                p.ScaleX = 1.045 - 0.008 * breath;
                p.ScaleY = 1.035 + 0.016 * breath;
                p.BodyY = 0.6;
                p.Tilt = 2.5;
                p.EyeOpen = 1.2;
                p.EyeWide = 1.1;
                p.GazeY = 0.12;
                p.GazeStiffness = 24;
                // Beside the head, clear of the body even at the loudest pulse.
                p.DotX = frame.Compact ? 83 : 86;
                p.DotY = frame.Compact ? 38 : 44;
                p.DotR = DotRadius * (live ? 0.75 + 0.6 * VoiceLevel(t) : 1.2);
                break;
            }
            case AiMascotStatus.Thinking:
            {
                var angle = -Math.PI / 2 + a * Tau * t / OrbitPeriod;
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);
                p.Tilt = -6 + a * 1.4 * Math.Sin(Tau * t / 3.3);
                p.BodyY = -a * 0.6 * Math.Sin(Tau * t / 4.1);
                p.DotX = AnchorX + OrbitRadiusX * cos;
                p.DotY = frame.OrbitY + OrbitRadiusY * sin;
                // The dot passes behind the head on the upper half of the orbit and switches layers only at its ends,
                // where it is clear of the body; across the crown in front it fades into the eye color.
                p.DotBack = sin < 0 ? 1 : 0;
                p.DotInk = sin < 0 ? 0 : 1 - SmoothStep(14, 27, Math.Abs(p.DotX - AnchorX));
                p.GazeX = 0.6 * cos;
                p.GazeY = -0.95;
                p.GazeStiffness = 8;
                break;
            }
            case AiMascotStatus.Writing:
            {
                var (from, to, penX, penY) = Pen(live ? t : WriteCycle * WriteShare * 0.5, frame.PenY);
                if (!live)
                {
                    // Half a written line with the pen resting at its end.
                    from = LineLeft;
                    to = (LineLeft + LineRight) / 2;
                    penX = to;
                    penY = frame.PenY;
                }
                p.LineFrom = from;
                p.LineTo = to;
                p.LineOpacity = 1;
                p.DotX = penX;
                p.DotY = penY;
                p.DotR = DotRadius * 0.85;
                p.BodyY = 1.2;
                p.Tilt = (penX - AnchorX) * 0.07;
                p.GazeX = Math.Clamp((penX - AnchorX) / 18, -1, 1);
                p.GazeY = 0.85;
                p.GazeStiffness = 46;
                p.EyeOpen = 0.92;
                break;
            }
            case AiMascotStatus.Done:
            {
                var breath = a * Math.Sin(Tau * t / 3.6);
                p.ScaleY = 1 + 0.02 * breath;
                p.ScaleX = 1 - 0.01 * breath;
                p.BodyY = -1.4 - a * 0.8 * Math.Sin(Tau * t / 4.8);
                p.Squint = 1;
                p.GazeY = -0.1;
                p.DotX = SparkleX;
                p.DotY = SparkleY;
                p.DotR = 0;
                p.DotOpacity = 0;
                p.Sparkle = 1 + a * 0.09 * Math.Sin(Tau * t / 1.6);
                p.SparkleTurn = a * 9 * Math.Sin(Tau * t / 3.2);
                break;
            }
            case AiMascotStatus.Error:
            {
                var shake = a * Shake(since);
                p.BodyX = shake;
                p.Tilt = shake * 0.9;
                p.ScaleY = 0.98;
                p.ScaleX = 1.01;
                p.Brows = 1;
                p.EyeOpen = 0.84;
                p.GazeX = -0.1;
                p.GazeY = 0.25;
                p.DotX = frame.Compact ? 82 : 83;
                p.DotY = 76;
                p.DotR = DotRadius * 0.85;
                p.DotOpacity = 0.6;
                break;
            }
        }
        return p;
    }

    /// <summary>The single shake of an error: 4.5 units at 5.5Hz, decaying to rest in about 0.7s.</summary>
    public static double Shake(double since)
    {
        if (since < 0 || since > 1.2)
        {
            return 0;
        }
        return 4.5 * Math.Exp(-since / 0.18) * Math.Sin(Tau * 5.5 * since);
    }

    /// <summary>A voice-like level (0..1) made of two incommensurate sines and a slow swell.</summary>
    public static double VoiceLevel(double t)
    {
        var v = 0.5 + 0.3 * Math.Sin(Tau * 1.7 * t) + 0.15 * Math.Sin(Tau * 2.9 * t + 1) + 0.12 * Math.Sin(Tau * 0.45 * t + 2);
        return Math.Clamp(v, 0, 1);
    }

    /// <summary>
    /// The writing cycle at time <paramref name="t"/>: for 72% of 2.5s the pen moves right along the line with small
    /// handwriting bumps and the line grows behind it; then the pen lifts and returns while the line clears from the
    /// left. Both ends of the cycle have a zero-length line, so the loop has no seam.
    /// </summary>
    public static (double From, double To, double PenX, double PenY) Pen(double t, double penY)
    {
        var phase = t / WriteCycle - Math.Floor(t / WriteCycle);
        var span = LineRight - LineLeft;
        if (phase < WriteShare)
        {
            var q = phase / WriteShare;
            var x = LineLeft + span * q;
            var bump = 0.9 * Math.Sin(Tau * q * 6) * Math.Sin(Math.PI * q);
            return (LineLeft, x, x, penY - Math.Abs(bump));
        }
        var r = (phase - WriteShare) / (1 - WriteShare);
        var s = SmoothStep(0, 1, r);
        return (LineLeft + span * s, LineRight, LineRight - span * s, penY - 6 * Math.Sin(Math.PI * r));
    }

    /// <summary>Adds <paramref name="pose"/> weighted by <paramref name="w"/>.</summary>
    public void Add(in AiMascotPose pose, double w)
    {
        BodyX += pose.BodyX * w;
        BodyY += pose.BodyY * w;
        ScaleX += pose.ScaleX * w;
        ScaleY += pose.ScaleY * w;
        Tilt += pose.Tilt * w;
        GazeX += pose.GazeX * w;
        GazeY += pose.GazeY * w;
        GazeStiffness += pose.GazeStiffness * w;
        EyeOpen += pose.EyeOpen * w;
        EyeWide += pose.EyeWide * w;
        Squint += pose.Squint * w;
        Brows += pose.Brows * w;
        DotX += pose.DotX * w;
        DotY += pose.DotY * w;
        DotR += pose.DotR * w;
        DotOpacity += pose.DotOpacity * w;
        DotBack += pose.DotBack * w;
        DotInk += pose.DotInk * w;
        Sparkle += pose.Sparkle * w;
        SparkleTurn += pose.SparkleTurn * w;
        LineFrom += pose.LineFrom * w;
        LineTo += pose.LineTo * w;
        LineOpacity += pose.LineOpacity * w;
    }

    /// <summary>The pose between <paramref name="a"/> (k = 0) and <paramref name="b"/> (k = 1).</summary>
    public static AiMascotPose Lerp(in AiMascotPose a, in AiMascotPose b, double k)
    {
        if (k <= 0)
        {
            return a;
        }
        if (k >= 1)
        {
            return b;
        }
        var p = new AiMascotPose();
        p.Add(a, 1 - k);
        p.Add(b, k);
        return p;
    }

    public const double Tau = Math.PI * 2;

    public static double SmoothStep(double e0, double e1, double x)
    {
        var k = Math.Clamp((x - e0) / (e1 - e0), 0, 1);
        return k * k * (3 - 2 * k);
    }
}
