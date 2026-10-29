---
title: Gravity well
summary: Tiles that lean toward the pointer, shrink and dim as if pulled into a well, with a click that rolls a shockwave through the grid.
---

<Demo name="Preview" />

## When to use

`GravityWell` lays its items out as a grid of square tiles and bends the grid around the pointer, as if a heavy mass
sat under it. Tiles near the pointer lean in, shrink, dim and slide under their neighbours; a click sends a shockwave
that rolls out through the grid and flashes each tile's ring as it passes.

- **Team, customer and partner walls**: many faces or logos at once, where play is the point.
- **Moodboards and galleries on landing pages**: show the breadth of a collection in one playful block.
- **Small decorative sets**: icons, stickers or swatches people can push around.

## When not to use

- **Grids people must scan or click precisely**, such as file pickers: tiles move under the pointer. Use a plain grid.
- **Large collections**: every tile is animated. Keep it to a few dozen; use a photo grid or a virtualized list for
  more.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:GravityWell Label="Team" Columns="6">
        <Border Background="#d9c2a5" />
        <Border Background="#b8c7a3" />
        <Border Background="#a9bcd6" />
    </arc:GravityWell>
</UserControl>
```

`GravityWell` is an `ItemsControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Each item becomes one square tile, either written inline or created from `ItemsSource` and an
`ItemTemplate`. The tile clips its content to `TileCornerRadius` (12px by default) and shows a muted surface behind it,
so photos that are still loading keep their place. Stretch images with `Stretch="UniformToFill"` to fill the square.

### Columns

`Columns` fixes the number of columns. Leave it at 0 and the grid fits as many columns as it can while keeping every
tile at least `MinTileSize` wide (56px by default); the tiles then share the remaining width. `Gap` is the space
between tiles (8px).

<Demo name="Team" />

### Reach and strength

`Radius` is the reach of the well in px (150) and `Strength` how hard it pulls, from 0 to about 1.5 (1). A larger
radius bends more of the grid at once; a stronger well pulls the nearest tiles further in and shrinks them more.

### Responding to a shockwave

`Pulsed` fires for every shockwave, from a click, a tap, Space or Enter, and `GravityWellPulseEventArgs.Origin` says
where it started, in px from the top left of the grid. `Pulse(point)` sends one from code. Tie it to something that
matters, such as shuffling or picking an item, or leave it as a small delight.

<Demo name="Shuffle" />

```csharp
well.Pulsed += (_, e) => Shuffle(people);
```

### Best practices

**Do**

- Use a uniform set of square pictures, around 30 to 60.
- Give the grid a `Label` that names what it shows, such as "Team" or "Customers".
- Give each image tile a tooltip or an accessible name when the people or logos matter.

**Avoid**

- Mixing portrait and landscape pictures without cropping them; the tiles are always square.
- Putting buttons or links inside the tiles. The tiles move under the pointer, so they are hard to hit.

### Common questions

**Does the reading order change when tiles slide under each other?** No. Tiles keep their slots in the grid and only
move by their render transform, opacity and stacking order.

**Why does the grid stay still?** Reduced motion keeps every tile in place; a shockwave then only flashes the rings.

## API reference

<!-- api: GravityWell -->

<!-- api: GravityWellPulseEventArgs -->

## Accessibility

- The grid is a focusable group named by `Label` ("Gravity well" by default) and described as a "gravity well". The
  tiles' content keeps its own names and roles.
- Motion is decoration. Tiles never leave their slots, so the order a screen reader follows matches the grid.
- Keyboard focus shows a 2px focus ring around the grid; pointer focus shows none. While the grid has keyboard focus
  the well itself shows which tile is current.
- Under reduced motion the tiles stay in place, and a shockwave only flashes their rings.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the grid and place the well on the current tile |
| `Left` / `Right` | Move the well to the previous or next tile |
| `Up` / `Down` | Move the well a row up or down |
| `Home` / `End` | Move the well to the first or last tile |
| `Space` / `Enter` | Send a shockwave from the current tile |

## Motion

- The well follows the pointer on a critically damped spring: it trails a fast move and arrives without overshooting.
- Each tile follows the well's pull on its own lightly damped spring, so a quick sweep leaves a wobbling wake. The
  deeper a tile sits, the more it leans toward the well, shrinks (to about 0.56 at the bottom), dims and sinks under
  its neighbours.
- A shockwave travels outward at 900px per second. As its front reaches a tile it kicks the tile away from the origin,
  dips its scale and flashes its 2px accent ring, more strongly near the origin; the ring then fades out.
- On touch screens a tap sends a shockwave and a drag pulls the well; vertical scrolling keeps working.
- Each frame writes only a transform, an opacity and a stacking order per tile, with no layout. Frames stop when every
  spring is at rest and pause while the grid is out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Well | Bends the grid | None | None |
| Shockwave | Kicks the tiles and flashes the rings | Flashes the rings | None |

## Related

- `DotGrid` bends a field of dots instead of content.
- `CardStack` and `Carousel` show a few pictures one at a time.

| Component | Choose it when |
|---|---|
| `GravityWell` | Many small pictures should feel playful together |
| `DotGrid` | You want a reactive background behind a headline |
| `Carousel` | Each picture deserves a closer look |
