---
title: Card to article
summary: Story cards that grow into full articles in one continuous morph and shrink back into their place when you pull them down.
---

<Demo name="Preview" />

## When to use

`CardToArticle` is an editorial grid whose cards open into the stories they announce. Each card is a picture with a tag,
a headline and a short meta line drawn over it. Pressing a card does not open a new page or a separate dialog: the card
itself grows into a centred article sheet. Its frame and corners stretch on one spring, the picture scales as a whole
into the article's hero (the headline grows with it and never reflows), the text rises in underneath and the close
button pops in last. Closing reverses the same path and lands the sheet exactly on the card's place.

- **Magazines, blogs and newsletters** that show a handful of stories and want reading to feel like a continuation of
  browsing, not a page load.
- **Guides and feature stories** in an app, where the reader glances at a card, reads, and comes back to the same
  spot in the grid.
- **Touch-first layouts**: the article is dismissed by pulling it down, the way people close a sheet on a phone.
- **Short to medium texts**: a byline, a few paragraphs and a pull quote fit the article's single scrolling column.

## When not to use

- **Long lists or feeds** with dozens of entries: the grid has no paging or virtualization. Use a list with a regular
  detail page.
- **Cards that are not pictures**: the morph relies on a picture filling the card and the hero at the same aspect ratio.
  For text cards with a quick look, use `Card` with its details.
- **Documents that need a full page**: tables, code and long navigation belong on their own route.
- **Forms or editing**: the article is for reading; put input in a `Dialog` or a `BottomSheet`.

## Usage

The preview shows four invented stories from `CardToArticleSamples.Stories()`. Set `Items` and the control does the rest:
the grid, the morph, the article, focus, Escape and the pull gesture.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CardToArticle x:Name="Stories" />
</UserControl>
```

```csharp
Stories.Items = CardToArticleSamples.Stories();
```

`CardToArticle` derives from Avalonia's `TemplatedControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`.

### Opening and closing from code

`OpenItemId` holds the id of the open article and binds two-way: pressing a card writes the card's id, closing writes
null, and setting it opens or closes with the same morph. `Open(id)` and `Close()` do the same from code.
`OpenItemChanged` reports every change with the old id, the new id and its `Reason` (`Click`, `Keyboard`, `CloseButton`,
`Escape`, `Drag`, `Wheel` or `Programmatic`). The demo below opens and closes from two buttons and logs each event.

<Demo name="EventLog" />

An article that is open before the control is first shown simply appears, without growing out of a card nobody has
seen. Setting `OpenItemId` to another id while an article is open swaps the story in place; an id that no item has
opens nothing.

### Anatomy

- **Grid**: two columns 16 px apart while the control is wider than `ColumnBreakpoint` (520 by default), one column
  12 px apart below. Every card is a column wide at `AspectRatio` (4:3 by default). The control is at most 680 px wide
  and centres itself.
- **Card**: a button that is the picture, with 26 px corners. The tag pill sits at the top-left; the headline and the
  meta line sit at the bottom-left over a scrim that darkens the bottom 60% of the picture. Every size inside the
  picture is a fraction of its width (tag text 3.2%, headline 7.4%, meta 3.4% capped at 20 px), which is why the card
  and the hero are the same drawing at two scales.
- **Article sheet**: on the window's overlay layer, at most 680 px wide, 16 px from the sides and 24 px from the top and
  bottom of the window (8 and 12 px in windows at most 520 px wide). It always fills that height, whatever the length of
  the text.
- **Hero**: the card's picture at the sheet's width; it scrolls away with the text.
- **Text**: the byline (avatar, author, date), paragraphs and pull quotes, with side padding of 6% of the window width
  between 20 and 48 px.
- **Reading progress**: a 3 px accent bar along the top edge that grows with the scroll position.
- **Close button**: a 36 px frosted circle 14 px inside the top-right corner; it does not scroll.
- **Backdrop**: the page background at 55% over the page, blurred.

### Writing the article

`CardToArticleItem.Body` takes plain text for the common case. A blank line starts a new paragraph and a paragraph that
begins with "> " becomes a pull quote with an accent rule. Set `Author`, `Published` and optionally `Avatar` for the
byline; without an avatar the author's initials sit on a colored circle. A `Body` that is a control is shown as it is,
and any other object goes through `BodyTemplate`.

<Demo name="OwnStories" />

The demo above uses its own items at 3:2 and rewords the close button and the card names through `Labels`.

### Pulling to close

The open article follows a downward pull: it moves down by 0.3 of the distance and shrinks by one part in 1500 per
pixel, around a point halfway across and 30% down the window, up to a pull of 480 px. Letting go past 120 px, or
flicking faster than 600 px/s, closes it; anything shorter springs back.

- **Mouse and pen**: drag the picture. A press on the text selects text instead.
- **Touch**: drag anywhere once the article is scrolled to its top. A drag that starts lower first scrolls the text
  and turns into a pull, in the same movement, when the top is reached.
- **Wheel and touchpad**: scrolling up at the top pulls by 0.6 of the scrolled distance and closes as soon as the pull
  passes 120 px; when the scrolling stops for 150 ms the article springs back.

### Narrow containers

The grid follows the control's own width, not the window's: in a 360 px column it shows one card per row. The article
always sizes itself to the window.

<Demo name="Narrow" />

### Reduced motion

<Demo name="ReducedMotion" />

With reduced motion the cards do not lift on hover, the article is in place at once and its text, close button and
backdrop only fade in; closing removes it at once. Pulling still follows the pointer, because the movement is the
reader's own.

### Content guidelines

- Keep tags to one or two words ("Recipe", "Field guide") and headlines short enough for two lines on a card; the headline
  wraps in balanced lines, so a two-line title reads as one shape.
- Use the meta line for one fact a reader decides by, such as a reading time.
- Pick pictures whose bottom 40% is calm: the headline sits there over the scrim.
- Write `ImageAlt` for the article's hero; the card itself is named by its headline and meta line.
- Keep the body to a few paragraphs and at most one or two pull quotes.

### Best practices

- Give every item a unique, stable `Id`: it is what `OpenItemId` stores and what a route or a deep link can restore.
- Use an observable list (`AvaloniaList` or `ObservableCollection`) for `Items` when stories arrive or leave while the
  page is open; removing the open story closes its article.
- Let the control own its width up to 680 px; a wider page can show several instances side by side.
- Keep the page under the article still while it is open: the blurred backdrop is taken from the page as it was.

### Common questions

**Can the article be wider than 680 px?** No; the measure of the text is part of the design. Place the control in a
wider layout instead.

**Does the morph work after the window was resized?** Yes. Closing measures the card's slot again at that moment, so the
sheet lands where the card now is, even when the grid has switched to one column.

**What happens when a card is pressed while another article is closing?** The closing article hands its card back at
once and the new one grows from the pressed card. Pressing the same card again while it is closing turns the morph
around with its current speed.

## API reference

<!-- api: CardToArticle -->

<!-- api: CardToArticleItem -->

<!-- api: CardToArticleLabels -->

<!-- api: CardToArticleOpenChangedEventArgs -->

<!-- api: CardToArticleChangeReason -->

<!-- api: CardToArticleSamples -->

## Accessibility

- Every card is a button named by its headline and meta line (`Labels.CardName`). It reports whether its article is
  expanded, and its picture is not announced separately.
- The open article is a modal dialog named by the story's headline (`Labels.ArticleName`). The hero's text alternative
  is `ImageAlt`.
- Focus moves to the close button when the article opens and stays inside the article while it is open. Closing returns
  focus to the card that opened it, with the keyboard focus ring when the keyboard was used.
- The close button is named "Close article" by default (`Labels.Close`).
- The reading progress bar and the scrim are decoration and are not announced.
- Keyboard focus rings are 2 px and show only for keyboard focus.

### Keyboard

| Key | Where | Action |
|---|---|---|
| `Tab` / `Shift+Tab` | Grid | Moves between the cards. |
| `Enter` / `Space` | Card | Opens its article. |
| `Tab` / `Shift+Tab` | Article | Stays on the close button. |
| `Up` / `Down` | Article | Scrolls the text by 40 px. |
| `PageUp` / `PageDown` | Article | Scrolls the text by most of a screen. |
| `Home` / `End` | Article | Scrolls to the top or the end. |
| `Enter` / `Space` | Close button | Closes the article. |
| `Escape` | Article | Closes the article. |

## Motion

| Moment | What moves | How |
|---|---|---|
| Hover a card | Card scales to 1.012 with a raised shadow | 240 ms, standard curve |
| Press a card | Card settles to 0.985 | 240 ms, standard curve |
| Open | Sheet and picture grow from the card | Critically damped spring, 0.52 s |
| Open | Backdrop fades in | 350 ms, enter curve |
| Open | Text rises 16 px and fades in | 360 ms, standard curve, after 135 ms |
| Open | Close button grows from 0.8 and fades in | Critically damped spring, 0.30 s, after 175 ms |
| Pull | Article moves down and shrinks | Follows the pointer exactly |
| Short pull released | Article springs back | Critically damped spring, 0.38 s |
| Close | Sheet and picture fly back onto the card | Critically damped spring, 0.44 s |
| Close | Text and close button fade out | 120 ms |
| Close | Backdrop fades out | 290 ms, enter curve |

Closing during the opening turns the same spring around with its current velocity, so the sheet keeps moving a little
before it heads home. While the sheet stretches, the corners stay round and the text and the close button keep their
size: only the sheet's edges clip them.

## Responsive behavior

- The story grid follows the control's own width: above `ColumnBreakpoint` (520 px) it has two columns, at 520 px or
  less one, and it is at most 680 px wide.
- The open article follows the viewport (the nearest viewport host, otherwise the window): it is a centered sheet up to
  680 px wide, and at 520 px or less it fills the screen edge to edge with smaller side padding.
- Long articles scroll inside the sheet; the scroll bar floats over the text and hides when you stop scrolling.
- On touch, pull the article down from its top to send it back to its card; the close button and Escape do the same, so
  nothing depends on the gesture.

## Related

- `Card`: a content card whose details open in a quick look that grows out of it.
- `BottomSheet`: a sheet that slides up from the bottom and closes by dragging it down.
- `Dialog`: a modal dialog for decisions and forms.
