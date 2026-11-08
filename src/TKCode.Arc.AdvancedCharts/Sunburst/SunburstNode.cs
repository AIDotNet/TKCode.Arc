using Avalonia.Collections;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>A node of a <see cref="Sunburst"/>: a leaf with a value, or a branch whose value is the sum of its children.</summary>
public sealed class SunburstNode
{
    /// <summary>Creates an empty node (for XAML).</summary>
    public SunburstNode()
    {
    }

    /// <summary>Creates a node.</summary>
    public SunburstNode(string id, string label, double? value = null, IEnumerable<SunburstNode>? children = null)
    {
        Id = id;
        Label = label;
        Value = value;
        if (children is not null)
        {
            Children.AddRange(children);
        }
    }

    /// <summary>Stable identity, unique in the tree: a segment that survives a data change moves to its new angles.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Segment, legend and breadcrumb label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Leaf value (ignored for branches, whose value is the sum of their children).</summary>
    public double? Value { get; set; }

    /// <summary>Child nodes.</summary>
    [Content]
    public AvaloniaList<SunburstNode> Children { get; } = new();
}

/// <summary>What moved the centre of a <see cref="Sunburst"/>.</summary>
public enum SunburstFocusChangeSource
{
    /// <summary><see cref="Sunburst.FocusedId"/> was set from code or a binding.</summary>
    Code,

    /// <summary>A segment was clicked or tapped, or the keyboard moved through the rings.</summary>
    Segment,

    /// <summary>A breadcrumb was chosen.</summary>
    Breadcrumb,

    /// <summary>A legend row was chosen.</summary>
    Legend,

    /// <summary>The centre was clicked or tapped, or Escape or Backspace zoomed out.</summary>
    Center
}

/// <summary>Arguments of <see cref="Sunburst.FocusedIdChanged"/>.</summary>
public sealed class SunburstFocusedIdChangedEventArgs : EventArgs
{
    internal SunburstFocusedIdChangedEventArgs(string id, string previousId, SunburstNode node, SunburstFocusChangeSource changeSource)
    {
        Id = id;
        PreviousId = previousId;
        Node = node;
        ChangeSource = changeSource;
    }

    /// <summary>Id of the node now at the centre (the root's own id at the root).</summary>
    public string Id { get; }

    /// <summary>Id of the node that was at the centre.</summary>
    public string PreviousId { get; }

    /// <summary>The node now at the centre.</summary>
    public SunburstNode Node { get; }

    /// <summary>What moved the centre.</summary>
    public SunburstFocusChangeSource ChangeSource { get; }
}

/// <summary>How a segment's label sits in its arc.</summary>
internal enum SunburstLabelFit
{
    None,
    Tangent,
    Radial
}

/// <summary>A node with its resolved value, place in the tree, position in the whole circle and draw caches.</summary>
internal sealed class SunburstEntry
{
    public required string Id;
    public required string Label;
    public required SunburstNode Node;
    public required string[] Path;
    public SunburstEntry? Parent;
    public readonly List<SunburstEntry> Children = new();
    public int Index;
    public int Last;
    public int Depth;
    public int Height;
    public int Branch = -1;
    public double Value;

    /// <summary>Turns of the whole circle (0 to 1) when the root is at the centre.</summary>
    public double Start;
    public double End;

    /// <summary>How much of the branch hue this node takes.</summary>
    public double Tint;

    public StreamGeometry? Geometry;
    public (double Radius, double T0, double T1, double R0, double R1) GeometryKey;
    public Color Fill;
    public TextLayout? Text;
    public SunburstLabelFit Fit;

    /// <summary>A node the view can open: at least one child has a value.</summary>
    public bool IsBranch => Height > 0;

    public bool IsUnder(SunburstEntry ancestor) => Index >= ancestor.Index && Index <= ancestor.Last;

    public SunburstEntry? FirstVisibleChild()
    {
        foreach (var child in Children)
        {
            if (child.Value > 0)
            {
                return child;
            }
        }
        return null;
    }
}

/// <summary>A flattened tree in depth-first order, resolved once per data change.</summary>
internal sealed class SunburstTree
{
    private const int MaxDepth = 64;

    private SunburstTree(SunburstEntry root, List<SunburstEntry> nodes, Dictionary<string, SunburstEntry> byId)
    {
        Root = root;
        Nodes = nodes;
        ById = byId;
    }

    public SunburstEntry Root { get; }

    public List<SunburstEntry> Nodes { get; }

    public Dictionary<string, SunburstEntry> ById { get; }

    public static SunburstTree Build(SunburstNode root)
    {
        var nodes = new List<SunburstEntry>();
        var byId = new Dictionary<string, SunburstEntry>(StringComparer.Ordinal);
        var entry = Visit(root, null, 0, 0, [], nodes, byId)!;
        Place(entry, 0, 1);
        return new SunburstTree(entry, nodes, byId);
    }

    /// <summary>A node without an id gets one from its place in the tree; a repeated id is skipped, which also ends a cycle.</summary>
    private static SunburstEntry? Visit(SunburstNode node, SunburstEntry? parent, int depth, int position, string[] path,
                                        List<SunburstEntry> nodes, Dictionary<string, SunburstEntry> byId)
    {
        var id = string.IsNullOrEmpty(node.Id) ? $"{parent?.Id}/{position}" : node.Id;
        if (byId.ContainsKey(id))
        {
            return null;
        }
        var own = depth == 0 ? path : [.. path, node.Label];
        var entry = new SunburstEntry { Id = id, Label = node.Label, Node = node, Path = own, Parent = parent, Depth = depth, Index = nodes.Count };
        byId[id] = entry;
        nodes.Add(entry);
        if (depth < MaxDepth && node.Children.Count > 0)
        {
            var sum = 0.0;
            var height = 0;
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is not { } child || Visit(child, entry, depth + 1, i, own, nodes, byId) is not { } resolved)
                {
                    continue;
                }
                entry.Children.Add(resolved);
                sum += resolved.Value;
                if (resolved.Value > 0)
                {
                    height = Math.Max(height, resolved.Height + 1);
                }
            }
            entry.Value = sum;
            entry.Height = height;
        }
        else
        {
            entry.Value = node.Value is { } value && double.IsFinite(value) ? Math.Max(0, value) : 0;
        }
        entry.Last = nodes.Count - 1;
        return entry;
    }

    private static void Place(SunburstEntry node, double start, double end)
    {
        node.Start = start;
        node.End = end;
        var at = start;
        foreach (var child in node.Children)
        {
            var sweep = node.Value > 0 ? (end - start) * child.Value / node.Value : 0;
            Place(child, at, at + sweep);
            at += sweep;
        }
    }
}

/// <summary>Maps a tree onto rings around a centre. Every node has five values: start and end turn, inner and outer radius (fractions of the ring's radius) and opacity.</summary>
internal static class SunburstLayout
{
    public const int Slots = 5;

    /// <summary>Rings on screen: the requested depth, or fewer when the centre has fewer levels below it.</summary>
    public static int Rings(SunburstEntry center, int depth) => Math.Max(1, Math.Min(Math.Max(1, depth), center.Height));

    /// <summary>
    /// Writes where every node belongs with <paramref name="center"/> in the middle. Nodes inside the centre's arc spread
    /// to the whole circle and the rest close up against its edges, so a zoom is one continuous move for every arc.
    /// </summary>
    public static void Compute(SunburstTree tree, SunburstEntry center, int depth, double hole, double[] output)
    {
        var rings = Rings(center, depth);
        var width = (1 - hole) / rings;
        var sweep = center.End - center.Start;
        for (var i = 0; i < tree.Nodes.Count; i++)
        {
            var node = tree.Nodes[i];
            var at = i * Slots;
            output[at] = sweep > 0 ? Math.Clamp((node.Start - center.Start) / sweep, 0, 1) : 0;
            output[at + 1] = sweep > 0 ? Math.Clamp((node.End - center.Start) / sweep, 0, 1) : 0;
            var level = node.Depth - center.Depth;
            // Levels that left through the centre close on the hole; levels beyond the last ring close on the outer edge.
            var ring = Math.Clamp(level, 0, rings + 1);
            output[at + 2] = ring == 0 ? hole : ring > rings ? 1 : hole + (ring - 1) * width;
            output[at + 3] = ring == 0 ? hole : ring > rings ? 1 : hole + ring * width;
            output[at + 4] = level >= 1 && level <= rings && node.IsUnder(center) && node.Value > 0 ? 1 : 0;
        }
    }

    /// <summary>Where a label of the given size fits: along the arc, across the ring, or nowhere.</summary>
    public static SunburstLabelFit Fit(double width, double height, double inner, double outer, double turns)
    {
        const double pad = 3;
        if (width <= 0 || height <= 0 || turns <= 0 || outer <= inner)
        {
            return SunburstLabelFit.None;
        }
        var middle = (inner + outer) / 2;
        var half = turns * Math.PI;
        var lowEdge = middle - height / 2;
        // Along the arc: the straight line of text must stay inside the curved band and between the sector's sides.
        if (lowEdge >= inner + pad && lowEdge > 0 &&
            Math.Sqrt(Math.Pow(middle + height / 2, 2) + width * width / 4) <= outer - pad &&
            Math.Atan(width / 2 / lowEdge) <= half - pad / middle)
        {
            return SunburstLabelFit.Tangent;
        }
        // Across the ring: the text must stay inside the band and clear the narrow inner end of the sector.
        var near = middle - width / 2;
        var inBand = near >= inner + pad && middle + width / 2 <= outer - pad;
        return inBand && (half >= Math.PI / 2 || near * Math.Tan(half) >= height / 2 + pad) ? SunburstLabelFit.Radial : SunburstLabelFit.None;
    }

    /// <summary>Splits a formatted value around its first number so only the digits roll. False when there is no number that reads back to the same text.</summary>
    public static bool TryParseNumber(string text, out string prefix, out double value, out int decimals, out bool grouped, out string suffix)
    {
        prefix = suffix = string.Empty;
        value = 0;
        decimals = 0;
        grouped = false;
        var start = 0;
        while (start < text.Length && !char.IsAsciiDigit(text[start]))
        {
            start++;
        }
        if (start == text.Length)
        {
            return false;
        }
        var end = start;
        var whole = 0.0;
        var run = 0;
        var groups = false;
        while (end < text.Length)
        {
            var c = text[end];
            if (char.IsAsciiDigit(c))
            {
                whole = whole * 10 + (c - '0');
                run++;
                end++;
            }
            else if (c == ',' && (groups ? run == 3 : run is >= 1 and <= 3) && IsThreeDigits(text, end + 1))
            {
                groups = true;
                run = 0;
                end++;
            }
            else
            {
                break;
            }
        }
        if (groups && run != 3)
        {
            return false;
        }
        var digitsEnd = end;
        if (end + 1 < text.Length && text[end] == '.' && char.IsAsciiDigit(text[end + 1]))
        {
            end++;
            var scale = 0.1;
            while (end < text.Length && char.IsAsciiDigit(text[end]))
            {
                whole += (text[end] - '0') * scale;
                scale /= 10;
                decimals++;
                end++;
            }
        }
        // A leading zero (007) or more digits than a double holds would not read back from the number.
        if (text[start] == '0' && digitsEnd - start > 1 || end - start > 15)
        {
            return false;
        }
        prefix = text[..start];
        suffix = text[end..];
        value = decimals > 0 ? Math.Round(whole, decimals) : whole;
        grouped = groups;
        return true;
    }

    private static bool IsThreeDigits(string text, int at)
        => at + 2 < text.Length && char.IsAsciiDigit(text[at]) && char.IsAsciiDigit(text[at + 1]) && char.IsAsciiDigit(text[at + 2]);
}
