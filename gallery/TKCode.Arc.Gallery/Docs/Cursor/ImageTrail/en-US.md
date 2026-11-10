---
title: Image trail
summary: A surface where the pointer leaves a trail of pictures that pop in along its path, rest for a moment and sink away.
---

<Demo name="Preview" />

## When to use

`ImageTrail` turns pointer movement into a trail of pictures. Every few dozen pixels of travel a picture pops in where
the pointer is, a little larger than its size and slightly tilted the way it moves, settles, rests for a moment and then
sinks and fades away. Pictures come from a list in turn, so a sweep across the surface shows the whole collection.
Content such as a headline sits underneath and stays readable when the trail is gone.

- **Portfolio and studio pages**: a body of work shown through play rather than a grid.
- **Event, travel and campaign heroes**: a quick sense of many moments behind one headline.
- **Empty areas that invite exploration**, such as the space under a "move around" prompt.

## When not to use

- **Pictures people need to look at closely or pick**: each one stays for about a second. Use a gallery or a carousel.
- **Surfaces with controls on them**: the trail draws over its content, so buttons underneath are hard to see.
- **Pages read mostly on touch screens**: the trail follows a finger only while it drags.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ImageTrail Label="Field notes" Height="360">
        <TextBlock Text="Field notes" FontSize="40" />
    </arc:ImageTrail>
</UserControl>
```

`ImageTrail` is a `ContentControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Set `Sources` to a list of `ImageSource`s (web addresses, `avares://` assets, files or bitmaps),
usually bound from code. Pictures load through `ArcImage`, so the shared image cache and a custom
`ArcImage.Loader` apply; until a picture arrives a muted placeholder of the same size takes its place. The content is
centered by default and the surface is at least 240 px tall. Give it a `Background` and `BorderBrush` to show it as a
panel; the trail is clipped to its corners.

### Size, spacing and pace

`ImageSize` is the longer side of each picture (140 px); pictures keep their own shape. `Spacing` is how far the
pointer travels between two pictures (72 px), and `Lifetime` how long each one rests before it sinks away (900 ms).
`MaxImages` (14) caps how many are on screen at once; when the pointer moves fast the oldest give way first.
`ImageCornerRadius` rounds the pictures' corners.

<Demo name="Dense" />

### From code

`DropAt(point)` drops a picture at a point, as the pointer would, and `Clear()` removes every picture at once.
`VisibleImages` says how many are on screen.

### Best practices

**Do**

- Use a set of pictures that look good together, at least six, so the trail does not repeat at once.
- Give the surface a `Label` that names the collection.
- Keep the content underneath short and high in contrast.

**Avoid**

- Very large pictures or a small `Spacing`: the trail turns into a pile.
- Relying on the trail to show information; it is decoration.

### Common questions

**Do the pictures load before the pointer arrives?** Yes. Every picture starts loading when the surface appears, so
the trail rarely shows placeholders after the first second.

**Can people without a mouse see it?** With keyboard focus the arrow keys walk a cursor from the middle of the surface
and leave the same trail (Shift takes larger steps), and Enter drops a picture.

## API reference

<!-- api: ImageTrail -->

## Accessibility

- The surface is a focusable group named by `Label` ("Image trail" by default) and described as an "image trail". The
  pictures are decoration and hidden from assistive technology; the content keeps its own names.
- Keyboard focus shows a focus ring around the surface; pointer focus shows none.
- Under reduced motion pictures only fade in and out, and with motion off there is no trail.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the surface |
| Arrow keys | Walk the trail cursor 24 px (72 px with `Shift`) |
| `Enter` / `Space` | Drop a picture at the cursor |
| `Escape` | Clear the trail |

## Motion

- A picture pops in over 220 ms from 60% of its size, past full size and back (a small overshoot), fading in as it
  grows. It is tilted at random by up to 6 degrees either way, plus up to 4 more leaning the way the pointer moves sideways.
- It rests for `Lifetime`, then over 400 ms sinks 22 px, shrinks a little and fades out.
- A fast flick drops pictures at even spacing along the path, never in a clump.
- Every picture is drawn by one control with a transform, an opacity and a rounded clip; a frame allocates nothing.
  Frames run only while pictures are on screen and pause while the surface is out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New picture | Pops in, tilted | Fades in | None |
| Old picture | Sinks and fades | Fades out | None |

## Responsive behavior

- The surface fills the width it is given and is at least 240 px tall; set `Height` for a taller hero.
- Pictures keep `ImageSize` at every width; on a phone a smaller `ImageSize`, about 96 px, keeps the trail from
  covering the content.
- On touch screens a finger leaves a trail only while it drags, so a swipe that scrolls the page stays a scroll.

## Related

- `GravityWell` and `DotGrid` are other surfaces that answer the pointer.
- `Carousel` shows pictures one at a time for a closer look.

| Component | Choose it when |
|---|---|
| `ImageTrail` | Many pictures should flash past as people explore |
| `GravityWell` | Many small pictures should stay in a grid and react |
| `Carousel` | Each picture deserves a closer look |
