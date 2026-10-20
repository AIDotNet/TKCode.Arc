---
title: In-view title
summary: Bring a section title in as it scrolls into view.
---

## When to use

- Section titles along a long feature page that should share one entrance.
- Titles that should replay each time they scroll back into view, with `IsReplayEnabled`.
- Titles with explicit line breaks that must not reflow on a narrow screen.

## When not to use

- The first headline of a page that plays on load: use `TextReveal`.
- A status line that keeps working, such as "Generating summary": use `TextShimmer`.
- Body text or long paragraphs: the per-word effect is for titles only.

## Usage

The title reveals when 45% of it is visible. It uses the `Blur` variant by default.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InViewTitle Text="Blur sharpens word by word"
                     Variant="Blur" />
</UserControl>
```

### Variants

`Variant` selects the reveal style. Each style starts with the opacity, then the blur clears, and the movement
settles last, so the title is readable before it fully arrives.

- `Word` rises each word out of its own clip.
- `Line` rises each line out of its clip, 80 ms apart. It uses `Lines`.
- `Blur` sharpens each word from a blur while it lifts. This is the default.
- `Tracking` opens the letter spacing while each word clears a soft blur.
- `Wipe` uncovers the title with a soft edge from left to right.

```xml
<arc:InViewTitle Text="Words rise out of a clip" Variant="Word" />
<arc:InViewTitle Text="A soft wipe uncovers the title" Variant="Wipe" />
```

### Explicit lines

`Lines` sets the lines for the `Line` variant. The lines are shown as given and do not reflow. Each line must fit
on a narrow screen. Without `Lines`, the whole `Text` is one line.

```csharp
title.Variant = InViewTitleVariant.Line;
title.Lines = ["Lines rise one by one,", "without reflowing"];
```

### Replay on every entry

`IsReplayEnabled` hides the title when it leaves the viewport, with a quick fade and no stagger. It replays each
time the title enters again. The default is `false`, so the title plays once.

```xml
<arc:InViewTitle Text="This one replays on every entry"
                 IsReplayEnabled="True"
                 Level="H3" />
```

### Heading level

`Level` sets the heading level (`H1`, `H2` or `H3`) and the matching `AutomationProperties.HeadingLevel`. The
default is `H2`. Choose the level from the page outline.

## API reference

<!-- api: InViewTitle -->

## Accessibility

- The title exposes its full text as its name and sets `HeadingLevel` from `Level`.
- The animated parts are hidden from assistive technology. Only the text is read.
- A title that is already scrolled past when the page opens shows its final state at once.
- The control is not focusable and has no keyboard interaction.
- Use a live region on a parent element when a change must be announced.

## Motion

- The title reveals when 45% of it is visible. Opacity leads, the blur clears next and the movement settles last.
- `Word` and `Line` rise out of a clip, `Wipe` uncovers from left to right, `Tracking` opens its spacing and `Blur`
  sharpens each word.
- With `IsReplayEnabled`, leaving the viewport is a fast fade with no stagger.
- Reduced motion shows the title at once, with no movement, blur or mask. Off behaves the same way.

## Related

- `TextReveal` for the headline that plays once when the page loads.
- `TextShimmer` for a status line that shows ongoing work.
