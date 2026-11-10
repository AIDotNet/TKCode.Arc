---
title: Logo marquee
summary: A quiet, continuously moving row of marks with a pause control.
---

<Demo name="Preview" />

## When to use

Use `LogoMarquee` for a customer or integration strip on a landing page, or a social proof section with a heading and a short supporting line.

## When not to use

- **Logos that need a description or a connect action**: use the `Integrations` block.
- **Items people should browse by hand**: use `Carousel`.
- **Marks you have no permission to show**: the default marks are invented; replace them only with marks you may represent.

## Usage

```xml
<arc:LogoMarquee Title="Trusted by product teams"
                 Description="Teams of every size ship with us." />
```

### Tone

`Tone` is `Color` (each mark in its colors, names at full contrast) or `Mono` (every mark and name in the secondary text color).

<Demo name="Mono" />

### Your own marks

`Brands` takes `LogoMarqueeBrand` items. `Icon` is a filled `Geometry` in any coordinate space, scaled to fit. `Color` tints it in `Color` tone. `ColorIcon` is a full color `IImage` that replaces `Icon` in `Color` tone.

<Demo name="CustomMarks" />

```csharp
marquee.Brands =
[
    new LogoMarqueeBrand { Name = "Acme", Icon = Geometry.Parse("M12 2 22 22H2z"), Color = Color.Parse("#3C9D6B") },
    new LogoMarqueeBrand { Name = "Globex", ColorIcon = myDrawingImage, Icon = fallbackShape }
];
```

### Pausing

The control under the row flips `IsPaused`, which you can also bind. Every label is a property with an English default.

## API reference

<!-- api: LogoMarquee -->

## Accessibility

- The row is exposed once, named by `RegionLabel`, with the names as its help text; the copies are not exposed.
- The pause control has an action name (`PauseActionName`, `PlayActionName`), and a live region says `PausedAnnouncement` when the row pauses.
- Below full motion the row does not move at all.

## Motion

- The row travels one list width in 39 s, linearly, and loops without a seam. It holds still while a mouse or pen rests on it.
- Frames move one transform on a `LiveLoop`, which pauses out of view; nothing redraws or allocates.
- Reduced motion and Off stop the row, draw each mark once in centered wrapped rows and hide the pause control.

## Responsive behavior

- The block follows its own width. Below 640 px (`:narrow`) the heading and text stack, the marks shrink from 22 to 20 px and the gaps tighten from 56 to 40 px.
- The row fades over 7% at each side, so marks never cut off hard at a narrow width.

## Related

- `Carousel` for slides people move by hand.
- The `Integrations` block for logos with a description and an action.
