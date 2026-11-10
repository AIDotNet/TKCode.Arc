---
title: Feature bento
summary: A bento grid of features in cards of different sizes, each with a small live drawing that moves only while on screen.
---

<Demo name="Preview" />

## When to use

`FeatureBento` arranges features in a bento grid: cards of four sizes packed into six columns, so one or two important
features get large cards and the rest fill in around them. Each card has a small live drawing at the top, a title and a
sentence. The drawings are built in and quiet: a line chart that keeps drawing itself, dots circling a hub, a terminal
typing a command and its answer, bars rising and falling, two devices passing packets, a shield with a scan line. They
move only while the card is on screen and at full motion, and hold still otherwise.

- **A platform or product overview** with four to eight features of unequal weight.
- **A landing page** section that needs visual rhythm without screenshots.

## When not to use

- **Features of equal weight**: use `SpotlightGrid`, whose cards are all the same size.
- **A step-by-step explanation**: use `ScrollStory`.
- **Real data**: use the chart and analytics blocks; these drawings are illustrations.

## Usage

```xml
<arc:FeatureBento Title="One platform, every stage">
    <arc:FeatureBentoItem Title="Live metrics" Art="Pulse" Size="Large" Description="Every request lands on the chart within a second." />
    <arc:FeatureBentoItem Title="Integrations" Art="Orbit" Size="Tall" Description="Connect the tools you already use." />
    <arc:FeatureBentoItem Title="Secure by default" Art="Shield" Size="Small" Description="Single sign-on on every plan." />
</arc:FeatureBento>
```

With no items the grid shows six invented cards; set `ShowsSampleItems` to false to leave it empty.

### Sizes and packing

| Size | Six columns | Two columns |
|---|---|---|
| `Small` | 2 × 1 | 1 × 1 |
| `Medium` | 3 × 1 | 1 × 1 |
| `Wide` | 4 × 1 | 2 × 1 |
| `Tall` | 2 × 2 | 1 × 2 |
| `Large` | 4 × 2 | 2 × 2 |

Cards are placed in order, each in the first free place its size fits, so a tall card leaves room beside it for the next
ones. In one column every card is one row.

### Your own cards

<Demo name="CustomItems" />

`Art` picks the drawing (`None` leaves it out). `CommandText` and `ResultText` set the two lines of the `Typing` drawing.
`ItemInvoked` reports a click, a tap, Enter or Space on a card.

## API reference

<!-- api: FeatureBento -->

## Accessibility

- The section is named by its title, a level 2 heading.
- Each card is focusable, named by its title, with the description as help text; the keyboard ring sits 4 outside it.
- The drawings are decorative and hidden from screen readers.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move through the cards in reading order. |
| Arrow keys | Next or previous card. |
| `Home` / `End` | First or last card. |
| `Enter` / `Space` | Raise `ItemInvoked`. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drawings | Move continuously while on screen | Still | Still |
| Hovered card | Border strengthens in 160 ms | Immediate | Immediate |

The drawings run on one shared frame clock, pause when the card scrolls out of view or the window hides, and draw without
allocating.

## Responsive behavior

- Below 860 of the block's own width (`:compact`) the grid has two columns; below 520 (`:narrow`) one column with
  slightly taller rows.
- Rows have a fixed height (240), so drawings keep their proportions; text that needs more room should stay short.

## Related

- `SpotlightGrid` for cards of equal size with an interactive light.
- `ScrollStory` for a scroll-driven walkthrough.
