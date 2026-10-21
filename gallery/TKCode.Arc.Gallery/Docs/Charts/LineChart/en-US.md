---
title: Line chart
summary: A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
---

<Demo name="Ranges" />

## When to use

- One to three measures over time, such as signups or revenue per day.
- A current period compared with a previous period or a target, using a dashed series.
- Ranges such as 7, 30 and 90 days that should morph the same lines instead of redrawing them.

## When not to use

- A compact trend next to a single value: use `Sparkline`.
- Discrete categories such as days of the week or regions: use `BarChart`.
- Parts of a whole: use `DonutChart` or `WaffleChart`.

## Usage

Each `ChartSeries` has a stable `Key`, so the lines keep their identity when the data changes. Set `Label` to name the
chart for assistive technology.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:LineChart Label="Signups" CategoryLabel="Date" ItemsSource="{Binding Days}">
        <arc:LineChart.Series>
            <arc:ChartSeries Key="current" Name="This period" Values="{Binding Current}" />
            <arc:ChartSeries Key="previous" Name="Previous period" Values="{Binding Previous}" IsDashed="True" />
        </arc:LineChart.Series>
    </arc:LineChart>
</UserControl>
```

### Ranges

Replace `ItemsSource` and the series values when the range changes. Every line morphs from the shape on screen, and the
value axis springs to the new range.

```csharp
void Range(int days)
{
    chart.ItemsSource = ChartsADemoData.Days(days);
    current.Values = ChartsADemoData.Daily(days, 420, 160, days, 2.4);
    previous.Values = ChartsADemoData.Daily(days, 380, 140, days + 11, 1.1);
}
```

### Single series and units

A list of `ChartPoint` items needs no series. `Unit` is shown in the tooltip, and `Curve` switches from the default
smooth curve to straight segments.

<Demo name="SingleSeries" />

```xml
<arc:LineChart Label="p95 latency" Unit="ms" Curve="Linear" PlotHeight="180" ItemsSource="{Binding Latency}" />
```

### Legend and hidden series

With more than one series the legend shows a toggle per series. `ShowLegend` forces it on or off. Bind
`HiddenSeries` to keep the hidden keys in your view model.

```xml
<arc:LineChart Label="Signups"
               ItemsSource="{Binding Days}"
               ShowLegend="True"
               HiddenSeries="{Binding HiddenKeys}" />
```

### Loading and empty states

`IsLoading` dims the lines and shows a skeleton when there are no points. `EmptyLabel` explains an empty range.

<Demo name="Empty" />

```xml
<arc:LineChart Label="Refunds" EmptyLabel="No refunds in this range" IsLoading="{Binding IsBusy}" />
```

### Reading values

`ActiveIndexChanged` fires while the crosshair moves and when it is released. Use it to show the values in a title.

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
    ? "Hover or use the arrow keys to read values."
    : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
```

## API reference

<!-- api: LineChart -->

## Accessibility

- `Label` names the chart. The plot exposes the range and the latest value in a hidden summary, and a hidden data table
  lists every point.
- The plot is a focusable slider. Arrow keys move one point, PageUp and PageDown move about a sixth of the range, Home
  and End go to the first and last point, and Escape releases the crosshair.
- The legend entries are toggle buttons. Each series is identified by its name and its line style, not only by color.
- The loading state is announced politely as "Loading". The lines and the tooltip are hidden from assistive technology
  to avoid repeated reading.

## Motion

- On first view the lines draw in from left to right, and the area fill is revealed with the line.
- A new range resamples the lines on shared x positions and morphs them from their current shape. The gridlines slide
  with the value axis.
- The crosshair glides between points, and the tooltip follows on its own spring and flips at the edges.
- Loading lines pulse until the data arrives. The pulse pauses when the chart leaves the viewport.
- Reduced motion skips the draw-in, the morph and the glide, and stops the loading pulse. Off jumps straight to the
  final state.

## Related

- `Sparkline` for a compact trend beside a value.
- `BarChart` for one measure compared across discrete days, weeks or months.
- `BrushChart` for a dense time series that needs zooming.
- `Streamgraph` when several series should be read as stacked layers.
