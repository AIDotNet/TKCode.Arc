---
title: Layout morph
summary: One set of cards in six views, from a grid and a list to a fanned stack, a 3D ring and a helix, with every card flying to its new place.
---

<Demo name="Preview" />

## When to use

`LayoutMorph` shows one collection of cards, such as products, people or projects, and lets the reader choose how to look
at it. The same cards can be a grid, a masonry wall, a list, a fanned stack, a 3D carousel or a helix. When the view
changes, every card flies from where it is to its new place on its own spring, starting near the option that was chosen
and spreading outward, so the reader can follow a card through the change. Pointing at a card lifts it and shows its
figures in the panel under the stage; selecting a card turns it into a detail with a picture, a description, facts and one
action.

- **A shop's collection page or a lookbook.** A grid scans quickly, the list puts the maker and the price beside each
  name, and the carousel shows the pictures large. The detail's button ("Add to bag") keeps its state while the reader
  keeps browsing.
- **A team or speaker page.** The carousel and the helix turn through the portraits one at a time, and the panel shows the
  person in front without any pointing.
- **A portfolio or a case-study index.** The masonry view keeps each picture's own proportions, and the stack makes a
  small set feel like a hand of cards.

## When not to use

- **Long or growing collections.** Every card is a live element that moves on every view change; the block is designed for
  about 10 to 40 cards. Use a virtualized list, `DataGrid` or a paged grid for hundreds of items.
- **Data people compare.** Use a table when readers sort, filter or compare values across many rows; the list view here
  shows one value per row and cannot be sorted.
- **A single fixed view.** If only one view makes sense for your content, use a plain grid or `Carousel` and skip the
  view switch.
- **Navigation.** Cards open an in-place detail, not a page. Use links or a list of cards when choosing an item should
  take the reader somewhere else.

## Usage

### Items

Each `LayoutMorphItem` is a card. `Title` is the name everywhere; `Subtitle` is the line under it in the grid, the
carousel, the floating tag and the panel; `Meta` is the second line of a list row and of the detail; `Value` sits on the
right of a list row and of the carousel caption. An item with an `Image` shows it cropped to fill; an item without one shows
a cover with its title at the top and its value at the bottom, set in a size that follows the card. `AspectRatio` (height
over width) shapes the masonry view.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LayoutMorph Title="Studio shelf" DefaultPanelTitle="Shelf">
        <arc:LayoutMorphItem Key="ink-tray"
                             Title="Ink pen tray"
                             Subtitle="By Yuki Aoyama"
                             Meta="Yuki Aoyama · Blackened steel"
                             Value="$36"
                             AspectRatio="0.9"
                             Description="A shallow tray for pens and a clip or two, folded from one sheet.">
            <arc:LayoutMorphItem.Facts>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="Size" Value="24 × 8 cm" />
            </arc:LayoutMorphItem.Facts>
            <arc:LayoutMorphItem.Stats>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="In stock" Value="31" />
                <arc:LayoutMorphFact Label="Rating" Value="4.8" />
            </arc:LayoutMorphItem.Stats>
        </arc:LayoutMorphItem>
        <arc:LayoutMorphItem Key="yuki-aoyama"
                             Title="Yuki Aoyama"
                             Subtitle="Metalsmith, Osaka"
                             Meta="Metalsmith in Osaka"
                             Value="2 pieces"
                             ActionText="Follow"
                             ActiveActionText="Following" />
    </arc:LayoutMorph>
</UserControl>
```

Give every item a `Key`: when you rebuild the collection, cards whose key stays keep their place, their focus and their state.
Use `ItemsSource` for a collection you own; an `ObservableCollection` is observed, and added or removed cards move the
others to their new places.

### Views

`Layout` is the current view and is two-way, so a control outside the block can drive it. `Layouts` chooses which views the
switch offers and in which order; the arrow keys on a card step through the same list. `SetLayout` changes the view from
code and can name the point the stagger starts from.

- **Grid**: wide containers show eight captioned cards per row; narrower ones show square cards in six, five or four
  columns, whichever gives the largest cards.
- **Masonry**: columns of cards in their own proportions; when the tallest column does not fit, every card shrinks by the
  same amount, so the masonry view never scrolls.
- **List**: one to three columns of rows with a thumbnail, two lines and the value. A list taller than the stage scrolls
  with the wheel or a vertical drag.
- **Stack**: a fan of cards; the card under the pointer slides up out of the fan.
- **Carousel**: large cards round a ring, from the first card to the last. Drag or swipe and it settles on the nearest card;
  a flick turns several cards. It does not wrap round: a drag past the first or last card stretches and springs back. A
  horizontal wheel (or Shift and the wheel) nudges it. Selecting a card beside the front turns it to the front; selecting
  the front card opens it.
- **Helix**: a spiral of ten cards per turn, tilted toward the reader. Dragging spins it and moves along the spiral, so the
  card in front stays in the middle.

The carousel and the helix remember where they were when you come back to them. `FrontItem` is the card in front.

<Demo name="Catalogue" />

### The panel and the detail

The panel under the stage names the card the reader points at, focuses, has in front or has opened, with its three `Stats`;
otherwise it shows `DefaultPanelTitle`, `DefaultPanelSubtitle` and `DefaultStats`. Values such as "$1,240", "4.8" or
"2 days" roll digit by digit; set `NumericValue` when a value's text is not a plain number.

Selecting a card raises `ItemInvoked` (set `Handled` to stop it), runs `OpenCommand` and opens the detail: the card itself
grows into a dialog while the others fade back. `OpenVariant="Hero"` gives a wide card with a large picture for products
with a photo; `Profile` gives a small picture beside the text. The detail's button flips `IsActive` and shows
`ActiveActionText`; it raises `ItemActionInvoked` and runs `ActionCommand`. Set `OpenedItem` to open a card from code and
null to close it, or call `Open` and `Close`.

```csharp
morph.ItemActionInvoked += (_, e) =>
{
    if (e.Item.IsActive)
    {
        bag.Add(e.Item.Key);
    }
    else
    {
        bag.Remove(e.Item.Key);
    }
};
```

<Demo name="Team" />

### Framed or flat

`IsFramed` (the default) draws the stage and the panel as bordered, rounded surfaces, for a block that sits among other
content. Set it to false when the block fills a page or a full-screen view; the cards then float on the page. `Title` and
`Lede` are optional: leave them out to place your own heading. `IsPanelVisible` and `IsHintVisible` hide the panel and
the hint.

<Demo name="Flat" />

### Narrow containers

The block reads its own width, not the window's. Below 720 the view switch shows icons only, stretched across the width,
with the names as tooltips; the hint moves under the switch, the panel puts the name above the statistics and the detail
stacks its picture over the text. Below 520 the cards use tighter spacing and the masonry view uses four columns. The
stage is 600 high, 540 at 720 and below, 500 below 480 and 460 below 360; `StageHeight` sets your own.

<Demo name="Narrow" />

### Texts

Every text has an English default and a property: the view names (`GridLabel` … `HelixLabel`), the hints (`GridHint` …
`HelixHint`), the switch's name (`ViewLabel`), the panel's default lines, and the formats read to screen readers
(`StageLabelFormat`, `LayoutChangedFormat`, `OpenedFormat`, `CloseTextFormat`, `ActionOnFormat`, `ActionOffFormat`).

```xml
<arc:LayoutMorph Title="Ateliers"
                 ViewLabel="Vue de la galerie"
                 GridLabel="Grille"
                 GridHint="Choisissez une carte pour l’ouvrir"
                 DefaultPanelSubtitle="Survolez une carte"
                 CloseTextFormat="Fermer {0}"
                 OpenedFormat="{}{0} ouvert" />
```

### Data shape and performance

- Cards draw each bitmap picture from a copy made at the size it is shown on screen (rounded up in steps of 1.25), so a
  large picture costs the GPU little and does not shimmer when shown small; the open detail draws the picture itself. A
  copy is made once: to change a picture, set `Image` to a new image (a bitmap drawn into again keeps its old copy). Use
  pictures about twice the largest size they are shown at (the hero detail is about 600 by 550) and decode them once; the
  demos draw theirs in code from a seed.
- Cards are self-drawn and move by their transform only, so a view change runs no layout. Each card has its own springs, and
  the stage stops asking for frames as soon as everything is at rest; an idle block costs nothing.
- Keep the collection to a few dozen cards. The carousel shows about five cards at a time; the cards farther along wait out
  of sight on the back of the ring.

## API reference

<!-- api: LayoutMorph -->

<!-- api: LayoutMorphItem -->

<!-- api: LayoutMorphFact -->

## Accessibility

- The view switch is a radio group named by `ViewLabel`. Only the chosen option is a tab stop; the arrow keys (wrapping), Home
  and End choose as they move. On narrow containers each icon keeps its name.
- The stage is a group named from `StageLabelFormat`: the title, the view, the number of cards and how to use the keys. Every
  card is a button named "Title, Subtitle, Value" (or `AutomationName`) and described by its meta, description and facts, so
  the same information as the hidden table of a web page is available card by card.
- Only one card is a tab stop. Up and Down move to the previous or next card in data order, Home and End to the first and the
  last, Enter or Space opens it, Left and Right switch to the previous or next view and keep the focus on the same card. In the
  carousel and the helix the focused card turns to the front, and the card in front is the tab stop; in a scrolling list the
  focused row scrolls into view.
- The open detail is a dialog named by the item's title and described by its description. Focus moves to the close button,
  named "Close" and the title, and Tab stays inside the dialog until it closes. Escape, the close button or a press on the stage
  outside the card closes it, and focus returns to the card.
- View changes, opened cards and the detail's button are announced politely ("Carousel view", "Halo floor lamp opened").
- The floating tag only repeats what the card's name says and is not read. The panel is plain text.

## Motion

- **View changes**: each card moves on its own spring (stiffness 100, damping 20, critically damped, about 1.15 seconds) from
  where it is to where it goes, size, corners and picture included. The card nearest the chosen option starts first, the
  farthest 330 ms later (`StaggerDuration`). While it flies a card lifts toward the reader (0.1 × the distance at its fastest)
  and leans into the motion. A card seen from behind (in the helix, or turning on its way out of the ring) shows only its
  base at 72%. Interrupting a change starts every card again from where it is, without a jump.
- **Captions**: the old caption fades out at once; the new one fades in once the card has nearly arrived, so two layouts never
  overlap. The hint and the panel's lines rise in over 240 ms; the panel's numbers roll.
- **Pointing**: the card lifts with a shadow on a quick spring (stiffness 500, damping 49); in the stack it slides out of the
  fan, in the helix it moves forward. In the masonry, stack and helix views a tag with the name floats above the card.
- **Carousel and helix**: the ring follows the pointer one to one and, on release, settles on the card the flick points at
  (stiffness 110, damping 19.5), keeping the release speed. Past the first or last card the drag meets rubber-band
  resistance and springs back. The helix tilts toward the reader and travels along the spiral as it turns.
- **Opening**: the card grows into the detail (stiffness 130, damping 26) while the stage recedes and the other cards fade to
  12%. The text fades in near the end, and the buttons 260 ms after the start.
- Reduced motion places every card at once and keeps only short fades (the detail's buttons, the tag). Off applies every
  change at once.

## Related

- `Carousel` for a single row of slides with pagination.
- `CardStack` for a swipeable pile of cards.
- `SegmentedControl` for a view switch outside the block.
- `RollingDigits` for the panel's rolling numbers.
- `ExplodedView` and `CustomerGalaxy`, the other 3D showpieces.
