---
title: Morph select
summary: A select whose trigger grows into the list, with a gliding highlight and type-ahead.
---

## When to use

- Known lists in forms and property panels, such as time zones, owners or projects.
- Grouped options that carry icons or a short meta value, shown with `SelectItem.Group`, `Icon` and `Meta`.
- A compact pill in a toolbar that opens into a full list.

## When not to use

- Free text, or long lists that come from a remote source: use `Combobox`.
- Choosing several values: use `MultiSelect`.
- Running actions rather than choosing a value: use `DropdownMenu`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MorphSelect Label="Status" SelectedIndex="1">
        <arc:SelectItem Content="Backlog" />
        <arc:SelectItem Content="In progress" />
        <arc:SelectItem Content="In review" />
        <arc:SelectItem Content="Done" />
        <arc:SelectItem Content="Canceled" IsEnabled="False" />
    </arc:MorphSelect>
</UserControl>
```

### Groups, icons and meta

Set `Group`, `Icon` and `Meta` on each option. A group title is inserted above each run of options that share a
group. `Icon` takes a Lucide geometry or an image. `Meta` is shown in a monospaced figure style.

```csharp
var zone = new MorphSelect
{
    Label = "Time zone",
    SelectedValue = "London",
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Group = c.Region,
        Meta = c.Utc,
        Keywords = c.Zone,
        Icon = ArcIcons.Globe
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### Compact toolbar

Hide the label with `IsLabelVisible="False"`. The trigger keeps the `Label` as its accessible name, so the pill
stays compact.

```xml
<arc:MorphSelect Label="Status" IsLabelVisible="False" />
<arc:MorphSelect Label="Priority" PanelAlignment="End" PanelWidth="220"
                 ItemsSource="{Binding Priorities}" />
```

### Search

`SearchMode` decides whether the menu has a search field. `Auto`, the default, adds one above eight options. `On`
always adds it, and `SearchPlaceholder` sets its hint. Typing a letter on the closed trigger jumps to a matching
option, and the search field takes over once it is open.

```xml
<arc:MorphSelect Label="Owner"
                 SearchMode="On"
                 SearchPlaceholder="Find a person"
                 ItemsSource="{Binding Owners}" />
```

### Panel size

`PanelWidth` sets the menu width (272 by default). `MaxListHeight` caps its height and the list scrolls beyond it.

## API reference

<!-- api: MorphSelect -->

<!-- api: SelectItem -->

## Accessibility

- `Label` names the trigger, even when `IsLabelVisible` is `False`.
- The trigger is a combo box with a listbox popup. While the menu is open, its expanded state is exposed and the
  highlighted option is announced.
- Arrows move the highlight and skip disabled options. Page Up and Page Down move eight options at a time. Home and
  End go to the first and last option.
- Enter or Space picks, Alt with Up picks and closes, and Escape closes and returns focus to the trigger.
- Option groups are named by their titles. The number of search results is announced politely.

## Motion

- The trigger grows into the list, and its corners reshape from the pill shape to the panel.
- The highlight glides between options.
- The chosen label flies from the list back into the trigger, and the trigger width settles to the new label.
- Reduced motion jumps the sizes and drops the flight and the scroll, leaving a short fade. Off switches directly.

## Related

- `Select` for a plain labelled choice field with a fixed list.
- `Combobox` for a searchable single choice, typed by the user.
- `MultiSelect` when several values are chosen.
- `DropdownMenu` when the menu runs actions.
