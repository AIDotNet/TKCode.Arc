---
title: Metrics dashboard
summary: A one-screen analytics overview with a range switch, metric tabs that count to their totals, an inspectable area chart that can overlay the previous period, and two ranked lists.
---

<Demo name="Preview" />

## When to use

`MetricsDashboard` answers "how are we doing over the last N days?" in one compact surface. The header names what the
numbers are about and offers a 7D / 30D / 90D switch and an Export button. Under it, each metric is a tab with its total
for the range and the change against the period before; the selected tab drives the chart. Two ranked lists close the
block, for the top pages and the top sources or any other pair of breakdowns.

- **Product and marketing overviews.** Sessions, sign-ups, a conversion rate and a churn or bounce rate, with the pages and
  referrers that brought the traffic.
- **Usage summaries for a team or a customer.** Active teams, new projects, seat utilization and failed jobs, with the
  busiest workspaces and the plans they are on.
- **A landing view before a deeper report.** Use `PointInspected`, `SelectedMetricChanged` and `RangeChanged` to open a
  detail page for the day, the metric or the range someone is looking at.

## When not to use

- **Many series on one chart.** The chart draws one metric and, on request, its previous period. Use a line chart for
  several series side by side, or `Small multiples` for many.
- **Live, second-by-second data.** Every point is a day or a week. Use a streaming chart for live operational data.
- **Arbitrary date ranges.** The ranges are fixed lengths ending at `EndDate`. Use a date range picker and your own chart
  when people choose any start and end.
- **More than six metrics.** Four fill the row exactly, and the medium layout shows them two by two. Split a longer set
  across dashboards or use a table.

## Usage

The block derives from Avalonia's `TemplatedControl` and holds no sample data. Each metric is a
`MetricsDashboardMetric` with one value per day in `Daily`, the last value being `EndDate` (today when it is not set).
Give at least twice the longest range, so every range has a previous period to compare with; without one the change
line is hidden. List rows are `MetricsDashboardItem`s with one value per range in `Values`, largest first.

```xml
<arc:MetricsDashboard x:Name="Dashboard"
                      Title="Insights"
                      Source="northwind.dev"
                      PrimaryListTitle="Top paths"
                      SecondaryListTitle="Referrers"
                      ListValueHeader="Sessions" />
```

```csharp
Dashboard.EndDate = new DateOnly(2026, 10, 18);
Dashboard.Metrics.Add(new MetricsDashboardMetric { Label = "Sessions", Daily = sessions });
Dashboard.Metrics.Add(new MetricsDashboardMetric
{
    Label = "Churn risk", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = churn
});
Dashboard.PrimaryItems.Add(new MetricsDashboardItem { Label = "/", Values = [2602, 10840, 29268] });
```

`MetricsSource`, `PrimaryItemsSource` and `SecondaryItemsSource` take collections you own; an
`ObservableCollection` is followed, and changing a metric's or an item's properties updates the block, counting the
numbers to their new values.

### Counts and rates

A `Count` metric adds up: the total of a range or of a week is the sum of its days, and the change is relative ("11.7%").
A `Ratio` metric is a rate in percent units: the change is the difference in points ("0.1 pts"). Give `Numerators` and
`Denominators` (conversions and sessions, seats used and seats paid) and every week and range is the exact ratio of the
sums; without them it is the mean of the daily rates. `HigherIsBetter="False"` turns a fall green, for churn, bounce or
error rates. The arrow always shows the direction, and screen readers hear it in words.

### Ranges and weeks

`Ranges` (7, 30, 90) are the switch's segments, written with `RangeLabelFormat` ("{0}D") and named for screen readers
with `RangeNameFormat` ("Last {0} days"); `SelectedRange` is two-way. Ranges longer than `WeeklyThreshold` (60) are drawn
as weeks counted from the range's first day: 90 days give 13 points, and the last week has the 6 days left over. Its
tooltip says so ("Week of Oct 13 (6 days)"), because a short week is summed as it is and reads lower than the others.

### Narrow containers

The block reads its own width, not the window's. Under 640 the tabs go two by two and the lists stack; under 460 the range
switch spans the header, values drop to 18px, the plot is 168 high and the x axis shows at most four dates. When the
titles and the actions do not fit on one line, the actions move under the titles.

<Demo name="Narrow" />

### Other data, other words

Every text is a property with an English default: `SubtitleFormat` and `SubtitleRangeFormat`, `CompareText`,
`PreviousLegendText`, `PreviousValueFormat` ("{0} previous"), `DateFormat`, `WeekTitleFormat`, `PartialWeekTitleFormat`,
`ExportText`, `ExportedText`, the list titles and `ListValueHeader`, and the screen reader texts `RangeGroupName`,
`MetricGroupName`, `ChartNameFormat`, `ChartHelpText` and `ChangeContextText`. A metric can format its own numbers with
`ValueFormatter` (tab and tooltip), `AxisFormatter` (y axis) and `DeltaFormatter` (the change); `ListValueFormatter` does
the same for the lists. This demo uses 14, 28 and 84 day ranges, an exact seat-utilization ratio and a failed-jobs rate
with two decimals.

<Demo name="ProductUsage" />

### State and events

`SelectedMetricIndex`, `SelectedRange` and `IsCompareEnabled` are two-way, so the view can be saved and restored or
driven from elsewhere. `RangeChanged`, `SelectedMetricChanged` and `CompareChanged` report every change, and
`PointInspected` reports the day (or week), its value and, with Compare on, the previous period's value whenever the
inspection moves. `InspectPoint(index)` and `ClearInspection()` do from code what the pointer and the arrow keys do;
`ActiveIndex` tells which point is inspected.

<Demo name="Events" />

### Export

The button does not export anything by itself. `ExportCommand` runs with a `MetricsDashboardExportRequest` (the range in
days, its first and last day, the selected metric and whether the previous period is shown); when it cannot execute,
nothing happens. Then `ExportRequested` is raised and the button confirms with a check and `ExportedText` for
`ExportConfirmDuration` (1.8 seconds). `Export()` does the same from code; `IsExportVisible` hides the button.

<Demo name="Export" />

## API reference

<!-- api: MetricsDashboard -->

<!-- api: MetricsDashboardMetric -->

<!-- api: MetricsDashboardItem -->

<!-- api: MetricsDashboardExportRequest -->

<!-- api: MetricsDashboardPointEventArgs -->

## Accessibility

### Keyboard

- **Tab** moves through the range switch, Export, the metric tabs, Compare and the chart. The range switch and the
  tabs are one stop each, on the selected item.
- In the range switch, **Left** / **Right** (and **Up** / **Down**) move and select, wrapping at the ends; **Home** and
  **End** jump to the first and last range.
- In the metric tabs, **Left** / **Right** move and select, wrapping; **Home** and **End** select the first and last metric.
- **Space** toggles Compare. **Enter** or **Space** presses Export.
- On the chart, focus alone shows nothing. **Right** shows the last point and **Left** the one before it; then
  **Left** / **Right** step through the points and stop at the ends, **Home** and **End** jump to the first and last
  point, and **Escape** ends the inspection. Moving the focus away or the pointer out of the plot ends it too.

### Screen readers and focus

- The block is named by its title, which is a level 2 heading; the list titles are level 3 headings and name their
  sections.
- The range switch is a group named by `RangeGroupName`; its segments are toggle buttons named "Last 7 days" and so on.
- The metric tabs are tabs in a group named by `MetricGroupName`; each says its label, its total and its change in words,
  for example "Sessions, 36,686, up 11.7% versus the previous period", so the meaning does not rest on the color.
- The chart is an image named "Sessions, last 30 days" with `ChartHelpText` as its help text. The inspected point
  ("Sep 21, 1,370, 1,111 previous") is announced politely as it changes, from the keyboard or the pointer. The legend,
  axes, gridlines and the tooltip itself are not read separately.
- Each list row is read as "name, value"; long names are cut with an ellipsis and show in full as a tooltip.
- Export's confirmation is announced; the button's name follows its text.
- Focus rings are drawn for keyboard focus only: around the range segment and Export, inside the tab's edge (so a
  neighbour or the block's edge never hides it), around the 14px Compare box and around the plot.

## Motion

- **Range change**: the switch's thumb glides on a quick critically damped spring (about 330 ms); every tab total and list
  value counts from the old number to the new one in 480 ms (.22, 1, .36, 1), all at once; the chart cross-fades from the
  old picture to the new one in 240 ms. Axis labels, dates, the subtitle and the change lines switch at once.
- **Metric change**: the underline glides to the new tab on a smooth spring (about 530 ms), across rows in the 2 × 2
  layout; the chart cross-fades in 240 ms.
- **Compare**: the dashed previous period fades in and out on the smooth spring (about 700 ms); the box's colors change in
  160 ms.
- **Inspection**: the rule, the dot and the tooltip appear, move and disappear at once. The tooltip sits in the half of the
  plot away from the point and never covers it.
- **Export**: the texts cross-fade and the glyphs swap with a fade and a scale from .8 in 160 ms; hover tints the button
  and the tabs in 160 ms and pressing Export scales it to .97.
- Nothing animates when the block first appears. Reduced motion jumps the thumb and the underline, replaces numbers and
  the chart at once and drops the color transitions; Off applies every change at once.

## Related

- `KPI drilldown`, `Metric explorer` and `Revenue explorer`, the other Analytics blocks with metric cards and charts.
- `Small multiples` for many series at a glance.
- `SegmentedControl` for a standalone range switch.
