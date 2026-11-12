---
title: Orbit menu
summary: Quick actions for one object, fanned out on an arc from a round button. A single press, drag and release runs one; a short tap leaves the arc open for clicking.
---

<Demo name="Preview" />

## When to use

`OrbitMenu` puts between three and six one-step actions for a single object behind a round button. On press, the
actions sweep out of the button onto an arc above it, and that same press can finish the job: drag in an action's
direction, let go, and it fires. A short tap instead leaves the arc out, so the actions can be hovered and clicked like
a regular menu. Whenever the arc is showing, a pill between the button and the arc names the action being aimed at;
once an action fires, the button shows a check and the pill a short confirmation.

- **A photo, a note or a message** that needs a few quick verbs: like, save, send, delete.
- **Phones and tablets**, where one press-drag-release is quicker than opening a menu and then tapping inside it.
- **Floating create buttons** that offer a few kinds of new item.

## When not to use

- **Many actions, or names people need to read first**: a `DropdownMenu` or `ContextMenu` shows every name at once.
- **A mode or tool that stays on after it is picked**: that belongs in a `ToolDock` or a toolbar.
- **Only one action**: a plain button is clearer.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OrbitMenu Label="Note actions">
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Pin}" Label="Pin" DoneText="Pinned" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Send}" Label="Send" DoneText="Sent" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Trash2}" Label="Delete" DoneText="Deleted" Tone="Danger" />
    </arc:OrbitMenu>
</UserControl>
```

`OrbitMenu` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Its items are
`OrbitMenuAction` elements, or data bound through `ItemsSource` (each item's text becomes the action's label). Every
action has a `Glyph` and a `Label`, the name the pill shows while the action is targeted and the name assistive
technology reads. After a fire the pill shows `DoneText`, or the label when that is empty; `Tone="Danger"` paints a
destructive action in the danger color.

When an action fires it raises its own `Click`, runs its `Command`, and the menu raises `ActionInvoked` with the action
and its item; then the arc folds, the button shows a check and the pill shows the confirmation for about 1.4 seconds.
`IsOpen` (two-way), `Open()`, `Close()` and `Toggle()` control the arc from code. `Label` names the button and the menu;
the pill also shows it whenever the arc is out but no action is aimed at. `CancelText` is the pill's text when a drag
moves away from every action. With `Announces` on (the default), confirmations are also read out by screen readers.

### Aiming, tapping and cancelling

Aim depends on direction, not on hitting the action. Once the pointer is more than 34 px from the button's center,
the target is the action whose angle around the button is closest to the pointer's, even before the pointer reaches
it. The lens slides under that action, and actions close to the pointer are drawn slightly toward it. Releasing fires
the target. A tap that lasts under about a third of a second leaves the arc open; releasing near the center, far past
the arc, or after holding still closes it without firing anything. A click between the actions or anywhere outside
closes it too.

### Placement

The menu takes only the button's room in the layout and draws the arc over what is around it. `Direction` is the
angle the arc faces, in degrees clockwise from the right: the default -90 faces up, 90 faces down and 180 faces left.
`Spread` sets how many degrees it spans (150 by default; 360 spreads the actions round a full ring) and `Radius` how
far out the actions sit (104 px).

<Demo name="Placement" />

### Best practices

**Do**

- Keep to three to six actions, each with a short `Label` and a `DoneText`.
- Use `Tone="Danger"` for anything that deletes or removes.
- Aim the arc where nothing important sits, with `Radius` plus about 30 px of clear room.

**Avoid**

- Hiding the only way to do something important behind the button.
- Placing the button where the arc would leave the window: it is not moved back inside.

## API reference

<!-- api: OrbitMenu -->

<!-- api: OrbitMenuAction -->

## Accessibility

- The button is named by `Label`, reports whether the menu is expanded and that it opens a menu.
- Each action is a menu item named by its `Label` and can be invoked by assistive technology. Folded actions are hidden
  from it and from the tab order, and the actions take a single stop in the tab order.
- The keyboard-focused action is the target: the lens sits under it and the pill names it, so focus is always visible.
- With `Announces` on, a polite live region reads each confirmation.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space on the button | Toggles the arc; when it opens, the first action has focus. |
| An arrow key on the button | Shows the arc with focus on the first action, or on the last one for Up. |
| Right or Down | Next action; after the last comes the first. |
| Left or Up | Previous action; before the first comes the last. |
| Home / End | First or last action. |
| Enter or Space on an action | Fires it; focus returns to the button. |
| Escape | Folds the arc; the button gets focus back. |
| Tab | Folds the arc, and focus goes on to the next control. |

## Motion

- Opening, the actions leave the button one by one in item order, 35 ms apart: each curves round to its spot on the
  arc while it grows from 40% and its soft blur clears. Closing runs the other way round, and quicker.
- A thin track draws out from the middle of the arc to both ends behind the actions.
- Between targets the lens slides along the arc, small while it moves and full size once it arrives. Actions close to
  the pointer are pulled slightly toward it and swell a little (mouse and pen only).
- On the button, the ellipsis changes shape into a close mark when the arc opens and into a check after a fire. The
  pill rises in, resizes on a spring to fit its text, and swaps text through a soft blur.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Actions out and in | Staggered swirl, scale and blur on springs | Fade in place | Appear and disappear |
| Lens | Glides on a spring | Jumps, fades | Jumps |
| Nearby actions | Lean and grow | Grow only | Grow only |
| Button mark | Morphs on a spring | Changes at once | Changes at once |
| Pill | Rises and grows in, text blur swap | Fades, text crossfade | Appears |

## Responsive behavior

- The button (56 px) and the actions (46 px) keep their size at every width; both are comfortable touch targets.
- On touch, press, drag and release works the same; the lean toward the pointer is for mouse and pen only.
- Nothing keeps the arc inside the window: on a phone, aim it toward the middle of the screen and leave it the room
  it needs.

## Related

- `DropdownMenu` lists actions with their names.
- `ContextMenu` keeps secondary actions next to the selected object.
- `ActionMorph` opens one action into a small panel.

| Component | Choose it when |
|---|---|
| `OrbitMenu` | Three to six well-known actions on one object, one gesture away |
| `DropdownMenu` | Actions need their names, or there are many |
| `ActionMorph` | One action needs a field or a few options |
