namespace TKCode.Arc;

public partial class DockPane
{
    /// <summary>
    /// Where the pane was docked before it floated (or was auto-hidden), so <see cref="DockLayout.Dock"/> can bring it back:
    /// its group and tab index, and, for when the group has gone since, the neighbour it sat beside with its share.
    /// </summary>
    internal DockPlacement? LastDockedPlacement { get; set; }
}

/// <summary>
/// A remembered docked place of a pane (spec 4.3, "path + share"). The nodes are held weakly: a layout that dropped them
/// must not be kept alive by a pane that once sat there.
/// </summary>
internal sealed class DockPlacement
{
    public DockPlacement(DockGroup group, int index, DockNode? sibling, DockPosition side, double share, bool inDocumentArea)
    {
        Group = new WeakReference<DockGroup>(group);
        Index = index;
        Sibling = sibling is null ? null : new WeakReference<DockNode>(sibling);
        Side = side;
        Share = share;
        InDocumentArea = inDocumentArea;
    }

    /// <summary>The group the pane was in.</summary>
    public WeakReference<DockGroup> Group { get; }

    /// <summary>The pane's tab index in that group.</summary>
    public int Index { get; }

    /// <summary>The node next to the group in its parent split (used when the group has gone).</summary>
    public WeakReference<DockNode>? Sibling { get; }

    /// <summary>Which side of <see cref="Sibling"/> the group was on.</summary>
    public DockPosition Side { get; }

    /// <summary>The group's share of the room it and <see cref="Sibling"/> had together (0..1).</summary>
    public double Share { get; }

    /// <summary>Whether the group was inside the document area.</summary>
    public bool InDocumentArea { get; }
}
