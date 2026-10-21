---
title: Split button
summary: A primary action with a menu of nearby alternatives.
---

<Demo name="MergeStrategies" />

## When to use

- A default action with two to five close alternatives, such as Merge with Squash and merge or Rebase and merge.
- Export or share actions where one format is the usual choice and the others sit in the menu.
- A default action whose label changes in place to confirm a result, such as Copy page to Copied.

## When not to use

- Equal options with no default action: use `DropdownMenu` from Menus.
- A single action with nothing to choose from: use `Button`.
- Actions on a content object, such as a row or a card: use `ContextMenu` from Menus.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SplitButton Label="Merge pull request">
        <arc:MenuEntry Header="Squash and merge" />
        <arc:MenuEntry Header="Rebase and merge" />
        <arc:MenuEntry Header="Close pull request" IsDestructive="True" HasSeparatorBefore="True" />
    </arc:SplitButton>
</UserControl>
```

### Click and menu selection

The main half raises `Click` and runs `Command`. The chevron half opens the menu, and a row raises `ItemClicked`
with the row that was chosen.

```xml
<arc:SplitButton Label="Export as CSV"
                 Variant="Secondary"
                 Command="{Binding ExportCommand}">
    <arc:MenuEntry Header="Export as JSON" />
    <arc:MenuEntry Header="Export as PDF" />
</arc:SplitButton>
```

```csharp
merge.Click += (_, _) => log.Text = "Merged with a merge commit.";
merge.ItemClicked += (_, e) => log.Text = $"Menu: {e.Header}";
```

### Rows from data

Use `ItemsSource` for rows that come from a view model, and `ItemTemplate` to draw them. Items that are not
`MenuEntry` instances are shown as row headers.

```xml
<arc:SplitButton Label="Share" ItemsSource="{Binding ShareTargets}" />
```

### Icon and result in place

`Icon` takes a 24 by 24 Lucide geometry and is set in code. Changing `Label` and `Icon` morphs the main half to the
new text, so a result can be shown on the button itself.

<Demo name="ResultInPlace" />

### Disabled

Set `IsEnabled` to `False` to disable both halves.

<Demo name="Disabled" />

### Secondary

`Variant="Secondary"` is a compact outlined pill with 12px text and a 142px main half. In this example, picking a format makes it the default.

<Demo name="Secondary" />

## API reference

<!-- api: SplitButton -->

<!-- api: MenuEntry -->

## Accessibility

- The main half and the chevron are two buttons. The chevron is named `{Label} more actions`, unless
  `MenuButtonLabel` sets another name, and reports whether the menu is expanded.
- The menu uses the Menu and MenuItem roles. Arrow keys move through the rows and loop at the ends.
- Enter or Space runs the main action on the main half, and opens the menu from the chevron.
- Escape closes the menu and returns focus to the chevron.
- A change of `Label` is announced politely.

## Motion

- The label changes letter by letter: shared letters glide, and changed letters rise out of a soft blur. The icon
  crossfades, and the main half springs to the new width.
- The main half presses in slightly. The chevron never scales, and it turns while the menu is open.
- The menu opens 4px below the chevron and its rows fade in one after another.
- Reduced motion removes the press, the width spring and the menu transform, and keeps a short fade. Off shows the
  menu and the label change at once.

## Related

- `DropdownMenu` for a menu of equal options with no default action.
- `Button` for a single action without alternatives.
- `ButtonGroup` for several related actions shown side by side.
- `MenuEntry` is the row type of both the split button and the menus.
