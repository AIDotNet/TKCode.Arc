---
title: Sheet stack
summary: Drill-down panels that rise over one another, keep the level below in view, drag away to close and turn into stacked dialogs on wide screens.
---

<Demo name="Preview" />

## When to use

`SheetStack` holds a short chain of modal panels that open one from another: an account panel opens the profile, the
profile opens the avatar picker. Each new level rises over the last; the levels below shrink by 5%, darken and keep a
thin strip of their top edge in view, so people always see how deep they are and how to get back. On a narrow container
the levels are bottom sheets that can be dragged or flicked down; from 640 px up the same declarations become centered
dialogs stacked the same way.

- **Settings and account drill-downs** where each answer leads to one more question, two or three levels deep.
- **Filters with sub-pages**: a filter list whose entries open their own choice panel and come back.
- **Mobile-first flows** that should also feel at home on a wide screen without a second layout.
- **Edits with a confirmation step**: a level for the change and one more for "are you sure", which cannot be swiped away.

## When not to use

- **A single question**: one confirmation or one form is a `Dialog`; it needs no stack.
- **One bottom panel with snap points** (a map drawer, a music queue): use `BottomSheet`, which has detents.
- **Long linear processes**: checkout or onboarding with numbered steps belongs in a multi-step form with progress.
- **Deep navigation**: past three or four levels people lose the thread; use pages and a back button instead.

## Usage

Put the page content inside the stack and declare every level in `Sheets`. A level opens when its `Id` is pushed: give
any button the attached property `SheetStack.OpenSheet`, or call `Push` from code. The open levels are the two-way
`Stack` property, bottom first.

```xml
<arc:SheetStack IsContained="True">
    <arc:SheetStack.Sheets>
        <arc:SheetStackSheet Id="account" Title="Account">
            <Button Content="Profile" arc:SheetStack.OpenSheet="profile" />
        </arc:SheetStackSheet>
        <arc:SheetStackSheet Id="profile" Title="Your profile" BackLabel="Details">
            <arc:Input Label="Name" />
            <arc:SheetStackSheet.Footer>
                <arc:Button Content="Save changes" />
            </arc:SheetStackSheet.Footer>
        </arc:SheetStackSheet>
    </arc:SheetStack.Sheets>
    <arc:Button Content="Open settings" arc:SheetStack.OpenSheet="account" />
</arc:SheetStack>
```

### Anatomy

- **Scrim**: covers the page (or, with `IsContained`, only the stack's own area, clipped to its corner radius). A press
  on it closes the top level.
- **Level**: a raised panel with a 1 px edge. Sheets are at most 560 wide, sit on the bottom edge and show a grabber;
  dialogs are 440 wide and centered.
- **Header**: a back pill on the left (from the second level up), the title in the middle and a round close button on the
  right. The back pill names the level it returns to: that level's `BackLabel`, or its `Title`.
- **Body**: the level's content, scrolling on its own when it is taller than the space left.
- **Footer**: optional content pinned under the body and aligned right. Put several buttons in a horizontal
  `StackPanel` with `Spacing="8"`.
- **Levels below**: each one shrinks by 0.05, darkens (half a step per level, fully at two) and shows 10 px (sheets) or
  14 px (dialogs) of its top edge above the level over it. They stay drawn exactly as they were, but they take no
  input.

### Opening, closing and events

`Push(id)` opens a level on top; pushing an id that is already open closes everything above it instead. `Pop` closes
the top level, `PopTo(id)` returns to a level, and `Close` closes them all at once: the whole stack drops together.
Setting `Stack` to a new list opens and closes levels to match. Every change raises `StackChanged` with the old and new
stacks and a `Reason` (push, back button, close button, close all, Escape, scrim press, drag, or programmatic), and
`Pushing` lets you refuse a push before it happens.

<Demo name="Events" />

### Dialogs, descriptions and confirmations

`Mode` chooses the presentation: `Auto` (sheets below `Breakpoint`, 640 by default, dialogs at or above it), `Sheet` or
`Dialog`. A `Description` adds a line under the header that screen readers read as the panel's description. Set
`IsDismissible="False"` on a level that must be answered: a drag or flick no longer closes it (it follows the pointer
with resistance and springs back), while its buttons, Escape and the scrim still work. `Labels` replaces the words on the
close button.

<Demo name="Dialogs" />

### Narrow containers

With `IsContained` the breakpoint measures the stack's own width, so a stack in a phone-sized frame shows full-width
sheets whatever the window size. Without it the stack covers the window and measures the window.

<Demo name="Narrow" />

### Gestures

Only the top level moves. A press anywhere on it (header, body, footer) becomes a drag after 6 px of vertical travel and
then follows the pointer one to one; upward it resists and settles back. The levels below rise toward their next
position as the drag goes. On release the level closes when it was flicked down faster than about 450 px/s (any
distance) or dragged past 35% of its height plus 24 px; a fast flick up always keeps it. A body that is scrolled away
from its top scrolls instead of dragging, text boxes keep their own selection drag, and any element marked
`SheetStack.IsDragExcluded="True"` (a slider, a map, a canvas) never starts one. Mouse, pen and touch behave alike.

### Reduced motion

Under reduced motion levels fade in place (about 210 ms in, 140 ms out), the levels below only darken, and nothing scales,
slides or peeks. Dragging still follows the pointer and closes on the same rules.

<Demo name="ReducedMotion" />

### Content guidelines

- Titles are short nouns ("Account", "Pick avatar"); they become the back label of the next level, so keep them under
  about 16 characters or give a shorter `BackLabel`.
- One task per level. If a level needs its own sub-sections, it is probably two levels.
- Put the action that completes a level in the footer; a level that only shows choices can close itself when one is
  picked (call `Pop`).
- Keep it to three levels; the stack draws any number, but each extra one costs another strip of space and a step back.

### Best practices

- Return to the level that asked: after a choice, `Pop` rather than `Close`, so people see the result where they
  started.
- Keep unsaved edits in the level that owns them; a level that closes by drag loses nothing it has not committed.
- Use `IsDismissible="False"` only for real decisions; everywhere else the drag is the quickest way back.
- Use `IsContained` for a stack inside a card or a preview frame, and the default window cover for app-wide flows.

### Common questions

**Can a level open from the page while the stack is open?** Yes. In a contained stack the rest of the page stays usable;
a trigger there pushes onto the current stack (or pops back to its level if it is already open).

**How do I open several levels at once?** Set `Stack`, for example to `["filters", "frame", "chart"]`; the levels past
the common part rise together.

**Why does focus land on the panel and not its first field?** Like a dialog, the new level takes focus itself so that a
screen reader announces its title first; the next Tab goes to its first control.

## API reference

<!-- api: SheetStack -->

<!-- api: SheetStackSheet -->

<!-- api: SheetStackMode -->

<!-- api: SheetStackLabels -->

<!-- api: SheetStackChangedEventArgs -->

<!-- api: SheetStackChangeReason -->

<!-- api: SheetStackPushingEventArgs -->

<!-- api: SheetStackSamples -->

<!-- api: SheetStackSampleAvatar -->

## Accessibility

- Each level is a modal panel (a window in the automation tree) named by its `Title`, with its `Description` as help
  text.
- Only the top level takes input. The levels below keep their look but are out of reach: they are not hit-testable,
  Tab skips them and they expose no children to assistive technology.
- When a level opens, focus moves to it; when it closes, focus returns to the control that opened it (with its focus ring
  when the keyboard was used), and closing everything returns focus to the trigger on the page.
- Tab cycles through the top level's controls and skips disabled ones, so focus cannot leak out past a disabled footer
  button.
- The back button's accessible name is the label it shows; the close button reads "Close", or "Close all" while more
  than one level is open. Both can be reworded through `Labels`.
- The stack itself announces nothing; announce the outcome of an action (saved, removed) from your own live region, as
  the preview does.

### Keyboard

| Key | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the top level's controls, wrapping around inside it. |
| `Enter` / `Space` | Activate the focused trigger, back or close button. |
| `Escape` | Close the top level only, even when focus is elsewhere on the page. |

## Motion

| Moment | Motion |
|---|---|
| A sheet opens | Rises from below the edge on a spring (ω 12, ζ 0.945); the levels below shrink, darken and move up on a spring (ω 12.3, ζ 0.99). |
| A dialog opens | Fades in over 195 ms while it grows from 0.97 and settles from 18 px lower. |
| A level closes | Sheets fall their height plus 24 px (ω 15.3, ζ 1); dialogs fade out over 110 ms; the levels below return a step. |
| Close all | Every level drops together, keeping its own size and dim. |
| Scrim | Fades in over 300 ms and out over 240 ms. |
| Content resizes | The level's height follows on the smooth spring; the levels below keep their strip in view as it moves. |
| Interruptions | Every spring keeps its velocity: closing a level while it is still rising turns it around smoothly. |

Motion off settles everything at once with the same layout; reduced motion is described above.

## Responsive behavior

- `SheetStack` picks its shape from the viewport's width (the nearest viewport host, otherwise the window): below
  `Breakpoint` (640 px) panels are bottom sheets as wide as the screen up to 560 px; at 640 px or more they become
  stacked dialogs 440 px wide. `Mode` fixes one shape, and `ActualMode` reports the one in use.
- With `IsContained`, the stack stays inside the control's own bounds and measures the breakpoint on its own width, so it
  works in a side panel or a preview frame.
- A short top panel lets the panels below slide down and show more of their top edge, so the depth stays readable on a
  short screen. Long content scrolls inside the panel; the header and footer stay put.
- On touch, drag a sheet down by its grabber or header to close it. Every action is also a button (Back, Close, Close
  all), so nothing depends on dragging or hover.

## Related

- `BottomSheet`: one bottom panel with snap points.
- `Dialog`: a single modal question or form.
- `Drawer`: a side panel for navigation or details.
