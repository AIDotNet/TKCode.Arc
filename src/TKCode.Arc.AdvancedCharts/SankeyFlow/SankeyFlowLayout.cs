namespace TKCode.Arc;

/// <summary>A node's place: <see cref="Main"/> runs along the flow, <see cref="Cross"/> across it; both are leading edges.</summary>
internal struct SankeyNodePose
{
    public double Main;
    public double Cross;
    public double Extent;
    public double Thickness;

    public readonly double CrossCenter => Cross + Extent / 2;

    public static SankeyNodePose Mix(in SankeyNodePose a, in SankeyNodePose b, double t) => new()
    {
        Main = ChartsBMath.Mix(a.Main, b.Main, t),
        Cross = ChartsBMath.Mix(a.Cross, b.Cross, t),
        Extent = Math.Max(0, ChartsBMath.Mix(a.Extent, b.Extent, t)),
        Thickness = Math.Max(0, ChartsBMath.Mix(a.Thickness, b.Thickness, t))
    };
}

/// <summary>A ribbon's place: its center line runs from (M0, C0) to (M1, C1) with <see cref="Thick"/> across it.</summary>
internal struct SankeyLinkPose
{
    public double M0;
    public double M1;
    public double C0;
    public double C1;
    public double Thick;

    /// <summary>How far the control points reach along the flow: half the span, or a loop for a ribbon that runs backward.</summary>
    public readonly double Reach => M1 >= M0 ? (M1 - M0) / 2 : (M0 - M1) / 2 + 48;

    public readonly double MainAt(double t)
    {
        var u = 1 - t;
        var reach = Reach;
        return u * u * u * M0 + 3 * u * u * t * (M0 + reach) + 3 * u * t * t * (M1 - reach) + t * t * t * M1;
    }

    public readonly double CrossAt(double t) => C0 + (C1 - C0) * t * t * (3 - 2 * t);

    public static SankeyLinkPose Mix(in SankeyLinkPose a, in SankeyLinkPose b, double t) => new()
    {
        M0 = ChartsBMath.Mix(a.M0, b.M0, t),
        M1 = ChartsBMath.Mix(a.M1, b.M1, t),
        C0 = ChartsBMath.Mix(a.C0, b.C0, t),
        C1 = ChartsBMath.Mix(a.C1, b.C1, t),
        Thick = Math.Max(0, ChartsBMath.Mix(a.Thick, b.Thick, t))
    };
}

/// <summary>The input of <see cref="SankeyFlowLayout.Arrange"/>, in flow coordinates.</summary>
internal readonly record struct SankeyFlowLayoutOptions(double MainStart, double MainEnd, double CrossStart, double CrossEnd, double NodeThickness, double NodeGap);

/// <summary>The outcome of <see cref="SankeyFlowLayout.Arrange"/>; <see cref="Columns"/> are sorted across the flow.</summary>
internal sealed record SankeyFlowPlacement(SankeyNodePose[] Nodes, SankeyLinkPose[] Links, int[][] Columns, double Scale);

/// <summary>
/// Places nodes and ribbons: one value scale for the whole chart so every thickness is proportional, columns spread along
/// the flow, weighted relaxation across it, and each node's ribbons stacked in the order of what they connect to.
/// </summary>
internal static class SankeyFlowLayout
{
    private const int Passes = 8;
    private const double MinExtent = 2;

    public static SankeyFlowPlacement Arrange(SankeyFlowModel model, in SankeyFlowLayoutOptions options)
    {
        var nodes = new SankeyNodePose[model.Nodes.Count];
        var links = new SankeyLinkPose[model.Links.Count];
        var columns = new int[model.ColumnCount][];
        if (nodes.Length == 0)
        {
            return new SankeyFlowPlacement(nodes, links, columns, 0);
        }
        var span = Math.Max(1, options.CrossEnd - options.CrossStart);
        var gaps = new double[model.ColumnCount];
        var scale = double.PositiveInfinity;
        for (var c = 0; c < model.ColumnCount; c++)
        {
            var members = (int[])model.Columns[c].Clone();
            columns[c] = members;
            gaps[c] = members.Length > 1 ? Math.Min(options.NodeGap, span * 0.35 / (members.Length - 1)) : 0;
            var sum = 0.0;
            foreach (var n in members)
            {
                sum += model.Nodes[n].Value;
            }
            if (sum > 0)
            {
                scale = Math.Min(scale, (span - gaps[c] * (members.Length - 1)) / sum);
            }
        }
        if (double.IsInfinity(scale))
        {
            scale = 0;
        }

        var run = Math.Max(0, options.MainEnd - options.MainStart - options.NodeThickness);
        for (var c = 0; c < model.ColumnCount; c++)
        {
            var main = options.MainStart + (model.ColumnCount == 1 ? run / 2 : run * c / (model.ColumnCount - 1));
            var members = columns[c];
            var used = gaps[c] * Math.Max(0, members.Length - 1);
            foreach (var n in members)
            {
                used += Math.Max(MinExtent, model.Nodes[n].Value * scale);
            }
            var cross = options.CrossStart + Math.Max(0, (span - used) / 2);
            foreach (var n in members)
            {
                var extent = Math.Max(MinExtent, model.Nodes[n].Value * scale);
                nodes[n] = new SankeyNodePose { Main = main, Cross = cross, Extent = extent, Thickness = options.NodeThickness };
                cross += extent + gaps[c];
            }
        }

        // Relax toward the value-weighted center of the neighbours, from each side in turn.
        for (var pass = 0; pass < Passes; pass++)
        {
            var alpha = 0.85 * Math.Pow(0.82, pass);
            for (var c = 1; c < model.ColumnCount; c++)
            {
                Relax(model, nodes, columns[c], alpha, true);
                Resolve(nodes, columns[c], gaps[c], options.CrossStart, options.CrossEnd);
            }
            for (var c = model.ColumnCount - 2; c >= 0; c--)
            {
                Relax(model, nodes, columns[c], alpha, false);
                Resolve(nodes, columns[c], gaps[c], options.CrossStart, options.CrossEnd);
            }
        }

        // Each node's ribbons leave in the order of what they connect to, stacked and centered on the node.
        foreach (var node in model.Nodes)
        {
            var pose = nodes[node.Index];
            var cursor = StackStart(model, pose, node.OutLinks, scale);
            foreach (var l in node.OutLinks.OrderBy(l => nodes[model.Links[l].Target].CrossCenter).ThenBy(l => l))
            {
                var thick = model.Links[l].Value * scale;
                links[l].M0 = pose.Main + pose.Thickness;
                links[l].C0 = cursor + thick / 2;
                links[l].Thick = thick;
                cursor += thick;
            }
            cursor = StackStart(model, pose, node.InLinks, scale);
            foreach (var l in node.InLinks.OrderBy(l => nodes[model.Links[l].Source].CrossCenter).ThenBy(l => l))
            {
                var thick = model.Links[l].Value * scale;
                links[l].M1 = pose.Main;
                links[l].C1 = cursor + thick / 2;
                cursor += thick;
            }
        }
        return new SankeyFlowPlacement(nodes, links, columns, scale);
    }

    private static double StackStart(SankeyFlowModel model, in SankeyNodePose pose, List<int> members, double scale)
    {
        var used = 0.0;
        foreach (var l in members)
        {
            used += model.Links[l].Value * scale;
        }
        return pose.Cross + Math.Max(0, pose.Extent - used) / 2;
    }

    private static void Relax(SankeyFlowModel model, SankeyNodePose[] nodes, int[] members, double alpha, bool fromSources)
    {
        foreach (var n in members)
        {
            var node = model.Nodes[n];
            var weight = 0.0;
            var sum = 0.0;
            foreach (var l in fromSources ? node.InLinks : node.OutLinks)
            {
                var link = model.Links[l];
                if (link.IsBack)
                {
                    continue;
                }
                weight += link.Value;
                sum += nodes[fromSources ? link.Source : link.Target].CrossCenter * link.Value;
            }
            if (weight > 0)
            {
                nodes[n].Cross += (sum / weight - nodes[n].CrossCenter) * alpha;
            }
        }
    }

    private static void Resolve(SankeyNodePose[] nodes, int[] members, double gap, double start, double end)
    {
        // Stable insertion sort: nearly sorted, and equal positions keep their order.
        for (var i = 1; i < members.Length; i++)
        {
            var member = members[i];
            var center = nodes[member].CrossCenter;
            var j = i - 1;
            while (j >= 0 && nodes[members[j]].CrossCenter > center)
            {
                members[j + 1] = members[j];
                j--;
            }
            members[j + 1] = member;
        }
        var cursor = start;
        foreach (var n in members)
        {
            nodes[n].Cross = Math.Max(nodes[n].Cross, cursor);
            cursor = nodes[n].Cross + nodes[n].Extent + gap;
        }
        cursor = end;
        for (var i = members.Length - 1; i >= 0; i--)
        {
            var n = members[i];
            if (nodes[n].Cross + nodes[n].Extent <= cursor)
            {
                break;
            }
            nodes[n].Cross = cursor - nodes[n].Extent;
            cursor = nodes[n].Cross - gap;
        }
    }
}
