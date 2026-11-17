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
    #region Checks and selection

    /// <summary>Sets the checks from <paramref name="values"/> (multiple mode): a parent's value checks its enabled descendants.</summary>
    private void CheckValues(IReadOnlyList<object?> values)
    {
        foreach (var node in _nodes.Values)
        {
            node.IsChecked = false;
        }
        foreach (var value in values)
        {
            if (value is not null && _byValue.TryGetValue(value, out var node))
            {
                if (node.HasChildren)
                {
                    SetSubtree(node, true);
                }
                else
                {
                    node.IsChecked = true;
                }
            }
        }
    }

    /// <summary>Checks or unchecks the leaves under <paramref name="node"/>, skipping disabled nodes and their subtrees.</summary>
    private static bool SetSubtree(TreeSelectNode node, bool on)
    {
        if (!node.HasChildren)
        {
            var changed = node.IsChecked != on;
            node.IsChecked = on;
            return changed;
        }
        var any = false;
        foreach (var child in node.Children!)
        {
            if (!child.IsDisabled)
            {
                any |= SetSubtree(child, on);
            }
        }
        return any;
    }

    /// <summary>Derives every parent's three-state check from its children (disabled children do not hold a parent back).</summary>
    private void Recompute()
    {
        foreach (var root in _roots)
        {
            StateOf(root);
        }
    }

    private static CheckboxState StateOf(TreeSelectNode node)
    {
        if (!node.HasChildren)
        {
            node.State = node.IsChecked ? CheckboxState.Checked : CheckboxState.Unchecked;
            return node.State;
        }
        int enabled = 0, enabledChecked = 0, all = 0, allChecked = 0;
        var any = false;
        foreach (var child in node.Children!)
        {
            var state = StateOf(child);
            all++;
            if (state != CheckboxState.Unchecked)
            {
                any = true;
            }
            if (state == CheckboxState.Checked)
            {
                allChecked++;
            }
            if (!child.IsDisabled)
            {
                enabled++;
                if (state == CheckboxState.Checked)
                {
                    enabledChecked++;
                }
            }
        }
        node.State = (enabled > 0 ? enabledChecked == enabled : allChecked == all)
            ? CheckboxState.Checked
            : any ? CheckboxState.Indeterminate : CheckboxState.Unchecked;
        return node.State;
    }

    /// <summary>The user toggles a node's check (multiple mode).</summary>
    internal void ToggleNode(TreeSelectNode node)
    {
        if (node.IsDisabled || IsReadOnly || !IsEffectivelyEnabled)
        {
            return;
        }
        var on = node.State != CheckboxState.Checked;
        if (!SetSubtree(node, on) && on)
        {
            // Nothing left to check (the rest is disabled): the click unchecks instead.
            SetSubtree(node, false);
        }
        Recompute();
        UpdateReported(null);
        Announce(node);
    }

    /// <summary>The user picks a node (single mode).</summary>
    internal void PickNode(TreeSelectNode node)
    {
        if (node.IsDisabled)
        {
            if (node.HasChildren)
            {
                SetNodeExpanded(node, !node.IsExpanded);
            }
            return;
        }
        if (LeafOnly && node.HasChildren)
        {
            SetNodeExpanded(node, !node.IsExpanded);
            return;
        }
        if (!IsReadOnly)
        {
            SetSelected(node, writeValue: true);
        }
        Close();
        Focus(FocusModality.For(this));
    }

    internal bool IsSelectedNode(TreeSelectNode node) => SelectionMode == TreeSelectMode.Single && ReferenceEquals(_selected, node);

    private void OnSelectedItemWritten(object? item)
    {
        if (item is null)
        {
            SetSelected(null, writeValue: true);
            return;
        }
        SetSelected(_nodes.GetValueOrDefault(item), writeValue: true);
    }

    private void OnSelectedValueWritten(object? value)
    {
        // Not found: the value stays (the data may still be loading).
        SetSelected(value is null ? null : _byValue.GetValueOrDefault(value), writeValue: false);
    }

    /// <summary>Makes <paramref name="node"/> the single selection and updates the item and value without echoing.</summary>
    private void SetSelected(TreeSelectNode? node, bool writeValue)
    {
        _syncing = true;
        try
        {
            var item = node?.Item;
            if (node is not null || writeValue)
            {
                if (!Equals(SelectedItem, item))
                {
                    SetCurrentValue(SelectedItemProperty, item);
                }
            }
            else if (SelectedItem is not null && !_nodes.ContainsKey(SelectedItem))
            {
                SetCurrentValue(SelectedItemProperty, null);
            }
            if (writeValue)
            {
                var value = node?.Value;
                if (!Equals(SelectedValue, value))
                {
                    SetCurrentValue(SelectedValueProperty, value);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
        if (ReferenceEquals(_selected, node))
        {
            return;
        }
        var old = _selected;
        _selected = node;
        if (_panel is not null)
        {
            if (old is not null)
            {
                _panel.RowFor(old.Item)?.Refresh();
            }
            if (node is not null)
            {
                _panel.RowFor(node.Item)?.Refresh();
            }
        }
        UpdateReported(null);
    }

    private void WriteSelectedValues(IReadOnlyList<object?> values)
    {
        if (_syncing)
        {
            return;
        }
        if (SelectionMode == TreeSelectMode.Single)
        {
            SetCurrentValue(SelectedValueProperty, values.Count > 0 ? values[0] : null);
            UpdateReported(values);
            return;
        }
        if (_roots.Count == 0)
        {
            // No data yet: keep the values as written and apply them when the data arrives.
            _pendingValues = values.Count > 0 ? values : null;
            SetAndRaise(SelectedValuesProperty, ref _selectedValues, values);
            return;
        }
        _pendingValues = null;
        CheckValues(values);
        Recompute();
        UpdateReported(values);
    }

    /// <summary>
    /// Recomputes <see cref="SelectedItems"/> and <see cref="SelectedValues"/> from the checks or the single selection,
    /// raises <see cref="SelectionChanged"/> when the items changed and refreshes the field and the rows.
    /// <paramref name="written"/> is the list a binding just wrote: it is kept when it says the same.
    /// </summary>
    private void UpdateReported(IReadOnlyList<object?>? written)
    {
        List<TreeSelectNode> reported = new();
        if (SelectionMode == TreeSelectMode.Multiple)
        {
            Collect(_roots, CheckStrategy, reported);
        }
        else if (_selected is not null)
        {
            reported.Add(_selected);
        }
        var oldItems = _selectedItems;
        var same = oldItems.Count == reported.Count;
        for (var i = 0; same && i < reported.Count; i++)
        {
            same = ReferenceEquals(oldItems[i], reported[i].Item);
        }
        if (!same)
        {
            var items = reported.Select(n => n.Item).ToArray();
            SelectedItems = items;
            var oldSet = new HashSet<object>(oldItems);
            var newSet = new HashSet<object>(items);
            var added = items.Where(i => !oldSet.Contains(i)).ToArray();
            var removed = oldItems.Where(i => !newSet.Contains(i)).ToArray();
            RaiseEvent(new TreeSelectSelectionChangedEventArgs(SelectionChangedEvent, added, removed));
        }
        if (_pendingValues is null)
        {
            var values = reported.Select(n => n.Value).ToArray();
            IReadOnlyList<object?> next = written is not null && written.SequenceEqual(values) ? written
                : _selectedValues.SequenceEqual(values) ? _selectedValues : values;
            if (SelectionMode == TreeSelectMode.Single && _selected is null && SelectedValue is { } kept)
            {
                // A single value the data does not contain yet is reported as written.
                next = _selectedValues.Count == 1 && Equals(_selectedValues[0], kept) ? _selectedValues : new[] { kept };
            }
            if (!ReferenceEquals(next, _selectedValues))
            {
                _syncing = true;
                try
                {
                    SetAndRaise(SelectedValuesProperty, ref _selectedValues, next);
                }
                finally
                {
                    _syncing = false;
                }
            }
        }
        UpdateField(animate: true);
        UpdateClear();
        if (_panel is not null)
        {
            foreach (var row in _panel.RealizedRows)
            {
                row.Refresh();
            }
        }
    }

    private static void Collect(List<TreeSelectNode> nodes, TreeSelectCheckStrategy strategy, List<TreeSelectNode> into)
    {
        foreach (var node in nodes)
        {
            if (!node.HasChildren)
            {
                if (node.State == CheckboxState.Checked)
                {
                    into.Add(node);
                }
                continue;
            }
            if (node.State == CheckboxState.Unchecked)
            {
                continue;
            }
            if (node.State == CheckboxState.Checked && strategy != TreeSelectCheckStrategy.Leaves)
            {
                into.Add(node);
                if (strategy == TreeSelectCheckStrategy.Parents)
                {
                    continue;
                }
            }
            Collect(node.Children!, strategy, into);
        }
    }

    #endregion
}
