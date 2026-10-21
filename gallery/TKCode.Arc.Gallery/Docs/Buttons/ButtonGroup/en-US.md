---
title: Button group
summary: Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
---

<Demo name="DocumentActions" />

## When to use

- Two to five related actions on the same object, such as Share, Duplicate and Archive on a document.
- Zoom controls or a small editing toolbar where the segments act on one thing.
- Rarer or destructive actions that belong with a row of common ones, collected in an attached menu.

## When not to use

- Options that stay selected, such as a view mode: use `SegmentedControl` from Toggles.
- One primary action with a few close alternatives: use `SplitButton`.
- Switching between views or panels: use `Tabs`.

## Usage

Each segment is a `ButtonGroupItem`. Give the group a `Label`, which is its accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ButtonGroup Label="Document actions">
        <arc:ButtonGroupItem Label="Share" />
        <arc:ButtonGroupItem Label="Duplicate" />
        <arc:ButtonGroupItem Label="Archive" />
    </arc:ButtonGroup>
</UserControl>
```

### Icons and icon-only segments

Set `Icon` to a 24 by 24 Lucide geometry in code. `IsIconOnly` hides the label, which then becomes the tooltip and
the accessible name.

```xml
<arc:ButtonGroup Label="Zoom" Size="Small">
    <arc:ButtonGroupItem Label="Zoom out" IsIconOnly="True" />
    <arc:ButtonGroupItem Label="Zoom in" IsIconOnly="True" />
</arc:ButtonGroup>
```

### Live value and label change

`Value` shows content in place of the label, such as a zoom level. `Label` stays the accessible name. A label can
also change after a press; list the other labels in `ReserveLabels` so the segment keeps the widest width.

<Demo name="LiveValue" />

```csharp
var zoomValue = new ButtonGroupItem { Label = "Reset zoom", Value = "100%" };
zoomValue.Click += (_, _) => zoomValue.Value = "100%";

var share = new ButtonGroupItem { Label = "Share", ReserveLabels = ["Copied"] };
share.Click += async (_, _) =>
{
    share.Label = "Copied";
    await Task.Delay(1600);
    share.Label = "Share";
};
```

### Attached menu

`Menu` adds a chevron segment at the end of the row. `MenuLabel` names it. The menu opens aligned to the group's
edge, and `MenuItemClicked` reports the chosen row.

```xml
<arc:ButtonGroup Label="Document actions" MenuLabel="More document actions">
    <arc:ButtonGroupItem Label="Share" />
    <arc:ButtonGroupItem Label="Archive" />
    <arc:ButtonGroup.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="Pin" />
            <arc:MenuEntry Header="Delete" IsDestructive="True" HasSeparatorBefore="True" />
        </arc:MenuSurface>
    </arc:ButtonGroup.Menu>
</arc:ButtonGroup>
```

### Variants, orientation and events

`Variant="Solid"` uses the primary fill; use it at most once per screen. `Orientation="Vertical"` stacks the
segments, and the menu then aligns to the start. `ActionInvoked` runs after a segment's own click handler.

<Demo name="Solid" />

<Demo name="Vertical" />

```csharp
document.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

### Collapsing labels

When the row does not fit, segments drop their labels and keep the icon; they come back once there is room for the full row.

<Demo name="CollapsingLabels" />

### Disabled

A segment with `IsDisabled` stays focusable and reports unavailable; a group with `IsEnabled` set to `False` dims as a whole.

<Demo name="Disabled" />

## API reference

<!-- api: ButtonGroup -->

## Accessibility

- The group has the Group role and is named by `Label`.
- Every segment is a Tab stop. Arrow keys move between segments along the orientation and wrap around. Home and End
  move to the first and last segment.
- Enter or Space activates a segment. On the menu segment, Enter, Space or ArrowDown opens the menu.
- Icon-only and collapsed segments use their label as the name and tooltip, and their icons are hidden.
- A label that changes shortly after a press is announced once. The state is never shown by color alone.

## Motion

- One highlight glides to the pointer or keyboard focus. The dividers beside it fade out as it passes.
- A pressed segment dips its content slightly and springs back. The group frame and its neighbours do not move.
- A segment whose label changes resizes on a spring, or crossfades when its width is reserved.
- The menu grows out of the group's edge and the chevron turns while it is open.
- Reduced motion jumps the highlight to its target, removes the press dip and the chevron turn, and crossfades the
  labels and the menu. Off moves everything to its final state at once.

## Related

- `SegmentedControl` for a choice that stays selected.
- `SplitButton` for one default action with a menu of alternatives.
- `FloatingButtonGroup` for separate soft buttons in a tray.
- `ExpandingButtonGroup` for compact icon buttons whose labels appear on focus.
