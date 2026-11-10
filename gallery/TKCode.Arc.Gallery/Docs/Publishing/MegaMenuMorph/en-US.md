---
title: Mega menu morph
summary: A site navigation bar whose one dropdown panel springs to each item's size and place as the pointer moves along the bar, with the content sliding in from the side it travelled to.
---

<Demo name="Preview" />

## When to use

`MegaMenuMorph` is the top navigation of a marketing site or a product with many destinations. Each top-level item can
open a large panel of links under the bar. Instead of closing one dropdown and opening the next, there is only one
panel: when the pointer moves from "Product" to "Teams", the panel springs to the new item's width, height and place
under the bar, the old links slide out toward where the pointer came from and the new ones slide in from where it went.
The movement tells people that they are browsing one menu, not opening a stack of separate ones.

- **Site headers with grouped destinations**: products, solutions, resources, each with a short description.
- **Panels of different sizes**: a two-column grid next to a single list next to a feature card; the morph absorbs the
  difference.
- **Bars that mix panels and plain links**: an item without a panel (Pricing) is a link and closes the panel.

## When not to use

- **App navigation inside a product**: use a side navigation or `Tabs`; a hover panel slows down repeated work.
- **A handful of actions**: use `DropdownMenu`.
- **Small screens only**: put the same destinations in a `Drawer` or `BottomSheet`; hover does not exist on touch.
- **Deep hierarchies**: a mega menu shows one level of links per item; use `TreeView` or a site map page for more.

## Usage

`MegaMenuMorph` lives in the Special package: register it with `UseArcSpecial()` next to `UseArc()`. Write the items as
children. An item's content is its panel: any control, usually a `Grid` or `StackPanel` of `MegaMenuMorphLink`s. An
item without content is a plain link.

```xml
<arc:MegaMenuMorph Label="Main">
    <arc:MegaMenuMorphItem Header="Product">
        <StackPanel Width="280" Spacing="4">
            <arc:MegaMenuMorphLink Title="Boards" Description="Plan work in columns" />
            <arc:MegaMenuMorphLink Title="Docs" Description="Write specs next to the work" />
        </StackPanel>
    </arc:MegaMenuMorphItem>
    <arc:MegaMenuMorphItem Header="Pricing" />
</arc:MegaMenuMorph>
```

`LinkInvoked` fires when a link in a panel, or an item without a panel, is chosen; its arguments carry the `Item`, the
`Link` (null for a plain item) and the `Title`. Navigate there; the panel has already started to close.

The panel measures its content at most `MaxPanelWidth` wide (760 by default) and never closer than 12px to the
viewport's edges, centred under its trigger. Give fixed widths to the columns of a panel so the morph has clear sizes to
travel between.

### Opening on click only

With `OpensOnHover` off, panels open on click, tap or the keyboard. Once one is open, moving to a neighbour still morphs
the panel there.

<Demo name="ClickToOpen" />

### Timing

`OpenDelay` (80 ms) is how long the pointer rests on a trigger before a closed menu opens, so a pointer crossing the bar
does not flash it open. `CloseDelay` (220 ms) is how long the pointer may be away from both the bar and the panel; it
covers the gap between them and short slips off the edge. Moves between items while open are immediate.

### Reduced motion

Under reduced motion the panel jumps to the next item's place and size and the content cross-fades in 120 ms without
sliding; opening and closing only fade.

<Demo name="ReducedMotion" />

### Anatomy

1. **Bar**: the triggers in a row, 36px high pills with a 2px gap, label in the secondary text color and medium weight.
2. **Highlight**: a muted pill that glides under the hovered, focused or open trigger.
3. **Chevron**: on items with a panel; it turns over while the panel is open.
4. **Panel**: the raised surface with a hairline edge, the panel radius and the floating shadow, 10px under the bar,
   12px padding.
5. **Link**: a row with an icon tile (36px, muted, tinted with the accent on hover), a title and an optional description.

## API reference

<!-- api: MegaMenuMorph -->

<!-- api: MegaMenuMorphItem -->

<!-- api: MegaMenuMorphLink -->

<!-- api: MegaMenuMorphLinkInvokedEventArgs -->

## Accessibility

- The control is a navigation landmark named by `Label`. Each trigger with a panel is a button that reports expanded
  or collapsed; a trigger without a panel and every `MegaMenuMorphLink` are links. A link's description is its help
  text.
- The open panel is a region named after its item.
- Keyboard focus shows the 2px focus ring on triggers and links and moves the highlight; pointer focus shows none.

### Keyboard

| Key | Action |
|---|---|
| Tab | Moves along the triggers; from inside a panel, past its last link to the next trigger (the panel closes) |
| Left / Right | Moves to the previous / next trigger; an open panel follows |
| Home / End | Moves to the first / last trigger |
| Enter / Space | Opens or closes the trigger's panel, or follows a plain link |
| Down | Opens the panel and moves to its first link; inside a panel, moves to the next link |
| Up | Inside a panel, moves to the previous link |
| Escape | Closes the panel and returns to its trigger |

## Motion

- **Morph**: the panel's rectangle (position, width and height together) follows one spring without overshoot that
  settles in about 0.4 s, from wherever it is, so quick passes along the bar stay continuous.
- **Content**: over 300 ms on the standard curve the old content slides 36px toward where the pointer came from and is
  gone by 55%; the new content slides in the same distance from the other side and fades in after the first 15%. Both
  are clipped to the panel's current shape.
- **Open / close**: the panel fades in over 200 ms on the enter curve while it settles 6px down and grows from 98.5%;
  it fades out over 150 ms. The chevron turns on the snappy spring.
- Reduced motion keeps only fades (the content cross-fades in 120 ms); with motion off everything is instant.

## Responsive behavior

- The panel is never wider than the viewport less 12px on each side; content wider than that is measured at the
  available width and wraps.
- The panel is centred under its trigger and slides along the bar to stay on screen near the edges.
- The bar does not wrap: on narrow screens put the same items in a `Drawer` and show `MegaMenuMorph` from a breakpoint
  up. Touch opens panels by tap; nothing depends on hover.

## Related

- `DropdownMenu`: a menu of actions under a button.
- `SiteHeader`: a complete site header block with a logo, navigation and actions.
- `Tabs`: switches between views of one page.
- `Drawer`: holds the same destinations on small screens.
