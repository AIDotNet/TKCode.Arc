using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace TKCode.Arc;

/// <summary>
/// Copied steps and the edges among them, apart from any workflow: what the clipboard carries. In the same process the
/// clipboard holds the fragment itself, so values that cannot be written as text (an icon) survive a paste; other
/// processes get <see cref="ToJson"/>.
/// </summary>
internal sealed class WorkflowFragment
{
    /// <summary>The clipboard formats: the fragment itself in this process, its JSON for other apps.</summary>
    public static readonly DataFormat<WorkflowFragment> InProcessFormat =
        DataFormat.CreateInProcessFormat<WorkflowFragment>("TKCode.Arc.WorkflowFragment");

    public static readonly DataFormat<string> JsonFormat = DataFormat.CreateStringApplicationFormat(FormatName);

    private const string FormatName = "tkcode-arc-workflow";

    private WorkflowFragment(List<WorkflowNode> nodes, List<WorkflowEdge> edges)
    {
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>Detached copies of the steps, at the places of the originals.</summary>
    public List<WorkflowNode> Nodes { get; }

    /// <summary>The edges among <see cref="Nodes"/>.</summary>
    public List<WorkflowEdge> Edges { get; }

    /// <summary>
    /// Copies <paramref name="nodes"/> and the <paramref name="edges"/> whose two steps are both among them; a step's run
    /// (state, result, time, error) is not copied.
    /// </summary>
    public static WorkflowFragment Copy(IReadOnlyList<WorkflowNode> nodes, IEnumerable<WorkflowEdge> edges)
    {
        var clones = new Dictionary<WorkflowNode, WorkflowNode>();
        foreach (var node in nodes)
        {
            clones[node] = Clone(node, node.Id);
        }
        return new WorkflowFragment(clones.Values.ToList(), CopyEdges(edges, clones));
    }

    /// <summary>New steps and edges from this fragment, with new ids that <paramref name="taken"/> does not hold.</summary>
    public (List<WorkflowNode> Nodes, List<WorkflowEdge> Edges) Instantiate(ISet<string> taken)
    {
        var clones = new Dictionary<WorkflowNode, WorkflowNode>();
        foreach (var node in Nodes)
        {
            var id = node.Id;
            for (var n = 2; taken.Contains(id); n++)
            {
                id = $"{node.Id}-{n}";
            }
            taken.Add(id);
            clones[node] = Clone(node, id);
        }
        return (clones.Values.ToList(), CopyEdges(Edges, clones));
    }

    /// <summary>The steps' rectangle in canvas units (a step without a size counts as a standard card, 300 × 60).</summary>
    public Rect Bounds
    {
        get
        {
            Rect? bounds = null;
            foreach (var node in Nodes)
            {
                var size = node.Size ?? (node.Appearance == WorkflowNodeAppearance.Compact ? new Size(200, 84) : new Size(300, 60));
                var rect = new Rect(WorkflowWorldPanel.PositionOf(node), size);
                bounds = bounds is { } b ? b.Union(rect) : rect;
            }
            return bounds ?? default;
        }
    }

    private static WorkflowNode Clone(WorkflowNode node, string id)
    {
        var copy = new WorkflowNode(id, node.Title)
        {
            Description = node.Description,
            Kind = node.Kind,
            // A control can sit in one card only; geometry and images are shared.
            Icon = node.Icon is Control ? null : node.Icon,
            Position = node.Position,
            Size = node.Size,
            Appearance = node.Appearance,
            HasDefaultInput = node.HasDefaultInput,
            IsLocked = node.IsLocked
        };
        foreach (var port in node.Inputs)
        {
            copy.Inputs.Add(ClonePort(port));
        }
        foreach (var port in node.Outputs)
        {
            copy.Outputs.Add(ClonePort(port));
        }
        return copy;
    }

    private static WorkflowPort ClonePort(WorkflowPort port)
        => new(port.Direction, port.Label) { DataType = port.DataType, MaxConnections = port.MaxConnections };

    private static List<WorkflowEdge> CopyEdges(IEnumerable<WorkflowEdge> edges, Dictionary<WorkflowNode, WorkflowNode> clones)
    {
        var copies = new List<WorkflowEdge>();
        foreach (var edge in edges)
        {
            if (clones.TryGetValue(edge.Source, out var source) && clones.TryGetValue(edge.Target, out var target))
            {
                copies.Add(new WorkflowEdge(source, PortOf(edge.Source.Outputs, edge.SourcePort, source.Outputs), target,
                    PortOf(edge.Target.Inputs, edge.TargetPort, target.Inputs)));
            }
        }
        return copies;
    }

    /// <summary>The port of the copy at the original port's place (null stays the implicit port).</summary>
    private static WorkflowPort? PortOf(IList<WorkflowPort> originals, WorkflowPort? port, IList<WorkflowPort> copies)
        => port is null ? null : copies[originals.IndexOf(port)];

    // JSON ----------------------------------------------------------------------------------------------------------------

    public string ToJson()
    {
        var data = new WorkflowClipboardData { Format = FormatName, Version = 1 };
        foreach (var node in Nodes)
        {
            data.Nodes.Add(new WorkflowClipboardNode
            {
                Id = node.Id,
                Kind = node.Kind,
                Title = node.Title,
                Description = node.Description,
                X = Finite(node.Position.X),
                Y = Finite(node.Position.Y),
                Width = node.Size?.Width,
                Height = node.Size?.Height,
                Appearance = node.Appearance,
                HasDefaultInput = node.HasDefaultInput ? null : false,
                IsLocked = node.IsLocked ? true : null,
                Inputs = Ports(node.Inputs),
                Outputs = Ports(node.Outputs)
            });
        }
        foreach (var edge in Edges)
        {
            data.Edges.Add(new WorkflowClipboardEdge
            {
                From = Nodes.IndexOf(edge.Source),
                Output = edge.SourcePort is { } output ? edge.Source.Outputs.IndexOf(output) : null,
                To = Nodes.IndexOf(edge.Target),
                Input = edge.TargetPort is { } input ? edge.Target.Inputs.IndexOf(input) : null
            });
        }
        return JsonSerializer.Serialize(data, WorkflowJsonContext.Default.WorkflowClipboardData);
    }

    /// <summary>The fragment in <paramref name="json"/>, or null when it is not a copied workflow (or is damaged).</summary>
    public static WorkflowFragment? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith('{'))
        {
            return null;
        }
        WorkflowClipboardData? data;
        try
        {
            data = JsonSerializer.Deserialize(json, WorkflowJsonContext.Default.WorkflowClipboardData);
        }
        catch (JsonException)
        {
            return null;
        }
        if (data is not { Format: FormatName, Version: 1 } || data.Nodes.Count == 0)
        {
            return null;
        }
        var nodes = new List<WorkflowNode>();
        foreach (var item in data.Nodes)
        {
            var node = new WorkflowNode(item.Id, item.Title ?? string.Empty)
            {
                Description = item.Description,
                Kind = item.Kind,
                Position = new Point(item.X ?? double.NaN, item.Y ?? double.NaN),
                Size = item is { Width: > 0, Height: > 0 } ? new Size(item.Width.Value, item.Height.Value) : null,
                Appearance = item.Appearance,
                HasDefaultInput = item.HasDefaultInput ?? true,
                IsLocked = item.IsLocked ?? false
            };
            foreach (var port in item.Inputs ?? [])
            {
                node.Inputs.Add(Port(WorkflowPortDirection.Input, port));
            }
            foreach (var port in item.Outputs ?? [])
            {
                node.Outputs.Add(Port(WorkflowPortDirection.Output, port));
            }
            nodes.Add(node);
        }
        var edges = new List<WorkflowEdge>();
        foreach (var item in data.Edges)
        {
            // Damaged references are skipped rather than failing the whole paste.
            if (item.From is not { } from || item.To is not { } to || from < 0 || from >= nodes.Count || to < 0 ||
                to >= nodes.Count || from == to)
            {
                continue;
            }
            var source = nodes[from];
            var target = nodes[to];
            if (item.Output is { } output && (output < 0 || output >= source.Outputs.Count) ||
                item.Input is { } input && (input < 0 || input >= target.Inputs.Count))
            {
                continue;
            }
            edges.Add(new WorkflowEdge(source, item.Output is { } o ? source.Outputs[o] : null, target,
                item.Input is { } i ? target.Inputs[i] : null));
        }
        return new WorkflowFragment(nodes, edges);
    }

    private static double? Finite(double value) => double.IsFinite(value) ? value : null;

    private static List<WorkflowClipboardPort>? Ports(IList<WorkflowPort> ports)
        => ports.Count == 0
            ? null
            : ports.Select(p => new WorkflowClipboardPort
            {
                Label = p.Label,
                DataType = p.DataType,
                MaxConnections = p.MaxConnections == int.MaxValue ? null : p.MaxConnections
            }).ToList();

    private static WorkflowPort Port(WorkflowPortDirection direction, WorkflowClipboardPort item)
        => new(direction, item.Label) { DataType = item.DataType, MaxConnections = item.MaxConnections ?? int.MaxValue };
}

/// <summary>The clipboard's JSON: <c>{"format":"tkcode-arc-workflow","version":1,"nodes":[…],"edges":[…]}</c>.</summary>
internal sealed class WorkflowClipboardData
{
    public string? Format { get; set; }

    public int Version { get; set; }

    public List<WorkflowClipboardNode> Nodes { get; set; } = [];

    public List<WorkflowClipboardEdge> Edges { get; set; } = [];
}

internal sealed class WorkflowClipboardNode
{
    public string? Id { get; set; }

    public string? Kind { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public double? X { get; set; }

    public double? Y { get; set; }

    public double? Width { get; set; }

    public double? Height { get; set; }

    public WorkflowNodeAppearance Appearance { get; set; }

    public bool? HasDefaultInput { get; set; }

    public bool? IsLocked { get; set; }

    public List<WorkflowClipboardPort>? Inputs { get; set; }

    public List<WorkflowClipboardPort>? Outputs { get; set; }
}

internal sealed class WorkflowClipboardPort
{
    public string? Label { get; set; }

    public string? DataType { get; set; }

    public int? MaxConnections { get; set; }
}

/// <summary>An edge between two steps of the fragment (indexes into its nodes, and into their outputs and inputs; null is the implicit port).</summary>
internal sealed class WorkflowClipboardEdge
{
    public int? From { get; set; }

    public int? Output { get; set; }

    public int? To { get; set; }

    public int? Input { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true,
    PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false)]
[JsonSerializable(typeof(WorkflowClipboardData))]
internal sealed partial class WorkflowJsonContext : JsonSerializerContext;
