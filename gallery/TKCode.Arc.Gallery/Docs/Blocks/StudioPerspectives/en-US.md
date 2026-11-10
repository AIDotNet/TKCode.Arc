---
title: Studio perspectives
summary: A testimonial stage that shows one large quote at a time with the person and their company, moving on by itself with a blur-and-rise change, progress bars, arrows and a pause button.
---

<Demo name="Preview" />

## When to use

`StudioPerspectives` is a testimonial stage: instead of a wall of small quotes it gives one quote the whole width, set
large in the display face, with the person, their role and their company under it. After a few seconds the old quote
blurs away as the next one rises in, and the person changes a beat later. A thin bar per quote shows where the stage is
and how long until it moves on; arrows and a pause button let the reader take over. Pointing at the stage or moving
focus into it pauses the timer, so nobody loses a quote while reading it.

- **A landing page** section of social proof between features and pricing.
- **A case study page** that cycles through what several customers said.
- **A pitch or event page** with a handful of strong quotes.

## When not to use

- **Many short reviews** that should be scanned together: use a grid of cards.
- **Ratings and star scores**: this block has no rating.
- **The people of your own team**: use `TeamShowcase`.

## Usage

```xml
<arc:StudioPerspectives Title="What studios say" Interval="0:0:8">
    <arc:StudioPerspectivesQuote Text="We plan from one board now." Name="Amara Lindqvist" Role="Creative Director" Company="Fieldnote Studio" />
    <arc:StudioPerspectivesQuote Text="Nothing gets in the way." Name="Diego Ferraz" Role="Lead Engineer" Company="Harbor and Pine" />
</arc:StudioPerspectives>
```

With no quotes the stage shows four invented ones; set `ShowsSampleQuotes` to false to leave it empty. A quote's
`Image` replaces the generated portrait.

### Your own quotes

<Demo name="CustomQuotes" />

`SelectedIndex` is two-way. `Next()`, `Previous()`, `Pause()` and `Play()` drive the stage from code; `IsPaused` reports
the pause button. `IsAutoAdvance` turns the timer off and `Interval` sets how long each quote stays (7 seconds).
`QuoteChanged` reports every change and whether the timer made it.

## API reference

<!-- api: StudioPerspectives -->

## Accessibility

- The stage is named by its title, a level 2 heading.
- The previous, pause and next buttons have the names `PreviousLabel`, `PauseLabel` or `PlayLabel`, and `NextLabel`.
- A change made by the reader is announced ("Quote 2 of 4, Diego Ferraz, Lead Engineer · Harbor and Pine"); changes
  made by the timer are not, so a screen reader is not interrupted.
- The timer stops while the pointer is over the stage, while focus is inside it and after the pause button, which meets
  the need to pause moving content.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move to the buttons. |
| `Left` / `Right` | Previous or next quote while focus is in the stage. |
| `Home` / `End` | First or last quote. |
| `Enter` / `Space` on a button | Press it. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Quote changes | Old quote blurs out, new one rises 4 px and sharpens; the height follows on a spring | Cross-fade 120 ms | Immediate |
| Person changes | Lifts in 80 ms after the quote | Cross-fade | Immediate |
| Progress bar | Fills over `Interval` | Fills | Fills |

## Responsive behavior

- Below 640 of the block's own width (`:compact`) the quote steps down from 36 to 22 and the paddings tighten.
- The stage is at most 880 wide; long quotes wrap and the stage grows to fit them on a spring.
- With a single quote the buttons and the progress bars are hidden.

## Related

- `TeamShowcase` for your own team.
- `Carousel` for a general slide show of any content.
- `HeroSignup` for the section that usually opens the page.
