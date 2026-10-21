---
title: Sparkline
summary: Show a compact trend beside a value.
---

<Demo name="KpiCards" />

## When to use

`Sparkline` is a small trend line with a headline value and an aside, sized to sit inside a card, a table cell or a KPI
block. It answers "where is this number going?" in the space a number takes, so the reader sees the direction and
shape of a metric next to its current value without an axis or a legend.

- **Revenue and usage cards on an analytics overview**: a card that shows `$48.2K` and `+4.1%` with the last 14 days of
  revenue underneath. The shape shows whether the number is rising in steady steps or spiking on one day.
- **Service rows in an operations console**: a table of services where each row shows a latency or error-rate trend.
  Many rows of the same size make the table scannable: one glance finds the service whose line bends the wrong way.
- **Churn, queue and backlog tiles in a CRM or support tool**: a value with its direction and a tone, such as a
  rising queue depth shown in the warning color. The tone tells the reader whether the trend is good or bad.
- **Billing and usage summaries**: a per-customer seat count or a daily API call volume beside the current total, where
  the reader needs the recent pattern more than the exact points.
- **Scrubbing a recent history in a detail panel**: a reader moves the pointer or the arrow keys along the line to read
  the value and the date of an earlier point, without opening a full chart.

## When not to use

- **Discrete periods that people compare side by side**, such as twelve months of revenue or the orders per weekday:
  use `BarChart`, which has a value axis and one bar per period.
- **A single current value with its change and no history**: use `MetricCard`. A sparkline with one point draws a flat
  line, which says nothing about direction.
- **A trend that needs axes, several series or a legend**, such as revenue by product line over a quarter: use
  `LineChart`.
- **A very long series that people must zoom or pan**, such as a year of minute-level metrics: use `BrushChart`, which
  holds a window over the full range and lets the reader select a span.
- **A value measured against a known range**, such as disk usage out of a 500 GB quota: use `Gauge`, which shows the
  position on a scale rather than the path to it.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Sparkline Label="Revenue"
                   Value="$48.2K"
                   Change="+4.1%"
                   Tone="Success"
                   Width="220"
                   ItemsSource="{Binding Revenue}" />
</UserControl>
```

`ItemsSource` holds the values from oldest to newest, one value per point. `Value` and `Change` form the headline and
the aside that are shown at rest. `Label` names the measurement.

`Sparkline` derives from `ChartBase`, the base of Arc's self-drawn charts. That base supplies the data pipeline
(`ItemsSource` with `YBinding` or `YSelector` for item objects, or plain numbers), the token palette, the first-view
reveal, the active-point readout (`ActiveIndex`), value formatting and the accessible summary. The sparkline adds the
headline and aside, the scrubbing gesture and a plot with no axes. Because it shares the base with `LineChart` and
`BarChart`, the same data and formatting rules apply everywhere in a screen.

### Anatomy

1. **Label** (`Label`): the measurement's name, 14 px, in the secondary text color, on the baseline of the headline.
   It is also the start of the accessible name.
2. **Headline** (`Value`): 18 px, medium weight, in the foreground color, right-aligned in its slot. It is hidden when
   `Value` is null.
3. **Aside** (`Change`): 12 px, medium weight. At rest it shows `Change` in the tone color (the strong accent color for
   `Accent`). While scrubbing it shows the point (see "Scrubbing" below).
4. **Caption gap**: 12 units of space between the caption row and the plot.
5. **Plot**: the line, its fill, a 1 px baseline in the border color and the scrubbing cursor. The line is
   2.5 units wide with round caps.
6. **Area** (`ShowArea`): a 9% wash of the tone text color under the line. It is drawn beneath the line and can be turned off.
7. **End dot**: a round dot of the tone color that rests on the latest point, 7 units in diameter at rest and 10 while a
   point is scrubbed. It is hidden until the first reveal has finished.
8. **Focus ring**: drawn by Arc in the focus color when the plot has keyboard focus. The window's default focus adorner
   is suppressed.

### Headline, aside and scrubbing

The caption shows one of two states. At rest it shows `Value` and `Change`. While a point is scrubbed it changes to the
scrubbed point, and the headline and aside roll to the new text.

| State | Headline | Aside |
|---|---|---|
| At rest, `Value` and `Change` set | `Value` | `Change`, in the tone color |
| Scrubbing, `Value` and `PointLabels` set | The scrubbed value, formatted with `ValueFormat` | The point label, such as `Sep 14` |
| Scrubbing, `Value` set, no `PointLabels` | The scrubbed value | The position, such as `4 of 14` |
| Scrubbing, `Value` null | Hidden | The scrubbed value and the point label, joined with a dot |
| At rest, `Value` null | Hidden | `Change`, or nothing when `Change` is empty |

While a point is scrubbed, the aside uses the secondary text color instead of the tone color. The change is not
happening at that point; the date or position is context, so it stays neutral.

### Tones

`Tone` colors the line, the fill, the end dot and, at rest, the aside. Pick it from the direction of the metric and
whether that direction is good, not from the chart's decoration.

| `Tone` | Use it for | Aside color |
|---|---|---|
| `Accent` (default) | A neutral trend such as traffic, or a metric without a good or bad direction | Strong accent |
| `Success` | A metric that improves when it rises: revenue, conversion, uptime | Success |
| `Warning` | A value that needs attention without being an error: a growing queue, a rising backlog | Warning |
| `Danger` | A metric that is getting worse: churn, error rate, latency above target | Danger |

```xml
<arc:Sparkline Label="Churn" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="Queue depth" Value="1,204" Change="Rising" Tone="Warning" ItemsSource="{Binding Queue}" />
```

Keep the sign in `Change` as well as the tone. A reader who cannot tell the colors apart still reads `+0.3 pts`, and a
reader on a monochrome print still reads the direction.

### Point labels

`PointLabels` gives one label for each point, in the same order as `ItemsSource`. When a point is scrubbed, its label
replaces `Change` in the aside, and it is the text the accessible value reads. Set it for every sparkline whose points
are dates. Without it, the aside shows the position, such as `4 of 14`, which means nothing to a reader.

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

The count of labels should match the count of values. A point without a label falls back to its position.

### Scale and size

`DrawingWidth` and `DrawingHeight` are in Arc's drawing units, with defaults of 160 and 52. They set only the aspect
ratio of the plot. The plot width follows the width of the sparkline, so the height is the width multiplied by
`DrawingHeight` divided by `DrawingWidth`. A sparkline 320 pixels wide at the default ratio is 104 pixels high.

| Place | Width | `DrawingHeight` | Notes |
|---|---|---|---|
| KPI card | 200 to 240 | 52 (default) | Headline and aside on one row above the line |
| Chart panel beside a table | 240 to 320 | 52 (default) | Room for `ShowArea` and scrubbing |
| Table row | 120 to 160 | 32 | Set `ShowArea="False"` and `IsInteractive="False"` |

Set `Width` on the element to fix its size, and let the height follow the ratio. Leave `Height` unset: the drawing
ratio is what keeps the line from stretching when a card resizes.

### Dense tables

In a table with many rows, turn scrubbing off. `IsInteractive="False"` removes the tab stop and the pointer handling
from each row, so the table keeps one focus order. `ShowArea="False"` keeps the line quiet when twenty rows carry a wash
each.

<Demo name="DenseTable" />

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

Keep the label short in a row. The row already names the service, so the sparkline label can repeat the service name
or be left as the metric, such as `Latency`.

### Without a headline

When `Value` is not set, the headline is hidden and the scrubbed value and its point label appear in the aside. Use
this form for a detail panel where the number is shown elsewhere and the line is only there to be read.

<Demo name="WithoutHeadline" />

```xml
<arc:Sparkline Label="Visits" Width="300" ItemsSource="{Binding Visits}" />
```

When the data changes, the line morphs from its current shape into the new one. The same chart keeps its place on the
screen, so a refresh of a visits panel reads as an update rather than a new chart.

### Binding to a view model

In an MVVM app the view model owns the series, the headline, the change and the labels. `Sparkline` reads them through
bindings. With CommunityToolkit.Mvvm, the view model is an `ObservableObject` with observable properties, and each
refresh assigns new collections instead of editing one in place.

```csharp
public sealed record DailyRevenue(DateOnly Day, double Amount);

public interface IRevenueApi
{
    Task<IReadOnlyList<DailyRevenue>> GetDailyRevenueAsync(int days);
}

public partial class RevenueCardViewModel : ObservableObject
{
    private readonly IRevenueApi _api;

    public RevenueCardViewModel(IRevenueApi api) => _api = api;

    [ObservableProperty]
    private string? _headline = "$48.2K";

    [ObservableProperty]
    private string? _change = "+4.1%";

    [ObservableProperty]
    private IReadOnlyList<double> _amounts = [];

    [ObservableProperty]
    private IReadOnlyList<string> _pointLabels = [];

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // The API returns the days from oldest to newest. A new list is one data change, so the line
        // morphs once per refresh.
        var days = await _api.GetDailyRevenueAsync(14);
        Amounts = days.Select(d => d.Amount).ToArray();
        PointLabels = days.Select(d => d.Day.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
        Headline = days[^1].Amount.ToString("C1", CultureInfo.GetCultureInfo("en-US"));
    }
}
```

```xml
<arc:Sparkline Label="Revenue, 14 days"
               Value="{Binding Headline}"
               Change="{Binding Change}"
               Tone="Success"
               Width="220"
               ItemsSource="{Binding Amounts}"
               PointLabels="{Binding PointLabels}" />
```

`Value`, `Change`, `ItemsSource` and `PointLabels` are one-way: the view model's values flow into the sparkline. The
only two-way property is `ActiveIndex`, the point being read. Bind it when the screen needs to know which point the
reader is on, for example to update a detail row beside the chart.

```xml
<arc:Sparkline Label="Revenue, 14 days"
               ItemsSource="{Binding Amounts}"
               ActiveIndex="{Binding SelectedDayIndex, Mode=TwoWay}" />
```

`ActiveIndex` is `null` at rest and the index of the scrubbed point while the reader scrubs. The `ActiveIndexChanged`
event reports the same moves for code that prefers events. It is raised with a `null` index when the reader lets go.

### Data shape and performance

`ItemsSource` takes numbers or item objects. For item objects, set `YBinding` (or `YSelector`) to read the value, and
`XBinding` (or `XSelector`) for the date, which can be a `DateTime`, `DateTimeOffset` or `DateOnly`. Use
`CompiledBinding` with `DataType` so the path is checked at compile time and works with ahead-of-time compilation.

- **Length**: use the period the card names, such as 14 daily values for "Revenue, 14 days". The drawn curve has a
  fixed number of samples, so a longer series draws at about the same cost per instance; the reader gains nothing
  from a long series in a card, so aggregate it first.
- **Flat or empty series**: one value draws a flat line. An empty series shows `EmptyLabel` in the accessible summary
  and draws nothing. Set `EmptyLabel` when an empty card should say why, such as "No data yet".
- **Update frequency**: each change morphs the line, so a refresh once a minute reads as an update. A series that
  changes every second makes the line move constantly; aggregate the data before it reaches the control.
- **Many instances**: each sparkline tracks its own size and reveal, so a table with hundreds of rows creates hundreds
  of plots. Page long tables, and turn scrubbing off in dense rows.
- **Number formatting**: values are formatted with `ValueFormat`, a .NET format string such as `"N0"` or `"0.0'%'"`.
  The default is `"#,##0.###"`. Format the `Value` headline yourself with the same rule so that the headline and the
  scrubbed values agree. For context-dependent text, `ValueFormatter` receives the value and its index.

### Example: operations overview

A platform team's overview shows three trends in one row: revenue, p95 latency and the support queue. Each card keeps
its headline at rest, so the current number is always readable, and the tone follows what is good for the business:
revenue rising is `Success`, latency rising is `Danger`, a growing queue is `Warning`. The cards stay interactive
because there are only three and the reader may want the date of a spike. Each card has its own point labels, so a
scrubbed point reads as a date.

The view model supplies the headline, the change, the values and the labels for each card. The XAML only binds them.

```xml
<WrapPanel ItemSpacing="16" LineSpacing="16">
    <arc:Sparkline Label="Revenue, 14 days"
                   Value="{Binding Revenue.Headline}"
                   Change="{Binding Revenue.Change}"
                   Tone="Success"
                   Width="240"
                   ItemsSource="{Binding Revenue.Amounts}"
                   PointLabels="{Binding Revenue.PointLabels}" />
    <arc:Sparkline Label="p95 latency, 14 days"
                   Value="{Binding Latency.Headline}"
                   Change="{Binding Latency.Change}"
                   Tone="Danger"
                   Width="240"
                   ItemsSource="{Binding Latency.Amounts}"
                   PointLabels="{Binding Latency.PointLabels}" />
    <arc:Sparkline Label="Support queue, 14 days"
                   Value="{Binding Queue.Headline}"
                   Change="{Binding Queue.Change}"
                   Tone="Warning"
                   Width="240"
                   ItemsSource="{Binding Queue.Amounts}"
                   PointLabels="{Binding Queue.PointLabels}" />
</WrapPanel>
```

### Content guidelines

`Label` names the measurement and its period, in sentence case. The accessible name starts with it, so a label such as
"Revenue, 14 days" reads better than the bare metric. Keep the headline and the change short enough to fit the card on
one row.

| Instead of | Write |
|---|---|
| `Label="LATENCY"` | `Label="Latency"` |
| `Label="Revenue trend for the last two weeks"` | `Label="Revenue, 14 days"` |
| `Value="48200"` | `Value="$48.2K"` |
| `Value="182 milliseconds"` | `Value="182 ms"` |
| `Value="1204"` | `Value="1,204"` |
| `Change="Up"` | `Change="+4.1%"` |
| `PointLabels` with `2026-09-14` | `PointLabels` with `Sep 14` |
| `Label=""` | A name for the metric, such as `Churn` |

### Best practices

**Do**

- Name the measurement and its period in `Label`, such as "Revenue, 14 days". It is the first thing a screen reader
  reads, and the period tells the reader what the line covers.
- Choose `Tone` from whether a rise is good for the business. The same shape means different things for revenue and for
  latency, and the tone tells the reader which.
- Set `PointLabels` on every sparkline whose points are dates, so a scrubbed point reads as a date rather than a position.
- Keep the sign in `Change` and format `Value` with the same rules as `ValueFormat`, so the headline and the scrubbed
  values agree.
- Turn `IsInteractive` off in table rows, where many tab stops would make the table hard to traverse.

**Avoid**

- A sparkline for a single value or a series of one point. It draws a flat line and says nothing about direction; use
  `MetricCard` for that case.
- `ShowArea` on every row of a long table. Twenty washes compete with the text they sit beside.
- Replacing `ItemsSource` on a timer that fires every second. Each change morphs the line, so the chart never settles;
  aggregate the data to the period the card shows.
- `Tone` as decoration. A color with no direction behind it teaches the reader to ignore the color.
- Setting `Height` on the element. The drawing ratio sets the height, and a fixed height stretches the line when the card
  resizes.

### Common questions

**Why does my headline disappear while I scrub?** It is hidden only when `Value` is null. Then the scrubbed value and its
point label appear in the aside. With `Value` set, the headline shows the scrubbed value and the aside shows the point
label, and both return to `Value` and `Change` when the reader leaves the plot or presses Escape.

**How do I start the readout on a specific point from code?** Set `ActiveIndex` to the point's index, for example
`revenue.ActiveIndex = 6;`, and set it to `null` to return to the latest value. The property is two-way, so a view model
bound to it sees the reader's moves too.

**What does a sparkline show with one value, or with no values?** One value draws a flat line at the middle of the plot.
An empty series draws nothing and reads `EmptyLabel` in the accessible summary. Set `EmptyLabel` when an empty card
should explain itself, such as "No data yet".

**Why is the line shorter than I expected?** The height comes from the drawing ratio, not from a fixed value. A
`Width` of 240 with the default `DrawingWidth` of 160 and `DrawingHeight` of 52 gives a plot about 78 pixels high. Change
`DrawingHeight` to make the line taller or shorter for the same width.

## API reference

<!-- api: Sparkline -->


## Accessibility

- `Label` names the chart. The accessible name combines the label, the latest value and the change, such as
  "Revenue: $48.2K. +4.1%". The drawing itself is hidden from assistive technology, so the name and the text are what
  a screen reader reads.
- The headline and the change are plain text, and they are read as they are.
- When the chart is interactive, the plot is a focusable slider named "`Label`, explore by point". Its value text reads
  the point label and the value, such as "Thu: 22", or the position and the value, such as "Point 2 of 3: 1,500",
  when there are no point labels.
- Set `PointLabels` so that a scrubbed point is read with its date, not with its position in the list.
- Arc draws the focus ring for the plot in the focus color. Keyboard focus is visible without the window's default
  adorner.
- A sparkline that is not interactive has no tab stop and does not answer the pointer. Its name and text remain
  available to a screen reader.
- Tone is never the only signal. Put the sign in `Change` (`+4.1%`, `-6 ms`) so the direction survives a color
  vision difference.

### Keyboard

A sparkline takes focus only when `IsInteractive` is `True`. The model is the same as the other Arc charts with a
single series: the arrows step through the points, and Escape leaves the readout.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the plot |
| `Left` or `Down` | Move to the previous point. From rest, the first press lands on the point before the latest |
| `Right` or `Up` | Move to the next point. From rest, the press stays on the latest point |
| `PageUp` / `PageDown` | Move about a sixth of the points (at least one), forward or back |
| `Home` / `End` | Move to the first or the last point |
| `Escape` | Stop scrubbing and return the caption to `Value` and `Change` |

With a mouse, a press or a move over the plot scrubs to the nearest point. Leaving the plot returns to the latest value.
With touch or pen, the readout stays on the touched point until the contact is released, then returns to the latest
value.

## Motion

- On first view the line draws in once, like a pen, and the end dot appears as the line lands. The draw starts when the
  sparkline is half visible, so a card below the fold draws when the reader scrolls to it.
- While scrubbing, the cursor moves between points on a snappy spring, the stretch after the cursor dims, and the end
  dot grows to show that a point is being read.
- The headline and the aside roll to the new text, and the headline's width eases to fit it.
- New data morphs the line from its current shape into the new one. It does not redraw from the start.
- Reduced motion shows the whole line at once and moves the cursor without a spring. Off jumps to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | The line draws over about 840 ms and the end dot fades in as it lands, then settles | The whole line appears at once; the end dot appears without a spring | The final state |
| Scrubbing the cursor | The cursor moves on a snappy spring | The cursor moves without a spring | Immediate |
| The stretch after the cursor | Dims on a short standard tween | Dims on the same short tween | Immediate |
| The end dot while scrubbing | Grows to 10 units on a snappy spring | Immediate size change without a spring | Immediate |
| New data | The line morphs to the new shape on a smooth spring | Jumps to the new shape | Immediate |

## Related

- `BarChart` for discrete periods that need an axis, and `LineChart` for a trend with axes, several series or a legend.
- `MetricCard` for a single value with its change and no trend.
- `Gauge` for a value against a known range, and `BrushChart` for a long series people must zoom.

| Component | Choose it when |
|---|---|
| `BarChart` | Each period is a separate value and people compare the periods |
| `LineChart` | The trend needs a value axis, a time axis, several series or a legend |
| `MetricCard` | There is one current value, and its change matters more than its path |
| `Gauge` | The value is read against a fixed range or a threshold, such as a quota |
| `BrushChart` | A long series needs a zoomable window over the full range |
| `Treemap` | The question is how a total splits among parts, not how it moved over time |
