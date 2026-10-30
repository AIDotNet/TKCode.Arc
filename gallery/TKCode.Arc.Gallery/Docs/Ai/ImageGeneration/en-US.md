---
title: Image generation
summary: An image generation card where four images resolve as a pixel mosaic, from coarse colour blocks to full detail, each with its own progress ring; pick one to upscale, vary or download.
---

<Demo name="Preview" />

## When to use

`ImageGeneration` is a text to image card. The person writes a prompt, picks a model and a shape, and four images
resolve side by side as pixel mosaics: coarse colour blocks first, then finer and finer blocks under a soft light band,
then the full image, each with its own progress ring. They pick one and continue with it: Upscale grows it to fill the
grid and sweeps a band of crisp pixels across it, Vary makes four new images from it and Download hands it to your app.

- **A text to image feature inside a product**: marketing assets, listing photos, illustrations for slides or documents.
- **Any flow where people compare a few candidates** and continue with one (upscale, vary, download).
- **Prototyping an image model integration** before the backend exists, with a simulation.

## When not to use

- **A full canvas editor** with layers, masks and inpainting: build on a drawing surface instead.
- **One image with a loading state** and no generation controls: show the image with its own placeholder.
- **A gallery of past results**: the card holds only the current batch; pair it with your own asset list.

## Usage

With no generator the card plays its `Simulation`, and with neither Generate stays unavailable. An
`ImageGenerationSimulation` holds the `Prompt` the card starts with, the `Models` and `Suggestions` on offer and a
`Painter` (an `ImageGenerationPainter`) that paints a picture for a prompt; the card reports progress frame by frame over
two to three seconds, resolves each tile from the painted preview, and generates once when it first scrolls into view.
Nothing is downloaded or sent anywhere. Your own `Prompt`, `Models` and `Suggestions` win over the simulation's, and
setting `Simulation` to null stops its run.

```csharp
card.Simulation = new ImageGenerationSimulation
{
    Prompt = "A quiet mountain lake at golden hour",
    Models = [new ImageGenerationModel { Id = "your-model", Name = "Your image model", Mark = ArcIcons.Sparkles }],
    Suggestions = ["A neon city street after rain at night"],
    // Opaque BGRA bytes, width × height × 4; the same prompt and seed should paint the same picture.
    Painter = (prompt, seed, width, height) => MyPainter.Paint(prompt, seed, width, height),
};
```

The demos on this page paint a lake at golden hour, a city street after rain, a cup on a sunlit sill or a pastel room
from `ImageGenerationDemoData`. The model catalog the gallery's other AI demos use lists no image generation models, so
these image models keep invented names.

Connect it to your image service with `Generator`. It is called once per tile, in parallel, for Generate, Vary and Retry:

```csharp
card.Generator = async (request, context) =>
{
    var job = await images.CreateAsync(request.Prompt, request.Model.Id, request.Aspect.Width, request.Aspect.Height,
        seedFrom: request.VariationOf?.Id, context.CancellationToken);
    await foreach (var step in job.ProgressAsync(context.CancellationToken))
    {
        context.ReportProgress(step.Fraction, step.Preview);
    }
    return new ImageGenerationImage
    {
        Id = job.Id,
        Source = await job.DownloadBitmapAsync(context.CancellationToken),
        Description = request.Prompt,
        Width = request.Aspect.Width,
        Height = request.Aspect.Height,
    };
};
```

Report progress from 0 to 1. A preview bitmap lets the mosaic resolve from your image; without one a quiet placeholder
grid shimmers until the image arrives and fades in, and without any reports the ring estimates. Throw to mark a tile
failed; Stop cancels `context.CancellationToken`. `Upscaler` works the same way for the picked image.

### Anatomy

1. **Prompt**: a text field; Cmd/Ctrl + Enter generates.
2. **Chips**: the models (the chosen one shows its name, the others collapse to their 16px mark with a tooltip), the
   shapes (each with a small outline) and Generate, which reads Stop while a batch runs.
3. **Grid**: four tiles in the batch's shape, never taller than 420px. Each running tile has a glass pill with a ring
   and its percentage; a picked tile has an accent ring and a check badge.
4. **Footer**: a status line, then Upscale, Vary and Download.

### Picking, upscaling, varying and downloading

Press a finished image (or focus it and press Enter or Space) to pick it; press it again to clear the pick. Upscale
grows the picked image from its cell to the whole grid and sweeps a band of crisp pixels from left to right: soft ahead
of it, sharp behind it. Escape or the button in its corner returns to the grid. Vary makes four new images with the
picked one as `VariationOf`. Download raises `DownloadRequested` with the image and a file name built from the prompt,
the image's number and its size; save it from there.

A failed tile shows an alert and "Failed", a stopped one a square and "Stopped"; pressing either tries that image again.

### Portrait shapes

<Demo name="Portrait" />

Each batch keeps its own shape: picking another aspect chip applies to the next generation. The grid springs to the new
shape and caps its height at 420px, so portrait batches stay compact.

### Your own generator

<Demo name="YourGenerator" />

Here the card's simulation stands in for an image service (`Card.Simulation.Generate`), and image 3 fails on its first try. `AutoGenerate` is off by default
when you set a generator; set it to `True` to generate once when the card scrolls into view.

### Listening to the card

<Demo name="Events" />

`SelectionChanged` reports the picked image (and the upscaled one once an upscale finishes), `SelectedImage` holds it,
and `DownloadRequested` carries the image and a file name.

```csharp
card.DownloadRequested += async (_, e) =>
{
    var file = await TopLevel.GetTopLevel(card)!.StorageProvider.SaveFilePickerAsync(new() { SuggestedFileName = e.FileName });
    if (file is not null && e.Image.Source is { } bitmap)
    {
        await using var stream = await file.OpenWriteAsync();
        bitmap.Save(stream);
    }
};
```

### Narrow containers

<Demo name="Narrow" />

The card fills its container up to 640px. Below 420px the aspect chips move to a second row; below 520px the footer
stacks, the status on top and three equal buttons below.

### Your own words and models

`Models` and `AspectRatios` replace the chips (`ImageGenerationModel` has an id, a name and an optional mark;
`ImageGenerationAspect` an id, a label and the pixel size a generation returns). `Suggestions` replaces the prompts the
empty grid offers. `Labels` holds every word the card shows or announces:

```csharp
card.Labels = new ImageGenerationLabels
{
    Generate = "Créer",
    Stop = "Arrêter",
    Generating = (count, model) => $"Création de {count} images avec {model}",
};
```

### Reduced motion

With reduced motion the ring, the stepped mosaic (it follows the progress, not time) and the final states stay; the
light band, the upscale sweep, the hover zoom and the expand travel go. With motion off everything changes at once.

### Content guidelines

- Keep model names to what your service actually runs; the demos' names are invented.
- Return the real width and height so the footer and the file name show the true size.
- Describe each image in `Description` (usually the prompt): screen readers read it on the tile.

### Best practices

- Pass `context.CancellationToken` to every call so Stop really stops the work.
- Report a preview as early as your service can: the mosaic then shows real colours from the first step.
- A service that returns four images per call can be wrapped by caching one request and resolving each index from it.

### Common questions

**Can the person change the prompt while a batch runs?** Yes; the change applies to the next Generate.

**Does the card keep earlier batches?** No. It holds the current batch; listen to `SelectionChanged` to keep results.

## API reference

<!-- api: ImageGeneration -->

<!-- api: ImageGenerationModel -->

<!-- api: ImageGenerationAspect -->

<!-- api: ImageGenerationImage -->

<!-- api: ImageGenerationRequest -->

<!-- api: ImageGenerationContext -->

<!-- api: ImageGenerationLabels -->

<!-- api: ImageGenerationSelectionChangedEventArgs -->

<!-- api: ImageGenerationDownloadEventArgs -->

<!-- api: ImageGenerationSimulation -->

<!-- api: ImageGenerationPainter -->

## Accessibility

- Model and aspect chips are radio groups with one tab stop each; a collapsed model chip keeps its name as its accessible
  name and in a tooltip.
- Each tile is a toggle button that reports whether it is picked; its name reads the image's number and description, or
  that it is generating, failed or stopped.
- A polite live region reports starts, finishes, failures, upscales and downloads, and says when results are simulated.
- Status is never colour alone: a failure pairs a danger icon with "Failed", a stop a square with "Stopped", a pick an
  accent ring with a check badge.
- While an image is expanded the other tiles take no pointer or focus.

### Keyboard

| Keys | Action |
|---|---|
| Cmd/Ctrl + Enter | Generates from the prompt field |
| Arrow keys | Move between model chips or aspect chips (and choose), and between images in the grid |
| Enter, Space | Pick or clear the focused image; retry a failed or stopped one |
| Escape | Close the expanded image and return to the grid |
| Tab | Prompt, model, aspect, Generate, the picked image, then Upscale, Vary and Download |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| An image resolves | 4 column blocks fade in from the centre in a seeded order, then 8, 16, 32 and 64 columns land on them, then the image crossfades in; one progress value drives the mosaic, ring and percentage | The same steps, driven by progress | The same steps |
| Running tile | A soft light band sweeps diagonally over the blocks on the resolving grid, fading as detail arrives, 1.6s a pass, paused off screen | None | None |
| Done tile | Picture zooms 2% on hover; the tile presses to 98.5% | None | None |
| Upscale | The tile grows from its cell to the grid on a bounce-free 240ms spring, the others fade; a band of crisp pixels sweeps left to right | At once, no sweep | At once |
| New batch shape | The grid springs to the new shape; a container resize cuts straight to the new size | At once | At once |
| Button labels | Stacked in one cell and crossfaded (100ms out, 160ms in), so no button changes width | Crossfade | At once |
| Footer status | Old line fades out over 100ms, the new one fades in over 160ms and rises 2px | Crossfade | At once |
| Model chip | The chosen chip springs its name open by its measured width | At once | At once |

## Related

- `ContextWindow` for how much of a model's window a session uses.
- `ToolCall` for the tool calls an agent makes.
- `AgentStatus` for the live status of a running agent.
