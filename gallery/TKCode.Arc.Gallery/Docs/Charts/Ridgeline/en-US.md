---
title: Ridgeline
summary: Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
---

<Demo name="DistributionsByMonth" />

## When to use

`Ridgeline` compares the shape of several distributions side by side. Each group is one ridge: a smoothed density curve
drawn over the row above it, so the reader sees spread, skew and a second hump that a single average would hide. It fits
an operations or analytics page where the question is how the groups differ in their distribution, not only in their
average.

- **Response times by region** on a status or observability page. Three ridges show that Asia Pacific has a second group
  of slow responses near 250 ms that the median alone would hide.
- **Daily highs by month** in a weather or energy-demand dashboard. The ridges show the seasonal shift and whether the
  spread widens in winter.
- **Order values by customer segment** in an e-commerce review. A segment with two humps (small single items and bulk
  orders) reads as two ridges' worth of shape, not a misleading mean.
- **Resolution times by team** in a support or CRM report. The long tail of slow tickets is visible on each team's row.
- **Delivery times by carrier** in a logistics console, where the on-time share matters more than the average.

## When not to use

- **One summary number per group**, such as average revenue by region: use `BarChart`, which compares the values exactly.
- **Few observations per group**: a curve drawn from a handful of values is guesswork. Use a table of summaries, or
  `BarChart` for the summary numbers, when a group has fewer than about 20 values.
- **A change of one value over time**: use `LineChart`, where the x axis is time.
- **Exact comparison of a few values**: use `BarChart` or a table. Ridges show shape, not precise differences.
- **Parts of a whole**, such as traffic share by source: use `DonutChart`.
- **Density over days and hours**, such as activity by weekday and hour: use `ActivityHeatmap`, which shows each cell as
  a count.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Ridgeline Label="Daily highs in Zurich"
                   Unit="°C"
                   DomainMinimum="-5"
                   DomainMaximum="35"
                   ItemsSource="{Binding Months}" />
</UserControl>
```

Each `RidgelineSeries` is one group: an `Id`, a `Label` and its raw `Values`. The chart computes a density for each group
and draws the ridges from top to bottom in the order of the series. `Ridgeline` derives from Avalonia's `Control`, not
from a templated chart, so the whole box takes the pointer and the ridges, labels and scale are drawn by the control
itself. It takes keyboard focus only when it has observations to read.

### Anatomy

1. **Row labels** (`RidgelineSeries.Label`): drawn in the left gutter, 52 px wide (36 px when the chart is narrower than
   420 px), above each row's baseline. A label that does not fit is shortened.
2. **Ridges**: one filled curve per group, sampled at 96 points on a domain shared by all groups. Each ridge is tinted by
   its median when `IsTinted` is on, and drawn in the first series color when it is off.
3. **Rows and overlap**: each row is 30 px high by default (`RowHeight`). The tallest ridge rises 2.4 row heights into the
   rows above (`Overlap`), so the top ridges are not clipped.
4. **Value axis**: a 28 px band under the last row, with tick labels about 72 px apart, formatted with `Unit`.
5. **Median scale** (`IsTinted`): a "Median" label, the lowest and highest values, and five color swatches, right aligned
   under the axis.
6. **Lifted ridge**: a copy drawn above the others, raised 6 px, with its middle half shaded and its median marked by a
   short stroke.
7. **Tooltip**: the series label, the median, the middle half and the range, plus the share of values at or below the
   reading cursor once it moves. It is at least 170 px wide and
   stays inside the chart.

### Datasets

Replace `ItemsSource` to switch the dataset. Every curve morphs from its current shape to the new one, and the ridge
order stays the same.

```csharp
// Keep each dataset as an array and assign the one the person chose.
chart.ItemsSource = selectedDataset == "zurich" ? zurichMonths : lisbonMonths;
```

Ridges are matched by `Id`, so a group that exists in both datasets with the same id morphs into its new shape. A group
that is new appears on its own row.

### Items and value formats

Add groups through `Items` when they are created in code, or bind `ItemsSource` for a view model. `ValueFormatter` sets
the format for the axis ticks, the median scale, the summary and the tooltip.

<Demo name="Untinted" />

### Overlap and smoothness

`Overlap` sets how far the ridges rise into the row above, as a multiple of the row height. `RowHeight` sets the height of
one row. `Bandwidth` sets the smoothing in the units of the values: a larger value gives a smoother, more general shape,
and a smaller value keeps more of the bumps in the data. By default each group uses Silverman's rule of thumb, which
picks a bandwidth from that group's own spread and count.

```xml
<arc:Ridgeline Label="Latency" Overlap="2.4" RowHeight="30" Bandwidth="4" ItemsSource="{Binding Regions}" />
```

### Tint and domain

`IsTinted` colors each ridge by its median, from a light tint to the first series color, and shows the median scale under
the axis. Turn it off when the groups should read as one family, or when the color would suggest a ranking that is not
the point of the chart. `DomainMinimum` and `DomainMaximum` fix the value axis, so it does not move when the dataset
changes. Leave them unset (NaN) and the axis covers the data with room for the smoothing, rounded to nice steps.

```xml
<arc:Ridgeline Label="Latency" IsTinted="False" DomainMinimum="0" DomainMaximum="300" ItemsSource="{Binding Regions}" />
```

### Reading a ridge

`ActiveId` is the lifted ridge, and it is two-way. `ActiveIdChanged` reports each change, with the lifted series in
`Series`.

```csharp
chart.ActiveIdChanged += (_, e) => readout.Text = e.Series is null
    ? string.Empty
    : $"{e.Series.Label}: {e.Series.Values.Count} observations";
```

The tooltip and the accessible text carry the median, the middle half and the range. If the page needs its own figures,
compute them from `Series` in the handler, for example a 95th percentile for a latency panel.

### Binding to a view model

With CommunityToolkit.Mvvm the view model owns the groups and the selected group. Each group is a `RidgelineSeries`,
and the view model replaces the array for a new dataset. The chart keeps the lifted group in `ActiveId`.

```csharp
public partial class LatencyViewModel : ObservableObject
{
    [ObservableProperty]
    private RidgelineSeries[] _regions = [];

    [ObservableProperty]
    private string? _activeRegion;

    public void Load(IReadOnlyDictionary<string, double[]> samplesByRegion)
    {
        Regions = samplesByRegion
            .Select(pair => new RidgelineSeries(pair.Key, pair.Key, pair.Value))
            .ToArray();
    }
}
```

```xml
<arc:Ridgeline Label="Response time by region"
               Unit="ms"
               ItemsSource="{Binding Regions}"
               ActiveId="{Binding ActiveRegion, Mode=TwoWay}" />
```

- `ActiveId` is two-way: the chart writes it when a ridge is lifted, and the view model can lift a ridge from code.
- Items that are not `RidgelineSeries` work when you set `KeySelector`, `LabelSelector` and `ValuesSelector`, or the
  matching member bindings (`KeyMemberBinding`, `LabelMemberBinding` and `ValuesMemberBinding`). The selectors win when
  both are set.
- Compiled bindings need `x:DataType` on the view; the examples use plain `Binding` for brevity.

### Data shape and performance

Pass raw observations, not a histogram. Non-finite values (NaN and infinity) are ignored. If two series share an `Id`, the
first one is drawn and the later one is dropped, so keep ids unique within a dataset.

The density is computed once per data change for each group, on the shared domain, and the animation only interpolates
the sampled curves. Keep the number of groups to about twenty, and the observations per group to at least twenty, so each
curve has a real shape. Heights share one scale: the tallest peak of all groups rises to the overlap height. A group
with more observations does not look taller. A tall, narrow ridge means that its values are concentrated.

Format values with `ValueFormatter` or `Unit`. The default format groups thousands, uses at most one decimal and a true
minus sign, then appends `Unit`, so -3 °C reads as "−3°C".

### Example: response times by region

An operations page for Acme Analytics shows how response times differ across three regions over one week. The median
alone would say that Asia Pacific is only a little slower than the Americas. The ridges show that a second group of slow
responses sits near 250 ms, which is the part the on-call engineer needs to see. The tinted ridges and the median scale
show the rank order, and the fixed domain keeps the axis still when the page is filtered to another week.

![A response time ridgeline with three regions, tinted by median, with a second slow group in Asia Pacific](images/ResponseTimeRidges.png)

```xml
<Border xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
        xmlns:collections="clr-namespace:Avalonia.Collections;assembly=Avalonia.Base"
        xmlns:sys="clr-namespace:System;assembly=System.Runtime"
        Width="680"
        Padding="24"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Response time by region" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Samples per request, Sep 4 to Sep 10, 2026. APAC has a second group near 250 ms."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:Ridgeline Label="Response time by region"
                       Unit="ms"
                       RowHeight="36"
                       Overlap="1.8"
                       DomainMinimum="0"
                       DomainMaximum="400">
            <arc:Ridgeline.Items>
                <arc:RidgelineSeries Id="eu" Label="EU">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>108</sys:Double>
                            <sys:Double>112</sys:Double>
                            <sys:Double>115</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>119</sys:Double>
                            <sys:Double>121</sys:Double>
                            <sys:Double>122</sys:Double>
                            <sys:Double>123</sys:Double>
                            <sys:Double>124</sys:Double>
                            <sys:Double>125</sys:Double>
                            <sys:Double>126</sys:Double>
                            <sys:Double>128</sys:Double>
                            <sys:Double>129</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>133</sys:Double>
                            <sys:Double>136</sys:Double>
                            <sys:Double>139</sys:Double>
                            <sys:Double>142</sys:Double>
                            <sys:Double>156</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
                <arc:RidgelineSeries Id="us" Label="US">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>138</sys:Double>
                            <sys:Double>145</sys:Double>
                            <sys:Double>149</sys:Double>
                            <sys:Double>152</sys:Double>
                            <sys:Double>155</sys:Double>
                            <sys:Double>156</sys:Double>
                            <sys:Double>158</sys:Double>
                            <sys:Double>160</sys:Double>
                            <sys:Double>161</sys:Double>
                            <sys:Double>162</sys:Double>
                            <sys:Double>163</sys:Double>
                            <sys:Double>165</sys:Double>
                            <sys:Double>166</sys:Double>
                            <sys:Double>168</sys:Double>
                            <sys:Double>171</sys:Double>
                            <sys:Double>174</sys:Double>
                            <sys:Double>178</sys:Double>
                            <sys:Double>185</sys:Double>
                            <sys:Double>196</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
                <arc:RidgelineSeries Id="ap" Label="APAC">
                    <arc:RidgelineSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>178</sys:Double>
                            <sys:Double>182</sys:Double>
                            <sys:Double>186</sys:Double>
                            <sys:Double>189</sys:Double>
                            <sys:Double>192</sys:Double>
                            <sys:Double>195</sys:Double>
                            <sys:Double>197</sys:Double>
                            <sys:Double>201</sys:Double>
                            <sys:Double>204</sys:Double>
                            <sys:Double>207</sys:Double>
                            <sys:Double>212</sys:Double>
                            <sys:Double>236</sys:Double>
                            <sys:Double>248</sys:Double>
                            <sys:Double>255</sys:Double>
                            <sys:Double>259</sys:Double>
                            <sys:Double>262</sys:Double>
                            <sys:Double>268</sys:Double>
                            <sys:Double>274</sys:Double>
                            <sys:Double>281</sys:Double>
                        </collections:AvaloniaList>
                    </arc:RidgelineSeries.Values>
                </arc:RidgelineSeries>
            </arc:Ridgeline.Items>
        </arc:Ridgeline>
    </StackPanel>
</Border>
```

The caption names the period and the finding, so the card reads without hovering. The domain is fixed at 0 to 400 ms, the
same on every week the page shows. Ridges are tinted by median, so the median scale below the axis explains the color.
Nothing is lifted in the screenshot: a ridge lifts only on hover or from the keyboard.

### Content guidelines

- **Chart label**: the measure and the scope: "Response time by region", "Daily highs in Zurich". The label names the
  chart for assistive technology and the summary.
- **Row label**: a short name for the group, such as "EU", "APAC" or "Jan". The gutter fits about 40 px of text, so a longer
  name is cut off with an ellipsis.
- **Unit**: a symbol that follows the number without a space, such as "°C" or "ms". The ticks and the tooltip use the same
  unit.
- **Card caption**: state the finding in words, with numbers as digits, for example "Asia Pacific has a second group near
  250 ms."

| Instead of | Write |
|---|---|
| Distribution | Response time by region, Sep 4 to Sep 10 |
| Group 1 | Europe |
| Values | Samples per request |
| Asia has issues | Asia Pacific has a second group near 250 ms |

### Best practices

**Do**

- Keep a fixed `DomainMinimum` and `DomainMaximum` on a page where the reader switches between datasets, so the axis
  does not move under them.
- Give every group at least about twenty observations, so each ridge has a real shape, and say so when a group is small.
- Keep the groups to about twenty, and sort them in a meaningful order, such as by median or by region, so the stack
  reads as a comparison.
- Use `IsTinted` for a median ranking that matters, and turn it off when the groups are equal.

**Avoid**

- Pre-binned histograms passed as observations. The density is computed from raw values, so binned counts produce
  distorted curves.
- Reading the height of a ridge as a count. Heights reflect concentration on a shared scale, not the number of values.
- Using a ridgeline for a single average per group. A bar chart or a table is clearer for one number.
- Duplicate ids in one dataset. The second group with the same id is dropped, so its data is missing from the chart.

### Common questions

**Why does a ridge look too wavy or too flat?** The default bandwidth comes from Silverman's rule for that group. For a
small group it can be too rough or too smooth. Set `Bandwidth` in the units of the values to make every group share one
smoothing, and check the result against the raw values.

**Why can I not Tab to the chart?** The chart takes focus only when it has observations. An empty chart shows its
`EmptyLabel` ("No data yet." by default) and is skipped by Tab until data arrives.

**Why are the tallest ridges the same height across groups?** Every group shares one scale, and the tallest peak of all
groups rises to the overlap height. A ridge is as tall as its density is concentrated, so compare the heights only
within the same chart.

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

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus into or out of the chart. The chart is skipped while it has no observations |
| `Up` | Lift the previous ridge. From rest, lifts the first ridge |
| `Down` | Lift the next ridge. From rest, lifts the first ridge |
| `Left` / `Right` | Move the reading cursor one axis tick step along the values. From rest, lifts the first ridge |
| `Home` / `End` | Lift the first or the last ridge |
| `Escape` | Put the lifted ridge down and clear the reading |

## Motion

- When the dataset or the domain changes, each curve is resampled on a shared grid and every point is interpolated. The
  ridges swell, slide and split on the same smooth spring.
- On first view the ridges roll in row by row, starting from the top.
- A lifted ridge is drawn as a copy on top. It rises 6px, its middle half gets a shadow, and its median is marked. The
  tooltip follows the pointer and stays inside the chart.
- Reduced motion draws the end state at once. Off behaves the same as Reduced.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Dataset or domain changes | Each curve morphs on a 0.7 s spring; ridges swell, slide and split | Drawn at the end state at once | Same as Reduced |
| First view | Rows roll in from the top, one after another | Drawn at the end state at once | Same as Reduced |

## Related

- `BarChart` for one summary value per group.
- `LineChart` for how one value changes over time.
- `Streamgraph` for how the totals of several layers change over time.
- `BrushChart` for a dense single time series.

| Component | Choose it when |
|---|---|
| `BarChart` | Each group has one summary value to compare exactly |
| `LineChart` | The x axis is time and the reader follows a value over it |
| `Streamgraph` | Several layers should be read as parts of a stacked total over time |
| `BrushChart` | One dense time series needs zooming and a range selection |
| `DonutChart` | The data is a few parts of one whole |
| `ActivityHeatmap` | The data is a grid of counts per day or per hour |
