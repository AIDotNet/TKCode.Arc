---
title: Ridgeline
summary: Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
---

## When to use

- The shape of several distributions side by side, such as daily highs per month or latency per region.
- Spread, skew and a second hump that a single summary number would hide.
- Up to about twenty groups, each with enough observations to draw a smooth curve (at least 20 values).

## When not to use

- One summary number per group: use `BarChart`.
- A few points per group or exact values: use `BarChart` or a table.
- A change over time of one value: use `LineChart`.

## Usage

Each `RidgelineSeries` is one group: an `Id`, a `Label` and its raw `Values`. The chart computes a density for each
group and draws the ridges from top to bottom in the order of the series.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Ridgeline Label="Daily highs in Zurich"
                   Unit="°C"
                   DomainMinimum="-5"
                   DomainMaximum="35"
                   ItemsSource="{Binding Months}" />
</UserControl>
```

### Datasets

Replace `ItemsSource` to switch the dataset. Every curve morphs from its current shape to the new one, and the ridge
order stays the same.

```csharp
zurich.Click += (_, _) => chart.ItemsSource = Zurich;
lisbon.Click += (_, _) => chart.ItemsSource = Lisbon;
```

### Items and value formats

Add groups through `Items` when they are created in code. `ValueFormatter` formats the values on the axis and in the
readout.

```csharp
var latency = new Ridgeline
{
    Label = "Response time by region",
    ValueFormatter = v => $"{v:0} ms",
    IsTinted = false,
    RowHeight = 36,
    Overlap = 1.8
};
latency.Items.Add(new RidgelineSeries("eu", "Europe", europeValues));
latency.Items.Add(new RidgelineSeries("us", "Americas", americasValues));
```

### Overlap and smoothness

`Overlap` sets how far the ridges rise into the row above, as a multiple of the row height. `RowHeight` sets the height of
one row. `Bandwidth` sets the smoothing in the units of the values. By default each group uses the Silverman rule.

```xml
<arc:Ridgeline Label="Latency" Overlap="2.4" RowHeight="30" Bandwidth="4" ItemsSource="{Binding Regions}" />
```

### Tint and domain

`IsTinted` colors each ridge by its median, from a light tint to the first series color, and shows a color scale below
the axis. `DomainMinimum` and `DomainMaximum` fix the value axis, so it does not move when the dataset changes.

```xml
<arc:Ridgeline Label="Latency" IsTinted="False" DomainMinimum="0" DomainMaximum="300" ItemsSource="{Binding Regions}" />
```

### Reading a ridge

`ActiveId` is the lifted ridge, and it is two-way. `ActiveIdChanged` reports each change.

```csharp
chart.ActiveIdChanged += (_, e) => readout.Text = e.Series is null
    ? string.Empty
    : $"{e.Series.Label}: median {Stats(e.Series).Median:0.0}";
```

## API reference

<!-- api: Ridgeline -->

<!-- api: RidgelineSeries -->

## Accessibility

- `Label` names the chart. The summary lists each group. The plot is a focusable group.
- The up and down arrow keys lift the previous or next ridge. The left and right arrow keys move the readout cursor by an
  axis step. Home and End lift the first and last ridge, and Escape lowers the ridge and clears the readout.
- A touch press lifts the ridge under the finger and reads the value there.
- A polite live region announces the median, the middle half, the range and the count of the lifted ridge.
- A hidden data table lists each group with its median, quartiles, extremes and count. Each row has a text label, so the
  tint is not the only way to read the median.

## Motion

- When the dataset or the domain changes, each curve is resampled on a shared grid and every point is interpolated. The
  ridges swell, slide and split on the same smooth spring.
- On first view the ridges roll in row by row, starting from the top.
- A lifted ridge is drawn as a copy on top. It rises 6px, its middle half gets a shadow, and its median is marked. The
  tooltip follows the pointer and stays inside the chart.
- Reduced motion draws the end state at once. Off behaves the same as Reduced.

## Related

- `BarChart` for one summary value per group.
- `LineChart` for how one value changes over time.
- `Streamgraph` for how the totals of several layers change over time.
- `BrushChart` for a dense single time series.
