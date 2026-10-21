---
title: Filter toolbar
summary: Keep collection filters close and easy to reset.
---

<Demo name="Filters" />

## When to use

- Faceted filters above a table or list, such as status, owner or priority.
- A list page where filters, search and a table work together.
- Filters that people remove one at a time, with a Clear all action when any filter is set.

## When not to use

- A fixed set of options shown as toggles: use `ChipGroup`.
- Free text search on its own: use `SearchField`.
- A form field that takes several values: use `MultiSelect`.

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

### Responding to changes

`FiltersChanged` reports what changed (`Kind`), the chip involved and the full set of active filters. Use it to
reload the data the toolbar filters.

```csharp
toolbar.FiltersChanged += (_, e) => Refilter(e.ActiveFilters);
```

### Search

`IsSearchVisible` adds a search box to the toolbar. `SearchText` is two-way, so the table can react to it.

<Demo name="SearchAndTable" />

```xml
<arc:FilterToolbar IsSearchVisible="True"
                   SearchPlaceholder="Search issues"
                   SearchText="{Binding Query, Mode=TwoWay}" />
```

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

## Motion

- A chip opens and closes its own slot, and its neighbors slide to make room.
- A changed value morphs in place. Clear all shows while any filter is set.
- The Add filter trigger grows into the menu surface, and the field and value steps slide in their direction.
- Reduced motion swaps the changes at once with a short fade of about 160 ms. Off jumps straight to the final state.

## Related

- `ChipGroup` for a small fixed set of toggles.
- `SearchField` for free text search without filters.
- `SortableDataTable` for the table that the filters narrow.
- `MultiSelect` for a form field that takes several values.
