using System.Threading;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A connection from one step of a <see cref="WorkflowGraph"/> to the next: the data of a run flows from
/// <see cref="Source"/> to <see cref="Target"/>. The ends are fixed; to reconnect, remove the edge and add a new one.
/// </summary>
public sealed class WorkflowEdge : AvaloniaObject
{
    private static long s_nextId;

    /// <summary>
    /// How the edge shows the run. <see cref="WorkflowEdgeState.Auto"/> (the default) follows its two steps, so a run only
    /// needs to set the steps' states.
    /// </summary>
    public static readonly StyledProperty<WorkflowEdgeState> StateProperty =
        AvaloniaProperty.Register<WorkflowEdge, WorkflowEdgeState>(nameof(State));

    /// <summary>Connects <paramref name="source"/> to <paramref name="target"/> through their implicit ports.</summary>
    /// <param name="source">The step the data leaves.</param>
    /// <param name="target">The step the data reaches.</param>
    /// <param name="id">A stable id; null generates one.</param>
    public WorkflowEdge(WorkflowNode source, WorkflowNode target, string? id = null)
        : this(source, null, target, null, id)
    {
    }

    /// <summary>Connects an output of <paramref name="source"/> to an input of <paramref name="target"/>.</summary>
    /// <param name="source">The step the data leaves.</param>
    /// <param name="sourcePort">One of its <see cref="WorkflowNode.Outputs"/>, or null for its implicit output.</param>
    /// <param name="target">The step the data reaches.</param>
    /// <param name="targetPort">One of its <see cref="WorkflowNode.Inputs"/>, or null for its implicit input.</param>
    /// <param name="id">A stable id; null generates one.</param>
    /// <exception cref="ArgumentException">A port is not one of its step's ports of that direction.</exception>
    public WorkflowEdge(WorkflowNode source, WorkflowPort? sourcePort, WorkflowNode target, WorkflowPort? targetPort,
                        string? id = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (sourcePort is not null && !source.Outputs.Contains(sourcePort))
        {
            throw new ArgumentException($"{sourcePort} is not an output of {source}.", nameof(sourcePort));
        }
        if (targetPort is not null && !target.Inputs.Contains(targetPort))
        {
            throw new ArgumentException($"{targetPort} is not an input of {target}.", nameof(targetPort));
        }
        Source = source;
        SourcePort = sourcePort;
        Target = target;
        TargetPort = targetPort;
        Id = string.IsNullOrEmpty(id) ? "edge-" + Interlocked.Increment(ref s_nextId) : id;
    }

    /// <summary>The edge's stable id.</summary>
    public string Id { get; }

    /// <summary>The step the data leaves.</summary>
    public WorkflowNode Source { get; }

    /// <summary>The output the data leaves through; null is the source's implicit output.</summary>
    public WorkflowPort? SourcePort { get; }

    /// <summary>The step the data reaches.</summary>
    public WorkflowNode Target { get; }

    /// <summary>The input the data comes in through; null is the target's implicit input.</summary>
    public WorkflowPort? TargetPort { get; }

    /// <inheritdoc cref="StateProperty"/>
    public WorkflowEdgeState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Source.Id} → {Target.Id}";
}
