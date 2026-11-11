---
title: Tile panel
summary: A start-screen tile grid: tiles span rows and columns, take the first free place without overlapping, and resize with the panel.
---

<Demo name="Preview" />

## When to use

`TilePanel` divides its area into equal cells and lets each child cover several of them: 1×1, 1×2, 2×2 and so on. Children
are placed in order at the first free spot where they fit, so a mix of sizes packs into a tidy grid without you computing
any positions.

- **Dashboards** where important figures get bigger tiles.
- **Launchers and start screens** with apps and shortcuts.
- **Galleries** where a few highlighted items are larger.

## When not to use

- **You want to put each child in a specific row and column**: use Avalonia's `Grid`.
- **Children of free heights**: use `WaterfallPanel`.
- **Equal tiles only**: Avalonia's `UniformGrid` or a `WrapPanel` is simpler.

## Usage

`TilePanel` derives from Avalonia's `Panel`. Set how many cells a child covers with the attached properties
`TilePanel.RowSpan` and `TilePanel.ColumnSpan` (1 by default).

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TilePanel Columns="6" Spacing="6">
        <Border arc:TilePanel.RowSpan="2" arc:TilePanel.ColumnSpan="2" Background="SteelBlue" />
        <Border arc:TilePanel.ColumnSpan="2" Background="Orange" />
        <Border Background="SeaGreen" />
    </arc:TilePanel>
</UserControl>
```

The placement is first fit and fully deterministic:

1. Children are placed in order. Collapsed children (`IsVisible="False"`) take no cell.
2. A child's spans are cut to `MaxRowSpan` and `MaxColumnSpan` (0, the default, means no limit) and to the grid.
3. The child goes to the first position where all its cells are free, scanning rows from the top and, in each row, columns
   from the left. A later small tile can therefore fill a hole left before a wider one.
4. Tiles never overlap, and the grid grows by rows until every tile has a place: no tile is ever dropped.

`GetPlacement(child)` returns the row, column and spans a child got, and `RowCount` / `ColumnCount` the size of the grid.

### Grid size

`Columns` fixes the column count and the columns share the width. `Rows` makes the rows share the height (when the height is
bounded); tiles that do not fit in that many rows continue on further rows of the same size below. A side that nothing sizes
copies the other, so cells are square unless both are set.

<Demo name="FixedGrid" />

With `Columns="0"` (the default) the panel fits as many columns of at least `MinCellSize` (96 px) as the width allows, and
the tiles reflow when the width changes. When the width is unbounded, for example in a horizontal `ScrollViewer`, set `Rows`
instead: the rows are fixed, tiles fill columns top to bottom and the grid grows to the right.

<Demo name="AutoColumns" />

### Span limits

`MaxRowSpan` and `MaxColumnSpan` cap what any tile may cover, whatever it asks for.

<Demo name="SpanLimits" />

### Auto fill

`IsAutoFill` (off by default) is an opt-in step after placement that lets tiles grow into empty cells. The rule is fixed so
the result is predictable:

- Tiles are visited in child order.
- A tile grows to the right one column at a time while every cell of the next column beside it is empty, up to
  `MaxColumnSpan` and the last column.
- Then it grows down one row at a time likewise, up to `MaxRowSpan` and the grid's height: `Rows`, or the rows the tiles
  already use, whichever is more.
- Every hole that is still empty is then offered to the tiles around it, on any side: the tile to its left or right grows
  sideways, the one above or below grows up or down, when the whole strip of cells it needs is empty and its span limit
  allows. This repeats until no tile can grow.
- Tiles never move, overlap or shrink, so explicit spans are always kept. A hole stays empty only when no neighbouring tile
  can cover it within `MaxRowSpan` / `MaxColumnSpan`.

<Demo name="AutoFill" />

### Auto span

`IsAutoSpan` (off by default) lets the panel size the tiles: children that set neither `TilePanel.RowSpan` nor
`TilePanel.ColumnSpan` get 1×1, 1×2, 2×1 or 2×2. This is meant for an `ItemsControl` whose items all use one template, where
nothing on the items says how big each one should be.

- **Repeatable.** A tile's size depends on its index, `Seed` (0 by default) and the grid, never on random values during
  layout: the same items, seed and grid always give the same tiles, and with a fixed `Columns` resizing the window only
  scales them. Set another `Seed` for another arrangement. Adding items at the end keeps the tiles before them; inserting or
  removing one changes the tiles after it.
- **No holes before a tile.** An automatic tile goes to the first free cell and takes a size that fits there, so it never
  leaves an empty cell before it.
- **Spread out.** Larger tiles are kept from touching each other, so they do not bunch up, and none straddles the last of
  `Rows`. `MaxRowSpan` and `MaxColumnSpan` still apply (`MaxColumnSpan="1"` allows only 1×1 and 2×1).
- **Explicit spans win.** A child that sets either span keeps the spans it asked for, as without auto span.

With `IsAutoFill` and a fixed grid (`Rows` set as well as `Columns`, or a width that fixes the column count), the panel
sizes the automatic tiles to cover the whole `Columns` × `Rows` grid: fewer items get larger tiles, and the auto fill step
covers whatever is left. With more items than cells, all automatic tiles are 1×1 and the rest continue on further rows.

<Demo name="AutoSpan" />

### In an ItemsControl

The panel's children are the item containers, so put the spans on them, for example with a style that reads them from the
item. Replacing an item rebuilds its container with the new spans. Items that should not choose a size need no spans at all
with `IsAutoSpan` (see [Auto span](#auto-span)).

<Demo name="ItemsControl" />

## API reference

<!-- api: TilePanel -->

## Accessibility

- The panel adds no semantics of its own; the order of the children is the reading and Tab order, which matches the
  placement order (left to right, top to bottom, with small tiles filling holes earlier). Keep the order meaningful.
- Give interactive tiles their own accessible names (`AutomationProperties.Name`).

## Motion

- The panel does not animate: tiles move to their new cells in a single layout pass when the width, the grid or the
  children change.

## Responsive behavior

- With a fixed `Columns` the placement stays the same and only the cell size follows the width.
- With `Columns="0"` the column count follows the width (as many columns of at least `MinCellSize` as fit) and the tiles
  reflow.
- Spans wider than the grid are cut to the column count, so tiles never leave the panel at narrow widths.

## Related

- `WaterfallPanel`: equal columns of children with free heights.
- `CyclicPanel`: a list that loops without end.
