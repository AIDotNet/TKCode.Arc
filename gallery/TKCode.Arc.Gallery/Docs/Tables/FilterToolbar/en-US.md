---
title: Filter toolbar
summary: Keep collection filters close and easy to reset.
---

<Demo name="Filters" />

## When to use

`FilterToolbar` is the strip above a table or a list that shows which filters narrow the rows, lets people remove them
one at a time and resets them together. It holds the applied filters as removable chips, the Add filter menu that
adds the next one, an optional search field and any actions the page needs, so the list page keeps one consistent
header instead of a set of loose dropdowns.

- **Faceted filters on an order list**: a fulfillment team filters orders by status, carrier and region, sees "Status:
  Shipped" and "Region: EU" as chips, and clears them with one action when the shift changes.
- **Incident and ticket queues**: an on-call view filters incidents by severity, service and owner. The chips make
  the current view explicit when someone shares a link or a screenshot in a review.
- **Invoice and billing lists**: a finance user filters invoices by status and customer, then exports the filtered
  rows through an action placed beside the Add filter trigger.
- **Customer and CRM tables** where a search box and two or three facets work together: the search narrows the text,
  the chips narrow the segment, and the table below shows what remains.
- **Dense admin consoles on narrow windows**: below 520 px the chips stack above the actions, so the same toolbar
  works in a side panel without a separate layout.

## When not to use

- **A fixed set of options shown as on or off toggles**, such as the three views of a list (Open, Closed, All): use
  `ChipGroup`, which shows every option at once and needs no menu.
- **Free text search on its own, with no facets**: use `SearchField`. The toolbar's search box exists to sit with the
  filters; a page without filters gets nothing from the chips.
- **A form field that takes several values**, such as the assignees of a task in an editor: use `MultiSelect`. A
  filter toolbar applies a value to a list; a form field stores it on a record.
- **Choosing one value from a long list**, such as a customer among thousands: use `Combobox` with its own search.
  The Add filter menu lists every option, so it suits short lists only.
- **Sorting the rows of a table**: use the column headers of `Table`. A filter removes rows; a sort orders
  them, and the two belong in different places.

## Usage

Each filter is a `FilterChip` with an `Id`, a `Label` and a `Value`. Fields for the Add filter menu go in
`Fields`. A field has options that people choose in the second step.

```xml
<arc:FilterToolbar Label="Active filters"
                   ActiveFilters="{Binding Filters}"
                   MenuAlignment="Start">
    <arc:FilterToolbar.Fields>
        <arc:FilterField Id="status" Label="Status">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Open" />
                <arc:FilterOption Value="In progress" />
                <arc:FilterOption Value="Closed" />
            </arc:FilterField.Options>
        </arc:FilterField>
        <arc:FilterField Id="owner" Label="Owner">
            <arc:FilterField.Options>
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
            </arc:FilterField.Options>
        </arc:FilterField>
    </arc:FilterToolbar.Fields>
</arc:FilterToolbar>
```

`FilterToolbar` derives from Avalonia's `TemplatedControl` and is a group for assistive technology. Its parts are a
chip area, the search box, the Add filter trigger, the Clear all button and any actions, laid out in one row. The
toolbar keeps the applied filters in `ActiveFilters`, which is two-way, so the list you bind to is the source of truth
and the chips are a view of it.

### Anatomy

1. **Chip area** (`ActiveFilters`): one chip per applied filter, in the order of the list. A chip shows the label,
   a middle dot and the value, for example "Status · Open". A chip without a `Value` shows only its label.
2. **Chip remove button**: a 24 px target with a 14 px close glyph at the end of each chip. Its name is the chip's text,
   for example "Remove Status: Open". Chips have a 1 px border, a muted surface and a pill radius, and they sit 8 px
   apart.
3. **Empty note** (`EmptyText`): "No filters applied" in the muted text color at the leading edge of the chip area when
   no filter is set. It rises in when the last chip leaves.
4. **Search box** (`IsSearchVisible`, `SearchText`, `SearchPlaceholder`): 200 px wide and as tall as the small control
   size, with a search glyph, a 1 px border and the control radius. Its focus draws the strong border color and the
   focus ring.
5. **Add filter trigger** (`AddFilterLabel`): a plus glyph and the label in medium weight, at least as tall as the
   small control size. It appears when `Fields` has at least one field and opens the two-step menu.
6. **Actions** (`Actions`): any controls you add, placed after the Add filter trigger, 12 px apart from the other
   actions.
7. **Clear all** (`ClearAllLabel`): a ghost button in the muted text color. It appears only while a filter is set.
8. **Frame**: the surface color, a 1 px border, the panel radius and 16 px of horizontal padding with 12 px of vertical
   padding. The chip area and the actions are separated by 16 px.

Below 520 px (the stacking breakpoint) the toolbar stacks: the chips take the first row and the actions move to a
second row aligned to the end.

The Add filter menu opens a panel 256 px wide (16 rem) with 6 px of inset. Each row of the field and value lists is
36 px tall, and the header row is 32 px. A value list taller than 320 px scrolls. Set `MenuAlignment` to `Start` or
`End` to choose the trigger edge the panel lines up with; the panel flips to the other side when there is no room.

### Filters from code

Create the active filters as a list of `FilterChip`. Each field keeps one chip: adding a chip with the same
`Id` replaces the chip that is already there.

```csharp
var toolbar = new FilterToolbar
{
    ActiveFilters = new ObservableCollection<FilterChip>
    {
        new("status", "Status", "Open"),
        new("owner", "Owner", "Mina")
    }
};
toolbar.AddFilter(new FilterChip("priority", "Priority", "High"));
toolbar.RemoveFilter("owner");
toolbar.ClearFilters();
```

`AddFilter`, `RemoveFilter` and `ClearFilters` are the toolbar's own operations, and they raise `FiltersChanged`.
`RemoveFilter` does nothing when no chip has the id, and `AddFilter` with an identical chip does nothing.

### Responding to changes

`FiltersChanged` reports what changed (`Kind`), the chip involved and the full set of active filters. Use it to
reload the data the toolbar filters.

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

`FilterChangeKind` has four values: `Added`, `Changed`, `Removed` and `Cleared`. `Changed` fires when a field gets a
new value, and `Previous` holds the chip it replaced. `Field` is set when the change came from the Add filter menu.

The event fires for changes made through the toolbar: a chip's remove button, Clear all, the Add filter menu and the
three operations above. It does not fire when your code changes the `ActiveFilters` list directly. The chips still
update and the change is announced, so for direct list edits, subscribe to the list's `CollectionChanged` instead.

### Search

`IsSearchVisible` adds a search box to the toolbar. `SearchText` is two-way, so the table can react to it.

<Demo name="SearchAndTable" />

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="Search issues"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

`SearchPlaceholder` sets the hint shown while the box is empty; the default is "Search". The search is not a filter
chip, so it does not appear in `ActiveFilters`, and it is not part of the `FiltersChanged` event. Filter the rows from
`SearchText` as well as from the chips.

### Extra actions

`Actions` puts other controls, such as an export button, next to the Add filter trigger.

```xml
<arc:FilterToolbar>
    <arc:FilterToolbar.Actions>
        <Button Content="Export" />
    </arc:FilterToolbar.Actions>
</arc:FilterToolbar>
```

### No filters

With no active filters, the empty note sits at the leading edge. Setting `MenuAlignment` to `Start` lines the menu up
with the start edge of the trigger.

<Demo name="NoFilters" />

### Narrow

Below 520px the toolbar stacks: chips first, then the actions aligned to the end.

<Demo name="Narrow" />

### Filter menu on its own

`FilterMenu` is the Add filter trigger and its two-step panel. Down on the trigger opens it on the first field, Right
enters a field and Left goes back.

<Demo name="FilterMenu" />

`FilterMenu` also works outside a toolbar. It takes its own `Fields`, shows the value each field already has through
`ActiveFilters`, and raises `FilterSelected` when a value is picked. The chip in that event has the field's `Id`, so
your handler can apply it with the same replace-by-id rule the toolbar uses.

### Binding to a view model

In an MVVM app the view model owns the applied filters, the search text and the rows they narrow. The toolbar edits
the lists it is given, so the view model stays the single source of truth. With CommunityToolkit.Mvvm:

```csharp
public partial class InvoicesViewModel : ObservableObject
{
    public InvoicesViewModel()
    {
        ActiveFilters.CollectionChanged += (_, _) => ApplyFilters();
    }

    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

    [ObservableProperty]
    private string? _searchText;

    partial void OnSearchTextChanged(string? value) => ApplyFilters();

    [RelayCommand]
    private Task ExportAsync() =>
        File.WriteAllLinesAsync("invoices.csv", VisibleInvoices.Select(invoice => invoice.Id));

    private void ApplyFilters()
    {
        // Filter the rows from ActiveFilters and SearchText, then refresh VisibleInvoices.
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:FilterToolbar ActiveFilters="{Binding ActiveFilters}"
                       IsSearchVisible="True"
                       SearchPlaceholder="Search invoices"
                       SearchText="{Binding SearchText}">
        <arc:FilterToolbar.Fields>
            <arc:FilterField Id="status" Label="Status">
                <arc:FilterOption Value="Draft" />
                <arc:FilterOption Value="Sent" />
                <arc:FilterOption Value="Overdue" />
            </arc:FilterField>
        </arc:FilterToolbar.Fields>
        <arc:FilterToolbar.Actions>
            <arc:Button Content="Export CSV" Variant="Secondary" Command="{Binding ExportCommand}" />
        </arc:FilterToolbar.Actions>
    </arc:FilterToolbar>
    <arc:Table ItemsSource="{Binding VisibleInvoices}" />
</StackPanel>
```

- `ActiveFilters` and `SearchText` are two-way, the default for both. A chip removed in the toolbar updates the
  collection, and the view model sees it through `CollectionChanged`.
- `ActiveFilters` also accepts a read-only list such as an array. The toolbar then replaces the list with a new one
  and writes it back, so use an `ObservableCollection` when the view model needs to react to every change.
- `Fields` is a read-only collection in the toolbar, so define the fields in XAML, or add them from code once with
  `toolbar.Fields.Add(...)`. They are not bound to the view model.
- Subscribe to `FiltersChanged` in code-behind only when a change needs the event's `Kind` or `Previous`. For plain
  refiltering, the `CollectionChanged` handler above is enough, because it also covers changes the toolbar does not
  report.

### Data shape and performance

The menu lists every option of a field. Options are not virtualized, so keep each value list short, roughly a few
dozen values. A field with hundreds of values belongs in a `Combobox` with its own search, and the toolbar shows the
chosen value only.

A chip stores the option's text, which is its `Label` when the option has one and its `Value` otherwise, and the
chip's `Value` is that text. Filter on the chip's value, so keep the text equal to the data the query uses. When the
wording people read must differ from the data, map the chip's `Id` and `Value` to the query in the view model.

`FilterOption.Hint` sits at the end of the row, which suits a count such as "Open 4".

### Focus and dismissal

The Add filter menu stays open until someone chooses a value or presses Escape. The trigger gets focus back when the
panel closes, whether by Escape or after a value is chosen.

When a chip is removed, focus moves to the next chip's remove button, or to the previous one when the removed chip
was last. When the last chip goes, focus moves to the Add filter trigger if it is visible.

### Example: invoice list in a billing console

A finance team reviews overdue invoices. Two filters are applied and the Add filter trigger is ready for the next
facet. Clear all appears because at least one filter is set. The table below the toolbar shows the rows that match
both chips, and the team clears the chips in one step when it starts a new review.

![A filter toolbar with status and owner chips and the Add filter trigger](images/InvoiceFilters.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:collections="using:System.Collections.ObjectModel">
    <StackPanel Width="600">
        <arc:FilterToolbar>
            <arc:FilterToolbar.ActiveFilters>
                <collections:ObservableCollection x:TypeArguments="arc:FilterChip">
                    <arc:FilterChip Id="status" Label="Status" Value="Overdue" />
                    <arc:FilterChip Id="owner" Label="Owner" Value="Mina" />
                </collections:ObservableCollection>
            </arc:FilterToolbar.ActiveFilters>
            <arc:FilterField Id="status" Label="Status" Icon="{x:Static arc:ArcIcons.CircleDot}">
                <arc:FilterOption Value="Draft" Hint="3" />
                <arc:FilterOption Value="Sent" Hint="18" />
                <arc:FilterOption Value="Overdue" Hint="6" />
            </arc:FilterField>
            <arc:FilterField Id="owner" Label="Owner" Icon="{x:Static arc:ArcIcons.User}">
                <arc:FilterOption Value="Mina" />
                <arc:FilterOption Value="Theo" />
                <arc:FilterOption Value="Ana" />
            </arc:FilterField>
            <arc:FilterField Id="region" Label="Region" Icon="{x:Static arc:ArcIcons.Flag}">
                <arc:FilterOption Value="EU" />
                <arc:FilterOption Value="US" />
                <arc:FilterOption Value="APAC" />
            </arc:FilterField>
        </arc:FilterToolbar>
    </StackPanel>
</UserControl>
```

Each field has one chip, so choosing a different owner replaces the chip rather than adding a second one. The field
options carry a `Hint` for the status counts, so the menu shows how many invoices sit in each state before the team
picks one.

### Content guidelines

- **Label** (the field name): a short noun in sentence case that matches the column it filters: "Status", "Owner",
  "Region". Do not use a verb, and do not repeat the page title.
- **Value**: the text people recognize from the table: "Overdue", "Mina", "EU". Use the same spelling as the data.
- **Add filter**: keep the default or use a verb phrase that describes the action: "Add filter" is the default and
  works for most pages.
- **Empty note**: state the condition in plain words: "No filters applied", not "Nothing to show".
- **Search placeholder**: name what is searched: "Search invoices", "Search customers by name".

| Instead of | Write |
|---|---|
| Filter | Status |
| Choose a value... | Search invoices |
| Clear | Clear all |
| 0 filters | No filters applied |
| STATUS = OVERDUE | Status: Overdue |

### Best practices

**Do**

- Keep the chips as the record of what filters the rows. Derive the rows from `ActiveFilters` and `SearchText`, so a
  reload, a saved view or a shared link shows the same result the chips describe.
- Give each field one `Id` that stays the same across releases. The id decides which chip a new value replaces.
- Put the search box and the chips in the same toolbar when they narrow the same rows. People then read them as one
  set of filters.
- Keep the value lists short, and put the most useful values first.

**Avoid**

- Adding filters that cannot change the result, such as a field with one option. The chip stays on screen and tells
  people nothing.
- Adding a second Clear all button beside the toolbar. The toolbar already shows its own while any filter is set,
  and two resets confuse people about which one clears the rows.
- Changing `ActiveFilters` from a background thread. Controls must change on the UI thread, so marshal the change
  with `Dispatcher.UIThread` first.
- Relying on `FiltersChanged` for direct list edits, which do not raise it. Refilter from the list's
  `CollectionChanged` instead.

### Common questions

**Why does the Add filter trigger not show?** It shows only when `Fields` has at least one field. Add the fields in
XAML, or add them in code before the toolbar is shown with `Fields.Add`.

**How do I preselect filters for a saved view?** Set `ActiveFilters` to the saved chips when the view opens, in XAML or
in the view model. No event fires for the initial value, so apply the same filter to the rows in the same step.

**Why does picking a second value replace the chip instead of adding one?** Each chip id is a field id, and the toolbar
keeps one chip per field. Picking a value for a field that already has one changes that chip, and `FiltersChanged`
reports the change with `Kind` set to `Changed` and `Previous` set to the old chip.

**How do I show a filter without a value?** Create the chip with no `Value`, for example
`new FilterChip("archived", "Archived")`. The chip shows its label alone, and the remove button is named after it.

**How do I reset every filter from code?** Call `ClearFilters()`, which raises `FiltersChanged` with `Kind` set to
`Cleared`. Clearing the list directly with `ActiveFilters.Clear()` updates the chips without the event, so pick the
call that matches what your handlers need.

## API reference

<!-- api: FilterToolbar -->

<!-- api: FilterChip -->

<!-- api: FilterField -->

## Accessibility

- The toolbar is a group named "Active filters" by default. Set `Label` to name it for your page.
- Each chip has a remove button named like "Remove Status: Open". After removing, focus moves to a neighbor chip or to the Add filter trigger.
- Adding and removing filters is announced in the status region.
- The Add filter menu is a named dialog. Its options are checkable menu items.
- Down or Up opens the menu from the trigger. Arrow keys move in the list, Right enters a field's values, Left goes back and typing jumps to an option.
- Escape closes the menu and returns focus to the trigger.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the chip remove buttons, the search box, the Add filter trigger, the actions and Clear all |
| `Enter` or `Space` on a chip's remove button | Remove that filter; focus moves to a neighbor chip or to the Add filter trigger |
| `Escape` in the search box | Clear the search text when it is not empty |
| `Enter`, `Space` or `Down` on the Add filter trigger | Open the menu on the first field |
| `Up` on the Add filter trigger | Open the menu on the last field |
| `Down` / `Up` in the menu | Move to the next or previous row; the list wraps around |
| `Home` / `End` in the menu | Move to the first or last row |
| `Right` on a field | Show that field's values |
| `Left` in a value list | Go back to the fields |
| `Enter` or `Space` on a value | Apply the value and close the menu |
| Letters in the menu | Jump to the next row whose label starts with the typed letters |
| `Escape` in the menu | Close the menu and return focus to the trigger |

## Motion

- A chip opens and closes its own slot, and its neighbors slide to make room.
- A changed value swaps in place with a soft blur. Clear all shows while any filter is set.
- The Add filter trigger grows into the menu surface, and the field and value steps slide in their direction.
- Reduced motion swaps the changes at once with a short fade of about 160 ms. Off jumps straight to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A filter is added | Its chip opens a slot on a morph spring, and the chips after it slide to make room | A short fade of about 160 ms | Immediate |
| A filter is removed | Its slot closes and the chips after it slide back | A short fade of about 160 ms | Immediate |
| A value changes | The value changes in place with a soft blur | A short fade of about 160 ms | Immediate |
| Clear all shows or goes | Its slot opens or closes | A short fade of about 160 ms | Immediate |
| The menu opens | The trigger grows into the panel, with a slight overshoot on open and a smooth close | A short fade of about 160 ms | Immediate |
| Field to value list | The panel slides 8 px in the direction of travel | A short fade of about 160 ms | Immediate |

## Related

- `ChipGroup` for a small fixed set of toggles.
- `SearchField` for free text search without filters.
- `Table` for the table that the filters narrow.
- `MultiSelect` for a form field that takes several values.

| Component | Choose it when |
|---|---|
| `ChipGroup` | The options are a short fixed set that people switch between, and all are visible |
| `SearchField` | The page searches text and has no facets to narrow |
| `Table` | The rows the filters leave are shown and sorted in a table |
| `MultiSelect` | A form stores several values on a record |
| `Combobox` | One value must be chosen from a long list that needs its own search |
