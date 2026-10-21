---
title: Select
summary: A compact choice field with a keyboard friendly menu.
---

<Demo name="Field" />

## When to use

- A short, fixed list such as a region, a sort order or a plan, where users pick one value and do not need to type.
- A labelled field in a form, with an optional description under the trigger.
- Lists that stay short enough to scan. The menu scrolls, but a long list is easier to search.

## When not to use

- Long or searchable lists: use `Combobox`.
- Choosing several values at once: use `MultiSelect`.
- Two to four options that should always be visible: use `SegmentedControl`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Select Label="Region"
                Description="Where your workspace data is stored."
                Width="280">
        <arc:SelectItem Content="North America" Value="na" />
        <arc:SelectItem Content="Europe" Value="eu" />
        <arc:SelectItem Content="South America (soon)" Value="sa" IsEnabled="False" />
        <arc:SelectItem Content="Africa" Value="af" />
    </arc:Select>
</UserControl>
```

### Bound options and value

Set `ItemsSource` to any objects or to `SelectOption` records. `SelectedValue` is two-way, so the bound property
receives the chosen value.

```xml
<arc:Select Label="Sort by"
            Width="220"
            ItemsSource="{Binding SortOrders}"
            SelectedValue="{Binding SortKey, Mode=TwoWay}" />
```

```csharp
var sort = new Select
{
    Label = "Sort by",
    Width = 220,
    ItemsSource = new[]
    {
        new SelectOption("newest", "Newest first"),
        new SelectOption("oldest", "Oldest first"),
        new SelectOption("az", "Name, A to Z"),
    },
    SelectedValue = "newest"
};
sort.SelectionChanged += (_, _) => Console.WriteLine(sort.SelectedValue);
```

### Custom labels and values

For data items that are not `SelectOption`, set `TextSelector` and `ValueSelector`. They are AOT safe, unlike
`DisplayMemberBinding`.

<Demo name="Selectors" />

### Placeholder and description

`Placeholder` is shown while nothing is selected. `Description` is a muted hint under the trigger and is also the
accessible help text.

```xml
<arc:Select Label="Plan" Placeholder="Choose a plan" Description="You can change it later." />
```

### Long lists and disabled state

The menu is capped at `MaxDropDownHeight` (320 by default) and scrolls. Only the visible rows are drawn, so a list
of thousands of items stays responsive. A disabled `Select` ignores input.

<Demo name="LongLists" />

<Demo name="Disabled" />

```xml
<arc:Select Label="Build number" ItemsSource="{Binding Builds}" MaxDropDownHeight="240" />
<arc:Select Label="Plan" IsEnabled="False" SelectedIndex="1">
    <arc:SelectItem Content="Free" />
    <arc:SelectItem Content="Pro" />
</arc:Select>
```

## API reference

<!-- api: Select -->

<!-- api: SelectItem -->

## Accessibility

- `Label` names the field and `Description` is its help text.
- The trigger takes focus. Enter, Space and the arrows open the menu. Arrows, Home, End, PageUp and PageDown move the
  highlight, skipping disabled options. Enter or Space picks, Escape closes without a change and Tab closes.
- Typing a letter jumps to the next option that starts with it. On the closed field it picks that option directly.
- The selected value is exposed as plain text for assistive technology. Animated copies are hidden.
- The focus ring stays on the trigger while the menu is open.

## Motion

- The shown value rolls in the direction of the list: later options rise from below, earlier options fall from above.
- The menu drops from the trigger with a short scale and fade. Closing is instant.
- The highlight glides from one option to the next.
- Reduced motion cross-fades the value instantly and fades the menu in and out. Off switches the value and the menu
  without any transition.

## Related

- `Combobox` when the list is long or the user should search it.
- `MultiSelect` when several values are chosen at once.
- `MorphSelect` when the trigger should grow into the list and the options need icons or groups.
- `SegmentedControl` when two to four options should always be visible.
