---
title: Design to code
summary: A screenshot-to-code card: a scan line finds the layout regions of a dropped screenshot or design file frame, then code streams in beside it, linked region by region, with a live preview one tab away.
---

<Demo name="Preview" />

## When to use

`DesignToCode` is the card of a screenshot-to-code feature. The person drops a UI screenshot on the design side or
imports a frame from a design file. A scan line crosses the screenshot and each layout region it finds appears as a box
with a tag. Then the code streams in beside it, region by region: the region being written is highlighted on the
screenshot and its lines carry a quiet tint. Pointing at a box or a tag lights up its lines, and pointing at a line
lights up its box. One tab away, the Preview shows the result working.

- **Design to code features**: upload a screenshot or pick a design file frame and get a component back.
- **Showing an AI vision step**: when what the model detected matters, such as layout parsing before generation.
- **Marketing and onboarding**: explain what a code generation product does at a glance. Give the card a
  `Simulation` and it plays a design and its code on a loop while it is on screen.

## When not to use

- **Plain file uploads with no generation step**: use a file upload.
- **Finished code without the source design**: use `CodeBlock`.
- **A full editor with files, tabs and a terminal**: build a workspace layout instead of stretching this card.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DesignToCode />
</UserControl>
```

`DesignToCode` is a `TemplatedControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. The card fills its container's width; keep it between 360 and 760px wide, in a page or a dialog.

With no `Generator` and no `Simulation` the card opens on its drop zone: drop a screenshot, choose a file or import a
design file. Wire a `Generator` to turn designs into code, or give the card a `Simulation` to play.

### Simulation

`DesignToCodeSimulation` is a design and the code a model could write for it: the `Source` (an image, or a sketch drawn
in code), the `Regions` the scan finds, the code as `Parts`, each `DesignToCodeSimulationPart` tagged with the region it
implements, and optionally the `Model` credited, the `FileName`, the `FrameName` a simulated import brings in, a
`PreviewFactory` and a `PreviewAction` the loop calls on the preview. Without a `Generator` the card shows its design
while `Source` is unset, and Use sample brings it back. The run is simulated: nothing is uploaded or sent anywhere, and
the announcements say so. While the card is on screen it plays on a loop (generate, hold on the code, show the preview,
start again) until the first pointer or key press inside the card. Setting `Simulation` to null stops a simulated run
and returns to the drop zone.

```csharp
card.Simulation = new DesignToCodeSimulation
{
    Source = new DesignToCodeSource("pricing-card.png", screenshot),
    Regions = [new DesignToCodeRegion("price", "Price", 7.5, 42.5, 52, 14.5)],
    Parts =
    [
        new DesignToCodeSimulationPart("export function PricingCard() {\n  return (\n"),
        new DesignToCodeSimulationPart("    <p className=\"text-4xl\">$24</p>\n", "price"),
        new DesignToCodeSimulationPart("  );\n}\n"),
    ],
    Model = new AgentModel("Your model"),
    FileName = "PricingCard.tsx",
};
```

A design can also be drawn in code instead of shipped as an image: give the source a `Sketch` (a `DesignToCodeSketch`
that draws into the design's rectangle with the card's `DesignToCodeSketchPalette`, so it follows light and dark) and
its `SketchSize`. The demos on this page draw a small pricing card that way and take everything from
`DesignToCodeDemoData`; the model credited is a real one (checked on 2026-10-29), the plan and prices are invented.

### Anatomy

1. **Design pane**: a header with the design's name, a "Sample" or "Design file" tag and Remove; under it the
   screenshot with its region boxes and tags, or the drop zone when there is no design.
2. **Region boxes and tags**: a 1px accent outline per region and a small tag level with its top, in the margin to its
   left (inside the box when there is no room). A tag is a button named "Price: show its code".
3. **Output pane**: a header with the file name, the model's mark and name, the Code and Preview toggle and Copy; under
   it the code with line numbers, or the live preview.
4. **Footer**: the status (an icon, a short label, the file and meta text such as "52 lines in 3.4s"), Import design
   file and the main action, which reads Generate code, Stop, Regenerate or Try again.

### The run

`Phase` follows the run: `Empty` without a design, `Ready` with one, then `Analyzing`, `Writing` and `Done`, or
`Stopped` and `Error`. Generate code (or `Generate()`) starts a run and returns the output to Code. Stop, Escape or
`Stop()` ends a run that is analyzing or writing; the code written so far is kept. `Code` holds the code on screen and
`Completed` reports the finished code with its line count and time.

| Phase | Status | Main action |
|---|---|---|
| Empty | Add a design to start | Generate code (off) |
| Ready | Ready pricing-card.png | Generate code |
| Analyzing | Analyzing layout pricing-card.png · 3 regions | Stop |
| Writing | Writing PricingCard.tsx · 5 regions | Stop |
| Done | Done PricingCard.tsx · 52 lines in 3.4s | Regenerate |
| Stopped | Stopped PricingCard.tsx · Partial code kept | Regenerate |
| Error | Generation failed: the model timed out. Try again. | Try again |

### Your model

Give the card a `Generator`: an async delegate that receives the design and a `DesignToCodeContext`. Call `AddRegion`
for each layout region as it is detected (its box in percent of the design), then `Write` code chunks as they stream,
passing the id of the region a chunk implements so its lines link to the box. Complete the task when done, throw to show
the error, and stop when the context's token is cancelled. Chunks can be any size: the card paces them into whole words.

<Demo name="YourModel" />

```csharp
card.Model = new AgentModel("Your model");
card.Generator = async (source, context) =>
{
    await foreach (var e in backend.StreamAsync(source.File, context.CancellationToken))
    {
        if (e.Region is { } region)
        {
            context.AddRegion(new DesignToCodeRegion(region.Id, region.Label, region.X, region.Y, region.Width, region.Height));
        }
        else
        {
            context.Write(e.Text, e.RegionId);
        }
    }
};
card.Completed += (_, e) => drafts.Save(e.Code);
```

`AddRegion` and `Write` can be called from any thread. With a generator the card opens on the drop zone and does not
loop; set `Source` to start from a design. `Model` credits the model in the code header with its mark (a 24 × 24 stroked
glyph); without one the card shows only the sparkle (or the simulation's model while it plays).

### Dropping a screenshot

Drop works anywhere on the design side and shows a "Drop to use this design" cover while a file is over it. Every drop
path also has a button: Choose file, Use sample and Import design file. `AcceptedFileTypes` (as in an input's accept
attribute: MIME types, `image/*` or extensions) and `MaxFileSize` (10 MB) decide what is accepted; any other file shows
an error that names the problem and the fix, and the design shown stays.

<Demo name="DropZone" />

Choose file raises `ChooseFileRequested`. Open your picker there, pass the file to `LoadFileAsync` and set `Handled`;
when nobody handles it the card uses the simulation's design, if any, and says that nothing was opened. A dropped or loaded file becomes a
`DesignToCodeSource` with its name, the decoded image and the `IStorageFile`, which your generator can send to your
backend. The card releases images it decoded itself when they are replaced.

```csharp
card.ChooseFileRequested += async (_, e) =>
{
    e.Handled = true;
    var files = await TopLevel.GetTopLevel(card)!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions());
    if (files.Count > 0)
    {
        await card.LoadFileAsync(files[0]);
    }
};
```

### Design files

Import design file runs `DesignFileImporter`, your picker or link flow: return the frame as a `DesignToCodeSource`
with its rendered image, or null when the person cancels. Without one the card imports the simulation's design under
its `FrameName`, if there is a simulation, and says that nothing was opened. A source without an image draws its
`Sketch`.

### The live preview

The Preview tab shows the finished code working. `PreviewFactory` renders it, for example in a sandboxed web view;
never run generated code inside your app. Without a factory a simulated run uses the simulation's `PreviewFactory`
(the demos render the pricing card, where Monthly and Yearly switch the price), and anything else shows a short note. Code and Preview stay mounted, so switching keeps each scroll position, and every new run returns to
Code so the stream is visible.

### Narrow containers

The layout follows the card's own width. From 640px the panes sit side by side (the design 344px, the code the rest);
below that they stack, each 352px tall, so the card never changes size while code streams. Under 520px the status drops
the file name and Import design file shows only its icon, keeping its name for assistive tech. Under 440px the meta text
and the model name hide; the mark stays. Long code lines wrap with a hanging indent, so nothing scrolls sideways.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion there is no scan line and no chunk fade: the regions appear at once, the code arrives a part
(a region) at a time and a simulation plays once instead of looping.

<Demo name="ReducedMotion" />

### Content guidelines

Region labels are tags on a small screenshot: one or two words that name the part, not the component.

| Instead of | Write |
|---|---|
| div.pricing-header-container | Header |
| Segmented control (2 options) | Billing toggle |
| Generation completed successfully | Done |

`Labels` takes a `DesignToCodeLabels` object with every word the card shows or announces. Sentences with values are
functions, so the word order can change in other languages:

```csharp
card.Labels = new DesignToCodeLabels
{
    Generate = "Write the code",
    DoneMeta = (lines, time) => $"{lines} lines, {time.TotalSeconds:0.0}s",
};
```

### Best practices

**Do**

- Send the regions first, then code tagged with the region it implements, so the boxes and the lines link up.
- Render the preview in a sandbox, and keep the card one per page or dialog.
- Stop your request when the context's token is cancelled.

**Avoid**

- Presenting a simulation as a real run. It is made up and says so.
- Running generated code inside your app to preview it.
- Sending regions without ids: a chunk links to its box through the id.

### Common questions

**Why does the card open on a design?** Without `Source` and without a `Generator` it shows its simulation's design.
Set `Source` to null to open on the drop zone.

**Why do my lines not link to a box?** A line belongs to the region of its first character; pass the region's id with
every chunk that implements it.

**What happens to code that arrived after Stop?** It is dropped. Only the code on screen is kept, and later writes from
the stopped run are ignored.

## API reference

<!-- api: DesignToCode -->

<!-- api: DesignToCodeSource -->

<!-- api: DesignToCodeRegion -->

<!-- api: DesignToCodeContext -->

<!-- api: DesignToCodeCompletedEventArgs -->

<!-- api: DesignToCodeSimulation -->

<!-- api: DesignToCodeSimulationPart -->

<!-- api: DesignToCodeSketchPalette -->

<!-- api: DesignToCodeLabels -->

The enums:

| Type | Values |
|---|---|
| `DesignToCodePhase` | `Empty`, `Ready`, `Analyzing`, `Writing`, `Done`, `Stopped`, `Error` |
| `DesignToCodeView` | `Code`, `Preview` |
| `DesignToCodeSourceKind` | `Sample`, `File`, `DesignFile` |

## Accessibility

- The card is named "Design to code" and holds two named groups, Design and Output. A polite live region announces each
  step: analyzing, the regions found, done with the line count and the time, stopped, and errors. The simulation says
  that nothing was uploaded or opened.
- Status is never color alone: each state pairs an icon with a short label, and errors name the problem and the fix.
- Region tags are real buttons named like "Price: show its code". The code area is a focusable, named region whose
  status reads "Writing" while code streams; it is not a live region, so screen readers are not flooded with tokens.
- The hidden layer of the Code and Preview pair is invisible and out of the tab order. A sketched design is
  decorative; a dropped image is named after its file.
- Every drop path has a button: Choose file, Use sample and Import design file.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Moves through Remove, the region tags, the view toggle, Copy, Import design file and the main action |
| `Enter` / `Space` | Activates the focused control; on the drop zone, Choose file asks for a file |
| `Left` / `Right` | Switch between Code and Preview on the view toggle |
| `Escape` | Stops a run that is analyzing or writing; the partial code is kept |
| Focus a region tag | Highlights that region's box and its lines, scrolling them into view |

## Motion

- Analyzing: a 1px scan line crosses the screenshot in one second, and each region's box and tag fade in as the line
  reaches its top (160ms opacity, no scale).
- Writing: code streams in batches of whole words about every 34ms (3 to 8 words at reading pace); each batch only
  fades in over 140ms. The region being written is highlighted on the screenshot and its lines carry a quiet tint.
- The status, the placeholders and the Code and Preview layers crossfade: the old one out in 100ms, the new one in over
  160ms. The main button's width follows its label without bounce.
- In the preview the billing thumb glides on a 0.24s spring with no bounce, and the price swaps with a 2px crossfade on
  tabular numerals.
- The simulation's loop waits while the card is off screen.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Scan line | Crosses in 1s | None | None |
| Regions | Fade in as the line reaches them | At once | At once |
| Code | Word batches, each fading in | A part at a time | A part at a time |
| Code and Preview | Crossfade | Short crossfade | At once |
| Simulation loop | Loops | Plays once | Plays once |

## Related

- `CodeBlock` for finished code with copy.
- `ToolCall` for one step of an agent's run.
- `AgentStatus` for a compact live status of a running agent.

| Component | Choose it when |
|---|---|
| `CodeBlock` | The code is finished and the design is not part of the story |
| `ToolCall` | The generation is one step in an agent transcript |
| `AgentStatus` | Only the progress of a run matters |
