---
title: Sortable data table
summary: Compare structured records with sortable columns.
---

## When to use

- Records with the same fields, such as projects, invoices or users, that people compare by column.
- Tables that need row selection with a checkbox column, Shift range selection and a count line.
- Short to medium lists. Pair long lists with `Pagination`.

## When not to use

- Order that the user decides by hand: use `ReorderableList`.
- Events in time order, such as an activity log: use `Timeline`.
- Filtering the rows by fields and search: put a `FilterToolbar` above the table.
- Editing cells in place like a spreadsheet: this component is read-only.

## Usage

Columns are declared in XAML. Each column reads its cell value through `Binding`.

```xml
<arc:SortableDataTable Caption="Projects"
                       ItemsSource="{Binding Projects}"
                       ItemNameOne="project"
                       ItemNameOther="projects"
                       SortKey="Budget"
                       SortDirection="Descending">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="Project" Key="Name" Binding="{Binding Name}" />
        <arc:DataTableColumn Header="Owner" Binding="{Binding Owner}" IsSortable="False" />
        <arc:DataTableColumn Header="Budget" Key="Budget" Binding="{Binding Budget}" IsNumeric="True" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### Selection

Set `IsSelectable` to show a checkbox column. Clicking a row selects it, Shift + click selects a range, and
the count line offers a Clear action. `SelectedItems` is two-way.

```xml
<arc:SortableDataTable ItemsSource="{Binding Projects}"
                       IsSelectable="True"
                       SelectedItems="{Binding SelectedProjects, Mode=TwoWay}">
    <arc:SortableDataTable.Columns>
        <arc:DataTableColumn Header="Project" Binding="{Binding Name}" />
    </arc:SortableDataTable.Columns>
</arc:SortableDataTable>
```

### Cell content and formatting

Use `Formatter` for text, or `CellTemplate` for custom content such as a status badge. Use typed value
selectors from code when you need to avoid bindings, for example when the app is published with AOT.

```csharp
var table = new SortableDataTable { ItemsSource = projects, Caption = "Projects" };
table.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
table.Columns.Add(DataTableColumn.Create<Project>("Budget", p => p.Budget, key: "Budget"));
table.Columns[1].Formatter = value => value is decimal amount ? amount.ToString("C0", CultureInfo.GetCultureInfo("en-US")) : "-";
table.Columns.Add(new DataTableColumn("Status", p => ((Project)p).Status)
{
    CellTemplate = new FuncDataTemplate<Project>((_, _) => new StatusPill())
});
```

### Sorting and events

`SortChanged` and `SelectionChanged` report what the user did. Set `IsSortClearable` to let a third click on a
header clear the sort.

```csharp
table.SortChanged += (_, e) => status.Text = e.Column is null
    ? "Sort cleared"
    : $"Sorted by {e.Column.Header}, {e.Direction}";
table.SelectionChanged += (_, e) => status.Text = $"{e.SelectedItems.Count} selected";
table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
```

### Empty and loading states

`EmptyMessage` shows when there are no rows. `IsLoading` covers the rows with a loading veil and
`LoadingMessage` names the wait.

```xml
<arc:SortableDataTable EmptyMessage="No projects match these filters"
                       IsLoading="{Binding IsBusy}" />
```

## API reference

<!-- api: SortableDataTable -->

<!-- api: DataTableColumn -->

## Accessibility

- The table has its `Caption` as its name and exposes table, header and row roles.
- A sortable header is a button named like "Sort by Budget, currently ascending". The sort direction is part of the name.
- The Select all checkbox shows a mixed state when only some rows are selected.
- Sort and selection changes are announced in the status region without interrupting the current speech.
- Arrow keys move between headers and checkboxes, Enter or Space sorts or toggles, and Escape clears the selection.

## Motion

- When the sort changes, rows slide to their new places on a spring and the sort arrow flips.
- The selection count rolls to its new value, and Clear fades in with a short blur.
- Reduced motion reorders rows at once and only fades the count. Off jumps straight to the final state.

## Related

- `TreeView` for records that nest, such as folders with children.
- `ReorderableList` when the user sets the order by dragging, not by a column.
- `FilterToolbar` to narrow the rows by fields and search before they reach the table.
- `Pagination` to split large record sets into pages.
