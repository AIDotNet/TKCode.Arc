---
title: Spotlight grid
summary: Feature cards lit by one soft light that follows the pointer or the keyboard focus across the grid, warming the faces and lighting the borders it passes.
---

<Demo name="Preview" />

## When to use

`SpotlightGrid` presents a set of features as quiet cards (an icon tile, a title, a sentence and an optional badge) and
adds one effect: a soft light in the accent color that follows the pointer across the whole grid. Under the light the
card faces warm slightly and the card borders glow, including the edges of neighbouring cards, so it reads as a single
lamp moving over the page rather than a hover state per card. When the pointer leaves, the light fades; when the
keyboard moves focus to a card, the light travels to that card.

- **A features section** of a landing page with three to nine features.
- **A dark marketing page** where a subtle interactive touch helps the grid feel alive.

## When not to use

- **Features with pictures of their own**: use `FeatureBento`, whose cards each carry a live drawing.
- **Navigation cards** that must look clickable at rest: use `Card` with a clear call to action.
- **Dense data**: use a table or a metrics block.

## Usage

```xml
<arc:SpotlightGrid Title="Everything in one place" MaxColumns="3">
    <arc:SpotlightGridItem Title="Instant search" Description="Every document answers in under 50 ms." Icon="{x:Static arc:ArcIcons.Search}" />
    <arc:SpotlightGridItem Title="Branch previews" Description="Every change gets its own link." Badge="New" />
</arc:SpotlightGrid>
```

With no items the grid shows six invented features; set `ShowsSampleItems` to false to leave it empty. `Icon` takes any
24×24 stroked geometry, such as the `ArcIcons`.

### The grid alone

<Demo name="CustomItems" />

With no eyebrow, title and description the heading is left out and the grid stands alone. Columns are added while each
stays at least `MinColumnWidth` (260) wide, up to `MaxColumns` (3). `IsSpotlightEnabled` turns the light off.
`ItemInvoked` reports a click, a tap, Enter or Space on a card.

## API reference

<!-- api: SpotlightGrid -->

## Accessibility

- The section is named by its title, a level 2 heading.
- Each card is focusable, named by its title, with the description as help text; the keyboard ring sits 4 outside it.
- The light is decoration only: nothing depends on it, and contrast is the same with it on or off.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move through the cards; the light follows the focus. |
| Arrow keys | Move between cards by column and row. |
| `Home` / `End` | First or last card. |
| `Enter` / `Space` | Raise `ItemInvoked`. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Light follows the pointer | A quick spring with no overshoot | Follows directly | Follows directly |
| Light appears and fades | 160 ms in, 180 ms out | Same | Immediate |

Following the pointer allocates nothing: the light's gradients are cached, and each frame only moves the drawing origin.

## Responsive behavior

- The number of columns follows the width: three on a wide page, two on a tablet, one on a phone.
- Below 720 of the block's own width (`:compact`) the title steps down and the paddings tighten.
- On touch screens the light shows under the finger while it is down and fades when it lifts.

## Related

- `FeatureBento` for a grid of cards with live drawings.
- `Card` for a single content card.
