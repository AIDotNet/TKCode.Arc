---
title: Dropdown menu
summary: A focused list of actions anchored to a trigger.
---

## When to use

- A short group of commands behind a labelled trigger, such as Rename, Duplicate and Delete.
- A row or card action menu where a visible entry point is clearer than a right-click.
- A list that groups related rows with separators and puts the destructive row last.

## When not to use

- A default action with a few alternatives: use `SplitButton`.
- Right-click actions on a piece of content: use `ContextMenu`.
- The account menu behind an avatar, with identity and theme: use `UserMenu`.
- A plain list of choices that are part of a form: use a select or a radio group.

## Usage

Declare the rows inline. Use `MenuEntry` for a command, `MenuEntrySeparator` for a divider and `MenuEntryLabel` for a
group heading.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DropdownMenu Label="Actions">
        <arc:MenuEntry Header="Rename" />
        <arc:MenuEntry Header="Duplicate" />
        <arc:MenuEntry Header="Delete"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

Set an icon from code with `ArcIcons`, a 24x24 Lucide geometry.

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### Handling a selection

`ItemClicked` reports the activated row. The menu closes after the event unless the row sets `StaysOpenOnClick`.

```csharp
menu.ItemClicked += (_, e) => status.Text = $"Selected: {e.Header}";
```

### Live label and radio rows

Set `Label` from code to change the trigger text. Radio rows with the same `GroupName` are mutually exclusive and
keep the menu closed when chosen. Check rows (`ToggleType="CheckBox"`) with `StaysOpenOnClick="True"` stay open so
several options can be flipped in one visit.

```xml
<arc:DropdownMenu Label="Sort: Newest" Alignment="Start">
    <arc:MenuEntryLabel Text="Sort by" />
    <arc:MenuEntry Header="Newest" ToggleType="Radio" GroupName="sort" IsChecked="True" />
    <arc:MenuEntry Header="Oldest" ToggleType="Radio" GroupName="sort" />
    <arc:MenuEntry Header="Name" ToggleType="Radio" GroupName="sort" />
</arc:DropdownMenu>

<arc:DropdownMenu Label="View">
    <arc:MenuEntry Header="Show sidebar"
                   ToggleType="CheckBox"
                   IsChecked="True"
                   StaysOpenOnClick="True"
                   InputGestureText="Ctrl+B" />
    <arc:MenuEntry Header="Wrap lines"
                   ToggleType="CheckBox"
                   StaysOpenOnClick="True" />
</arc:DropdownMenu>
```

### Data-driven rows and a custom trigger

`ItemsSource` creates one row per item. `Trigger` replaces the built-in button with any control. The menu anchors to
that control, and a `Button` opens it on click.

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

### Opening from code

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

## API reference

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## Accessibility

- The trigger is announced with its `Label` and reports whether the menu is expanded.
- Rows have the menu item role. Typeahead moves to a row by its first letters, and disabled rows are skipped.
- Enter, Space or ArrowDown on the trigger opens the menu on the first row. Escape closes it and returns focus to the
  trigger.
- A destructive row is marked by its colour only, so its `Header` should say what will be deleted.
- Icons and the highlight are hidden from assistive technology.

## Motion

- The menu grows from the trigger edge on a spring with a short fade. Closing is quicker and smaller.
- One highlight glides between rows for the pointer and jumps for the keyboard. Rows fade in with a short stagger.
- A new `Label` rises in with a soft blur while the trigger width adjusts to it. The chevron turns when the menu opens.
- Reduced motion removes the scale and the glide and keeps a quick fade. Off shows the menu at once.

## Related

- `ContextMenu` for secondary actions on a selected object, opened by right-click.
- `SplitButton` when one action is the default and the menu holds the alternatives.
- `UserMenu` for the account entry point in an app's top bar.
- `MenuSurface` is the shared menu popup, for anchoring a menu to any control.
