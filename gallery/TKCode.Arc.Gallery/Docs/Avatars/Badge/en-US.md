---
title: Badge
summary: A small label for status, category, or metadata.
---

<Demo name="Tones" />

## When to use

- A short status next to a title or in a table cell, such as Live, Draft or Failed.
- A count or state that changes in place while the badge stays on screen.
- A row marker with a tone and an optional leading icon.

## When not to use

- A full sentence of feedback: use `Alert` or `Toast`.
- A value the user can switch on and off: use `ChipGroup`.
- A large number with context around it: use `MetricCard`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Badge Content="Draft" />
</UserControl>
```

### Tones

`Tone` sets the color treatment: `Neutral` (the default), `Success`, `Info` (the accent color), `Warning` and
`Danger`. The label must name the state by itself, because the tone is not the only signal.

```xml
<WrapPanel>
    <arc:Badge Content="Draft" />
    <arc:Badge Tone="Success" Content="Live" />
    <arc:Badge Tone="Info" Content="Beta" />
    <arc:Badge Tone="Warning" Content="Degraded" />
    <arc:Badge Tone="Danger" Content="Failed" />
</WrapPanel>
```

### Sizes

`Size` sets the height: `Medium` (the default) is 26 and `Small` is 22. Use `Small` in dense tables.

<Demo name="Small" />

### Icon

`Icon` takes a leading icon. It takes the tone color and is hidden from assistive technology. The example uses an
`ArcGlyph` from `ArcIcons`.

<Demo name="Icons" />

```csharp
badge.Tone = BadgeTone.Success;
badge.Content = "Passing";
badge.Icon = new ArcGlyph { Data = ArcIcons.Check, Size = 12 };
```

### Changing in place

Keep the badge mounted and change `Content`. A new string or number rises in from a soft blur, and the pill width
springs to fit. Re-creating the badge loses this motion.

<Demo name="ChangingInPlace" />

## API reference

<!-- api: Badge -->

## Accessibility

- The badge is inline text: its label is read with the text around it.
- The icon is hidden from assistive technology. The label must say the state, not the color.
- While a label changes, the old text is hidden from assistive technology and only the current text is read.
- A badge does not announce its changes. If a change must be announced, put the badge in a live region of the
  surrounding content.
- The badge is not focusable and has no keyboard behavior.

## Motion

- A new label rises from a soft blur, and the old one lifts away with a subtle blur. The pill width follows on a
  morph spring.
- A new icon pops in from a slightly smaller size. A size change caused by a font change happens at once.
- Reduced motion fades the label quickly and jumps the width. Off changes everything at once.

## Related

- `Alert` or `Toast` for a sentence of feedback.
- `ChipGroup` for values the user can select.
- `MetricCard` for a large value with its context.
