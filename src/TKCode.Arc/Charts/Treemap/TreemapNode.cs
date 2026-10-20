using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>A node of a <see cref="Treemap"/>: a leaf with a value, or a branch whose value is the sum of its children.</summary>
public sealed class TreemapNode
{
    /// <summary>Creates an empty node (for XAML).</summary>
    public TreemapNode()
    {
    }

    /// <summary>Creates a node.</summary>
    public TreemapNode(string id, string label, double? value = null, double? colorValue = null, IEnumerable<TreemapNode>? children = null)
    {
        Id = id;
        Label = label;
        Value = value;
        ColorValue = colorValue;
        if (children is not null)
        {
            Children.AddRange(children);
        }
    }

    /// <summary>Stable identity, unique in the tree: a tile that survives a data change morphs to its new size.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Tile and breadcrumb label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Leaf size (ignored for branches, whose size is the sum of their children).</summary>
    public double? Value { get; set; }

    /// <summary>Optional second measure for shading, such as growth; a branch uses the size-weighted mean of its children.</summary>
    public double? ColorValue { get; set; }

    /// <summary>Child nodes.</summary>
    [Content]
    public AvaloniaList<TreemapNode> Children { get; } = new();

    /// <summary>The original data item when the node was read from <see cref="Treemap.ItemsSource"/>.</summary>
    public object? Source { get; internal set; }
}

/// <summary>Arguments of <see cref="Treemap.FocusedNodeChanged"/>.</summary>
public sealed class TreemapFocusedNodeChangedEventArgs : EventArgs
{
    internal TreemapFocusedNodeChangedEventArgs(string id, TreemapNode? node)
    {
        Id = id;
        Node = node;
    }

    /// <summary>Id of the node that now fills the view.</summary>
    public string Id { get; }

    /// <summary>The node that now fills the view.</summary>
    public TreemapNode? Node { get; }
}

/// <summary>A flattened node with its resolved value, color measure, depth and path.</summary>
internal sealed class TreemapFlat
{
    public required string Id;
    public required string Label;
    public double Value;
    public double? Color;
    public int Depth;
    public string? Parent;
    public readonly List<string> Children = new();
    public required string[] Path;
    public required TreemapNode Node;
}

/// <summary>Squarified layout (Bruls et al.), ported from Arc: rows along the short side, closed when the worst aspect ratio would grow.</summary>
internal static class TreemapLayout
{
    public const double Gap = 2;
    public const double Pad = 3;
    public const double Header = 22;

    public static Dictionary<string, TreemapFlat> Flatten(TreemapNode root)
    {
        var flat = new Dictionary<string, TreemapFlat>();
        Visit(root, 0, null, [], flat);
        return flat;
    }

    private static (double Value, double Weighted, double Colored) Visit(TreemapNode node, int depth, string? parent, string[] path,
                                                                         Dictionary<string, TreemapFlat> flat)
    {
        var value = 0.0;
        var weighted = 0.0;
        var colored = 0.0;
        var own = depth == 0 ? new[] { node.Label } : path.Append(node.Label).ToArray();
        var entry = new TreemapFlat { Id = node.Id, Label = node.Label, Depth = depth, Parent = parent, Path = own, Node = node };
        flat[node.Id] = entry;
        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                if (child is null || flat.ContainsKey(child.Id))
                {
                    continue;
                }
                entry.Children.Add(child.Id);
                var r = Visit(child, depth + 1, node.Id, depth == 0 ? [] : own, flat);
                value += r.Value;
                weighted += r.Weighted;
                colored += r.Colored;
            }
        }
        else
        {
            value = Math.Max(0, node.Value ?? 0);
            if (node.ColorValue is { } color && double.IsFinite(color))
            {
                weighted = color * value;
                colored = value;
            }
        }
        entry.Value = value;
        entry.Color = colored > 0 ? weighted / colored : null;
        return (value, weighted, colored);
    }

    public static void Squarify(IReadOnlyList<(string Id, double Value)> items, Rect rect, Dictionary<string, Rect> output)
    {
        var list = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).ToList();
        var total = list.Sum(i => i.Value);
        if (total <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            foreach (var item in items)
            {
                output[item.Id] = new Rect(rect.X, rect.Y, 0, 0);
            }
            return;
        }
        var scale = rect.Width * rect.Height / total;
        double x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;
        var i = 0;
        static double Worst(List<double> row, double side)
        {
            var sum = row.Sum();
            var max = row.Max();
            var min = row.Min();
            return Math.Max(side * side * max / (sum * sum), sum * sum / (side * side * min));
        }
        while (i < list.Count)
        {
            var side = Math.Min(w, h);
            var row = new List<double> { list[i].Value * scale };
            var j = i + 1;
            while (j < list.Count)
            {
                var next = new List<double>(row) { list[j].Value * scale };
                if (Worst(next, side) > Worst(row, side))
                {
                    break;
                }
                row.Add(list[j].Value * scale);
                j++;
            }
            var sum = row.Sum();
            if (w >= h)
            {
                var cw = sum / h;
                var cy = y;
                for (var k = i; k < j; k++)
                {
                    var th = list[k].Value * scale / cw;
                    output[list[k].Id] = new Rect(x, cy, cw, th);
                    cy += th;
                }
                x += cw;
                w -= cw;
            }
            else
            {
                var rh = sum / w;
                var cx = x;
                for (var k = i; k < j; k++)
                {
                    var tw = list[k].Value * scale / rh;
                    output[list[k].Id] = new Rect(cx, y, tw, rh);
                    cx += tw;
                }
                y += rh;
                h -= rh;
            }
            i = j;
        }
        foreach (var item in items)
        {
            if (!output.ContainsKey(item.Id))
            {
                output[item.Id] = new Rect(rect.X, rect.Y, 0, 0);
            }
        }
    }

    /// <summary>Rects of every descendant when <paramref name="focus"/> fills the view: branches hold their children under a header.</summary>
    public static Dictionary<string, Rect> From(Dictionary<string, TreemapFlat> flat, string focus, Rect view)
    {
        var output = new Dictionary<string, Rect>();
        void Nest(string id, Rect rect, bool top)
        {
            var node = flat[id];
            if (node.Children.Count == 0)
            {
                return;
            }
            var room = top ? rect : Inset(rect, rect.Height > 48 && rect.Width > 64 ? Header : Pad, Pad);
            var placed = new Dictionary<string, Rect>();
            Squarify(node.Children.Select(c => (c, flat[c].Value)).ToList(), room, placed);
            foreach (var (child, r) in placed)
            {
                var gapped = new Rect(r.X + Gap / 2, r.Y + Gap / 2, Math.Max(0, r.Width - Gap), Math.Max(0, r.Height - Gap));
                output[child] = gapped;
                Nest(child, gapped, false);
            }
        }
        Nest(focus, view, true);
        return output;
    }

    private static Rect Inset(Rect r, double top, double side)
        => new(r.X + side, r.Y + top, Math.Max(0, r.Width - side * 2), Math.Max(0, r.Height - top - side));

    /// <summary>The affine map that carries rect <paramref name="from"/> onto rect <paramref name="to"/>, applied to <paramref name="r"/>.</summary>
    public static Rect Carry(Rect r, Rect from, Rect to)
    {
        var sx = to.Width / (from.Width == 0 ? 1 : from.Width);
        var sy = to.Height / (from.Height == 0 ? 1 : from.Height);
        return new Rect(to.X + (r.X - from.X) * sx, to.Y + (r.Y - from.Y) * sy, r.Width * sx, r.Height * sy);
    }
}
