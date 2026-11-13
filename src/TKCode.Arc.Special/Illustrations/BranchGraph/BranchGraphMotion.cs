using Avalonia.Animation.Easings;

namespace TKCode.Arc;

/// <summary>Where an item is drawn: its lane x, its row's top (0 = first row), the dot's scale and opacity, the row's opacity, and how far it is lifted.</summary>
internal struct BranchGraphPose
{
    public double X;
    public double RowY;
    public double Scale;
    public double Opacity;
    public double RowOpacity;
    public double Lift;

    public static BranchGraphPose Hidden(double x, double rowY) => new() { X = x, RowY = rowY, Scale = 0.35, Opacity = 0, RowOpacity = 0 };

    public static BranchGraphPose Shown(double x, double rowY) => new() { X = x, RowY = rowY, Scale = 1, Opacity = 1, RowOpacity = 1 };

    public readonly bool IsShown => Opacity > 0.01 || RowOpacity > 0.01;
}

/// <summary>How an item moves in a transition.</summary>
internal enum BranchGraphTrackKind
{
    /// <summary>Stays (or is hidden and stays hidden).</summary>
    Static,

    /// <summary>Slides to its new row as the rows reflow.</summary>
    Reflow,

    /// <summary>Lifts into a card, flies along an eased arc to its new lane and row, and sets down with a ring.</summary>
    Fly,

    /// <summary>Flies into the squash point and shrinks away there.</summary>
    SquashIn,

    /// <summary>Appears at the squash point, then flies back out to its row.</summary>
    SquashOut,

    /// <summary>Lands in place (the dot grows from 0.35, the row fades in).</summary>
    Appear,

    /// <summary>Shrinks and fades away in place.</summary>
    Disappear
}

/// <summary>One item's movement in a transition (times in seconds from the transition's start).</summary>
internal struct BranchGraphTrack
{
    public BranchGraphTrackKind Kind;
    public BranchGraphPose From;
    public BranchGraphPose To;

    /// <summary>Reflow: when the slide starts and how long it takes. Fly: when the lift starts. Appear / Disappear: when it starts.</summary>
    public double Start;
    public double Duration;

    /// <summary>When a flyer sets down (or reaches the squash point); the time an appearing item lands.</summary>
    public double Land;

    /// <summary>When the landing ring starts (NaN for none).</summary>
    public double Ring;

    /// <summary>The squash point (x and row) for <see cref="BranchGraphTrackKind.SquashIn"/> and <see cref="BranchGraphTrackKind.SquashOut"/>.</summary>
    public double PointX;
    public double PointRowY;
}

/// <summary>One edge in a transition: its drawn fraction from the parent end, where it starts and where it goes.</summary>
internal struct BranchGraphEdgeTrack
{
    public BranchGraphEdgeSpec Spec;
    public double From;

    /// <summary>True: draws in to 1 from <see cref="Start"/>; false: retracts to 0.</summary>
    public bool Show;
    public double Start;

    /// <summary>The first-load sweep reveals it as the draw front passes (from the parent's y up to the child's).</summary>
    public bool Sweep;
}

/// <summary>
/// The choreography of <see cref="BranchGraph"/> (Arc Branch graph, design reference, measured at speed 1): lifts of
/// 0.13 s, flights of 0.83 s starting 0.115 s after the lift (y on cubic-bezier(0.45, 0, 0.55, 1), x on an in-out cubic,
/// so the commit travels on an arc), set-downs of 0.13 s with a ring (radius 5 → 15 on an exponential ease-out, opacity
/// 0.5 → 0, 0.6 s), commits 0.15 s apart oldest first, row reflow on cubic-bezier(0.4, 0, 0.2, 1), edges drawn in over
/// 0.3 s and retracted over 0.2 s, landings that grow the dot from 0.35 (exponential, 75 ms) while it fades in over 0.3 s.
/// </summary>
internal static class BranchGraphMotion
{
    public const double LiftSeconds = 0.13;
    public const double FlightDelay = 0.115;
    public const double FlightSeconds = 0.83;
    public const double SetDownSeconds = 0.13;
    public const double Stagger = 0.15;
    public const double FirstLift = 0.03;
    public const double LiftScale = 1.38;
    public const double LiftRowScale = 0.008;
    public const double RingSeconds = 0.6;
    public const double RingFrom = 5;
    public const double RingTo = 15;
    public const double RingOpacity = 0.5;
    public const double LandScale = 0.35;
    public const double LandTau = 0.075;
    public const double LandFade = 0.3;
    public const double LandSettle = 0.33;
    public const double DisappearSeconds = 0.25;
    public const double DisappearScale = 0.6;
    public const double RetractSeconds = 0.2;
    public const double RetractDelay = 0.03;
    public const double DrawSeconds = 0.3;
    public const double SquashFade = 0.25;
    public const double SquashScale = 0.55;
    public const double SquashAppear = 0.2;
    public const double HashFlashSeconds = 0.9;
    public const double HashAt = 0.9;
    public const double TagDelay = 0.35;
    public const double TagSeconds = 0.36;
    public const double StageSeconds = 0.5;
    public const double MergeAppear = 0.175;
    public const double AppearRingDelay = 0.22;
    public const double AppearEdgeDelay = 0.29;
    public const double AfterLand = 0.03;
    public const double ReflowStart = 0.04;
    public const double ReflowStartWithFlights = 0.12;

    /// <summary>The first-load draw front's pace (px per second) and when it starts.</summary>
    public const double SweepSpeed = 520;
    public const double SweepStart = 0.1;

    /// <summary>A commit lands this long before the front reaches it.</summary>
    public const double SweepLead = 0.025;

    public static readonly Easing Reflow = new SplineEasing(0.4, 0, 0.2, 1);
    public static readonly Easing FlightY = new SplineEasing(0.45, 0, 0.55, 1);
    public static readonly Easing Draw = new SplineEasing(0.45, 0, 0.55, 1);
    public static readonly Easing StageHeight = new SplineEasing(0.6, 0, 0.4, 1);

    public static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : double.IsNaN(v) ? 1 : v;

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    public static double InOutSine(double t) => -(Math.Cos(Math.PI * t) - 1) / 2;

    public static double OutExpo(double t) => t >= 1 ? 1 : 1 - Math.Pow(2, -10 * t);

    /// <summary>A landing dot's scale <paramref name="seconds"/> after it starts.</summary>
    public static double LandScaleAt(double seconds) =>
        seconds <= 0 ? LandScale : LandScale + (1 - LandScale) * (1 - Math.Exp(-seconds / LandTau));

    /// <summary>Evaluates <paramref name="track"/> at <paramref name="t"/> seconds into its transition.</summary>
    public static BranchGraphPose Evaluate(in BranchGraphTrack track, double t)
    {
        var src = track.From;
        var dst = track.To;
        switch (track.Kind)
        {
            case BranchGraphTrackKind.Static:
                return dst;
            case BranchGraphTrackKind.Reflow:
            {
                var p = Reflow.Ease(Clamp01((t - track.Start) / track.Duration));
                var settle = Clamp01(t / LiftSeconds);
                return new BranchGraphPose
                {
                    X = Lerp(src.X, dst.X, p),
                    RowY = Lerp(src.RowY, dst.RowY, p),
                    Scale = Lerp(src.Scale, dst.Scale, Clamp01(t / LandSettle)),
                    Opacity = Lerp(src.Opacity, dst.Opacity, Clamp01(t / LandFade)),
                    RowOpacity = Lerp(src.RowOpacity, dst.RowOpacity, Clamp01(t / LandFade)),
                    Lift = src.Lift * (1 - InOutSine(settle))
                };
            }
            case BranchGraphTrackKind.Appear:
            {
                var s = t - track.Start;
                if (s <= 0)
                {
                    return src with { X = dst.X, RowY = dst.RowY, Lift = 0 };
                }
                var fade = Clamp01(s / LandFade);
                return new BranchGraphPose
                {
                    X = dst.X,
                    RowY = dst.RowY,
                    Scale = Math.Max(src.Opacity > 0.01 ? src.Scale : 0, LandScaleAt(s)),
                    Opacity = Math.Max(src.Opacity, fade),
                    RowOpacity = Math.Max(src.RowOpacity, fade),
                    Lift = 0
                };
            }
            case BranchGraphTrackKind.Disappear:
            {
                var p = Clamp01((t - track.Start) / DisappearSeconds);
                return new BranchGraphPose
                {
                    X = src.X,
                    RowY = src.RowY,
                    Scale = src.Scale * (1 - (1 - DisappearScale) * p),
                    Opacity = src.Opacity * (1 - p),
                    RowOpacity = src.RowOpacity * (1 - p),
                    Lift = src.Lift * (1 - p)
                };
            }
            default:
                return EvaluateFlight(track, t);
        }
    }

    private static BranchGraphPose EvaluateFlight(in BranchGraphTrack track, double t)
    {
        var src = track.From;
        var dst = track.To;
        var lift0 = track.Start;
        var squashOut = track.Kind == BranchGraphTrackKind.SquashOut;
        var squashIn = track.Kind == BranchGraphTrackKind.SquashIn;
        var flight0 = track.Land - FlightSeconds;
        var startX = squashOut ? track.PointX : src.X;
        var startY = squashOut ? track.PointRowY : src.RowY;
        var endX = squashIn ? track.PointX : dst.X;
        var endY = squashIn ? track.PointRowY : dst.RowY;
        double lift;
        double scale;
        double opacity;
        double rowOpacity;
        if (squashOut)
        {
            // Appears at the point lifted, then flies out.
            var a = Clamp01((t - lift0) / SquashAppear);
            lift = t < lift0 ? 0 : 1;
            scale = SquashScale + (LiftScale - SquashScale) * a;
            opacity = a;
            rowOpacity = a;
        }
        else
        {
            var l = InOutSine(Clamp01((t - lift0) / LiftSeconds));
            lift = src.Lift + (1 - src.Lift) * l;
            scale = Lerp(src.Scale, LiftScale, l);
            opacity = Lerp(src.Opacity, 1, l);
            rowOpacity = Lerp(src.RowOpacity, 1, l);
        }
        var p = Clamp01((t - flight0) / FlightSeconds);
        var x = Lerp(startX, endX, InOutCubic(p));
        var y = Lerp(startY, endY, FlightY.Ease(p));
        if (squashIn)
        {
            var f = Clamp01((t - (track.Land - SquashFade)) / SquashFade);
            if (f > 0)
            {
                scale = LiftScale - (LiftScale - SquashScale) * f;
                opacity *= 1 - f;
                rowOpacity *= 1 - f;
            }
        }
        else if (t >= track.Land)
        {
            var d = InOutSine(Clamp01((t - track.Land) / SetDownSeconds));
            lift = 1 - d;
            scale = LiftScale - (LiftScale - 1) * d;
            opacity = 1;
            rowOpacity = 1;
        }
        return new BranchGraphPose { X = x, RowY = y, Scale = scale, Opacity = opacity, RowOpacity = rowOpacity, Lift = lift };
    }

    /// <summary>The end of <paramref name="track"/> (when it stops moving).</summary>
    public static double EndOf(in BranchGraphTrack track) => track.Kind switch
    {
        BranchGraphTrackKind.Static => 0,
        BranchGraphTrackKind.Reflow => Math.Max(track.Start + track.Duration, LandSettle),
        BranchGraphTrackKind.Appear => track.Start + LandSettle,
        BranchGraphTrackKind.Disappear => track.Start + DisappearSeconds,
        BranchGraphTrackKind.SquashIn => track.Land,
        _ => track.Land + SetDownSeconds
    };

    /// <summary>An edge's drawn fraction at <paramref name="t"/>.</summary>
    public static double EvaluateEdge(in BranchGraphEdgeTrack edge, double t)
    {
        if (edge.Show)
        {
            if (edge.From >= 1)
            {
                return 1;
            }
            return edge.From + (1 - edge.From) * Draw.Ease(Clamp01((t - edge.Start) / DrawSeconds));
        }
        return edge.From * (1 - InOutSine(Clamp01((t - edge.Start) / RetractSeconds)));
    }

    public static double EdgeEnd(in BranchGraphEdgeTrack edge) => edge.Show
        ? edge.From >= 1 ? 0 : edge.Start + DrawSeconds
        : edge.Start + RetractSeconds;

    /// <summary>The ring's radius and opacity <paramref name="seconds"/> after it starts (opacity 0 when not running).</summary>
    public static (double Radius, double Opacity) RingAt(double seconds)
    {
        if (seconds < 0 || seconds >= RingSeconds || double.IsNaN(seconds))
        {
            return (RingFrom, 0);
        }
        var p = seconds / RingSeconds;
        return (RingFrom + (RingTo - RingFrom) * OutExpo(p), RingOpacity * (1 - p));
    }
}
