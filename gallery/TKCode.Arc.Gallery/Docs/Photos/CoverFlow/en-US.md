---
title: Cover flow
summary: A row of album or book covers in perspective. The centred cover faces you, the others turn toward it, and a flick coasts and snaps to a cover.
---

<Demo name="Preview" />

## When to use

`CoverFlow` shows a collection as the covers people know it by. The centred cover faces forward with its title and
subtitle underneath; the covers on either side turn toward it and stack into the distance, standing on a glossy floor
that reflects them. Dragging the row moves it with the hand; letting go makes it coast with the flick's momentum and
then snap a cover to the centre, so a long shelf can be crossed in one throw. Clicking a side cover brings it to the
centre, and clicking the centred one opens it.

- **Music and audiobooks**: albums, playlists and series, picked by their art.
- **Books and magazines**: a reading list or a shelf of issues.
- **Featured items**: a short set of games, films or templates on a landing page.

## When not to use

- **Finding something by name**: a searchable list is faster than flicking through covers.
- **Many small items**: only a handful of covers are visible at once; use a grid.
- **Pictures that are not covers**: photos of mixed shapes look cramped in perspective; use `PhotoGrid` or
  `LightboxGallery`.

## Usage

```xml
<arc:CoverFlow Label="Albums" SelectedIndex="1">
    <arc:CoverFlowItem Source="https://example.com/covers/lanterns.jpg" Title="Paper Lanterns" Subtitle="Mira Vale" />
    <arc:CoverFlowItem Source="https://example.com/covers/copper.jpg" Title="Copper Hours" Subtitle="The Lindens" />
    <arc:CoverFlowItem Source="https://example.com/covers/north.jpg" Title="North of Nowhere" Subtitle="Oskar Fen" />
</arc:CoverFlow>
```

`CoverFlow` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Write the covers
inline as `CoverFlowItem`s, or bind `ItemsSource` to a list of them, of `ImageSource`s or of addresses. `Title` and
`Subtitle` are shown under the centred cover, and `Tag` carries any object of yours. `SelectedIndex` is the centred
cover and can be bound both ways; `SelectedItem` is that cover.

### Covers of another shape

`CoverWidth` sets the width of a cover and `CoverAspectRatio` its shape (square by default, about 0.67 for books).
`TiltAngle` sets how far the side covers turn, and `ShowsReflection="False"` takes away the floor.

<Demo name="Books" />

### Opening a cover

`ItemInvoked` is raised when the centred cover is clicked or tapped, or Enter or Space is pressed. Its arguments carry
the `Index` and the `Item`.

<Demo name="Invoked" />

### Best practices

**Do**

- Give every cover a `Title`: it is shown under the centre and read by screen readers.
- Start on a cover in the middle when the shelf has no natural first item, so both sides show.
- Keep the shelf to a few dozen covers; offer a search or a grid for more.

**Avoid**

- Covers that are mostly text at a small size: the side covers are turned and shaded.
- Placing it where a sideways drag must scroll something else.

## API reference

<!-- api: CoverFlow -->

## Accessibility

- The row is one tab stop, a list named by `Label`. Its help text, and a polite status line, say which cover is at the
  centre: "Night Ferry, Ilse Marrow, 10 of 12". `ItemNameFormat` changes the position part.
- Arrow keys move one cover, Page Up and Page Down five, Home and End to the ends; Enter or Space open the centred
  cover.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring around the row.

### Keyboard interactions

| Key | Action |
|---|---|
| Left / Right | Brings the previous or next cover to the centre. |
| Page Up / Page Down | Moves five covers. |
| Home / End | Goes to the first or the last cover. |
| Enter or Space | Opens the centred cover (`ItemInvoked`). |

## Motion

- Every cover is placed from one position, the index at the centre. Between the centre and the first neighbour a
  cover turns, slides and shrinks smoothly, so a slow drag rotates the covers one by one.
- A drag follows the hand, one cover per 73 px by default, and resists past the ends. Letting go projects the flick
  with a 0.995 per ms decay to the cover it would coast to, and a critically damped 0.5 s spring that starts at the
  release speed settles there. A flick faster than three covers a second always moves at least one cover.
- A mouse wheel moves one cover per notch; a trackpad scrolls the row sideways and snaps 140 ms after it rests.
- Covers past the edge fade out. A frame writes one perspective transform per visible cover; nothing is laid out.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Another cover | The row glides on a spring | The row moves at once | The row moves at once |
| Drag and flick | Follows the hand, then coasts and snaps | Follows the hand, then snaps at once | Follows the hand, then snaps at once |
| Caption | Slides from the side it comes from | Cross-fades | Changes at once |

## Responsive behavior

- The row fills the width it is given; a narrow row simply shows fewer side covers. Its height follows `CoverWidth`
  and `CoverAspectRatio`, plus the reflection.
- On a phone, use a smaller `CoverWidth` (about 120) so a neighbour shows on each side.
- A touch drag that starts sideways moves the row; a vertical drag still scrolls the page, and so does vertical
  trackpad scrolling.

## Related

- `Carousel` pages through slides one at a time.
- `LightboxGallery` opens photos full screen.
- `HoloCard` shows a single card in perspective.

| Component | Choose it when |
|---|---|
| `CoverFlow` | A shelf of covers to flick through |
| `Carousel` | Slides with content, one at a time |
| `LightboxGallery` | Photos to look at closely |
