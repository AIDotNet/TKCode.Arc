---
title: Sand text
summary: A line of text that crumbles into grains of sand and drifts off on the wind, then gathers back grain by grain into the same letters.
---

<Demo name="Preview" />

## When to use

`SandText` shows one line of large text that can come apart. When it is set off, a front sweeps across the letters from
the side the wind comes from; behind the front each letter breaks into small square grains that lift, flutter and fade
as the wind carries them away. Gathering plays the same paths backwards, so the grains fly home and become text again.
A click, hovering or code sets it off, and turning around half way is seamless.

- **Moments of letting go**: a deleted draft, an expired offer or a cleared notification, shown with a little poetry.
- **Hero headlines** that react to the visitor on a landing page or a portfolio.
- **Playful reveals**, where one word gives way so the next can arrive.

## When not to use

- **Text people need to read or act on**, such as errors or button labels: while it drifts it cannot be read.
- **Long or wrapping text**: `SandText` draws a single line, and very long lines use coarser grains to stay fast.
- **Real deletion feedback on its own**: pair it with a message or an undo, since grains are decoration.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SandText Text="Nothing stays" />
</UserControl>
```

`SandText` is a control in the Special package. Register the package with `UseArcSpecial()` next to `UseArcControls()`.
It measures its text like a `TextBlock` with no wrapping, and uses the display font at 52 px in the semibold weight by
default; set `FontSize`, `FontFamily`, `FontWeight` and `Foreground` as on any text. The grains take the text color.

### Triggers

`Trigger` decides who sets the text off. `Click` (the default) toggles on a click, a tap, Enter or Space. `Hover` blows
the text away while the mouse is over it and gathers it when the mouse leaves; on touch screens a tap toggles instead.
`None` leaves it to code.

<Demo name="Hover" />

### Wind and grains

`Drift` is the way the wind blows: `Right` (the default), `Left` or `Up`. The side the wind comes from crumbles first.
`Distance` is how far a typical grain travels before it has faded (110 px), and `Duration` how long the whole text
takes to go, and to come back (1.6 s). `GrainSize` is the side of a grain (2 px). `MaxGrains` (4000) caps the number of
grains: a long or very large line uses larger grains so it stays within the cap.

### From code

`IsDissolved` is the state and binds both ways: true blows the text away, false gathers it. `Dissolve()`, `Gather()`
and `Toggle()` do the same from code. A text that is already dissolved when it first shows starts gone, without motion.
`Dissolved` fires when the last grain has gone and `Gathered` when the last grain is home.

<Demo name="Code" />

### Best practices

**Do**

- Keep the text short and large: a word or a short phrase at 32 px or more reads best as sand.
- Give people a way back, either the same trigger or a visible Undo.

**Avoid**

- Using it for text whose meaning matters at the moment it disappears.
- Several sand headlines moving at once on one screen; one is a moment, many are noise.

### Common questions

**Is the text still there for assistive technology when it has blown away?** Yes. The accessible name is always the
text, and the help text says "Blown away" while it is gone.

**Why do the grains look larger on a long line?** The text is broken into at most `MaxGrains` grains, so a longer line
gets larger grains. Raise `MaxGrains` if you need finer sand on a long line.

## API reference

<!-- api: SandText -->

## Accessibility

- The accessible name is the text, so screen readers read it whether it is standing or gone. While it is gone the help
  text says "Blown away".
- With `Trigger` set to `Click` the text is a button: it takes keyboard focus, shows a focus ring for keyboard focus
  only, and can be invoked by assistive technology. With `Hover` it is focusable text; with `None` it is plain text.
- The grains are decoration: nothing about the text depends on them.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the text (when `Trigger` is not `None`) |
| `Enter` / `Space` | Blow the text away, or gather it back |
| `Escape` | Gather the text back |

## Motion

- A release front crosses the text from the windward side in the first 42% of `Duration`. Each grain waits a little
  longer behind the front at random, so the edge crumbles instead of cutting.
- A grain is picked up slowly and then carried (it speeds up as it goes), drifts sideways a little, flutters across
  its path, shrinks to about half its size and fades over the last 70% of its flight.
- Gathering runs the same timeline backwards at the same speed. Turning around half way continues from where every
  grain is.
- Grains are drawn by the control itself, one small square each, from a few cached colors; a frame allocates nothing.
  Ahead of the front the letters are drawn as ordinary text. Frames run only while the text moves and pause while it is
  out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Blow away | Grains drift off over `Duration` | The text fades out in 240 ms | The text disappears |
| Gather | Grains fly home over `Duration` | The text fades in in 240 ms | The text appears |

## Responsive behavior

- `SandText` is one line and as wide as its text. It does not wrap or shrink; on a narrow phone choose a smaller
  `FontSize` or a shorter text.
- Grains fly up to one and a half times `Distance` beyond the text and are drawn outside the control's box. Leave room around it
  and do not clip its parent if the whole drift should show.
- On touch screens a tap sets it off in both the `Click` and `Hover` modes, because touch has no hover.

## Related

- `TextMorph` changes one text into another without breaking it apart.
- `TextReveal` brings text in as it scrolls into view.

| Component | Choose it when |
|---|---|
| `SandText` | A word should come apart and come back, as a moment |
| `TextMorph` | A label changes to a new label |
| `TextReveal` | A headline should arrive, not leave |
