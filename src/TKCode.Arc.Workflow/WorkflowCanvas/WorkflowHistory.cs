namespace TKCode.Arc;

/// <summary>
/// Undo and redo for the edits people make on a <see cref="WorkflowCanvas"/> showing the workflow: moving, deleting,
/// connecting, disconnecting and pasting steps. Changes your code makes to the <see cref="WorkflowGraph"/> are recorded
/// only inside <see cref="BeginGroup"/>. Keeps the last 100 edits.
/// </summary>
public sealed class WorkflowHistory
{
    private const int Limit = 100;

    private readonly WorkflowGraph _graph;
    private readonly LinkedList<WorkflowChange> _undo = new();
    private readonly Stack<WorkflowChange> _redo = new();
    private int _groupDepth;
    private WorkflowState? _groupStart;

    internal WorkflowHistory(WorkflowGraph graph) => _graph = graph;

    /// <summary>Raised when what can be undone or redone changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Whether edits are recorded (default true). Turn it off when your app keeps its own undo history.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether there is an edit to undo.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether there is an undone edit to redo.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>True while an undo or redo changes the workflow, so the change is not recorded again.</summary>
    internal bool IsApplying { get; private set; }

    /// <summary>Undoes the last edit; returns it, or null when there was none.</summary>
    internal WorkflowChange? UndoChange()
    {
        if (_undo.Last?.Value is not { } last)
        {
            return null;
        }
        _undo.RemoveLast();
        Apply(last.Undo);
        _redo.Push(last);
        Changed?.Invoke(this, EventArgs.Empty);
        return last;
    }

    /// <summary>Redoes the last undone edit; returns it, or null when there was none.</summary>
    internal WorkflowChange? RedoChange()
    {
        if (!_redo.TryPop(out var change))
        {
            return null;
        }
        Apply(change.Redo);
        _undo.AddLast(change);
        Changed?.Invoke(this, EventArgs.Empty);
        return change;
    }

    /// <summary>Undoes the last edit.</summary>
    public void Undo() => UndoChange();

    /// <summary>Redoes the last undone edit.</summary>
    public void Redo() => RedoChange();

    /// <summary>
    /// Records what your code changes until the returned object is disposed as one edit that people undo and redo at
    /// once: steps and edges added or removed, and steps moved. Groups nest (the outermost one records); a group that
    /// changed nothing records nothing.
    /// </summary>
    /// <example>
    /// <code>
    /// using (graph.History.BeginGroup())
    /// {
    ///     graph.Nodes.Add(step);
    ///     graph.Connect(source, step);
    /// }
    /// </code>
    /// </example>
    public IDisposable BeginGroup()
    {
        if (_groupDepth++ == 0)
        {
            _groupStart = WorkflowState.Capture(_graph);
        }
        return new Group(this);
    }

    private void EndGroup()
    {
        if (--_groupDepth > 0 || _groupStart is not { } start)
        {
            return;
        }
        _groupStart = null;
        var end = WorkflowState.Capture(_graph);
        if (!start.SameAs(end))
        {
            Record(new WorkflowStateChange(start, end));
        }
    }

    private sealed class Group(WorkflowHistory history) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                history.EndGroup();
            }
        }
    }

    /// <summary>Forgets every edit.</summary>
    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
        {
            return;
        }
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records an edit that was just made (ignored while undoing, or when off).</summary>
    internal void Record(WorkflowChange change)
    {
        if (!IsEnabled || IsApplying)
        {
            return;
        }
        _undo.AddLast(change);
        if (_undo.Count > Limit)
        {
            _undo.RemoveFirst();
        }
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(Action<WorkflowGraph> action)
    {
        IsApplying = true;
        try
        {
            action(_graph);
        }
        finally
        {
            IsApplying = false;
        }
    }
}

/// <summary>One recorded edit: how to undo and redo it, and the steps and edges it touched.</summary>
internal abstract class WorkflowChange
{
    public abstract IReadOnlyList<WorkflowNode> Nodes { get; }

    public abstract IReadOnlyList<WorkflowEdge> Edges { get; }

    public abstract void Undo(WorkflowGraph graph);

    public abstract void Redo(WorkflowGraph graph);
}

/// <summary>Steps moved from one set of positions to another.</summary>
internal sealed class WorkflowMoveChange(IReadOnlyList<WorkflowNode> nodes, IReadOnlyList<Avalonia.Point> from,
                                         IReadOnlyList<Avalonia.Point> to) : WorkflowChange
{
    public override IReadOnlyList<WorkflowNode> Nodes => nodes;

    public override IReadOnlyList<WorkflowEdge> Edges => [];

    public override void Undo(WorkflowGraph graph) => Set(from);

    public override void Redo(WorkflowGraph graph) => Set(to);

    private void Set(IReadOnlyList<Avalonia.Point> positions)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].Position = positions[i];
        }
    }
}

/// <summary>
/// Steps and edges removed (or, with <c>added</c>, added): their places in the lists are kept so undo puts them back
/// where they were.
/// </summary>
internal sealed class WorkflowRemoveChange : WorkflowChange
{
    private readonly (WorkflowNode Node, int Index)[] _nodes;
    private readonly (WorkflowEdge Edge, int Index)[] _edges;
    private readonly bool _added;

    private WorkflowRemoveChange((WorkflowNode, int)[] nodes, (WorkflowEdge, int)[] edges, bool added)
    {
        _nodes = nodes;
        _edges = edges;
        _added = added;
    }

    /// <summary>Records removing <paramref name="nodes"/> with their edges and <paramref name="edges"/>, before it happens.</summary>
    public static WorkflowRemoveChange Removing(WorkflowGraph graph, IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowEdge> edges)
    {
        var nodeSet = nodes.ToHashSet();
        var edgeSet = edges.ToHashSet();
        foreach (var edge in graph.Edges)
        {
            if (nodeSet.Contains(edge.Source) || nodeSet.Contains(edge.Target))
            {
                edgeSet.Add(edge);
            }
        }
        return new WorkflowRemoveChange(
            graph.Nodes.Select((n, i) => (n, i)).Where(p => nodeSet.Contains(p.n)).ToArray(),
            graph.Edges.Select((e, i) => (e, i)).Where(p => edgeSet.Contains(p.e)).ToArray(),
            added: false);
    }

    /// <summary>Records adding <paramref name="edge"/>, after it happened.</summary>
    public static WorkflowRemoveChange Adding(WorkflowGraph graph, WorkflowEdge edge)
        => new([], [(edge, graph.Edges.IndexOf(edge))], added: true);

    public override IReadOnlyList<WorkflowNode> Nodes => _nodes.Select(p => p.Node).ToList();

    public override IReadOnlyList<WorkflowEdge> Edges => _edges.Select(p => p.Edge).ToList();

    public override void Undo(WorkflowGraph graph)
    {
        if (_added)
        {
            Remove(graph);
        }
        else
        {
            Restore(graph);
        }
    }

    public override void Redo(WorkflowGraph graph)
    {
        if (_added)
        {
            Restore(graph);
        }
        else
        {
            Remove(graph);
        }
    }

    private void Remove(WorkflowGraph graph)
    {
        foreach (var (edge, _) in _edges)
        {
            graph.Edges.Remove(edge);
        }
        foreach (var (node, _) in _nodes)
        {
            graph.Nodes.Remove(node);
        }
    }

    /// <summary>Puts the steps back first (edges need their ends), each at its old place, lowest place first.</summary>
    private void Restore(WorkflowGraph graph)
    {
        foreach (var (node, index) in _nodes.OrderBy(p => p.Index))
        {
            graph.Nodes.Insert(Math.Min(index, graph.Nodes.Count), node);
        }
        foreach (var (edge, index) in _edges.OrderBy(p => p.Index))
        {
            graph.Edges.Insert(Math.Min(index, graph.Edges.Count), edge);
        }
    }
}

/// <summary>The steps, the edges and the steps' positions of a workflow at one moment.</summary>
internal sealed class WorkflowState
{
    private WorkflowState(WorkflowNode[] nodes, WorkflowEdge[] edges, Dictionary<WorkflowNode, Avalonia.Point> positions)
    {
        Nodes = nodes;
        Edges = edges;
        Positions = positions;
    }

    public WorkflowNode[] Nodes { get; }

    public WorkflowEdge[] Edges { get; }

    public Dictionary<WorkflowNode, Avalonia.Point> Positions { get; }

    public static WorkflowState Capture(WorkflowGraph graph)
        => new(graph.Nodes.ToArray(), graph.Edges.ToArray(), graph.Nodes.ToDictionary(n => n, n => n.Position));

    public bool SameAs(WorkflowState other)
        => Nodes.SequenceEqual(other.Nodes) && Edges.SequenceEqual(other.Edges) &&
           Positions.All(p => other.Positions.TryGetValue(p.Key, out var q) && SamePlace(p.Value, q));

    /// <summary>Equal places; two unplaced steps (NaN) count as the same.</summary>
    public static bool SamePlace(Avalonia.Point a, Avalonia.Point b)
        => (a.X == b.X || (double.IsNaN(a.X) && double.IsNaN(b.X))) && (a.Y == b.Y || (double.IsNaN(a.Y) && double.IsNaN(b.Y)));

    /// <summary>Puts <paramref name="graph"/> back in this state, touching only what differs.</summary>
    public void Restore(WorkflowGraph graph)
    {
        var edges = Edges.ToHashSet();
        var nodes = Nodes.ToHashSet();
        for (var i = graph.Edges.Count - 1; i >= 0; i--)
        {
            if (!edges.Contains(graph.Edges[i]))
            {
                graph.Edges.RemoveAt(i);
            }
        }
        for (var i = graph.Nodes.Count - 1; i >= 0; i--)
        {
            if (!nodes.Contains(graph.Nodes[i]))
            {
                graph.Nodes.RemoveAt(i);
            }
        }
        // Steps first: an edge needs both of its steps.
        Order(graph.Nodes, Nodes);
        Order(graph.Edges, Edges);
        foreach (var (node, position) in Positions)
        {
            if (!SamePlace(node.Position, position))
            {
                node.Position = position;
            }
        }
    }

    /// <summary>Makes <paramref name="list"/> hold <paramref name="items"/> in their order (it holds none of the others).</summary>
    private static void Order<T>(Avalonia.Collections.AvaloniaList<T> list, T[] items) where T : class
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (i < list.Count && ReferenceEquals(list[i], items[i]))
            {
                continue;
            }
            var at = list.IndexOf(items[i]);
            if (at >= 0)
            {
                list.RemoveAt(at);
            }
            list.Insert(i, items[i]);
        }
    }
}

/// <summary>What a <see cref="WorkflowHistory.BeginGroup"/> recorded: the workflow before and after it.</summary>
internal sealed class WorkflowStateChange(WorkflowState before, WorkflowState after) : WorkflowChange
{
    public override IReadOnlyList<WorkflowNode> Nodes
        => before.Nodes.Union(after.Nodes)
            .Where(n => !before.Positions.TryGetValue(n, out var a) || !after.Positions.TryGetValue(n, out var b) ||
                        !WorkflowState.SamePlace(a, b))
            .ToList();

    public override IReadOnlyList<WorkflowEdge> Edges => before.Edges.Except(after.Edges).Union(after.Edges.Except(before.Edges)).ToList();

    public override void Undo(WorkflowGraph graph) => before.Restore(graph);

    public override void Redo(WorkflowGraph graph) => after.Restore(graph);
}

/// <summary>Several edits undone and redone as one (a reconnect is a disconnect and a connect).</summary>
internal sealed class WorkflowCompositeChange(IReadOnlyList<WorkflowChange> changes) : WorkflowChange
{
    public override IReadOnlyList<WorkflowNode> Nodes => changes.SelectMany(c => c.Nodes).Distinct().ToList();

    public override IReadOnlyList<WorkflowEdge> Edges => changes.SelectMany(c => c.Edges).Distinct().ToList();

    public override void Undo(WorkflowGraph graph)
    {
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            changes[i].Undo(graph);
        }
    }

    public override void Redo(WorkflowGraph graph)
    {
        foreach (var change in changes)
        {
            change.Redo(graph);
        }
    }
}
