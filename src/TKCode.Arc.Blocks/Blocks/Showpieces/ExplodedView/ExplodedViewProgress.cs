namespace TKCode.Arc;

/// <summary>
/// The timeline of an <see cref="ExplodedView"/> (Arc Exploded view, design reference): progress 0..1 to tilt, separation,
/// label draw-in and stops. Pure functions, no allocation. Opening tilts first and then separates (tilt leads by 0.04);
/// closing joins first and then stands up (separation leads by 0.04). The layer stops share [0.28, 0.80) evenly.
/// </summary>
internal static class ExplodedViewProgress
{
    public const double TiltOpenStart = 0.04;
    public const double TiltOpenLength = 0.18;
    public const double TiltCloseStart = 0.84;
    public const double TiltCloseLength = 0.14;
    public const double SpreadOpenStart = 0.08;
    public const double SpreadOpenLength = 0.20;
    public const double SpreadCloseStart = 0.80;
    public const double SpreadCloseLength = 0.15;

    /// <summary>Progress where the first layer stop begins.</summary>
    public const double LayersStart = 0.28;

    /// <summary>Progress where the layer stops end and the closing stop begins.</summary>
    public const double LayersEnd = 0.80;

    /// <summary>Separation at which the bottom label starts to draw.</summary>
    public const double LabelStart = 0.45;

    /// <summary>Separation between the first and the last label's start (0.07 per label for five).</summary>
    public const double LabelStagger = 0.28;

    /// <summary>Separation over which one label draws in.</summary>
    public const double LabelLength = 0.22;

    /// <summary>Opacity of a sheet's outline at full separation.</summary>
    public const double OutlineOpacity = 0.9;

    /// <summary>Depth offset between neighbouring layers at rest, so flat layers never fight for the same depth.</summary>
    public const double RestingDepthStep = 0.5;

    /// <summary><c>3u² − 2u³</c> with u clamped to 0..1.</summary>
    public static double Smoothstep(double u)
    {
        if (u <= 0)
        {
            return 0;
        }
        if (u >= 1)
        {
            return 1;
        }
        return u * u * (3 - 2 * u);
    }

    /// <summary>How far the stack has tipped back and turned (0 flat, 1 at the full angles).</summary>
    public static double Tilt(double progress) =>
        Math.Min(Smoothstep((progress - TiltOpenStart) / TiltOpenLength), 1 - Smoothstep((progress - TiltCloseStart) / TiltCloseLength));

    /// <summary>How far the layers have separated (0 together, 1 one gap apart).</summary>
    public static double Spread(double progress) =>
        Math.Min(Smoothstep((progress - SpreadOpenStart) / SpreadOpenLength), 1 - Smoothstep((progress - SpreadCloseStart) / SpreadCloseLength));

    /// <summary>
    /// The draw-in (0..1) of label <paramref name="index"/> (0 = bottom) of <paramref name="count"/> at separation
    /// <paramref name="spread"/>: the bottom label starts at 0.45, the top one finishes at 0.95, so opening draws them bottom
    /// up and closing hides them top down.
    /// </summary>
    public static double LabelDraw(int index, int count, double spread)
    {
        var start = LabelStart + (count > 1 ? LabelStagger * index / (count - 1) : 0);
        return Math.Clamp((spread - start) / LabelLength, 0, 1);
    }

    /// <summary>The number of stops for <paramref name="layers"/> layers: the whole screen, one per layer, and the screen put back together.</summary>
    public static int StopCount(int layers) => Math.Max(0, layers) + 2;

    /// <summary>
    /// The stop at <paramref name="progress"/>: 0 before 0.28, layer k (1..N) in its share of [0.28, 0.80), and N + 1 from
    /// 0.80 on (the closing stop begins the moment the layers start to join).
    /// </summary>
    public static int StopAt(double progress, int layers)
    {
        if (layers <= 0)
        {
            return progress >= LayersEnd ? 1 : 0;
        }
        if (progress < LayersStart)
        {
            return 0;
        }
        if (progress >= LayersEnd)
        {
            return layers + 1;
        }
        var share = (LayersEnd - LayersStart) / layers;
        // The epsilon keeps an exact boundary (0.384 for five layers) on the later stop, as the inequality says.
        var stop = (int)Math.Floor((progress - LayersStart) / share + 1e-9) + 1;
        return Math.Clamp(stop, 1, layers);
    }

    /// <summary>The progress a stop button scrolls to: 0, the middle of a layer's share, or 1.</summary>
    public static double StopProgress(int stop, int layers)
    {
        if (stop <= 0 || layers <= 0)
        {
            return stop <= 0 ? 0 : 1;
        }
        if (stop > layers)
        {
            return 1;
        }
        return LayersStart + (LayersEnd - LayersStart) / layers * (stop - 0.5);
    }

    /// <summary>The focused layer index at a stop (stop − 1), or −1 at the two ends.</summary>
    public static int LayerAt(int stop, int layers) => stop >= 1 && stop <= layers ? stop - 1 : -1;

    /// <summary>
    /// Tilt and separation for a frame. Continuous from the smoothed progress at full motion; discrete under reduced motion:
    /// flat at the two ends, fully apart at every layer stop, nothing in between.
    /// </summary>
    public static (double Tilt, double Spread) Pose(double progress, int layers, bool discrete)
    {
        if (!discrete)
        {
            return (Tilt(progress), Spread(progress));
        }
        return LayerAt(StopAt(progress, layers), layers) >= 0 ? (1, 1) : (0, 0);
    }

    /// <summary>
    /// Depth of layer <paramref name="index"/> of <paramref name="count"/>: centered on the middle layer, one
    /// <paramref name="gap"/> apart at full separation, plus a 0.5px resting step per layer.
    /// </summary>
    public static double LayerDepth(int index, int count, double gap, double spread) =>
        (index - (count - 1) / 2.0) * gap * spread + RestingDepthStep * index;
}
