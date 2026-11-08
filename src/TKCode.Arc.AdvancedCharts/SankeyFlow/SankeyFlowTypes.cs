namespace TKCode.Arc;

/// <summary>One stage node of a <see cref="SankeyFlow"/>.</summary>
public sealed class SankeyFlowNode
{
    /// <summary>Creates an empty node (for XAML).</summary>
    public SankeyFlowNode()
    {
    }

    /// <summary>Creates a node.</summary>
    public SankeyFlowNode(string id, string label, int? column = null)
    {
        Id = id;
        Label = label;
        Column = column;
    }

    /// <summary>Stable identity: links refer to it, and a node that survives a data change morphs to its new place.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name drawn next to the node.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Zero-based column; null places the node one column after the furthest node that flows into it.</summary>
    public int? Column { get; set; }
}

/// <summary>One flow of a <see cref="SankeyFlow"/>, drawn as a ribbon from its source node to its target node.</summary>
public sealed class SankeyFlowLink
{
    /// <summary>Creates an empty link (for XAML).</summary>
    public SankeyFlowLink()
    {
    }

    /// <summary>Creates a link.</summary>
    public SankeyFlowLink(string source, string target, double value)
    {
        Source = source;
        Target = target;
        Value = value;
    }

    /// <summary>Id of the node the flow leaves.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Id of the node the flow enters.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Size of the flow; it sets the ribbon's thickness. Zero, negative and non-finite values are ignored.</summary>
    public double Value { get; set; }
}

/// <summary>Arguments of <see cref="SankeyFlow.ParticlesChanged"/>.</summary>
public sealed class SankeyFlowParticlesChangedEventArgs : EventArgs
{
    internal SankeyFlowParticlesChangedEventArgs(bool isEnabled) => IsEnabled = isEnabled;

    /// <summary>True when particles are now enabled.</summary>
    public bool IsEnabled { get; }
}
