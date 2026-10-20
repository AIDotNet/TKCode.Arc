---
title: Chip group
summary: Filter by a few facets with chips that morph as you pick them.
---

## When to use

- Filters with a few facets that people switch often, such as topics or categories.
- A single choice that can also be cleared, with `IsMultiple="False"`.
- Options that should all stay visible, so the user can compare the choices at a glance.

## When not to use

- Space is tight, or the field must show its values inside a trigger: use `MultiSelect`.
- Form fields where each option is a yes or no answer: use `Checkbox`.
- Choosing one view out of a few: use `SegmentedControl`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ChipGroup Label="Topics" MaxWidth="560">
        <arc:SelectItem Content="Design" />
        <arc:SelectItem Content="Engineering" />
        <arc:SelectItem Content="Research" />
        <arc:SelectItem Content="Marketing" />
        <arc:SelectItem Content="Sales" />
    </arc:ChipGroup>
</UserControl>
```

### Bound options and values

Set `ItemsSource` to any objects, or to `SelectOption` records. `SelectedValues` lists the values of the chosen
chips in option order. It is read-only, so read it in a handler.

```xml
<arc:ChipGroup Label="Topics"
               MaxWidth="560"
               ItemsSource="{Binding Topics}"
               SelectedItems="{Binding ChosenTopics}" />
```

```csharp
var topics = new ChipGroup
{
    Label = "Topics",
    MaxWidth = 560,
    ItemsSource = new[] { "Design", "Engineering", "Research", "Marketing" }
};
topics.Selection.Select(1);
topics.SelectionChanged += (_, _) => Console.WriteLine(string.Join(", ", topics.SelectedValues));
```

### Single choice

`IsMultiple="False"` allows one chip at a time. The selected chip can still be clicked again to clear the choice.

```xml
<arc:ChipGroup Label="Status" IsMultiple="False">
    <arc:SelectItem Content="Open" Value="open" />
    <arc:SelectItem Content="In progress" Value="progress" />
    <arc:SelectItem Content="Closed" Value="closed" />
</arc:ChipGroup>
```

### Folding long lists

`MaxVisibleItems` shows that many chips, and the rest fold behind a "+N more" chip. A selected chip stays in view
even when it is past the limit. `IsExpanded` is two-way and shows or hides the folded chips.

```xml
<arc:ChipGroup Label="Topics"
               MaxVisibleItems="5"
               MaxWidth="560"
               IsExpanded="{Binding ShowAllTopics, Mode=TwoWay}"
               ItemsSource="{Binding Topics}" />
```

### Toggling in code

`Toggle(index)` changes the chip at an index, the same as a click.

```csharp
topics.Toggle(0);
```

## API reference

<!-- api: ChipGroup -->

<!-- api: SelectItem -->

## Accessibility

- `Label` is the accessible name of the group.
- Each chip is a button with a pressed state. Screen readers announce it as a toggle.
- The group uses a roving tab stop: only one chip is in the Tab order. Arrows move between chips and wrap around,
  and Home and End go to the first and last chip.
- Space or Enter toggles the focused chip. On the "+N more" chip, it expands or collapses the folded chips.
- The "+N more" chip reports whether the folded chips are expanded. The focus ring is drawn on the focused chip.

## Motion

- A picked chip grows a check from its left edge, and its label slides open to the new width.
- Neighbouring chips glide to their new positions, including across line breaks.
- Folded chips appear with a short stagger when the list expands, and the group height changes smoothly.
- A pressed chip scales down slightly and springs back when released.
- Reduced motion replaces the reshaping with a short fade. Off switches the chips directly.

## Related

- `MultiSelect` when the values must fit in a compact field.
- `Checkbox` for form choices that each stand on their own.
- `SegmentedControl` for picking one view from two to four.
- `Select` for a single value from a short list in a form.
