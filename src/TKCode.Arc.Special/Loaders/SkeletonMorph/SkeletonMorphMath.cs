using Avalonia;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The measured rules of <see cref="SkeletonMorph"/> and <see cref="MorphBlock"/> (Arc Skeleton morph, design reference):
/// the skeleton pulse, the skeleton box of a block, the text bars inside it and the resolve timings. Pure functions, so
/// the tests check them against the observed numbers without a window.
/// </summary>
internal static class SkeletonMorphMath
{
    /// <summary>One half of the pulse (1 → 0.55), 1.4 s; the pulse alternates, so a full breath is 2.8 s.</summary>
    public const double PulseHalfPeriod = 1.4;

    /// <summary>Content fades in and sharpens from a 4 px blur over 330 ms (Standard curve, fitted).</summary>
    public static readonly TimeSpan RevealDuration = TimeSpan.FromMilliseconds(330);

    /// <summary>The placeholder fades out over 380 ms (Standard curve, fitted) ...</summary>
    public static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(380);

    /// <summary>... starting 50 ms after its content mounts.</summary>
    public static readonly TimeSpan FadeDelay = TimeSpan.FromMilliseconds(50);

    /// <summary>Reduced motion: placeholder and content crossfade in 120 ms, together.</summary>
    public static readonly TimeSpan ReducedCrossfade = TimeSpan.FromMilliseconds(120);

    /// <summary>The reduced placeholder fade's curve: CSS ease-out (0, 0, .58, 1).</summary>
    public static readonly Avalonia.Animation.Easings.Easing ReducedFadeEasing = new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1);

    /// <summary>
    /// The skeleton opacity <paramref name="seconds"/> after loading started: 1 → <paramref name="minimum"/> over one half
    /// period on the in-out curve (.65, 0, .35, 1), then back, forever (CSS alternate). Every block shares the clock, so
    /// all skeletons breathe in phase.
    /// </summary>
    public static double Pulse(double seconds, double minimum, double halfPeriod = PulseHalfPeriod)
    {
        if (seconds <= 0 || halfPeriod <= 0)
        {
            return 1;
        }
        var phase = seconds / halfPeriod;
        var iteration = Math.Floor(phase);
        var local = phase - iteration;
        // A reversed iteration plays the keyframes backwards (the curve is symmetric, so this is the mirrored value).
        var progress = ((long)iteration & 1) == 0 ? local : 1 - local;
        return 1 + (minimum - 1) * ArcEasings.InOut.Ease(progress);
    }

    /// <summary>
    /// The skeleton box of a block: <paramref name="width"/> (NaN fills <paramref name="availableWidth"/> times
    /// <paramref name="widthRatio"/>; an unbounded width fills nothing) by <paramref name="height"/> (NaN: lines ×
    /// line height for a text skeleton, the width for a circle with a set width, otherwise 12).
    /// </summary>
    public static Size SkeletonSize(double width, double widthRatio, double height, int lines, double lineHeight, bool isCircle,
        double availableWidth)
    {
        double w;
        if (double.IsNaN(width))
        {
            w = double.IsInfinity(availableWidth) || double.IsNaN(availableWidth) ? 0 : availableWidth * Math.Clamp(widthRatio, 0, 1);
        }
        else
        {
            w = Math.Max(0, width);
        }
        double h;
        if (!double.IsNaN(height))
        {
            h = Math.Max(0, height);
        }
        else if (lines > 0)
        {
            h = lines * Math.Max(0, lineHeight);
        }
        else if (isCircle && !double.IsNaN(width))
        {
            h = w;
        }
        else
        {
            h = 12;
        }
        return new Size(w, h);
    }

    /// <summary>
    /// The height of the bar drawn in a text line box: <paramref name="lineHeight"/> − <paramref name="inset"/>, at most
    /// <paramref name="maxHeight"/> (measured: 12 in 18 and 20 px lines, 10 in 16 px lines).
    /// </summary>
    public static double BarHeight(double lineHeight, double maxHeight, double inset)
        => Math.Max(0, Math.Min(maxHeight, lineHeight - inset));

    /// <summary>
    /// Bar <paramref name="index"/> of a text skeleton with <paramref name="lines"/> lines in a placeholder
    /// <paramref name="boxWidth"/> wide: one bar per line box, centred vertically; the last of several is
    /// <paramref name="lastLineRatio"/> of the width.
    /// </summary>
    public static Rect Bar(int index, int lines, double boxWidth, double lineHeight, double barHeight, double lastLineRatio)
    {
        var width = lines > 1 && index == lines - 1 ? boxWidth * lastLineRatio : boxWidth;
        var top = index * lineHeight + (lineHeight - barHeight) / 2;
        return new Rect(0, top, Math.Max(0, width), barHeight);
    }

    /// <summary>The corner radius of a shape skeleton: a circle (or pill) uses half the short side; others clamp to it.</summary>
    public static double ShapeRadius(Size box, double radius, bool isCircle)
    {
        var half = Math.Min(box.Width, box.Height) / 2;
        return isCircle ? half : Math.Clamp(radius, 0, half);
    }

    /// <summary>When block <paramref name="index"/> (reading order) mounts its content: index × stagger at full motion, now otherwise.</summary>
    public static TimeSpan StaggerDelay(int index, TimeSpan stagger, ArcMotionLevel level)
        => level == ArcMotionLevel.Full && stagger > TimeSpan.Zero ? TimeSpan.FromTicks(stagger.Ticks * Math.Max(0, index)) : TimeSpan.Zero;

    /// <summary>The blur on resolving content: 4 px at mount, gone when it is fully opaque (blur = 4 · (1 − opacity)).</summary>
    public static double RevealBlur(double reveal, double maxBlur) => maxBlur * (1 - Math.Clamp(reveal, 0, 1));

    /// <summary>Blur radii below this are not drawn (invisible, and each blur is an offscreen layer).</summary>
    public const double BlurCutoff = 0.3;

    /// <summary>The blur actually drawn for <paramref name="radius"/>: 0.5 px steps, 0 below <see cref="BlurCutoff"/>.</summary>
    public static double BlurStep(double radius)
    {
        if (!(radius >= BlurCutoff))
        {
            return 0;
        }
        return Math.Max(0.5, Math.Round(radius * 2, MidpointRounding.AwayFromZero) / 2);
    }
}
