namespace TKCode.Arc;

public sealed partial class DockWorkspace
{
    /// <summary>
    /// Moves a docked pane of the main tree to the auto-hide strip of <paramref name="side"/>, remembering its group, its
    /// place among the tabs and its docked size in px (<paramref name="extent"/>) so <see cref="PinBack"/> can return it.
    /// </summary>
    internal bool AutoHideTo(DockPane pane, DockSide side, double extent)
    {
        if (!ReferenceEquals(pane.Workspace, this) || pane.Group is not { } group || group.Float is not null)
        {
            return false;
        }
        pane.AutoHidePlacement = new DockAutoHidePlacement(group, group.Panes.IndexOf(pane), side);
        pane.AutoHideExtent = extent;
        BeginUpdate();
        try
        {
            Detach(pane);
            AutoHidden(side).Add(pane);
        }
        finally
        {
            EndUpdate();
        }
        return true;
    }

    /// <summary>
    /// Pins an auto-hidden pane back: into the group it left, at its old place, when that group is still in the main tree;
    /// otherwise into a new group along its strip's edge taking <paramref name="edgeShare"/> of the layout.
    /// </summary>
    internal DockGroup? PinBack(DockPane pane, double edgeShare)
    {
        if (!ReferenceEquals(pane.Container, this) || pane.HiddenSide is not { } side)
        {
            return null;
        }
        var placement = pane.AutoHidePlacement;
        DockGroup group;
        BeginUpdate();
        try
        {
            Detach(pane);
            if (placement?.Group is { } old && ReferenceEquals(old.Workspace, this) && old.Float is null && old.Panes.Count > 0)
            {
                old.Panes.Insert(Math.Clamp(placement.Index, 0, old.Panes.Count), pane);
                old.ActivePane = pane;
                group = old;
            }
            else
            {
                group = new DockGroup(pane);
                InsertAtEdge(group, PositionOf(side), Math.Clamp(edgeShare, 0.05, 0.6));
            }
        }
        finally
        {
            EndUpdate();
        }
        pane.AutoHidePlacement = null;
        return group;
    }

    /// <summary>Drops a pane from the panes this workspace remembers (a pane made from a source whose item went away).</summary>
    internal void Forget(DockPane pane)
    {
        if (_known.TryGetValue(pane.Id, out var known) && ReferenceEquals(known, pane))
        {
            _known.Remove(pane.Id);
        }
    }
}

/// <summary>Where an auto-hidden pane came from: its group and tab index, and the strip it went to.</summary>
internal sealed record DockAutoHidePlacement(DockGroup Group, int Index, DockSide Side);
