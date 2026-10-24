using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// A workflow: its steps (<see cref="Nodes"/>) and the connections between them (<see cref="Edges"/>). Show it with a
/// <see cref="WorkflowCanvas"/>; change the collections or any step and the canvas follows. Removing a step removes its
/// edges; an edge whose steps are not in <see cref="Nodes"/> is refused.
/// </summary>
public sealed class WorkflowGraph : AvaloniaObject
{
    /// <summary>Whether a connection may close a loop (default false: people cannot connect a step to one before it).</summary>
    public static readonly StyledProperty<bool> AllowCyclesProperty =
        AvaloniaProperty.Register<WorkflowGraph, bool>(nameof(AllowCycles));

    /// <summary>Creates an empty workflow.</summary>
    public WorkflowGraph()
    {
        History = new WorkflowHistory(this);
        Nodes.CollectionChanged += OnNodesChanged;
        Edges.CollectionChanged += OnEdgesChanged;
    }

    /// <summary>The steps.</summary>
    public AvaloniaList<WorkflowNode> Nodes { get; } = new();

    /// <summary>The connections between steps.</summary>
    public AvaloniaList<WorkflowEdge> Edges { get; } = new();

    /// <summary>Undo and redo for the edits people make on a canvas showing this workflow.</summary>
    public WorkflowHistory History { get; }

    /// <inheritdoc cref="AllowCyclesProperty"/>
    public bool AllowCycles
    {
        get => GetValue(AllowCyclesProperty);
        set => SetValue(AllowCyclesProperty, value);
    }

    /// <summary>
    /// Connects an output of <paramref name="source"/> to an input of <paramref name="target"/> (null ports are the
    /// implicit ones) and returns the new edge. It does not check <see cref="CanConnect"/>.
    /// </summary>
    public WorkflowEdge Connect(WorkflowNode source, WorkflowPort? sourcePort, WorkflowNode target, WorkflowPort? targetPort)
    {
        var edge = new WorkflowEdge(source, sourcePort, target, targetPort);
        Edges.Add(edge);
        return edge;
    }

    /// <summary>
    /// Whether people may connect these ports (null ports are the implicit ones); when not, <paramref name="reason"/>
    /// says why in the current language. In order: a step to itself, ports of the wrong direction, an existing
    /// connection, a full port, different <see cref="WorkflowPort.DataType"/>s, and a loop unless
    /// <see cref="AllowCycles"/>.
    /// </summary>
    public bool CanConnect(WorkflowNode source, WorkflowPort? sourcePort, WorkflowNode target, WorkflowPort? targetPort,
                           out string? reason)
    {
        reason = null;
        WorkflowLangResourceKind? problem = null;
        if (ReferenceEquals(source, target))
        {
            problem = WorkflowLangResourceKind.ConnectSelf;
        }
        else if (!Nodes.Contains(source) || !Nodes.Contains(target) ||
                 (sourcePort is not null && !source.Outputs.Contains(sourcePort)) ||
                 (targetPort is not null && !target.Inputs.Contains(targetPort)) ||
                 (targetPort is null && !target.HasDefaultInput))
        {
            problem = WorkflowLangResourceKind.ConnectDirection;
        }
        else if (Edges.Any(e => e.Source == source && e.SourcePort == sourcePort && e.Target == target && e.TargetPort == targetPort))
        {
            problem = WorkflowLangResourceKind.ConnectDuplicate;
        }
        else if ((sourcePort is not null && Edges.Count(e => e.SourcePort == sourcePort) >= sourcePort.MaxConnections) ||
                 (targetPort is not null && Edges.Count(e => e.TargetPort == targetPort) >= targetPort.MaxConnections))
        {
            problem = WorkflowLangResourceKind.ConnectFull;
        }
        else if (sourcePort?.DataType is { } from && targetPort?.DataType is { } to && from != to)
        {
            problem = WorkflowLangResourceKind.ConnectType;
        }
        else if (!AllowCycles && Reaches(target, source))
        {
            problem = WorkflowLangResourceKind.ConnectCycle;
        }
        if (problem is { } kind)
        {
            reason = WorkflowText.Get(kind);
            return false;
        }
        return true;
    }

    /// <summary>Whether a path of edges leads from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private bool Reaches(WorkflowNode from, WorkflowNode to)
    {
        var seen = new HashSet<WorkflowNode> { from };
        var stack = new Stack<WorkflowNode>([from]);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (ReferenceEquals(node, to))
            {
                return true;
            }
            foreach (var edge in Edges)
            {
                if (ReferenceEquals(edge.Source, node) && seen.Add(edge.Target))
                {
                    stack.Push(edge.Target);
                }
            }
        }
        return false;
    }

    /// <summary>Connects <paramref name="source"/> to <paramref name="target"/> and returns the new edge.</summary>
    /// <exception cref="ArgumentException">A step is not part of this workflow.</exception>
    public WorkflowEdge Connect(WorkflowNode source, WorkflowNode target)
    {
        var edge = new WorkflowEdge(source, target);
        Edges.Add(edge);
        return edge;
    }

    /// <summary>The step with <paramref name="id"/>, or null.</summary>
    public WorkflowNode? FindNode(string id)
    {
        foreach (var node in Nodes)
        {
            if (node.Id == id)
            {
                return node;
            }
        }
        return null;
    }

    /// <summary>The edges that leave <paramref name="node"/>.</summary>
    public IEnumerable<WorkflowEdge> OutgoingEdges(WorkflowNode node) => Edges.Where(e => ReferenceEquals(e.Source, node));

    /// <summary>The edges that reach <paramref name="node"/>.</summary>
    public IEnumerable<WorkflowEdge> IncomingEdges(WorkflowNode node) => Edges.Where(e => ReferenceEquals(e.Target, node));

    /// <summary>
    /// Gets ready for a new run: every step becomes <see cref="WorkflowNodeState.Pending"/> without result, time or error,
    /// and every edge that shows the run by itself goes quiet.
    /// </summary>
    public void ResetRun()
    {
        foreach (var node in Nodes)
        {
            node.State = WorkflowNodeState.Pending;
            node.Result = null;
            node.Duration = null;
            node.Error = null;
        }
        foreach (var edge in Edges)
        {
            if (edge.State is WorkflowEdgeState.Active or WorkflowEdgeState.Skipped)
            {
                edge.State = WorkflowEdgeState.Auto;
            }
        }
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            return;
        }
        // Edges of removed steps go with them (a cleared list reports a reset without the old items).
        for (var i = Edges.Count - 1; i >= 0; i--)
        {
            var edge = Edges[i];
            if (!Nodes.Contains(edge.Source) || !Nodes.Contains(edge.Target))
            {
                Edges.RemoveAt(i);
            }
        }
    }

    private void OnEdgesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is null)
        {
            return;
        }
        foreach (WorkflowEdge edge in e.NewItems)
        {
            if (!Nodes.Contains(edge.Source) || !Nodes.Contains(edge.Target))
            {
                throw new ArgumentException($"The edge {edge} connects a step that is not in this workflow's Nodes.");
            }
        }
    }
}
