using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>The direction ranks run in a <see cref="LayeredGraphLayout"/>.</summary>
public enum LayeredGraphDirection
{
    /// <summary>Ranks run down the page; edges leave a node's bottom and enter the next node's top (Arc's workflow graph).</summary>
    TopToBottom,

    /// <summary>Ranks run across the page; edges leave a node's right side and enter the next node's left side.</summary>
    LeftToRight
}

/// <summary>A directed edge between two nodes of a <see cref="LayeredGraphLayout"/>, by index.</summary>
public readonly record struct LayeredGraphEdge(int Source, int Target);

/// <summary>Spacing and effort of a <see cref="LayeredGraphLayout"/>.</summary>
public sealed class LayeredGraphLayoutOptions
{
    /// <summary>The defaults: Arc's workflow graph spacing, top to bottom.</summary>
    public static LayeredGraphLayoutOptions Default { get; } = new();

    /// <summary>Which way the ranks run (default <see cref="LayeredGraphDirection.TopToBottom"/>).</summary>
    public LayeredGraphDirection Direction { get; init; } = LayeredGraphDirection.TopToBottom;

    /// <summary>Space between two ranks (default 52: Arc's 60 high steps placed every 112).</summary>
    public double RankGap { get; init; } = 52;

    /// <summary>
    /// Space below a rank where some node fans out to two or more nodes of the next rank, so the curves have room to
    /// spread (default 72: Arc's branch at 224 + 60 and its actions at 356).
    /// </summary>
    public double FanOutRankGap { get; init; } = 72;

    /// <summary>Space between neighbours in a rank (default 20: Arc's 200 wide actions placed every 220).</summary>
    public double NodeGap { get; init; } = 20;

    /// <summary>Barycenter sweeps that reorder ranks to remove crossings (default 12; stops early at none).</summary>
    public int OrderingSweeps { get; init; } = 12;

    /// <summary>Alternating down and up passes that center nodes over their neighbours (default 4).</summary>
    public int PositioningPasses { get; init; } = 4;
}

/// <summary>The outcome of <see cref="LayeredGraphLayout.Arrange"/>; lists are indexed like the input nodes.</summary>
public sealed class LayeredGraphLayoutResult
{
    internal LayeredGraphLayoutResult(Point[] positions, int[] ranks, int[] orders, int rankCount, Size size,
                                      int[] reversedEdges, int crossings)
    {
        Positions = positions;
        Ranks = ranks;
        Orders = orders;
        RankCount = rankCount;
        Size = size;
        ReversedEdges = reversedEdges;
        Crossings = crossings;
    }

    /// <summary>Each node's top-left corner; the layout's bounding box starts at (0, 0).</summary>
    public IReadOnlyList<Point> Positions { get; }

    /// <summary>Each node's rank, from 0 (sources) on.</summary>
    public IReadOnlyList<int> Ranks { get; }

    /// <summary>Each node's place among the nodes of its rank, from the left (top for left to right).</summary>
    public IReadOnlyList<int> Orders { get; }

    /// <summary>The number of ranks.</summary>
    public int RankCount { get; }

    /// <summary>The bounding box of all nodes.</summary>
    public Size Size { get; }

    /// <summary>Indices of input edges that were laid out reversed to break cycles.</summary>
    public IReadOnlyList<int> ReversedEdges { get; }

    /// <summary>Edge crossings between adjacent ranks that remain (long edges counted per rank they span).</summary>
    public int Crossings { get; }
}

/// <summary>
/// Arranges a directed graph in ranks (a simplified Sugiyama layout): cycles are broken by reversing DFS back edges,
/// nodes get the rank of their longest path from a source (sources move down next to their first successor), edges
/// that span several ranks are routed through placeholder nodes, barycenter sweeps reorder each rank to remove
/// crossings, and alternating passes center every node over its neighbours while keeping
/// <see cref="LayeredGraphLayoutOptions.NodeGap"/> between neighbours (a weighted isotonic regression per rank).
/// <para>
/// Pure and deterministic: the same input always gives the same output, and nodes keep their input order wherever
/// that adds no crossing. Nodes in a rank share its leading edge (top for top to bottom).
/// </para>
/// </summary>
public static class LayeredGraphLayout
{
    private const double DummyWeight = 4;
    private const double LooseWeight = 0.01;

    /// <summary>
    /// Arranges nodes of the given <paramref name="sizes"/> connected by <paramref name="edges"/>. Self loops and
    /// repeated edges are ignored.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An edge refers to a node index outside <paramref name="sizes"/>.</exception>
    public static LayeredGraphLayoutResult Arrange(IReadOnlyList<Size> sizes, IReadOnlyList<LayeredGraphEdge> edges,
                                                   LayeredGraphLayoutOptions? options = null)
    {
        options ??= LayeredGraphLayoutOptions.Default;
        var n = sizes.Count;
        if (n == 0)
        {
            return new LayeredGraphLayoutResult([], [], [], 0, default, [], 0);
        }

        var (dagEdges, reversed) = BreakCycles(n, edges);
        var ranks = AssignRanks(n, dagEdges, out var rankCount);
        var graph = new LayeredGraph(n, ranks, rankCount, dagEdges);
        var crossings = Order(graph, options.OrderingSweeps);

        var horizontal = options.Direction == LayeredGraphDirection.TopToBottom;
        var breadth = new double[graph.Count];
        for (var v = 0; v < n; v++)
        {
            breadth[v] = horizontal ? sizes[v].Width : sizes[v].Height;
        }
        var cross = Position(graph, breadth, options);

        // Rank offsets: each rank is as deep as its deepest node, then a gap (wider below a fan-out).
        var rankStart = new double[rankCount];
        for (var r = 0; r + 1 < rankCount; r++)
        {
            var depth = 0.0;
            var fanOut = false;
            foreach (var v in graph.Layers[r])
            {
                if (v < n)
                {
                    depth = Math.Max(depth, horizontal ? sizes[v].Height : sizes[v].Width);
                    fanOut |= graph.Down[v].Count >= 2;
                }
            }
            rankStart[r + 1] = rankStart[r] + depth + (fanOut ? options.FanOutRankGap : options.RankGap);
        }

        var minCross = double.PositiveInfinity;
        for (var v = 0; v < n; v++)
        {
            minCross = Math.Min(minCross, cross[v]);
        }
        var positions = new Point[n];
        var orders = new int[n];
        double width = 0, height = 0;
        for (var r = 0; r < rankCount; r++)
        {
            var order = 0;
            foreach (var v in graph.Layers[r])
            {
                if (v >= n)
                {
                    continue;
                }
                orders[v] = order++;
                var c = cross[v] - minCross;
                positions[v] = horizontal ? new Point(c, rankStart[r]) : new Point(rankStart[r], c);
                width = Math.Max(width, positions[v].X + sizes[v].Width);
                height = Math.Max(height, positions[v].Y + sizes[v].Height);
            }
        }
        return new LayeredGraphLayoutResult(positions, ranks, orders, rankCount, new Size(width, height), reversed, crossings);
    }

    // Cycles ------------------------------------------------------------------------------------------------------------

    /// <summary>Distinct edges of an acyclic graph, with DFS back edges reversed, and the input indices that were reversed.</summary>
    private static (List<(int From, int To)> Edges, int[] Reversed) BreakCycles(int n, IReadOnlyList<LayeredGraphEdge> edges)
    {
        var outgoing = new List<int>[n];
        for (var v = 0; v < n; v++)
        {
            outgoing[v] = [];
        }
        var seen = new HashSet<(int, int)>();
        for (var i = 0; i < edges.Count; i++)
        {
            var (s, t) = (edges[i].Source, edges[i].Target);
            if ((uint)s >= (uint)n || (uint)t >= (uint)n)
            {
                throw new ArgumentOutOfRangeException(nameof(edges), $"Edge {i} ({s} → {t}) refers to a node outside 0..{n - 1}.");
            }
            if (s != t && seen.Add((s, t)))
            {
                outgoing[s].Add(i);
            }
        }

        // Iterative DFS in node and edge order; an edge into a node still on the stack closes a cycle.
        var state = new byte[n];
        var back = new bool[edges.Count];
        var stack = new Stack<(int Node, int Next)>();
        for (var root = 0; root < n; root++)
        {
            if (state[root] != 0)
            {
                continue;
            }
            state[root] = 1;
            stack.Push((root, 0));
            while (stack.Count > 0)
            {
                var (v, next) = stack.Pop();
                if (next < outgoing[v].Count)
                {
                    stack.Push((v, next + 1));
                    var e = outgoing[v][next];
                    var w = edges[e].Target;
                    if (state[w] == 1)
                    {
                        back[e] = true;
                    }
                    else if (state[w] == 0)
                    {
                        state[w] = 1;
                        stack.Push((w, 0));
                    }
                }
                else
                {
                    state[v] = 2;
                }
            }
        }

        var result = new List<(int, int)>();
        var reversed = new List<int>();
        var kept = new HashSet<(int, int)>();
        for (var v = 0; v < n; v++)
        {
            foreach (var e in outgoing[v])
            {
                var (s, t) = (edges[e].Source, edges[e].Target);
                if (back[e])
                {
                    (s, t) = (t, s);
                    reversed.Add(e);
                }
                if (kept.Add((s, t)))
                {
                    result.Add((s, t));
                }
            }
        }
        reversed.Sort();
        return (result, reversed.ToArray());
    }

    // Ranks -------------------------------------------------------------------------------------------------------------

    private static int[] AssignRanks(int n, List<(int From, int To)> edges, out int rankCount)
    {
        var successors = new List<int>[n];
        var predecessorCount = new int[n];
        for (var v = 0; v < n; v++)
        {
            successors[v] = [];
        }
        foreach (var (from, to) in edges)
        {
            successors[from].Add(to);
            predecessorCount[to]++;
        }

        // Kahn's order, sources in index order.
        var topo = new List<int>(n);
        var remaining = (int[])predecessorCount.Clone();
        var queue = new Queue<int>();
        for (var v = 0; v < n; v++)
        {
            if (remaining[v] == 0)
            {
                queue.Enqueue(v);
            }
        }
        while (queue.Count > 0)
        {
            var v = queue.Dequeue();
            topo.Add(v);
            foreach (var w in successors[v])
            {
                if (--remaining[w] == 0)
                {
                    queue.Enqueue(w);
                }
            }
        }

        // Longest path from the sources.
        var ranks = new int[n];
        foreach (var v in topo)
        {
            foreach (var w in successors[v])
            {
                ranks[w] = Math.Max(ranks[w], ranks[v] + 1);
            }
        }
        // A source that only feeds a deep node moves down next to it instead of stretching a long edge from the top.
        for (var i = topo.Count - 1; i >= 0; i--)
        {
            var v = topo[i];
            if (predecessorCount[v] == 0 && successors[v].Count > 0)
            {
                var first = int.MaxValue;
                foreach (var w in successors[v])
                {
                    first = Math.Min(first, ranks[w]);
                }
                ranks[v] = first - 1;
            }
        }

        // Renumber the used ranks 0, 1, 2… (keeps every edge pointing down).
        var used = new SortedSet<int>(ranks);
        var map = new Dictionary<int, int>();
        foreach (var r in used)
        {
            map[r] = map.Count;
        }
        for (var v = 0; v < n; v++)
        {
            ranks[v] = map[ranks[v]];
        }
        rankCount = map.Count;
        return ranks;
    }

    // Ordering ----------------------------------------------------------------------------------------------------------

    /// <summary>Barycenter sweeps; keeps the order with the fewest crossings and returns that count.</summary>
    private static int Order(LayeredGraph graph, int sweeps)
    {
        var best = graph.CountCrossings();
        var bestLayers = graph.SnapshotOrder();
        for (var sweep = 0; sweep < sweeps && best > 0; sweep++)
        {
            var down = sweep % 2 == 0;
            if (down)
            {
                for (var r = 1; r < graph.Layers.Length; r++)
                {
                    graph.Reorder(r, graph.Up);
                }
            }
            else
            {
                for (var r = graph.Layers.Length - 2; r >= 0; r--)
                {
                    graph.Reorder(r, graph.Down);
                }
            }
            var crossings = graph.CountCrossings();
            if (crossings < best)
            {
                best = crossings;
                bestLayers = graph.SnapshotOrder();
            }
        }
        graph.RestoreOrder(bestLayers);
        return best;
    }

    // Positioning -------------------------------------------------------------------------------------------------------

    /// <summary>Cross-axis leading edge of every layered node (placeholders have no breadth).</summary>
    private static double[] Position(LayeredGraph graph, double[] breadth, LayeredGraphLayoutOptions options)
    {
        var gap = options.NodeGap;
        var x = new double[graph.Count];

        // Pack each rank, centered on the widest one.
        var widest = 0.0;
        var widths = new double[graph.Layers.Length];
        for (var r = 0; r < graph.Layers.Length; r++)
        {
            var layer = graph.Layers[r];
            var w = 0.0;
            for (var i = 0; i < layer.Count; i++)
            {
                w += breadth[layer[i]] + (i > 0 ? gap : 0);
            }
            widths[r] = w;
            widest = Math.Max(widest, w);
        }
        for (var r = 0; r < graph.Layers.Length; r++)
        {
            var cursor = (widest - widths[r]) / 2;
            foreach (var v in graph.Layers[r])
            {
                x[v] = cursor;
                cursor += breadth[v] + gap;
            }
        }

        for (var pass = 0; pass < options.PositioningPasses; pass++)
        {
            if (pass % 2 == 0)
            {
                for (var r = 1; r < graph.Layers.Length; r++)
                {
                    Place(graph.Layers[r], graph.Up, graph.RealCount, breadth, gap, x);
                }
            }
            else
            {
                for (var r = graph.Layers.Length - 2; r >= 0; r--)
                {
                    Place(graph.Layers[r], graph.Down, graph.RealCount, breadth, gap, x);
                }
            }
        }
        return x;
    }

    /// <summary>
    /// Moves the nodes of one rank as close as possible to the centers of their neighbours in the reference rank,
    /// keeping their order and the gap: with <c>s_i</c> the packed offset of node i, the free coordinate
    /// <c>y_i = x_i − s_i</c> must not decrease, so weighted pool-adjacent-violators gives the least-squares answer.
    /// </summary>
    private static void Place(List<int> layer, List<int>[] neighbours, int realCount, double[] breadth, double gap, double[] x)
    {
        var count = layer.Count;
        if (count == 0)
        {
            return;
        }
        var targets = new double[count];
        var weights = new double[count];
        var offsets = new double[count];
        for (var i = 0; i < count; i++)
        {
            var v = layer[i];
            offsets[i] = i == 0 ? 0 : offsets[i - 1] + breadth[layer[i - 1]] + gap;
            var adjacent = neighbours[v];
            double center;
            if (adjacent.Count > 0)
            {
                center = 0;
                foreach (var u in adjacent)
                {
                    center += x[u] + breadth[u] / 2;
                }
                center /= adjacent.Count;
                weights[i] = adjacent.Count * (v >= realCount ? DummyWeight : 1);
            }
            else
            {
                center = x[v] + breadth[v] / 2;
                weights[i] = LooseWeight;
            }
            targets[i] = center - breadth[v] / 2 - offsets[i];
        }

        // Pool adjacent violators: blocks of (weight, weighted sum, length).
        var blockWeight = new double[count];
        var blockSum = new double[count];
        var blockLength = new int[count];
        var blocks = 0;
        for (var i = 0; i < count; i++)
        {
            blockWeight[blocks] = weights[i];
            blockSum[blocks] = weights[i] * targets[i];
            blockLength[blocks] = 1;
            blocks++;
            while (blocks > 1 && blockSum[blocks - 2] / blockWeight[blocks - 2] > blockSum[blocks - 1] / blockWeight[blocks - 1])
            {
                blockWeight[blocks - 2] += blockWeight[blocks - 1];
                blockSum[blocks - 2] += blockSum[blocks - 1];
                blockLength[blocks - 2] += blockLength[blocks - 1];
                blocks--;
            }
        }
        var index = 0;
        for (var b = 0; b < blocks; b++)
        {
            var value = blockSum[b] / blockWeight[b];
            for (var k = 0; k < blockLength[b]; k++, index++)
            {
                x[layer[index]] = value + offsets[index];
            }
        }
    }

    /// <summary>The ranked graph with placeholder nodes on long edges; nodes at or past <see cref="RealCount"/> are placeholders.</summary>
    private sealed class LayeredGraph
    {
        public readonly int RealCount;
        public readonly List<int>[] Layers;
        public readonly List<int>[] Up;
        public readonly List<int>[] Down;
        private readonly int[] _position;

        public LayeredGraph(int realCount, int[] ranks, int rankCount, List<(int From, int To)> edges)
        {
            RealCount = realCount;
            var nodeRanks = new List<int>(ranks);
            var links = new List<(int, int)>();
            foreach (var (from, to) in edges)
            {
                var previous = from;
                for (var r = ranks[from] + 1; r < ranks[to]; r++)
                {
                    var dummy = nodeRanks.Count;
                    nodeRanks.Add(r);
                    links.Add((previous, dummy));
                    previous = dummy;
                }
                links.Add((previous, to));
            }

            var count = nodeRanks.Count;
            Up = new List<int>[count];
            Down = new List<int>[count];
            for (var v = 0; v < count; v++)
            {
                Up[v] = [];
                Down[v] = [];
            }
            foreach (var (u, v) in links)
            {
                Down[u].Add(v);
                Up[v].Add(u);
            }
            Layers = new List<int>[rankCount];
            for (var r = 0; r < rankCount; r++)
            {
                Layers[r] = [];
            }
            // Input order: real nodes by index, then placeholders as their edges were added.
            for (var v = 0; v < count; v++)
            {
                Layers[nodeRanks[v]].Add(v);
            }
            _position = new int[count];
            UpdatePositions();
        }

        public int Count => _position.Length;

        /// <summary>
        /// Sorts one rank by the mean position of each node's neighbours in the adjacent rank; nodes without neighbours
        /// there keep their slot, and ties keep the current order.
        /// </summary>
        public void Reorder(int rank, List<int>[] neighbours)
        {
            var layer = Layers[rank];
            var movable = new List<(double Key, int Position, int Node)>();
            foreach (var v in layer)
            {
                var adjacent = neighbours[v];
                if (adjacent.Count == 0)
                {
                    continue;
                }
                var sum = 0.0;
                foreach (var u in adjacent)
                {
                    sum += _position[u];
                }
                movable.Add((sum / adjacent.Count, _position[v], v));
            }
            movable.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Position.CompareTo(b.Position));
            var next = 0;
            for (var i = 0; i < layer.Count; i++)
            {
                if (neighbours[layer[i]].Count > 0)
                {
                    layer[i] = movable[next++].Node;
                }
            }
            for (var i = 0; i < layer.Count; i++)
            {
                _position[layer[i]] = i;
            }
        }

        /// <summary>Crossings between every pair of adjacent ranks (inversions counted with a Fenwick tree).</summary>
        public int CountCrossings()
        {
            var total = 0;
            var pairs = new List<(int Upper, int Lower)>();
            for (var r = 0; r + 1 < Layers.Length; r++)
            {
                pairs.Clear();
                foreach (var u in Layers[r])
                {
                    foreach (var v in Down[u])
                    {
                        pairs.Add((_position[u], _position[v]));
                    }
                }
                pairs.Sort();
                var size = Layers[r + 1].Count;
                var tree = new int[size + 1];
                var inserted = 0;
                foreach (var (_, lower) in pairs)
                {
                    // Earlier edges (smaller upper end) whose lower end lies further right cross this one.
                    var notGreater = 0;
                    for (var i = lower + 1; i > 0; i -= i & -i)
                    {
                        notGreater += tree[i];
                    }
                    total += inserted - notGreater;
                    for (var i = lower + 1; i <= size; i += i & -i)
                    {
                        tree[i]++;
                    }
                    inserted++;
                }
            }
            return total;
        }

        public int[][] SnapshotOrder()
        {
            var snapshot = new int[Layers.Length][];
            for (var r = 0; r < Layers.Length; r++)
            {
                snapshot[r] = Layers[r].ToArray();
            }
            return snapshot;
        }

        public void RestoreOrder(int[][] snapshot)
        {
            for (var r = 0; r < Layers.Length; r++)
            {
                Layers[r].Clear();
                Layers[r].AddRange(snapshot[r]);
            }
            UpdatePositions();
        }

        private void UpdatePositions()
        {
            foreach (var layer in Layers)
            {
                for (var i = 0; i < layer.Count; i++)
                {
                    _position[layer[i]] = i;
                }
            }
        }
    }
}
