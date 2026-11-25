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
    #region Data

    // Inline TreeViewItem nodes (as TreeView takes them) need no selectors: header, children, icon and disabled come from them.
    private string TextOf(object item) =>
        (TextSelector?.Invoke(item) ?? (item as TreeViewItem)?.Header ?? item.ToString()) ?? string.Empty;

    private object? ValueOf(object item) => ValueSelector is { } selector ? selector(item) : item;

    /// <summary>Reads the whole tree again (data or a selector changed) and re-applies the selection by value.</summary>
    private void Reload()
    {
        _rootsNotifier?.CollectionChanged -= OnRootsChanged;
        _rootsNotifier = ItemsSource as INotifyCollectionChanged;
        _rootsNotifier?.CollectionChanged += OnRootsChanged;
        ReadTree();
    }

    private void OnRootsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ReadTree();

    private void ReadTree()
    {
        _loaded = true;
        // The values are the durable selection: checks survive a reload through them.
        var values = _pendingValues ?? (SelectionMode == TreeSelectMode.Multiple ? _selectedValues : null);
        var icon = IconSelector;
        var disabled = DisabledSelector;
        var nodes = new Dictionary<object, TreeSelectNode>(Math.Max(16, _nodes.Count));
        var byValue = new Dictionary<object, TreeSelectNode>(Math.Max(16, _byValue.Count));
        _warnedDuplicate = false;
        _anyIcon = false;
        _roots.Clear();
        Build(ItemsSource, null, 1, _roots, nodes, byValue, icon, disabled);
        _nodes = nodes;
        _byValue = byValue;
        _expanded.RemoveWhere(item => !nodes.ContainsKey(item));
        _matched.Clear();
        _filterVisible.Clear();
        ApplyExpandDepth();

        if (_roots.Count > 0)
        {
            _pendingValues = null;
            if (values is { Count: > 0 })
            {
                CheckValues(values);
            }
        }
        else if (values is { Count: > 0 } && SelectionMode == TreeSelectMode.Multiple)
        {
            // No data (yet): the values wait for it.
            _pendingValues = values;
        }
        Recompute();
        // Single mode: find the selection again (a value the data lacks is kept, as Cascader does).
        if (SelectedValue is { } value)
        {
            SetSelected(byValue.GetValueOrDefault(value), writeValue: false);
        }
        else if (SelectedItem is { } item)
        {
            SetSelected(nodes.GetValueOrDefault(item), writeValue: true);
        }
        else
        {
            SetSelected(null, writeValue: false);
        }
        UpdateReported(values);
        if (_query.Length > 0)
        {
            Refilter();
        }
        else if (IsOpen)
        {
            RebuildRows(animate: false);
        }
        UpdatePalette();
    }

    private void Build(IEnumerable? source, TreeSelectNode? parent, int depth, List<TreeSelectNode> into,
                       Dictionary<object, TreeSelectNode> nodes, Dictionary<object, TreeSelectNode> byValue,
                       Func<object, Geometry?>? icon, Func<object, bool>? disabled)
    {
        if (source is null)
        {
            return;
        }
        var start = into.Count;
        foreach (var item in source)
        {
            if (item is null || nodes.ContainsKey(item))
            {
                continue;
            }
            var node = new TreeSelectNode(item)
            {
                Parent = parent,
                Depth = depth,
                Label = TextOf(item),
                Value = ValueOf(item),
                Icon = icon is not null ? icon(item) : (item as TreeViewItem)?.Icon,
                IsDisabled = disabled is not null ? disabled(item) : item is TreeViewItem { IsDisabled: true }
            };
            _anyIcon |= node.Icon is not null;
            nodes[item] = node;
            if (node.Value is { } value && !byValue.TryAdd(value, node) && !_warnedDuplicate)
            {
                _warnedDuplicate = true;
                Debug.WriteLine($"TreeSelect: the value '{value}' belongs to more than one node; the first one wins. Values must be unique across the tree.");
            }
            into.Add(node);
            if ((ChildrenSelector is { } selector ? selector(item) : (item as TreeViewItem)?.Items) is { } children)
            {
                var list = new List<TreeSelectNode>();
                Build(children, node, depth + 1, list, nodes, byValue, icon, disabled);
                if (list.Count > 0)
                {
                    node.Children = list;
                }
            }
        }
        var size = into.Count - start;
        for (var i = start; i < into.Count; i++)
        {
            into[i].Position = i - start + 1;
            into[i].SetSize = size;
        }
    }

    private void ApplyExpandDepth()
    {
        if (_expandApplied || _roots.Count == 0)
        {
            return;
        }
        _expandApplied = true;
        var depth = ExpandDepth;
        if (depth == 0)
        {
            return;
        }
        var target = _expandedBeforeSearch ?? _expanded;
        foreach (var node in _nodes.Values)
        {
            if (node.HasChildren && (depth < 0 || node.Depth <= depth))
            {
                target.Add(node.Item);
            }
        }
    }

    #endregion
}
