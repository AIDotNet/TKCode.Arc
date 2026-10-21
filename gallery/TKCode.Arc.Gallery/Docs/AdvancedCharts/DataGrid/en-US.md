---
title: Data grid
summary: A spreadsheet grid with range selection, inline editing, a fill handle, clipboard, undo, sorting, filters, grouping, totals and export.
---

<Demo name="Renewals" />

## When to use

- Editing many values in place like a spreadsheet, such as budgets, price lists or inventory.
- Data that people move to and from Excel or Google Sheets with copy and paste.
- Work that needs range selection, a fill handle, fill down and undo.
- Tables of up to tens of thousands of rows that people filter, sort, group with subtotals and total.
- Server data too large to load at once, read block by block through a data source.
- Bulk work on checked rows: copy, export, delete or your own actions.

## When not to use

- Read-only records with sorting and row selection: use `SortableDataTable`.
- Editing one record at a time: use a form with fields such as `Input` and `Select`.
- Records that need rich content such as images or long text: use cards or a list.

## Usage

Columns map to row fields by `Key`. Rows are `DataGridRow` objects (a key plus named fields) or any class read
through column bindings or accessors. Every row needs a unique, stable key. Store raw values: numbers as numbers and
days as `DateOnly`, not formatted text.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataGrid Label="Renewals" MaxViewportHeight="440" ExportFormats="All">
        <arc:DataGridColumn Key="account" Header="Account" Width="188" IsPinned="True" />
        <arc:DataGridColumn Key="stage" Header="Stage" Type="Select" />
        <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" />
        <arc:DataGridColumn Key="health" Header="Health" Type="Percent" />
        <arc:DataGridColumn Key="renewal" Header="Renews" Type="Date" />
    </arc:DataGrid>
</UserControl>
```

```csharp
grid.ItemsSource = new ObservableCollection<DataGridRow>
{
    new("acct-1") { { "account", "Lumen Labs" }, { "stage", "Proposal" }, { "arr", 48000m }, { "renewal", new DateOnly(2026, 9, 30) } }
};
```

### Column types

`Type` decides how a column reads, shows and totals its values. `Number`, `Currency` and `Percent` align right,
parse typed numbers (with currency symbols, percent signs, grouping and accounting parentheses) and are summed in the
totals row (percent is averaged). `Select` picks from `Options`, which also set the sort order. `Date` stores a
`DateOnly`, shows it with the language's month name or `DateFormat`, edits with a calendar and refuses all-number
forms such as 01/02/2026 that read differently by country. `Decimals` and `CurrencySymbol` format numbers; the
currency symbol stays the same in every interface language.

```xml
<arc:DataGrid Label="Price list">
    <arc:DataGridColumn Key="price" Header="Price" Type="Currency" CurrencySymbol="€" Decimals="2" />
    <arc:DataGridColumn Key="margin" Header="Margin" Type="Percent" Decimals="1" Aggregate="Min" />
    <arc:DataGridColumn Key="due" Header="Due" Type="Date" DateFormat="yyyy-MM-dd" />
</arc:DataGrid>
```

### Editing, clipboard and undo

Type to replace a value, or press Enter or F2 (or double click) to edit it. Enter and Tab commit and move; a value
that does not parse keeps the editor open and shakes. Copy writes tab separated text and an HTML table, so Excel and
Google Sheets read typed numbers; paste reads either, and a selection that is a whole multiple of the clipboard is
tiled. Drag the fill handle to continue a series (numbers by their step, days by days or months) and press Ctrl+D
(Cmd+D) to fill down. Undo keeps `UndoLimit` steps (100 by default). Mark computed or locked columns
`IsEditable="False"`: edits, pastes, fills and clears skip them.

<Demo name="Budget" />

`CellEditEnding` lets the app refuse a value, and `CellsChanged` reports every written cell with its old and new
value and what wrote it. Editing a value never re-sorts or re-filters, so rows stay where they are until the sort
or filters change.

```csharp
grid.CellEditEnding += (_, e) =>
{
    if (e.NewValue is decimal amount && amount < 0)
    {
        e.Cancel = true;
        e.ErrorMessage = "ARR cannot be negative";
    }
};
grid.CellsChanged += (_, e) => SaveChanges(e.Changes);
```

### Rows from your own classes

Give columns a compiled `Binding` (edits write back two-way) or accessors with `DataGridColumn.Create`, which is
AOT safe and needs no bindings. A column with a getter and no setter is read only.

<Demo name="TypedRows" />

```csharp
grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### Sorting, filters and search

Click a header to sort ascending, descending and off; Shift-click adds the column as the next sort key. The column
menu sorts, filters, groups, pins and hides. The filter row takes text (`=` for an exact match, `!` to exclude),
numbers (`>100`, `10..50`), days, months or years (`> 2026-03`, `2026-01-01..2026-03-31`) and value checklists for
select columns. The quick search matches every word across the visible columns.

### View state

Sort, filters, search, checked rows, hidden and pinned columns, density, grouping and the filter row are two-way
properties, so a view model can save and restore the view, or set it from outside.

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### Grouping and totals

`GroupBy` takes column keys, outermost first. Group rows show a toggle, the value, a row count and a subtotal under
every column whose aggregate is not `None`; the same aggregate drives the totals row, which always covers every row
the filters keep. An Excel export writes groups as outlined rows with SUBTOTAL formulas.

<Demo name="Ledger" />

### Checked rows and bulk actions

Check rows in the gutter (Shift-click checks a run, Shift+Space checks the selected rows). The toolbar then offers
Copy, Export, your `BulkActions` and Delete; a delete can be undone from the status bar.

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

### Export

`ExportFormats` lists CSV and Excel. Export writes every row the filters keep in view order (the bulk Export writes
the checked rows); `Exporting` can rename or cancel. `ExportAsync` exports from code. The CSV, xlsx, clipboard,
parsing and filter functions are public static helpers (`DataGridCsv`, `DataGridXlsx`, `DataGridClipboardText`,
`DataGridValueParser`, `DataGridFilterExpression`, `DataGridSorting`) that also run on a server.

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

### Server data

For data too large to send at once, set `DataSource`: the grid asks for the blocks that cover the rows in view
(placeholder rows show while they load), keeps up to 40 blocks, and starts over, cancelling requests in flight,
when the sort, filters or search change. The grid is then read only and does not group.

<Demo name="ServerData" />

### Loading and empty states

`IsLoading` shows six placeholder rows. `EmptyMessage` replaces "No rows yet"; when filters hide every row the grid
says so and offers Clear filters.

<Demo name="States" />

## API reference

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

## Accessibility

- The grid body is one focusable element. Arrow keys, Shift+arrows, Ctrl+arrows (Cmd on macOS), Home, End, PageUp
  and PageDown move and extend the selection; Tab leaves the grid.
- The body's accessible name follows the active cell ("column, row: value") and its help text gives the address, so
  screen readers announce each move. Avalonia has no grid automation pattern, so rows and cells are not separate
  automation elements.
- Edits, sorts, checks, copies, pastes, fills, undo and redo are announced in a polite live region.
- In a grouped grid, Alt+Left folds the group of the active row and Alt+Right unfolds it again; the group and its
  row count are announced.
- Header sort buttons name the direction, the sort priority and the resize keys (Alt+arrows). The select-all box is a
  three-state checkbox.
- Typing starts editing with the typed text. With an input method (Chinese, Japanese), start editing with Enter, F2
  or a double click first, then compose in the editor.

## Motion

- The selection and the active cell spring between ranges; scrolling and resizing columns move them at once.
- After a sort, rows slide to their new places and rows arriving from outside the view fade in.
- Pasted, filled, undone and redone cells flash a wash that fades out over 0.9s.
- The toolbar swaps to bulk actions with a short rise and fade; totals, statistics and the checked count roll.
- An invalid edit shakes once, the filter row opens on a spring and group chevrons turn.
- Reduced motion jumps the selection, drops the row slide and the shake, and keeps short fades. Off shows every
  change at once.

## Related

- `SortableDataTable` for read-only records.
- `FilterToolbar` for filters above a list or table.
- `DatePicker` and `Calendar`, which the date editor uses.
- `AnimatedCounter`, which rolls the totals.
