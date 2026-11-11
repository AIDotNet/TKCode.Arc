---
title: Waterfall panel
summary: A masonry panel: cards of different heights flow into equal columns, each to the shortest one, so they pack without gaps.
---

<Demo name="Preview" />

## When to use

`WaterfallPanel` lays out children of different heights in equal-width columns (or children of different widths in
equal-height rows) without the row gaps a `WrapPanel` or `UniformGrid` leaves. Each child goes to the end of the lane that
is currently shortest, so the lanes stay close in length.

- **Image walls and mood boards**, where pictures keep their own aspect ratio.
- **Note and card lists** whose cards hold text of different lengths.
- **Resource browsers and dashboards** with cards of mixed heights.

## When not to use

- **Reading order matters across rows**: a masonry layout reads down the lanes, not across. Use a `WrapPanel` or a grid.
- **Tiles that span several columns**: use `TilePanel`.
- **Thousands of items**: the panel creates every child; it does not virtualize.

## Usage

`WaterfallPanel` derives from Avalonia's `Panel`. Put children in it directly, or use it as the `ItemsPanel` of an
`ItemsControl`. With `Orientation="Vertical"` (the default) the width is split into `Columns` columns (3 by default) with
`Spacing` (12 px) between them; every child is measured at the column width and keeps its own height.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer>
        <arc:WaterfallPanel Columns="3" Spacing="10">
            <Border Height="120" Background="SteelBlue" />
            <Border Height="200" Background="Orange" />
            <Border Height="150" Background="SeaGreen" />
            <Border Height="90" Background="Purple" />
        </arc:WaterfallPanel>
    </ScrollViewer>
</UserControl>
```

Placement is shortest lane first, in child order: the child goes to the lane whose end is nearest the top, and of two
equally short lanes the first one wins. The same children therefore always give the same layout, and adding a child at the
end never moves the ones before it. By default sizes come from the children's own measurement; `ItemSizing="Varied"` lets
the panel pick them instead (see [Varied sizes](#varied-sizes)). The panel is as tall as its longest lane, so it scrolls
correctly in a `ScrollViewer`.

Collapsed children (`IsVisible="False"`) take no place. Adding, removing or resizing a child, or changing any property,
lays the panel out again.

### Horizontal

`Orientation="Horizontal"` swaps the axes: the height is split into `Rows` rows (3 by default), children keep their own
widths and go to the end of the shortest row. Put it in a `ScrollViewer` that scrolls sideways.

<Demo name="Horizontal" />

### Lane count

`Columns` (or `Rows` when horizontal) sets the number of lanes. Set it to `0` to fit as many lanes as the width allows, each
at least `MinLaneSize` (200 px by default) wide: a narrow window gets one or two columns and a wide one five or six.
`LaneCount` reports the number used in the last layout.

<Demo name="Columns" />

### Item size limits

`MinItemSize` and `MaxItemSize` clamp the size of each child along the flow (its height in columns). Children are measured
within `MaxItemSize`, so wrapping text has the room it will get. A child's own `MinHeight` (or `MinWidth` in rows) always
wins over `MaxItemSize`, so its content is never cut below what it asked for.

<Demo name="ItemSize" />

### Varied sizes

When every item uses the same template, their heights are equal and the panel lays them out as a plain grid. Set
`ItemSizing="Varied"` to ignore the content's size: the panel gives each child a height (a width in rows) between
`MinItemSize` and `MaxItemSize`, so cards with the same template still form a masonry wall. Children are measured within the
size they get, so let the template stretch (a picture or color area that fills the card) rather than fix its height.

- **Repeatable.** A size depends only on the child's index and `Seed` (0 by default). Nothing is random during layout: the
  same items, seed and lane count always give the same wall, and resizing the window never shuffles it. Set another `Seed`
  for another arrangement.
- **Index-based.** Adding items at the end keeps the sizes of the items before them; inserting or removing an item changes
  the sizes of the items after it. Hiding a child (`IsVisible="False"`) keeps the others' sizes.
- **Bounds.** Set both `MinItemSize` and `MaxItemSize` for a fixed range. A bound left unset follows the lane size: half a
  lane for the minimum, one and a half lanes for the maximum. A child's own `MinHeight` (or `MinWidth`) still wins.

<Demo name="VariedSizes" />

### Filling the last item

`FillLastItem` stretches the last child of every lane to the end of the panel, so all lanes end together. The end is the
longest lane, or the panel's arranged size when its parent gives it more (a fixed `Height`, a stretching `Grid` row). Inside
a `ScrollViewer` it is always the longest lane, so nothing stretches without bound. Only the last visible child of each
lane changes; collapsed children are never stretched.

<Demo name="FillLastItem" />

### In an ItemsControl

As an `ItemsPanel` the panel lays out the item containers, so items added to or removed from an `ObservableCollection`
appear in place. This example also uses `Columns="0"`, so the column count follows the width.

<Demo name="ItemsControl" />

## API reference

<!-- api: WaterfallPanel -->

## Accessibility

- The panel adds no semantics of its own; the order of the children is the reading and Tab order, which in a masonry
  layout runs down the lanes in placement order, not left to right. Keep the order of children meaningful.
- Give interactive cards their own accessible names (`AutomationProperties.Name`).

## Motion

- The panel does not animate: children move to their new places in a single layout pass when the width, the lane count or
  the children change.

## Responsive behavior

- With a fixed `Columns` the columns share whatever width the panel gets and shrink or grow with it.
- With `Columns="0"` the column count follows the width: as many columns of at least `MinLaneSize` as fit, at least one.
- In a horizontal `ScrollViewer` (unbounded width) with `Orientation="Vertical"`, every column is as wide as the widest
  child; prefer a vertical `ScrollViewer`.

## Related

- `TilePanel`: a grid of tiles that span rows and columns.
- `CyclicPanel`: a list that loops without end.
