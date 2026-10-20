---
title: Text morph
summary: Morph a label into its next state, letter by letter.
---

## When to use

- A short status word that changes, such as "Follow" and "Following" or "Save" and "Saved".
- A label inside a custom button or badge that should change in place.
- A counter or price whose start and end stay put while the middle changes.

## When not to use

- A button that needs click behavior and states: use `Button`.
- Text that wraps onto several lines: `TextMorph` is always one line.
- Long text or body copy: the per-letter effect is for short labels only.
- A status line that shows ongoing work: use `TextShimmer`.

## Usage

Set `Text` to the current label. When `Text` changes, the letters glide, rise or fade to the new label.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TextMorph Text="Deploy" FontSize="28" />
</UserControl>
```

```csharp
status.Text = "Deploying";
```

### Changing state

A status word changes from one state to the next. Letters both labels share glide to their new positions. New
letters rise in with a short stagger, and removed letters blur away.

```csharp
var words = new[] { "Deploy", "Deploying", "Deployed", "Rolled back" };
var index = 0;
next.Click += (_, _) => status.Text = words[index = (index + 1) % words.Length];
```

### Matching

`Matching` sets how letters are paired between the old and the new text.

- `Occurrence` (default) pairs each character with the same occurrence in the new text. The second "i" pairs with the
  second "i".
- `PrefixSuffix` keeps the shared start and end in place and replaces only the middle. Use it for prices and
  counters.

```xml
<arc:TextMorph Text="$12 / month" FontSize="20" Matching="PrefixSuffix" />
```

### Font

`TextMorph` uses the font and foreground properties of `TextElement`. Set them on the control or on a parent.

```xml
<arc:TextMorph Text="Saved" FontSize="16" FontWeight="SemiBold" />
```

## API reference

<!-- api: TextMorph -->

## Accessibility

- The current text is the control's name. Assistive technology reads the plain text.
- The letters are hidden from assistive technology, so a change is not read letter by letter.
- The control does not announce a change. Use a live region on a parent element when the new state must be announced.
- The control is not focusable and has no keyboard interaction.

## Motion

- Shared letters glide to their new positions on a spring.
- New letters sharpen in from a soft blur with a short stagger. Removed letters blur out.
- The container width follows the new label on a spring. A font change or a size change of the container jumps to the
  new width without animation.
- Reduced motion replaces the label and the width at once. Off behaves the same way.

## Related

- `Button` for a control that is clicked and has states of its own.
- `TextShimmer` for a status line that shows ongoing work.
- `TextReveal` for a headline that plays once when it appears.
