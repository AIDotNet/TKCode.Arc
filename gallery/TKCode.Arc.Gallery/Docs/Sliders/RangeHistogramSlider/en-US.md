---
title: Range histogram slider
summary: A price range over a live histogram of the data, lighting the bars inside the range and settling on bar edges.
---

<Demo name="PriceFilter" />

## When to use

`RangeHistogramSlider` picks a lower and an upper value while showing how the data is spread between them. People see
where the values cluster before they choose, so they rarely filter down to an empty result.

- **A price filter in a catalog or a booking search**: the bars show that most rooms cost about $140, and a second group
  sits near $380.
- **A size or duration filter**: employees per company, file sizes, session lengths, or anything else with a long tail.
- **A range in an analytics view**: response times or order values, where the shape of the data tells people which part
  is worth looking at.

## When not to use

- **A range without data behind it**, such as an opening-hours window: use `Slider` with `IsRange`.
- **One value**: use `Slider`, or `RulerSlider` when the value needs fine control.
- **An exact number the person must type**: use `NumberField`.
- **A chart that people read but do not filter with**: use a chart from the Charts group.

## Usage

Give it `Label` and the raw values in `Data`. The control counts them into `Bins` bars. `Data` takes a list in code or
through a binding; assign a new list when the data changes, because edits inside the list are not observed.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RangeHistogramSlider Label="Price per night"
                              Data="{Binding Prices}"
                              LowerValue="{Binding From}"
                              UpperValue="{Binding To}"
                              Step="5" />
</UserControl>
```

`LowerValue` and `UpperValue` are two-way and may be null, which means the end of the scale. `RangeChanged` fires on
every change, and `ValueCommitted` fires once when a drag is released or a key changes the range, which is the moment to
run a search.

The scale comes from the data: `Minimum` is the smallest value rounded down and never above 0, and `Maximum` is the
largest value rounded up. Set them when the scale should stay the same while the data changes.

### Bar edges and the distance between thumbs

With `SnapToBins` on (the default), a released thumb that lands within 9 px of a bar edge settles on that edge, so a range
does not cut a bar in half. Turn it off when any value on the `Step` grid is as good as another.
`MinStepsBetweenThumbs` keeps the thumbs apart, counted in steps. It is 1 by default, so the thumbs never meet.

<Demo name="FreeThumbs" />

### Disabled

`IsEnabled="False"` keeps the bars and the range visible at half opacity and takes the thumbs out of the tab order.

<Demo name="Disabled" />

## API reference

<!-- api: RangeHistogramSlider -->

## Accessibility

- Each thumb is a slider with its own tab stop. They are named from `Label` as "Price per night, minimum" and "Price per
  night, maximum", unless `LowerThumbName` and `UpperThumbName` say otherwise.
- Each thumb reports the range it can reach, which stops at the other thumb, and speaks its value through
  `ValueFormatter`.
- The chart is decoration for assistive technology. The numbers it would give are in the thumbs and the readout.
- The focus ring appears for keyboard focus and after a key press.

### Keyboard

| Keys | Action |
|---|---|
| `Right` / `Up` | Raises the focused thumb by `Step` |
| `Left` / `Down` | Lowers the focused thumb by `Step` |
| `Shift` + arrow, `PageUp` / `PageDown` | Moves to the next bar edge in that direction |
| `Home` / `End` | Goes to the lowest or highest value this thumb can reach |
| A key at a limit | The value stays; the thumb strains toward the key and springs back |

## Motion

- The lit bars and the track fill share one clip, so they cut at the thumb on the same frame, even inside a bar.
- A held thumb grows to 1.18 times its size on the snappy spring. A hairline rises from it through the chart: it fades in
  over 160 ms and grows from 60% to full height over 240 ms.
- Past a limit the thumb gives up to 10 px and springs back. On release the speed may carry it one bar further, then the
  snappy spring takes it to its value and keeps that speed.
- A press away from the thumbs moves the nearest thumb there at once in value and with the snappy spring on screen.
- With reduced motion there is no stretch, spring or momentum: the thumb goes straight to its value, and the hairline
  appears at full height without a fade. With motion off everything is immediate.

## Responsive behavior

The control fills the width its parent gives it and has no breakpoints. The bars divide that width evenly with a 2 px gap
and may get narrow, so lower `Bins` in a tight column. The label is cut with an ellipsis before the readout is. In a parent
shorter than the control, the chart gives up height and the 36 px control row stays. Touch gets the same 44 px thumb target
as a mouse, and a press anywhere on the chart also works; the control does not need hover.

## Related

| Component | Choose it when |
|---|---|
| `Slider` | The range has no data to show, or there is one value |
| `RulerSlider` | One value needs fine control on a scale |
| `ElasticSlider` | One value is set by feel |
| `NumberField` | The person must type an exact number |
