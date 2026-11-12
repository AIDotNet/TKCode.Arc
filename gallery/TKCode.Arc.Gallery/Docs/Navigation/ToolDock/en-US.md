---
title: Tool dock
summary: A floating toolbar for the tools and modes of an editor, such as a canvas: the tool in use keeps a tinted backdrop, hovering shows its name and key, and variants of one tool share a slot that opens into a tray.
---

<Demo name="Preview" />

## When to use

`ToolDock` is a floating strip for the tools of an editor that people swap over and over while they work: select,
pan, draw, type, comment. The active tool sits on a soft accent tint, and choosing another tool moves the tint over to it. A small
label above the dock names the tool under the mouse or keyboard focus, together with its key, and travels along as you
go. Variants of one tool (rectangle, ellipse, line) can share a single slot that opens a tray above the dock, made of
the same material; whatever is picked there becomes the slot's icon.

- **Pointer tools of a visual editor**: select, pan, shapes, text, sticky notes, comments.
- **Editor modes** where one stays active until another replaces it.
- **Toolbars people customize**: with reordering on, slots can be dragged, or moved with the arrow keys while Alt is
  held.

## When not to use

- **A choice among two to four options** inside a form or a toolbar: a `SegmentedControl` fits better.
- **Commands that just run**, such as undo or export, and leave nothing selected: ordinary buttons are the right tool.
- **Navigation between parts of an app**: `Tabs` or a `LiquidTabBar`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ToolDock SelectedValue="select">
        <arc:ToolDockItem Value="select" Label="Select" Shortcut="V" Glyph="{x:Static arc:ArcIcons.Move}" />
        <arc:ToolDockItem Value="shapes" Label="Shapes">
            <arc:ToolDockItem Value="square" Label="Square" Shortcut="R" Glyph="{x:Static arc:ArcIcons.Square}" />
            <arc:ToolDockItem Value="circle" Label="Circle" Shortcut="O" Glyph="{x:Static arc:ArcIcons.Circle}" />
        </arc:ToolDockItem>
        <arc:ToolDockItem Value="text" Label="Text" Shortcut="T" Glyph="{x:Static arc:ArcIcons.Type}" />
    </arc:ToolDock>
</UserControl>
```

`ToolDock` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Each entry is a
`ToolDockItem` with a `Value`, a `Label`, a 24 × 24 `Glyph` and, if you like, a `Shortcut` and a `Badge`.
`SelectedValue` (two-way) is the `Value` of the chosen entry; `ItemChosen` reports every choice made by pointer,
keyboard or assistive technology. The key hint is only shown: handle the keys on the page and set `SelectedValue`, as
the preview above does. Entries can also come from `ItemsSource`, with an `ItemContainerTheme` that binds the
containers' properties.

Each slot is 40 px and the dock is simply as wide as its slots added up; nothing is squeezed to fit. Keep free space
above the dock, because both the tray and the floating label appear there.

### Groups and trays

An entry with members nested inside it becomes a group. Pressing a group makes its visible member the current tool and
lifts a tray with every member above the slot; picking from the tray makes that member current and puts the tray away.
From then on the slot wears the picked member's glyph (the old one blurs out as the new one fades in), even while other
tools are in use. Groups carry a small mark in one corner, so they are easy to tell apart from single tools. A press
anywhere beyond the dock dismisses the tray; in code, call `CloseTray()`.

<Demo name="Groups" />

### Reordering

Set `CanReorder` to let people rearrange the dock: a slot dragged sideways lifts and its neighbours slide out of its way,
and Alt with the left or right arrow moves the focused slot. The inline items, or an `ItemsSource` that is a changeable
list, are reordered in place, and `Reordered` reports each move with the item and its old and new index. Only
horizontal drags reorder; scrolling the page with a vertical swipe keeps working over the dock.

<Demo name="Reorder" />

### Badges

`Badge` puts a count on a slot's corner, for example unresolved comments. The digits roll when the count changes, and
the badge leaves when it reaches 0. The count is part of the slot's accessible name.

<Demo name="Badges" />

### Best practices

**Do**

- Give every entry a short `Label`: it is the floating label and the accessible name.
- Pick glyphs that are clear without their names, since touch never brings up the floating label.
- Keep the dock short, about four to eight slots, and group variants of one tool.

**Avoid**

- Filling the dock with commands that leave nothing selected.
- Placing the dock where there is no room above it for the tray and the label.

## API reference

<!-- api: ToolDock -->

<!-- api: ToolDockItem -->

## Accessibility

- The dock is a toolbar named by `Label` ("Tools" by default) with one tab stop: Tab enters it at the last focused slot
  (or the chosen one) and the arrow keys move along it.
- A single tool is a button that reports whether it is pressed (chosen). A group is a button that reports whether its
  tray is expanded; its tray is a group named after it and is hidden from assistive technology while closed.
- Every slot's accessible name ends with its key hint and, if there is one, its badge count: "Text, T", "Comment, C, 2".
  For a group the name joins the group's label and its visible member, as in "Shapes: Circle, O", and adds "selected"
  while the current tool is one of its members.
- Because the floating label only repeats the accessible name, it is hidden from screen readers. Mouse hover and
  keyboard focus bring it up; touch never does. A focus ring appears for keyboard use only.

### Keyboard interactions

| Key | Action |
|---|---|
| Left / Right | Steps from slot to slot, wrapping at the ends; inside an open tray, steps between the members. |
| Home / End | Moves to the first or last slot (or member). |
| Enter or Space | Makes the focused tool current. On a group, the visible member becomes current and the tray is shown or hidden. Inside a tray, the member becomes current, the tray is hidden and focus goes back to the group. |
| Up | From a group: shows the tray, with focus on the member the slot holds. |
| Down | From a tray: hides it and focuses its group. |
| Tab | From a tray: hides it and lets focus continue past the dock. |
| Alt + Left / Right | Moves the focused slot when `CanReorder` is true. |
| Escape | Hides the open tray and focuses its group. |

## Motion

- The label waits a moment the first time (400 ms for the mouse, about 200 ms for the keyboard), then rises into place
  with a small scale. While it is up, or shortly after the pointer left, it moves straight to the next tool, gliding on a
  spring while its text swaps with a quick blur.
- The chosen tool's tint slides to the new tool on a spring with a hint of give; it keeps its shape all the way.
- Pressing a slot dips it to 92% within 90 ms before it springs back.
- A tray grows out of the group's slot to its full size, and its members fade, scale and rise in one after another,
  35 ms apart. Closing sinks the tray back into the dock.
- A dragged slot lifts to 112% and its neighbours slide aside on a smooth spring.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Label | Rises, glides and blurs its text | Fades in place, jumps between tools | Appears |
| Tint | Slides on a spring | Jumps | Jumps |
| Tray | Grows and sinks, members staggered | Opens and closes at once | Opens and closes at once |
| Group glyph | Shrinks, blurs and crossfades | Crossfades | Swaps |
| Press and drag | Dip, lift and sliding neighbours | None; neighbours jump | None |
| Badge | Digits roll, pill pops in and out | Digits jump, pill fades | Changes at once |

## Responsive behavior

- Slots stay 40 px at every width, comfortable for touch; keep the dock short on phones.
- The dock never shrinks below its slots; place it where its full width fits.
- Without hover, touch users never see the floating label, so every glyph has to be recognizable alone.
- A swipe up or down that starts on the dock scrolls the page as usual; reordering needs a horizontal drag.

## Related

- `MagnifyDock` is an app launcher whose icons swell under the pointer.
- `LiquidTabBar` moves between the main areas of an app.
- `SegmentedControl` picks one of a few inline options.

| Component | Choose it when |
|---|---|
| `ToolDock` | Tools or modes that stay chosen, in a floating bar |
| `LiquidTabBar` | Places to go in an app |
| `SegmentedControl` | Two to four options inside a form or a toolbar |
