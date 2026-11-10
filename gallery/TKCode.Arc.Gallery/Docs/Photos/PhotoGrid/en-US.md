---
title: Photo grid
summary: Photos in justified rows of one height or in masonry columns. A selected photo opens wide in place, and every photo glides to its new spot when the grid changes.
---

<Demo name="Preview" />

## When to use

`PhotoGrid` lays photos out edge to edge without cropping them. In justified rows, the default, every row has one
height: a row takes photos until they would be wider than the grid at the target height, then they are all scaled
together to fill the width exactly. In masonry columns, every photo goes to the shortest column. Selecting a photo
(a click, Enter or Space) opens it wide on a row of its own, and the photos around it flow aside. Whenever the layout
changes, because the window is resized, photos are added or removed, or a photo is selected, every photo glides from
where it was to its new spot instead of jumping, so the eye can follow it.

- **Albums and camera rolls**: many photos of mixed shapes, shown whole.
- **Portfolios and mood boards**: work where the shape of each picture matters.
- **Search results for images**: a list that grows and changes while people look.

## When not to use

- **Opening photos full screen**: a grid that opens a lightbox suits that; use `LightboxGallery`.
- **Equal tiles with text**: cards with titles and actions belong in a card grid.
- **Thousands of photos**: each photo is a control of its own; page the set, or use a virtualized list.

## Usage

```xml
<arc:PhotoGrid Label="Trip photos" RowHeight="160">
    <arc:PhotoGridPhoto Source="https://example.com/trip/ridge.jpg" AspectRatio="1.5" Description="First light on the ridge" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/tower.jpg" AspectRatio="0.75" Description="The old signal tower" />
    <arc:PhotoGridPhoto Source="https://example.com/trip/dunes.jpg" AspectRatio="1.78" Description="Dunes at noon" />
</arc:PhotoGrid>
```

`PhotoGrid` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Write the photos
inline as `PhotoGridPhoto`s, or bind `ItemsSource` to a list of them, of `ImageSource`s or of addresses. Give
`AspectRatio` (width divided by height) when you know it: the layout is then final before the pictures arrive.
Without it a photo is laid out at 3:2 and moves into its own shape once it has loaded. `RowHeight` is the height the
rows aim for; they end up somewhat taller so the width is filled. `Spacing` sets the gap both ways.

### Masonry columns

`Layout="Masonry"` stacks the photos in columns as wide as `ColumnWidth` allows. The selected photo spans every column.

<Demo name="Masonry" />

### Changes that glide

Bind `ItemsSource` to an `ObservableCollection` and the grid follows it: new photos grow in at their place, removed ones
fade where they are and the rest glide to their new spots. Switching `Layout`, changing `RowHeight` or resizing glides
the same way.

<Demo name="Reflow" />

### Selection

`SelectedIndex` is the photo opened wide (-1 for none) and can be bound both ways. Clicking it again or pressing Escape
closes it. `SelectedMaxHeight` caps its height. Set `IsSelectionEnabled="False"` for a plain layout.

### Best practices

**Do**

- Give `AspectRatio` for every photo you can, so nothing moves while pictures load.
- Pick a `RowHeight` that shows a few photos per row at your usual width.
- Write a `Description` for each photo: screen readers read it.

**Avoid**

- Very tall or very wide panoramas mixed with small photos: they make rows very low or very high.
- Laying text over the photos; put it beside the grid.

## API reference

<!-- api: PhotoGrid -->

## Accessibility

- The grid is a list named by `Label`; each photo is a list item named "Photo 4 of 18: description"
  (`ImageNameFormat` changes the wording), and the selected one says so.
- The grid is one tab stop. Left and Right move through the photos in order, Up and Down move to the photo above or
  below in the nearest row, Home and End go to the first and the last.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring around the photo.

### Keyboard interactions

| Key | Action |
|---|---|
| Left / Right | Moves to the previous or next photo. |
| Up / Down | Moves to the photo above or below. |
| Home / End | Moves to the first or the last photo. |
| Enter or Space | Selects the photo, or closes it when it is selected. |
| Escape | Closes the selected photo. |

## Motion

- Every change of layout is one reflow: each photo travels from the box it is drawn in to its new box on a critically
  damped 0.5 s spring, moving and resizing together. A change in the middle of a reflow starts from where the photos
  are, so nothing jumps.
- New photos grow from 92% and fade in at their place; removed photos fade out where they were.
- A reflow writes one transform per photo each frame; nothing is laid out while the photos move.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Resize, select, add, remove | Photos glide to their new places | Moved photos fade in at their new places (240 ms) | Photos move at once |
| New photo | Grows and fades in | Fades in | Appears |
| Removed photo | Fades out | Fades out | Disappears |

## Responsive behavior

- The grid fills the width it is given and is as tall as its photos; put it in a `ScrollViewer` for long sets.
- Justified rows keep their target height at every width, so a narrow window simply has fewer photos per row. Masonry
  drops columns as it narrows.
- The selected photo is as wide as the grid allows, up to `SelectedMaxHeight` tall, and is scrolled into view.

## Related

- `LightboxGallery` opens photos full screen from a grid of equal tiles.
- `ProductGallery` shows a few views of one item.
- `CardToArticle` lays out story cards that open into articles.

| Component | Choose it when |
|---|---|
| `PhotoGrid` | Photos of mixed shapes, shown whole |
| `LightboxGallery` | Photos opened one by one, large |
| `ProductGallery` | A few views of one item |
