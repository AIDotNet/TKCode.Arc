using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class TreeSelect
{
    #region Rows

    /// <summary>Flattens the open part of the tree (filtered while searching) into the rows panel.</summary>
    private void RebuildRows(bool animate, object? staggerAfter = null)
    {
        var rows = new List<object>(Math.Max(16, _rows.Count));
        var filtering = _query.Length > 0;
        Visit(_roots, rows, filtering);
        _rows = rows;
        if (_panel is null)
        {
            return;
        }
        _panel.SetRows(rows, animate && IsLoaded, staggerAfter);
        if (_activeKey is not null && _panel.IndexOfKey(_activeKey) < 0)
        {
            _activeKey = null;
            _panel.SetHighlight(null, animate: false);
        }
    }

    private void Visit(List<TreeSelectNode> nodes, List<object> rows, bool filtering)
    {
        foreach (var node in nodes)
        {
            if (filtering && !_filterVisible.Contains(node))
            {
                node.IsExpanded = false;
                continue;
            }
            node.IsExpanded = node.HasChildren && _expanded.Contains(node.Item);
            rows.Add(node);
            if (node.IsExpanded)
            {
                Visit(node.Children!, rows, filtering);
            }
        }
    }

    internal void SetNodeExpanded(TreeSelectNode node, bool expanded)
    {
        if (!node.HasChildren)
        {
            return;
        }
        var changed = expanded ? _expanded.Add(node.Item) : _expanded.Remove(node.Item);
        if (!changed)
        {
            return;
        }
        if (!expanded && _activeKey is not null && _nodes.TryGetValue(_activeKey, out var active) && IsDescendant(active, node))
        {
            SetActive(node.Item, animate: false, scroll: false);
        }
        RebuildRows(animate: true, expanded ? node.Item : null);
        if (_panel?.RowFor(node.Item) is { } row)
        {
            row.Refresh();
        }
    }

    private static bool IsDescendant(TreeSelectNode node, TreeSelectNode ancestor)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Opens the ancestors of the selection, so the menu opens on it.</summary>
    private void ExpandToSelection()
    {
        if (SelectionMode == TreeSelectMode.Single)
        {
            if (_selected is not null)
            {
                ExpandAncestors(_selected);
            }
            return;
        }
        foreach (var item in _selectedItems)
        {
            if (_nodes.TryGetValue(item, out var node))
            {
                ExpandAncestors(node);
            }
        }
    }

    private void ExpandAncestors(TreeSelectNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            _expanded.Add(parent.Item);
        }
    }

    private object? DefaultActive()
    {
        if (SelectionMode == TreeSelectMode.Single && _selected is not null && _panel?.IndexOfKey(_selected.Item) >= 0)
        {
            return _selected.Item;
        }
        return _rows.Count > 0 ? ((TreeSelectNode)_rows[0]).Item : null;
    }

    private void SetActive(object? key, bool animate, bool scroll)
    {
        _activeKey = key;
        if (_panel is null)
        {
            return;
        }
        _panel.SetHighlight(key, animate);
        if (scroll && key is not null)
        {
            ScrollToRow(key);
        }
    }

    private void ScrollToRow(object key)
    {
        if (_panel is null || _scroll is null)
        {
            return;
        }
        var index = _panel.IndexOfKey(key);
        if (index < 0)
        {
            return;
        }
        var top = _panel.SettledRowTop(index);
        var bottom = top + RowHeight;
        var offset = _scroll.Offset.Y;
        var viewport = _scroll.Viewport.Height;
        if (viewport <= 0)
        {
            return;
        }
        if (top < offset)
        {
            _scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, top));
        }
        else if (bottom > offset + viewport)
        {
            _scroll.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(0, bottom - viewport));
        }
    }

    internal void OnRowHovered(TreeSelectRow row)
    {
        if (row.Node is { } node)
        {
            SetActive(node.Item, animate: true, scroll: false);
        }
    }

    internal void OnRowsPointerExited()
    {
        if (IsOpen && _activeKey is not null)
        {
            _activeKey = null;
            _panel?.SetHighlight(null, animate: false);
        }
    }

    internal void OnRowClicked(TreeSelectRow row, bool onChevron)
    {
        if (row.Node is not { } node)
        {
            return;
        }
        SetActive(node.Item, animate: false, scroll: false);
        if (onChevron)
        {
            SetNodeExpanded(node, !node.IsExpanded);
        }
        else if (SelectionMode == TreeSelectMode.Multiple)
        {
            if (node.IsDisabled)
            {
                SetNodeExpanded(node, !node.IsExpanded);
            }
            else
            {
                ToggleNode(node);
            }
        }
        else
        {
            PickNode(node);
            return;
        }
        if (IsOpen && _search is { IsVisible: true } search && !search.IsFocused)
        {
            search.Focus(NavigationMethod.Pointer);
        }
    }

    private void Announce(TreeSelectNode node)
    {
        if (_announcer is null)
        {
            return;
        }
        var text = $"{node.Label}, {TreeSelectText.Level(node.Depth)}";
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            text += ", " + TreeSelectText.StateOf(node.State);
        }
        else if (ReferenceEquals(node, _selected))
        {
            text += ", " + TreeSelectLangResources.Get(TreeSelectLangResourceKind.Selected);
        }
        _announcer.Text = text;
    }

    #endregion
}
