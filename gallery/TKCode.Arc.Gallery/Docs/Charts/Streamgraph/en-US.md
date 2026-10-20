---
title: Streamgraph
summary: Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
---

## When to use

- The makeup of a total over time, such as support tickets per topic each week.
- Seasonality or spikes that are easier to see as flowing layers than as separate lines.
- Three to eight layers. Fold the smaller ones into an "Other" series before you pass them in.

## When not to use

- Exact values or comparisons between series: use `LineChart`.
- A few periods or discrete categories: use `BarChart`.
- Data with negative values: the layers stack from a baseline, so the stream does not read correctly. Use `LineChart`.

## Usage

Each `ChartSeries` is one layer. `ItemsSource` holds the shared x positions, such as the week starts. Hover or focus a
layer to isolate it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Streamgraph Label="Support tickets by topic"
                     Unit="tickets"
                     CategoryLabel="Week"
                     ItemsSource="{Binding Weeks}">
        <arc:Streamgraph.Series>
            <arc:ChartSeries Key="billing" Name="Billing" Values="{Binding Billing}" />
            <arc:ChartSeries Key="login" Name="Login" Values="{Binding Login}" />
            <arc:ChartSeries Key="performance" Name="Performance" Values="{Binding Performance}" />
        </arc:Streamgraph.Series>
    </arc:Streamgraph>
</UserControl>
```

### Ranges

Replace `ItemsSource` and the series values together. Every layer blends from its shape on screen to the new one, and
ranges of different lengths also morph.

```csharp
void Range(int weeks)
{
    var start = ChartsADemoData.Today.AddDays(-7 * (weeks - 1));
    chart.ItemsSource = Enumerable.Range(0, weeks).Select(w => start.AddDays(7 * w)).ToArray();
    for (var i = 0; i < series.Length; i++)
    {
        series[i].Values = ChartsADemoData.Daily(weeks, 60 - i * 9, 40, weeks * 7 + i, i % 2 == 0 ? 0.8 : -0.3);
    }
}
```

### Offset

`Offset` chooses how the layers stack: `Wiggle` (the default) minimizes the slope of the layers for a flowing stream,
`Silhouette` centers the stack around a flat middle, and `Zero` stacks the layers in order from a flat baseline.

```xml
<arc:Streamgraph Label="Topics" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

### Legend, direct labels and hidden layers

The legend and the direct labels name the layers. Direct labels are drawn on the thickest part of a layer when there is
room. `HiddenSeries` is two-way, so the view model can keep the hidden keys.

```xml
<arc:Streamgraph Label="Topics"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### Reading a week

`ActiveIndexChanged` reports the week under the pointer or the keyboard, and `ActiveSeriesKey` names the isolated layer.

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

## API reference

<!-- api: Streamgraph -->

## Accessibility

- `Label` names the chart. The plot is a focusable group: focus reads the latest week, and each readout announces the
  week, the value of the isolated layer and the total.
- The left and right arrow keys move along time, and the up and down arrow keys change the isolated layer. PageUp and
  PageDown move about a sixth of the range, Home and End go to the first and last week, and Escape clears the readout.
- The legend entries are toggles. Hovering or focusing an entry isolates its layer.
- A hidden data table lists every layer for every week.
- The layers are named on the plot, so they are not told apart by shade alone. A 2px gap of the surface color separates
  neighboring layers.

## Motion

- On first view the stream swells out from a hairline along its centre, on a long spring without overshoot.
- A new range or a new dataset blends every layer on a shared grid, so the layers keep their order and their speed.
- A hidden layer thins to nothing while the others re-stack around it.
- The crosshair and the tooltip slide on springs, and the other layers dim while one is isolated.
- Reduced motion shows the end state at once and keeps the crosshair and tooltip, without moving them. Off jumps to the
  end state.

## Related

- `LineChart` when the exact value of each series matters.
- `BarChart` for a few discrete periods.
- `Ridgeline` for the distribution of several groups rather than their totals over time.
- `DonutChart` for the share of each part in one total.
