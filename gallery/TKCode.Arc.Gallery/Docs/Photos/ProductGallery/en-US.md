---
title: Product gallery
summary: The pictures of a product page. One large picture with a strip of thumbnails, a zoom that follows the mouse over the picture, and a swipe on touch screens.
---

<Demo name="Preview" />

## When to use

`ProductGallery` is the picture block at the top of a product page. One picture fills a square stage and the others
wait as thumbnails under it or down its left side. People shopping want to look closely before they buy, so moving a
mouse or a pen over the picture zooms into the spot under the pointer, and the zoom follows the pointer as it moves:
the weave of a fabric or the print on a label can be read without opening anything. On a phone the picture is swiped
from side to side instead, and it keeps the speed of the flick when the finger lets go. Arrows, the arrow keys and the
thumbnails all slide to another picture.

- **Shop product pages**: views of the item, a close-up of the material, the item in use.
- **Listings**: a flat for rent, a used bike or a handmade item, where every picture matters.
- **Variants**: swap the pictures when a color or a finish is picked, and keep the same view on the stage.

## When not to use

- **Browsing many photos**: a grid that opens full screen suits an album better; use `LightboxGallery`.
- **Editorial pictures**: a story's photos read better at their own sizes in a `PhotoGrid`.
- **One picture only**: an `ArcImage` in a rounded frame is enough; the gallery hides its strip and arrows then anyway.

## Usage

```xml
<arc:ProductGallery Label="Speckled stoneware mug" MaxWidth="440">
    <arc:ProductGalleryImage Source="https://example.com/mug/front.jpg" Description="Front, handle on the right" />
    <arc:ProductGalleryImage Source="https://example.com/mug/side.jpg" Description="Handle side" />
    <arc:ProductGalleryImage Source="https://example.com/mug/glaze.jpg" Description="Close-up of the glaze" />
</arc:ProductGallery>
```

`ProductGallery` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Write the
pictures inline as `ProductGalleryImage`s, or bind `ItemsSource` to a list of them, of `ImageSource`s or of addresses.
Give each picture a `Description`: it is what a screen reader says for it. A `Thumbnail` can point at a smaller copy for
the strip. `SelectedIndex` is the picture on the stage and can be bound both ways.

### Thumbnails on the side and a stronger zoom

`ThumbnailPlacement="Left"` runs the strip down the left side, which suits a wide page. `AspectRatio` shapes the stage
(width divided by height, square by default) and `Stretch` decides how a picture fills it: `Uniform` shows the whole
product on the muted ground, `UniformToFill` crops it to the stage. `ZoomScale` sets how far the hover zoom goes; 1 or
`IsHoverZoomEnabled="False"` turns it off.

<Demo name="ThumbnailsLeft" />

### Variants

Give the gallery a new `ItemsSource` when a color or a finish is picked. Set `SelectedIndex` back to the view that was
showing, and the stage keeps it. `SelectedImage` is the picture on the stage.

<Demo name="Variants" />

### Best practices

**Do**

- Put the plainest view first: it is the one shown before anyone touches the gallery.
- Use pictures at least twice the size of the stage, so the zoom has detail to show.
- Keep the same views in the same order across variants.

**Avoid**

- Mixing proportions: a stage of one shape with pictures of many shapes leaves uneven margins.
- Text that only appears in a picture, such as a size chart; put it on the page as well.

## API reference

<!-- api: ProductGallery -->

## Accessibility

- The gallery is one tab stop with the name in `Label`. Its help text, and a polite status line, say which picture is
  on the stage: "Image 2 of 5: Handle side". `ImageNameFormat` changes the wording.
- The thumbnails are buttons named the same way. The strip is one more tab stop, on the selected thumbnail, and the
  arrow keys move along it.
- The previous and next buttons are not tab stops, since the arrow keys do the same; they are named by `PreviousLabel`
  and `NextLabel` for assistive technology.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring around the stage or the thumbnail.

### Keyboard interactions

| Key | Action |
|---|---|
| Left / Right | Slides to the previous or next picture (Up / Down in a strip on the left). |
| Home / End | Goes to the first or the last picture. |
| Tab | Moves between the stage and the thumbnail strip. |
| Enter or Space | On a thumbnail, shows its picture. |

## Motion

- A new picture slides in from the side it comes from, on a critically damped spring. A jump across several pictures
  slides between the two that are seen, never through the ones in between.
- A swipe follows the finger 1:1 and resists past the first and last picture. Letting go picks the next picture when
  the swipe went past half way or was flicked faster than 360 px/s, and the spring starts at the finger's speed.
- The hover zoom grows in over 240 ms toward the point under the pointer, follows the pointer on a short spring, and
  shrinks back over 160 ms when the pointer leaves. The arrows and the counter fade away while zoomed.
- Nothing is laid out while the pictures move: each frame writes three transforms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New picture | Slides in on a spring | Fades in over 160 ms | Appears |
| Hover zoom | Grows in and follows the pointer | Zooms at once and follows | Zooms at once and follows |
| Arrows and counter | Fade in and out | Change at once | Change at once |

## Responsive behavior

- The stage takes the width it is given and its height follows `AspectRatio`; with a fixed height it is as wide as the
  height allows.
- The strip scrolls when the thumbnails do not fit. A strip on the left suits wide pages; switch to the bottom on a
  phone.
- Touch never zooms by hovering: a touch drag swipes the pictures, and vertical drags still scroll the page.

## Related

- `LightboxGallery` opens many photos full screen.
- `PhotoGrid` lays out photos at their own proportions.
- `ImageCompare` puts two pictures on one stage to compare them.

| Component | Choose it when |
|---|---|
| `ProductGallery` | One item, several views, and a close look before buying |
| `LightboxGallery` | Many photos to browse, each opened full screen |
| `PhotoGrid` | A set of photos shown at their own shapes |
