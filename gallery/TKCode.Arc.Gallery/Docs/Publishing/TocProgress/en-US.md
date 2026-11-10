---
title: Table of contents progress
summary: A table of contents for a long article that highlights the section being read, fills a rail down the list as you scroll and jumps to any section; on narrow screens it folds into a progress button.
---

<Demo name="Preview" />

## When to use

`TocProgress` sits beside a long article, a guide or a policy page and follows the reader. The section being read is in
the full text color, the sections above it read as done and the ones below stay muted. Along the left edge of the list
a thin rail fills downward: it reaches the current section's row and keeps growing through that row as the reader moves
through the section, so the list doubles as a progress bar. The percentage of the article read sits beside the heading.
Choosing a section scrolls the article there.

- **Documentation and guides**: people jump between sections and want to know where they are.
- **Long reads**: essays, reports and changelogs where the remaining length matters.
- **Policies and terms**: readers look for one clause and need to see the structure.

## When not to use

- **Short pages** that fit on one or two screens: the list adds nothing.
- **Steps the reader must do in order**: use `Stepper`, which shows completion, not position.
- **Navigation between pages**: use `Tabs` or a side navigation; this list moves inside one page.
- **Feeds without sections**: show a plain `Progress` bar, or nothing.

## Usage

`TocProgress` lives in the Special package: register it with `UseArcSpecial()` next to `UseArc()`. Point `Target` at the
article's `ScrollViewer` and mark the headings inside it with `TocProgress.Heading` (and `TocProgress.Level` 2 or 3 for
sub-sections). The list collects the marked elements in document order.

```xml
<Grid ColumnDefinitions="*,Auto" ColumnSpacing="32" Height="400">
    <ScrollViewer x:Name="Article">
        <StackPanel Spacing="12">
            <TextBlock Text="Install" arc:TocProgress.Heading="Install" FontSize="22" />
            <TextBlock Text="Add the package to your project." TextWrapping="Wrap" />
            <TextBlock Text="Options" arc:TocProgress.Heading="Options" arc:TocProgress.Level="2" FontSize="18" />
            <TextBlock Text="Every option has a sensible default." TextWrapping="Wrap" />
        </StackPanel>
    </ScrollViewer>
    <arc:TocProgress Grid.Column="1" Width="220" Target="{Binding #Article}" />
</Grid>
```

A heading counts as reached once it passes the reading line, a quarter of the viewport below its top
(`ActivationRatio`). When the article cannot scroll any further, the last section is active even if its heading never
reached the line. `ActiveIndex`, `Progress` and the `ActiveChanged` event report the reader's place, for example to
update the address bar.

### Sections listed by hand

Add `TocProgressEntry` children when the list's words should differ from the headings, or the headings are built in
code. Each entry names its `Title`, its `Level` and the `Target` element it scrolls to.

<Demo name="Entries" />

### Compact

`DisplayMode` Compact shows one pill button with a progress ring and the current section's title; it opens the list in
a popup. The default, Auto, switches to it when the viewport is narrower than `ArcViewport.CompactWidth` (640px).

<Demo name="Compact" />

### Reduced motion

Under reduced motion the rail and the article jump to their new places; the highlight, the percentage and the compact
title change at once.

<Demo name="ReducedMotion" />

### Anatomy

1. **Heading**: "On this page" in small medium text and the percentage read in the mono font.
2. **Rail**: a 2px track in the border color in a 20px gutter, the accent fill, a brighter segment beside the
   active row and an 8px dot at the fill's end, ringed with the surface color.
3. **Rows**: the section titles, up to two lines, 14px further in per level: muted when upcoming, secondary once read,
   full color and medium weight while active.
4. **Compact button**: a 40px pill with a 20px ring, the current title and a chevron.

### Content guidelines

- Keep titles short; the list wraps at two lines and ends with an ellipsis.
- Use two levels at most in the list even if the article has more; deeper levels crowd the rail.
- Leave enough room under the last section (padding at the end of the article) so its heading can reach the reading line.

## API reference

<!-- api: TocProgress -->

<!-- api: TocProgressEntry -->

<!-- api: TocProgressLabels -->

<!-- api: TocProgressPosition -->

<!-- api: TocProgressActiveChangedEventArgs -->

## Accessibility

- The control is a navigation landmark named "On this page" (`Labels.Title`). The list is a list of links: each row is
  named by its title; the active row's help text says "current section" and read rows say "read". Progress is shown by
  the rail, the text colors and the percentage, never by color alone.
- Only one row is in the tab order (the active one, or the last one focused).
- The compact button is named with the current section and the percentage, "Contents, Feeding every day, 42% read".
- Keyboard focus shows the 2px focus ring; pointer focus shows none.

### Keyboard

| Key | Action |
|---|---|
| Tab | Moves into the list (to the active row) and out of it |
| Up / Down | Moves to the previous / next row |
| Home / End | Moves to the first / last row |
| Enter / Space | Scrolls the article to the section |
| Escape | Closes the compact popup and returns to its button |

## Motion

- The fill's end follows the reading position on a spring without overshoot that settles in about 0.3 s; it keeps its
  velocity when the reader changes direction, so fast scrolling never stutters. The active segment moves to the new row
  on the snappy spring.
- Choosing a section scrolls the article on the smooth spring; the chosen row stays highlighted for the whole trip, and
  any wheel or scroll bar input takes over at once.
- The compact title swaps with Arc's label swap (out 100 ms, in 160 ms rising 2px).
- Reduced motion and motion off jump to the end state.

## Responsive behavior

- The list takes the width it is given; titles wrap to two lines and then end with an ellipsis.
- In Auto mode the list becomes the compact button below `ArcViewport.CompactWidth` (640px), and back when there is
  room again; the popup is 300px wide and stays inside the viewport.
- Rows are at least 30px high, and the compact button is 40px, so both work for touch.

## Related

- `Progress`: a plain progress bar.
- `Stepper`: progress through steps the user completes in order.
- `ScrollArea`: a scroll container with Arc scroll bars to hold the article.
- `Tabs`: switches between views instead of moving within one.
