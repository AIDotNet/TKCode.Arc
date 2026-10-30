---
title: Page curl
summary: A small book that opens on its cover, with pages that fold along a line that follows the pointer.
---

<Demo name="Preview" />

## When to use

`PageCurl` is a small book or magazine. It opens on its cover; tap the right page to go forward and the left page to go
back, or drag a corner and watch the page fold over along a line that follows your hand, showing its back, a lit curl
and a shadow on the page below. A page count with previous and next buttons sits underneath.

- **A printed piece brought online**: an issue, a brochure or a programme that people expect to leaf through.
- **A few spreads, each worth a look**: a seasonal menu, a short catalogue, a class book.
- **Pictures in a fixed order**: a portfolio or an annual report told in eight to twenty pages.

## When not to use

- **Anything long or searchable**: turning forty pages one at a time is tiring; use a scrolling page or `Carousel`.
- **Small print people must read closely**: pages are scaled into slots and cannot be zoomed or selected.
- **A shelf of books on one screen**: one book draws the eye; many compete for it.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:PageCurl Label="Field notes" Width="520">
        <Border Background="#2B2622" />
        <TextBlock Text="Page two" Margin="24" />
        <TextBlock Text="Page three" Margin="24" />
    </arc:PageCurl>
</UserControl>
```

`PageCurl` is a templated control in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. The pages are the children, in reading order. Page one is the cover and sits alone on the right;
spread `n` shows pages `2n` and `2n + 1`. A book with an even number of pages ends on a back cover alone on the left,
and the closed book slides to the middle on its cover and its back cover.

### Sizing

Page size derives from the control's width: the width holds two pages side by side, so a 288px wide book still shows
two readable pages. `PageAspect` is a page's width divided by its height (0.72 by default). Each page renders into a
slot of that size, so make pages simple and fixed in size. A `Viewbox` around a page of a fixed design size makes it
scale with the book:

```xml
<Viewbox>
    <Panel Width="288" Height="400" />
</Viewbox>
```

### Pages from data

Items that are not controls are built with `PageTemplate`. A control is used as the page itself, so it can be on one
page only.

<Demo name="Template" />

`Spread` is the spread on show (0 is the closed cover) and binds two-way. Setting it from code turns one step with the
curl when the new spread is next to the current one, and jumps otherwise. `SpreadChanged` is raised when a page
finishes turning by a tap, a drag, a key or a button, with the new spread. `NextSpread()` and `PreviousSpread()` turn
from code.

### Tease, hint and controls

On first view the cover corner curls up and settles next to a "Drag the corner" hint. `Tease` turns the curl off, and
`Hint` replaces the text or hides it when it is `null`; the hint fades out for good after the first drag, tap, key or
button press. Until the first turn, moving the mouse into the book repeats the tease at half the size, at most every
two and a half seconds. `ShowControls` hides the page count row.

<Demo name="Compact" />

### Best practices

**Do**

- Make page one a real cover with a title, so the closed book reads as something to open.
- Keep pages simple and fixed in size, with a few spreads.
- Give the book a name with `Label`.

**Avoid**

- Pages that need scrolling or text selection: the page is a drag target.
- Interactive controls on pages: taps and drags turn the page.

### Common questions

**Why does a drag on a phone not scroll the page?** A vertical swipe does scroll the page behind the book; only a
horizontal drag turns a page.

**Can a page be the same control twice?** No. A control has one parent, so a page shown in two places needs a
`PageTemplate` and a data item.

## API reference

<!-- api: PageCurl -->

## Accessibility

- The book is a focusable group named by `Label`, announced as a book. A polite status reads the visible pages, such as
  "Page 2 and 3 of 8".
- Only the resting pages are in the accessibility tree; the pages that are turning are hidden from it.
- Previous and next are real buttons with labels, and they disable at the covers. The visible count and the hint are
  decorative duplicates of the status.
- The keyboard focus ring shows only for keyboard focus.

### Keyboard

| Key | Action |
|---|---|
| Right arrow, Page Down | Turn to the next spread |
| Left arrow, Page Up | Turn to the previous spread |
| Tab | Move from the book to the previous and next page buttons |

## Motion

- The fold line is the perpendicular bisector between the corner and the pointer, so the page follows the finger from
  where it was grabbed. The corner is held within a page width of the spine, like paper bound on one side.
- The flap is the sheet mirrored across the fold, showing the back of the page with a lit curl at the crease; the page
  below gets a soft shadow at the fold and the flap casts a light shadow that always falls downward.
- On release, past the middle or a flick turns the page on a spring that keeps the release velocity; otherwise it lays
  back down. Grabbing a page that is still settling catches it where it is.
- The first time the book is mostly in view, the cover corner lifts about a quarter of a page on a soft spring, holds,
  and lays back down. Hovering a corner lifts it a little, and moving to another corner lays the first one flat before
  lifting the next. Hovering a page shows a chevron at its outer edge for the direction a tap turns.
- Taps, arrow keys and the buttons turn along a lifted arc.
- Only the pages involved in a turn are drawn, and a turn moves them with transforms and clips: nothing is laid out
  per frame.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Turning | Curls and follows the pointer | Swaps spreads | Swaps spreads |
| Tease and hover lift | Run | Skipped | Skipped |

## Related

- `Carousel` browses a row of slides by dragging or arrowing through them.
- `CardStack` fans a deck of cards you can flick away.

| Component | Choose it when |
|---|---|
| `PageCurl` | A short sequence should feel like paper |
| `Carousel` | You have a row of slides of any length |
| `CardStack` | Items are a deck rather than a sequence |
