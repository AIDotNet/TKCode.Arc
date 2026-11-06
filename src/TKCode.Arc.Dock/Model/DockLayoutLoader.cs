using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Turns a parsed layout (<see cref="DockLayoutData"/>) into the workspace's tree. Pane ids are resolved by the caller
/// (the panes the workspace has held, then the host's resolver); unknown ids and repeats are skipped. Afterwards the
/// structure is tidied and the known panes the layout does not mention go back by their preference.
/// </summary>
internal static class DockLayoutLoader
{
    /// <param name="workspace">The workspace to fill; its current content is replaced.</param>
    /// <param name="data">The parsed layout.</param>
    /// <param name="resolve">Finds a pane by id for ids the workspace has never held; null skips the id.</param>
    /// <param name="clampFloat">Pulls a floating window's bounds back on screen; null keeps them.</param>
    /// <param name="edgeShare">The share of the layout a tool placed by its preference takes along its edge.</param>
    /// <returns>The pane named as active in the layout, if it was placed.</returns>
    public static DockPane? Apply(DockWorkspace workspace, DockLayoutData data, Func<string, DockPane?>? resolve,
        Func<Rect, Rect>? clampFloat, Func<DockPane, double>? edgeShare)
    {
        // Every pane that is or was in the workspace, the current ones first in their order.
        var known = new List<DockPane>();
        var byId = new Dictionary<string, DockPane>(StringComparer.Ordinal);
        foreach (var pane in workspace.AllPanes())
        {
            if (byId.TryAdd(pane.Id, pane))
            {
                known.Add(pane);
            }
        }
        foreach (var (id, pane) in workspace.KnownPanes)
        {
            if (byId.TryAdd(id, pane))
            {
                known.Add(pane);
            }
        }
        var closed = new HashSet<string>(data.Closed, StringComparer.Ordinal);
        var used = new HashSet<DockPane>();

        DockPane? Resolve(string id)
        {
            var pane = byId.GetValueOrDefault(id) ?? resolve?.Invoke(id);
            if (pane is null || pane.Container is not null || !used.Add(pane))
            {
                return null;
            }
            if (!string.Equals(pane.Id, id, StringComparison.Ordinal))
            {
                pane.Id = id;
            }
            return pane;
        }

        DockPane? active = null;
        workspace.BeginUpdate();
        try
        {
            workspace.Clear();
            workspace.Root = Build(data.Root, Resolve);
            foreach (var (side, ids) in data.Hidden)
            {
                var list = workspace.AutoHidden(side);
                foreach (var id in ids)
                {
                    if (Resolve(id) is { } pane)
                    {
                        list.Add(pane);
                    }
                }
            }
            foreach (var floating in data.Floats)
            {
                if (Build(floating.Root, Resolve) is not { } root)
                {
                    continue;
                }
                var bounds = clampFloat?.Invoke(floating.Bounds) ?? floating.Bounds;
                workspace.Floats.Add(new DockFloat(root, bounds) { IsMaximized = floating.Maximized });
            }
            workspace.RequestTidy();
        }
        finally
        {
            workspace.EndUpdate();
        }
        foreach (var pane in known)
        {
            if (!used.Contains(pane) && pane.Container is null && !closed.Contains(pane.Id) && workspace.AllPanes().All(p => p.Id != pane.Id))
            {
                workspace.PlaceByPreference(pane, edgeShare?.Invoke(pane) ?? DockWorkspace.EdgeShare);
            }
        }
        if (data.Active is { } activeId)
        {
            active = workspace.FindPane(activeId);
        }
        return active;
    }

    private static DockNode? Build(DockNodeData? data, Func<string, DockPane?> resolve)
    {
        switch (data)
        {
            case DockGroupData groupData:
            {
                var group = new DockGroup { Size = groupData.Size };
                foreach (var id in groupData.Panes)
                {
                    if (resolve(id) is { } pane)
                    {
                        group.Panes.Add(pane);
                    }
                }
                if (groupData.Active is { } activeId && group.Panes.FirstOrDefault(p => p.Id == activeId) is { } active)
                {
                    group.ActivePane = active;
                }
                return group;
            }
            case DockSplitData splitData:
            {
                var split = new DockSplit { Orientation = splitData.Orientation, Size = splitData.Size };
                foreach (var child in splitData.Children)
                {
                    if (Build(child, resolve) is { } node)
                    {
                        split.Children.Add(node);
                    }
                }
                return split;
            }
            case DockDocumentsData documents:
                return new DockDocumentArea { Size = documents.Size, Root = Build(documents.Root, resolve) };
            default:
                return null;
        }
    }
}
