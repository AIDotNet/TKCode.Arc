---
title: Multi-select
summary: Select several values while keeping the field readable.
---

<Demo name="Chips" />

## When to use

- Compact multi-value choices such as labels or owners, picked from a fixed list.
- Filters that show the picked values inside the field.
- Lists where each option may need a short search, which `IsSearchable` adds.

## When not to use

- All options should stay visible and be toggled at once: use `ChipGroup`.
- Free values that the user types and creates: use `TagInput`.
- Exactly one value: use `Select`, or `Combobox` for a long list.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiSelect Label="Labels"
                     Description="Shown on the issue card."
                     Width="320">
        <arc:SelectItem Content="Bug" Value="bug" />
        <arc:SelectItem Content="Feature" Value="feature" />
        <arc:SelectItem Content="Documentation" Value="docs" />
        <arc:SelectItem Content="Infrastructure" Value="infra" IsEnabled="False" />
    </arc:MultiSelect>
</UserControl>
```

### Bound options and values

Set `ItemsSource` to `SelectOption` records. `SelectedValues` lists the values in the order they were picked. It is
read-only, so read it in a handler or from code.

```csharp
var labels = new MultiSelect
{
    Label = "Labels",
    Width = 320,
    ItemsSource = new[]
    {
        new SelectOption("bug", "Bug"),
        new SelectOption("feature", "Feature"),
        new SelectOption("docs", "Documentation"),
        new SelectOption("infra", "Infrastructure", isDisabled: true),
    }
};
labels.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", labels.SelectedValues));
```

### Chips and overflow

Picked values appear as chips in the field. `MaxVisibleChips` sets how many show before the rest fold into a `+N`
chip. The default is 2.

<Demo name="OneVisibleChip" />

```xml
<arc:MultiSelect Label="Owners"
                 MaxVisibleChips="1"
                 Width="280"
                 ItemsSource="{Binding Owners}" />
```

### Searchable menu

`IsSearchable` adds a search field at the top of the menu. `SearchPlaceholder` sets its hint. This is an addition
to Arc, whose menu has no search field.

<Demo name="Searchable" />

```xml
<arc:MultiSelect Label="Cities"
                 Placeholder="Pick cities"
                 SearchPlaceholder="Type a city"
                 IsSearchable="True"
                 Width="320"
                 ItemsSource="{Binding Cities}" />
```

### Selecting in code

`Selection` is the selection model. `SelectAll()` and `UnselectAll()` are offered for code only, since Arc has no
select-all control.

```csharp
labels.Selection.Select(0);
labels.Selection.Select(1);
labels.UnselectAll();
```

### Placeholder and disabled state

`Placeholder` is shown while nothing is selected. A disabled field ignores input and hides the clear button.

<Demo name="Disabled" />

```xml
<arc:MultiSelect Label="Reviewers" Placeholder="Select options" IsEnabled="False" />
```

## API reference

<!-- api: MultiSelect -->

<!-- api: SelectOption -->

## Accessibility

- `Label` names the field. The name also lists the picked labels, so a screen reader hears the current values.
- The field announces that it opens a multi-select list. Expanded state is exposed while the menu is open.
- Enter or Space opens and closes the menu. Arrows open it and move the highlight, wrapping and skipping disabled
  options. Enter toggles the highlighted option and the menu stays open. Escape closes it.
- Chips are hidden from assistive technology and replaced by a spoken summary. The clear button is named
  "Clear selections".
- The focus ring is drawn on the trigger.

## Motion

- Chip slots open to their new width with a smooth spring. Each chip grows in from a blur.
- The `+N` count rolls up or down as the number changes.
- Each check draws itself in the menu, and the menu drops in from above.
- Reduced motion replaces the size and scale changes with short cross-fades. Off switches the changes directly.

## Related

- `ChipGroup` when all options are visible and a few facets are toggled.
- `TagInput` when the user creates free values.
- `Select` for one value from a short list.
- `Combobox` for one value from a long list the user searches.
