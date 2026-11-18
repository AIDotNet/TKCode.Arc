using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The shape of one confetti piece.</summary>
internal enum ConfettiShape : byte
{
    /// <summary>A bar with round ends, about 10–15 by 4–5 px; tumbles.</summary>
    Ribbon,

    /// <summary>A square with slightly rounded corners, 6–8 px; tumbles.</summary>
    Square,

    /// <summary>A disc, 5–8 px; stays round.</summary>
    Circle,

    /// <summary>A small triangle, 7–9 px; tumbles.</summary>
    Triangle
}

/// <summary>One live confetti piece, in the button's coordinates (px, seconds, radians).</summary>
internal struct ConfettiPiece
{
    public double X;
    public double Y;
    public double Vx;
    public double Vy;
    public double Rotation;
    public double Spin;
    public double Flip;
    public double FlipRate;
    public double FlutterPhase;
    public double FlutterRate;
    public double FlutterSpeed;
    public double Age;
    public double Life;
    public double Width;
    public double Height;
    public ConfettiShape Shape;
    public byte Color;
}

/// <summary>
/// The confetti system of <see cref="CelebrateButton"/>, kept pure so tests can step it. Values are Arc Celebrate button
/// (design reference) measurements: pieces leave from the button's capsule outline, more along the top edge, at 260–600
/// px/s outward (the ends tilted up), fall under 920 px/s² of gravity with an air drag of 2.1 /s on the velocity and a
/// small sideways flutter, grow in over 100 ms, tumble, and fade over the last 350 ms of a 1.1–1.45 s life.
/// </summary>
internal static class CelebrateConfettiMath
{
    /// <summary>Most pieces alive at once; a burst over the cap emits only what fits.</summary>
    public const int MaxLive = 180;

    /// <summary>Pieces per burst by default.</summary>
    public const int DefaultCount = 44;

    public const double Gravity = 920;
    public const double Drag = 2.1;
    public const double MinSpeed = 260;
    public const double MaxSpeed = 600;
    public const double GrowSeconds = 0.1;
    public const double FadeSeconds = 0.35;
    public const double MinLife = 1.1;
    public const double MaxLife = 1.45;

    /// <summary>Kick of the button as the burst leaves: up to 1.07 in 130 ms, back in 330 ms.</summary>
    public const double KickScale = 1.07;
    public const double KickUpSeconds = 0.13;
    public const double KickDownSeconds = 0.33;

    // Where pieces leave: the share of the top edge, of the bottom edge, and of each round end.
    private const double TopShare = 0.42;
    private const double BottomShare = 0.2;

    // The default palette's weights: accent, its lighter tone, its strong tone, the yellow and the ink.
    private static readonly double[] s_paletteWeights = [0.38, 0.2, 0.1, 0.22, 0.1];

    /// <summary>
    /// Emits up to <paramref name="count"/> pieces into <paramref name="pieces"/> (live ones first, <paramref name="live"/>
    /// of them) from the outline of a <paramref name="width"/> by <paramref name="height"/> capsule at the origin.
    /// Returns the new live count. <paramref name="colors"/> is the palette size; with the default palette
    /// (<paramref name="weighted"/>) the colors are drawn by its weights, otherwise evenly.
    /// </summary>
    public static int Emit(ConfettiPiece[] pieces, int live, int count, double width, double height, int colors,
                           bool weighted, Random random)
    {
        var free = Math.Min(pieces.Length, MaxLive) - live;
        var n = Math.Clamp(count, 0, Math.Max(0, free));
        var r = Math.Min(height, width) / 2;
        var straight = Math.Max(0, width - (2 * r));
        for (var i = 0; i < n; i++)
        {
            ref var p = ref pieces[live + i];
            OutlinePoint(random.NextDouble(), random.NextDouble(), width, height, r, straight,
                out var x, out var y, out var nx, out var ny);
            var angle = Math.Atan2(ny, nx);
            if (Math.Abs(nx) > 0.3)
            {
                // The round ends lean upward: rotate toward -90° by up to 30°.
                var tilt = random.NextDouble() * (Math.PI / 6);
                angle += nx > 0 ? -tilt : tilt;
            }
            else
            {
                angle += (random.NextDouble() - 0.5) * (Math.PI / 3);
            }
            var speed = MinSpeed + (random.NextDouble() * (MaxSpeed - MinSpeed));
            p.X = x;
            p.Y = y;
            p.Vx = Math.Cos(angle) * speed;
            p.Vy = Math.Sin(angle) * speed;
            p.Rotation = random.NextDouble() * Math.PI * 2;
            p.Spin = (random.NextDouble() - 0.5) * 16;
            p.Flip = random.NextDouble() * Math.PI * 2;
            p.FlipRate = 8 + (random.NextDouble() * 10);
            p.FlutterPhase = random.NextDouble() * Math.PI * 2;
            p.FlutterRate = 4 + (random.NextDouble() * 5);
            p.FlutterSpeed = 15 + (random.NextDouble() * 25);
            p.Age = 0;
            p.Life = MinLife + (random.NextDouble() * (MaxLife - MinLife));
            var kind = random.NextDouble();
            if (kind < 0.35)
            {
                p.Shape = ConfettiShape.Ribbon;
                p.Width = 10 + (random.NextDouble() * 5);
                p.Height = 4 + random.NextDouble();
            }
            else if (kind < 0.6)
            {
                p.Shape = ConfettiShape.Square;
                p.Width = p.Height = 6 + (random.NextDouble() * 2);
            }
            else if (kind < 0.85)
            {
                p.Shape = ConfettiShape.Circle;
                p.Width = p.Height = 5 + (random.NextDouble() * 3);
            }
            else
            {
                p.Shape = ConfettiShape.Triangle;
                p.Width = p.Height = 7 + (random.NextDouble() * 2);
            }
            p.Color = (byte)PickColor(random.NextDouble(), colors, weighted);
        }
        return live + n;
    }

    /// <summary>A point on the capsule outline and its outward normal; <paramref name="u"/> picks the side, <paramref name="v"/> the place on it.</summary>
    internal static void OutlinePoint(double u, double v, double width, double height, double r, double straight,
                                      out double x, out double y, out double nx, out double ny)
    {
        var cy = height / 2;
        if (u < TopShare && straight > 0)
        {
            x = r + (v * straight);
            y = cy - r;
            nx = 0;
            ny = -1;
            return;
        }
        if (u < TopShare + BottomShare && straight > 0)
        {
            x = r + (v * straight);
            y = cy + r;
            nx = 0;
            ny = 1;
            return;
        }
        // A round end: a half circle from -90° to 90° (right) or 90° to 270° (left).
        var right = straight <= 0 ? v >= 0.5 : u >= TopShare + BottomShare + ((1 - TopShare - BottomShare) / 2);
        var t = straight <= 0 ? (v * 2) % 1 : v;
        var a = (-Math.PI / 2) + (t * Math.PI);
        var cx = right ? r + straight : r;
        nx = Math.Cos(a) * (right ? 1 : -1);
        ny = Math.Sin(a);
        x = cx + (nx * r);
        y = cy + (ny * r);
    }

    private static int PickColor(double u, int colors, bool weighted)
    {
        if (colors <= 1)
        {
            return 0;
        }
        if (!weighted || colors != s_paletteWeights.Length)
        {
            return Math.Min(colors - 1, (int)(u * colors));
        }
        var acc = 0.0;
        for (var i = 0; i < s_paletteWeights.Length; i++)
        {
            acc += s_paletteWeights[i];
            if (u < acc)
            {
                return i;
            }
        }
        return s_paletteWeights.Length - 1;
    }

    /// <summary>
    /// Advances every live piece by <paramref name="dt"/> seconds and drops the ones whose life ran out, keeping the live
    /// ones packed at the front. Returns the new live count.
    /// </summary>
    public static int Step(ConfettiPiece[] pieces, int live, double dt)
    {
        if (dt <= 0)
        {
            return live;
        }
        var drag = Math.Exp(-Drag * dt);
        var write = 0;
        for (var i = 0; i < live; i++)
        {
            ref var p = ref pieces[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                continue;
            }
            p.Vy += Gravity * dt;
            p.Vx *= drag;
            p.Vy *= drag;
            p.FlutterPhase += p.FlutterRate * dt;
            p.X += (p.Vx + (Math.Sin(p.FlutterPhase) * p.FlutterSpeed)) * dt;
            p.Y += p.Vy * dt;
            p.Rotation += p.Spin * dt;
            p.Flip += p.FlipRate * dt;
            if (write != i)
            {
                pieces[write] = p;
            }
            write++;
        }
        return write;
    }

    /// <summary>The piece's scale now: grows from 0 to 1 over the first 100 ms.</summary>
    public static double Grow(in ConfettiPiece p) => Math.Clamp(p.Age / GrowSeconds, 0, 1);

    /// <summary>The piece's opacity now: 1 until the last 350 ms of its life, then down to 0.</summary>
    public static double Alpha(in ConfettiPiece p) => Math.Clamp((p.Life - p.Age) / FadeSeconds, 0, 1);

    /// <summary>The button's kick scale <paramref name="t"/> seconds after the burst: 1 → 1.07 → 1 on the standard curve.</summary>
    public static double Kick(double t)
    {
        if (t <= 0 || t >= KickUpSeconds + KickDownSeconds)
        {
            return 1;
        }
        var ease = ArcEasings.Standard;
        return t < KickUpSeconds
            ? 1 + ((KickScale - 1) * ease.Ease(t / KickUpSeconds))
            : 1 + ((KickScale - 1) * (1 - ease.Ease((t - KickUpSeconds) / KickDownSeconds)));
    }
}
