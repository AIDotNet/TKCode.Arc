---
title: In-view title
summary: Bring a section title in as it scrolls into view.
---

<Demo name="Variants" />

## When to use

An in-view title is a section heading on a long page. It stays hidden until 45% of it is inside the viewport, then
it reveals with one of five styles. Each section announces itself as the reader reaches it, so a long page gets a
paced entrance without a separate animation timeline for every heading. It fits pages that people read from top to
bottom: a feature overview, a product tour, an annual report, a security or compliance page.

- **A billing product overview**: "Invoices and payment terms", "Tax and VAT" and "Failed payments" as section
  headings down a long landing page. Each heading reveals when the reader reaches it, so the page feels paced
  without any motion work in the app.
- **A customer success review**: a quarterly report whose chapters open with a title. The default `Blur` style suits
  editorial headings: the reader sees a calm entrance, not a burst of motion.
- **A help center or onboarding guide**: section titles that should play again with `IsReplayEnabled` when the reader
  scrolls back up to a step, so the heading they returned to is visibly there.
- **A pricing or plan comparison with fixed breaks**: a two-line headline such as "Pay for the seats you use." and
  "Add more when your team grows." The `Line` variant with `Lines` keeps those breaks at every width.

## When not to use

- **The first headline of a page that appears on load**: use `TextReveal`. An in-view title waits for the reader to
  scroll, so a hero heading at the top of the page would start only after the reader has already seen it empty.
- **A status line that keeps working, such as "Generating invoice summary"**: use `TextShimmer`. It loops while the
  work continues; an in-view title plays once and then rests.
- **Body text, labels, table headers or long paragraphs**: use a regular `TextBlock`. The per-word reveal is built
  for headings, and a paragraph that reveals word by word is slow to read.
- **A value that changes in place, such as a status word or a plan name**: use `TextMorph`, which moves the letters
  from the old label to the new one. An in-view title does not animate changes to `Text`.
- **A heading that opens and closes content**: use `Accordion`, which gives the heading a toggle and keeps the
  content state with it.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:InViewTitle Text="Blur sharpens word by word"
                     Variant="Blur" />
</UserControl>
```

`InViewTitle` derives from Avalonia's `Control` and draws the whole title itself. It does not create a child control
per word or per line, so a page with forty section titles holds forty controls, not thousands. It measures to the
size of its text, so it sits in a `StackPanel` or a `Grid` like any heading. It names itself by its `Text` and sets
the heading level from `Level`, so screen readers see a real heading in the page outline.

The reveal starts when 45% of the title is visible. A title that is visible when the page opens starts at once; a
title below the fold waits until the reader scrolls to it. The default variant is `Blur`.

### Anatomy

1. **Title text** (`Text`): the whole title, drawn as one run of words, lines or letters depending on the variant.
   Its text is also the accessible name.
2. **Heading level** (`Level`): sets the font size and the heading level. `H1` uses the 4XL display size, `H2`
   (default) the 3XL size, `H3` the 2XL size, and `Paragraph` the XL size. All four levels use the display font
   family, medium weight and display tracking from the theme.
3. **Line box** (`Leading`): the line height is 1.1 times the font size, from the title token. A two-line title is
   therefore about 2.2 font sizes tall.
4. **Foreground** (`Foreground`): the title color. The theme sets it from the foreground token; when no brush is set
   the title is painted black.
5. **Explicit lines** (`Lines`): an optional list of lines for the `Line` variant. Without it, the whole `Text` is
   treated as one line.
6. **Starting blur** (`BlurRadius` and `SoftBlurRadius`): the blur each word starts from in the `Blur` variant
   (`BlurRadius`) and in the `Tracking` variant (`SoftBlurRadius`). The theme sets both from the blur tokens.

### Choosing a variant

`Variant` selects the reveal style. The opacity rises first, the blur clears next where the style has one, and the
movement settles last, so the title is readable before it fully arrives. Use one variant for most headings on a page.

| Variant | What moves | Spacing between units | Settles in | Use it for |
|---|---|---|---|---|
| `Blur` (default) | Each word sharpens from a blur and lifts 0.2em | 60 ms, shortened for long titles | 0.8 s | Most section headings |
| `Word` | Each word rises 0.9em out of its own clip | 60 ms, shortened for long titles | 0.8 s | Headings where a clipped rise suits the layout |
| `Line` | Each line rises out of its clip | 80 ms per line | 0.86 s | Headlines with fixed breaks (needs `Lines`) |
| `Tracking` | Tight letter spacing opens up while each word clears a soft blur | 32 ms per letter | 0.9 s | Short, large openers such as a one-line hero section |
| `Wipe` | A soft-edged mask uncovers the title from left to right | None: one unit | 0.9 s | A single short phrase used as a divider |

```xml
<arc:InViewTitle Text="Words rise out of a clip" Variant="Word" />
<arc:InViewTitle Text="A soft wipe uncovers the title" Variant="Wipe" />
```

The word stagger is 60 ms per word, but it is shortened when a title has many words, so the whole stagger stays
within 0.4 s. A ten-word heading therefore starts each word 40 ms after the last. Choose `Blur` or `Word` for long
headings: the stagger stays short and the title is complete quickly.

Use `Tracking` only for short titles. The letters open across the full width of the title, so a long title looks
stretched while it moves. Use `Wipe` sparingly: its mask reads as one gesture across the whole heading, which is
effective once per page and tiring when every section does it.

### Heading level

`Level` sets the heading level (`H1`, `H2` or `H3`) and the matching `AutomationProperties.HeadingLevel`. The default
is `H2`. Choose the level from the page outline, not from the size you want. A page usually has one `H1` for its
title, `H2` for its sections and `H3` for the subsections inside a section. `Paragraph` sets no heading level, so
use it only for a short lead-in that is not part of the outline.

### Explicit lines

`Lines` sets the lines for the `Line` variant. The lines are shown as given and do not reflow. Each line must fit on
a narrow screen, so check the longest line at the smallest width the page supports. Without `Lines`, the whole `Text`
is one line.

```csharp
title.Variant = InViewTitleVariant.Line;
title.Lines = ["Lines rise one by one,", "without reflowing"];
```

Set `Lines` when the break carries meaning, such as a headline that pairs a claim with its qualifier. For copy
that should wrap naturally, use `Word` or `Blur` with `Text` alone.

### Replay on every entry

`IsReplayEnabled` hides the title when it leaves the viewport, with a quick fade and no stagger. It replays each
time the title enters again. The default is `false`, so the title plays once and then stays visible.

```xml
<arc:InViewTitle Text="This one replays on every entry"
                 IsReplayEnabled="True"
                 Level="H3" />
```

Use replay for a heading that the reader scrolls back to, such as a step in a long setup guide that the reader
returns to from a later step. Keep it off for the other headings on the page, because a heading that fades out and
rises again on every scroll competes with the content the reader is reading.

### Driving it from data

`InViewTitle` is display-only: the text comes from the view model, and the reveal runs by itself. Bind `Text` and
keep the heading level and variant in XAML.

```csharp
public partial class BillingOverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string _invoicesTitle = "Invoices and payment terms";

    [ObservableProperty]
    private string _failuresTitle = "Failed payments";
}
```

```xml
<StackPanel Spacing="48">
    <arc:InViewTitle Text="{Binding InvoicesTitle}" Variant="Blur" />
    <arc:InViewTitle Text="{Binding FailuresTitle}" Variant="Blur" Level="H3" />
</StackPanel>
```

Changing `Text` updates the words, but it does not play the reveal again. The entrance belongs to the moment the title
enters the viewport. Set the copy once per page. A title whose words change with the selection in the page, such as
the name of the open invoice, is better served by a plain heading.

### Using motion with restraint

Section titles are where this effect earns its place: the reader is already scrolling, and the heading marks the
start of a new idea. The motion is short (under a second) and it finishes before the reader reads the paragraph
beneath it.

It distracts when a page has many differently animated titles, when a heading replays on every scroll in a long list
of settings, or when a title sits inside a dense dashboard where people scan numbers. For those pages, leave the title
at the default `Blur`, keep `IsReplayEnabled` off and let the content carry the page.

Reduced motion removes all of it. With the motion level set to `Reduced` or `Off`, the title shows its final state
at once, with no movement, blur or mask. Set the level on a window or a container with `arc:ArcMotion.Level`, which
the title inherits.

### Example: billing product feature page

A feature page for a billing product has a page title in plain text and two sections below it: how invoices are
issued, and how failed payments are retried. Both section headings use `Blur` at `H2`, so the page has one rhythm
from the first section to the last. The paragraph under each heading is ordinary text, so the reader can read it as
soon as the heading settles. Each heading is a real `H2`, which keeps the outline correct for a screen reader.

![Two section titles on a billing feature page with paragraphs beneath](images/InViewTitleFeature.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Billing automation"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:InViewTitle Text="Invoices and payment terms" Variant="Blur" />
    <TextBlock Text="Acme Analytics issues invoices on the first business day of each month and sends the net 30 terms with every one."
               TextWrapping="Wrap"
               FontSize="15"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:InViewTitle Text="Failed payment retries" Variant="Blur" />
    <TextBlock Text="A declined card is retried on days 3, 7 and 14. The account owner receives a notice before the last attempt."
               TextWrapping="Wrap"
               FontSize="15"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### Content guidelines

Section titles name the topic and stay short. Write them as noun phrases in sentence case, without a final period,
and keep them to one line at the width of the page where possible. A title is a label for the section, so the
paragraph under it carries the explanation.

| Instead of | Write |
|---|---|
| "INVOICES & PAYMENTS" | "Invoices and payment terms" |
| "Here is how we handle the invoices you send." | "Invoices" |
| "Ready to get started" with an exclamation mark | "Start your first invoice" |
| "Failed payment processing system overview" | "Failed payments" |
| "Learn more about VAT." | "Tax and VAT" |

### Best practices

**Do**

- Use one variant for most headings on a page. Blur is the default for a reason: it reads as calm on every section.
- Set `Level` from the page outline: one `H1`, then `H2` for sections and `H3` for subsections. Screen readers and
  the outline both depend on it.
- Use `Lines` only when the breaks are part of the message, and check every line at a 360 px wide screen before you ship.
- Keep titles short. A title that wraps to four lines delays the paragraph under it and weakens the rhythm.
- Keep the copy fixed for the life of the page. Changing it does not replay the reveal, so the change is not announced.

**Avoid**

- Mixing `Word`, `Line`, `Tracking` and `Wipe` on one page. Each variant has its own timing, and the mix reads as
  noise rather than as structure.
- `IsReplayEnabled` on every heading. Replay is for a heading the reader returns to; on every heading it competes
  with the content.
- `Tracking` on long titles. The letters open across the full width, so a long heading looks stretched while it moves.
- Titles that must be read at once, such as a warning above a form. The title is hidden until 45% of it is in view,
  so a warning should be an `Alert` or a plain text row.
- Using the title as a status line or a counter. Use `TextShimmer` or `TextMorph` for copy that changes state.

### Common questions

**Why does my title not appear when I scroll to it?** The reveal starts when 45% of the title is inside a scrolling
viewport. A title that is never shown, for example one inside a tab that is not selected, does not reveal until it is
shown and then scrolled into view.

**A title near the top of a long page is already visible when the page opens. What happens?** A title that is visible
when it is attached starts its reveal at once. A title that the reader has already scrolled past shows its final state
without motion, so a reader who opens the page halfway down does not see headings play in behind them.

**Does a title play again when the reader scrolls back up?** Only with `IsReplayEnabled="True"`. The default plays
once. With replay on, the title fades out quickly when it leaves the viewport and plays the full reveal again when
it enters.

**How do I change the font of one title?** Set `FontFamily`, `FontSize`, `FontWeight` or `Foreground` on the control.
These local values override the theme for that title only. Set `Level` instead when you want the heading size that
matches the outline.

**Can I turn off the motion for a single title?** Not on its own. The motion level is inherited, so set
`arc:ArcMotion.Level="Reduced"` (or `Off`) on a window or a container, and every title under it shows its final state
at once.

## API reference

<!-- api: InViewTitle -->

## Accessibility

- The title exposes its full text as its name and sets `HeadingLevel` from `Level`. A `Paragraph` title sets no
  heading level.
- The control reports the text role. The animated parts are hidden from assistive technology, so only the text is read.
- A title that is already scrolled past when the page opens shows its final state at once.
- A change to `Text` is not announced. Use a live region on a parent element when a change must be announced.
- The control is not focusable and has no keyboard interaction, so it adds no tab stop to the page.

## Motion

- The title reveals when 45% of it is visible. Opacity leads, the blur clears next and the movement settles last.
- `Word` and `Line` rise out of a clip, `Wipe` uncovers from left to right, `Tracking` opens its spacing and `Blur`
  sharpens each word.
- With `IsReplayEnabled`, leaving the viewport is a fast fade with no stagger.
- Reduced motion shows the title at once, with no movement, blur or mask. Off behaves the same way.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Title reaches 45% visible | Units reveal in the variant's order over 0.8 to 0.9 s | Shown at once | Shown at once |
| Title already scrolled past at mount | Shown at once | Shown at once | Shown at once |
| Title leaves the viewport with `IsReplayEnabled` | Quick fade with no stagger (about 180 ms) | Stays visible | Stays visible |
| Title re-enters with `IsReplayEnabled` | Reveals again | Shown at once | Shown at once |

## Related

- `TextReveal` for the headline that plays once when the page loads.
- `TextShimmer` for a status line that shows ongoing work.

| Component | Choose it when |
|---|---|
| `TextReveal` | The headline is the first thing on the page and should play when it appears |
| `TextShimmer` | A status line shows work that is still running |
| `TextMorph` | A short label changes state and the letters should move from the old text to the new |
| `Accordion` | A heading opens and closes the content beneath it |
| `Alert` | A warning or a message must be read at once, without a reveal |
