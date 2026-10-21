---
title: Context menu
summary: Secondary actions kept close to the selected object.
---

<Demo name="FileTile" />

## When to use

- Secondary actions on a file, card or row, such as Copy link or Delete file, next to the object they act on.
- Actions that should not clutter the layout with a visible menu button.
- A short, flat list with one destructive row at the end.

## When not to use

- Actions that people need to find without a right-click or a long press: use `DropdownMenu` with a visible trigger.
- Touch-first lists with swipe actions: use `SwipeActions`.
- Content that needs its own click action: a plain click also opens the menu, so use a `Button` next to it instead.
- A default action with alternatives: use `SplitButton`.

## Usage

Wrap the target in `ContextMenu` and set its `Label`. The content is the target. Add the rows from code to `Items`. In the
snippet below, `file` is the `ContextMenu`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ContextMenu Label="Actions for Q3 report.pdf">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 report.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "Copy link", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "Delete file", Icon = ArcIcons.Trash2, IsDestructive = true });
```

### Handling a selection

`ItemClicked` reports the activated row. The menu closes after the event unless the row sets `StaysOpenOnClick`.

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 report.pdf: {e.Header}";
```

### Checked rows

A row without an icon shows a check when `IsChecked` is true. Toggle it in the row's `Click` handler.

```csharp
var pinned = new MenuEntry { Header = "Pinned", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

### Data-driven rows

`ItemsSource` creates one row per item. `MenuEntry` instances are used as they are; other data needs an `ItemTemplate`.

```xml
<arc:ContextMenu Label="File actions" ItemsSource="{Binding FileActions}">
    <TextBlock Text="{Binding Name}" />
</arc:ContextMenu>
```

### Attaching a menu to any control

`arc:ContextMenu.Menu` gives any control a right-click menu without wrapping it.

<Demo name="Attached" />

### Opening and closing from code

```csharp
file.Open(); // under the target
file.Close();
```

## API reference

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## Accessibility

- The target is focusable and the menu is announced with the `Label`, which names both the target and the menu.
- Rows have the menu item role. Enter and Space choose a row, and Escape closes the menu and returns focus to the target.
- Shift+F10 and the ContextMenu key open the menu under a focused target.
- A checked row without an icon shows a check, but the checked state is not announced. Put the state in the `Header`
  when it matters.
- Destructive rows are marked by colour only, so name what will be removed in the `Header`.

## Motion

- Right-click makes the menu grow from the pointer on a quick spring with a short fade. Rows fade in with a short stagger.
- A click, tap or Shift+F10 opens the menu under the target with the same motion.
- Scrolling or resizing closes the menu. It does not move to follow the target.
- Reduced motion removes the scale and the glide and keeps a quick fade. Off shows and hides the menu at once.

## Related

- `DropdownMenu` when the actions need a visible trigger.
- `SwipeActions` for touch-first rows that reveal actions by a swipe.
- `SplitButton` when one action is the default and the menu holds the alternatives.
- `MenuSurface` is the shared menu popup, for anchoring a menu to any control.
