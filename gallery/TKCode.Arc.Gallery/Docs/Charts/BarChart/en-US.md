---
title: Bar chart
summary: Compare one measure across days and scrub any bar for its value.
---

## When to use

- One measure compared across days, weeks or months, such as active minutes per day.
- A range where the average is a useful reference, shown as a dashed line.
- Categories that are discrete, such as deploys per week, where each bar stands on its own.

## When not to use

- A continuous trend over many points or several series: use `LineChart`.
- A compact trend beside a value: use `Sparkline`.
- A full year of daily activity as a grid: use `ActivityHeatmap`.

## Usage

The headline shows the average at rest and the value of the scrubbed bar while the pointer or the keyboard is on a bar.
`Period` sets the range shown under the headline.

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

### Ranges

Replace `ItemsSource` and `Period` when the range changes. Bars with the same key morph to their new height and
position, and new bars grow in beside them.

```csharp
void Week()
{
    var days = ChartsADemoData.Days(7);
    var values = ChartsADemoData.Daily(7, 46, 30, 3);
    chart.Period = "Sep 24–30, 2026";
    chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])
    {
        Label = day.ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
        AxisLabel = day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
    }).ToArray();
}
```

`ChartPoint.Label` is the full name read to assistive technology. `ChartPoint.AxisLabel` is the short text under the bar.

### Headline labels

`AverageLabel` names the resting headline and `ValueLabel` names the value of a scrubbed bar. Hide the average line
with `ShowAverage` when a reference is not useful.

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

### Axes

`ShowGrid`, `ShowXAxis` and `ShowYAxis` control the guides. `YMaximum` fixes the top of the value axis, and
`TickFormatter` formats the value-axis ticks.

```xml
<arc:BarChart Label="Tickets" YMaximum="100" ShowGrid="False" ItemsSource="{Binding Tickets}" />
```

### Reading values

`ActiveIndexChanged` reports the bar under the pointer or the keyboard. `Average` gives the mean of all values.

```csharp
chart.ActiveIndexChanged += (_, e) => status.Text = e.Index is null
    ? $"Average {chart.Average:N0}"
    : e.Label;
```

## API reference

<!-- api: BarChart -->

## Accessibility

- `Label` names the chart. The summary names the average, the highest and the lowest bar.
- The arrow keys move between bars, PageUp and PageDown move up to seven bars, Home and End go to the first and last bar,
  and Escape stops scrubbing and returns to the average.
- A hidden data table lists every value, so the chart can be read without seeing the bars.
- A change of range is announced politely.
- The bars are hidden from assistive technology. The scrub position is shown by a cursor, not by a focus ring.

## Motion

- On first view the bars grow in one after another, from the latest bar backwards.
- A change of range morphs the kept bars, the axis ticks and the average line to their new values.
- The scrub cursor glides between bars, and the headline value rolls to the new number.
- The headline label swaps between the average and the value label with a short blur.
- Reduced motion shows the bars at their final size and replaces the numbers in place. Off jumps straight to the final
  state.

## Related

- `LineChart` for a continuous trend over many points.
- `Sparkline` for a compact trend in a table cell or a card.
- `ActivityHeatmap` for a year of daily activity.
- `DonutChart` when the bars should show parts of a whole.
