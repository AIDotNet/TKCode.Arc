using System.Collections;
using Avalonia;
using Avalonia.Interactivity;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>What a paste does with rows of the clipboard that run past the last row of the grid.</summary>
public enum DataGridPasteOverflow
{
    /// <summary>Drops them and says so in the status bar.</summary>
    Truncate,

    /// <summary>Adds rows for them (needs <see cref="DataGrid.CanInsertRows"/> and a way to create rows).</summary>
    AppendRows
}

public partial class DataGrid
{
    /// <summary>
    /// Creates the object for a new row, for typed rows: <c>() => new Account()</c>. Rows of <see cref="DataGridRow"/> need no
    /// factory. Give typed rows a key that <see cref="RowKeySelector"/> reads and that no other row has.
    /// </summary>
    public static readonly StyledProperty<Func<object>?> RowFactoryProperty =
        AvaloniaProperty.Register<DataGrid, Func<object>?>(nameof(RowFactory));

    /// <summary>
    /// Shows Add row in the toolbar and allows <see cref="AddRow"/> and <see cref="InsertRow"/> (Ctrl+Shift+Plus inserts above the
    /// active row). Needs an <see cref="ItemsSource"/> that is a writable list, and either <see cref="DataGridRow"/> rows or a
    /// <see cref="RowFactory"/>. Added rows can be undone. Default false.
    /// </summary>
    public static readonly StyledProperty<bool> CanInsertRowsProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(CanInsertRows));

    /// <summary>What a paste does with clipboard rows that run past the last row: drop them (default) or add rows for them.</summary>
    public static readonly StyledProperty<DataGridPasteOverflow> PasteOverflowProperty =
        AvaloniaProperty.Register<DataGrid, DataGridPasteOverflow>(nameof(PasteOverflow));

    /// <summary>Raised after rows are added, and after a redo puts them back (undoing an addition raises <see cref="RowsDeleted"/>).</summary>
    public static readonly RoutedEvent<DataGridRowsEventArgs> RowsInsertedEvent =
        RoutedEvent.Register<DataGrid, DataGridRowsEventArgs>(nameof(RowsInserted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="RowFactoryProperty"/>
    public Func<object>? RowFactory { get => GetValue(RowFactoryProperty); set => SetValue(RowFactoryProperty, value); }

    /// <inheritdoc cref="CanInsertRowsProperty"/>
    public bool CanInsertRows { get => GetValue(CanInsertRowsProperty); set => SetValue(CanInsertRowsProperty, value); }

    /// <inheritdoc cref="PasteOverflowProperty"/>
    public DataGridPasteOverflow PasteOverflow { get => GetValue(PasteOverflowProperty); set => SetValue(PasteOverflowProperty, value); }

    /// <inheritdoc cref="RowsInsertedEvent"/>
    public event EventHandler<DataGridRowsEventArgs>? RowsInserted
    {
        add => AddHandler(RowsInsertedEvent, value);
        remove => RemoveHandler(RowsInsertedEvent, value);
    }

    /// <summary>Whether rows can be added now: allowed, not a data source, a writable list, and a way to make a row.</summary>
    internal bool CanCreateRows =>
        CanInsertRows && !HasDataSource && _store.IsWritableList && (RowFactory is not null || _store.Count == 0 || _store.ItemAt(0) is DataGridRow);

    private object NewRow()
    {
        if (RowFactory is { } factory)
        {
            return factory();
        }
        string key;
        do
        {
            key = Guid.NewGuid().ToString("N");
        }
        while (_store.IndexOfKey(key) >= 0);
        return new DataGridRow(key);
    }

    /// <summary>Adds a row at the end of the list, selects its first editable cell and returns it (null when rows cannot be added). One undo step.</summary>
    public object? AddRow()
    {
        EnsureView();
        return CanCreateRows && Insert(_store.Count, 1, select: true) is { Count: > 0 } added ? added[0].Item : null;
    }

    /// <summary>Adds a row above or below the active row (at the end without one), selects it and returns it (null when rows cannot be added).</summary>
    /// <param name="below">True inserts below the active row, false above it.</param>
    public object? InsertRow(bool below = true)
    {
        EnsureView();
        if (!CanCreateRows)
        {
            return null;
        }
        var index = _store.Count;
        if (HasCells && SourceOfRow(Anchor.Row) is >= 0 and var source)
        {
            index = below ? source + 1 : source;
        }
        return Insert(index, 1, select: true) is { Count: > 0 } added ? added[0].Item : null;
    }

    /// <summary>Adds rows at the end for a paste that overflows; one undo step.</summary>
    internal void AppendRows(int count)
    {
        if (count > 0 && CanCreateRows)
        {
            Insert(_store.Count, count, select: false);
            EnsureView();
        }
    }

    private List<DataGridDeletedRow>? Insert(int index, int count, bool select)
    {
        if (_store.Source is not IList list)
        {
            return null;
        }
        index = Math.Clamp(index, 0, list.Count);
        var created = new List<DataGridDeletedRow>(count);
        for (var i = 0; i < count; i++)
        {
            created.Add(new DataGridDeletedRow(index + i, NewRow()));
        }
        if (_edit is not null && !CommitEdit(null, null))
        {
            return null;
        }
        var step = new DataGridChangeSet { Reason = DataGridChangeReason.Edit };
        step.Inserted.AddRange(created);
        _undo.Push(step);
        InsertRows(created);
        UpdateUndoState();
        if (select)
        {
            EnsureView();
            var source = _store.IndexOfItem(created[0].Item);
            var display = source >= 0 && source < View.DisplayOfSource.Length ? View.DisplayOfSource[source] : -1;
            if (display >= 0)
            {
                var column = 0;
                var visible = VisibleAccessors;
                for (var c = 0; c < visible.Length; c++)
                {
                    if (ColumnOf(visible[c]).IsEditable)
                    {
                        column = c;
                        break;
                    }
                }
                Select(new DataGridCellAddress(display, column));
            }
            Say(DataGridText.Get(DataGridLangResourceKind.RowAdded), DataGridText.Get(DataGridLangResourceKind.Undo), () =>
            {
                Undo();
                Viewport.FocusGrid();
            });
        }
        return created;
    }

    /// <summary>Puts rows into the list at their indices (adding, and redoing an addition) and raises <see cref="RowsInserted"/>.</summary>
    private void InsertRows(IReadOnlyList<DataGridDeletedRow> rows)
    {
        if (_store.IsWritableList && _store.Source is IList list)
        {
            _store.Batch(() =>
            {
                foreach (var row in rows.OrderBy(r => r.Index))
                {
                    list.Insert(Math.Clamp(row.Index, 0, list.Count), row.Item);
                }
            });
        }
        RaiseEvent(new DataGridRowsEventArgs(RowsInsertedEvent, rows.Select(r => r.Item).ToArray(), rows.Select(r => r.Index).ToArray()));
    }
}
