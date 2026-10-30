using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The one 0 → 1 timeline of a <see cref="SketchToImage"/> pass and where each stage sits on it: sketching takes 38%,
/// blocking 24%, refining 26% and the final crossfade to the photo 12%. A controlled stage plays to its end and holds
/// there; done plays the crossfade too. Pure functions, no state.
/// </summary>
internal static class SketchToImageTimeline
{
    public const double SketchEnd = 0.38;
    public const double BlockEnd = 0.62;
    public const double RefineEnd = 0.88;

    /// <summary>How long the canvas takes to fade back to blank paper (the loop's seam, Replay, a stage moving back).</summary>
    public static readonly TimeSpan FadeToPaper = TimeSpan.FromMilliseconds(520);

    /// <summary>How long the uncontrolled loop holds the finished photo before it fades back to paper.</summary>
    public static readonly TimeSpan LoopHold = TimeSpan.FromSeconds(2);

    /// <summary>Reduced motion: each half of the short fade between end states.</summary>
    public static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(160);

    /// <summary>Where a stage ends: the point a controlled stage plays to and holds at.</summary>
    public static double EndOf(SketchToImageStatus status) => status switch
    {
        SketchToImageStatus.Idle => 0,
        SketchToImageStatus.Sketching => SketchEnd,
        SketchToImageStatus.Blocking => BlockEnd,
        SketchToImageStatus.Refining => RefineEnd,
        _ => 1,
    };

    /// <summary>
    /// The stage on screen at <paramref name="t"/>. Each stage owns the end of its span, so a stage that holds at its end
    /// still reads as that stage. At 0 the canvas is blank: idle, unless a pass is heading somewhere (then sketching).
    /// </summary>
    public static SketchToImageStatus StageAt(double t, bool heading)
    {
        if (t <= 0)
        {
            return heading ? SketchToImageStatus.Sketching : SketchToImageStatus.Idle;
        }
        if (t <= SketchEnd + 1e-9)
        {
            return SketchToImageStatus.Sketching;
        }
        if (t <= BlockEnd + 1e-9)
        {
            return SketchToImageStatus.Blocking;
        }
        return t < 1 - 1e-9 ? SketchToImageStatus.Refining : SketchToImageStatus.Done;
    }

    /// <summary>The fraction of the span from <paramref name="start"/> to <paramref name="end"/> that <paramref name="t"/> has covered, clamped.</summary>
    public static double Local(double t, double start, double end) =>
        end <= start ? (t >= end ? 1 : 0) : Math.Clamp((t - start) / (end - start), 0, 1);

    /// <summary>Arc's in-out curve (0.65, 0, 0.35, 1), which each pencil stroke and brush sweep follows.</summary>
    public static double InOut(double u) => ArcEasings.InOut.Ease(Math.Clamp(u, 0, 1));

    /// <summary>The opacities of the refining layers at <paramref name="t"/>.</summary>
    public static SketchToImageLayers LayersAt(double t)
    {
        var block = Local(t, SketchEnd, BlockEnd);
        var refine = Local(t, BlockEnd, RefineEnd);
        var final = Local(t, RefineEnd, 1);
        // The pencil dims to a guide in the first sixth of blocking, then fades as the soft passes arrive.
        var lines = 1 - 0.62 * Smooth(Local(block, 0, 0.16));
        lines *= 1 - Smooth(Local(refine, 0.1, 0.6));
        return new SketchToImageLayers(
            Lines: lines,
            SoftPass: Smooth(Local(refine, 0, 0.45)),
            DetailPass: Smooth(Local(refine, 0.3, 0.8)),
            Grain: 0.55 * Smooth(Local(refine, 0.35, 0.75)),
            Light: Smooth(Local(refine, 0.45, 1)),
            Photo: Smooth(final));
    }

    /// <summary>A gentle S curve for fades (smoothstep).</summary>
    public static double Smooth(double u)
    {
        u = Math.Clamp(u, 0, 1);
        return u * u * (3 - 2 * u);
    }
}

/// <summary>The opacities of the canvas layers at one point of the timeline.</summary>
internal readonly record struct SketchToImageLayers(double Lines, double SoftPass, double DetailPass, double Grain, double Light, double Photo);
