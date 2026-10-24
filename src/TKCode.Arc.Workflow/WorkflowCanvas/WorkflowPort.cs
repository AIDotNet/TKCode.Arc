using System.Threading;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Which side of a step a <see cref="WorkflowPort"/> is on.</summary>
public enum WorkflowPortDirection
{
    /// <summary>Data comes in (the card's top, or its left side when the workflow runs left to right).</summary>
    Input,

    /// <summary>Data goes out (the card's bottom, or its right side).</summary>
    Output
}

/// <summary>
/// A named connection point of a <see cref="WorkflowNode"/>, such as the "Yes" and "No" outputs of a condition. A step
/// without ports of a direction has one implicit port there (an edge's port is then null).
/// </summary>
public sealed class WorkflowPort : AvaloniaObject
{
    private static long s_nextId;

    /// <summary>A short name shown next to the port, such as "Yes"; null shows none.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<WorkflowPort, string?>(nameof(Label));

    /// <summary>
    /// Your name for the kind of data the port carries; two ports with a data type connect only when the types are the
    /// same (null connects to anything).
    /// </summary>
    public static readonly StyledProperty<string?> DataTypeProperty =
        AvaloniaProperty.Register<WorkflowPort, string?>(nameof(DataType));

    /// <summary>How many edges the port takes (default no limit).</summary>
    public static readonly StyledProperty<int> MaxConnectionsProperty =
        AvaloniaProperty.Register<WorkflowPort, int>(nameof(MaxConnections), int.MaxValue);

    /// <summary>Creates a port.</summary>
    /// <param name="direction">Input or output.</param>
    /// <param name="label">A short name shown next to it; null shows none.</param>
    /// <param name="id">A stable id; null generates one.</param>
    public WorkflowPort(WorkflowPortDirection direction, string? label = null, string? id = null)
    {
        Direction = direction;
        Label = label;
        Id = string.IsNullOrEmpty(id) ? "port-" + Interlocked.Increment(ref s_nextId) : id;
    }

    /// <summary>The port's stable id.</summary>
    public string Id { get; }

    /// <summary>Input or output.</summary>
    public WorkflowPortDirection Direction { get; }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="DataTypeProperty"/>
    public string? DataType
    {
        get => GetValue(DataTypeProperty);
        set => SetValue(DataTypeProperty, value);
    }

    /// <inheritdoc cref="MaxConnectionsProperty"/>
    public int MaxConnections
    {
        get => GetValue(MaxConnectionsProperty);
        set => SetValue(MaxConnectionsProperty, value);
    }

    /// <inheritdoc />
    public override string ToString() => Label is { Length: > 0 } label ? $"{Id} ({label})" : Id;
}
