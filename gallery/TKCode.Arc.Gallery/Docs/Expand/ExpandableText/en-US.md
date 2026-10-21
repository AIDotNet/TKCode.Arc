---
title: Expandable text
summary: Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.
---

<Demo name="Default" />

## When to use

`ExpandableText` shows a long paragraph in a few lines and lets the reader open the rest. It keeps the layout of a
list or a column even when the text lengths differ, which matters most when each item is a card or a row with a
fixed grid behind it. The toggle appears only when the text really overflows, so short text stays plain.

- **Customer reviews and comments**: a review list on a product page or a comment thread in a support tool, where
  each entry shows two lines and the reader opens the ones they want to read in full.
- **Profile and team bios**: a directory of people in an internal tool, where the bio shows three or four lines in
  each card and the full text opens on demand.
- **Product descriptions in a catalog**: a long description under the product title, clamped so the price and the
  add-to-cart button stay above the fold, with "Show full description" as the toggle label.
- **Release notes and changelogs in a developer console**: each release shows the first lines of its notes, and the
  rest opens in place without navigating to another page.

## When not to use

- **Required text**, such as legal terms, a refund policy at checkout, or an error with the steps to fix it. Do not
  clamp it; show the whole text, or link to the page that holds it.
- **A group of titled sections or questions**: use `Accordion`, which gives each section its own question as the
  row header.
- **Structured details hidden behind a summary line**, such as an order with a status and a list of items: use
  `ExpandableCard`, which keeps a header and a summary visible.
- **Text that must keep a fixed height and scroll**, such as a log or a transcript: use `ScrollArea`.
- **Text that is short by design**, such as a label or a title: use a plain `TextBlock`. A toggle on a title is noise.

## Usage

Set `Text` for the content. The text is clamped to `Lines` lines, which defaults to 3. The toggle appears only when
the text overflows the clamp.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableText Text="We moved our whole design team onto the new workspace in a single afternoon. The import kept every comment and version."
                        MaxWidth="520"
                        HorizontalAlignment="Left" />
</UserControl>
```

`ExpandableText` derives from Avalonia's `TemplatedControl` and owns three parts: the clamped text, the fade over
its last line and the toggle row under it. The text is an ordinary wrapping `TextBlock` inside the clamp, so the
text styling (`FontSize`, `Foreground`, `LineHeight`) is set on the control and the text inherits it. The default
text is the small type size in the secondary text color with a 1.4 line height, which is the Arc look for body copy
in cards and lists.

Set `MaxWidth` or a `Width` on the control, not on the text. The clamp is measured against the width the control
receives, so the number of visible lines follows the layout.

### Anatomy

1. **Text** (`Text` or `Inlines`): wrapped text in the secondary text color at the small type size, with a 19.6 px
   line height (14 px times 1.4).
2. **Clamp**: while collapsed, the text is exactly `Lines` times the line height tall. Text beyond that is hidden.
   The clamp is recalculated when the width or the font changes, so no code is needed for a resize.
3. **Fade**: a soft mask over the bottom 1.6 lines while the text is collapsed. It dissolves as the text opens and
   returns near the clamp when it closes.
4. **Toggle row**: a gap of 8 px below the text. The toggle is left aligned, with a minimum height of 28 px, 2 px of
   horizontal padding and a left margin of minus 2 px so the label lines up with the text edge.
5. **Toggle label** (`MoreLabel` or `LessLabel`): medium weight in the foreground color. Both labels share one cell, so
   the toggle's width does not change when the label does. Hover changes the label to the secondary text color and
   pressing it changes the label to the muted text color.
6. **Chevron**: a 16 px down chevron after the label, in the muted text color. It turns 180 degrees with the
   opening, following the same progress as the text.
7. **Focus ring**: a 2 px ring around the toggle, shown for keyboard focus only.

### Line count

`Lines` sets how many lines show while the text is collapsed. Use 2 lines for dense lists such as comments and
reviews, where many entries must fit on one screen. Use 3 or 4 lines for profiles and product descriptions, where
the first sentences carry the most information. The minimum is one line; smaller values are raised to one.

<Demo name="TwoLines" />

```xml
<arc:ExpandableText Lines="2"
                    Text="{Binding CommentText}"
                    MaxWidth="420" />
```

Count lines against the real width. The same text clamps to a different number of lines in a narrow card than in a
wide column, so check the clamp at the smallest width the text appears in.

### Labels

`MoreLabel` and `LessLabel` set the toggle text. The defaults are `Read more` and `Show less`. Change them when the
toggle should name what opens, such as "Show full bio" or "Show full description", because a specific label tells
the reader what they will get.

```xml
<arc:ExpandableText MoreLabel="Show full bio"
                    LessLabel="Show less"
                    Lines="4"
                    Text="{Binding Bio}" />
```

Both labels are announced to assistive technology as the toggle's name, so each one must read as a complete action
on its own.

### Binding the expanded state

`IsExpanded` is two-way. Bind it to a view model property, or set it from code. `ExpandedChanged` is raised when the
text opens or closes, with an `ExpandableTextExpandedChangedEventArgs` whose `IsExpanded` holds the new state.

<Demo name="Controlled" />

```xml
<arc:ExpandableText Text="{Binding Bio}"
                    IsExpanded="{Binding BioExpanded, Mode=TwoWay}" />
```

Use the event when the page needs to react to the change, such as to update a status line or to save the reader's
choice. Use the binding when another control must open or close the text, as the demo's button does.

### Text that fits

The toggle appears only when the text really overflows, and follows width and font changes. Short text has no
toggle and no fade, so a list that mixes short and long entries looks correct without any extra logic.

<Demo name="Fits" />

`IsOverflowing` is read-only and reports the current state. Use it to show a count or a hint elsewhere on the page,
not to decide whether the toggle appears: the control already does that.

### Driving it from data

A review list is a collection of items, each with its own text and its own open state. Keep the open state on the
item, so that opening one review does not change the others.

```csharp
public sealed partial class ReviewItem : ObservableObject
{
    public required string Author { get; init; }

    public required string Date { get; init; }

    public required string Body { get; init; }

    [ObservableProperty]
    private bool _isExpanded;
}

public partial class ProductReviewsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ReviewItem> _reviews = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Reviews}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Spacing="6" Margin="0,0,0,16">
                <TextBlock Text="{Binding Author}"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:ExpandableText Lines="2"
                                    Text="{Binding Body}"
                                    IsExpanded="{Binding IsExpanded, Mode=TwoWay}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

The view model owns the text and the item owns the open state, so a reader's choice survives when the list is
filtered or re-sorted, as long as the same item objects stay in the collection. Replacing the items with new objects
resets their open state to collapsed.

### Rich text and links

`Text` takes plain text, with paragraphs separated by line breaks. When the copy needs runs or links, set `Inlines`
instead; it is used in place of `Text` when it is set. Build the collection in code: the property has no
default collection, so XAML cannot add runs to it. A link inside the text is an inline control, placed in an
`InlineUIContainer`, so it takes keyboard focus like any other control.

```csharp
var notice = new ExpandableText { Lines = 3, MaxWidth = 520 };
notice.Inlines = new InlineCollection
{
    new Run("Your data is stored in the EU region. Read the "),
    new InlineUIContainer(new Button { Content = "privacy policy" }),
    new Run(" for the retention rules and the list of processors.")
};
```

A link inside the clamped part is reachable by keyboard even though the reader cannot see it. When such a link takes
focus while it sits below the clamp, the text opens so that the focus is never on hidden content. Use links that
the reader would want to reach, and place the most important link early in the copy so it is usually visible.

### Example: product reviews

A review list on a product page of a storefront. Two-line clamps keep the reviews compact, and the toggle appears only
on the long reviews, so the short review reads as it is. The author, the verified-purchase label and the date sit
above the text in a small muted line, so the reader can judge the review before opening it. The list is 560 wide,
the width of the product column.

![A product review list with two clamped reviews, one with a Read more toggle, and one short review](images/ReviewList.png)

```xml
<StackPanel Width="560" Spacing="18">
    <TextBlock Text="Customer reviews"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Spacing="6">
        <TextBlock Text="Priya N. · Verified purchase · 2026-03-14"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="We took the Trail Pack on a three-day ridge walk in wet weather. The main pocket kept the sleeping bag dry, the hip belt stayed comfortable under a loaded pack, and the zips still ran smoothly on the last morning. The only change I would make is a larger side pocket for water bottles." />
    </StackPanel>
    <StackPanel Spacing="6">
        <TextBlock Text="Daniel K. · Verified purchase · 2026-02-27"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="Fits a laptop and a lunch box with room to spare." />
    </StackPanel>
    <StackPanel Spacing="6">
        <TextBlock Text="Mei L. · Verified purchase · 2026-02-08"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="Bought it for the commute. The shoulder straps needed a little adjusting on the first day, and after that it carried the laptop, a change of clothes and a water bottle without any strain on my back. The reflective trim is a welcome detail in the winter months." />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Toggle labels**: a verb phrase that names what opens, in sentence case. "Read more" is the default; "Show full bio"
  and "Show full description" say what the reader gets. "Show less" closes the text.
- **Lines of copy**: write the first sentence of a review, bio or description so it stands on its own in a clamp. The
  reader decides from those lines whether to open the text.
- **Metadata above the text**: author, date and any verification or status in a short muted line, so the reader
  judges the item before the copy.
- **Numbers**: write them as digits in dates and counts ("2026-03-14", "14 reviews") so the clamp shows the full
  date before the toggle.

| Instead of | Write |
|---|---|
| More... | Read more |
| Expand | Show full description |
| Click here to see the rest of the text | Show full bio |
| Collapse | Show less |
| Read the rest of this review, which goes on for a while about the zip. | Read more |

### Best practices

**Do**

- Use two lines for dense lists and three or four lines for a single item that the reader is likely to read, such as a
  profile. The clamp is the first thing the reader sees, so choose it for the content, not for the layout.
- Put the most useful information in the first sentence, because the rest is hidden until the reader opens it.
- Keep `Lines` the same for every item in a list so the rows align when the toggles are not shown.
- Let the control measure the overflow. Do not split the text yourself or count characters, because the clamp follows
  the real width and font.

**Avoid**

- Clamping text the reader must read, such as terms, prices, errors or warnings. The toggle hides it until the reader
  asks, and many readers do not.
- Setting `Lines` to 1 for a review or a bio. A single line hides almost everything and the toggle becomes the main
  content.
- Putting a form or a button that matters in the hidden part of the text. The reader may never open it, and focus
  would open the text unexpectedly.
- Using `ExpandableText` for a list of titled sections. Each section should be an `Accordion` row so it has its own
  heading.
- Changing `Text` on every update of a live feed. Each change re-measures the clamp, and the toggle can appear or
  disappear as the text changes. Update the text when the reader has a reason to see it.

### Common questions

**Why is there no toggle on my text?** The toggle appears only when the full text is taller than the clamp. If the text
fits in `Lines` lines at the current width, there is nothing to reveal, so the control shows it plainly. Check
`Lines`, the width and the font size.

**Why does the toggle keep the same width when the label changes?** Both labels share one cell in the toggle, so the
toggle is as wide as the longer label. The width is constant, and the labels crossfade inside it.

**How do I know when the text opens or closes?** Subscribe to `ExpandedChanged`, which receives an
`ExpandableTextExpandedChangedEventArgs`, and read `IsExpanded`. Or bind `IsExpanded` two-way and watch the view model
property.

**Can a link in the hidden part of the text open it?** Yes. When a link inside the text takes keyboard focus while it
sits below the clamp, the text opens. A link that is visible above the clamp does not change the state; only the toggle
and a keyboard focus on a hidden link do.

**What happens when the text closes far down the page?** The control brings the start of the text back into view, so
the reader is not left below the text they just collapsed. Under reduced motion this happens at once.

## API reference

<!-- api: ExpandableText -->

### Event arguments

`ExpandableTextExpandedChangedEventArgs` is the argument of `ExpandedChanged`.

| Member | Meaning |
|---|---|
| `IsExpanded` | The new state: `True` when the text has opened, `False` when it has closed |

## Accessibility

- The whole text is always in the accessibility tree. Screen readers read all of it without using the toggle, so the
  clamp is purely visual.
- The toggle is a button that exposes the expand and collapse pattern. Its name is the current label, `MoreLabel` or
  `LessLabel`, and it changes when the state changes.
- Links inside the clamped part can take keyboard focus. Focusing a link that is hidden below the clamp opens the
  text, so focus is never left on hidden content.
- The toggle works with Enter and Space.
- The text itself does not take focus. Only the toggle and the links inside it are stops in the tab order.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves through the links inside the text, then to the toggle when it is shown |
| `Enter` / `Space` | Opens or closes the text from the toggle |
| Focus on a link below the clamp | Opens the text, so the focused link is visible |

## Motion

- The height, the fade over the last line and the chevron follow one progress value, so they always move in step. They
  ease out on a calm curve and do not overshoot.
- Opening takes about half a second and closing about 0.4 seconds. Longer distances take slightly longer, up to a
  small limit.
- The fade over the last line dissolves as the text opens. Lines that appear rise through a soft edge, so no hard line
  is uncovered.
- A click during the motion reverses from the current height, so the text never jumps to either end.
- The two toggle labels crossfade with a 4 px drift and a 2 px blur, timed to the opening, and the toggle width does
  not change.
- Reduced motion opens and closes the text at once. When the text closes far down the page, the start of the text is
  brought back into view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Text opens | Height grows on a calm ease-out, the fade over the last line dissolves and the chevron turns | Opens at once | Immediate |
| Text closes | Height shrinks in about 0.4 seconds, the fade returns and the chevron turns back | Closes at once, with the start of the text brought into view | Immediate |
| Toggle label | "Read more" and "Show less" crossfade with a 4 px drift and a 2 px blur | Immediate | Immediate |
| Toggle hover and press | Label color crossfades | Immediate | Immediate |

## Related

- `Accordion` for several titled sections that open in place.
- `ExpandableCard` for a summary card with hidden details.
- `ScrollArea` for content that must keep a fixed height.

| Component | Choose it when |
|---|---|
| `Accordion` | Several titled sections open in place, each with its own question |
| `ExpandableCard` | A summary card needs a header and a summary line above its hidden details |
| `ScrollArea` | The content must keep a fixed height and scroll |
| `Textarea` | The reader edits the text rather than reads it |
| `TextBlock` | The text is short by design, such as a label or a title |
