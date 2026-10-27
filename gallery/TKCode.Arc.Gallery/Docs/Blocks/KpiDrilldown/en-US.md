---
title: KPI drilldown
summary: KPI cards with sparklines and period changes that grow in place into a detail view with a trend chart, a prior-period comparison, a readout you can drive by keyboard and a breakdown by dimension.
---

<Demo name="Preview" />

## When to use

`KpiDrilldown` is an overview that keeps its context. A grid of cards shows each metric's latest value, how it moved
against the previous period and the shape of the period; choosing a card grows it, in place, into the full story, and
Escape folds it back.

- **A finance or growth overview.** Revenue, accounts, retention, churn and conversion on one page, each one click from its
  chart and its split.
- **Period reviews.** One range switch (7, 30 or 90 days) moves every card and the open detail view together, and Compare
  puts the prior period behind the current one.
- **Where did the change come from?** The breakdown table splits the open metric by plan, region or channel, with each
  member's share, value and change.

## When not to use

- **Many metrics.** Six cards read at a glance; more than that belongs in a table or a metric picker.
- **Free exploration.** The detail view has one chart, one range and one split. For zooming, brushing or choosing series use
  a metric explorer.
- **Live streaming values.** The block is designed around daily series that change when a range, Compare or a dimension does.

## Usage

The block derives from Avalonia's `TemplatedControl`. It contains no data: give it `Metrics`, each a `KpiMetric` whose
`Values` are daily numbers ending on `EndDate` (at least twice the longest range, so the prior period exists), and
`Dimensions` for the breakdown with matching `KpiBreakdown`s on each metric.

```xml
<arc:KpiDrilldown Title="Overview" RangeDays="30">
    <arc:KpiDrilldown.Dimensions>
        <arc:KpiDimension Key="tier" Label="Tier" />
    </arc:KpiDrilldown.Dimensions>
    <arc:KpiMetric Key="arr" Label="Annual run rate" Format="CurrencyCompact" Description="Contracted yearly revenue." />
</arc:KpiDrilldown>
```

```csharp
var metric = new KpiMetric("refunds", "Refund rate", dailyValues)
{
    Kind = KpiKind.Rate,        // the change is a difference in points ("−0.2 pts")
    IsLowerBetter = true,       // a fall is green; the arrow still points down
    Format = KpiValueFormat.Percent,
};
metric.Breakdowns.Add(new KpiBreakdown("tier", [new KpiBreakdownRow("Team", teamValues)]));
```

All numbers are invented in the demos, from a fixed seed.

### What a card shows

The value is the series' last day and does not change with the range. The change compares the last day of the current
period with the last day of the prior one: a relative percent for `Total` metrics, a difference in points for `Rate`
metrics. It shows an arrow, a sign (a true minus) and a colour, and reads "unchanged" with a level arrow when it rounds to
zero. The sparkline is the current period resampled to 40 points, drawn straight and normalised to its own range.

### Detail view

The detail view shows the name, the value, the change and "vs … in the prior N days", the metric's `Description`, a 220px
trend chart (72 points; three gridlines carrying the highest, middle and lowest value, three dates), a legend and the
breakdown. Pointing at the chart snaps a cursor to the nearest point and shows a readout with the date, the value and, with
Compare on, the prior period's value for the same day.

<Demo name="Controlled" />

### Bound state and events

`SelectedKey`, `RangeDays`, `IsCompareEnabled` and `SelectedDimensionKey` are two-way. `Open(key)` and `Close()` do what
choosing a card and Escape do. `SelectedChanged`, `RangeChanged`, `CompareChanged` and `DimensionChanged` report what the
person did, and `SelectedCommand` runs when a detail view opens (parameter: the metric) or closes (parameter: null).

<Demo name="FourMetrics" />

### Fewer metrics, other ranges

`Ranges` sets the range switch's days; with no `Dimensions` the breakdown is left out, and `IsCompareEnabled` starts the
block with the prior period hidden. `MetricsSource` follows a list you own.

<Demo name="Narrow" />

### Narrow containers

The block reads its own width: three columns from 760, two from 520, and below 520 one column, a 22px value, an icon-only
Compare button and a breakdown without the share column.

### Words

Every word besides the data comes from `Labels` (`KpiDrilldownLabels`): the Compare text, range names, the legend, the
"vs" line, the tooltip's prior line, the cards' spoken names and the chart's summary.

## API reference

<!-- api: KpiDrilldown -->

<!-- api: KpiMetric -->

<!-- api: KpiBreakdown -->

<!-- api: KpiBreakdownRow -->

<!-- api: KpiDimension -->

<!-- api: KpiDrilldownLabels -->

<!-- api: KpiKind -->

<!-- api: KpiValueFormat -->

<!-- api: KpiSelectionChangedEventArgs -->

<!-- api: KpiRangeChangedEventArgs -->

<!-- api: KpiCompareChangedEventArgs -->

## Accessibility

### Keyboard

- **Tab** reaches Compare, the range switch (one stop; the arrows move the selection, Home and End jump) and each card.
- **Enter** or **Space** on a card opens it and moves focus to the close button.
- In the detail view Tab reaches the close button, the chart and the dimension switch; the cards behind are skipped.
- On the chart **←** and **→** move the cursor by one day (the first press shows the last day), **Home** and **End** jump to
  the first and last day, and the cursor disappears when the chart loses focus.
- **Escape** closes the detail view from anywhere on the page and returns focus to the card.

### Screen readers

- Each card is one button named by its label, value and change in words ("Refund rate, 2.6%, down 0.2 pts against the prior
  30 days. Show details"). The cards are hidden from assistive technology while a detail view is open.
- The chart is an image named by a summary of its start and end; moving the cursor by keyboard is announced politely with the
  date and value.
- The breakdown is a labelled section with one named row per member; the change uses an arrow or sign as well as colour.
- Compare keeps its name when it shrinks to an icon. Focus rings are 2px, outside the element, and only show for the keyboard.

## Motion

- **Opening**: the card hides and the panel grows from its box to the card area (inset 8) on one spring, real width, height
  and position (about 0.5s visual duration, no bounce); the content keeps its final layout and is clipped. The veil fades in
  over 240 ms, the body fades in after 120 ms over 480 ms and the surface warms over 480 ms. The area's height follows.
- **Closing**: the body fades in 120 ms, the shadow in 480 ms, and the panel folds back into the card on the same spring
  before the card reappears; focus returns to the card.
- **Range, Compare, dimension**: every sparkline and chart line morphs point by point on a smooth spring, the axis labels
  show the interpolated numbers, every number rolls digit by digit, the prior line fades in 240 ms, texts cross-fade (out
  80 ms, then in 240 ms), and the share bars spring.
- Reduced motion shows the panel at its final size with a 160 ms fade; morphs, bars and numbers jump. Off applies everything
  at once.

## Related

- `SegmentedControl` for the switches in other blocks.
- `Metric explorer` and `Revenue explorer`, the other Analytics blocks that go deeper into one number.
- `Metrics dashboard` for a row of KPIs without drilldown.
