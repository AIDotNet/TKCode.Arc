using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

public partial class WorkflowCanvas
{
    // The fragment pasted last (its JSON) and how many times, so pasting it again cascades instead of stacking.
    private string? _lastPasted;
    private int _pasteCount;

    /// <summary>
    /// Copies the selected steps (or the focused one) and the edges among them to the clipboard; returns false when
    /// there was nothing to copy or no clipboard. Works on a read-only canvas too.
    /// </summary>
    public async Task<bool> CopySelectionAsync()
    {
        if (CopyTargets() is not { Count: > 0 } nodes || Graph is not { } graph)
        {
            return false;
        }
        var fragment = WorkflowFragment.Copy(nodes, graph.Edges);
        if (!await WriteClipboardAsync(fragment))
        {
            return false;
        }
        Announce(Count(WorkflowLangResourceKind.AnnounceCopiedOne, WorkflowLangResourceKind.AnnounceCopiedOther, nodes.Count));
        return true;
    }

    /// <summary>
    /// Copies the selected steps (or the focused one) to the clipboard and deletes them, except locked steps; returns
    /// false on a read-only canvas or when nothing was copied.
    /// </summary>
    public async Task<bool> CutSelectionAsync()
    {
        if (!IsEditable || CopyTargets() is not { Count: > 0 } nodes || Graph is not { } graph)
        {
            return false;
        }
        if (!await WriteClipboardAsync(WorkflowFragment.Copy(nodes, graph.Edges)))
        {
            return false;
        }
        // The selection may have changed while the clipboard was written.
        SetSelection(nodes.Where(graph.Nodes.Contains).ToList());
        var (deleted, _) = DeleteSelected(announce: false);
        // Locked steps are copied but stay.
        Announce(deleted.Count > 0
            ? Count(WorkflowLangResourceKind.AnnounceCutOne, WorkflowLangResourceKind.AnnounceCutOther, deleted.Count)
            : Count(WorkflowLangResourceKind.AnnounceCopiedOne, WorkflowLangResourceKind.AnnounceCopiedOther, nodes.Count));
        return true;
    }

    /// <summary>
    /// Pastes steps copied from a workflow canvas: next to where they were copied from when that place shows, else in
    /// the middle of the view; they become the selection. Returns false on a read-only canvas or when the clipboard holds
    /// no steps.
    /// </summary>
    public async Task<bool> PasteAsync()
    {
        if (!IsEditable || Graph is null)
        {
            return false;
        }
        var fragment = await ReadClipboardAsync();
        if (fragment is null || !IsEditable || Graph is null)
        {
            Announce(WorkflowText.Get(WorkflowLangResourceKind.AnnounceNothingToPaste));
            return false;
        }
        var key = fragment.ToJson();
        _pasteCount = key == _lastPasted ? _pasteCount + 1 : 1;
        _lastPasted = key;
        Insert(fragment, _pasteCount);
        return true;
    }

    /// <summary>
    /// Puts a copy of the selected steps (or the focused one) and the edges among them one grid step down and right,
    /// without the clipboard; returns how many steps it added.
    /// </summary>
    public int DuplicateSelection()
    {
        if (!IsEditable || CopyTargets() is not { Count: > 0 } nodes || Graph is not { } graph)
        {
            return 0;
        }
        return Insert(WorkflowFragment.Copy(nodes, graph.Edges), 1).Count;
    }

    /// <summary>The selected steps in reading order, or the focused step.</summary>
    private List<WorkflowNode> CopyTargets()
    {
        if (_selection.Count > 0)
        {
            var order = ReadingOrder();
            return _selection.OrderBy(order.IndexOf).ToList();
        }
        return FocusedNode() is { } focused ? [focused] : [];
    }

    /// <summary>
    /// Adds a copy of <paramref name="fragment"/> as one edit, <paramref name="steps"/> grid steps from where it was
    /// copied (or in the middle of the view when that place is out of sight), selects it and brings it into view.
    /// </summary>
    private List<WorkflowNode> Insert(WorkflowFragment fragment, int steps)
    {
        var graph = Graph!;
        var (nodes, edges) = fragment.Instantiate(graph.Nodes.Select(n => n.Id).ToHashSet());
        var bounds = fragment.Bounds;
        var spacing = _grid?.Spacing ?? 22;
        var view = _view.ScreenToWorld(new Rect(ViewportSize));
        var placed = nodes.All(n => double.IsFinite(n.Position.X) && double.IsFinite(n.Position.Y));
        Vector delta;
        if (placed && view.Intersects(bounds))
        {
            delta = new Vector(spacing, spacing) * steps;
        }
        else
        {
            // Out of sight (or copied without places): centered in the view, on the grid.
            delta = view.Center - bounds.Center;
            if (SnapToGrid && spacing > 0)
            {
                var corner = bounds.TopLeft + delta;
                delta += new Vector(Math.Round(corner.X / spacing) * spacing - corner.X, Math.Round(corner.Y / spacing) * spacing - corner.Y);
            }
        }
        for (var i = 0; i < nodes.Count; i++)
        {
            var from = WorkflowWorldPanel.PositionOf(fragment.Nodes[i]);
            nodes[i].Position = placed ? from + delta : bounds.TopLeft + delta + new Vector(0, i * (spacing * 4));
        }
        ChangeGraph(() =>
        {
            using (graph.History.BeginGroup())
            {
                foreach (var node in nodes)
                {
                    graph.Nodes.Add(node);
                }
                foreach (var edge in edges)
                {
                    graph.Edges.Add(edge);
                }
            }
        });
        SetSelection(nodes);
        var target = Target.EnsureVisible(new Rect(bounds.Position + delta, bounds.Size), ViewportSize, Padding);
        if (target != Target)
        {
            _manualView = true;
            SetView(target, animate: true);
        }
        Announce(Count(WorkflowLangResourceKind.AnnouncePastedOne, WorkflowLangResourceKind.AnnouncePastedOther, nodes.Count));
        RaiseEvent(new WorkflowEditEventArgs(GraphEditedEvent, WorkflowEditKind.Paste, nodes, edges));
        return nodes;
    }

    private async Task<bool> WriteClipboardAsync(WorkflowFragment fragment)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return false;
        }
        var json = fragment.ToJson();
        try
        {
            var item = new DataTransferItem();
            item.Set(WorkflowFragment.InProcessFormat, fragment);
            item.Set(WorkflowFragment.JsonFormat, json);
            item.SetText(json);
            var transfer = new DataTransfer();
            transfer.Add(item);
            await clipboard.SetDataAsync(transfer);
            return true;
        }
        catch (Exception)
        {
            // Some platforms take plain text only (the browser).
            try
            {
                await clipboard.SetTextAsync(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>The steps on the clipboard: the copied fragment itself in this process, else its JSON (or text that is).</summary>
    private async Task<WorkflowFragment?> ReadClipboardAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return null;
        }
        try
        {
            using (var local = await clipboard.TryGetInProcessDataAsync())
            {
                if (local is not null && local.Contains(WorkflowFragment.InProcessFormat) &&
                    await local.TryGetValueAsync(WorkflowFragment.InProcessFormat) is { } fragment)
                {
                    return fragment;
                }
            }
            using var data = await clipboard.TryGetDataAsync();
            if (data is null)
            {
                return null;
            }
            if (data.Contains(WorkflowFragment.JsonFormat) &&
                WorkflowFragment.FromJson(await data.TryGetValueAsync(WorkflowFragment.JsonFormat)) is { } fromJson)
            {
                return fromJson;
            }
            return WorkflowFragment.FromJson(await data.TryGetTextAsync());
        }
        catch (Exception)
        {
            return null;
        }
    }
}
