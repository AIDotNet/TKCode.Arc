using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What a person changed on a <see cref="WorkflowCanvas"/> (<see cref="WorkflowEditEventArgs.Kind"/>).</summary>
public enum WorkflowEditKind
{
    /// <summary>Steps were moved: dragged and let go, or moved with the keyboard and placed.</summary>
    Move,

    /// <summary>Steps or edges were deleted (a step's edges go with it).</summary>
    Delete,

    /// <summary>Two steps were connected.</summary>
    Connect,

    /// <summary>An edge was taken off its input (dropped on the empty canvas, or moved to another input).</summary>
    Disconnect,

    /// <summary>The last edit was undone.</summary>
    Undo,

    /// <summary>An undone edit was redone.</summary>
    Redo,

    /// <summary>Steps were pasted or duplicated (with the edges among them); they are selected.</summary>
    Paste
}

/// <summary>One edit a person made on a <see cref="WorkflowCanvas"/>, for <see cref="WorkflowCanvas.GraphEdited"/>.</summary>
public sealed class WorkflowEditEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WorkflowEditEventArgs(RoutedEvent routedEvent, WorkflowEditKind kind, IReadOnlyList<WorkflowNode> nodes)
        : this(routedEvent, kind, nodes, [])
    {
    }

    /// <summary>Creates the arguments with the edges the edit touched.</summary>
    public WorkflowEditEventArgs(RoutedEvent routedEvent, WorkflowEditKind kind, IReadOnlyList<WorkflowNode> nodes,
                                 IReadOnlyList<WorkflowEdge> edges)
        : base(routedEvent)
    {
        Kind = kind;
        Nodes = nodes;
        Edges = edges;
    }

    /// <summary>What changed.</summary>
    public WorkflowEditKind Kind { get; }

    /// <summary>
    /// The steps it changed: at their new positions after a move, already out of the workflow after a delete, the new
    /// steps after a paste.
    /// </summary>
    public IReadOnlyList<WorkflowNode> Nodes { get; }

    /// <summary>The edges it changed: added by a connect or paste, removed by a delete or disconnect.</summary>
    public IReadOnlyList<WorkflowEdge> Edges { get; }
}

/// <summary>A change of a <see cref="WorkflowCanvas"/>'s selection, for <see cref="WorkflowCanvas.SelectionChanged"/>.</summary>
public sealed class WorkflowSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WorkflowSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<WorkflowNode> added,
                                             IReadOnlyList<WorkflowNode> removed)
        : this(routedEvent, added, removed, [], [])
    {
    }

    /// <summary>Creates the arguments with the edges that changed too.</summary>
    public WorkflowSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<WorkflowNode> added,
                                             IReadOnlyList<WorkflowNode> removed, IReadOnlyList<WorkflowEdge> addedEdges,
                                             IReadOnlyList<WorkflowEdge> removedEdges)
        : base(routedEvent)
    {
        Added = added;
        Removed = removed;
        AddedEdges = addedEdges;
        RemovedEdges = removedEdges;
    }

    /// <summary>The steps that became selected.</summary>
    public IReadOnlyList<WorkflowNode> Added { get; }

    /// <summary>The steps that are no longer selected (including steps that left the workflow).</summary>
    public IReadOnlyList<WorkflowNode> Removed { get; }

    /// <summary>The edges that became selected.</summary>
    public IReadOnlyList<WorkflowEdge> AddedEdges { get; }

    /// <summary>The edges that are no longer selected.</summary>
    public IReadOnlyList<WorkflowEdge> RemovedEdges { get; }
}
