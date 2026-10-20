---
title: Text reveal
summary: Reveal a short piece of content with restrained motion.
---

## When to use

- A hero headline or landing page opening that should play once when it appears.
- A short intro paragraph that reveals word by word under a headline.
- A headline that must be readable from its first frame, even if the script loads slowly.

## When not to use

- Section titles further down a page that should reveal as they scroll into view: use `InViewTitle`.
- Long body text: the per-word effect is for headlines and short paragraphs only.
- A status line that keeps working: use `TextShimmer`.

## Usage

The control plays its entrance once. Text with `\n` breaks lines on purpose; lines are balanced and at most 24
characters wide.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextReveal Text="Interfaces that feel considered from the first frame"
                    Level="H1" />
</UserControl>
```

### Heading levels and paragraphs

`Level` sets the heading level (`H1`, `H2`, `H3`) or `Paragraph`. Headings use display type and tighter leading. A
paragraph uses body type and a softer blur. The level also sets `AutomationProperties.HeadingLevel` for headings.

```xml
<arc:TextReveal Text="Ship the quiet details.&#10;Keep the motion honest."
                Level="H2"
                Delay="0:0:0.2" />

<arc:TextReveal Text="A paragraph reveals with a softer blur, word after word."
                Level="Paragraph" />
```

`Delay` waits before the first word rises. The stagger after it stays under half a second, so long text does not
extend the entrance.

### Replay

`Replay()` plays the entrance again. Call it from code, for example from a button click.

```csharp
var hero = new TextReveal { Text = "Interfaces that feel considered from the first frame", Level = TextLevel.H1 };
var replay = new Button { Content = "Replay", Variant = ButtonVariant.Secondary };
replay.Click += (_, _) => hero.Replay();
```

### Entrance before the first layout

A control that is mounted below the fold waits, hidden, until it first enters the viewport. A control that is on
screen when it mounts starts on its first frame. `IsRevealing` is `true` while the entrance runs.

### Tracking, leading and blur

`Tracking` sets letter spacing in em, `Leading` sets the line height as a multiple of the font size, and
`BlurRadius` sets the starting blur of each word. The level theme sets these values; override them only when a
headline needs a different rhythm.

```xml
<arc:TextReveal Text="Quiet by default" Level="H2" Tracking="-0.02" Leading="1.2" />
```

## API reference

<!-- api: TextReveal -->

## Accessibility

- Headings expose the full text as their name and set `HeadingLevel` 1 to 3. A paragraph is plain text.
- A forced line break is read as a space.
- The control does not announce its changes. Use a live region on a parent element when the text must be announced.
- The control is not focusable and has no keyboard interaction.
- Choose the level from the page outline, not from the size you want.

## Motion

- Each word rises out of its own clip while it sharpens from a soft blur. Words start in order.
- The whole entrance plays once when the control appears on screen. It replays only when `Replay()` is called.
- Reduced motion keeps the word order but removes the rise and the clip; the words fade in quickly.
- Off shows the final text at once.

## Related

- `InViewTitle` for section titles that reveal as they scroll into view.
- `TextShimmer` for a status line that shows ongoing work.
- `TextMorph` for a short label that changes state letter by letter.
