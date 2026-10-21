---
title: Bar chart
summary: Compare one measure across days and scrub any bar for its value.
---

<Demo name="WeekAndMonth" />

## When to use

`BarChart` compares one measure across a run of days, weeks or months and lets the reader scrub any bar for its exact
value. The headline holds the average at rest, so the first thing a reader sees is the level of the measure, and the
bars show how each period compared with it. It fits a panel on an analytics page or an operations dashboard where the
question is "which days were unusual" rather than "how does this trend continue".

- **Daily active minutes in a product analytics page**: a product team compares active minutes per day across a week
  against the daily average, and scrubs the Thursday bar to see the exact total.
- **Deploys or releases per week in an engineering console**: each bar is a week that stands on its own, so the team
  reads which sprint shipped the most without a line implying a continuous trend.
- **Support tickets per day in a help desk dashboard**: a queue lead sees the spike on Monday and reads the count
  behind it, with the average line showing what a normal day looks like.
- **Revenue or order volume by month in a finance review**: a reference line for the monthly average makes the months
  above and below it obvious, and the value axis can be fixed so two periods are drawn to the same scale.
- **Usage per billing period in a metered-service console**: API calls or storage used per day, with the total for the
  period in the headline when the reader scrubs a bar.

## When not to use

- **A continuous trend over many points or several series**: use `LineChart`, which draws the line through the points
  and can show several series with a legend.
- **A compact trend beside a single value**: use `Sparkline`, which fits in a table cell or a KPI card.
- **A full year of daily activity as a grid**: use `ActivityHeatmap`, where each day is a square and the rhythm of weeks
  is the point.
- **Parts of one whole, such as traffic sources or spend categories**: use `DonutChart`, where the segments add up to a
  total.
- **A single value against a target or a limit**: use `Gauge` or `UsageMeter`, which show how full a measure is.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BarChart Label="Active minutes"
                  Period="Sep 24–30, 2026"
                  Unit="min"
                  CategoryLabel="Day"
                  ItemsSource="{Binding Week}" />
</UserControl>
```

`BarChart` derives from `ChartBase`, the base of Arc's self-drawn charts. The base owns the data pipeline, the
palette, the first-view reveal, the readout keyboard model, the accessible summary and the hidden data table, so every
Arc chart reads and scrubs the same way. The bar chart adds the headline, the average line and the bar geometry on top.
The whole plot is drawn in one pass, which keeps a week or a month of bars cheap to update.

### Anatomy

1. **Headline kind** (`AverageLabel`, `ValueLabel`): the small label above the number. It reads "Daily average" at rest and
   "Total" while a bar is scrubbed. It sits in the secondary text color at the small type size.
2. **Headline value**: the average at rest, or the scrubbed bar's value, in the display font at the 3XL size with medium
   weight. Its digits roll to the new number. `Unit` follows the number, lifted onto its baseline, in the secondary color.
3. **Period and readout** (`Period`): under the headline, the range shown at rest ("Sep 24–30, 2026") and the scrubbed
   bar's full label ("Tuesday, Sep 22") while a bar is active. The row keeps its height so swaps never move the plot.
4. **Plot**: one bar per data point. A bar fills 58% of its slot and is at most 28 px wide. Wide containers gain spacing
   between the bars instead of thicker bars.
5. **Average line** (`ShowAverage`): a dashed reference line at the average, drawn across the plot.
6. **Value axis gutter**: 52 px on the right, holding the value ticks and a short "Avg" label for the average.
7. **Category axis row**: 26 px under the bars, holding the short axis labels (`AxisLabel`, or the date's short form).
8. **Gridlines** (`ShowGrid`): horizontal guides at the value ticks, drawn behind the bars.

The headline and the plot are separated by 20 px. The plot height defaults to 176 px and is set with `PlotHeight`; the
width follows the container.

### Ranges

Replace `ItemsSource` and `Period` together when the range changes. A bar keeps its identity across the change when
its key is the same, so a day that stays in the range morphs to its new height and position, and a new bar grows in
beside it. The key defaults to the category value, so a date keeps its key; set `ChartPoint.Key` yourself when the
category is not unique or not stable.

```csharp
void ShowWeek()
{
    var culture = CultureInfo.GetCultureInfo("en-US");
    var start = new DateOnly(2026, 9, 24);
    int[] minutes = [46, 30, 52, 61, 38, 27, 44];
    chart.Period = "Sep 24–30, 2026";
    chart.ItemsSource = Enumerable.Range(0, minutes.Length).Select(i =>
    {
        var day = start.AddDays(i);
        return new ChartPoint(day, minutes[i])
        {
            Label = day.ToString("dddd, MMM d", culture),
            AxisLabel = day.ToString("ddd", culture)
        };
    }).ToArray();
}
```

`ChartPoint` is a record with the category, the value and optional text. `ChartPoint.Label` is the full name read to
assistive technology and shown in the readout. `ChartPoint.AxisLabel` is the short text under the bar; leave it null
to keep the axis quiet at that bar. The readout and the announcement use the same full names, so a range change tells the
reader the new period and the new average at once.

Set `IsLoading` while the next range is fetched. The current shapes stay on screen, dimmed and breathing, until the new
data arrives, so the page does not flash empty between two ranges.

### Headline labels

`AverageLabel` names the resting headline and `ValueLabel` names the value of a scrubbed bar. The defaults are "Daily
average" and "Total". Change them to the measure's own vocabulary, such as "Weekly average" for weekly bars, and hide the
average line with `ShowAverage` when a reference is not useful for the reader.

<Demo name="WithoutAverage" />

```xml
<arc:BarChart Label="Deploys"
              Period="Last 12 weeks"
              AverageLabel="Weekly average"
              ValueLabel="Deploys"
              CategoryLabel="Week"
              ShowAverage="False"
              PlotHeight="140"
              ItemsSource="{Binding Weeks}" />
```

`Average` is the mean of all values and is read-only, so a caption or a status line can show the same number as the
headline. The average line itself is a reference drawn from the data, not a target.

### Value formatting and axes

`ValueFormat` takes a .NET format string in en-US, such as `N0` for grouped whole numbers, `C0` for currency or
`0.0'%'` for a percent with one decimal. A `ValueFormatter` receives the value with its index and series and wins over
`ValueFormat` when both are set, which is the right choice when the label depends on the bar, such as "3 incidents, 1
critical". The `Unit` text is appended to readouts such as "50 min" and to the data table.

`ShowGrid`, `ShowXAxis` and `ShowYAxis` control the guides and the labels. `YMinimum` fixes the bottom of the value axis
and `YMaximum` fixes its top. By default the axis starts at zero, or at the lowest value when data dips below zero, and
ends at the next nice tick above the largest value. Fix `YMaximum` when two charts on one page must share a scale, and
`TickFormatter` formats the tick labels when they need a different format from the values.

```xml
<arc:BarChart Label="Tickets" YMaximum="100" ShowGrid="False" ItemsSource="{Binding Tickets}" />
```

### Reading values

`ActiveIndex` is the bar under the pointer or the keyboard, or null at rest, and it is two-way. `ActiveIndexChanged`
reports every move, with the index, the item, the label and the values at that point. Use it to show a value in a
status line outside the chart.

```csharp
chart.ActiveIndexChanged += (_, e) => status.Text = e.Index is null
    ? $"Average {chart.Average:N0}"
    : e.Label;
```

`Summary` is the one-paragraph description for assistive technology, such as "Active minutes, Sep 15–21. Daily average
40 min. Highest Thursday, 60 min. Lowest Wednesday, 20 min." `DataTableText` is the same data as a tab-separated table,
with a header line and one line per bar, for example "Day\tActive minutes" followed by "Mon\t10 min". The table is
hidden from sight and available to assistive technology, so the chart can be read without seeing the bars.

### Data shape and performance

The chart reads its data once per change of `ItemsSource`, not on every redraw. Use `ChartPoint` for data that is
already in memory: `new ChartPoint(category, value)` takes an object for the category, so a `DateOnly`, a `DateTime`, a
string or a number all work, and the default axis labels format dates as "Sep 15" and full labels as "Tue, Sep 15".
When the data arrives as rows, read it with `XSelector` and `YSelector` or with `XBinding` and `YBinding`. The compiled
binding form, such as `YBinding="{CompiledBinding Visits, DataType=local:Day}"`, is the AOT-safe path.

A week or a month of bars is well within what the chart handles smoothly. For a long series prefer a `LineChart`, or
aggregate the data into weeks, because the bars become thin and the category labels thin out. The hidden data table
grows with the number of bars, so keep the range to what a reader can scan.

### Example: weekly active minutes for a product team

A product analytics page for the week of September 24 to 30, 2026. The headline shows the daily average, the period sits
under it, and the bars use short weekday labels on the axis. The team reads that Thursday was the peak and Wednesday
the trough, which is what the summary line also says. The screenshot is not shown here because the bars and the
headline number are drawn from code-supplied data (see the Demo above for a live example); the XAML needs the same
`ItemsSource` as the code sample.

```xml
<arc:BarChart Label="Active minutes"
              Period="Sep 24–30, 2026"
              Unit="min"
              CategoryLabel="Day"
              ItemsSource="{Binding ActiveMinutesThisWeek}" />
```

### Content guidelines

- **Label**: name the measure in sentence case, such as "Active minutes" or "Support tickets". It names the chart, the
  summary and the data table, so it must make sense on its own.
- **Period**: a readable range with the year, such as "Sep 24–30, 2026" or "Last 12 weeks". The period is announced
  when the range changes, so it must be specific.
- **Unit**: a short unit after the value, such as "min", "ms" or "calls". Write the full word in the label when the unit
  is long.
- **Average and value labels**: use the measure's vocabulary: "Weekly average" for weekly bars, "Deploys" for a count.
- **Category label**: the singular noun for one bar, "Day", "Week" or "Month", used as the column name in the data table.

| Instead of | Write |
|---|---|
| Data | Active minutes |
| Avg | Daily average |
| Sep 24 - 30 | Sep 24–30, 2026 |
| Items (count) | Support tickets |
| Value | Total |

### Best practices

**Do**

- Give every chart a `Label` and a `Period`. The label names the chart for screen readers and the period is announced
  when it changes, so a reader always knows what the bars count and over which days.
- Keep the bars in one range and one unit per chart. Mixing minutes and calls in one chart makes the average meaningless.
- Set `YMaximum` when two charts must be compared side by side, so the same height means the same value on both.
- Set `Key` on `ChartPoint` when the category is not a stable identifier, so the bars morph across a range change
  instead of being redrawn.
- Use `ActiveIndexChanged` to show the scrubbed value outside the chart when the headline is not enough.

**Avoid**

- Drawing a line through unrelated categories. A bar per category keeps the reader from reading a trend between days
  that have nothing to do with each other.
- Hiding the average line when the average is the story. Leave `ShowAverage` on when the reader asks whether a day was
  above or below normal.
- Using a bar chart for a long series of bars. The bars become too thin to scrub, and a line or an aggregated
  view reads better.
- Changing `ItemsSource` many times a second. Each change morphs the bars and the average line, so frequent changes make
  the chart move without telling the reader anything.
- Relying on the colors of the bars alone. The headline, the readout and the data table carry the values, so the
  chart works for readers who cannot see the bar heights.

### Common questions

**Why does the headline show the average and not the latest bar?** At rest the headline is the average, which is the
reference for the whole range. The headline changes to the scrubbed bar's value while the pointer or the keyboard is on a
bar, and it returns to the average when the readout is released with Escape or when the focus leaves the plot.

**How do I set the scrubbed bar from code?** `ActiveIndex` is two-way: setting it moves the headline and the readout to
that bar, and setting it to null returns the headline to the average. Use it to restore a selection or to link the chart
with a list that the reader picks from.

**Why are the bars not the same width when I resize the page?** The bar width is capped at 28 px and fills 58% of its
slot. On a wide plot the extra width becomes spacing between the bars, so bars stay the same thickness and the gaps grow.

**Why does the first view animate and later changes not?** The bars grow once, left to right, the first time the chart
is seen. Later changes morph the bars from their current heights to the new ones, so a reader sees the change, not the
initial growth again.

**How do I format the value in the readout differently from the axis?** Set `ValueFormat` or `ValueFormatter` for the
values and `TickFormatter` for the value axis. The two are separate, so a value such as "1,284 min" can read in full
while the axis uses a compact format such as "1.3K".

## API reference

<!-- api: BarChart -->

## Accessibility

- `Label` names the chart. The summary names the average, the highest and the lowest bar.
- The arrow keys move between bars, PageUp and PageDown move up to seven bars, Home and End go to the first and last bar,
  and Escape stops scrubbing and returns to the average.
- A hidden data table lists every value, so the chart can be read without seeing the bars.
- A change of range is announced politely.
- The bars are hidden from assistive technology. The scrub position is shown by a cursor, not by a focus ring.
- The plot is a focusable element that assistive technology reads as a slider, so each bar's full label and value are
  read as the readout moves.

### Keyboard

The plot takes focus as one tab stop. Keyboard focus starts on the latest bar, so the most recent value is read first.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus into the plot on the latest bar, or out of it |
| `Left` / `Right`, `Down` / `Up` | Move to the previous or next bar; the first press from rest starts from the latest bar |
| `PageUp` / `PageDown` | Move up to seven bars earlier or later |
| `Home` / `End` | Move to the first or the last bar |
| `Escape` | Stop scrubbing and return the headline to the average |

Losing focus clears the readout, so the headline returns to the average when the reader leaves the chart.

## Motion

- On first view the bars grow in one after another, from left to right.
- A change of range morphs the kept bars, the axis ticks and the average line to their new values, and the bars that
  leave sink into the baseline.
- The scrub cursor glides between bars, and the headline value rolls to the new number.
- The headline label swaps between the average and the value label with a short blur.
- Reduced motion shows the bars at their final size and replaces the numbers in place. Off jumps straight to the final
  state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Bars grow in a left-to-right wave | Bars at their final size | Immediate |
| New range | Kept bars morph, new bars grow beside them, leaving bars sink | Bars at their final size | Immediate |
| Scrubbing a bar | The cursor glides to the bar and the headline value rolls | The headline value is replaced in place | Immediate |
| Headline label | Swaps between the average and the value label with a short blur | Replaced in place | Immediate |

Motion here follows the data. A bar that keeps its key morphs, so the reader can follow one day across two ranges. Keep
the first-view wave for the chart that is the reason for the section; on a dashboard that updates often, the morphs on
each refresh add up, so refresh the data on a schedule that matches how often the numbers change.

## Related

- `LineChart` for a continuous trend over many points.
- `Sparkline` for a compact trend in a table cell or a card.
- `ActivityHeatmap` for a year of daily activity.
- `DonutChart` when the bars should show parts of a whole.

| Component | Choose it when |
|---|---|
| `LineChart` | The trend over many points, or several series, matters more than single periods |
| `Sparkline` | A compact trend sits beside a number in a table or a card |
| `ActivityHeatmap` | A full year of daily activity is shown as a grid of squares |
| `DonutChart` | The values are parts of one whole that add up to a total |
| `AnimatedCounter` | A single changing number is the whole message, without a chart |
