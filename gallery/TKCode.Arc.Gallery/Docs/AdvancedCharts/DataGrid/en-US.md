---
title: Data grid
summary: A spreadsheet grid with range selection, inline editing, a fill handle, clipboard, undo, sorting, filters, grouping, totals and export.
---

<Demo name="Renewals" />

## When to use

`DataGrid` is the control for a table that people edit in place, like a spreadsheet. It carries the toolbar,
header menus, filter row, totals row and status bar, so the table itself is the working surface.

- **Budget and forecast planning** in a finance tool: line items by quarter that a planner types into, pastes from a
  spreadsheet, fills across the months and undoes when a paste goes wrong. The totals row stays current as values change.
- **Renewal pipelines** in a customer success console: several hundred accounts with owner, region, stage, seats and
  ARR. Managers filter to one region, sort by renewal date and move stages in place.
- **Price lists and catalogs** in an e-commerce admin: prices, margins and effective dates maintained in bulk, with
  copy and paste that follows Excel and Google Sheets.
- **Journal review** in fintech or billing: ledger lines grouped by account and period with subtotals, exported to a
  workbook that keeps the outline and the SUBTOTAL formulas.
- **Large operational history** such as orders, payouts or sync logs held on a server: `DataSource` loads only the blocks
  in view.

## When not to use

- **Read-only records with sorting and row selection**, such as support tickets or users: use `SortableDataTable`. It
  needs no editing machinery.
- **Editing one record at a time**, such as a customer profile or an invoice header: use a form built from `Input`,
  `Select` and `DatePicker`, which validate each field.
- **Rows with images, long text or actions**: use `Card` or `ReorderableList`, where each item has room for its content.
- **Hierarchical data such as folders or a chart of accounts**: use `TreeView`. Grouping folds rows by key values; it
  does not build a tree from parent and child records.
- **A table that only summarizes figures on a dashboard**: use `MetricCard` or `BarChart`. The toolbar and status bar
  are too much chrome for a summary.

## Usage

Columns map to row fields by `Key`. Rows are `DataGridRow` objects (a key plus named fields) or any class read through
column bindings or accessors. Every row needs a unique, stable key. Store raw values: numbers as numbers and days as
`DateOnly`, not formatted text, so sorting, totals, filters and export all work on the values.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`DataGrid` derives from `TemplatedControl` and draws only the rows and columns in view. Its view state (sort, filters,
search, checked rows, hidden and pinned columns, density and grouping) is two-way, so a view model can save and restore it.

### Anatomy

1. **Toolbar** (`ShowToolbar`): 52 px high, with 32 px buttons and a 220 px search field. It offers search, undo and
   redo, Filter, Columns, density and Export. Checked rows switch it to the bulk state. Below 640 px the buttons show
   icons only; below 460 px the row count and undo and redo are hidden.
2. **Header row**: 40 px. Each cell shows the `Header`, a sort button and a menu button for sort, filter, group, pin and
   hide. Numeric columns right-align their header.
3. **Filter row** (`IsFilterRowOpen`): 40 px, opened from Filter or a column menu. An invalid expression marks its cell.
4. **Gutter** (`ShowRowNumbers`, `IsRowCheckEnabled`): 48 px with the row number and a checkbox. With
   `ShowRowNumbers="False"` it narrows to 40 px and the checkboxes stay visible.
5. **Body rows** (`Density`): 32 px in `Compact` (13 px text), 40 px in `Standard` and 48 px in `Comfortable`, separated
   by a 1 px subtle line.
6. **Selection, active cell and fill handle**: the selection fills with the accent color at 9% opacity and has a 1 px
   accent outline; the active cell has a 2 px accent ring. The fill handle is an 8 px accent square at the selection's
   bottom right, with a 2 px surface ring.
7. **Totals row** (`ShowTotals`): 40 px and sticky at the bottom. It covers every row the filters keep, including rows in
   collapsed groups.
8. **Status bar**: 34 px. It shows the selection address with Sum, Average and Count (`ShowSelectionStats`), the row count
   (for example "12 of 120 rows" while filtering) and messages. A message that offers Undo stays for 6 seconds; others
   stay for 2.4 seconds.
9. **Pinned columns** (`IsPinned`): they stay at the left edge while the body scrolls, with a 10 px edge shadow.

### Column types

`Type` decides how a column reads, shows, parses and totals its values. Number, currency and percent columns align right
and parse typed numbers, including currency symbols, percent signs, grouping and accounting parentheses such as `(12)`
for -12.

| Type | Stores | Shows | Default total | Input accepts |
|---|---|---|---|---|
| `Text` | `string` | The text | None | Any text |
| `Number` | `decimal` | Grouped digits with `Decimals` places | Sum | Numbers |
| `Currency` | `decimal` | `CurrencySymbol` (default `$`) and grouped digits | Sum | Numbers, with or without the symbol |
| `Percent` | `decimal` | The value and a percent sign | Average | Numbers, with or without the sign |
| `Select` | `string` | The option text | None | One of `Options`, ignoring case |
| `Date` | `DateOnly` | The language's month name, or `DateFormat` | None | Calendar picks, ISO days, month names |
| `Boolean` | `bool` | A checkbox | None | Yes, no, true, false, 1, 0, 是, 否 |

`Select` sorts by the order of `Options`, not alphabetically. `Date` refuses all-number forms such as 01/02/2026, which
read differently in different countries; write `2026-02-01` or use the month name. The currency symbol does not change
with the interface language, so a euro column stays in euros when the app switches languages.

`Aggregate` takes `None`, `Sum`, `Average`, `Min`, `Max` or `Count` (non-empty cells), for example `Min` for a margin review.

A `Boolean` column draws a checkbox in the middle of the cell. Click it, or press Space, Enter or F2, to flip the value;
typing yes or no sets it. A flip is an ordinary edit: `CellEditEnding` can refuse it, each one is an undo step, and a
read-only column does not flip. Copy and CSV write TRUE and FALSE, an Excel export writes boolean cells, and the filter
offers Yes and No.

### Cell renderers

By default a column draws its value as text. Set `Renderer` to draw something richer instead:

| Renderer | Draws | Settings |
|---|---|---|
| `BadgeRenderer` | A small pill colored by the value, for a stage or a status | `ToneMap` (or `Tones`), `DefaultTone` |
| `ProgressRenderer` | A bar with the value's text at its right edge | `Minimum`, `Maximum`, `ShowText`, `Tone` |
| `LinkRenderer` | An accent link; a click raises `CellInvoked` and does not select the cell | `Part` |
| `IconTextRenderer` | An icon chosen by the value, then the text | `Icons`, `DefaultIcon`, `Tones`, `ShowText` |
| `CheckboxRenderer` | A checkbox in the middle of the cell; a click flips the value (Boolean columns use it by default) | None |
| `ActionsRenderer` | A row of icon buttons, such as Edit and Delete at the end of a row; a click runs the action's `Command` with the row and raises `CellInvoked` | `Actions` |

<Demo name="CustomCells" />

```xml
<arc:DataGrid Label="Deals">
    <arc:DataGridColumn Key="stage" Header="Stage" Type="Select">
        <arc:DataGridColumn.Renderer>
            <arc:BadgeRenderer ToneMap="Won=Success, Proposal=Warning, Lost=Danger" />
        </arc:DataGridColumn.Renderer>
    </arc:DataGridColumn>
</arc:DataGrid>
```

The grid draws renderers together with the rest of the row, the way it draws text, so a table with thousands of rows stays
smooth; they are not controls. A renderer changes how a cell looks, not what it holds: sorting, filters, totals, export and
editing still work on the value, and copy and screen readers read `GetText`. Colors come from `DataGridTone` (`Neutral`,
`Accent`, `Success`, `Warning`, `Danger`), which follows the theme and the accent in light and dark.

To draw something else, derive from `DataGridCellRenderer` and override `Render`. One renderer is shared by every cell of
its column, so keep no state per cell.

```csharp
public sealed class RatingRenderer : DataGridCellRenderer
{
    public override void Render(in DataGridCellRenderContext context)
    {
        var stars = context.Value is decimal value ? (int)value : 0;
        context.DrawText(new string('★', stars), context.Bounds.X, context.Bounds.Width, context.Palette.Accent);
    }

    public override string GetText(object? value, DataGridCellFormat format, CultureInfo culture) =>
        value is decimal d ? $"{d:0} of 5" : string.Empty;
}
```

Draw text with `DrawText`, which keeps each line laid out between frames; use `MeasureText` with the same arguments to
size things around it, and take brushes from `Palette`. Override `OnPointerPressed` (and `IsInteractiveAt` for the hand
cursor) to take clicks, and call `Invoke` to raise `CellInvoked`.

To edit a column's cells with your own control, set `EditTemplate`. The template builds the content for a
`DataGridEditContext`: read and write its `Value`, and Enter, Tab or Escape commit or cancel the edit (or call `Commit()`
and `Cancel()`). Only the cell being edited is a real control, so the grid stays fast. The value takes the path that
typing takes: it must fit the column's type, and `CellEditEnding` can refuse it.

```csharp
column.EditTemplate = new FuncDataTemplate<DataGridEditContext>((context, _) =>
{
    var slider = new Slider { Minimum = 0, Maximum = 100, Value = context.Value is decimal v ? (double)v : 0 };
    slider.ValueChanged += (_, e) => context.Value = Math.Round((decimal)e.NewValue);
    return slider;
});
```

### Editing, clipboard and undo

Type to replace a value, or press Enter, F2 or double click to edit it and keep the value, with the caret at the end.
Enter commits and moves down, Shift+Enter moves up, and Tab commits and moves right. Escape cancels. A value that does not
parse keeps the editor open with the text in the danger color and a short shake. Clicking outside the grid commits a
valid value and drops an invalid one.

Copy writes tab separated text and an HTML table, so Excel and Google Sheets read the typed numbers. Paste reads either
format, and a selection that is a whole multiple of the clipboard is tiled. Drag the fill handle to continue a series:
numbers by their step, days by days or by months. Ctrl+D fills down. Undo keeps `UndoLimit` steps (100 by default; 0
turns undo off). Mark computed or locked columns `IsEditable="False"`: edits, pastes, fills and clears skip them. A paste
that holds more than `MaxPasteCells` cells (1,000,000 by default) is refused with a message, so a huge or hostile table
cannot exhaust memory.

<Demo name="Budget" />

`CellEditEnding` runs before a value is written, so the app can refuse it. `CellsChanged` runs after every write,
undo and redo included, with each cell's old and new value and a `Reason` (`Edit`, `Paste`, `Cut`, `Clear`, `Fill`,
`FillDown`, `Undo` or `Redo`). Editing never re-sorts or re-filters, so rows stay where they are.

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

### Validation, locked cells and conditional styles

Give a column `Rules` to check what people type, paste or fill. The built-in rules are `RequiredRule`, `RangeRule`
(`Minimum`, `Maximum`), `MaxLengthRule` and `PatternRule`; `DataGridRule.Custom` takes a function that returns the message,
or null when the value passes. Messages follow the interface language, or set `Message`.

By default (`ValidationMode="Block"`) a failing value is refused: the editor stays open with the message, and pasted or
filled cells that fail are skipped and counted in the status bar. Clearing a cell, undo and redo are never blocked. With
`ValidationMode="Flag"` the value is accepted and the cell is outlined in red with a dot; the toolbar shows how many are
flagged (click it, or press F8, to jump to the next one), and selecting a flagged cell announces its message.
`ValidateAll()` checks every row, for data that arrives already wrong, and `GetErrors()` lists the flagged cells.

<Demo name="Validation" />

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="qty" Header="Qty" Type="Number">
        <arc:DataGridColumn.Rules>
            <arc:RequiredRule />
            <arc:RangeRule Minimum="1" Maximum="99" />
        </arc:DataGridColumn.Rules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

`IsEditable` locks a whole column. To lock rows or single cells set `CanEditCell`, a function of the cell (its row, key,
column and value): edits, pastes, fills and clears skip the cells it refuses.

To draw attention to values, give a column `FormatRules`. Each rule tests the value (`GreaterThan`, `LessThan`, `Between`,
`Equal`, `Contains`, `IsEmpty`, `Negative` and more) and tints the cell with a tone (`Neutral`, `Accent`, `Success`,
`Warning` or `Danger`), optionally in bold; the first rule that matches wins. Compared values are written as they would be
typed into the column, such as `$1,000` or `2026-06-01`.

```xml
<arc:DataGrid Label="Orders">
    <arc:DataGridColumn Key="due" Header="Due" Type="Date">
        <arc:DataGridColumn.FormatRules>
            <arc:DataGridFormatRule Condition="LessThan" Value="2026-06-01" Tone="Danger" />
        </arc:DataGridColumn.FormatRules>
    </arc:DataGridColumn>
</arc:DataGrid>
```

For logic that rules cannot express, set `CellStyleSelector` (per cell) or `RowStyleSelector` (per row). Both run for every
cell in view on every frame, so keep them cheap.

### Adding rows

Set `CanInsertRows` to show Add row in the toolbar and to allow `AddRow()` and `InsertRow()`; Ctrl+Shift+Plus inserts above
the active row. Rows of `DataGridRow` need nothing more. Typed rows need a `RowFactory` that returns a new object with a key
of its own, and `ItemsSource` must be a writable list. An added row is one undo step, `RowsInserted` reports it, and undoing
it raises `RowsDeleted`. Set `PasteOverflow="AppendRows"` to make a paste that runs past the last row add the rows it needs
instead of dropping them.

```csharp
grid.CanInsertRows = true;
grid.RowFactory = () => new Account(Guid.NewGuid().ToString("N"));
```

### Columns, saved views and cell events

Drag a header label to move its column; a line shows where it will land. Alt+Shift+Left and Alt+Shift+Right in a header
do the same from the keyboard. Pinned columns reorder among themselves. `ColumnOrder` holds the keys in display order and
`ColumnWidths` the widths people set; both are two-way, like the rest of the view state. Double click a header's right
edge, or choose Fit column width in the column menu, to size a column to its widest cell and its header (the first 1,000
rows are measured). `AutoFitColumns()` does every column and `ResetColumnWidths()` undoes them.

To save a view, `GetLayoutState()` returns a `DataGridLayoutState` (column order and widths, pinned and hidden columns,
sort, filters, grouping and density), and `ToJson()` writes it with a serializer that is safe for trimming and AOT.
`SetLayoutState()` restores it: fields the state lacks fall back to the defaults, and keys of columns that no longer exist are
ignored, so a saved view survives a change of columns.

```csharp
File.WriteAllText(path, grid.GetLayoutState().ToJson());

// later, when the window opens:
if (DataGridLayoutState.FromJson(File.ReadAllText(path)) is { } state)
{
    grid.SetLayoutState(state);
}
```

The grid raises events for the cells people touch. `CellClick` fires when a body cell is pressed with the primary button,
after the selection moved. `CellDoubleClick` fires before the editor opens; set `Handled` to keep it closed.
`RowActivated` fires for Enter on a cell that cannot be edited and for a double click on the row number, which is the
"open this row" gesture. `CellContextRequested` fires for a right click, Shift+F10, the menu key and a long press; set
`Handled` to show your own menu.

Otherwise the grid selects the cell (when it is outside the selection) and opens a menu: Cut, Copy, Copy with headers and
Paste; Insert row above or below (with `CanInsertRows`) and Delete rows (with `CanDeleteRows`); Clear contents and Fill down;
Filter by this value, Sort and Group by. Add entries with `ContextMenuItems`, each running its `Command` with the clicked
row; change them for one opening with `ContextMenuOpening` (`Items`, `Cancel`); or turn the menu off with
`ShowContextMenu="False"`.

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.ContextMenuItems>
        <arc:DataGridMenuItem Label="Open account" />
    </arc:DataGrid.ContextMenuItems>
</arc:DataGrid>
```

### Find and replace

Ctrl+F opens the find bar at the top right of the body, and Ctrl+H adds the replace row (the chevron does too). It looks
in the text people see, so `1,200` finds a cell that shows `$1,200`. While the bar is open every match is tinted and the bar
counts them ("3 of 27"). Enter and Shift+Enter, or F3 and Shift+F3, step to the next and previous match, wrapping round;
Escape closes the bar. `Match case` and `Match entire cell` narrow the search.

Replace swaps the text in the active match and moves to the next one; Replace all changes every match as one undo step.
The new text is read back the way typing reads it, so replacing inside a currency or date cell works when the result is
still a valid value, and a cell it would break (an option that does not exist, text in a number column) is skipped and
counted in the status bar. Read-only cells are skipped too. With a `DataSource` Ctrl+F focuses the toolbar search instead,
since only the blocks in view are loaded.

```csharp
grid.FindText = "labs";
grid.FindMatchCase = false;
grid.ReplaceAll("Group");   // one undo step; cells whose new text does not parse are skipped
```

### Rows from your own classes

Give a column a compiled `Binding` (edits write back two-way), or create it with `DataGridColumn.Create`, which uses
accessors, needs no bindings and is AOT safe. A column without a setter is read only. Both paths receive normalized
values: a string, a `decimal`, a `DateOnly` or null.

<Demo name="TypedRows" />

```csharp
public sealed class Account { public decimal Arr { get; set; } public int Seats { get; set; } }

grid.Columns.Add(DataGridColumn.Create<Account>("arr", "ARR", a => a.Arr, (a, v) => a.Arr = v as decimal? ?? 0,
    DataGridColumnType.Currency));
grid.Columns.Add(DataGridColumn.Create<Account>("total", "Total", a => a.Arr * a.Seats, type: DataGridColumnType.Currency));
```

### Sorting, filters and search

Click a header to sort ascending, descending, then off. Shift-click adds the column as the next sort key, or flips its
direction when it is already a key. Empty values sink to the bottom either way. The filter row takes these expressions:

- **Text**: `abc` contains the text, `=abc` matches exactly and `!abc` excludes it. Matching ignores case and accents.
- **Numbers, currency and percent**: `>100`, `<5`, `=12`, `!=0`, or a closed range such as `10..50`.
- **Dates**: `2026-01-31` is a day, `2026-03` a month and `2026` a year. A prefix such as `> 2026-03` compares by the
  boundary, so it means after March 2026. A range is `2026-01-01..2026-03-31`.
- **Select**: a checklist of values, with a search box above seven values and an entry for blank cells.

An expression that cannot be read marks its cell invalid and does not apply. A filter on a hidden column still applies
and still counts on the Filter button. Quick search (`SearchText`) splits the text into words; each word must appear in
some visible column.

### View state

Sort, filters, search, checked rows, hidden and pinned columns, density and grouping are two-way properties, so a view
model can save, restore or reset a view. `ClearFilters()` clears the column filters and the quick search.

<Demo name="ControlledState" />

```xml
<arc:DataGrid SortDescriptions="{Binding Sort}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding Checked}"
              Density="{Binding Density}" />
```

### Grouping and totals

`GroupBy` takes column keys, outermost first. A group row shows a toggle, the value, a row count and a subtotal under
every column whose aggregate is not `None`. Group on columns with few distinct values; numeric columns are not groupable
by default, and `IsGroupable="True"` offers grouping on one.

<Demo name="Ledger" />

An Excel export writes the groups as outlined rows with SUBTOTAL formulas, so the reader can fold them in Excel too.

### Checked rows and bulk actions

Check rows in the gutter: Shift-click checks a run, and Shift+Space checks the rows under the selection. Once a row is
checked, the toolbar offers Copy, Export, your `BulkActions` and Delete (when `CanDeleteRows` is on). Delete records the
row positions, so Undo puts the rows back in order; the status bar offers the same undo for 6 seconds.

```xml
<arc:DataGrid Label="Accounts">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" Command="{Binding ArchiveCommand}" />
        <arc:DataGridBulkAction Label="Remove from plan" IsDanger="True" Command="{Binding RemoveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

The command receives the checked rows as an `IReadOnlyList<object>` in data order. The grid also raises
`BulkActionInvoked` with the action and the rows. Labels hide below 640 px of grid width, so give every action an icon.

### Export

`ExportFormats` lists `Csv`, `Xlsx` or both (`All`); with more than one, Export opens a menu. Export writes every row the
filters keep, in view order, and the bulk Export writes only the checked rows. `Exporting` can rename or cancel the file,
and `ExportAsync` exports from code. Export is disabled where the host cannot show a save dialog.

A CSV opened in Excel or Sheets runs text that starts with `=`, `+`, `-` or `@` as a formula, which lets a hostile cell
read data or start programs (CSV injection). The grid therefore writes such text with a leading single quote (`'=1+1`), and
the quote stays visible in the cell. Numbers and days are never prefixed, and an Excel export always stores text as text.
Set `NeutralizeCsvFormulas="False"` only when the file never reaches a spreadsheet and the text must round-trip byte for
byte.

```csharp
grid.Exporting += (_, e) => e.FileName = $"renewals-{DateTime.Today:yyyy-MM-dd}";
await grid.ExportAsync(DataGridExportFormat.Xlsx);
```

The writers, clipboard helpers and parsers are public static classes (`DataGridCsv`, `DataGridXlsx`,
`DataGridClipboardText`, `DataGridValueParser`, `DataGridFilterExpression`, `DataGridSorting`) that a server can reuse.

### Server data

For data too large to send at once, set `DataSource` to an `IDataGridDataSource`. The grid requests the blocks that cover
the rows in view, shows placeholder rows while they load, and keeps up to 40 blocks. When the sort, filters or search
change, it starts over and cancels the requests in flight. The grid is then read only, does not group, hides Select all
and exports only checked rows. Set `RowKeySelector` as well, which is required with a data source.

<Demo name="ServerData" />

### Loading and empty states

`IsLoading` shows six placeholder rows, and the totals row reads "Loading". `EmptyMessage` replaces the built-in "No rows
yet" when there are no rows at all. When the filters hide every row, the grid says so and offers Clear filters.

<Demo name="States" />

### Binding to a view model

In an MVVM app the view model owns the rows, the view state and the commands. With CommunityToolkit.Mvvm, the rows are an
`ObservableCollection<DataGridRow>`, and the view state is observable properties that the grid writes back into.

```csharp
public partial class RenewalsViewModel : ObservableObject
{
    private readonly IRenewalService _renewals;

    public RenewalsViewModel(IRenewalService renewals)
    {
        _renewals = renewals;
    }

    public ObservableCollection<DataGridRow> Renewals { get; } = new();

    [ObservableProperty]
    private IReadOnlyList<DataGridSort> _sortDescriptions = [];

    [ObservableProperty]
    private IReadOnlyDictionary<string, DataGridFilter> _filters = new Dictionary<string, DataGridFilter>();

    [ObservableProperty]
    private IReadOnlyList<object> _checkedRowKeys = [];

    [ObservableProperty]
    private DataGridDensity _density = DataGridDensity.Standard;

    [RelayCommand]
    private async Task ArchiveAsync(IReadOnlyList<object> rows)
    {
        await _renewals.ArchiveAsync(rows.Cast<DataGridRow>().Select(r => r.Key));
    }
}
```

```xml
<arc:DataGrid Label="Renewals"
              ItemsSource="{Binding Renewals}"
              SortDescriptions="{Binding SortDescriptions}"
              Filters="{Binding Filters}"
              CheckedRowKeys="{Binding CheckedRowKeys}"
              Density="{Binding Density}">
    <arc:DataGrid.BulkActions>
        <arc:DataGridBulkAction Label="Archive" Command="{Binding ArchiveCommand}" />
    </arc:DataGrid.BulkActions>
</arc:DataGrid>
```

- `SortDescriptions`, `Filters`, `CheckedRowKeys`, `Density`, `SearchText` and `GroupBy` are two-way: header clicks update
  the view model, and changes in the view model update the grid.
- `ItemsSource` is one-way. Edits write into the row objects, and the grid raises `CellsChanged` for each write. Save
  changes in a `CellsChanged` handler in code-behind, since it is an event and not a command.

### Data shape and performance

Every row needs a stable key (`DataGridRow.Key`, or a `RowKeySelector` for typed rows) that never changes, since the
key identifies checked rows and scroll targets. The design targets about 10,000 rows in client mode as smooth and about
50,000 as usable; above that, use `DataSource`. Store numbers as `decimal` and days as `DateOnly`, and set `Culture` for
figures that belong to a fixed locale.

### Example: renewal review for a customer success team

A customer success manager reviews the next two quarters of renewals. The pinned account column keeps the name in view.
Stage is a `Select` with an ordered option list, so the sort follows the pipeline. ARR and region are read only because the
billing export owns them; an edit here would be overwritten at the next import. The `Min` health total shows the weakest
account in view, and `CanDeleteRows="False"` keeps accounts until billing closes them.

```xml
<arc:DataGrid Label="Renewals Q4 2026"
              MaxViewportHeight="440"
              ExportFormats="All"
              ExportFileName="renewals-q4-2026"
              CanDeleteRows="False">
    <arc:DataGridColumn Key="account" Header="Account" Width="188" MinWidth="120" IsPinned="True" IsEditable="False" />
    <arc:DataGridColumn Key="owner" Header="Owner" Width="148" />
    <arc:DataGridColumn Key="region" Header="Region" Type="Select" Width="144" IsEditable="False" />
    <arc:DataGridColumn Key="stage" Header="Stage" Type="Select" Width="140" Options="{Binding StageOptions}" />
    <arc:DataGridColumn Key="seats" Header="Seats" Type="Number" Width="96" Aggregate="Sum" />
    <arc:DataGridColumn Key="arr" Header="ARR" Type="Currency" CurrencySymbol="$" Width="128" IsEditable="False" />
    <arc:DataGridColumn Key="health" Header="Health" Type="Percent" Width="104" Aggregate="Min" />
    <arc:DataGridColumn Key="renewal" Header="Renews" Type="Date" DateFormat="yyyy-MM-dd" Width="132" />
</arc:DataGrid>
```

### Content guidelines

- **Header**: a short noun phrase in sentence case, such as "Renews" or "ARR". Add the unit when the cells do not show it
  ("ARR (USD)"). The header is the column's accessible name and the export column title.
- **Label**: name the table for the person who opens it. It is the accessible name and the default export file name.
- **Bulk action**: a verb such as "Archive". Use `IsDanger` for destructive actions, and "Remove from plan" when the record
  stays in another system.

| Instead of | Write |
|---|---|
| Amt | ARR (USD) |
| Save file | Export as Excel |
| Error | ARR cannot be negative |
| Delete all now | Archive checked accounts |

### Best practices

**Do**

- Store raw values (decimals, `DateOnly`, option strings) and let the column format them, so sorting, totals and export
  match what the person sees.
- Key every row with a unique, stable value. A sort or a deletion moves rows, and only a stable key keeps the checks on
  the right rows.
- Pin the column that identifies the row, usually the name, so it stays visible when the grid scrolls sideways.
- Mark imported or computed columns `IsEditable="False"` rather than relying on people to leave them alone.
- Validate in `CellEditEnding` with a specific message. A refused edit keeps the editor open, so the person can fix it
  where they are.

**Avoid**

- `DataSource` for a few hundred rows. It makes the grid read only and disables grouping, which the client mode supports
  at that size.
- Formatting numbers into strings before they reach the grid. A string sorts as text and the totals ignore it.
- Several editable grids with bulk actions on one screen. Each has its own checked set, and people lose track of which
  one a bulk action affects.

### Common questions

**Why does a row stay in place after I change a value that is sorted?** Editing never re-sorts or re-filters, so the row
keeps its position until the sort or the filters change. Click the header again, or change the filter, to reorder.

**How do I undo a bulk delete from code?** Call `Undo()`, which restores the last edit, fill, paste, clear or row delete.
`CanUndo` says whether a step exists, and `ClearHistory()` forgets all steps, for example after a successful save.
`UndoLimit` sets how many steps are kept.

## API reference

<!-- api: DataGrid -->

<!-- api: DataGridColumn -->

<!-- api: DataGridBulkAction -->

<!-- api: DataGridCellRenderer -->

<!-- api: BadgeRenderer -->

<!-- api: ProgressRenderer -->

<!-- api: LinkRenderer -->

<!-- api: IconTextRenderer -->

<!-- api: CheckboxRenderer -->

<!-- api: DataGridCellAction -->

<!-- api: ActionsRenderer -->

<!-- api: DataGridEditContext -->

<!-- api: RequiredRule -->

<!-- api: RangeRule -->

<!-- api: MaxLengthRule -->

<!-- api: PatternRule -->

<!-- api: DataGridRule -->

<!-- api: DataGridFormatRule -->

<!-- api: DataGridCellContext -->

<!-- api: DataGridMenuItem -->

<!-- api: DataGridCellEventArgs -->

<!-- api: DataGridContextMenuOpeningEventArgs -->

<!-- api: DataGridLayoutState -->

<!-- api: DataGridFilterState -->

<!-- api: DataGridCellRenderContext -->

<!-- api: DataGridCellPointerContext -->

<!-- api: DataGridRenderPalette -->

<!-- api: DataGridCellInvokedEventArgs -->

Server data comes from an `IDataGridDataSource`. The grid calls `GetRowsAsync` for each block it needs.

| Member | Description |
|---|---|
| `int BlockSize` | Rows per request. The default is 100. |
| `Task<DataGridRowsResult> GetRowsAsync(DataGridRowsRequest request, CancellationToken cancellation)` | Returns one block of the sorted, filtered and searched rows. |

`DataGridRowsRequest` carries `Start` (inclusive), `End` (exclusive), `Sort`, `Filters` and `Search`. The result carries
`Rows`, `RowCount` (the whole filtered set) and `Totals` (by column key, or null to keep the previous totals).

## Accessibility

- The grid body is one focusable element. Arrow keys, Shift with arrows, Home, End, PageUp and PageDown move and extend
  the selection; Tab leaves the grid.
- The body's accessible name follows the active cell ("column, row: value"), and its help text gives the address, so
  screen readers announce each move. Avalonia has no grid automation pattern, so rows and cells are not separate nodes.
- Edits, sorts, checks, copies, pastes, fills, undo and redo are announced in a polite live region.
- Header sort buttons name the direction and the priority; the select-all box is a three-state checkbox.
- Typing starts editing with the typed text. For an input method such as Chinese or Japanese, press Enter, F2 or
  double click first, then compose in the editor.
- Narrator, NVDA and VoiceOver are untested with this control; verify the announcements on each platform.

### Keyboard

The grid body is the focus stop. These keys apply when no editor is open; Ctrl means Cmd on macOS.

| Keys | Action |
|---|---|
| Arrow keys | Move the active cell and collapse the selection to it |
| `Shift` + arrow keys | Extend the selection from the anchor cell |
| `Ctrl` + arrow keys | Jump to the first or last row or column in that direction; add Shift to extend |
| `Home` / `End` | First or last column of the row; `Ctrl+Home` and `Ctrl+End` go to the first and last cell |
| `Ctrl+Space` | Extend the selection to the whole column |
| `Shift+Space` | Check the rows under the selection, or uncheck them when all are checked |
| `Enter` / `F2` | Edit the active cell and keep its value |
| Printable character | Start editing and replace the value with the typed text |
| `Alt+Down` | On a `Select` or `Date` column, open the option list or the calendar |
| `Alt+Left` / `Alt+Right` | In a grouped grid, fold or unfold the group of the active row |
| `F8` | Select the next cell flagged by validation |
| `Ctrl+Shift+Plus` | Insert a row above the active row (with `CanInsertRows`) |
| `Shift+F10` / menu key | Open the context menu on the active cell |
| `Escape` | Collapse a multi-cell selection to the active cell; otherwise clear the row checks |
| `Delete` / `Backspace` | Clear the selection; read-only cells are skipped |
| `Ctrl+D` | Fill down from the first row of the selection |
| `Ctrl+F` / `Ctrl+H` | Open find, or find and replace |
| `F3` / `Shift+F3` | Next or previous match |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | Copy, cut and paste |
| `Ctrl+Z` / `Ctrl+Shift+Z` or `Ctrl+Y` | Undo and redo |
| `Tab` / `Shift+Tab` | Leave the grid |

While an editor is open, Enter and Shift+Enter commit and move down or up, Tab and Shift+Tab commit and move right or
left, and Escape cancels. On a header, Alt+Left and Alt+Right narrow and widen the column by 16 px, and Alt+Shift+Left and Alt+Shift+Right move it.

## Motion

- After a sort, rows slide to their new places within 700 ms, and rows that arrive from outside the view fade in.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Selection and active cell | Spring between ranges | Jump to the new range | Jump to the new range |
| Rows after a sort | Slide to the new place within 700 ms | Jump to the new place | Jump to the new place |
| Pasted, filled, undone or redone cells | 0.9 s wash that fades out | Short fade | Immediate |
| Toolbar bulk state and status messages | Blur and vertical swap | Fade only | Immediate |
| Totals, selection statistics and checked count | Digits roll | Digits roll by the counter rules | Immediate |
| Filter row and group chevrons | Spring open, chevron turns | Height jumps, opacity fades | Immediate |

## Related

| Component | Choose it when |
|---|---|
| `SortableDataTable` | The records are read-only, and people sort, select and page through them |
| `Input` / `Select` / `DatePicker` | People edit one record at a time, with validation per field |
| `Card` / `ReorderableList` | Each item has its own content, images or actions |
| `TreeView` | The data is hierarchical and people expand branches |
| `BarChart` / `MetricCard` | The point is a figure or a trend, not the individual rows |
