---
title: Morph loader
summary: A tiny loader whose four strokes morph between dots, bars, a ring and a square, then fold into a check or a cross when the work finishes.
---

<Demo name="Preview" />

## When to use

`MorphLoader` is a loading mark made of only four strokes. While work runs it loops in one of four shapes; when the
work ends the same strokes fold into a check or a cross in the same spot, with one small pop. Going back to loading
gathers them into the shape again, so one loader serves request after request.

- **Inline progress where the result should appear in the same place**: inside a button, a table row, a field or a
  toolbar, next to the thing that is saving.
- **Short operations with a clear outcome**: saving, publishing, uploading, sending, syncing.
- **Anywhere a spinner should be small and crisp**: it is vector-drawn at any size and inherits the text color, so it
  matches the icons around it.

## When not to use

- **The amount of work is known**: show a `Progress` bar with a percentage instead.
- **Content is loading into a layout**: reserve the space with a `Skeleton`, or let a `SkeletonMorph` grow into the
  content.
- **The whole button should change its label as well**: use a button with its own loading state, or put a decorative
  loader inside your button as shown below.
- **The work takes minutes and needs steps or a cancel action**: show its progress in a status pill or a panel.

## Usage

Set `Status` from your request state and leave `Variant` the same across your product. With nothing set, the loader
is a 24px row of three hopping dots.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphLoader Variant="Ring" Size="20" Status="{Binding SaveState}" />
</UserControl>
```

`MorphLoader` derives from Avalonia's `Control` and draws itself; it has no template. It lives in the Special package:
register it with `UseArcSpecial()` next to `UseArcControls()`.

The preview above rebuilds a small test bench: a shape picker with a gliding pill, and Resolve and Reject buttons that
set `Status` to `Success` or `Error`. After 1.7 seconds the bench sets `Status` back to `Loading`, and the two buttons
are disabled while a result shows. The shape can change at any time, also in the middle of a morph or while a result
shows. The bench, its timer and its labels ("Saving", "Saved", "Couldn't save") belong to the demo, not to the loader.

```csharp
loader.Status = MorphLoaderStatus.Loading;
try
{
    await api.SaveAsync(draft);
    loader.Status = MorphLoaderStatus.Success;
}
catch (HttpRequestException)
{
    loader.Status = MorphLoaderStatus.Error;
}
```

### Shapes and results

Every shape and both results, at 32px. The second row turns `Tone` off, so the check and the cross keep the text color.

<Demo name="States" />

| Shape | While loading |
|---|---|
| `Dots` (default) | Three dots hop 3.2 grid units in a wave, one after another, and grow slightly at the top of the hop |
| `Bars` | Four bars grow and shrink in turn from left to right, between 4.5 and 12 units |
| `Ring` | Four arcs of a ring turn clockwise (330 degrees a second) while the arcs breathe shorter and longer |
| `Square` | A square rests, then tumbles a quarter turn while its corners open and close again |

| Status | Result |
|---|---|
| `Loading` | The variant's loop |
| `Success` | A check in the success tone |
| `Error` | A cross in the danger tone |

### Sizes

The drawing is a 24 unit grid scaled to `Size`, and the loader always takes exactly `Size` by `Size`, so a result never
shifts the layout. 16 to 32px suit inline use; 48px and more suit an empty state. At 16px set `StrokeWidth` to 3 so the
strokes match the weight of the icons around them.

<Demo name="Sizes" />

`StrokeWidth` is in grid units and applies to the ring, the square, the check and the cross; the dots and bars keep
their own widths (4 and 3 units).

### Inside a button

A loader inside a control that announces its own state should be `IsDecorative`, so screen readers do not hear the
state twice. Here the button changes its label and accessible name; the loader only draws. `Settled` is raised once a
result has finished folding, popping and taking its color, which is a good moment to reset the button.

<Demo name="InButton" />

```csharp
loader.Settled += (_, e) =>
{
    // e.Status is Success or Error
    ScheduleReset();
};
```

### Color

The loader inherits `Foreground` like text, so it takes the color of the button or line it sits in. With `Tone` on (the
default) the check takes the theme's success color and the cross its danger color, over 240ms; going back to loading
returns to the foreground color. Set `Foreground` to give the loader its own color, for example the accent in an empty
state.

### Reduced motion

Under reduced motion each state is a single static shape (three dots, four bars, the four arcs of the ring, a closed
square, a check, a cross) and every change swaps at once, color included. While loading, the still shape breathes in
opacity (from full to 45 % and back every 1.8 s) so it still shows that work is in progress; with motion off it does
not. Announcements are unchanged, and the result is never shown by color alone.

<Demo name="ReducedMotion" />

### Anatomy

1. **Strokes**: four strokes with round caps. Each is a center, a length, a direction, a bend, a width and an opacity.
   The dots hide the fourth stroke at the center; it grows out of the middle when another shape needs it.
2. **Drawing**: the four strokes turn and scale together around the center: the ring and the square turn while
   loading, a result turns forward to upright and pops.
3. **Footprint**: a square of `Size`, centered in the space it gets.

### Content guidelines

The loader has no visible text; its labels are for screen readers. Name the work in the present tense and the result
as what happened.

| Instead of | Write |
|---|---|
| Loading | Saving |
| Success | Saved |
| Error | Couldn't save |
| Please wait while we upload your file | Uploading |

### Best practices

**Do**

- Pick one variant for your product and keep it everywhere.
- Return `Status` to `Loading` for the next request instead of creating a new loader: the strokes gather back into the
  shape.
- Set `IsDecorative` inside buttons and rows that announce their own progress.
- Give `Labels` that name the work ("Publishing", "Published", "Couldn't publish").

**Avoid**

- Leaving a result on screen with no way to retry an error.
- Using it for work that takes more than a few seconds without saying what is happening.
- Several loaders for one request on the same screen.

### Common questions

**Does a result ever change the loader's size?** No. The pop is a render scale; the footprint is always `Size`.

**What happens if the status changes in the middle of a morph?** Every stroke parameter is on its own spring that keeps
its speed when it gets a new target, so the strokes bend smoothly toward the new shape.

**Can I change the variant while a result shows?** Yes. Nothing changes on screen until `Status` returns to
`Loading`; then the strokes gather into the new shape.

**Does it run when nobody can see it?** No. The frame loop runs only while the loader is loading or settling, and
pauses when it is hidden or scrolled out of view.

## API reference

<!-- api: MorphLoader -->

<!-- api: MorphLoaderLabels -->

<!-- api: MorphLoaderSettledEventArgs -->

The enums:

| Type | Values |
|---|---|
| `MorphLoaderVariant` | `Dots` (default), `Bars`, `Ring`, `Square` |
| `MorphLoaderStatus` | `Loading` (default), `Success`, `Error` |

## Accessibility

- The loader is a polite live region. Its accessible name is the label for the current status (`Labels.Loading`,
  `Labels.Success` or `Labels.Error`; "Loading", "Done" and "Failed" by default), so a change is read once.
- A result is never shown by color alone: the strokes become a check or a cross.
- `IsDecorative` hides the loader from assistive technology and silences it, for loaders inside a control that
  announces its own state.
- The loader cannot be focused and takes no pointer input.

### Keyboard

The loader has no keyboard interaction. In the preview bench every shape option and both buttons are tab stops;
`Enter` or `Space` presses them, and the result buttons leave the tab order while they are disabled.

## Motion

- Every stroke parameter has its own critically damped spring. Shape to shape morphs settle in about half a second
  (visual duration 0.46 s), all strokes together. Leaving the dots, the strokes travel first and stretch into lines a
  moment later.
- A result freezes the loop in its resting shape for one frame, then the strokes fold into the check or cross one after
  another, 35ms apart (visual duration 0.52 s). The check is drawn along the pen's path: the short side, then the long
  side from the corner up.
- At the same time the drawing turns forward to the next upright position, keeping the speed it had (visual duration
  0.6 s), and pops once: 1, 1.14, 1 over 420ms.
- Back to loading, the strokes gather together and the loop fades in within about 100ms; the ring speeds up with it.
- The color eases to the success or danger tone over 240ms and back.
- Changes in the middle of a motion keep their speed.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Loading loop | Animated | Still shape that breathes in opacity (1.8 s) | Static shape |
| Shape change | Morphs | Instant | Instant |
| Result | Folds, turns upright and pops | Instant | Instant |
| Color | 240ms | Instant | Instant |

## Responsive behavior

- The loader is vector-drawn at any size and never changes its footprint.
- It is centered in the space it gets and is never stretched.
- The frame loop runs only while loading or settling, and pauses off screen or while hidden.

## Related

- `Progress` for work with a known amount.
- `Skeleton` and `SkeletonMorph` for content loading into a layout.
- `Button` with its own loading state when the button itself shows the progress.

| Component | Choose it when |
|---|---|
| `MorphLoader` | A short operation should end in a check or a cross in the same spot |
| `Progress` | You know how much of the work is done |
| `Skeleton` | Content is about to fill a layout |
| `SkeletonMorph` | Placeholders should grow into the content |
