---
title: Sketch to image
summary: An image generation visual that paints the way an artist does: pencil strokes sketch the scene, a round brush blocks in flat colors region by region, grain and warm light refine it, and it resolves into the finished picture.
---

<Demo name="Preview" />

## When to use

`SketchToImage` is a waiting state for image generation that shows the picture taking shape instead of a spinner.
Pencil lines sketch the scene stroke by stroke while a pen tip lifts and travels between strokes, a round brush sweeps
flat colors in region by region from back to front, soft detail passes, film grain and a warm light refine it, and it
resolves into the finished picture. Under the canvas a stage label with an icon sits over the prompt, beside a thin
progress line, a percent and a Replay button.

- **An image canvas** while the first result renders.
- **A chat reply that returns an image**, in place of the image until it is ready.
- **A landing or onboarding section** that explains text to image or sketch to image generation.

## When not to use

- **Text or code generation**: use a streaming reply or a shimmering label.
- **Long batch jobs with many images**: use a progress list with thumbnails.
- **Real intermediate frames from the model**: show those frames instead of a simulated painting.
- **Exact progress**: the painting is paced by `Duration`, not by your backend, so do not use it where the percent must be true.

## Usage

Give the card a `Scene` (what it draws, below) and a `Prompt`. With no `Status` it plays the whole pass on its own, holds
the picture for about two seconds, fades back to blank paper and paints again. Nothing is generated: it is a simulated
preview over the scene you give it. Without a `Scene` or an `Image` the card shows its empty state: blank paper, the
Waiting label at 0% and no prompt, and it does not loop (the Empty tab above). The alpine scene on this page is demo data
the Gallery draws in code; it is not part of the library.

```xml
<arc:SketchToImage Scene="{Binding Scene}"
                   Prompt="Layered alpine ridges at golden hour, warm haze" />
```

### Following a request

<Demo name="FollowRequest" />

Set `Status` to follow a real generation. Each stage plays to its end and holds there with a shimmering label until the
next one arrives; a later stage travels through the ones in between, and moving back (for example to `Idle` for a new
request) fades the canvas to blank paper first. Showing the card with `Done` shows the picture straight away. Map your
backend's progress like this:

| Backend | `Status` |
|---|---|
| Queued | `Idle` or `Sketching` |
| Early steps | `Sketching` |
| Middle steps | `Blocking` |
| Late steps | `Refining` |
| Image ready | `Done` |

```csharp
card.Status = SketchToImageStatus.Blocking;
card.StageChanged += (_, e) => Log($"Now {e.Stage}");
card.Completed += (_, _) => ShowResult();
card.ReplayRequested += (_, _) => Regenerate();
```

`Progress` (0 to 1) takes the place of the stage ends when you would rather pass a fraction: sketching ends at 0.38,
blocking at 0.62, refining at 0.88. The painting still travels there at its own pace.

### Anatomy

1. **Canvas**: blank paper at 3:2, rounded. Pencil strokes, the brush blocks, two soft passes of the picture, grain, a
   warm light and the sharp picture are layered on it.
2. **Progress line**: a 2px line under the canvas in the tone color.
3. **Stage label**: an icon (pen, brush, sparkles, check) and a word that shimmers while the stage is in progress.
4. **Prompt**: one muted line under the label; long prompts end in an ellipsis with the full text in a tooltip.
5. **Percent and Replay**: the percent keeps a fixed width; Replay fades to blank paper and paints again.

### Stages

| Stage | Share of `Duration` | What happens |
|---|---|---|
| `Sketching` | 38% | Pencil strokes draw in order, each in time with its length; the pen tip lifts and travels between them |
| `Blocking` | 24% | A round brush sweeps back and forth through each region, back to front; the pencil dims to a guide |
| `Refining` | 26% | Two pre-blurred passes of the picture, a film grain and a warm light fade in; the pencil fades out |
| `Done` | 12% | The sharp picture crossfades in |

### Sizes

<Demo name="Sizes" />

`Size` sets the card's maximum width (340, 520 or 720px) and the pencil's weight so lines stay crisp at every size. The
card fills its container up to that width. `Small` and cards narrower than 400px show only Replay's icon. For a small
indicator in a composer use `Size="Small"` with `ShowsReplay="False"`.

### Tone

<Demo name="Neutral" />

`Tone="Neutral"` draws the pencil, the pen tip, the stage icon and the progress line in the foreground color instead of
the accent.

### Your own scene

<Demo name="OwnScene" />

`Scene` sets what is drawn on a 600 × 400 canvas: `Lines` are SVG path strings drawn in order, `Regions` are painted back
to front, each a list of shapes with a flat `Fill` and a finished `Finish` brush and a `Brush` rectangle the round brush
sweeps through, and `Light` is where the warm light comes from. A shape with no `Fill` appears only in the finished
picture (haze, highlights). Trace the lines and regions from your picture so the sketch matches it.

Set `Image` to resolve into your real result: it fills the canvas in the soft passes and the final crossfade. Keep the
scene of your own as an illustrative placeholder, or trace one from your image.

```csharp
card.Image = new Bitmap(resultStream);
card.Status = SketchToImageStatus.Done;
```

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
card.Labels = new SketchToImageLabels
{
    Sketching = "Esquisse",
    Blocking = "Aplats de couleur",
    Refining = "Finitions",
    Done = "Terminé",
    Replay = "Rejouer",
};
```

### Reduced motion

With reduced motion the card shows the end of the current stage, the finished picture when it plays on its own. A
status change or Replay is a short fade between end states with no drawing or travel. With motion off everything changes
at once.

### Best practices

- Keep one per view.
- Show the real image after `Done`, or pass it as `Image` so the painting resolves into it.
- Write a prompt people recognize; it names what is being painted.

## API reference

<!-- api: SketchToImage -->

<!-- api: SketchToImageScene -->

<!-- api: SketchToImageRegion -->

<!-- api: SketchToImageShape -->

<!-- api: SketchToImageLabels -->

## Accessibility

- The canvas is one image whose name is the stage and the scene ("Sketching: Layered mountain ridges under a warm evening
  sky") and becomes the scene's description when done; without a scene it is the stage alone ("Waiting"). Its drawing
  layers are hidden from assistive technology.
- A polite status region says each stage when `Status` is set, and that the preview is simulated. While the card loops
  on its own it says once that it is a looping simulated preview and stays quiet.
- The progress line is a read-only progress bar from 0 to 100.
- A stage is never shown by color alone: each pairs an icon with a word.
- Every word is in `SketchToImageLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the Replay button |
| Enter, Space | On Replay, fades the canvas back to blank paper and paints again |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pencil strokes | Each draws on an in-out curve, in time with its length; the pen tip follows the head and lifts (shrinks, dims) as it travels to the next | End state | End state |
| Brush blocks | A wide round brush sweeps back and forth through each region, back to front, with a tip in the region's color; the pencil dims to a guide | End state | End state |
| Refining | Two pre-blurred passes, a static grain and a warm light fade in (only their opacity moves), then the sharp picture crossfades in | End state | End state |
| Loop | Holds the picture 2s, fades to blank paper over 520ms while the progress line drains, resets while invisible and paints again | Holds the picture | Holds the picture |
| Status change, Replay | Plays forward, or fades to paper first when moving back | 160ms fade out and in between end states | At once |
| Stage label | Old label fades out in 100ms, new one fades in over 160ms rising 2px; shimmers while in progress | 120ms crossfade, no shimmer | At once |

One clock drives a single 0 to 1 timeline. A frame redraws the ink layer from cached geometry and sets layer opacities;
it allocates nothing. The clock stops when the card is off screen and while nothing moves; the label changes only with
the stage and the percent only when its rounded value does.

## Related

- `ToolCall` for the actions an agent takes while it works.
- `AgentStatus` for the live status of a whole run.
- `SegmentedControl` to switch between a few related views.
