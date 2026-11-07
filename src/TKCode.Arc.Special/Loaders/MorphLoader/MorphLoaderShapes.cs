using Avalonia;
using Avalonia.Animation.Easings;

namespace TKCode.Arc;

/// <summary>
/// One of the four strokes of a <see cref="MorphLoader"/>, on the 24 unit grid: the chord's midpoint
/// (<see cref="Cx"/>, <see cref="Cy"/>), the chord length, the direction from the start to the end point in degrees
/// (y down, clockwise positive), the bend (curvature 1/R, 0 = straight; a bent stroke bulges to the left of its
/// direction), the line width and the opacity. Any shape morphs into any other by moving these seven numbers.
/// </summary>
internal struct MorphLoaderStroke
{
    public const int ParameterCount = 7;
    public const int CxIndex = 0;
    public const int CyIndex = 1;
    public const int LengthIndex = 2;
    public const int AngleIndex = 3;
    public const int BendIndex = 4;
    public const int WidthIndex = 5;
    public const int OpacityIndex = 6;

    public double Cx;
    public double Cy;
    public double Length;
    public double Angle;
    public double Bend;
    public double Width;
    public double Opacity;

    public MorphLoaderStroke(double cx, double cy, double length, double angle, double bend, double width, double opacity)
    {
        Cx = cx;
        Cy = cy;
        Length = length;
        Angle = angle;
        Bend = bend;
        Width = width;
        Opacity = opacity;
    }

    /// <summary>The straight stroke from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static MorphLoaderStroke Line(Point from, Point to, double width)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return new MorphLoaderStroke((from.X + to.X) / 2, (from.Y + to.Y) / 2, Math.Sqrt(dx * dx + dy * dy),
            Math.Atan2(dy, dx) * 180 / Math.PI, 0, width, 1);
    }

    public double this[int index]
    {
        readonly get => index switch
        {
            CxIndex => Cx,
            CyIndex => Cy,
            LengthIndex => Length,
            AngleIndex => Angle,
            BendIndex => Bend,
            WidthIndex => Width,
            _ => Opacity
        };
        set
        {
            switch (index)
            {
                case CxIndex: Cx = value; break;
                case CyIndex: Cy = value; break;
                case LengthIndex: Length = value; break;
                case AngleIndex: Angle = value; break;
                case BendIndex: Bend = value; break;
                case WidthIndex: Width = value; break;
                default: Opacity = value; break;
            }
        }
    }
}

/// <summary>The six resting shapes a <see cref="MorphLoader"/> draws.</summary>
internal enum MorphLoaderShape
{
    Dots,
    Bars,
    Ring,
    Square,
    Check,
    Cross
}

/// <summary>
/// The pure geometry and timing of <see cref="MorphLoader"/> (Arc morph loader, design reference; measured values from
/// the public preview): the resting shapes on the 24 unit grid, the loading loops, the direction rule for morph targets,
/// the forward turn to upright, the result pop and the sampling of a bent stroke into points.
/// </summary>
internal static class MorphLoaderShapes
{
    public const int StrokeCount = 4;
    public const double Grid = 24;
    public const double Center = 12;

    /// <summary>The drawn length never goes below this (a zero-length stroke is a round dot).</summary>
    public const double MinLength = 0.01;

    // Dots: three dots 7 apart; the wave is 900ms, each dot hops 3.2 up during the first 450ms of its cycle, 116.7ms
    // after the previous one, and grows 0.1875 width per unit of lift.
    public const double DotSpacing = 7;
    public const double DotPeriod = 0.9;
    public const double DotHop = 0.45;
    public const double DotLift = 3.2;
    public const double DotDelay = 0.1167;
    public const double DotGrowth = 0.1875;

    // Bars: x 4.5 / 9.5 / 14.5 / 19.5, 12 long at rest; 950ms sine between 4.5 and 12, each bar 0.7rad behind.
    public const double BarRest = 12;
    public const double BarDip = 3.75;
    public const double BarPeriod = 0.95;
    public const double BarLag = 0.7;

    // Ring: four 64 degree arcs of a radius 8.5 circle (gaps of 26 degrees); 330 degrees/s and a 1400ms breath to 40 degrees.
    public const double RingRadius = 8.5;
    public static readonly double RingChord = 2 * RingRadius * Math.Sin(32 * Math.PI / 180);
    public static readonly double RingInset = RingRadius * Math.Cos(32 * Math.PI / 180);
    public const double RingBreath = 1.6;
    public const double RingPeriod = 1.4;
    public const double RingSpeed = 330;

    // Square: a closed 14 box (5..19); 180ms hold, then a 720ms smoothstep quarter turn while the sides shrink to 6.
    public const double SquareSide = 14;
    public const double SquareHold = 0.18;
    public const double SquareTumble = 0.72;
    public const double SquarePeriod = SquareHold + SquareTumble;
    public const double SquareOpen = 8;

    // The result pop: 1 -> 1.14 -> 1 over 420ms, the peak at 35.2%, ease-out up and ease-in-out down.
    public const double PopDuration = 0.42;
    public const double PopPeak = 0.352;
    public const double PopScale = 1.14;
    private static readonly Easing s_popUp = new SplineEasing(0, 0, 0.58, 1);
    private static readonly Easing s_popDown = new SplineEasing(0.42, 0, 0.58, 1);

    /// <summary>The loop clock wraps after this many seconds: a whole number of every loop period (900, 950, 1400ms).</summary>
    public const double LoopWrap = 239.4;

    private static readonly Point[] s_check =
    [
        new(5, 12.5), new(7.6, 15.1), new(10.2, 17.7), new(14.6, 12.6), new(19, 7.5)
    ];

    /// <summary>The shape a loader rests in for <paramref name="variant"/> and <paramref name="status"/>.</summary>
    public static MorphLoaderShape ShapeFor(MorphLoaderVariant variant, MorphLoaderStatus status) => status switch
    {
        MorphLoaderStatus.Success => MorphLoaderShape.Check,
        MorphLoaderStatus.Error => MorphLoaderShape.Cross,
        _ => variant switch
        {
            MorphLoaderVariant.Bars => MorphLoaderShape.Bars,
            MorphLoaderVariant.Ring => MorphLoaderShape.Ring,
            MorphLoaderVariant.Square => MorphLoaderShape.Square,
            _ => MorphLoaderShape.Dots
        }
    };

    /// <summary>
    /// Writes the resting strokes of <paramref name="shape"/>. Dots and bars use their own widths; the ring, square, check
    /// and cross use <paramref name="strokeWidth"/>. The dots hide the fourth stroke at the center (no width, no opacity).
    /// </summary>
    public static void WriteRest(MorphLoaderShape shape, double strokeWidth, double dotWidth, double barWidth, Span<MorphLoaderStroke> strokes)
    {
        switch (shape)
        {
            case MorphLoaderShape.Dots:
                for (var i = 0; i < 3; i++)
                {
                    strokes[i] = new MorphLoaderStroke(Center + (i - 1) * DotSpacing, Center, 0, 0, 0, dotWidth, 1);
                }
                strokes[3] = new MorphLoaderStroke(Center, Center, 0, 0, 0, 0, 0);
                break;
            case MorphLoaderShape.Bars:
                for (var i = 0; i < 4; i++)
                {
                    strokes[i] = new MorphLoaderStroke(4.5 + i * 5, Center, BarRest, 90, 0, barWidth, 1);
                }
                break;
            case MorphLoaderShape.Ring:
            {
                var bend = 1 / RingRadius;
                var near = Center - RingInset;
                var far = Center + RingInset;
                strokes[0] = new MorphLoaderStroke(Center, near, RingChord, 0, bend, strokeWidth, 1);
                strokes[1] = new MorphLoaderStroke(far, Center, RingChord, 90, bend, strokeWidth, 1);
                strokes[2] = new MorphLoaderStroke(Center, far, RingChord, 180, bend, strokeWidth, 1);
                strokes[3] = new MorphLoaderStroke(near, Center, RingChord, -90, bend, strokeWidth, 1);
                break;
            }
            case MorphLoaderShape.Square:
            {
                var near = Center - SquareSide / 2;
                var far = Center + SquareSide / 2;
                strokes[0] = new MorphLoaderStroke(Center, near, SquareSide, 0, 0, strokeWidth, 1);
                strokes[1] = new MorphLoaderStroke(far, Center, SquareSide, 90, 0, strokeWidth, 1);
                strokes[2] = new MorphLoaderStroke(Center, far, SquareSide, 180, 0, strokeWidth, 1);
                strokes[3] = new MorphLoaderStroke(near, Center, SquareSide, -90, 0, strokeWidth, 1);
                break;
            }
            case MorphLoaderShape.Check:
                // Drawn along the pen's path: the short side's two halves, then the long side from the corner up.
                for (var i = 0; i < 4; i++)
                {
                    strokes[i] = MorphLoaderStroke.Line(s_check[i], s_check[i + 1], strokeWidth);
                }
                break;
            default:
                // Top-left half, bottom-right half, top-right half, bottom-left half.
                strokes[0] = MorphLoaderStroke.Line(new Point(6.5, 6.5), new Point(12, 12), strokeWidth);
                strokes[1] = MorphLoaderStroke.Line(new Point(12, 12), new Point(17.5, 17.5), strokeWidth);
                strokes[2] = MorphLoaderStroke.Line(new Point(17.5, 6.5), new Point(12, 12), strokeWidth);
                strokes[3] = MorphLoaderStroke.Line(new Point(12, 12), new Point(6.5, 17.5), strokeWidth);
                break;
        }
    }

    /// <summary>Positive modulo.</summary>
    public static double Wrap(double value, double period) => value - period * Math.Floor(value / period);

    /// <summary>How far dot <paramref name="index"/> is lifted at loop time <paramref name="t"/> (0 .. 3.2).</summary>
    public static double DotLiftAt(double t, int index)
    {
        var tau = Wrap(t - index * DotDelay, DotPeriod);
        return tau < DotHop ? DotLift * Math.Sin(Math.PI * tau / DotHop) : 0;
    }

    /// <summary>How much bar <paramref name="index"/> is shorter than its rest length at loop time <paramref name="t"/> (0 .. 7.5).</summary>
    public static double BarShortening(double t, int index) =>
        BarDip * (1 - Math.Sin(2 * Math.PI * t / BarPeriod - BarLag * index));

    /// <summary>How much every ring arc's chord is shorter than at rest at loop time <paramref name="t"/> (0 .. 3.2).</summary>
    public static double RingShortening(double t) => RingBreath * (1 - Math.Sin(2 * Math.PI * t / RingPeriod));

    /// <summary>The tumble's eased progress inside the current square cycle (0 during the hold).</summary>
    public static double SquareTurn(double t)
    {
        var u = Math.Clamp((Wrap(t, SquarePeriod) - SquareHold) / SquareTumble, 0, 1);
        return u * u * (3 - 2 * u);
    }

    /// <summary>Quarter turns the square has made by loop time <paramref name="t"/> (whole cycles plus the current tumble).</summary>
    public static double SquareTurns(double t) => Math.Floor(t / SquarePeriod) + SquareTurn(t);

    /// <summary>How much the square's sides are shorter than at rest at loop time <paramref name="t"/> (0 .. 8).</summary>
    public static double SquareShortening(double t) => SquareOpen * Math.Sin(Math.PI * SquareTurn(t));

    /// <summary>The loop's own turning speed in degrees per second at loop time <paramref name="t"/> (ring and square).</summary>
    public static double LoopTurnRate(MorphLoaderVariant variant, double t)
    {
        if (variant == MorphLoaderVariant.Ring)
        {
            return RingSpeed;
        }
        if (variant == MorphLoaderVariant.Square)
        {
            var u = (Wrap(t, SquarePeriod) - SquareHold) / SquareTumble;
            return u is <= 0 or >= 1 ? 0 : 90 * 6 * u * (1 - u) / SquareTumble;
        }
        return 0;
    }

    /// <summary>True when the loop of <paramref name="variant"/> turns the drawing itself (ring, square).</summary>
    public static bool Turns(MorphLoaderVariant variant) => variant is MorphLoaderVariant.Ring or MorphLoaderVariant.Square;

    /// <summary>
    /// Adds <paramref name="weight"/> times the loop offset of <paramref name="variant"/> at loop time <paramref name="t"/>
    /// to <paramref name="strokes"/> (the offsets only shorten, lift or widen; the drawn length is clamped later).
    /// </summary>
    public static void ApplyLoop(MorphLoaderVariant variant, double t, double weight, Span<MorphLoaderStroke> strokes)
    {
        if (weight <= 0)
        {
            return;
        }
        switch (variant)
        {
            case MorphLoaderVariant.Dots:
                for (var i = 0; i < 3; i++)
                {
                    var lift = weight * DotLiftAt(t, i);
                    strokes[i].Cy -= lift;
                    strokes[i].Width += DotGrowth * lift;
                }
                break;
            case MorphLoaderVariant.Bars:
                for (var i = 0; i < 4; i++)
                {
                    strokes[i].Length -= weight * BarShortening(t, i);
                }
                break;
            case MorphLoaderVariant.Ring:
            {
                var shortening = weight * RingShortening(t);
                for (var i = 0; i < 4; i++)
                {
                    strokes[i].Length -= shortening;
                }
                break;
            }
            default:
            {
                var shortening = weight * SquareShortening(t);
                for (var i = 0; i < 4; i++)
                {
                    strokes[i].Length -= shortening;
                }
                break;
            }
        }
    }

    /// <summary>
    /// The direction to aim a stroke at: <paramref name="target"/> plus whole turns, nearest to <paramref name="current"/>.
    /// A straight stroke going to a straight shape has no direction (swapping its ends is the same line), so it takes the
    /// nearest of target and target + 180; a bent stroke cannot flip without turning its bulge inside out, so it takes the
    /// shortest way around the full circle.
    /// </summary>
    public static double AimAngle(double current, double currentBend, double target, double targetBend)
    {
        var period = Math.Abs(currentBend) < 1e-3 && Math.Abs(targetBend) < 1e-3 ? 180 : 360;
        var turns = Math.Round((current - target) / period, MidpointRounding.ToEven);
        return target + turns * period;
    }

    /// <summary>The next whole turn at or ahead of <paramref name="angle"/> (the drawing turns forward to upright, never back).</summary>
    public static double NextUpright(double angle) => Math.Ceiling(angle / 360 - 1e-9) * 360;

    /// <summary>The result pop's scale <paramref name="t"/> seconds after the result arrived.</summary>
    public static double PopAt(double t)
    {
        if (t <= 0 || t >= PopDuration)
        {
            return 1;
        }
        var peak = PopDuration * PopPeak;
        if (t < peak)
        {
            return 1 + (PopScale - 1) * s_popUp.Ease(t / peak);
        }
        return PopScale - (PopScale - 1) * s_popDown.Ease((t - peak) / (PopDuration - peak));
    }

    /// <summary>True when <paramref name="stroke"/> is drawn as an arc rather than a straight line.</summary>
    public static bool IsBent(in MorphLoaderStroke stroke) => stroke.Bend > 1e-4;

    /// <summary>
    /// Writes points along <paramref name="stroke"/> (drawn with <paramref name="length"/>) into <paramref name="points"/>
    /// and returns how many: two for a straight stroke, one per 6 degrees of arc (at most the span's size) for a bent one.
    /// The arc's radius is 1/bend, at least half the chord (then the arc is a half circle).
    /// </summary>
    public static int Sample(in MorphLoaderStroke stroke, double length, Span<Point> points)
    {
        var a = stroke.Angle * Math.PI / 180;
        var dx = Math.Cos(a);
        var dy = Math.Sin(a);
        var half = length / 2;
        if (!IsBent(stroke) || points.Length < 3)
        {
            points[0] = new Point(stroke.Cx - dx * half, stroke.Cy - dy * half);
            points[1] = new Point(stroke.Cx + dx * half, stroke.Cy + dy * half);
            return 2;
        }
        var radius = Math.Max(1 / stroke.Bend, half);
        var beta = Math.Asin(Math.Clamp(half / radius, 0, 1));
        // The bulge points to the left of the direction (y down): (sin a, -cos a); the circle's center lies opposite.
        var nx = dy;
        var ny = -dx;
        var offset = radius * Math.Cos(beta);
        var cx = stroke.Cx - nx * offset;
        var cy = stroke.Cy - ny * offset;
        var psi = Math.Atan2(ny, nx);
        var segments = (int)Math.Clamp(Math.Ceiling(2 * beta / (6 * Math.PI / 180)), 1, points.Length - 1);
        for (var i = 0; i <= segments; i++)
        {
            // From the start point (psi - beta) to the end point (psi + beta): clockwise on screen.
            var angle = psi - beta + 2 * beta * i / segments;
            points[i] = new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
        }
        return segments + 1;
    }
}
