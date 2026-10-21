using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>Arguments of the cell events of a <see cref="DataGrid"/>: <see cref="DataGrid.CellClick"/>, <see cref="DataGrid.CellDoubleClick"/>, <see cref="DataGrid.CellContextRequested"/> and <see cref="DataGrid.RowActivated"/>.</summary>
public sealed class DataGridCellEventArgs : RoutedEventArgs
{
    internal DataGridCellEventArgs(RoutedEvent routedEvent, object row, object rowKey, DataGridColumn column, DataGridCellAddress address,
                                   object? value, PointerType? pointerType, KeyModifiers keyModifiers)
        : base(routedEvent)
    {
        Row = row;
        RowKey = rowKey;
        Column = column;
        Address = address;
        Value = value;
        PointerType = pointerType;
        KeyModifiers = keyModifiers;
    }

    /// <summary>The row object (a <see cref="DataGridRow"/> or one of your own classes).</summary>
    public object Row { get; }

    /// <summary>The row's key.</summary>
    public object RowKey { get; }

    /// <summary>The column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>The cell's display row and visible column.</summary>
    public DataGridCellAddress Address { get; }

    /// <summary>The cell's normalized value.</summary>
    public object? Value { get; }

    /// <summary>The kind of pointer that caused the event; null for the keyboard.</summary>
    public PointerType? PointerType { get; }

    /// <summary>The modifier keys held down.</summary>
    public KeyModifiers KeyModifiers { get; }
}

/// <summary>An entry you add to the grid's context menu through <see cref="DataGrid.ContextMenuItems"/>.</summary>
public sealed class DataGridMenuItem : AvaloniaObject
{
    /// <summary>The entry's text.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<DataGridMenuItem, string>(nameof(Label), string.Empty);

    /// <summary>A 24 x 24 stroked geometry shown before the label.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<DataGridMenuItem, Geometry?>(nameof(Icon));

    /// <summary>Runs with the row object of the clicked cell as its parameter.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<DataGridMenuItem, ICommand?>(nameof(Command));

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
}

/// <summary>Arguments of <see cref="DataGrid.ContextMenuOpening"/>: change <see cref="Items"/> or set <see cref="Cancel"/> to stop the menu.</summary>
public sealed class DataGridContextMenuOpeningEventArgs : RoutedEventArgs
{
    internal DataGridContextMenuOpeningEventArgs(RoutedEvent routedEvent, DataGridCellEventArgs cell, List<DataGridMenuItem> items)
        : base(routedEvent)
    {
        Cell = cell;
        Items = items;
    }

    /// <summary>The cell the menu opens on.</summary>
    public DataGridCellEventArgs Cell { get; }

    /// <summary>The entries added after the built-in ones (from <see cref="DataGrid.ContextMenuItems"/>); edit the list to change them for this opening.</summary>
    public List<DataGridMenuItem> Items { get; }

    /// <summary>Set to true to keep the menu from opening.</summary>
    public bool Cancel { get; set; }
}

public partial class DataGrid
{
    /// <summary>Shows the built-in right-click menu (also Shift+F10 and the menu key, and a long press on touch). Default true.</summary>
    public static readonly StyledProperty<bool> ShowContextMenuProperty =
        AvaloniaProperty.Register<DataGrid, bool>(nameof(ShowContextMenu), true);

    /// <summary>Raised when a body cell is pressed with the primary button, after the selection moved (or when a renderer took the press).</summary>
    public static readonly RoutedEvent<DataGridCellEventArgs> CellClickEvent =
        RoutedEvent.Register<DataGrid, DataGridCellEventArgs>(nameof(CellClick), RoutingStrategies.Bubble);

    /// <summary>Raised on a double click or double tap of a body cell, before the editor opens; set <c>Handled</c> to keep the editor from opening.</summary>
    public static readonly RoutedEvent<DataGridCellEventArgs> CellDoubleClickEvent =
        RoutedEvent.Register<DataGrid, DataGridCellEventArgs>(nameof(CellDoubleClick), RoutingStrategies.Bubble);

    /// <summary>Raised when a context menu is asked for on a body cell; set <c>Handled</c> to show your own menu instead of the built-in one.</summary>
    public static readonly RoutedEvent<DataGridCellEventArgs> CellContextRequestedEvent =
        RoutedEvent.Register<DataGrid, DataGridCellEventArgs>(nameof(CellContextRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when Enter is pressed on a cell that cannot be edited, and on a double click of the row number: "open this row".</summary>
    public static readonly RoutedEvent<DataGridCellEventArgs> RowActivatedEvent =
        RoutedEvent.Register<DataGrid, DataGridCellEventArgs>(nameof(RowActivated), RoutingStrategies.Bubble);

    /// <summary>Raised before the built-in menu opens, so the entries can be changed or the menu cancelled.</summary>
    public static readonly RoutedEvent<DataGridContextMenuOpeningEventArgs> ContextMenuOpeningEvent =
        RoutedEvent.Register<DataGrid, DataGridContextMenuOpeningEventArgs>(nameof(ContextMenuOpening), RoutingStrategies.Bubble);

    private readonly AvaloniaList<DataGridMenuItem> _contextMenuItems = new();

    /// <inheritdoc cref="ShowContextMenuProperty"/>
    public bool ShowContextMenu { get => GetValue(ShowContextMenuProperty); set => SetValue(ShowContextMenuProperty, value); }

    /// <summary>Entries added to the built-in context menu after a separator; each runs its command with the clicked row.</summary>
    public AvaloniaList<DataGridMenuItem> ContextMenuItems => _contextMenuItems;

    /// <inheritdoc cref="CellClickEvent"/>
    public event EventHandler<DataGridCellEventArgs>? CellClick
    {
        add => AddHandler(CellClickEvent, value);
        remove => RemoveHandler(CellClickEvent, value);
    }

    /// <inheritdoc cref="CellDoubleClickEvent"/>
    public event EventHandler<DataGridCellEventArgs>? CellDoubleClick
    {
        add => AddHandler(CellDoubleClickEvent, value);
        remove => RemoveHandler(CellDoubleClickEvent, value);
    }

    /// <inheritdoc cref="CellContextRequestedEvent"/>
    public event EventHandler<DataGridCellEventArgs>? CellContextRequested
    {
        add => AddHandler(CellContextRequestedEvent, value);
        remove => RemoveHandler(CellContextRequestedEvent, value);
    }

    /// <inheritdoc cref="RowActivatedEvent"/>
    public event EventHandler<DataGridCellEventArgs>? RowActivated
    {
        add => AddHandler(RowActivatedEvent, value);
        remove => RemoveHandler(RowActivatedEvent, value);
    }

    /// <inheritdoc cref="ContextMenuOpeningEvent"/>
    public event EventHandler<DataGridContextMenuOpeningEventArgs>? ContextMenuOpening
    {
        add => AddHandler(ContextMenuOpeningEvent, value);
        remove => RemoveHandler(ContextMenuOpeningEvent, value);
    }

    /// <summary>Builds the event arguments for a body cell; null for rows that are not loaded.</summary>
    internal DataGridCellEventArgs? CellArgs(RoutedEvent routedEvent, DataGridCellAddress cell, PointerType? pointerType, KeyModifiers modifiers)
    {
        if (cell.Column < 0 || cell.Column >= VisibleAccessors.Length || KeyOfRow(cell.Row) is not { } key || ItemOfRow(cell.Row) is not { } item)
        {
            return null;
        }
        var accessor = VisibleAccessors[cell.Column];
        return new DataGridCellEventArgs(routedEvent, item, key, ColumnOf(accessor), cell, ValueAt(cell.Row, accessor), pointerType, modifiers);
    }

    /// <summary>Raises a cell event; returns its arguments (so a handler's <c>Handled</c> can be read), or null when the cell is not loaded.</summary>
    internal DataGridCellEventArgs? RaiseCellEvent(RoutedEvent<DataGridCellEventArgs> routedEvent, DataGridCellAddress cell, PointerType? pointerType,
                                                   KeyModifiers modifiers)
    {
        if (CellArgs(routedEvent, cell, pointerType, modifiers) is not { } args)
        {
            return null;
        }
        RaiseEvent(args);
        return args;
    }

    /// <summary>Handles a context request on a body cell: selects it when needed, raises the event and opens the menu unless handled.</summary>
    internal bool RequestContextMenu(DataGridCellAddress cell, Point position, PointerType? pointerType, KeyModifiers modifiers)
    {
        if (!HasCells || CellArgs(CellContextRequestedEvent, cell, pointerType, modifiers) is not { } args)
        {
            return false;
        }
        if (_edit is not null && !CommitEdit(null, null))
        {
            return true;
        }
        if (!Range.Contains(cell.Row, cell.Column))
        {
            Select(cell);
        }
        RaiseEvent(args);
        if (!args.Handled && ShowContextMenu)
        {
            OpenContextMenu(args, position);
        }
        return true;
    }

    /// <summary>Opens the built-in menu for a cell at a viewport position.</summary>
    internal void OpenContextMenu(DataGridCellEventArgs cell, Point position)
    {
        var extra = _contextMenuItems.ToList();
        var opening = new DataGridContextMenuOpeningEventArgs(ContextMenuOpeningEvent, cell, extra);
        RaiseEvent(opening);
        if (opening.Cancel)
        {
            return;
        }
        var column = cell.Column;
        var accessor = _store.AccessorIndex(column.Key);
        var entries = new List<Control>();
        var editable = !HasDataSource;
        if (editable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.Cut), DataGridIcons.Scissors, "cut"));
        }
        entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.Copy), ArcIcons.Copy, "copy"));
        entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.CopyWithHeaders), ArcIcons.Copy, "copyHeaders"));
        if (editable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.Paste), DataGridIcons.Clipboard, "paste"));
            entries.Add(new MenuEntrySeparator());
            if (CanCreateRows)
            {
                entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.InsertRowAbove), ArcIcons.Plus, "insertAbove"));
                entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.InsertRowBelow), ArcIcons.Plus, "insertBelow"));
            }
            if (CanDeleteRows)
            {
                entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.DeleteRowsOfSelection), ArcIcons.Trash2, "delete"));
            }
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.ClearContents), ArcIcons.Eraser, "clear"));
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.FillDownAction), ArcIcons.ArrowDown, "fillDown", Range.RowCount > 1));
        }
        entries.Add(new MenuEntrySeparator());
        if (column.IsFilterable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.FilterByValue), ArcIcons.Filter, "filterBy", !DataGridSorting.IsEmpty(cell.Value)));
        }
        if (column.IsSortable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.SortAscending), ArcIcons.ArrowUpNarrowWide, "asc"));
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.SortDescending), ArcIcons.ArrowDownWideNarrow, "desc"));
        }
        if (!HasDataSource && column.EffectiveIsGroupable)
        {
            var grouped = GroupBy.Contains(column.Key);
            entries.Add(Entry(grouped ? DataGridText.Get(DataGridLangResourceKind.StopGrouping) : DataGridText.Format(DataGridLangResourceKind.GroupByColumn, column.Header),
                grouped ? DataGridIcons.Ungroup : DataGridIcons.Group, "group"));
        }
        if (opening.Items.Count > 0)
        {
            entries.Add(new MenuEntrySeparator());
            for (var i = 0; i < opening.Items.Count; i++)
            {
                entries.Add(new MenuEntry { Header = opening.Items[i].Label, Icon = opening.Items[i].Icon, Tag = opening.Items[i] });
            }
        }
        var menu = Menu(entries, entry =>
        {
            if (entry.Tag is DataGridMenuItem custom)
            {
                if (custom.Command?.CanExecute(cell.Row) ?? false)
                {
                    custom.Command.Execute(cell.Row);
                }
                return;
            }
            RunContextAction(entry.Tag as string, cell, accessor);
        });
        menu.Alignment = MenuAlignment.Start;
        menu.OpenAt(Viewport, position, MenuOpenFocus.Surface);
    }

    private void RunContextAction(string? action, DataGridCellEventArgs cell, int accessor)
    {
        switch (action)
        {
            case "cut":
                _ = CutSelectionAsync();
                break;
            case "copy":
                _ = CopySelectionAsync();
                break;
            case "copyHeaders":
                _ = CopySelectionAsync(withHeaders: true);
                break;
            case "paste":
                _ = PasteAsync();
                break;
            case "insertAbove":
                InsertRow(below: false);
                break;
            case "insertBelow":
                InsertRow(below: true);
                break;
            case "delete":
                DeleteSelectedRows();
                break;
            case "clear":
                ClearRange(Range);
                break;
            case "fillDown":
                FillDown();
                break;
            case "filterBy":
                FilterByValue(cell.Column, accessor, cell.Value);
                break;
            case "asc":
                ApplySort([DataGridSort.Ascending(cell.Column.Key)]);
                break;
            case "desc":
                ApplySort([DataGridSort.Descending(cell.Column.Key)]);
                break;
            case "group":
                ToggleGroupBy(cell.Column.Key);
                break;
        }
        Viewport.FocusGrid();
    }

    /// <summary>Filters a column to the value of one of its cells (a value list for select and boolean columns, an exact expression otherwise).</summary>
    private void FilterByValue(DataGridColumn column, int accessor, object? value)
    {
        if (DataGridSorting.IsEmpty(value))
        {
            return;
        }
        var filter = column.Type is DataGridColumnType.Select or DataGridColumnType.Boolean
            ? DataGridFilter.OneOf([DataGridSorting.TextOf(value)])
            : DataGridFilter.Text("=" + (value is decimal number ? number.ToString("0.############################", EffectiveCulture) : ClipboardText(value)));
        SetFilter(column.Key, filter);
        SetCurrentValue(IsFilterRowOpenProperty, true);
    }

    /// <summary>Deletes the rows the selection touches (undoable).</summary>
    internal void DeleteSelectedRows()
    {
        if (HasDataSource || !CanDeleteRows || !HasCells)
        {
            return;
        }
        var keys = new HashSet<object>();
        for (var row = Range.Row0; row <= Range.Row1; row++)
        {
            if (KeyOfRow(row) is { } key)
            {
                keys.Add(key);
            }
        }
        DeleteRowsByKeys(keys);
    }
}
