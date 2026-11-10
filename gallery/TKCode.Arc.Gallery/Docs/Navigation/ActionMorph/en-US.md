---
title: Action morph
summary: A button that grows into a small inline panel for its action, such as a field or a few options, and shrinks back into the button when done.
---

<Demo name="Preview" />

## When to use

`ActionMorph` is a button that becomes the place where its action happens. Pressing "New list" does not open a dialog:
the button itself widens and deepens on a spring into a small panel holding a field and a Create button, and when the
list is created the panel shrinks back into the button. The action stays where the person was looking, and the page
around it moves only as much as the panel needs.

- **Quick creation**: a name for a new list, a tag, a folder.
- **A short choice**: snooze for an hour, this evening or tomorrow.
- **Inline input in toolbars and rows**, where a dialog would be too much.

## When not to use

- **Forms with several fields or validation steps**: use a dialog or a page.
- **Actions with consequences that need a warning**: use a confirm dialog.
- **Places where the layout must not move**: the panel takes room next to its neighbours; use a `Popover` instead.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionMorph Label="New list">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Input Placeholder="List name" Width="180" />
            <arc:Button Content="Create" Variant="Primary" Size="Small" arc:ActionMorph.Closes="True" />
        </StackPanel>
    </arc:ActionMorph>
</UserControl>
```

`ActionMorph` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. `Label` and
`Glyph` make the button, and the content is the panel. When the panel opens, focus moves to its first focusable
element. It folds back with Escape, its close button, a press outside, `Collapse()` or a click on any button inside
marked `arc:ActionMorph.Closes="True"`. `IsExpanded` is two-way, and `Expanded` and `Collapsed` are raised as it opens
and closes: clear the panel's fields in `Collapsed`.

`CollapsesOnOutsidePress` keeps it open when the person clicks elsewhere, and `ShowsCloseButton` hides the close button
when the content has its own way out.

### The primary face, and options

`Variant="Primary"` gives the button the accent fill. The open panel is always a plain surface, so the fill flows out
into it. A few ghost buttons marked `Closes` make a quick choice.

<Demo name="Options" />

### Best practices

**Do**

- Name the button with the action: "New list", "Add tag", "Remind me".
- Keep the panel to one line or a short column, so it reads as part of the button.
- Mark the button that finishes the action with `arc:ActionMorph.Closes="True"`.

**Avoid**

- Putting the morph where its growth would push important content out of view.
- Long content in the panel: it is not a page.

## API reference

<!-- api: ActionMorph -->

## Accessibility

- The button is named by `Label` and reports whether its panel is expanded. The open panel is a group with the same
  name, and the close button is named by `CloseLabel`.
- While the panel is open the button leaves the tab order; focus moves into the panel, and back to the button when it
  closes.
- Keyboard focus shows the shared focus ring around the button; pointer use shows none.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space on the button | Opens the panel and focuses its first element. |
| Tab | Moves through the panel's content and its close button. |
| Escape | Folds the panel and returns focus to the button. |

## Motion

- Opening and closing are one surface changing width and height on the morph spring, its corners going from the
  button's 18 px to the panel's 20 px and its shadow rising.
- The button fades out while the panel fades in; on the way back the panel keeps its room until it has faded, then the
  surface shrinks to the button.
- Only the morph is laid out while it moves; what is next to it moves with it.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Size | Morph spring | Jumps | Jumps |
| Corners, fill and shadow | Spring and 240ms | Fill in 120ms | Immediate |
| Button and panel | Crossfade | Crossfade | Swap |

## Responsive behavior

- The panel is at least 240 px wide, otherwise as wide as its content. Keep the content narrow enough for a 320 px
  phone.
- The button keeps its height (36 px) and its width follows its label.
- Neighbours in the same row move while the panel grows; place the morph at the end of a row when that matters.

## Related

- `Popover` opens content over the page without moving it.
- `OrbitMenu` offers a few actions around one button.

| Component | Choose it when |
|---|---|
| `ActionMorph` | One action needs a little input, right where the button is |
| `Popover` | The content must not move the layout |
| `Dialog` | The task needs several fields or a confirmation |
