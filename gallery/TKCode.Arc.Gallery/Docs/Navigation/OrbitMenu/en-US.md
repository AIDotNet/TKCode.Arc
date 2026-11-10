---
title: Orbit menu
summary: A round button that fans its actions out around itself on an arc, one after another, with the arrow keys walking around the ring.
---

<Demo name="Preview" />

## When to use

`OrbitMenu` is a round trigger that keeps a few related actions folded inside it. Pressing it sends the actions out
one after another, each springing to its place on an arc around the trigger, while the trigger's plus turns into a
close mark. Picking an action, pressing outside or Escape folds them back in. Because every action is the same
distance from the thumb or the pointer, the choice is quick and easy to reach.

- **Create menus**: a floating plus that offers photo, voice, text or file.
- **Quick reactions** on a message or a post, on a full ring.
- **Tool palettes** on a canvas, opening where the hand already is.

## When not to use

- **More than about eight actions**: the arc gets crowded; use a menu with labels.
- **Actions that need their names to be understood**: the ring shows glyphs, and names only as tooltips.
- **Primary navigation**: places to go belong in a tab bar or a nav, not behind a button.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OrbitMenu Label="Create">
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Camera}" Label="Photo" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Mic}" Label="Voice note" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.PenLine}" Label="Text note" />
    </arc:OrbitMenu>
</UserControl>
```

`OrbitMenu` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Its items are
`OrbitMenuAction` elements, each with a `Glyph` and a `Label` (the tooltip and the accessible name), or bound through
`ItemsSource`. An action raises its own `Click` and runs its `Command`; the menu raises `ActionInvoked` with the action
and folds back, unless `ClosesOnInvoke` is false. `IsOpen` (two-way), `Open()`, `Close()` and `Toggle()` control it
from code; `Label` names the trigger and `Glyph` changes its glyph.

The menu takes only the trigger's room. The actions are drawn over what is around it, so leave them space: `Radius`
(88 px by default) plus half an action on the sides they open to.

### The arc

`StartAngle` is where the first action goes and `SweepAngle` how far round the others follow, in degrees clockwise from
the right (-90 is straight up, 180 is left). The default fans over the top from left to right. A sweep of 360 spreads
the actions evenly round a full ring.

<Demo name="FullRing" />

### Best practices

**Do**

- Give every action a short `Label`: it is the only name assistive technology has.
- Point the arc into open space, such as up from a trigger at the bottom of the screen.
- Keep to three to six actions.

**Avoid**

- Hiding the only way to do something important behind the ring.
- Placing the trigger where the arc would leave the window.

## API reference

<!-- api: OrbitMenu -->

<!-- api: OrbitMenuAction -->

## Accessibility

- The trigger is a button named by `Label` that reports whether the ring is expanded.
- Each action is announced as a menu item named by its `Label`, and can be invoked by assistive technology. Folded
  actions are hidden from it and from the tab order.
- Opening from the keyboard moves focus to the first action; closing returns it to the trigger. Pointer use shows no
  focus ring; keyboard use shows the shared ring around the trigger or the action.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space on the trigger | Opens or closes the ring. |
| Right or Down | Moves to the next action round the ring, wrapping. |
| Left or Up | Moves to the previous action, wrapping. |
| Home / End | Moves to the first or last action. |
| Enter or Space on an action | Runs the action and folds the ring. |
| Escape | Folds the ring and returns focus to the trigger. |
| Tab away | Folds the ring. |

## Motion

- On opening, each action leaves the trigger 35ms after the one before, growing from 40% of its size and springing
  out to its place with a light bounce. On closing they return in reverse order on a quick spring without bounce.
- The trigger's glyph turns 45 degrees on a snappy spring, so a plus becomes a close mark.
- Actions move with render transforms only; nothing is laid out while they travel.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Actions out | Staggered springs from the trigger | Fade in place, 160ms | Appear |
| Actions in | Quick springs back, in reverse order | Fade out, 180ms | Disappear |
| Trigger glyph | Turns on a spring | Turns at once | Turns at once |

## Responsive behavior

- The trigger keeps its size (52 px) at every width, comfortable for a thumb.
- The arc keeps its `Radius`; on a phone, point it into the screen (for example up from a trigger at the bottom) and
  leave the room it needs.
- Actions are 44 px across, a comfortable touch target.

## Related

- `DropdownMenu` lists actions with their names.
- `ActionMorph` opens one action into a small panel.

| Component | Choose it when |
|---|---|
| `OrbitMenu` | A few well-known actions, one quick reach away |
| `DropdownMenu` | Actions need their names, or there are many |
| `ActionMorph` | One action needs a field or a few options |
