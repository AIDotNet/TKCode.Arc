---
title: Floating button group
summary: Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
---

## When to use

- Actions on a floating editor or canvas, such as Undo, Redo, Share and Present.
- A selection toolbar that appears over text, with icon-only buttons and shortcut hints.
- A vertical tool rail beside a canvas.

## When not to use

- A choice that stays selected, such as a text alignment: use `SegmentedControl` from Toggles.
- A toolbar that does not fit its row: use `DropdownMenu` from Menus for the overflow.
- Related actions joined into one surface with dividers: use `ButtonGroup`.
- A single action: use `Button`.

## Usage

Each button is a `FloatingButtonGroupItem`. Give the tray a `Label`, which is its accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FloatingButtonGroup Label="Board actions">
        <arc:FloatingButtonGroupItem Label="Undo" Shortcut="⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupItem Label="Redo" Shortcut="⇧⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="Share" />
    </arc:FloatingButtonGroup>
</UserControl>
```

### Icons and icon-only items

Set `Icon` to a 24 by 24 Lucide geometry in code. `IsIconOnly` on an item hides its label. Set `IsIconOnly` on the
tray to make every item icon only. An item's own setting wins over the tray's. Icon-only buttons and buttons with a
`Shortcut` show their label in a tooltip.

```xml
<arc:FloatingButtonGroup Label="Text formatting"
                         Variant="Floating"
                         IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="Bold" Shortcut="⌘B" />
    <arc:FloatingButtonGroupItem Label="Italic" Shortcut="⌘I" />
    <arc:FloatingButtonGroupSeparator />
    <arc:FloatingButtonGroupItem Label="Link" Shortcut="⌘K" />
</arc:FloatingButtonGroup>
```

```csharp
bold.Icon = ArcIcons.Bold;
link.Icon = ArcIcons.Link;
```

`Shortcut` only shows a hint. The control does not bind keys; handle the key yourself.

### Label change and reserved width

Set `ReserveLabels` to the other labels a button can show, such as "Copied". The button then keeps the width of the
widest label, so the neighbours do not move.

```csharp
var share = new FloatingButtonGroupItem { Label = "Share", ReserveLabels = ["Copied"] };
share.Click += async (_, _) =>
{
    share.Label = "Copied";
    await Task.Delay(1600);
    share.Label = "Share";
};
```

### Items from data and vertical layout

`ItemsSource` turns plain data into buttons. `Orientation="Vertical"` stacks the buttons in a column, and the
tooltips then open to the right by default. Set `TooltipPlacement` to change the side.

```xml
<arc:FloatingButtonGroup Label="Zoom" Size="Small" ItemsSource="{Binding ZoomLevels}" />

<arc:FloatingButtonGroup Label="Tools" Orientation="Vertical" IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="Select" Shortcut="V" />
    <arc:FloatingButtonGroupItem Label="Pen" Shortcut="P" />
</arc:FloatingButtonGroup>
```

### Events

`ActionInvoked` runs after an item's own `Click` handler and command. `e.Item` is the button that ran, and `e.Data`
is the source item.

```csharp
board.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

## API reference

<!-- api: FloatingButtonGroup -->

## Accessibility

- The tray has the ToolBar role, named by `Label`, and reports its orientation.
- The tray is a single Tab stop. Focus returns to the last button that had focus.
- Arrow keys move between enabled buttons and wrap. Home and End move to the first and last button.
- Enter and Space activate a button. A checked button reports its toggle state.
- Each button is named by its label. Separators are announced as separators. Shortcut hints are exposed as help text.
- Disabled buttons stay readable and are skipped by the arrow keys.

## Motion

- One highlight morphs in position and size to the button under the pointer or keyboard focus. It holds still when the
  pointer crosses a gap or a separator.
- A tray appears with its highlight growing out of the button. The highlight fades when the pointer leaves the tray.
- A pressed button darkens the highlight and the highlight contracts slightly. The button itself does not scale.
- Label changes crossfade in place. A button with reserved labels keeps its width; otherwise the highlight follows the
  new width.
- Reduced motion moves the highlight to its target with a short fade and crossfades the labels without a shift. Off
  puts everything in its final place at once.

## Related

- `ButtonGroup` for related actions joined into one surface with dividers.
- `ExpandingButtonGroup` for compact icon buttons whose labels expand on focus.
- `SegmentedControl` for a choice that stays selected.
- `Button` for a single action.
