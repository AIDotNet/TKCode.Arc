---
title: Metric explorer
summary: A row of KPI cards that each open into a chart you can inspect by dragging: the card's surface grows into the panel, the tabs morph the line from metric to metric point by point, the range switch moves a camera window over the daily data, and this period is drawn against the one before it.
---

<Demo name="Preview" />

## When to use

`MetricExplorer` is the top of a growth or finance page: four or so numbers that matter, each with its recent shape, and a
way to look closer without leaving the page. The cards show the latest value, the change over the selected range and a
sparkline. Opening one turns the card into a chart of the same metric, with tabs for the other metrics, a range switch and
a readout that follows the pointer.

- **KPI rows that need one more level of detail.** The chart answers "is this a blip or a trend?" with a previous-period
  line behind it, without a separate page.
- **Comparing periods.** Every day on the chart is compared with the same day one period earlier, so a scrub reads
  "Sep 6 vs Aug 7" and a change in the readout, not only a value.
- **Daily data of two or three kinds.** Money, counts and rates share one block: each metric chooses its format, its axis
  precision and whether a rise is good.

## When not to use

- **A single hero number.** Use a stat card; the opening chart is overhead for one value.
- **Dozens of metrics.** The row and the tab strip are designed for three to five metrics. Use a table or the
  `Metrics dashboard` block for more.
- **Data that is not daily.** The chart assumes one value per day and compares a range with the same number of days
  before it. Use `Revenue explorer` for brushing and zooming irregular series.
- **Exact lookups.** The readout shows one day at a time; put a grid beside the block when people need to copy numbers.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its content is a list of `ExplorerMetric`s. A metric has a `Title`
(the card's label and the readout's heading), a `ShortTitle` for the tab, a `Description` for the line under the chart,
and `Values`: a daily series, oldest first, whose last value belongs to `EndDate` (today when it is not set). The series
needs at least twice the longest range plus one day, because the chart draws each range against the one before it.

```xml
<arc:MetricExplorer Title="Workspace health" Caption="Refreshed Oct 14, 2026 at 08:30 UTC">
    <arc:ExplorerMetric Title="Annual run-rate" ShortTitle="ARR" ValueFormat="CurrencyCompact"
                        Values="{Binding Arr}" EndDate="{Binding Today}"
                        Description="Yearly value of active contracts, after discounts." />
</arc:MetricExplorer>
```

```csharp
explorer.Metrics.Add(new ExplorerMetric("Weekly active teams", teams, ExplorerValueFormat.Count) { ShortTitle = "Teams" });
explorer.Open(0);          // the surface grows out of the first card
explorer.SelectedRangeIndex = 1;   // 90 days
```

`MetricsSource` takes a collection you own instead (an `ObservableCollection` is followed). Changing a metric's
properties updates the block in place.

### Anatomy

- **Heading**: `Title` on the left and `Caption` on the right; the caption wraps under the title when there is no room.
- **Cards**: label, latest value, an arrow with the change and the range ("30 days"), and a sparkline ending in a dot.
  Two columns under 760 wide, four above. Hover darkens the edge; Enter, Space or a click opens the card.
- **Open chart**: metric tabs (with their values), a close button, the readout, the range switch, the legend with the
  date span, the chart and the metric's description.

### Value formats, changes and tones

`ValueFormat` chooses how a value is written: `Count` ("5,874"), `Currency` ("$12,480"), `CurrencyCompact` ("$4.62M",
"$412.00K"), `Percent` (a value in percent units, "27.3%") or `Ratio` ("1.12×"). `ValueFormatter` replaces it, and
`AxisFormatter` receives a tick's value and the step between ticks for the y labels. The y axis always has five ticks and
chooses the finest of 1, 1.5, 2, 2.5, 3, 4 and 5 times a power of ten that holds both lines with 3% of padding;
`MaxAxisDecimals` caps how many decimals a step may have (two for compact money, one for percentages, none for counts).

`ChangeMode` is `Relative` for the usual "+1.6%" or `Points` for rates ("+1.2 pts"). The arrow shows direction and the
color shows whether that is good: with `IsHigherBetter="False"` a rise turns red and a fall green, so a falling cost reads
as good news with a downward arrow.

<Demo name="Finance" />

### Ranges

A range is a window of days: 30, 90 and 364 by default (the buttons "30D", "90D" and "12M"). Create your own with
`Ranges` or `RangesSource`; each `ExplorerRange` has a `Label`, `Days`, the `CardText` the cards show ("30 days") and the
`CompareText` the readout shows ("vs 30 days ago"). `SparklineStep` is the number of days between sparkline points: 1 for
daily, 7 for a long range, so a year stays at 53 points. `SelectedRangeIndex` is two-way and the cards follow it, so the
chart you leave open decides what the collapsed cards say.

<Demo name="Ranges" />

`ShowPreviousPeriod` hides the dashed line and the second dot of the inspection.

### Inspecting a day

Move over the plot, press and drag, or touch it: the nearest day gets a crosshair, a dot on each line and a date capsule
on the axis. The readout's number, change and comparison switch to that day against the matching day one period
earlier ("Sep 6 vs Aug 7"), and the change digits roll. Leaving the plot clears the inspection; a finger lifting does too.

`ScrubIndex` is the inspected day (0 is the first day of the range) and `ScrubChanged` reports the date, the value and the
previous period's value. A click focuses the chart; with the keyboard, Tab onto the chart shows the last day and the
arrow keys walk through the days.

<Demo name="Events" />

### Narrow containers

The block reads its own width. Under 760 the cards take two columns. Under 640 the open chart tightens its paddings to 16,
drops the values from the tabs and puts the range switch under the readout. At 440 and under the cards get a two-line
label and a smaller number, the tabs share a full row, the close button moves down beside the readout and the x axis picks a
coarser ladder.

<Demo name="Compact" />

### Words and screen reader text

Every visible and spoken word is a property with an English default: the legend (`ThisPeriodText`, `PreviousPeriodText`),
the close button, the tab and range labels, the card name (`CardNameFormat`), the announcements (`AnnouncementFormat`,
`ClosedAnnouncementFormat`), the comparison while inspecting (`ScrubCompareFormat`) and the direction words (`UpText`,
`DownText`, `FlatText`).

## API reference

<!-- api: MetricExplorer -->

<!-- api: ExplorerMetric -->

<!-- api: ExplorerRange -->

<!-- api: ExplorerValueFormat -->

<!-- api: ExplorerChangeMode -->

<!-- api: MetricExplorerMetricEventArgs -->

<!-- api: MetricExplorerRangeEventArgs -->

<!-- api: MetricExplorerScrubEventArgs -->

## Accessibility

- Each card is a button named by its content: "Annual run-rate: $4.62M, up 1.6% over 30 days. Open chart".
- The tab strip is named "Metric" and the range switch "Chart range"; both are one tab stop that follows the selection.
- The chart is a slider named "Scrub" and the metric, whose value text is the inspected day with its previous-period
  counterpart ("Sep 6, 2026: $4.55M. Aug 7, 2026: $4.43M"). Focus draws a ring around the plot. Decorative parts (the
  sparklines, the axis, the legend) are hidden from assistive technology.
- Changing the metric or the range in the open chart is announced politely with the latest value and the change; closing
  the chart announces how many metrics are shown. Inspecting a day is not announced, because the slider already says it.
- Direction is always in words as well as color ("up", "down", "unchanged").
- Colors come from the theme tokens in light and dark; the line, dots and capsule keep their contrast on both.

### Keyboard

- **Tab** goes through the cards, then (when open) the selected tab, the close button, the selected range and the chart.
- **Enter / Space** on a card opens it and moves focus to its tab.
- **← →** on the tabs and on the range switch select the previous or next item at once and wrap; **Home / End** jump to
  the ends.
- **← ↓** and **→ ↑** on the chart step to the previous or next day and stop at the ends; **Home / End** jump to the first
  and last day.
- **Escape** clears the inspection first; pressing it again (or on any other control of the chart) closes the chart and
  returns focus to its card.

## Motion

- **Card to chart**: the card's surface travels and grows into the chart's rectangle on a smooth spring (about half a
  second) while the other cards drop away at once; the chart's content fades in with a small rise and blur, starting 120 ms
  into the flight. Closing runs it backwards: the surface shrinks into its card, the other surfaces fade back in, and the
  card contents rise in again (the opened card last). The block's height changes at once.
- **Range switch**: a camera over the daily data. One spring moves the number of days in view, and every frame the lines
  are rebuilt for that window, so the point count changes continuously while today stays pinned to the right edge; the y
  domain follows the same spring and the x labels slide with it. The label ladders (weekly, every two weeks, monthly)
  cross-fade as the old ladder leaves in 160 ms and the new one arrives in 240 ms.
- **Metric switch**: every point of the lines springs from where it is to its new place, so the curve flows from one
  metric into the next; switching again mid-way starts from the shape on screen. The y labels cross-fade one by one from
  the bottom, 35 ms apart. The readout lines rise and fade; the change digits roll like an odometer and the arrow turns.
- **Tabs and range switch**: the highlight glides on the same spring.
- **Inspection** appears, moves and disappears without animation.
- Reduced motion lands the surface, the lines and the highlights at once and keeps only 120 ms fades; Off applies
  everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown` and `Revenue explorer`, the other Analytics blocks around KPIs.
- `SegmentedControl`, the range switch the block uses.
- `Cohort retention`, another block that morphs between views of one dataset.
