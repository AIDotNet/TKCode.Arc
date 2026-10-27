---
title: Tree table
summary: A table with nested rows, tri-state selection, hierarchy-preserving sort, and lazy-loaded children.
---

<Demo name="Budget" />

## When to use

`TreeTable` shows records that nest and share columns. A row can open to reveal its children, every row keeps the
same columns, and a parent can roll up what is under it. People expand the branches they care about, sort within a
branch, and select a whole branch with one checkbox.

- **File listings with size, owner and date columns**: folders open to their files, and sorting by size orders the
  files inside each folder without pulling them out of it.
- **Budgets and cost reports** that roll up through nested rows: a department holds teams, a team holds line items,
  and a footer can total the rows a person selected.
- **Org charts and permission matrices**: teams contain people, and each row carries an owner, a role or a count.
- **Large trees that load on demand**: a row can be marked as having children that are not loaded yet, and the table
  fetches them the first time the row opens, with a retry row when the request fails.
- **Bulk actions on a hierarchy**: checking a parent checks everything under it, and a parent shows a mixed state
  when only some of its children are checked.

## When not to use

- **A navigation tree without columns**: use `TreeView`, which draws one label per node and virtualizes thousands of
  rows.
- **Flat records that people compare by column**: use `Table`. A tree table adds indent and a
  chevron to the first column for no benefit when nothing nests.
- **A spreadsheet that people edit cell by cell**: use `DataGrid`, which has range selection and inline editing.
- **Items that open into free-form content**: use `Accordion`. A tree table opens into more rows with the same
  columns.
- **A few thousand rows open at once**: every visible row exists and animates. Keep an expanded tree to a few hundred
  rows and load the deeper levels on demand.

## Usage

Set `ItemsSource` to the top-level rows and `ChildrenSelector` (a delegate, set from code) to return the children of a
row. A row whose selector returns an empty list is a leaf. Add columns to `Columns`; the first column shown holds the
tree.

```xml
<arc:TreeTable Caption="Files"
               ItemsSource="{Binding Files}"
               IsSelectable="True"
               MaxHeight="480">
    <arc:TreeTableColumn Header="Name" Binding="{Binding Name}" />
    <arc:TreeTableColumn Header="Size" Binding="{Binding SizeMb}" StringFormat="{}{0} MB"
                         Width="96" IsEndAligned="True" />
    <arc:TreeTableColumn Header="Owner" Binding="{Binding Owner}" HideBelow="520" />
</arc:TreeTable>
```

`TreeTable` derives from Avalonia's `TemplatedControl`. Like Arc, it does not virtualize: every visible row is a real
row that grows in, leaves and animates. Selectors such as `ChildrenSelector` and `KeySelector` take a delegate, so
they are set from code; the rest of the table can be written in XAML.

```csharp
var table = new TreeTable
{
    ItemsSource = files,
    ChildrenSelector = item => ((FileNode)item).Children,
    KeySelector = item => ((FileNode)item).Path,
    RowLabelSelector = item => ((FileNode)item).Name,
    IsSelectable = true
};
table.Columns.Add(TreeTableColumn.Create<FileNode>("Name", f => f.Name));
table.Columns.Add(new TreeTableColumn { Header = "Size", ValueSelector = f => ((FileNode)f).SizeMb, IsEndAligned = true });
```

### Anatomy

1. **Frame**: the surface background with a 1 px border in the border color and the panel corner radius. The table
   fills its width. `MaxHeight` caps the whole table: the header stays pinned and the body scrolls.
2. **Header**: 40 px tall, in the small type size and the muted text color, with a 1 px rule below it. A sortable
   header is a button; the first column's header also holds the expand all toggle.
3. **Row**: 44 px tall with a hairline below it. Hover and focus fill the row with the muted surface color, and a
   selected row uses the accent subtle color.
4. **Tree cell**: the first column shown. It starts 10 px in, indents 20 px for each level, and has a 24 px chevron
   slot before the content. A leaf keeps the empty slot so labels line up.
5. **Indent guides**: a 1 px line through the chevron column of every ancestor. The guide of the branch under the
   pointer or the focused row darkens.
6. **Cells**: one per other shown column, with 12 px inline padding. An end-aligned column right-aligns its text and
   sets figures in tabular width.
7. **Checkbox column** (`IsSelectable`): 44 px wide, with a select all checkbox in the header.
8. **Loading and retry rows**: under a parent that is fetching, a spinner row with skeleton bars in the other
   columns, or a "Couldn't load" row with a Retry button.

### Columns

`TreeTableColumn` defines a column. The cell value comes from `ValueSelector` (a delegate, safe for AOT) or `Binding`,
and `Formatter` or `StringFormat` turns it into text. For anything other than text, set `CellTemplate`; its data
context is the row item.

| Property | Use it to |
|---|---|
| `Header`, `Key` | Label the column, and name it for `SortKey` (the header is the key when `Key` is not set) |
| `Width` | Set a pixel width (`96`) or a share of the rest (`2*`); the default is `1*`, which shrinks and truncates |
| `IsEndAligned` | Right-align numbers and amounts |
| `HideBelow` | Hide the column while the table is narrower than this many pixels |
| `SortValueSelector`, `IsSortable` | Sort by something other than the displayed text, or turn sorting off |

Columns share one set of tracks, so they line up through every level. When the table gets narrower than a column's
`HideBelow`, the column is removed from the header and every row; the tree column always stays.

### Row identity

`KeySelector` returns the identity of a row. `ExpandedKeys` and `SelectedKeys` hold these keys, so use something
stable such as an id or a path. Without a `KeySelector` the row item itself is the key.

### Expanding and collapsing

A click on a parent row, a click on its chevron, Enter, and the arrow keys all toggle it. `ExpandedKeys` is two-way:
set it to open rows from code or bind it to a view model, and read it to save the open state. The header's toggle
opens every row whose children are loaded, or closes them all.

<Demo name="Controlled" />

```csharp
table.Expand(item);
table.Collapse(item);
table.ExpandAll();          // every row whose children are known
table.CollapseAll();
table.FocusItem(item);      // moves keyboard focus to a visible row
```

### Selection

With `IsSelectable`, each row gets a checkbox. Checking a parent checks everything under it, and a parent whose
children are only partly checked shows the mixed state. `SelectedKeys` always holds a normalized set: a parent is in
it exactly when all its known children are. The select all checkbox is mixed when only some rows are selected.

`SelectionChanged` reports the new keys and the loaded row items. A parent rolls up its children, so a total over
the selection should count only the leaves.

```csharp
table.SelectionChanged += (_, e) =>
{
    var leaves = e.SelectedItems.Cast<Spend>().Where(s => s.Children is not { Count: > 0 });
    footer.Text = $"{leaves.Sum(s => s.Monthly):C0} per month";
};
```

### Sorting

A header with a value cycles ascending, descending, then off. Sorting is hierarchy-safe: the siblings of each parent
are sorted inside that parent, so a child never leaves its branch. Numbers compare as numbers, text with natural
order, and empty values sort last in both directions. `SortKey` and `SortDirection` are two-way, so the sort can be
set from code or saved.

<Demo name="Files" />

### Loading children on demand

Mark a row that has children which are not loaded with `HasChildrenSelector`, and give the table a `ChildrenLoader`.
The first time such a row opens, the table calls the loader and shows a loading row under it. When the task finishes,
the first child takes over the loading row's place. When the task faults, a "Couldn't load" row with a Retry button
appears, `ChildrenLoadFailed` is raised, and Enter on the parent retries. Children that arrive under a checked parent
start checked. A row loads once: opening it again does not fetch again.

<Demo name="LazyLoading" />

```csharp
table.HasChildrenSelector = item => ((Account)item).HasChildren;
table.ChildrenLoader = async item => await api.GetChildrenAsync(((Account)item).Id);
```

### Activating a row

A click or Enter on a row without children raises `RowActivated`, which is where you open the file or the record.
A parent row toggles instead.

### Responsive columns

A table adapts to the width it is given, not to the window. Set `HideBelow` on the columns that matter least, such as
an owner or a modified date, and they drop out as the table narrows.

### Example: Cloud spend by team

The demo at the top rolls spend up through three levels. The Owner column hides below 560 px and Resources below
420 px, the Monthly column is end-aligned and formatted as currency, and a footer totals the selected lines.

### Content guidelines

- Keep the tree column's text short. It truncates, and the first column is where people look for a row.
- Put the figure people compare in an end-aligned column with a unit in its header or format, such as `{0} MB`.
- Name rows for assistive technology with `RowLabelSelector`, such as the file or team name; it labels each checkbox.
- Write `EmptyMessage` to say what is missing, such as "No files in this folder".

### Best practices

- Give every row a stable `KeySelector`. Index-based keys break the open state when the data reorders.
- Load deep levels on demand with `ChildrenLoader` rather than preloading thousands of rows.
- Put the hierarchy's rolled-up totals on the parent rows yourself; the table does not compute them.
- Keep `HideBelow` on secondary columns only, so the table never loses the column people read first.
- Set `MaxHeight` on a table that can grow tall, so the header stays in view.

### Common questions

**Why does a parent not open?** A row opens when its selector returns at least one child, or when
`HasChildrenSelector` is true and a `ChildrenLoader` is set. An empty list means a leaf.

**Can a column sort the tree flat?** No. Sorting orders the siblings inside each parent, so the hierarchy never
breaks. Use `Table` for a flat list.

**Why are the expanded rows lost when I reload the data?** `ExpandedKeys` holds keys. If `KeySelector` returns new
values on every load, the rows no longer match. Use ids that survive a reload.

**Does the table total the selection?** No. Use `SelectionChanged` and add up the items you need.

## API reference

<!-- api: TreeTable -->

<!-- api: TreeTableColumn -->

### Event arguments

| Type | Member | Meaning |
|---|---|---|
| `TreeTableExpandedChangedEventArgs` | `ExpandedKeys` | The keys of every expanded row |
| `TreeTableSelectionChangedEventArgs` | `SelectedKeys`, `SelectedItems` | The selected keys, and the loaded items they belong to |
| `TreeTableSortChangedEventArgs` | `Column`, `Direction` | The sorted column (null when cleared) and the direction |
| `TreeTableRowEventArgs` | `Item`, `Key`, `Error` | The row; `Error` is set when a load failed |

## Accessibility

- The table has the name "Tree table" by default. Set `Caption` to name it for your content.
- Rows expose their name, their position in the set, their size and their expanded state. A sortable header
  reports its sort direction.
- Only one row is in the Tab order, and the arrow keys move between rows.
- Each checkbox is named from `RowLabelSelector`, such as "Select Design", and the mixed state is exposed.
- Loading, load errors, sorting, expand all and selection changes are announced through a polite live region.
- If the focused row disappears because its parent collapsed, focus moves to the nearest visible ancestor.
- Hover fills apply to mouse pointers only; a touch tap toggles and activates rows directly.

### Keyboard

| Keys | Action |
|---|---|
| `Up` / `Down` | Move between visible rows |
| `Right` | Open a row; on an open row, move to its first child |
| `Left` | Close a row; otherwise move to its parent |
| `Home` / `End` | Move to the first or last visible row |
| `PageUp` / `PageDown` | Move ten rows |
| `*` | Open all siblings of the focused row |
| `Space` | Toggle the row's selection (with `IsSelectable`) |
| `Enter` | Toggle a parent, retry a failed load, or activate a leaf |
| `Ctrl` / `Cmd` + `A` | Select all rows, or clear the selection (with `IsSelectable`) |

## Motion

- Rows grow from zero height on a smooth spring while they fade in, with a short stagger of 18 ms between siblings, and
  they shrink away when a parent closes.
- A loading row hands its place to the first child that arrives, so nothing squeezes past anything else.
- Sorting moves each row to its new place on a spring.
- The chevron turns a quarter on a snappy spring, the sort arrow turns over, and the expand all glyph swaps with a
  turn.
- Hover and selection fills fade in 160 ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A row opens or closes | Height spring with stagger and fade | A short fade with no height spring or stagger | Immediate |
| Sorting | Rows spring to their new places | Rows jump | Immediate |
| Chevron and sort arrow | Spring turn | Immediate | Immediate |

## Related

- `TreeView` for navigation trees without columns.
- `Table` for flat records with sortable columns.
- `DataGrid` for a spreadsheet-style grid with range selection and inline editing.
- `Checkbox`, the binary control behind each row's selection.

| Component | Choose it when |
|---|---|
| `TreeView` | The hierarchy is navigation with one label per node |
| `Table` | The records are flat and compared by column |
| `DataGrid` | People edit cells and select ranges |
| `Accordion` | A few sections open into free-form content |
