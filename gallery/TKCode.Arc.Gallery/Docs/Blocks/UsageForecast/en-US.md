---
title: Usage forecast
summary: A billing cycle against a budget: measured usage and a dashed forecast with its likely range, a budget line you drag or step with the keys while the headline, tiles, red overage and alert chips react, and a metric switch that morphs every path.
---

<Demo name="Preview" />

## When to use

`UsageForecast` answers "will we stay inside the budget this cycle, and when will we not?". It draws the days of a billing
cycle left to right: what was measured up to today as a solid polyline, the forecast as a dashed one, and a cone around the
forecast that widens the further it looks. A horizontal line is the budget. Where the forecast meets it, a marker names the
day, and every part of every path above the budget turns red.

- **Billing and plan pages.** Usage, forecast and projected overage in one view, with a budget the account owner can move to
  see what a different limit would mean.
- **Alerts and limits.** The 50, 80 and 100% chips say when each threshold was or will be reached and let the person choose
  which ones notify them.
- **Several meters, one story.** Calls, storage and seats share the block: the switch morphs the same chart into another
  meter with its own axis, budget range, unit and price.

## When not to use

- **Spend without a forecast.** For a plain history of one number, use a line chart; this block's value is the forecast and
  the budget.
- **Many metrics at once.** The block shows one meter at a time. Compare several meters in `Metrics dashboard` or
  `Small multiples`.
- **Cycles of hundreds of days.** One vertex is drawn per day and the date labels sit a week apart; the block is made for
  cycles of about a month.

## Usage

Give the block its cycle (`PeriodStart`, `PeriodDays`) and one `UsageMetric` per meter. A metric holds the `Readings` of
days 1 to today, a `StartValue` (0 for a running total; leave it out for a level such as storage in use), the fixed value
axis, the budget with its range and step, the unit and the price of one unit over the budget. Today is the number of
readings unless `TodayIndex` says otherwise. The forecast and its range are computed from the last two weeks when you do not
supply them.

```xml
<arc:UsageForecast Title="Usage outlook" Subtitle="Harborline Studio · Growth plan · Renews Jul 1" PeriodDays="30">
    <arc:UsageMetric Key="events" Label="Events" Readings="{Binding EventReadings}" StartValue="0"
                     AxisMaximum="40" BudgetMinimum="8" BudgetMaximum="40" BudgetStep="0.5" Budget="26"
                     Unit="M" UnitPrice="150" PriceNote="At $0.15 per 1k events" />
</arc:UsageForecast>
```

```csharp
forecast.PeriodStart = new DateOnly(2027, 6, 1);
forecast.Metrics.AddRange(metrics);
forecast.SelectedMetricKey = "database";
```

`MetricsSource` takes a collection you own instead (an `ObservableCollection` is followed). With one metric the switch is
hidden, and a `UnitPrice` of 0 hides the cost tile. `Budget` is two-way on every metric and keeps its value when you switch
away and back.

### Anatomy

- **Header**: `Title`, `Subtitle` and the metric switch on the right.
- **Headline**: one sentence in four forms (the budget was passed on a day, the forecast reaches it on a day, it reaches it on
  the last day, it stays under) and a line under it with the time left and the likely range. They are rewritten while you
  drag, and only the words that change animate.
- **Tiles**: used so far with its share of the budget, the forecast for the last day with its range, and the overage cost
  (red above zero, with the price note under it).
- **Chart**: six value ticks over a fixed axis, dates a week apart, a "Today" line, the cone, the forecast, the measured line,
  alert ticks, the crossing marker with its date pill, and the budget band.
- **Footer**: the alert chips and the legend.

### The budget line

The whole 32px band around the line takes the pointer. A drag is relative (pressing off the centre does not make the line
jump), the value snaps to `BudgetStep`, and beyond the budget range the line keeps following but is squeezed, then springs
back when you let go. Every step recalculates the headline, tiles, chips and the crossing; `BudgetChanged` is raised for every
step with `IsCommitted` false and once more with `IsCommitted` true on release or after a key, which is the moment to save:

```csharp
forecast.BudgetChanged += (_, e) => { if (e.IsCommitted) Save(e.Metric.Key, e.NewValue); };
```

`BudgetCommittedCommand` does the same from a binding, with the new budget as its parameter. `SetBudget` moves the line from
code with the same spring.

<Demo name="Events" />

### Alerts

`AlertThresholds` holds the chips; when you add none the block uses 50% (off), 80% and 100% (on). A threshold's `IsEnabled` is
two-way, its read-only `Status` is `AlreadyPast`, `Reached`, `Expected` or `NotExpected` and its `Date` the day. An enabled
threshold draws a tick where the line reaches that share of the budget, with its percent beside it. `AlertThresholdToggled`
reports each switch; the block never sends anything itself.

### Outcomes

<Demo name="StaysUnder" />

<Demo name="AlreadyPassed" />

Read-only properties give the same facts to the rest of your page: `Outcome`, `CrossingDay`, `CrossingEarly`, `CrossingLate`,
`ProjectedTotal`, `ProjectedLow`, `ProjectedHigh` and `OverageCost`.

### Inspecting a day

Point at the chart, or focus it and use the keys, to get a crosshair and a card for the nearest day: its date, whether it was
measured or projected, the value, and either the share of the budget or the likely range. The card sits to the right of the
crosshair and flips to the left near the edge. `InspectedDay` is two-way (null for none).

### Narrow containers

The block reads its own width. Under 560 the paddings tighten, the switch moves under the titles, the chart is 232 high and
the tiles tighten; under 440 the tiles stack with the value on the right and every chip fills a row. The headline size follows
the width between 18 and 22.

<Demo name="Flat" />

### Words, formats and dates

`IsFramed` false drops the card frame. `DateFormat` writes every date. A metric's `ValueFormatter`, `Unit`, `UnitSeparator`
and `CurrencyFormatter` control how values read; the tiles roll their digits from the decimals that `ValueFormatter` gives, so
it should return a plain number. Every sentence, label and spoken text is a property of `Labels`.

<Demo name="Words" />

## API reference

<!-- api: UsageForecast -->

<!-- api: UsageMetric -->

<!-- api: UsageAlertThreshold -->

<!-- api: UsageForecastLabels -->

<!-- api: UsageBudgetChangedEventArgs -->

<!-- api: UsageOutcome -->

<!-- api: UsageAlertStatus -->

## Accessibility

- The chart is a slider over the days: its name is a full sentence (the metric, used so far, the forecast, the budget and the
  headline), its value reads "Jun 21: 22.9M used, 88% of budget" or "Jun 27 projected: 29.4M, likely 28.2M to 31M", and its help
  text says how to use the keys.
- The budget band is a slider over the budget with the value as text and the hint "Drag the budget line or press the arrow
  keys."
- The headline and its line are read through a polite live region when the budget is settled, a metric is chosen or an alert is
  switched; the animated words themselves are hidden from screen readers.
- Alert chips are toggle buttons named with their percent and status ("80% of budget, hit jun 19").
- A hidden table lists every day with its reading and range, projected days marked.
- The overage turns red and also says "Overage" and the amount; the red part of the paths is repeated by the headline.

### Keyboard

- **Tab** visits the metric switch (arrow keys change the selection), the chart, the budget line and each chip.
- Chart: **← →** (or **↓ ↑**) one day, **Page Up / Page Down** one week, **Home / End** the first and last day, **Escape** hides the card.
  Focusing the chart shows today.
- Budget line: **↑ →** one step up, **↓ ←** one step down, **Page Up / Page Down** five steps, **Home / End** the lowest and highest budget.
- Chips: **Enter** or **Space** toggles.

## Motion

- **Budget line**: moves on the snappy spring after a key and follows a drag with a stiff spring; the red clip follows it. Past the range the line is squeezed and springs back.
- **Crossing marker, alert ticks**: they chase the intersection of the displayed budget and the displayed line along the polyline; the date pill fades in and out and nearby date labels fade as it passes.
- **Alert ticks** draw themselves bottom up (700 ms) and are wiped the same way; their labels fade.
- **Metric switch**: every vertex of the measured line, forecast and cone springs to the new metric at once, the budget line and ticks ride with them, value labels cross-fade, tile digits roll, the headline swaps only changed words (160 ms out, 240 ms in, 40 ms apart) and chip statuses morph letter by letter.
- **Chips**: the check opens its slot on the quick spring and the width follows.
- **Inspect card**: follows the crosshair on a stiff spring, fades in 160 ms.
- Reduced motion lands every spring at once and keeps only short fades (120 ms for words). Off applies everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown`, `Cohort retention`, the other Analytics blocks.
- `SegmentedControl`, the switch the block uses.
