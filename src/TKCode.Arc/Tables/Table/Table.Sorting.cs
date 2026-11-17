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
    #region Sorting

    /// <summary>Sorts by <paramref name="column"/> (null or None restores the source order) and raises <see cref="SortChanged"/>.</summary>
    public void Sort(DataTableColumn? column, DataTableSortDirection direction)
    {
        SetSortState(column, column is null ? DataTableSortDirection.None : direction, clearKey: true);
        ApplySort(animate: IsLoaded);
        RaiseSortChanged();
    }

    /// <summary>Arc: a new column sorts ascending, the sorted column toggles; with <see cref="IsSortClearable"/> descending clears.</summary>
    internal void CycleSort(DataTableColumn column)
    {
        var current = ReferenceEquals(SortColumn, column) ? SortDirection : DataTableSortDirection.None;
        var next = current switch
        {
            DataTableSortDirection.Ascending => DataTableSortDirection.Descending,
            DataTableSortDirection.Descending when IsSortClearable => DataTableSortDirection.None,
            _ => DataTableSortDirection.Ascending
        };
        Sort(next == DataTableSortDirection.None ? null : column, next);
        Announce(next == DataTableSortDirection.None
            ? "Sort cleared"
            : $"Sorted by {column.Header}, {(next == DataTableSortDirection.Ascending ? "ascending" : "descending")}");
        if (_presenter?.Header.CellFor(Columns.IndexOf(column)) is { } cell)
        {
            _presenter.Header.RevealPill(cell);
        }
    }

    private void RaiseSortChanged() =>
        RaiseEvent(new DataTableSortChangedEventArgs(SortChangedEvent, SortDirection == DataTableSortDirection.None ? null : SortColumn, SortDirection));

    /// <summary>
    /// Writes the three sort properties together. A key without a matching column (columns not added yet) is kept
    /// with its direction and resolved when the columns arrive.
    /// </summary>
    private void SetSortState(DataTableColumn? column, DataTableSortDirection direction, bool clearKey = false)
    {
        _syncingSort = true;
        try
        {
            SetCurrentValue(SortColumnProperty, column);
            SetCurrentValue(SortKeyProperty, column?.EffectiveKey ?? (clearKey ? null : SortKey));
            SetCurrentValue(SortDirectionProperty, direction);
        }
        finally
        {
            _syncingSort = false;
        }
    }

    private void ApplySort(bool animate, bool captureFlip = true)
    {
        var count = _items.Count;
        if (captureFlip && _presenter is not null)
        {
            var oldPosition = _position;
            _presenter.CaptureFlip(key => _indexByKey.TryGetValue(key, out var source) && source < oldPosition.Length ? oldPosition[source] : -1,
                animate && ArcMotion.IsFull(this));
        }
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }
        var columnIndex = SortedColumnIndex;
        if (columnIndex >= 0 && count > 1)
        {
            var state = _states[columnIndex];
            var values = state.SortValues;
            if (values is null || values.Length != count)
            {
                values = new object?[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = SortValueOf(i, columnIndex);
                }
                state.SortValues = values;
                state.SortProbe?.Release();
                state.ValueProbe?.Release();
            }
            var descending = SortDirection == DataTableSortDirection.Descending;
            var comparer = state.Column.Comparer;
            Array.Sort(order, (a, b) =>
            {
                var left = values[a];
                var right = values[b];
                var emptyLeft = TableValues.IsEmpty(left);
                var emptyRight = TableValues.IsEmpty(right);
                // Empty values stay at the bottom in both directions.
                if (emptyLeft || emptyRight)
                {
                    return emptyLeft == emptyRight ? a - b : emptyLeft ? 1 : -1;
                }
                var result = TableValues.Compare(left!, right!, comparer);
                if (descending)
                {
                    result = -result;
                }
                return result != 0 ? result : a - b;
            });
        }
        var position = new int[count];
        for (var i = 0; i < count; i++)
        {
            position[order[i]] = i;
        }
        _order = order;
        _position = position;
        if (_currentRow >= count)
        {
            _currentRow = count - 1;
        }
        UpdateHeaderSort(animate);
        _presenter?.InvalidateMeasure();
        _presenter?.InvalidateVisual();
        foreach (var row in _presenter?.RealizedRows ?? [])
        {
            row.InvalidateVisual();
        }
    }

    private void UpdateHeaderSort(bool animate)
    {
        if (_presenter is null)
        {
            return;
        }
        var sorted = SortedColumnIndex;
        var descending = SortDirection == DataTableSortDirection.Descending;
        foreach (var cell in _presenter.Header.Cells)
        {
            if (!cell.IsSelectAll)
            {
                cell.SetSort(cell.ColumnIndex == sorted, descending, animate && IsLoaded);
            }
        }
    }

    #endregion
}
