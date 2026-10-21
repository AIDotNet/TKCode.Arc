---
title: Text morph
summary: Morph a label into its next state, letter by letter.
---

<Demo name="Status" />

## When to use

`TextMorph` is a short label that changes state in place. It keeps one line, measures to its text and moves the letters
that stay the same to their new positions, so a status change reads as the same label updating, not as a new one
appearing. It fits a line of product copy that is read at a glance: a status, a plan name or a price that changes
while the person is looking at it.

- **Deployment status on a release dashboard**: the label on a pipeline card moves from "Deploy" to "Deploying", then
  to "Deployed" or "Rolled back". The shared letters stay put, so the card does not flicker while the state changes.
- **Invoice status in a billing detail header**: "Draft", "Sent" or "Paid" next to the number `inv_2026_0412`. The
  change is visible from across the room in a finance review, without a toast or a modal.
- **A price in a plan picker**: "$12 / month" changes to "$120 / year" when the person switches the billing period.
  With `PrefixSuffix` matching only the amount changes; the unit at the end stays where it was.
- **A label inside a custom control**: a badge or a segmented label that changes its text as the person acts, where
  a plain `Button` would not give the right shape or behavior.

## When not to use

- **A control that is clicked and has states of its own**: use `Button`. `TextMorph` is text only and has no click,
  hover or focus behavior of its own.
- **A confirmation after copying**: use `CopyButton`, which is built for the copy action and its feedback.
- **Text that wraps onto several lines**: `TextMorph` is always one line. Use a regular `TextBlock`, or `TextReveal`
  for a headline that plays once.
- **A status line that shows ongoing work**: use `TextShimmer`. It sweeps a light across the text while work runs;
  `TextMorph` only moves letters when the text changes.
- **A message the person must read in full**: use `Toast`, which carries a full sentence and its own timing. A
  morphing label is too short to explain what happened.
- **A number that counts up or down over time**: use `AnimatedCounter`. A figure that changes every second would
  keep its letters moving, which is distracting.

## Usage

Set `Text` to the current label. When `Text` changes after the control has loaded, the letters glide, rise or fade to
the new label. The control derives from Avalonia's `Control` and draws its text itself: it measures each letter with
the text layout and draws the letters directly, so it creates no child control per letter.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextMorph Text="Deploy" FontSize="28" />
</UserControl>
```

```csharp
status.Text = "Deploying";
```

The control takes its font and brush from `TextElement`, the same as a text block. A value set on the control wins over
one set on a parent, and the theme sets the body font family, so an unstyled label already matches the body copy.

### Anatomy

1. **Label** (`Text`): the one line of text. Letters are measured and drawn one at a time, so the text is not wrapped
   or truncated.
2. **Font** (`FontFamily`, `FontSize`, `FontWeight`): the body font family from the theme, with the size and weight set
   on the control or on a parent. A change to any of them redraws the label at its new size without animation.
3. **Foreground** (`Foreground`): the brush that paints the letters. It inherits from `TextElement` like the font.
4. **Width**: the control is as wide as the current text. The width follows a change on a spring, so the space around
   the label (a badge edge or a button edge) grows and shrinks with the text.
5. **Motion area**: entering letters rise 0.14em from below and leaving letters lift 0.1em. The control does not clip
   its content, so these moves stay visible outside the measured box.

### Changing state

A status word changes from one state to the next. The letters both labels share glide to their new positions. New
letters rise in with a short stagger, and removed letters fade away. Set `Text` to the next state in code, for example
when the job's status arrives from a service.

```csharp
var words = new[] { "Deploy", "Deploying", "Deployed", "Rolled back" };
var index = 0;
next.Click += (_, _) => status.Text = words[index = (index + 1) % words.Length];
```

Keep the list of states short and in the order a person reads them. A label that jumps between unrelated words, such
as "Deploy" and "Rolled back", still animates correctly, but the shared letters are few and the motion carries less
meaning. Use a plain change for such pairs.

### Binding to a view model

`TextMorph` is display-only. The view model owns the state, and the label follows its text. Bind `Text` to a string
property; each change is a morph, so set the property only when the state changes.

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusLabel = "Deploy";

    public void MarkRunning() => StatusLabel = "Deploying";

    public void MarkDone(bool succeeded) => StatusLabel = succeeded ? "Deployed" : "Rolled back";
}
```

```xml
<arc:TextMorph Text="{Binding StatusLabel}" FontSize="28" FontWeight="SemiBold" />
```

Do not bind `Text` to a value that changes on every frame or every tick. The label morphs on each change, so a value
that changes ten times a second keeps the letters in motion all the time.

### Matching

`Matching` sets how letters are paired between the old and the new text. It takes a `TextMorphMatching` value.

- `Occurrence` (default) pairs each character with the same occurrence in the new text. The second "i" pairs with the
  second "i". Use it for words, where letters move to their new places within the word.
- `PrefixSuffix` keeps the shared start and end in place and replaces only the middle. Use it for prices and counters,
  where the unit or the currency sign should not move while the number changes.

<Demo name="PrefixAndSuffix" />

Matching works on single characters, so a character that the font would join into one shape (a ligature, or a
combined emoji) can separate while it morphs. Check such strings in the real font before you rely on them.

### Font

`TextMorph` uses the font and foreground properties of `TextElement`. Set them on the control or on a parent.

```xml
<arc:TextMorph Text="Saved" FontSize="16" FontWeight="SemiBold" />
```

A parent that sets the style for a whole row works the same way, so the status labels in a list share one size:

```xml
<StackPanel TextElement.FontSize="14"
            TextElement.FontWeight="Medium"
            TextElement.Foreground="{arc:ArcDesignTokenResource TextSecondary}">
    <arc:TextMorph Text="Draft" />
</StackPanel>
```

### Example: invoice status in a billing header

A billing detail header shows the invoice number, the amount and a status label. The status is the only part that
changes while the person works on the invoice, so it is the one `TextMorph`. The card keeps the same width for every
state, so the header does not move when the label grows from "Sent" to "Awaiting payment". The amount and the due
date are plain text that stays put.

![An invoice header with a morphing status label and the amount due](images/TextMorphInvoiceStatus.png)

```xml
<Border Width="560"
        Padding="20"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="12">
        <TextBlock Text="Invoice inv_2026_0412"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        <arc:TextMorph Text="Awaiting payment"
                       FontSize="22"
                       FontWeight="SemiBold" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Grid.Column="0"
                       Text="Due 2026-11-05"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <TextBlock Grid.Column="1"
                       Text="$4,800.00"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </Grid>
    </StackPanel>
</Border>
```

### Content guidelines

A morphing label is short by design: one word or a short phrase in sentence case, with no final period. Keep the
states the same kind of word, such as the status of an object, so the change reads as a state and not as a sentence.
Put numbers in digits and keep the unit with the number.

| Instead of | Write |
|---|---|
| "Your invoice has now been sent to the customer." | "Sent" |
| "PAID IN FULL" | "Paid" |
| "Deploying now..." | "Deploying" |
| "Price: 120 dollars per year" | "$120 / year" |
| "Rolled back because of a failure" | "Rolled back" |

### Best practices

**Do**

- Keep each state to one or two words. A long label makes the width spring visible and pushes its neighbors.
- Place the label in a container with a fixed width, or in a `Grid` column that is wide enough for the longest state.
  The control's width follows its text, so an unbounded label moves what sits beside it.
- Use `PrefixSuffix` for prices and counters, so the stable part of the label stays in place.
- Change the label only when the state changes. Each change is a motion the person must follow, so keep them rare.
- Announce the change with a live region on a parent when the new state matters. The control does not announce it.

**Avoid**

- Binding the label to a value that changes every tick, such as a timer or a progress percentage. The letters never
  settle, and a progress value is better shown in `Progress`.
- Using `TextMorph` for a button's label that has click behavior. The control has no click or focus of its own.
- Long sentences or wrapped copy. The control is one line, and a long state pushes the layout around it.
- Relying on the motion to convey the change. Reduced and Off motion jump to the new label, so the words must
  already say what changed.
- Setting the label before the control loads and expecting it to animate. The first text appears without motion.

### Common questions

**Why does my label jump instead of morphing?** The label morphs only after it has loaded, and only while the motion
level is `Full`. Under `Reduced` or `Off`, a change replaces the label at once. A change to `FontSize`, `FontWeight`
or `FontFamily` also jumps to the new size, because the letters are re-measured rather than moved.

**Why does the row next to the label move when the state changes?** The control's width follows its text on a spring,
so a longer state pushes the controls beside it. Give the label a fixed width in a `Grid` column, or measure the
longest state and reserve that width.

**Does a label morph when I set `Text` to the same value?** No. The morph starts when the text changes; setting the
same string again leaves the letters where they are.

**Can the label wrap onto two lines?** No. The control is always one line. For text that needs two lines, use
`TextReveal` for a headline or a regular text block for body copy.

**How do I make the letters move with a different motion?** The motion is fixed by the control. Its timing and
distances are built in, and the motion level set on a window or a container (`arc:ArcMotion.Level`) decides whether it
plays. Pick the states and the matching mode, and let the control do the rest.

## API reference

<!-- api: TextMorph -->

## Accessibility

- The current text is the control's name. Assistive technology reads the plain text.
- The letters are hidden from assistive technology, so a change is not read letter by letter.
- The control does not announce a change. Use a live region on a parent element when the new state must be announced.
- Under `Reduced` and `Off` motion the new text appears at once, so the words alone must carry the change.
- The control is not focusable and has no keyboard interaction, so it adds no tab stop to the page.

## Motion

- Shared letters glide to their new positions on a spring.
- New letters rise and grow in from 80% size while they fade in, one after another. The first starts after 50 ms, each
  following letter 32 ms later, for up to eight letters.
- Removed letters fade out while they shrink slightly and lift a little, then disappear. Their exit is a short
  standard fade.
- The container width follows the new label on a spring. A font change or a size change of the container jumps to the
  new width without animation.
- Reduced motion replaces the label and the width at once. Off behaves the same way.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Shared letters move | Glide to their new positions on a spring | Jump | Jump |
| New letters appear | Rise 0.14em while they fade in, staggered | Jump | Jump |
| Removed letters | Fade out, lift 0.1em, then leave | Removed at once | Removed at once |
| Width of the label | Springs to the new width | Jumps | Jumps |
| Font, weight or size change | Jumps to the new size | Jumps | Jumps |
| Text set before the control loads | Shown at once, no motion | Shown at once | Shown at once |

## Related

- `Button` for a control that is clicked and has states of its own.
- `TextShimmer` for a status line that shows ongoing work.
- `TextReveal` for a headline that plays once when it appears.

| Component | Choose it when |
|---|---|
| `Button` | The label belongs to a control that is clicked |
| `CopyButton` | The label confirms that something was copied |
| `TextShimmer` | A status line shows work that is still running |
| `TextReveal` | A headline should play once when it appears |
| `AnimatedCounter` | A figure counts up or down over time |
| `Toast` | A full sentence must be read, such as the result of an action |
