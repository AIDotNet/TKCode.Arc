using Avalonia;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>A resolved node: its flows, value and column.</summary>
internal sealed class SankeyFlowModelNode
{
    public required int Index { get; init; }
    public required string Id { get; init; }
    public required string Label { get; init; }
    public int? ExplicitColumn { get; init; }
    public int Column { get; set; }
    public double In { get; set; }
    public double Out { get; set; }
    public double Seed { get; set; }
    public List<int> InLinks { get; } = new();
    public List<int> OutLinks { get; } = new();

    /// <summary>The node's size: the larger of what enters and what leaves.</summary>
    public double Value => Math.Max(In, Out);
}

/// <summary>A resolved link; <see cref="IsBack"/> marks the link that closes a cycle.</summary>
internal sealed class SankeyFlowModelLink
{
    public required int Index { get; init; }
    public required int Source { get; init; }
    public required int Target { get; init; }
    public required string Key { get; init; }
    public double Value { get; set; }
    public bool IsBack { get; set; }
}

/// <summary>The part of the flow that passes through a traced node or ribbon.</summary>
internal sealed class SankeyFlowTrace
{
    public SankeyFlowTrace(int nodes, int links)
    {
        NodeFlow = new double[nodes];
        NodeHop = new int[nodes];
        LinkFlow = new double[links];
        LinkHop = new int[links];
        LinkUpstream = new bool[links];
    }

    public double Amount { get; init; }
    public double[] NodeFlow { get; }
    public int[] NodeHop { get; }
    public double[] LinkFlow { get; }
    public int[] LinkHop { get; }
    public bool[] LinkUpstream { get; }
    public int MaxHop { get; set; } = 1;
}

/// <summary>
/// Resolves <see cref="SankeyFlowNode"/> and <see cref="SankeyFlowLink"/> items into a graph: unknown link ends become
/// nodes, repeated links add up, self loops and unusable values drop out, cycles are broken by
/// <see cref="LayeredGraphLayout"/> and columns follow the longest path. Pure and deterministic.
/// </summary>
internal sealed class SankeyFlowModel
{
    private readonly int[] _topological;

    private SankeyFlowModel(List<SankeyFlowModelNode> nodes, List<SankeyFlowModelLink> links, int[] topological, int columnCount, double total, int[][] columns)
    {
        Nodes = nodes;
        Links = links;
        _topological = topological;
        ColumnCount = columnCount;
        Total = total;
        Columns = columns;
    }

    public IReadOnlyList<SankeyFlowModelNode> Nodes { get; }
    public IReadOnlyList<SankeyFlowModelLink> Links { get; }
    public int ColumnCount { get; }

    /// <summary>What enters the journey: the outflow of every node that nothing flows into, cycles aside.</summary>
    public double Total { get; }

    /// <summary>Node indices per column, in their starting order.</summary>
    public int[][] Columns { get; }

    public bool IsEmpty => Links.Count == 0;

    public static SankeyFlowModel Build(IEnumerable<SankeyFlowNode>? nodes, IEnumerable<SankeyFlowLink>? links)
    {
        var resolved = new List<SankeyFlowModelNode>();
        var byId = new Dictionary<string, int>();
        if (nodes is not null)
        {
            foreach (var node in nodes)
            {
                if (node is null || string.IsNullOrEmpty(node.Id) || byId.ContainsKey(node.Id))
                {
                    continue;
                }
                byId[node.Id] = resolved.Count;
                resolved.Add(new SankeyFlowModelNode
                {
                    Index = resolved.Count,
                    Id = node.Id,
                    Label = string.IsNullOrEmpty(node.Label) ? node.Id : node.Label,
                    ExplicitColumn = node.Column is >= 0 ? node.Column : null
                });
            }
        }

        var flows = new List<SankeyFlowModelLink>();
        var byPair = new Dictionary<(int, int), SankeyFlowModelLink>();
        if (links is not null)
        {
            foreach (var link in links)
            {
                if (link is null || !double.IsFinite(link.Value) || link.Value <= 0 ||
                    string.IsNullOrEmpty(link.Source) || string.IsNullOrEmpty(link.Target) || link.Source == link.Target)
                {
                    continue;
                }
                var source = NodeOf(link.Source);
                var target = NodeOf(link.Target);
                if (byPair.TryGetValue((source, target), out var existing))
                {
                    existing.Value += link.Value;
                    continue;
                }
                var flow = new SankeyFlowModelLink { Index = flows.Count, Source = source, Target = target, Key = link.Source + "\u0001" + link.Target, Value = link.Value };
                byPair[(source, target)] = flow;
                flows.Add(flow);
            }
        }

        foreach (var flow in flows)
        {
            resolved[flow.Source].Out += flow.Value;
            resolved[flow.Source].OutLinks.Add(flow.Index);
            resolved[flow.Target].In += flow.Value;
            resolved[flow.Target].InLinks.Add(flow.Index);
        }

        // The layered layout breaks the cycles and gives a first order with few crossings.
        var sizes = new Size[resolved.Count];
        Array.Fill(sizes, new Size(1, 1));
        var edges = new LayeredGraphEdge[flows.Count];
        for (var i = 0; i < flows.Count; i++)
        {
            edges[i] = new LayeredGraphEdge(flows[i].Source, flows[i].Target);
        }
        var layered = LayeredGraphLayout.Arrange(sizes, edges, new LayeredGraphLayoutOptions
        {
            Direction = LayeredGraphDirection.LeftToRight, RankGap = 1, FanOutRankGap = 1, NodeGap = 1, OrderingSweeps = 6, PositioningPasses = 0
        });
        foreach (var reversed in layered.ReversedEdges)
        {
            flows[reversed].IsBack = true;
        }
        for (var i = 0; i < resolved.Count; i++)
        {
            resolved[i].Seed = layered.Orders[i];
        }

        var order = TopologicalOrder(resolved, flows);
        var columnCount = 0;
        foreach (var index in order)
        {
            var node = resolved[index];
            var column = 0;
            foreach (var l in node.InLinks)
            {
                if (!flows[l].IsBack)
                {
                    column = Math.Max(column, resolved[flows[l].Source].Column + 1);
                }
            }
            node.Column = node.ExplicitColumn ?? column;
            columnCount = Math.Max(columnCount, node.Column + 1);
        }

        var total = 0.0;
        foreach (var node in resolved)
        {
            if (!node.InLinks.Exists(l => !flows[l].IsBack))
            {
                total += node.Out;
            }
        }

        var columns = new int[columnCount][];
        for (var c = 0; c < columnCount; c++)
        {
            columns[c] = resolved.Where(n => n.Column == c).OrderBy(n => n.Seed).ThenBy(n => n.Index).Select(n => n.Index).ToArray();
        }
        return new SankeyFlowModel(resolved, flows, order, columnCount, total, columns);

        int NodeOf(string id)
        {
            if (byId.TryGetValue(id, out var index))
            {
                return index;
            }
            index = resolved.Count;
            byId[id] = index;
            resolved.Add(new SankeyFlowModelNode { Index = index, Id = id, Label = id });
            return index;
        }
    }

    private static int[] TopologicalOrder(List<SankeyFlowModelNode> nodes, List<SankeyFlowModelLink> links)
    {
        var pending = new int[nodes.Count];
        foreach (var link in links)
        {
            if (!link.IsBack)
            {
                pending[link.Target]++;
            }
        }
        var ready = new Queue<int>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (pending[i] == 0)
            {
                ready.Enqueue(i);
            }
        }
        var order = new int[nodes.Count];
        var count = 0;
        while (ready.TryDequeue(out var index))
        {
            order[count++] = index;
            foreach (var l in nodes[index].OutLinks)
            {
                if (!links[l].IsBack && --pending[links[l].Target] == 0)
                {
                    ready.Enqueue(links[l].Target);
                }
            }
        }
        return order;
    }

    /// <summary>Traces all of a node's flow up and down the journey.</summary>
    public SankeyFlowTrace TraceNode(int node)
    {
        var value = Nodes[node].Value;
        var trace = new SankeyFlowTrace(Nodes.Count, Links.Count) { Amount = value };
        trace.NodeFlow[node] = value;
        Propagate(trace, node, value, node, value);
        return trace;
    }

    /// <summary>Traces one ribbon's flow up and down the journey.</summary>
    public SankeyFlowTrace TraceLink(int link)
    {
        var flow = Links[link];
        var trace = new SankeyFlowTrace(Nodes.Count, Links.Count) { Amount = flow.Value };
        trace.LinkFlow[link] = flow.Value;
        trace.NodeFlow[flow.Source] = flow.Value;
        trace.NodeFlow[flow.Target] = flow.Value;
        if (!flow.IsBack)
        {
            Propagate(trace, flow.Target, flow.Value, flow.Source, flow.Value);
        }
        return trace;
    }

    // Each node passes on the fraction of itself that is traced, split by its links. Back links take their share but
    // stop there, so a cycle ends after one lap.
    private void Propagate(SankeyFlowTrace trace, int downFrom, double downAmount, int upFrom, double upAmount)
    {
        var down = new double[Nodes.Count];
        var up = new double[Nodes.Count];
        var downHop = new int[Nodes.Count];
        var upHop = new int[Nodes.Count];
        down[downFrom] = downAmount;
        up[upFrom] = upAmount;
        foreach (var index in _topological)
        {
            if (down[index] <= 0 || Nodes[index].Value <= 0)
            {
                continue;
            }
            var share = down[index] / Nodes[index].Value;
            foreach (var l in Nodes[index].OutLinks)
            {
                var link = Links[l];
                var flow = link.Value * share;
                var hop = downHop[index] + 1;
                Mark(trace, l, flow, hop, false);
                if (!link.IsBack)
                {
                    down[link.Target] += flow;
                    downHop[link.Target] = Math.Max(downHop[link.Target], hop);
                }
            }
        }
        for (var i = _topological.Length - 1; i >= 0; i--)
        {
            var index = _topological[i];
            if (up[index] <= 0 || Nodes[index].Value <= 0)
            {
                continue;
            }
            var share = up[index] / Nodes[index].Value;
            foreach (var l in Nodes[index].InLinks)
            {
                var link = Links[l];
                var flow = link.Value * share;
                var hop = upHop[index] + 1;
                Mark(trace, l, flow, hop, true);
                if (!link.IsBack)
                {
                    up[link.Source] += flow;
                    upHop[link.Source] = Math.Max(upHop[link.Source], hop);
                }
            }
        }
        for (var i = 0; i < Nodes.Count; i++)
        {
            trace.NodeFlow[i] = Math.Max(trace.NodeFlow[i], Math.Max(down[i], up[i]));
            trace.NodeHop[i] = Math.Max(downHop[i], upHop[i]);
            trace.MaxHop = Math.Max(trace.MaxHop, trace.NodeHop[i]);
        }
        foreach (var hop in trace.LinkHop)
        {
            trace.MaxHop = Math.Max(trace.MaxHop, hop);
        }
    }

    private static void Mark(SankeyFlowTrace trace, int link, double flow, int hop, bool upstream)
    {
        if (flow > trace.LinkFlow[link])
        {
            trace.LinkFlow[link] = flow;
            trace.LinkHop[link] = hop;
            trace.LinkUpstream[link] = upstream;
        }
    }
}
