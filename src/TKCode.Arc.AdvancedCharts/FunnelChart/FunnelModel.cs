namespace TKCode.Arc;

/// <summary>
/// The funnel's numbers: cleaned counts per segment and stage, the counts, retention and drop-off of a view, and the
/// band edges of a view in band units (the band's full thickness is 1, the centre line is 0). Pure, no Avalonia state.
/// </summary>
internal sealed class FunnelModel
{
    public const string TotalView = "total";
    public const string SplitView = "split";

    private readonly double[][] _layers;
    private readonly double[] _totals;

    public FunnelModel(IReadOnlyList<FunnelStage> stages, IReadOnlyList<FunnelSegment> segments)
    {
        Stages = stages;
        Segments = segments;
        var stageCount = stages.Count;
        LayerCount = Math.Max(1, segments.Count);
        _layers = new double[LayerCount][];
        for (var k = 0; k < LayerCount; k++)
        {
            var layer = new double[stageCount];
            for (var i = 0; i < stageCount; i++)
            {
                // A stage cannot hold more people than the one before it: drop-off is never negative.
                var raw = Clean(ReadCount(stages[i], k, segments.Count));
                layer[i] = i == 0 ? raw : Math.Min(raw, layer[i - 1]);
            }
            _layers[k] = layer;
        }
        _totals = new double[stageCount];
        for (var i = 0; i < stageCount; i++)
        {
            for (var k = 0; k < LayerCount; k++)
            {
                _totals[i] += _layers[k][i];
            }
        }
    }

    public IReadOnlyList<FunnelStage> Stages { get; }

    public IReadOnlyList<FunnelSegment> Segments { get; }

    public int StageCount => Stages.Count;

    /// <summary>Bands the geometry holds: one per segment, or one without segments.</summary>
    public int LayerCount { get; }

    /// <summary>Number of values the band springs animate: both edges per layer and stage, plus the split amount.</summary>
    public int SpringLength => LayerCount * StageCount * 2 + 1;

    public static int SpringIndex(int layer, int stage, int stageCount) => (layer * stageCount + stage) * 2;

    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    private static double ReadCount(FunnelStage stage, int layer, int segmentCount)
    {
        if (segmentCount == 0)
        {
            if (stage.Values is not { Count: > 0 } all)
            {
                return stage.Value;
            }
            var sum = 0.0;
            foreach (var value in all)
            {
                sum += Clean(value);
            }
            return sum;
        }
        return stage.Values is { } values && layer < values.Count ? values[layer] : 0;
    }

    /// <summary>A known view: "split" needs segments, any other unknown name falls back to "total".</summary>
    public string ResolveView(string? view)
    {
        if (view == SplitView && Segments.Count > 0)
        {
            return SplitView;
        }
        return IsolatedLayer(view) >= 0 ? view! : TotalView;
    }

    /// <summary>The segment a view isolates, or -1. "total" and "split" stay views even if a segment uses those keys.</summary>
    public int IsolatedLayer(string? view)
    {
        if (view is null or TotalView or SplitView)
        {
            return -1;
        }
        for (var k = 0; k < Segments.Count; k++)
        {
            if (Segments[k].Key == view)
            {
                return k;
            }
        }
        return -1;
    }

    public double LayerCountAt(int layer, int stage) => _layers[layer][stage];

    public double CountAt(string? view, int stage)
    {
        var isolated = IsolatedLayer(view);
        return isolated >= 0 ? _layers[isolated][stage] : _totals[stage];
    }

    public bool IsEmpty(string? view) => StageCount == 0 || CountAt(view, 0) <= 0;

    /// <summary>Share of <paramref name="from"/> that reached <paramref name="count"/>, always between 0 and 1.</summary>
    public static double Retention(double count, double from) => from > 0 ? Math.Clamp(count / from, 0, 1) : 0;

    public double FromPrevious(string? view, int stage)
    {
        if (stage == 0)
        {
            return CountAt(view, 0) > 0 ? 1 : 0;
        }
        return Retention(CountAt(view, stage), CountAt(view, stage - 1));
    }

    public double FromFirst(string? view, int stage) => Retention(CountAt(view, stage), CountAt(view, 0));

    public double DropOff(string? view, int stage) => stage == 0 ? 0 : Math.Max(0, CountAt(view, stage - 1) - CountAt(view, stage));

    /// <summary>The chance that a dot of a layer that reached <c>stage - 1</c> leaves before <paramref name="stage"/>.</summary>
    public double ExitChance(int layer, int stage) => stage <= 0 ? 0 : 1 - Retention(_layers[layer][stage], _layers[layer][stage - 1]);

    /// <summary>Writes the band edges of a view, plus the split amount (0 total, 1 otherwise) in the last slot.</summary>
    /// <param name="target">At least <see cref="SpringLength"/> values.</param>
    /// <param name="view">The view.</param>
    /// <param name="gap">The gap between split bands, as a fraction of the band thickness.</param>
    public void FillTargets(Span<double> target, string? view, double gap)
    {
        var resolved = ResolveView(view);
        var isolated = IsolatedLayer(resolved);
        var split = resolved == SplitView;
        var stageCount = StageCount;
        var first = stageCount > 0 ? CountAt(resolved, 0) : 0;
        var gaps = 0;
        if (split && stageCount > 0)
        {
            for (var k = 0; k < LayerCount; k++)
            {
                gaps += _layers[k][0] > 0 ? 1 : 0;
            }
            gaps = Math.Max(0, gaps - 1);
        }
        // The first step of a view always fills the band: isolating a segment rescales it, gaps come out of the thickness.
        var scale = first > 0 ? Math.Max(0.2, 1 - gap * gaps) / first : 0;
        for (var i = 0; i < stageCount; i++)
        {
            if (isolated >= 0)
            {
                var half = _layers[isolated][i] * scale / 2;
                for (var k = 0; k < LayerCount; k++)
                {
                    var index = SpringIndex(k, i, stageCount);
                    target[index] = k == isolated ? -half : 0;
                    target[index + 1] = k == isolated ? half : 0;
                }
                continue;
            }
            var present = 0;
            for (var k = 0; k < LayerCount; k++)
            {
                present += _layers[k][i] > 0 ? 1 : 0;
            }
            var between = split ? gap : 0;
            var cursor = -(_totals[i] * scale + between * Math.Max(0, present - 1)) / 2;
            for (var k = 0; k < LayerCount; k++)
            {
                var thickness = _layers[k][i] * scale;
                var index = SpringIndex(k, i, stageCount);
                target[index] = cursor;
                target[index + 1] = cursor + thickness;
                cursor += thickness;
                if (thickness > 0)
                {
                    present--;
                    cursor += present > 0 ? between : 0;
                }
            }
        }
        target[SpringLength - 1] = resolved == TotalView ? 0 : 1;
    }
}
