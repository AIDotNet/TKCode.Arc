---
title: Split bar
summary: One bar split into parts by their share of a total, with a legend of labels, values and percentages; the parts spring to their new shares as the data changes.
---

<Demo name="Storage" />

## When to use

`SplitBar` answers "what is this total made of?" in one line. It draws a single horizontal bar cut into parts, each
as wide as its share of the total, with a small gap between them, and a legend underneath that names each part with
its value and its percentage. The total can be the sum of the parts, or a fixed amount such as a disk size or a
budget, in which case the rest of the bar stays empty and the legend names it ("Free", "Remaining"). When a value
changes, a part is added or one is removed, every part springs to its new width; the springs keep their speed when
new data arrives mid-motion, so a live feed never makes the bar jump.

- **Usage and quotas**: storage by file type, seats by plan, tokens by model, with the free space left over.
- **Budgets and costs**: spending by category against a monthly budget.
- **Test and job results**: passed, skipped and failed, colored by meaning.
- **Live mixes**: traffic by source or load by region, updated every few seconds.

## When not to use

- **Comparing parts across several totals**: stacked bars in a `BarChart` show many totals side by side.
- **Many small parts**: past six or seven parts the slivers and the legend get hard to read. Group the long tail into
  "Other".
- **Exact reading of each value**: when people need the numbers more than the proportions, use a `Table` or a list.
- **Progress toward one goal**: a `Progress` bar says "how far" more plainly than a split.

## Usage

Add `SplitBarSegment` items with a `Label` and a `Value`. Without `Total` the full bar is the sum of the parts.

```xml
<arc:SplitBar Title="Storage" Total="256" ValueDecimals="1" ValueSuffix=" GB" RemainderLabel="Free">
    <arc:SplitBarSegment Label="Photos" Value="82.4" />
    <arc:SplitBarSegment Label="Apps" Value="46.1" />
    <arc:SplitBarSegment Label="Documents" Value="21.7" />
</arc:SplitBar>
```

The header shows `Title` and the rolling sum ("168.2 GB of 256 GB"); `IsSumVisible="False"` hides the sum and
`IsLegendVisible="False"` hides the legend. Values are formatted with `ValuePrefix`, `ValueDecimals` and
`ValueSuffix`; `FormatValue(value)` gives the same text for your own labels.

### Live data

Bind `ItemsSource` to a collection of segments and change their `Value` in place: the bar listens to each segment
while it is on screen. Adding or removing a segment grows it in from nothing or shrinks it out where it was.

<Demo name="LiveShares" />

```csharp
foreach (var segment in segments)
{
    segment.Value = Math.Round(segment.Value * 1.1);
}
```

### Highlight and invoke

Pointing at a part, or at its legend entry, lifts it and dims the others; `HighlightedIndex` reports it and can be
bound. A click, or Enter on the focused bar, raises `SegmentInvoked` with the `Segment`, its `Index` and its `Share`.

### Colors

A segment without a `Brush` takes the chart series colors in order (series 1 to 4, then lighter rounds of them).
Give each part a `Brush` when the colors carry meaning, as in the results bar below.

<Demo name="Compact" />

## API reference

<!-- api: SplitBar -->

<!-- api: SplitBarSegment -->

<!-- api: SplitBarSegmentEventArgs -->

## Accessibility

- The control is a group named by `Title` ("Split bar" without one). The bar itself is one tab stop whose accessible
  name lists every part ("Photos, 82.4 GB, 32 percent; ..."), or only the highlighted part while one is highlighted.
- Left and Right (or Up and Down) move the highlight, Home and End jump to the first and last part, Enter or Space
  invokes the highlighted part and Escape clears it. The keyboard ring is drawn around the highlighted part.
- The legend repeats every label and value in text, so color is never the only way to tell parts apart.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| New shares | Every part springs to its width (about 0.45 s, a little overshoot, speed kept when interrupted) | Jumps | Jumps |
| Added / removed part | Grows from nothing / shrinks out where it was | Appears / disappears | Appears / disappears |
| Highlight | Other parts dim over 160 ms | Dim at once | Dim at once |
| Legend values | Digits roll | Change at once | Change at once |

The first data shows at once; only later changes animate. The frame loop runs only while a share is moving.

## Responsive behavior

- The bar fills its width; the legend entries wrap onto more lines when they do not fit, and long labels are cut with
  an ellipsis.
- The header puts the sum on the right of the title; give the control at least 240 px so both fit.
- On touch screens there is no hover highlight; a tap invokes the part under the finger.

## Related

| Component | Choose it when |
|---|---|
| `BarChart` | You compare several totals or a series over time |
| `Progress` | You show how far one task has come |
| `MetricTicker` | You show several live numbers rather than parts of one total |
