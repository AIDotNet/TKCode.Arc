using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class Table
{
    #region Selection

    private int SelectedPresentCount()
    {
        if (!IsSelectable || _selected.Count == 0)
        {
            return 0;
        }
        var count = 0;
        foreach (var key in _keys)
        {
            if (_selected.Contains(key))
            {
                count++;
            }
        }
        return count;
    }

    internal void ToggleAll()
    {
        if (!IsSelectable)
        {
            return;
        }
        var all = _items.Count > 0 && SelectedPresentCount() == _items.Count;
        Commit(all ? new HashSet<object>() : new HashSet<object>(_keys));
    }

    /// <summary>Selects every row.</summary>
    public void SelectAll()
    {
        if (IsSelectable)
        {
            Commit(new HashSet<object>(_keys));
        }
    }

    /// <summary>Clears the selection and moves focus to Select all (Arc's Clear selection).</summary>
    public void ClearSelection()
    {
        Commit(new HashSet<object>());
        _presenter?.Header.CellFor(-1)?.Focus();
    }

    /// <summary>Toggles one row; with <paramref name="extend"/> the range from the last toggled row takes the same state.</summary>
    private void ToggleRow(object key, bool extend)
    {
        var next = new HashSet<object>(_selected);
        var check = !_selected.Contains(key);
        var to = _indexByKey.TryGetValue(key, out var source) ? _position[source] : -1;
        var from = extend && _anchor is not null && _indexByKey.TryGetValue(_anchor, out var anchorSource) ? _position[anchorSource] : -1;
        if (from < 0 || to < 0)
        {
            Apply(key);
        }
        else
        {
            for (var d = Math.Min(from, to); d <= Math.Max(from, to); d++)
            {
                Apply(_keys[_order[d]]);
            }
        }
        _anchor = key;
        Commit(next);

        void Apply(object item)
        {
            if (check)
            {
                next.Add(item);
            }
            else
            {
                next.Remove(item);
            }
        }
    }

    private void Commit(HashSet<object> next)
    {
        var present = new HashSet<object>();
        var added = new List<object>();
        var removed = new List<object>();
        foreach (var key in _keys)
        {
            if (!next.Contains(key))
            {
                continue;
            }
            present.Add(key);
        }
        for (var d = 0; d < _order.Length; d++)
        {
            var source = _order[d];
            var key = _keys[source];
            var was = _selected.Contains(key);
            var now = present.Contains(key);
            if (was != now)
            {
                (now ? added : removed).Add(_items[source]);
            }
        }
        _selected = present;
        ApplySelectionVisuals(animate: true);
        var selectedItems = SyncSelectedItemsOut(raise: true);
        Announce(selectedItems.Count > 0 ? $"{selectedItems.Count} of {_items.Count} selected" : "Selection cleared");
        RaiseEvent(new DataTableSelectionChangedEventArgs(SelectionChangedEvent, selectedItems, added, removed));
    }

    private void ApplySelectionVisuals(bool animate)
    {
        if (_presenter is not null)
        {
            foreach (var row in _presenter.RealizedRows)
            {
                if (row.SourceIndex >= 0)
                {
                    row.SetSelected(IsSourceSelected(row.SourceIndex), animate && IsLoaded);
                }
            }
            var count = SelectedPresentCount();
            _presenter.Header.CellFor(-1)?.Checkbox?.Set(_items.Count > 0 && count == _items.Count,
                count > 0 && count < _items.Count, animate && IsLoaded);
            _presenter.Header.CellFor(-1)?.InvalidateVisual();
        }
        UpdateCount();
        UpdatePseudoClasses();
    }

    private List<object> SelectedInDisplayOrder()
    {
        var list = new List<object>();
        if (!IsSelectable)
        {
            return list;
        }
        foreach (var source in _order)
        {
            if (_selected.Contains(_keys[source]))
            {
                list.Add(_items[source]);
            }
        }
        return list;
    }

    private List<object> SyncSelectedItemsOut(bool raise)
    {
        var selected = SelectedInDisplayOrder();
        var list = SelectedItems;
        _syncingSelection = true;
        try
        {
            if (list is null || list.IsReadOnly || list.IsFixedSize)
            {
                SetCurrentValue(SelectedItemsProperty, new AvaloniaList<object>(selected));
            }
            else
            {
                var same = list.Count == selected.Count;
                for (var i = 0; same && i < selected.Count; i++)
                {
                    same = ReferenceEquals(list[i], selected[i]);
                }
                if (!same)
                {
                    list.Clear();
                    foreach (var item in selected)
                    {
                        list.Add(item);
                    }
                }
            }
        }
        finally
        {
            _syncingSelection = false;
        }
        _ = raise;
        return selected;
    }

    private void OnSelectedItemsChanged(IList? value)
    {
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectedCollectionChanged;
            _observedSelection = null;
        }
        if (value is INotifyCollectionChanged observable)
        {
            _observedSelection = observable;
            if (!_sourcesDetached)
            {
                observable.CollectionChanged += OnSelectedCollectionChanged;
            }
        }
        if (!_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void OnSelectedCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void SyncSelectedItemsIn()
    {
        var next = new HashSet<object>();
        if (SelectedItems is { } list)
        {
            var selector = RowKeySelector;
            foreach (var item in list)
            {
                if (item is not null)
                {
                    next.Add(selector?.Invoke(item) ?? item);
                }
            }
        }
        _selected = next;
        ApplySelectionVisuals(animate: true);
    }

    internal void OnRowClicked(TableRow row, bool extend)
    {
        if (!IsSelectable || row.Key is null)
        {
            return;
        }
        ToggleRow(row.Key, extend);
        // Keep the keyboard path where the pointer left off, so arrows continue from this row.
        _currentRow = row.DisplayIndex;
        row.Focus(NavigationMethod.Pointer);
        _presenter?.UpdateTabStops();
    }

    internal void OnRowFocused(TableRow row)
    {
        if (row.DisplayIndex >= 0)
        {
            _currentRow = row.DisplayIndex;
            _presenter?.UpdateTabStops();
        }
    }

    #endregion
}
