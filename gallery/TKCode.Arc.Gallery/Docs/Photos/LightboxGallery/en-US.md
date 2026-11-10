---
title: Lightbox gallery
summary: A grid of photos that opens full screen. The tile grows into the lightbox, arrows and swipes move between photos, and the wheel or a pinch zooms in.
---

<Demo name="Preview" />

## When to use

`LightboxGallery` shows a set of photos as a grid of equal tiles and opens any of them over the whole window. The
chosen tile grows into the lightbox: the photo opens out of the tile's crop to its whole frame, and on close it shrinks
back into its tile, even into another tile if the person moved on to another photo. In the lightbox the photo fills the
window at its own proportions; arrows, the arrow keys and a sideways swipe move between photos, and the wheel, a
trackpad or touch pinch, a double click or the zoom buttons enlarge it around the pointer so it can be dragged about.
Escape, the close button, a click beside the photo or pulling the photo up or down closes it.

- **Albums and trip photos**: many photos people want to see one by one, large.
- **Portfolios and project pages**: work that needs room to be judged.
- **Attachments**: the pictures of a ticket, a listing or a message thread.

## When not to use

- **One item with a few views**: a product page wants the pictures beside the price; use `ProductGallery`.
- **Photos of mixed shapes that should keep them in the grid**: use `PhotoGrid`, which lays photos out at their own
  proportions.
- **Documents and scans**: a page needs paging and fitting to width; use `PageViewer`.

## Usage

```xml
<arc:LightboxGallery Label="Trip photos" MinTileWidth="140">
    <arc:LightboxGalleryImage Source="https://example.com/trip/ridge.jpg" Caption="First light on the ridge" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/harbour.jpg" Caption="Boats coming home" />
    <arc:LightboxGalleryImage Source="https://example.com/trip/valley.jpg" Caption="Pine valley after the rain" />
</arc:LightboxGallery>
```

`LightboxGallery` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Write the
photos inline as `LightboxGalleryImage`s, or bind `ItemsSource` to a list of them, of `ImageSource`s or of addresses.
`Caption` is shown under the photo in the lightbox. `Thumbnail` can point at a smaller copy for the tile, which the
lightbox also shows until the full photo has arrived, and `AspectRatio` shapes the lightbox before then.

### Tiles

The grid fits as many columns as `MinTileWidth` allows, or exactly `Columns`. `TileAspectRatio` shapes every tile
(square by default) and `Spacing` sets the gap. Tiles crop their photo; the lightbox always shows it whole. `MaxZoom`
caps how far a photo can be enlarged.

<Demo name="WideTiles" />

### Opening from code

`Open(index)` opens the lightbox on a photo, and `Close()` closes it. `OpenIndex` is the photo on show, -1 while the
lightbox is closed; it can be bound both ways, so setting it opens, moves or closes the lightbox too. `IsOpen` and
`IsZoomed` report the state.

<Demo name="OpenFromCode" />

### Best practices

**Do**

- Write a caption for each photo, or a `Description` when it needs no caption: screen readers read it.
- Give `AspectRatio` when you know it, so the lightbox has the right shape at once.
- Pass smaller thumbnails for big sets: the grid then loads quickly.

**Avoid**

- Putting buttons or links on the photos: the tile is one button that opens the lightbox.
- Very small tiles: under about 96 px a photo is hard to recognize.

## API reference

<!-- api: LightboxGallery -->

## Accessibility

- The grid is a list named by `Label`; each tile is a button named "Photo 3 of 12: caption" (`ImageNameFormat` changes
  the wording). The grid is one tab stop and the arrow keys move between tiles.
- The lightbox is a dialog: focus moves into it, stays inside while it is open, and returns to the tile of the photo
  that was on show when it closes.
- A polite status line announces each photo as it comes on.
- The buttons of the lightbox are named by `CloseLabel`, `PreviousLabel`, `NextLabel`, `ZoomInLabel` and
  `ZoomOutLabel`.

### Keyboard interactions

| Key | Action |
|---|---|
| Arrow keys | In the grid, move between tiles. |
| Enter or Space | Opens the lightbox on the focused tile. |
| Left / Right | In the lightbox, show the previous or next photo; pan a zoomed photo. |
| Up / Down | Pan a zoomed photo. |
| + / - | Zoom in or out about the middle. |
| 0 | Shows the whole photo again. |
| Home / End | Show the first or the last photo. |
| Escape | Closes the lightbox. |

## Motion

- Opening, the tile flies into the lightbox on a critically damped 0.46 s spring while the backdrop darkens. The photo
  is redrawn every frame with the crop that the stretch turns into an undistorted picture, so it opens out of the
  tile's crop rather than squashing, and its corners melt from the tile's radius to square.
- Closing flies the photo back into the tile of the photo on show (0.4 s) while the backdrop clears. The tile is
  hidden while its photo is in the lightbox.
- Swipes follow the finger and keep their speed when let go; arrows and keys slide the next photo in on a spring.
- A pull up or down shrinks the photo a little and clears the backdrop as it goes. Past 18% of the height, or flicked
  at 700 px/s, it closes; otherwise it springs back.
- Zooming with buttons, keys or a double click animates between two views with the point under the pointer held still;
  the wheel and pinches follow the hand directly.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening and closing | The tile flies into the lightbox and back | The lightbox fades in and out (160 ms) | Appears and disappears |
| Next photo | Slides in on a spring | Fades in over 160 ms | Appears |
| Zoom by buttons and keys | Animates about the anchor | Changes at once | Changes at once |

## Responsive behavior

- The grid drops columns as it narrows (`MinTileWidth`); the tiles always keep `TileAspectRatio`.
- The lightbox covers the window, or the nearest `ArcOverlayHost`. Below 640 px wide the photo runs edge to edge and
  the side arrows give way to swiping.
- A drag that starts sideways swipes and one that starts up or down pulls to close; once zoomed, any drag pans.

## Related

- `ProductGallery` shows a few views of one item with a hover zoom.
- `PhotoGrid` lays photos out at their own proportions.
- `CardToArticle` grows a card into a full article with the same kind of flight.

| Component | Choose it when |
|---|---|
| `LightboxGallery` | Many photos, each opened large |
| `ProductGallery` | One item, several views, beside other content |
| `PhotoGrid` | The set itself is the page |
