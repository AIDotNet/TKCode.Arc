---
title: Streamgraph
summary: Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
---

<Demo name="TopicsOverTime" />

## When to use

`Streamgraph` shows how a total is made up of parts as it changes over time. Each part is a layer, and the layers
flow along a shared time axis as one band, so the reader sees both the size of the total and the share of each part
at a glance. It fits a screen that answers "what changed in the mix, and when?" rather than "what was the exact value
of each part?"

- **Support tickets by topic in an operations review**: weekly volume split into billing, login, performance,
  integrations and mobile. After a release, the performance layer swells and the reader sees the shift in the mix as
  well as the rise in volume.
- **Traffic by acquisition channel in a marketing dashboard**: daily visits from search, social, email, referral and
  direct, over 26 weeks. The stream shows which channel carried a campaign and how the others shrank to make room.
- **Revenue by plan tier in a SaaS finance review**: monthly recurring revenue from Starter, Growth and Enterprise. The
  reader sees mix changes, such as an upgrade wave, without comparing five separate lines.
- **Incident categories over a quarter in a reliability report**: the count of incidents per category per week, where
  seasonality and spikes read more clearly as flowing layers than as separate lines.
- **Seasonal demand by product category in an e-commerce planning view**: units sold per category per week, where the
  shape of the band shows when each category peaks.

## When not to use

- **Exact values or comparisons between layers**, such as "did Billing grow faster than Login in March?": use
  `LineChart`, which puts every series on its own baseline and an axis.
- **A few periods or discrete categories**, such as four quarters or six regions: use `BarChart`, where each value is a
  separate bar and the reader can compare their heights.
- **Data with negative values**, such as net flow or profit and loss by business line: the layers stack from a
  baseline, so the stream stops reading as parts of a total. Use `LineChart`.
- **The parts of one total at a single moment**, such as the share of revenue by region today: use `DonutChart`. A
  stream needs a time axis to earn its place.
- **Many layers that are all small**: a stream with more than eight layers becomes a stack of slivers. Fold the
  smallest into an "Other" series, or use `LineChart` when every series matters.
- **Distributions of several groups rather than their totals over time**, such as the spread of response times for
  each service: use `Ridgeline`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

Each `ChartSeries` is one layer. `ItemsSource` holds the shared x positions, such as the week starts, and every
layer's `Values` has one number per position. Hover or focus a layer to isolate it.

`Streamgraph` derives from `ChartBase`, the base of Arc's self-drawn charts, and shares its data pipeline, palette,
reveal, readout and accessible summary with `LineChart` and `BarChart`. The stream adds the stacking, the layer
isolation and the direct labels. Because the layers are stacked, a layer's height is its value, and the total height
of the stream is the total of the visible layers at each week.

### Anatomy

1. **Legend** (`ShowLegend`): one square swatch and name per layer, above the plot with a 16 unit gap. The legend
   extends 8 units into the margins so that its hover areas line up with the plot edges. It is shown by default when
   there is more than one layer.
2. **Plot** (`PlotHeight`, default 260): the stacked layers, each filled with its series color. A 2 unit gap in the
   surface color separates neighboring layers, so the boundaries stay readable without outlines.
3. **Direct labels** (`ShowDirectLabels`, on by default): the layer's name on its thickest stretch, with a halo in the
   surface color so the text reads over any layer. A label appears only when its layer is at least 19 units thick at
   that point and the text fits in the plot.
4. **Crosshair**: a 1 px vertical line in the foreground color at 50% opacity, at the active week.
5. **Tooltip**: the week's title and one row per visible layer, in stack order. The isolated layer's row is emphasized.
6. **Time axis** (`ShowXAxis`): a 26 unit row under the plot with the category labels. Long ranges show fewer labels.

### Layers and series

A layer is a `ChartSeries` with a `Key`, a `Name` and `Values`. The `Key` identifies the layer in `HiddenSeries`,
`ActiveSeriesKey` and the `HiddenSeriesChanged` and `ActiveIndexChanged` events, so keep it stable across data
refreshes. The `Name` appears in the legend, the direct label, the tooltip and the announcement.

Order the layers by what the reader should see. The stack places the first layer in the middle of the stream and
alternates the rest above and below it, so the largest or the most important layer is often the best first. Keep the
same order across screens that share a legend, so that the same topic has the same place each time.

### Offset

`Offset` chooses how the layers stack around the baseline. The default is `Wiggle`.

| Value | Stack | Choose it when |
|---|---|---|
| `Wiggle` (default) | Minimizes the slope of the layers, so the stream flows and stays level | The story is the flow: layers rise and fall together and the reader should follow the band |
| `Silhouette` | Centers the whole stack around a flat middle | The total's up and down is the story, and the layers are the detail around it |
| `Zero` | Stacks the layers in order from a flat baseline at the bottom | The reader needs a stable baseline to compare the bottom layer and the layers above it |

```xml
<arc:Streamgraph Label="Topics" Offset="Silhouette" ItemsSource="{Binding Weeks}" />
```

Changing `Offset` re-stacks the layers without new data. Keep one offset per screen, so that the same layer does not
change place when the reader moves between two charts.

### Ranges

Replace `ItemsSource` and the series values together when the range changes. Every layer blends from its shape on
screen to the new one, and ranges of different lengths also morph, because each layer is resampled to the same grid
before the blend.

```csharp
void ShowRange(Streamgraph chart, IReadOnlyList<DateOnly> weeks, double[][] valuesByLayer)
{
    chart.ItemsSource = weeks;
    for (var i = 0; i < chart.Series.Count; i++)
    {
        chart.Series[i].Values = valuesByLayer[i];
    }
}
```

A layer that is hidden thins to nothing while the others close over it, and the hidden layer comes back the same way
when the reader shows it again.

### Legend, direct labels and hidden layers

The legend and the direct labels name the layers. Direct labels are drawn on the thickest stretch of each layer when
there is room, so a thin layer may have no label; its legend entry still names it. Turn `ShowDirectLabels` off when the
legend is enough and the plot is busy.

`HiddenSeries` is two-way: the legend toggles it, and the view model can keep the hidden keys. A hidden layer is removed
from the stack, the tooltip, the announcement and the total, and the others re-stack around it.

```xml
<arc:Streamgraph Label="Topics"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 HiddenSeries="{Binding HiddenKeys}"
                 ItemsSource="{Binding Weeks}" />
```

### Reading a week

`ActiveIndexChanged` reports the week under the pointer or the keyboard. Its `Index` and `Label` identify the week,
`SeriesKey` names the isolated layer, and `Values` lists each layer's value at that week. `ActiveSeriesKey` returns the
isolated layer at any time, or `null` when no layer is isolated or the isolated layer is hidden.

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Label ?? string.Empty;
```

### Binding to a view model

In an MVVM app the view model owns the weeks, the layer values and the hidden keys. With CommunityToolkit.Mvvm, the view
model is an `ObservableObject` and each range change is one command.

```csharp
public sealed record TicketReport(
    IReadOnlyList<DateOnly> Weeks,
    double[] Billing,
    double[] Login,
    double[] Performance);

public interface ITicketApi
{
    Task<TicketReport> GetTicketsByTopicAsync(int weeks);
}

public partial class TicketTrendViewModel : ObservableObject
{
    private readonly ITicketApi _api;

    public TicketTrendViewModel(ITicketApi api) => _api = api;

    [ObservableProperty]
    private IReadOnlyList<DateOnly> _weeks = [];

    [ObservableProperty]
    private double[] _billing = [];

    [ObservableProperty]
    private double[] _login = [];

    [ObservableProperty]
    private double[] _performance = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [RelayCommand]
    private async Task LoadRangeAsync(int weeks)
    {
        // The week starts and the layer values come from one report, so the chart receives them as one update.
        var report = await _api.GetTicketsByTopicAsync(weeks);
        Weeks = report.Weeks;
        Billing = report.Billing;
        Login = report.Login;
        Performance = report.Performance;
    }
}
```

```xml
<arc:Streamgraph Label="Support tickets by topic"
                 Unit="tickets"
                 CategoryLabel="Week"
                 ItemsSource="{Binding Weeks}"
                 HiddenSeries="{Binding HiddenKeys, Mode=TwoWay}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="Billing" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="Login" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="Performance" Values="{Binding Performance}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

`HiddenSeries` is two-way, so the view model sees the layers the reader hid and can keep them hidden after a refresh.
`ItemsSource` and the layer values are one-way. The `HiddenSeriesChanged` event reports the new hidden list in its
`Hidden` property for code that prefers events. `ActiveSeriesKey` is read-only; use `ActiveIndexChanged` to react to
the reader's moves.

### Data shape and performance

Each layer is resampled to a fixed grid of 161 points when the data changes, and the stack, the edges and the labels are
recomputed from those points on each frame. The cost therefore depends on the number of layers more than on the number
of weeks. Keep the layers to three to eight, and keep the values non-negative.

- **Week count**: each week is one category. The curves are resampled to the fixed grid at any week count, and the time
  axis picks its labels so that they do not overlap.
- **Layer values**: every layer needs one value per week, in the same order as `ItemsSource`. Pass zero for a week with no
  activity, so that the layers line up on the same weeks.
- **Update frequency**: a refresh once a day or once a week reads as a change of range. A stream that updates every few
  seconds keeps the layers moving and makes the isolation hard to use.
- **Formatting**: values are formatted with `ValueFormat` (a .NET format string, en-US) and the `Unit` appears after them
  in the tooltip and the announcement, such as "4 tickets". Format the axis dates with `XAxisFormatter` when the
  default short date is not the one your product uses.

### Example: ticket mix after a release

A support lead reviews the last 26 weeks of tickets. The stream shows that the performance layer grew in the weeks after
a release, while billing stayed flat. The legend stays visible so each layer can be hidden or isolated, the direct
labels name the thick layers, and the time axis labels the weeks. The reader can hide the Mobile layer to look at
the mix of the other four without a new query.

```xml
<arc:Streamgraph Label="Support tickets by topic, 26 weeks"
                 Unit="tickets"
                 CategoryLabel="Week"
                 PlotHeight="240"
                 ShowLegend="True"
                 ShowDirectLabels="True"
                 Offset="Wiggle"
                 ItemsSource="{Binding Weeks}">
    <arc:Streamgraph.Series>
        <arc:ChartSeries Key="billing" Name="Billing" Values="{Binding Billing}" />
        <arc:ChartSeries Key="login" Name="Login" Values="{Binding Login}" />
        <arc:ChartSeries Key="performance" Name="Performance" Values="{Binding Performance}" />
        <arc:ChartSeries Key="integrations" Name="Integrations" Values="{Binding Integrations}" />
        <arc:ChartSeries Key="mobile" Name="Mobile" Values="{Binding Mobile}" />
    </arc:Streamgraph.Series>
</arc:Streamgraph>
```

The keys are fixed strings, so a hidden layer stays hidden when the data refreshes. The `Name` values are the words the
reader sees in the legend and the tooltip, so they are written for people rather than for the data model.

### Content guidelines

Name the layers by the thing they count, in sentence case, and name the unit in `Unit`. A legend that reads "Billing,
Login, Performance" lets the reader scan the stream without a key.

| Instead of | Write |
|---|---|
| `Label="Chart 1"` | `Label="Support tickets by topic"` |
| `Name="billing_v2"` | `Name="Billing"` |
| `Unit="count"` | `Unit="tickets"` |
| `CategoryLabel="x"` | `CategoryLabel="Week"` |
| `Name="Other (misc.)"` | `Name="Other"` with the smallest categories folded in |
| `Name="ALL CAPS TOPIC"` | `Name="Login"` |

### Best practices

**Do**

- Keep the layer keys stable. The key, not the name, identifies a layer in `HiddenSeries` and in events, so a renamed key
  resets what the reader hid.
- Fold the smallest categories into "Other" before you pass the data in. A layer too thin to hold its direct label is
  hard to isolate, and the legend and the tooltip become its only identification.
- Keep one offset per screen. Changing the offset re-stacks the layers, which makes it hard to follow a layer across
  two charts.
- Replace the week starts and every layer's values together when the range changes, so that the layers morph from the
  shape on screen to the new one.
- Name the unit in `Unit`, so that the tooltip and the announcement read "4 tickets" rather than a bare number.

**Avoid**

- A stream for data with negative values. The stack has no meaning below the baseline; use `LineChart`.
- More than eight layers. The thin layers become slivers that the reader cannot isolate; combine them.
- A stream for exact comparisons between layers. The stacked layers are read by their band, and their exact values are
  only in the tooltip and the announcement.
- Direct labels on a small plot with many layers. The labels hide on thin layers, so the legend is the reliable key.
- Refreshing the data every few seconds. Each refresh moves every layer, and the stream never settles for the reader.

### Common questions

**How do I isolate a layer from code?** The chart has no setter for the isolated layer. The pointer, the keyboard and
the legend preview set it, and `ActiveSeriesKey` reads it back. Read `ActiveSeriesKey` in `ActiveIndexChanged`, or when
you need the current layer, to drive a detail panel.

**Why does a layer disappear when I hide it, and why do the others move?** A hidden layer is removed from the stack, so the
remaining layers close over its space and re-stack. Its legend entry stays, and the key is kept in `HiddenSeries` until
the reader shows it again.

**What does the announcement say?** It names the week, the isolated layer and its value, the number of visible layers
and the total of the visible layers, such as "W5. Login 4 tickets, 3 layers, 30 tickets in total". Without an isolated
layer, it names the week and the totals only. Set `Unit` and `Label` so that the announcement is meaningful.

**Why is a layer missing its direct label?** Direct labels are drawn only where the layer is at least 19 units thick and
the text fits inside the plot. A thin or narrow layer keeps its legend entry and its tooltip row, and the label returns
when the layer grows.

**Can I use dates for the x axis?** Yes. `ItemsSource` accepts `DateTime`, `DateTimeOffset` or `DateOnly`, and the
category labels follow the default date format. Set `CategoryLabel` to the word the table header should use, such as
"Week".

## API reference

<!-- api: Streamgraph -->

## Accessibility

- `Label` names the chart. The plot is a focusable group named with the label and the keyboard instructions, such as
  "Support tickets by topic. Use left and right arrows to move through time, up and down to change layer."
- Each reading updates the value text and the polite announcement with the week, the isolated layer's value and the
  total of the visible layers. A reader using a screen reader hears the same numbers the tooltip shows.
- The legend entries are toggles. Focusing an entry isolates its layer, and pressing `Enter` shows or hides it.
- A hidden data table lists every layer for every week.
- The layers are named on the plot and in the legend, so they are not told apart by shade alone. A 2 unit gap in the
  surface color separates neighboring layers.
- With a pointer, moving over a layer isolates it and leaving the plot clears the reading. With touch, the reading stays
  on the touched point until the contact is released.

### Keyboard

The plot takes focus with `Tab`. When focus arrives from the keyboard, the reading starts on the latest week.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus among the legend entries and the plot |
| `Left` / `Right` | Move to the previous or the next week. From rest, `Right` stays on the latest week |
| `Up` / `Down` | Isolate the next or the previous visible layer in series order, for the current week. From none, `Up` picks the last visible layer and `Down` picks the first |
| `PageUp` / `PageDown` | Move about a sixth of the weeks, forward or back |
| `Home` / `End` | Move to the first or the last week |
| `Escape` | Clear the reading and the isolated layer |
| `Enter` (on a legend entry) | Show or hide the layer |

## Motion

- On first view the stream swells out from a hairline along its centre on a long spring with no overshoot.
- A new range or a new dataset blends every layer on a shared grid, so the layers keep their order and their speed.
- A hidden layer thins to nothing while the others re-stack around it.
- The crosshair and the tooltip slide on springs, and the other layers dim while one is isolated.
- Reduced motion shows the end state at once and keeps the crosshair and the tooltip, which jump to the new week
  without a spring. Off jumps to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | The stream swells out of its centre line on a long spring, without overshoot | The end shape appears at once | The end shape appears at once |
| New range or dataset | Layers blend to the new shape on a smooth spring; new layers grow out of nothing | Layers jump to the new shape | Immediate |
| Hiding a layer | The layer thins to nothing while the others re-stack around it | Jumps to the re-stacked shape | Immediate |
| Isolating a layer | The other layers fade on a short standard tween | The same short tween | Immediate |
| Crosshair and tooltip | Slide to the new week on springs | Jump to the new week without a spring | Immediate |

## Related

- `LineChart` when the exact value of each series matters, and `BarChart` for a few discrete periods.
- `Ridgeline` for the distribution of several groups rather than their totals over time.
- `DonutChart` for the share of each part in one total.

| Component | Choose it when |
|---|---|
| `LineChart` | The reader must compare exact values, or the data has negative values |
| `BarChart` | There are a few discrete periods or categories to compare |
| `Ridgeline` | You compare the distributions of several groups, not their totals over time |
| `DonutChart` | You show the share of each part of one total at a single moment |
| `Sparkline` | You show the trend of a single total beside its headline value |
