using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The structure of a journey, independent of the counts: stages left to right, nodes stacked in each stage, and the
/// links between neighbouring stages with each node's outgoing links ordered by their target's place in its stage and
/// incoming links ordered by their source's place (the order the ribbons leave and enter a node, top to bottom).
/// </summary>
internal sealed class JourneyFlowModel
{
    private JourneyFlowModel(int stageCount, int[] nodeStage, int[] nodeRank, JourneyNodeKind[] nodeKind, int[] linkSource, int[] linkTarget)
    {
        StageCount = stageCount;
        NodeStage = nodeStage;
        NodeRank = nodeRank;
        NodeKind = nodeKind;
        LinkSource = linkSource;
        LinkTarget = linkTarget;
        var stageNodes = new List<int>[stageCount];
        for (var s = 0; s < stageCount; s++)
        {
            stageNodes[s] = new List<int>();
        }
        for (var n = 0; n < nodeStage.Length; n++)
        {
            stageNodes[nodeStage[n]].Add(n);
        }
        StageNodes = stageNodes.Select(l => l.ToArray()).ToArray();
        var outgoing = new List<int>[nodeStage.Length];
        var incoming = new List<int>[nodeStage.Length];
        for (var n = 0; n < nodeStage.Length; n++)
        {
            outgoing[n] = new List<int>();
            incoming[n] = new List<int>();
        }
        for (var l = 0; l < linkSource.Length; l++)
        {
            outgoing[linkSource[l]].Add(l);
            incoming[linkTarget[l]].Add(l);
        }
        Outgoing = outgoing.Select(list => list.OrderBy(l => nodeRank[linkTarget[l]]).ToArray()).ToArray();
        Incoming = incoming.Select(list => list.OrderBy(l => nodeRank[linkSource[l]]).ToArray()).ToArray();
    }

    /// <summary>An empty journey.</summary>
    public static JourneyFlowModel Empty { get; } = new(0, [], [], [], [], []);

    /// <summary>Stages (columns).</summary>
    public int StageCount { get; }

    /// <summary>Nodes.</summary>
    public int NodeCount => NodeStage.Length;

    /// <summary>Links.</summary>
    public int LinkCount => LinkSource.Length;

    /// <summary>Stage of each node.</summary>
    public int[] NodeStage { get; }

    /// <summary>Place of each node in its stage (0 = top).</summary>
    public int[] NodeRank { get; }

    /// <summary>Kind of each node.</summary>
    public JourneyNodeKind[] NodeKind { get; }

    /// <summary>Source node of each link.</summary>
    public int[] LinkSource { get; }

    /// <summary>Target node of each link (in the stage after the source's).</summary>
    public int[] LinkTarget { get; }

    /// <summary>Nodes of each stage, top to bottom.</summary>
    public int[][] StageNodes { get; }

    /// <summary>Outgoing links of each node, ordered by the target's place.</summary>
    public int[][] Outgoing { get; }

    /// <summary>Incoming links of each node, ordered by the source's place.</summary>
    public int[][] Incoming { get; }

    /// <summary>
    /// Builds the structure from keys. Nodes whose stage is unknown are left out; links are the distinct source → target
    /// pairs whose target is in the stage right after the source's, ordered by source node, then by the target's place
    /// (the flow table's row order). Returned index maps give each input node's and pair's index (−1 when left out).
    /// </summary>
    public static JourneyFlowModel Build(
        IReadOnlyList<string?> stageKeys,
        IReadOnlyList<(string? Key, string? StageKey, JourneyNodeKind Kind)> nodes,
        IReadOnlyList<(string? Source, string? Target)> pairs,
        out int[] nodeIndexOf,
        out int[] linkIndexOf)
    {
        var stageIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var s = 0; s < stageKeys.Count; s++)
        {
            if (stageKeys[s] is { } key)
            {
                stageIndex.TryAdd(key, s);
            }
        }
        nodeIndexOf = new int[nodes.Count];
        var nodeStage = new List<int>();
        var nodeKind = new List<JourneyNodeKind>();
        var nodeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var rankCount = new int[stageKeys.Count];
        var nodeRank = new List<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            var (key, stageKey, kind) = nodes[i];
            if (key is null || stageKey is null || !stageIndex.TryGetValue(stageKey, out var stage) || nodeIndex.ContainsKey(key))
            {
                nodeIndexOf[i] = -1;
                continue;
            }
            nodeIndexOf[i] = nodeStage.Count;
            nodeIndex[key] = nodeStage.Count;
            nodeStage.Add(stage);
            nodeKind.Add(kind);
            nodeRank.Add(rankCount[stage]++);
        }
        linkIndexOf = new int[pairs.Count];
        var distinct = new List<(int Source, int Target)>();
        var pairIndex = new Dictionary<(int, int), int>();
        var pairOf = new int[pairs.Count];
        for (var i = 0; i < pairs.Count; i++)
        {
            pairOf[i] = -1;
            var (source, target) = pairs[i];
            if (source is null || target is null || !nodeIndex.TryGetValue(source, out var s) || !nodeIndex.TryGetValue(target, out var t))
            {
                continue;
            }
            if (nodeStage[t] != nodeStage[s] + 1)
            {
                continue;
            }
            if (!pairIndex.TryGetValue((s, t), out var p))
            {
                p = distinct.Count;
                pairIndex[(s, t)] = p;
                distinct.Add((s, t));
            }
            pairOf[i] = p;
        }
        var order = Enumerable.Range(0, distinct.Count)
                              .OrderBy(p => distinct[p].Source)
                              .ThenBy(p => nodeRank[distinct[p].Target])
                              .ToArray();
        var newIndex = new int[distinct.Count];
        for (var i = 0; i < order.Length; i++)
        {
            newIndex[order[i]] = i;
        }
        for (var i = 0; i < pairs.Count; i++)
        {
            linkIndexOf[i] = pairOf[i] < 0 ? -1 : newIndex[pairOf[i]];
        }
        var linkSource = order.Select(p => distinct[p].Source).ToArray();
        var linkTarget = order.Select(p => distinct[p].Target).ToArray();
        return new JourneyFlowModel(stageKeys.Count, nodeStage.ToArray(), nodeRank.ToArray(), nodeKind.ToArray(), linkSource, linkTarget);
    }

    /// <summary>The link from <paramref name="source"/> to <paramref name="target"/>, or −1.</summary>
    public int FindLink(int source, int target)
    {
        if (source < 0 || source >= NodeCount)
        {
            return -1;
        }
        foreach (var l in Outgoing[source])
        {
            if (LinkTarget[l] == target)
            {
                return l;
            }
        }
        return -1;
    }
}

/// <summary>How much of each node label fits, by chart width (Arc: percent from 600, names of top nodes from 440).</summary>
internal enum JourneyLabelDensity
{
    /// <summary>Name and "count · share%" (share left out in the first stage).</summary>
    Full,

    /// <summary>Name and count.</summary>
    NoShare,

    /// <summary>Count only for the top node of each stage (its stage name says what it is), name and count for the others; short stage names.</summary>
    Compact
}

/// <summary>Where everything of a journey chart sits for one set of counts and one size (pixels, chart coordinates).</summary>
internal sealed class JourneyFlowLayout
{
    /// <summary>Chart width.</summary>
    public double Width;

    /// <summary>Chart height.</summary>
    public double Height;

    /// <summary>Top of the node area (40: two label lines fit above the top nodes).</summary>
    public double Top;

    /// <summary>Bottom of the node area (height − 34).</summary>
    public double Bottom;

    /// <summary>Pixels per person.</summary>
    public double Scale;

    /// <summary>Left edge of each stage's nodes.</summary>
    public double[] ColumnX = [];

    /// <summary>Each node's bar (8 wide).</summary>
    public Rect[] Nodes = [];

    /// <summary>Each node's count (max of in and out).</summary>
    public double[] NodeTotals = [];

    /// <summary>Each stage's total.</summary>
    public double[] StageTotals = [];

    /// <summary>Top of each link at its source node.</summary>
    public double[] LinkSourceY = [];

    /// <summary>Top of each link at its target node.</summary>
    public double[] LinkTargetY = [];

    /// <summary>Thickness of each link.</summary>
    public double[] LinkThickness = [];

    /// <summary>Y of the bottom rule (crisp: H − 23.5).</summary>
    public double BaselineY => Height - JourneyFlowEngine.RuleOffset;

    /// <summary>Baseline of the stage names (H − 6).</summary>
    public double StageNameBaseline => Height - JourneyFlowEngine.StageNameOffset;

    /// <summary>The source x of a link (right edge of the source bar).</summary>
    public double LinkSourceX(JourneyFlowModel model, int link) => Nodes[model.LinkSource[link]].Right;

    /// <summary>The target x of a link (left edge of the target bar).</summary>
    public double LinkTargetX(JourneyFlowModel model, int link) => Nodes[model.LinkTarget[link]].Left;

    /// <summary>Makes the arrays fit a model (reused between frames).</summary>
    public void Ensure(JourneyFlowModel model)
    {
        Resize(ref ColumnX, model.StageCount);
        Resize(ref StageTotals, model.StageCount);
        Resize(ref Nodes, model.NodeCount);
        Resize(ref NodeTotals, model.NodeCount);
        Resize(ref LinkSourceY, model.LinkCount);
        Resize(ref LinkTargetY, model.LinkCount);
        Resize(ref LinkThickness, model.LinkCount);
    }

    private static void Resize<T>(ref T[] array, int count)
    {
        if (array.Length != count)
        {
            array = new T[count];
        }
    }
}

/// <summary>
/// The journey chart's arithmetic (Arc Journey flow, measured): chart height tiers, column positions, the Sankey scale and
/// stacking, link ends, cohort tracing, label density and the derived figures of the side panel. Pure and allocation-free
/// on the per-frame paths (layout and cohort take caller arrays).
/// </summary>
internal static class JourneyFlowEngine
{
    /// <summary>Node bar width.</summary>
    public const double NodeWidth = 8;

    /// <summary>Node bar corner radius.</summary>
    public const double NodeRadius = 1;

    /// <summary>Vertical gap between nodes of a stage (room for the lower node's two label lines).</summary>
    public const double NodeGap = 44;

    /// <summary>Top of the node area.</summary>
    public const double PlotTop = 40;

    /// <summary>Space under the node area (rule and stage names).</summary>
    public const double AxisBand = 34;

    /// <summary>The bottom rule is this far above the chart's bottom (half-pixel aligned).</summary>
    public const double RuleOffset = 23.5;

    /// <summary>The stage names' baseline is this far above the chart's bottom.</summary>
    public const double StageNameOffset = 6;

    /// <summary>Baseline of a node's name line, relative to the node's top.</summary>
    public const double NameBaseline = -20;

    /// <summary>Baseline of a node's count line, relative to the node's top.</summary>
    public const double CountBaseline = -7;

    /// <summary>Ribbons widen their hit area by this much on each side (Arc's 4px transparent stroke).</summary>
    public const double RibbonHitSlop = 2;

    /// <summary>Chart height for a chart width: 404 from 720, 372 from 480, 340 below.</summary>
    public static double ChartHeight(double width) => width >= 720 ? 404 : width >= 480 ? 372 : 340;

    /// <summary>Which label parts fit at a chart width.</summary>
    public static JourneyLabelDensity Density(double width) =>
        width >= 600 ? JourneyLabelDensity.Full : width >= 440 ? JourneyLabelDensity.NoShare : JourneyLabelDensity.Compact;

    /// <summary>JavaScript's <c>Math.round</c> (halves go up), which Arc's column positions use.</summary>
    public static double RoundHalfUp(double value) => Math.Floor(value + 0.5);

    /// <summary>The node bar's hit rectangle: 6px out to the sides, 3 above and below (Arc's 20 × (h + 6) button).</summary>
    public static Rect NodeHitRect(Rect node) => new(node.X - 6, node.Y - 3, NodeWidth + 12, node.Height + 6);

    /// <summary>The label's hit rectangle (92 × 33 above the node; the last stage's grows to the left).</summary>
    public static Rect LabelHitRect(Rect node, bool isLast) => new(isLast ? node.X - 78 : node.X - 6, node.Y - 36, 92, 33);

    /// <summary>
    /// Counts of every node: the larger of what flows in and what flows out (first stage: what flows out; exits and
    /// goals: what flows in).
    /// </summary>
    public static void NodeTotals(JourneyFlowModel model, ReadOnlySpan<double> values, Span<double> totals)
    {
        for (var n = 0; n < model.NodeCount; n++)
        {
            double inSum = 0, outSum = 0;
            foreach (var l in model.Incoming[n])
            {
                inSum += Value(values, l);
            }
            foreach (var l in model.Outgoing[n])
            {
                outSum += Value(values, l);
            }
            totals[n] = Math.Max(inSum, outSum);
        }
    }

    /// <summary>
    /// Lays the journey out for <paramref name="values"/> (one count per link) at <paramref name="width"/> ×
    /// <paramref name="height"/>: columns at <c>round(i · (W − 8) / (n − 1))</c>, scale <c>k = min over stages of
    /// (plotH − 44 · (m − 1)) / total</c>, nodes stacked from the top 44 apart, link ends stacked in each node in the order of
    /// the other end.
    /// </summary>
    public static void Layout(JourneyFlowModel model, ReadOnlySpan<double> values, double width, double height, JourneyFlowLayout layout)
    {
        layout.Ensure(model);
        layout.Width = width;
        layout.Height = height;
        layout.Top = PlotTop;
        layout.Bottom = height - AxisBand;
        var plotHeight = Math.Max(0, layout.Bottom - layout.Top);
        NodeTotals(model, values, layout.NodeTotals);
        var n = model.StageCount;
        var scale = double.PositiveInfinity;
        for (var s = 0; s < n; s++)
        {
            layout.ColumnX[s] = n <= 1 ? 0 : RoundHalfUp(s * (width - NodeWidth) / (n - 1));
            double total = 0;
            foreach (var node in model.StageNodes[s])
            {
                total += layout.NodeTotals[node];
            }
            layout.StageTotals[s] = total;
            if (total > 0)
            {
                var room = Math.Max(0, plotHeight - NodeGap * (model.StageNodes[s].Length - 1));
                scale = Math.Min(scale, room / total);
            }
        }
        layout.Scale = double.IsFinite(scale) ? scale : 0;
        for (var s = 0; s < n; s++)
        {
            var y = layout.Top;
            foreach (var node in model.StageNodes[s])
            {
                var h = layout.NodeTotals[node] * layout.Scale;
                layout.Nodes[node] = new Rect(layout.ColumnX[s], y, NodeWidth, h);
                y += h + NodeGap;
            }
        }
        for (var node = 0; node < model.NodeCount; node++)
        {
            var y = layout.Nodes[node].Y;
            foreach (var l in model.Outgoing[node])
            {
                var t = Value(values, l) * layout.Scale;
                layout.LinkSourceY[l] = y;
                layout.LinkThickness[l] = t;
                y += t;
            }
            y = layout.Nodes[node].Y;
            foreach (var l in model.Incoming[node])
            {
                layout.LinkTargetY[l] = y;
                y += Value(values, l) * layout.Scale;
            }
        }
    }

    /// <summary>
    /// The people of a node's cohort (everyone who passed through it) in every link. Downstream every outgoing link counts
    /// in full, and each later node passes on the cohort's share of its inflow to each of its outgoing links;
    /// upstream every incoming link counts in full, and each earlier node passes on the cohort's share of its outflow to
    /// each of its incoming links (Arc's proportional mixing, measured on every link).
    /// </summary>
    public static void NodeCohort(JourneyFlowModel model, ReadOnlySpan<double> values, int node, Span<double> cohort)
    {
        cohort.Clear();
        if (node < 0 || node >= model.NodeCount)
        {
            return;
        }
        foreach (var l in model.Outgoing[node])
        {
            cohort[l] = Value(values, l);
        }
        foreach (var l in model.Incoming[node])
        {
            cohort[l] = Value(values, l);
        }
        Downstream(model, values, model.NodeStage[node] + 1, cohort);
        Upstream(model, values, model.NodeStage[node] - 1, cohort);
    }

    /// <summary>
    /// The people of a link's cohort (everyone who took that link) in every link: the link in full, then the target's share
    /// passed downstream and the source's share passed upstream as in <see cref="NodeCohort"/>.
    /// </summary>
    public static void LinkCohort(JourneyFlowModel model, ReadOnlySpan<double> values, int link, Span<double> cohort)
    {
        cohort.Clear();
        if (link < 0 || link >= model.LinkCount)
        {
            return;
        }
        cohort[link] = Value(values, link);
        Downstream(model, values, model.NodeStage[model.LinkTarget[link]], cohort);
        Upstream(model, values, model.NodeStage[model.LinkSource[link]], cohort);
    }

    /// <summary>A node's cohort people: the larger of its cohort inflow and outflow.</summary>
    public static double NodeCohortTotal(JourneyFlowModel model, ReadOnlySpan<double> cohort, int node)
    {
        double inSum = 0, outSum = 0;
        foreach (var l in model.Incoming[node])
        {
            inSum += cohort[l];
        }
        foreach (var l in model.Outgoing[node])
        {
            outSum += cohort[l];
        }
        return Math.Max(inSum, outSum);
    }

    /// <summary>
    /// The node whose count a stage's shares are measured against: the nearest earlier node whose count equals the stage's
    /// total (everyone in the stage came through it), or −1. Stage 0 has none.
    /// </summary>
    public static int ShareBaseNode(JourneyFlowModel model, ReadOnlySpan<double> totals, int stage)
    {
        if (stage <= 0 || stage >= model.StageCount)
        {
            return -1;
        }
        double stageTotal = 0;
        foreach (var n in model.StageNodes[stage])
        {
            stageTotal += totals[n];
        }
        var tolerance = Math.Max(1e-6, stageTotal * 1e-9);
        for (var s = stage - 1; s >= 0; s--)
        {
            foreach (var n in model.StageNodes[s])
            {
                if (Math.Abs(totals[n] - stageTotal) <= tolerance)
                {
                    return n;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// The exits that end the journey early (exit nodes before the last stage with nothing flowing on), largest first, at
    /// most <paramref name="max"/>, each with its share of the nodes it came from. An exit that people still move on from
    /// (a stalled step that some recover from) is not a drop-off, as measured.
    /// </summary>
    public static List<(int Node, double Count, double Share)> DropOffs(JourneyFlowModel model, ReadOnlySpan<double> values, ReadOnlySpan<double> totals, int max)
    {
        var list = new List<(int Node, double Count, double Share)>();
        for (var n = 0; n < model.NodeCount; n++)
        {
            if (model.NodeKind[n] != JourneyNodeKind.Exit || model.NodeStage[n] >= model.StageCount - 1 || HasOutflow(model, values, n))
            {
                continue;
            }
            double sources = 0;
            var seen = new HashSet<int>();
            foreach (var l in model.Incoming[n])
            {
                if (seen.Add(model.LinkSource[l]))
                {
                    sources += totals[model.LinkSource[l]];
                }
            }
            list.Add((n, totals[n], sources > 0 ? totals[n] / sources : 0));
        }
        list.Sort((a, b) => b.Count.CompareTo(a.Count));
        if (list.Count > max)
        {
            list.RemoveRange(max, list.Count - max);
        }
        return list;
    }

    /// <summary>
    /// Keyboard ←/→: the node of the neighbouring stage whose vertical centre is nearest to <paramref name="node"/>'s, or
    /// −1 at the ends.
    /// </summary>
    public static int Nearest(JourneyFlowModel model, JourneyFlowLayout layout, int node, int direction)
    {
        var stage = model.NodeStage[node] + Math.Sign(direction);
        if (stage < 0 || stage >= model.StageCount || model.StageNodes[stage].Length == 0)
        {
            return -1;
        }
        var y = layout.Nodes[node].Center.Y;
        var best = -1;
        var bestDistance = double.PositiveInfinity;
        foreach (var candidate in model.StageNodes[stage])
        {
            var distance = Math.Abs(layout.Nodes[candidate].Center.Y - y);
            if (distance < bestDistance - 1e-9)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Visitors: the first stage's total.</summary>
    public static double Visitors(JourneyFlowModel model, ReadOnlySpan<double> totals)
    {
        if (model.StageCount == 0)
        {
            return 0;
        }
        double sum = 0;
        foreach (var n in model.StageNodes[0])
        {
            sum += totals[n];
        }
        return sum;
    }

    /// <summary>People in goal nodes.</summary>
    public static double GoalTotal(JourneyFlowModel model, ReadOnlySpan<double> totals)
    {
        double sum = 0;
        for (var n = 0; n < model.NodeCount; n++)
        {
            if (model.NodeKind[n] == JourneyNodeKind.Goal)
            {
                sum += totals[n];
            }
        }
        return sum;
    }

    /// <summary>
    /// The curve parameter t at <paramref name="x"/> on a ribbon edge from <paramref name="x0"/> to <paramref name="x1"/>.
    /// With both control points at the horizontal midpoint, <c>x(t) = x0 + Δ·(1.5t − 1.5t² + t³)</c>, which only grows, so
    /// bisection finds it.
    /// </summary>
    public static double RibbonParameter(double x0, double x1, double x)
    {
        var span = x1 - x0;
        if (Math.Abs(span) < 1e-9)
        {
            return 0;
        }
        var target = Math.Clamp((x - x0) / span, 0, 1);
        double lo = 0, hi = 1;
        for (var i = 0; i < 30; i++)
        {
            var mid = (lo + hi) / 2;
            var value = 1.5 * mid - 1.5 * mid * mid + mid * mid * mid;
            if (value < target)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        return (lo + hi) / 2;
    }

    /// <summary>
    /// The y of a ribbon edge at curve parameter <paramref name="t"/>: the control points keep the end heights, so
    /// <c>y(t) = y0 + (y1 − y0)·(3t² − 2t³)</c>.
    /// </summary>
    public static double RibbonEdgeY(double y0, double y1, double t) => y0 + (y1 - y0) * (t * t * (3 - 2 * t));

    /// <summary>
    /// Whether a ribbon (see <see cref="AnalyticsGeometry.Ribbon"/>) contains <paramref name="point"/>, its area widened
    /// by <paramref name="slop"/> on every side (Arc's 4px transparent stroke = 2px each side).
    /// </summary>
    public static bool RibbonContains(double x0, double y0Top, double y0Bottom, double x1, double y1Top, double y1Bottom, Point point, double slop = RibbonHitSlop)
    {
        if (point.X < x0 - slop || point.X > x1 + slop)
        {
            return false;
        }
        var t = RibbonParameter(x0, x1, point.X);
        var top = RibbonEdgeY(y0Top, y1Top, t);
        var bottom = RibbonEdgeY(y0Bottom, y1Bottom, t);
        return point.Y >= top - slop && point.Y <= bottom + slop;
    }

    private static bool HasOutflow(JourneyFlowModel model, ReadOnlySpan<double> values, int node)
    {
        foreach (var l in model.Outgoing[node])
        {
            if (Value(values, l) > 0)
            {
                return true;
            }
        }
        return false;
    }

    private static void Downstream(JourneyFlowModel model, ReadOnlySpan<double> values, int fromStage, Span<double> cohort)
    {
        for (var s = Math.Max(0, fromStage); s < model.StageCount; s++)
        {
            foreach (var node in model.StageNodes[s])
            {
                double inCohort = 0, inTotal = 0;
                foreach (var l in model.Incoming[node])
                {
                    inCohort += cohort[l];
                    inTotal += Value(values, l);
                }
                var share = inTotal > 0 ? Math.Clamp(inCohort / inTotal, 0, 1) : 0;
                foreach (var l in model.Outgoing[node])
                {
                    cohort[l] = Value(values, l) * share;
                }
            }
        }
    }

    private static void Upstream(JourneyFlowModel model, ReadOnlySpan<double> values, int fromStage, Span<double> cohort)
    {
        for (var s = Math.Min(model.StageCount - 1, fromStage); s >= 0; s--)
        {
            foreach (var node in model.StageNodes[s])
            {
                double outCohort = 0, outTotal = 0;
                foreach (var l in model.Outgoing[node])
                {
                    outCohort += cohort[l];
                    outTotal += Value(values, l);
                }
                var share = outTotal > 0 ? Math.Clamp(outCohort / outTotal, 0, 1) : 0;
                foreach (var l in model.Incoming[node])
                {
                    cohort[l] = Value(values, l) * share;
                }
            }
        }
    }

    private static double Value(ReadOnlySpan<double> values, int link)
    {
        if (link >= values.Length)
        {
            return 0;
        }
        var v = values[link];
        return double.IsFinite(v) && v > 0 ? v : 0;
    }
}
