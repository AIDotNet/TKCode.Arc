---
title: Image reveal
summary: An image generation canvas where a soft, blurred preview made from the image's own colors sharpens top to bottom behind a soft-edged band, with fine film grain, a translucent progress pill and calm queued and error states.
---

<Demo name="Preview" />

## When to use

`ImageReveal` shows an image model at work the way current image tools do. A soft, heavily blurred preview made from the
picture's own colors fades in with fine film grain, then a soft-edged band travels top to bottom: above it the picture is
sharp, inside it a near-sharp copy blends in, below it the preview stays soft while it slowly comes into focus. A
translucent pill shows a progress ring and the step count. Queued jobs get a calm surface with one broad shimmer, and a
failed job an alert, its message and Retry.

- **An image canvas** or an inline image in a chat reply while the model renders it.
- **A grid of variations** that resolve one after another.
- **A queued job** that should feel alive before it starts.
- **An empty state** while a first image renders.

## When not to use

- **Plain image loading** from a server or disk: use a blur-up placeholder or a skeleton.
- **Text generation or tool calls**: use a text shimmer, `ToolCall` or `AgentStatus`.
- **Determinate file uploads**: use a progress bar.

## Usage

Pass the job's `Status` and `Progress` (step over total) and the image in `Source` as soon as a preview or the final
image exists: the soft preview is sampled from it, and until it is there the canvas keeps the calm queued look. Coarse
progress updates, one per model step, are smoothed into one continuous reveal.

```xml
<arc:ImageReveal Source="{Binding Image}"
                 AltText="A calm alpine lake at golden hour"
                 Status="{Binding Status}"
                 Progress="{Binding Progress}"
                 Steps="30"
                 AspectRatio="0.8" />
```

With no status and no generator, an image in `Source` is simply shown, finished. With nothing at all the canvas rests on
its plain surface: no pill, no shimmer, and it is named "No image" (the Empty tab above). For demos, a simulation plays a
looping generation over pictures you give it (below).

### Anatomy

1. **Canvas**: fills its container up to 560px wide and keeps `AspectRatio` (width over height); the image is
   cover-cropped to it. Corners follow `Size`.
2. **Soft preview**: copies of the image 6 to 48 pixels wide, smoothly upscaled and blurred, crossfaded along that chain
   by progress, with fine monochrome grain and a very slow drift.
3. **Band**: a soft-edged strip about a quarter of the canvas high that sharpens the picture as it travels down.
4. **Pill**: at the top left, a spinner, ring or check with a short label and the step count ("18 / 30", tabular
   numerals in a reserved width). It fades out shortly after the image resolves.
5. **Error state**: an alert, the message and a Retry button in the middle of the canvas.

### States

| `ActualStatus` | Canvas | Pill |
|---|---|---|
| `Queued` | The surface tone with one broad, soft shimmer | A spinner and `QueueLabel` ("Queued, 2 ahead") |
| `Generating` | The soft preview, sharpening top to bottom behind the band | A progress ring, "Generating" and "18 / 30" |
| `Done` | The sharp image; one soft sheen sweeps across | A check and "Done", then the pill fades out |
| `Error` | The sharp layers blur back out over a dimmed preview | Hidden; the alert, `ErrorMessage` and Retry show |

Every change starts from the current pose: restarting, failing or swapping the picture blurs the sharp layers back out
before anything resets underneath, and a new picture crossfades in from a snapshot of the old frame.

### A generator

<Demo name="Generator" />

`Generator` takes an async delegate that runs one job: report `ImageRevealProgress` values (the progress and, as soon as
you have it, the image) and return the final image. Throwing shows the error state, and Retry runs the generator again.
`Restart()` (or a new `RunKey`) cancels the running job and starts another.

```csharp
reveal.Generator = async (progress, cancellationToken) =>
{
    var job = await images.StartAsync(prompt, cancellationToken);
    await foreach (var update in job.UpdatesAsync(cancellationToken))
    {
        progress.Report(new ImageRevealProgress(update.Step / 30.0, update.Preview));
    }
    return job.Image;
};
```

### No progress events

<Demo name="Estimated" />

When the service reports no progress, set only `Status` and an estimated `Duration`. The pill eases towards the end and
waits there; the band sweeps the image in once you set `Source` and `Done`.

### A simulation

For a demo or a prototype, leave `Status`, `Generator` and `Source` unset and give the canvas an `ImageRevealSimulation`
with your own pictures (each an `ImageRevealSimulationPicture`: the image and its alt text). Each run waits in the queue
for `QueueDuration` (900ms), generates over `Duration` (5.2s) and reveals the next picture; with `IsLooping` it holds the
finished image for `HoldDuration` (2.6s), blurs back out and runs again. `Seed` picks the first picture, and the
announcements add "simulated". Nothing is generated or downloaded. Setting `Simulation` to null stops it and empties the
canvas.

```csharp
reveal.Simulation = new ImageRevealSimulation
{
    Pictures =
    [
        new ImageRevealSimulationPicture { Image = lakeBitmap, AltText = "A calm alpine lake at golden hour" },
        new ImageRevealSimulationPicture { Image = dunesBitmap, AltText = "Desert dunes at dusk" },
    ],
};
```

Give each control its own simulation: it keeps its place in the pictures. The preview above reveals three scenes the
Gallery paints in code (an alpine lake, desert dunes, a lighthouse); they are demo data, not part of the library.

### A grid of variations

<Demo name="Grid" />

Use `Size="Small"` in grids: the pill shows only the ring and the count. Give each tile its own `Seed` so the grain and
the drift differ (here each tile also plays its own simulation over the same pictures), stagger the runs with
`StartDelay`, turn off `IsLooping`, and set `MaxWidth="Infinity"` so the tiles fill their cells. Changing `RunKey` (or
calling `Restart()`) blurs every tile back out and runs again.

### Your own words

`Labels` holds every word the control shows or says; `QueueLabel` and `ErrorMessage` change the chip and the message of
one job, and `AltText` names the finished image.

```csharp
reveal.Labels = new ImageRevealLabels
{
    Generating = "Génération",
    Done = "Terminé",
    Count = (step, total) => $"{step} sur {total}",
};
```

`RingBrush` colors the progress ring (the accent by default), `ShowsProgress` hides the pill and `ShowsSheen` the
completion sheen.

### Content guidelines

- `AltText` describes the picture ("A calm alpine lake at golden hour"), not the job.
- Keep `QueueLabel` short: "Queued", "Queued, 2 ahead".
- `ErrorMessage` says what happened and what to do, in one sentence.

### Best practices

- Pass the image as soon as any preview exists; the reveal is sampled from it.
- Report every model step: the 200ms follow turns them into one smooth reveal.
- In grids, use Small tiles, different seeds and a stagger of about 400ms.

### Common questions

**Does it read the image's pixels?** No. The copies are drawn, never read back, so any `IImage` works.

**Why does it stay in the queued look while generating?** There is no image yet; pass `Source` (or report one from the
generator) and the preview fades in.

## API reference

<!-- api: ImageReveal -->

<!-- api: ImageRevealStatus -->

<!-- api: ImageRevealProgress -->

<!-- api: ImageRevealStatusChangedEventArgs -->

<!-- api: ImageRevealSimulation -->

<!-- api: ImageRevealSimulationPicture -->

<!-- api: ImageRevealLabels -->

## Accessibility

- The canvas reports itself as an image; its name says the state while it works ("Generating image") and becomes
  `AltText` once done. An empty canvas is named "No image" and announces nothing.
- A polite live region announces Queued, Generating image, Image ready and the error message, not every step. The
  simulation appends "simulated".
- While generating, the pill is a read-only progress bar whose value is the step, named "Step 18 of 30".
- Status is never color alone: the pill pairs a spinner, ring or check with a short label, and the error state pairs an
  alert icon with its message.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the Retry button in the error state. The canvas itself is not focusable. |
| Enter, Space | Activates Retry. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Queued | One broad, soft shimmer, off canvas to off canvas every 2.6s | The plain surface tone | The plain surface tone |
| Preview appears | Fades in over 420ms from the 6px copy, crossfading along the blur chain by progress | The same fade, no drift | At once |
| Generating | A soft-edged band travels top to bottom; grain refreshes 24 times a second; a very slow drift settles as it resolves | No band: the near-sharp copy and then the image crossfade in by progress; still grain | The same crossfade, following progress at once |
| Progress updates | A 200ms exponential follow; the simulation eases out so the composition resolves early | The same | At once |
| Resolves | One soft sheen sweeps across (1.1s), then the pill fades out (240ms) | No sheen | At once |
| Restart, failure, new picture | The sharp layers blur back out (500ms) before the reveal resets; a new picture crossfades from a snapshot (450ms) | The same | At once |
| Pill label | Old text out over 100ms, new text in over 160ms after a 60ms gap; the pill's width springs with no bounce | 120ms crossfade, width at once | At once |

The frame loop pauses off screen and when the window is hidden, and draws nothing once the image has resolved.

## Related

- `AgentStatus` for the live status of an agent run.
- `ToolCall` for the tool calls an agent makes.
- `ReasoningBlock` for the thinking part of an AI reply.
