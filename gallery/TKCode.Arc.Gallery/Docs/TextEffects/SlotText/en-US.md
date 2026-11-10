---
title: Slot text
summary: Text and numbers that spin into their new value like slot machine reels.
---

<Demo name="Price" />

## When to use

Slot text gives a change a beat. Every character is its own reel: digits count through the wheel in the direction the
number moved, letters shuffle through the alphabet, and the reels stop one after another from left to right.

- **Prices and plans**: a price that changes with a billing period or a seat count, where the new amount deserves a look.
- **Headline stats**: a total on a dashboard or a landing page that updates rarely.
- **Launch moments**: a status word or a countdown at the point where something goes live.

## When not to use

- **A number that changes often**: use `AnimatedCounter`, whose wheels settle quickly and stay readable in a live feed.
- **A label that changes state in a control**: use `TextMorph`, which moves only the letters that differ.
- **Text the reader must follow as it appears**: use `TextReveal`.
- **Long sentences**: every character spins, so keep the value short.

## Usage

Set `Value`. A number goes through `Format`, and without one it uses en-US grouping, so 12480 reads 12,480. Anything else
shows as text. A value written in XAML is text, so bind a number or set it from code when you want grouping.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SlotText Value="{Binding Total}" FontSize="44" FontWeight="SemiBold" />
</UserControl>
```

The first value appears without motion, and so does any value set before the control is on screen. `FontSize`,
`FontWeight`, `FontFamily` and `Foreground` work as they do on `TextBlock`.

### Numbers that change length

Numbers match reels from the end, so 999 to 1,000 keeps the ones and grows on the left. A leading symbol such as a
currency sign or a plus keeps its own reel and never spins into a digit. Text matches from the start. `Align` overrides
either default.

### Words

Letters shuffle through their own case on a fixed sequence, so the same change always spins the same letters. Symbols
swap in one step. A reel with no character left shrinks away, and a slot's width follows its character on a spring, so
the word never resizes in one frame.

<Demo name="Launch" />

### Timing

`Duration` is how long the first reel spins, `Stagger` is the time between reels stopping, and `Spins` is the number of
extra full turns a digit makes. `Spins="0"` rolls straight to the new digit.

<Demo name="Timing" />

## API reference

<!-- api: SlotText -->

## Accessibility

The control exposes one text element named with the whole formatted value, never the characters on the reels. Set
`Announce="True"` to have new values read politely by screen readers; leave it off for a value that changes often. Slot
text takes no focus and no pointer input.

## Motion

A reel spins for `Duration` plus `Stagger` for each character before it, 0.9 s plus 70 ms by default, on a curve with a
fast start and a long soft landing that never passes the target. It blurs with its speed, up to 2.4 px, and glyphs fade at
the edges of the reel's window. A new value restarts each changed reel from the cell on screen, so nothing jumps. Slot
widths follow on a spring with no bounce, for at most 0.6 s; a leaving reel shrinks in 0.35 s. With reduced motion the
value changes at once.

## Responsive behavior

Slot text stays on one line and is as wide as its characters. Digits use tabular figures when the font has them, so a
changing number keeps its width and nothing beside it moves. All distances follow `FontSize`, and the glyphs reach a
little past the line while they spin, so leave a small margin above and below in tight layouts.

## Related

| Component | Choose it when |
|---|---|
| `AnimatedCounter` | A number updates often and should settle quickly |
| `TextMorph` | A short label changes letter by letter |
| `TextReveal` | A piece of text appears for the first time |
| `TextShimmer` | Text shows that work is in progress |
