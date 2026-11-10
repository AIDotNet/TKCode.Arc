---
title: Ruler slider
summary: A tape of ticks that slides under a fixed needle, with momentum, snapping, and a rolling readout.
---

<Demo name="Height" />

## When to use

`RulerSlider` sets one number on a scale that moves under a fixed needle. The scale can be far longer than the control,
so people get fine control over a wide range, and the large readout shows the value while the tape moves.

- **Body measurements in onboarding or a health app**: height, weight or a target, where the range is wide and a form
  field feels heavy on a phone.
- **A dose or an amount in a booking or a checkout**: a monthly budget or a loan amount with round steps.
- **A setting with real units**: a focal length, a distance or a temperature, where the ticks show the scale itself.

## When not to use

- **A setting where the position on the track is what matters**, such as a volume: use `Slider` or `ElasticSlider`, where
  the whole range is in view.
- **A range with two ends**: use `RangeHistogramSlider`, or `Slider` with `IsRange`.
- **A number the person already knows and wants to type**: use `NumberField`.
- **Tight rows and dense forms**: the control needs a full-width row, and about 150 px of height with its label and
  readout. Use `Slider` or `ScrubInput` there.

## Usage

Set `Label`, the limits and `Value`. `Unit` follows the readout and is spoken with the value. `RulerSlider` derives from
Avalonia's `RangeBase`, so `Value`, `Minimum`, `Maximum`, `SmallChange`, `LargeChange` and `ValueChanged` behave as on any
range control. `ValueChanged` fires for every tick that passes the needle; `ValueCommitted` fires once the tape comes to
rest after a drag or a flick, and at once for a tap or a key.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RulerSlider Label="Height"
                     Unit="cm"
                     Minimum="120"
                     Maximum="220"
                     Value="{Binding Height}" />
</UserControl>
```

### Steps, ticks and spacing

`SmallChange` is the value of one small tick, and the tape always rests on a tick. `MajorEvery` sets how many small ticks
lie between labelled ticks, and an even number adds a taller tick halfway. `TickSpacing` sets the pixels between small
ticks: wider spacing means more travel for each tick and so finer control.

<Demo name="FineSteps" />

### Formatting

`ValueFormatter` formats the readout, the tick labels and the spoken value. Digits inside the text still roll, and text
around them, such as a currency sign, stays put. Without a formatter, tick labels show only the decimals their own spacing
needs, so a half-kilo scale with labels every 5 reads 30, 35, 40. `LargeChange` sets the distance of `PageUp`, `PageDown`
and `Shift` with an arrow, and defaults to one labelled tick.

<Demo name="Formatted" />

### Disabled

`IsEnabled="False"` shows the value at half opacity and takes the control out of the tab order.

<Demo name="Disabled" />

## API reference

<!-- api: RulerSlider -->

## Accessibility

- The control is one tab stop and reports itself as a slider, with `Label` as its name and the limits as its range.
- The spoken value is the formatted value with `Unit` after it, such as "172 cm".
- The tape, its labels and the needle are drawn in the control and are not separate elements.
- The focus ring appears around the tape for keyboard focus.

### Keyboard

| Keys | Action |
|---|---|
| `Right` / `Up` | Raises the value by one tick |
| `Left` / `Down` | Lowers the value by one tick |
| `Shift` + arrow, `PageUp` / `PageDown` | Moves by `LargeChange` |
| `Home` / `End` | Goes to `Minimum` or `Maximum` |

## Motion

- The tape follows a drag 1:1. Past either end it gives up to 44 px, and on release it returns on a spring (0.5 s, bounce
  0.1) with the speed it was thrown at.
- A flick coasts: it travels as far as 0.42 s of its speed, snaps that to a tick, and eases there with a 260 ms decay. The
  value and the readout change as each tick passes the needle. Pressing the tape catches it where it is.
- A tap, a key or a new value glides the tape on the snappy spring, keeping any speed it already has.
- Each digit of the readout slides on its own over 240 ms, and a column that appears or leaves opens or closes on the
  morph spring.
- With reduced motion the tape stops at its ends and jumps to the value, and the digits change at once. With motion off
  everything is immediate.

## Responsive behavior

The control fills the width its parent gives it, with no breakpoints: the view shows more or fewer ticks, and the needle
stays in the middle. The height is fixed, with a 72 px tape. Ticks fade toward both edges, so a narrow view still reads as
a tape that continues. On touch, a horizontal drag moves the tape and a vertical one is left to the page, so the control
can sit inside a scrolling page. It does not need hover.

## Related

| Component | Choose it when |
|---|---|
| `Slider` | The position on the whole range matters, or the row is tight |
| `RangeHistogramSlider` | The value is a range over data |
| `NumberField` | The person types or steps an exact number |
| `ScrubInput` | The number is changed by dragging on its label |
