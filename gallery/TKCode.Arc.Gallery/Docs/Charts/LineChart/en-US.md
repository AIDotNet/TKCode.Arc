---
title: Line chart
summary: A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
---

<Demo name="Ranges" />

## When to use

`LineChart` shows one to three measures over time and lets the reader compare them at any point. It is the main trend
chart of an analytics page: it sits under a row of KPI tiles and follows a range selector (7, 30 and 90 days). Each
series is a stable line, so a range change reshapes the same lines instead of replacing them with a new picture.

- **Signups or revenue over a selected range** on an analytics overview. The 7, 30 and 90 day ranges share the same
  series, so the lines morph from their current shape when the person changes the range.
- **Current period against the previous period** on a billing or growth dashboard. A dashed second series shows whether
  this month is above or below the last one without a second chart.
- **Latency or error rate over time** on an incident page. A single series with `Unit="ms"` and `Curve="Linear"` keeps
  spikes at their true height, and the tooltip reads the exact value with its unit.
- **Refunds or failed payments per day** in a fintech operations console. `EmptyLabel` explains a quiet range, and
  `IsLoading` keeps the last shape on screen while the next range is fetched.
- **Usage against a target** on a quota page. A dashed target line sits beside the actual usage, and the legend lets the
  reader hide either one.

## When not to use

- **A compact trend next to one number**, such as a KPI tile or a table cell: use `Sparkline`, which has no axes, legend
  or tooltip.
- **Discrete categories such as days of the week, regions or plans**: use `BarChart`. Bars compare separate values; a
  line implies that the space between two points means something.
- **Parts of a whole**, such as revenue share by plan: use `DonutChart`. Use `WaffleChart` when the parts are counts of
  equal units.
- **More than about four measures**: the palette has four series colors, and the rest take neutral steps that are hard
  to tell apart. Use `Streamgraph` when the layers add up to a total.
- **A long history that needs zooming**: use `BrushChart`, which pairs a detail view with an overview to brush over.
- **Daily activity across a year**, such as logins per day in a calendar grid: use `ActivityHeatmap`.

## Usage

Each `ChartSeries` has a stable `Key`, so the lines keep their identity when the data changes. Set `Label` to name the
chart for assistive technology.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LineChart Label="Signups" CategoryLabel="Date" ItemsSource="{Binding Days}">
        <arc:LineChart.Series>
            <arc:ChartSeries Key="current" Name="This period" Values="{Binding Current}" />
            <arc:ChartSeries Key="previous" Name="Previous period" Values="{Binding Previous}" IsDashed="True" />
        </arc:LineChart.Series>
    </arc:LineChart>
</UserControl>
```

`LineChart` derives from `ChartBase`, which derives from Avalonia's `TemplatedControl`. The template holds a legend above
a plot that draws every line, area, gridline, label and tooltip itself. Transitions cannot reach the coordinates inside
that one drawing, which is why the chart's motion is driven in code.

### Anatomy

1. **Legend** (`ShowLegend`): one toggle pill per series with a color swatch, 16 px above the plot. It reaches 8 px into
   the margins, so the hover pills line up with the plot edges.
2. **Plot** (`PlotHeight`): 220 px tall by default and as wide as its container. The top tick sits 12 px below the top.
3. **Gridlines** (`ShowGrid`): horizontal lines at the value ticks, in the border color, with a 1 px baseline in the
   strong border color.
4. **Lines**: 2 px round-capped strokes in the series color. A dashed series is 0.25 px thinner, so it does not look
   heavier than a solid line beside it.
5. **Area fill**: a faint 9% tint under the line. By default only the first series has one (`HasArea` changes this).
6. **Value axis** (`ShowYAxis`): tick labels in a 48 px gutter on the right, compact by default (1.2K, 2.4K).
7. **Date axis** (`ShowXAxis`): one 26 px row under the plot. Labels are spaced so they do not collide, and
   `XAxisFormatter` can drop one by returning null.
8. **Crosshair and dots**: a vertical line at the active point and a 4 px dot on each visible line, outlined in the
   surface color.
9. **Tooltip**: the date, then one row per visible series with its value and unit. It sits 14 px to the side of the
   crosshair and flips before it would leave the plot.

### Ranges

Replace the rows and the series values together when the range changes, so every series has one value per row of the
new range. Each line then morphs from the shape on screen, and the value axis springs to the new range.

```csharp
void ShowRange(SignupDay[] rows)
{
    chart.ItemsSource = rows.Select(r => r.Date).ToArray();
    chart.Series[0].Values = rows.Select(r => (double)r.Current).ToArray();
    chart.Series[1].Values = rows.Select(r => (double)r.Previous).ToArray();
}
```

The demo at the top shows the same two series for 7, 30 and 90 days. The axis rescales to the range on screen, and the
lines keep their keys.

### Single series and units

A list of `ChartPoint` items needs no series. `Unit` is shown in the tooltip, and `Curve` switches from the default
smooth curve to straight segments. `Smooth` never swings past the data; use `Linear` when a reader might take the curve
between two days as a real value.

<Demo name="SingleSeries" />

```xml
<arc:LineChart Label="p95 latency" Unit="ms" Curve="Linear" PlotHeight="180" ItemsSource="{Binding Latency}" />
```

### Legend and hidden series

With more than one series the legend shows a toggle per series. `ShowLegend` forces it on or off, and `HiddenSeries`
holds the hidden keys.

```xml
<arc:LineChart Label="Signups"
               ItemsSource="{Binding Days}"
               ShowLegend="True"
               HiddenSeries="{Binding HiddenKeys}" />
```

Hiding a series fades it out and flattens it into the baseline, and the value axis rescales to the series that remain.
`HiddenSeries` is two-way: a legend toggle writes the new list back and raises `HiddenSeriesChanged`, and a change from
code shows or hides the line. When every series is hidden, the plot says "Choose a series to show".

### Loading and empty states

`IsLoading` dims the lines and shows a skeleton when there are no points. `EmptyLabel` explains an empty range.

<Demo name="Empty" />

```xml
<arc:LineChart Label="Refunds" EmptyLabel="No refunds in this range" IsLoading="{Binding IsBusy}" />
```

The default empty text is "No data for this range". Write the reason, so the reader knows whether the chart is wrong or
the period is quiet. While `IsLoading` is true and there are no points, the skeleton shows instead. With points, the
current shapes stay on screen and breathe, which says that new data is coming.

### Reading values

`ActiveIndexChanged` fires while the crosshair moves and when it is released. Use it to show the values in a title.

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
    ? "Hover or use the arrow keys to read values."
    : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
```

`ActiveIndex` holds the same point and is two-way, so code can point the readout at a day, such as the day of an
incident. `Values` lists only the visible series, so a hidden series never appears in the readout.

### Binding to a view model

With CommunityToolkit.Mvvm the view model owns the rows, the range and the hidden series. It replaces the whole array
for each range, and the chart morphs from the previous rows to the new ones.

```csharp
public sealed record SignupDay(string Date, int Current, int Previous);

public interface ISignupService
{
    Task<SignupDay[]> GetDailyAsync(int days);
}

public partial class SignupsViewModel : ObservableObject
{
    private readonly ISignupService _signups;

    public SignupsViewModel(ISignupService signups) => _signups = signups;

    [ObservableProperty]
    private SignupDay[] _days = [];

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [ObservableProperty]
    private int? _activeIndex;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task LoadRangeAsync(int days)
    {
        IsBusy = true;
        try
        {
            // A new array is one data change: the chart takes the rows once and morphs the lines to them.
            Days = await _signups.GetDailyAsync(days);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

```xml
<arc:LineChart Label="Signups"
               CategoryLabel="Date"
               Height="260"
               ItemsSource="{Binding Days}"
               XBinding="{Binding Date}"
               HiddenSeries="{Binding HiddenKeys, Mode=TwoWay}"
               ActiveIndex="{Binding ActiveIndex, Mode=TwoWay}"
               IsLoading="{Binding IsBusy}">
    <arc:LineChart.Series>
        <arc:ChartSeries Key="current" Name="This period" ValueBinding="{Binding Current}" />
        <arc:ChartSeries Key="previous"
                         Name="Previous period"
                         IsDashed="True"
                         ValueBinding="{Binding Previous}" />
    </arc:LineChart.Series>
</arc:LineChart>
```

- `XBinding` reads the category from each record. Without it the chart reads the item itself, which works for `string`
  and `DateTime` items but not for a record.
- `HiddenSeries` and `ActiveIndex` are two-way, because the legend and the crosshair write them. `ItemsSource`,
  `IsLoading` and the value bindings are one-way.
- The examples use plain `Binding` for brevity. In an app that compiles its bindings, set `x:DataType` on the view and a
  `DataType` on each binding.

### Data shape and performance

`ItemsSource` holds one item per point, oldest first. Every series reads its value from the same index, so all series
need one value per row, in the same order. A missing value counts as zero, so a day without data draws as a drop to
zero. Supply a real value for every day in the range.

Format numbers with `ValueFormat`, a .NET format string read with en-US conventions: `N0` for counts, `C0` for money,
`0.0'%'` for a percentage. The default groups thousands and shows at most one decimal, so 2400 reads as "2,400". For a
currency that depends on the reader's locale, set `ValueFormatter`. Dates default to "Tue, Sep 15" in the tooltip and
"Sep 15" on the axis; set `XLabelFormatter` or `XAxisFormatter` for another label.

### Value axis

The value axis starts at zero, or at the data minimum when the data dips below zero, and ends at the next tick above the
largest value. Change that when the business question needs it:

- `YMinimum` and `YMaximum` fix either end. Fix both for a percentage (0 to 100) so the axis does not jump between ranges.
- `TickFormatter` formats the tick labels only, for example `chart.TickFormatter = v => $"{v:0}%";` in code.
- `ShowYAxis="False"` removes the gutter when the values are shown elsewhere in the card, such as in a KPI tile above.
- A `YMinimum` away from zero makes a small change look large. Use it only where zero is not a meaningful baseline, such
  as a score, and say so in the card.

```xml
<arc:LineChart Label="Checkout conversion"
               YMinimum="0"
               YMaximum="100"
               ValueFormat="0.0'%'" />
```

### Example: signups overview with a dashed comparison

A growth dashboard for Acme Analytics shows the signups of the last ten days against the same ten days of the previous
month. The current period is solid, the comparison is dashed, and the legend lets the reader hide the comparison. The
caption states the period, the total and the change in words, so the headline reads without the tooltip.

![A signups overview card with a solid current period, a dashed previous period and a legend](images/SignupsOverview.png)

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
            <TextBlock Text="Signups" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Sep 1 to Sep 10, 2026. 1,411 new workspaces, up 28% on the previous period."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:LineChart Label="Signups"
                       CategoryLabel="Date"
                       PlotHeight="200">
            <arc:LineChart.ItemsSource>
                <collections:AvaloniaList x:TypeArguments="sys:String">
                    <sys:String>Sep 1</sys:String>
                    <sys:String>Sep 2</sys:String>
                    <sys:String>Sep 3</sys:String>
                    <sys:String>Sep 4</sys:String>
                    <sys:String>Sep 5</sys:String>
                    <sys:String>Sep 6</sys:String>
                    <sys:String>Sep 7</sys:String>
                    <sys:String>Sep 8</sys:String>
                    <sys:String>Sep 9</sys:String>
                    <sys:String>Sep 10</sys:String>
                </collections:AvaloniaList>
            </arc:LineChart.ItemsSource>
            <arc:LineChart.Series>
                <arc:ChartSeries Key="current" Name="This period">
                    <arc:ChartSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>120</sys:Double>
                            <sys:Double>138</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>152</sys:Double>
                            <sys:Double>149</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>104</sys:Double>
                            <sys:Double>160</sys:Double>
                            <sys:Double>171</sys:Double>
                            <sys:Double>168</sys:Double>
                        </collections:AvaloniaList>
                    </arc:ChartSeries.Values>
                </arc:ChartSeries>
                <arc:ChartSeries Key="previous" Name="Previous period" IsDashed="True">
                    <arc:ChartSeries.Values>
                        <collections:AvaloniaList x:TypeArguments="sys:Double">
                            <sys:Double>96</sys:Double>
                            <sys:Double>104</sys:Double>
                            <sys:Double>112</sys:Double>
                            <sys:Double>109</sys:Double>
                            <sys:Double>118</sys:Double>
                            <sys:Double>92</sys:Double>
                            <sys:Double>88</sys:Double>
                            <sys:Double>124</sys:Double>
                            <sys:Double>131</sys:Double>
                            <sys:Double>127</sys:Double>
                        </collections:AvaloniaList>
                    </arc:ChartSeries.Values>
                </arc:ChartSeries>
            </arc:LineChart.Series>
        </arc:LineChart>
    </StackPanel>
</Border>
```

### Content guidelines

- **Label**: the measure, with its unit when the axis has none: "Signups", "p95 latency (ms)". The label names the chart
  for assistive technology and the data table, so it must make sense on its own.
- **Series name**: a short noun phrase for the period or the comparison: "This period", "Previous period", "Target".
- **Empty text**: the reason for the empty range, with the scope named: "No refunds in this range".
- **Card title and caption**: state the total or the change in words, with numbers as digits, such as "1,411 new
  workspaces".

| Instead of | Write |
|---|---|
| Data | Signups per day |
| Previous | Previous period |
| No data | No refunds in this range |
| Series 1 | This period |
| Chart of revenue | Revenue, Sep 1 to Sep 10, 2026 |

### Best practices

**Do**

- Use one series per measure and name each for its period or meaning. The legend and the tooltip repeat the name, so it
  must read without the card title.
- Set `CategoryLabel` to the unit of the axis ("Date", "Week"). The data table uses it as the first column, so screen
  reader users hear the axis name first.
- Replace the whole `ItemsSource` and the series values together on a range change, so the lines morph between two
  consistent states.
- Use `Linear` for latency and counts, and the default `Smooth` for trends where the shape matters more than the exact
  points.

**Avoid**

- A non-zero `YMinimum` on a metric that starts at zero, such as signups or revenue. A truncated axis makes a 5% change
  look like a collapse.
- More than three or four series. Beyond that the colors are hard to tell apart and the tooltip becomes a long list.
- Refreshing the data every second. The lines, the axis and the tooltip all move at once, which hides the change you
  want to show.
- Zero as a placeholder for a day with no data. A missing value counts as zero, so the line drops and reads as a real fall.
- `Smooth` for counts that come in whole units, such as refunds. A curve between two days suggests values that never
  happened.

### Common questions

**Why does the value axis start at zero?** The default baseline is zero, or the data minimum when the data dips below
zero. Set `YMinimum` to change it, and use that deliberately, as described in the value axis section.

**Why is there no legend on my chart?** With one series the legend is hidden by default. Set `ShowLegend="True"` to show
it, or add a second series and the legend appears on its own.

**Why does only the first line have a fill?** `HasArea` is null by default, so only the first series gets an area.
Set `HasArea="True"` on another series to fill it too, or `HasArea="False"` to remove the fill.

## API reference

<!-- api: LineChart -->

## Accessibility

- `Label` names the chart. The plot exposes the date range and the latest value of each series in a hidden summary, and
  a hidden data table lists every point.
- The plot is a focusable slider. Arrow keys move one point, PageUp and PageDown move about a sixth of the range, Home
  and End go to the first and last point, and Escape releases the crosshair.
- The readout for the active point names the date and each visible series with its value and unit, such as "Fri, Sep 4:
  This period 240 users, Previous 160 users". Keyboard focus starts the readout at the latest point.
- The legend entries are toggle buttons. Each series is identified by its name and its line style, not only by color.
- The loading state is announced politely as "Loading". The lines and the tooltip are hidden from assistive technology
  to avoid repeated reading.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the legend toggles and the plot. Entering the plot from the keyboard selects the latest point |
| `Left` / `Down` | Move to the previous point. From rest, the first press lands on the latest point |
| `Right` / `Up` | Move to the next point. From rest, the first press lands on the latest point |
| `PageUp` / `PageDown` | Move about a sixth of the points; PageUp from rest lands on the latest point |
| `Home` / `End` | Move to the first or the last point |
| `Escape` | Release the crosshair and hide the tooltip |
| `Enter` / `Space` (on a legend toggle) | Show or hide that series |

Leaving the plot releases the crosshair. A mouse reads the nearest point while it hovers the plot. Touch reads on press
and drag, and releases when the finger lifts.

## Motion

- On first view the lines draw in from left to right, and the area fill is revealed with the line.
- A new range resamples the lines on shared x positions and morphs them from their current shape. The gridlines slide
  with the value axis.
- The crosshair glides between points, and the tooltip follows on its own spring and flips at the edges.
- Loading lines pulse until the data arrives. The pulse pauses when the chart leaves the viewport.
- Reduced motion skips the draw-in, the morph and the glide, and stops the loading pulse. Off jumps straight to the
  final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Lines draw in from the left over about 840 ms, and the area reveals with them | The lines fade in without the draw-in | Immediate |
| New range | Lines morph from their current shape; the value axis springs and the gridlines slide | Immediate | Immediate |
| Crosshair moves | Glides between points on a spring | Jumps to the point | Jumps to the point |
| Loading | Lines breathe, dimmed | Pulse stops | Pulse stops |

## Related

- `Sparkline` for a compact trend beside a value.
- `BarChart` for one measure compared across discrete days, weeks or months.
- `BrushChart` for a dense time series that needs zooming.
- `Streamgraph` when several series should be read as stacked layers.

| Component | Choose it when |
|---|---|
| `Sparkline` | The trend sits beside one number, with no axes or legend |
| `BarChart` | The categories are discrete days, weeks or months and each value stands alone |
| `DonutChart` | The data is a few parts of one whole |
| `BrushChart` | The history is long and the reader needs to zoom into a period |
| `Streamgraph` | Several layers should read as parts of one stacked total |
| `ActivityHeatmap` | The data is a grid of days, such as activity across a year |
