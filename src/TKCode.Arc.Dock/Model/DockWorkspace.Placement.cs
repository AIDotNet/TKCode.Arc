using Avalonia;
using Avalonia.Layout;

namespace TKCode.Arc;

public sealed partial class DockWorkspace
{
    /// <summary>True when <paramref name="node"/> is part of this workspace's main tree (not a floating window).</summary>
    internal bool IsInMainTree(DockNode node) => ReferenceEquals(node.Workspace, this) && node.Float is null;

    /// <summary>
    /// The docked place of a pane in the main tree, for <see cref="Restore"/>: its group and index, and the neighbour the
    /// group sits beside with its share, for when the group is gone by then. Null when the pane is not docked in the main tree.
    /// </summary>
    internal DockPlacement? PlacementOf(DockPane pane)
    {
        if (pane.Group is not { } group || !IsInMainTree(group))
        {
            return null;
        }
        var index = group.Panes.IndexOf(pane);
        DockNode? sibling = null;
        var side = DockPosition.Left;
        var share = SplitShare;
        if (group.Parent is DockSplit split)
        {
            var at = split.Children.IndexOf(group);
            var horizontal = split.Orientation == Orientation.Horizontal;
            if (at > 0)
            {
                sibling = split.Children[at - 1];
                side = horizontal ? DockPosition.Right : DockPosition.Bottom;
            }
            else if (at + 1 < split.Children.Count)
            {
                sibling = split.Children[at + 1];
                side = horizontal ? DockPosition.Left : DockPosition.Top;
            }
            if (sibling is not null)
            {
                var total = Math.Max(0, group.Size) + Math.Max(0, sibling.Size);
                share = total > 0 ? Math.Max(0, group.Size) / total : SplitShare;
            }
        }
        return new DockPlacement(group, index, sibling, side, share, group.IsInDocumentArea);
    }

    /// <summary>
    /// Puts a pane back at a remembered place of the main tree: into its old group when that is still there, else beside the
    /// old neighbour with the old share, else (for a place in the document area) into the documents. False when none of
    /// those fits; the pane is then left where it was.
    /// </summary>
    internal bool Restore(DockPane pane, DockPlacement placement)
    {
        BeginUpdate();
        try
        {
            if (placement.Group.TryGetTarget(out var group) && IsInMainTree(group) && !ReferenceEquals(pane.Group, group)
                && Accepts(pane, group.IsInDocumentArea))
            {
                Detach(pane);
                group.Panes.Insert(Math.Clamp(placement.Index, 0, group.Panes.Count), pane);
                group.ActivePane = pane;
                return true;
            }
            if (placement.Sibling is { } weak && weak.TryGetTarget(out var sibling) && IsInMainTree(sibling)
                && Accepts(pane, sibling is not DockDocumentArea && sibling.IsInDocumentArea))
            {
                Detach(pane);
                InsertBeside(sibling, new DockGroup(pane), placement.Side, Math.Clamp(placement.Share, 0.05, 0.95));
                return true;
            }
            if (placement.InDocumentArea && Accepts(pane, intoDocuments: true))
            {
                Detach(pane);
                AddToDocuments(pane);
                return true;
            }
            return false;
        }
        finally
        {
            EndUpdate();
        }
    }

    private static bool Accepts(DockPane pane, bool intoDocuments)
        => pane.IsDocument ? intoDocuments : !intoDocuments || pane.CanDockAsDocument;

    /// <summary>
    /// Moves <paramref name="panes"/> (from wherever they are) into a new floating window at <paramref name="bounds"/>, in
    /// one group with <paramref name="active"/> selected.
    /// </summary>
    internal DockFloat FloatPanes(IReadOnlyList<DockPane> panes, Rect bounds, DockPane? active)
    {
        BeginUpdate();
        try
        {
            var group = new DockGroup();
            foreach (var pane in panes)
            {
                Detach(pane);
                group.Panes.Add(pane);
            }
            group.ActivePane = active ?? (panes.Count > 0 ? panes[0] : null);
            var dock = new DockFloat(group, bounds);
            Floats.Add(dock);
            return dock;
        }
        finally
        {
            EndUpdate();
        }
    }
}
