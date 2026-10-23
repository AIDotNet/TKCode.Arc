---
title: Brush chart
summary: A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
---

<Demo name="OverviewAndDetail" />

## When to use

`BrushChart` draws a long daily series in one plot and keeps the whole history in an overview strip under it. The
reader drags a window across the strip to choose the period they care about, and the plot follows. It belongs in the
body of an analytics or review page, where the series is the main content and exploring it is the task.

- **Daily active users on a SaaS admin console**: a product team reviews a year of daily sessions, then narrows the
  window to the weeks around a pricing change. The overview keeps the rest of the year visible, so the reader never
  loses their place.
- **Incident review timelines**: a reliability lead plots p95 latency for a quarter and marks deploys and outages with
  `Annotations`. Each event sits on the line and on the overview, so the review reads cause and effect together.
- **Revenue or order volume in a finance dashboard**: a controller opens a daily revenue series of several thousand
  points, presses a preset such as "Last 30 days" that sets `Range`, and reads the new window in a caption that
  `RangeChanged` updates.
- **Metered usage on a billing page**: a customer opens API calls per day for the current billing cycle, pans to a
  spike and reads its date and value before they open a support ticket.

## When not to use

- **A few weeks of data, or several series to compare**: use `LineChart`. It draws several series with a legend and
  shows the whole range without an overview strip.
- **A small trend in a table cell or a KPI tile**: use `Sparkline`, which has no axes and no interaction.
- **Negative values, or a value axis that must not start at zero**: use `LineChart`, which exposes `YMinimum` and
  `YMaximum`. The brush chart always starts its value axis at zero, so it cannot show a signed series.
- **Layers of a composition over time, such as traffic per channel**: use `Streamgraph`, which stacks the layers. The
  brush chart draws one series.
- **A single total that changes as a count goes up**: use `AnimatedCounter`, which animates the number itself.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BrushChart Label="Daily active users"
                    Unit="users"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

`BrushChart` derives from `ChartBase`, the base of the Arc chart family. The base supplies the shared shell: `Label`,
`Unit`, `PlotHeight`, `EmptyLabel`, `ValueFormat`, `ItemsSource` with `XBinding` and `YBinding`, and the tooltip rules.
The brush chart adds what a long history needs: the overview strip, the visible window (`Range`), its minimum span and
the event markers. It is self-drawn: the plot and the strip are rendered from the data in code, not assembled from child
elements, which keeps a long series responsive.

Each item supplies a date and a value. The date can be a `DateTimeOffset`, a `DateTime` or a `DateOnly`, and the
control converts it to a time internally. The control sorts the points by date whenever the data changes, so the
order of the source collection does not matter, although an ascending source avoids the work.

### Anatomy

1. **Plot**: the raw line for the points in view, or a seven-point average with its low-to-high band
   when the view is zoomed far out. The plot is 240 px tall by default (`PlotHeight`).
2. **Value axis**: on the right, in a 44 px gutter (the token's `GutterWidth`). It starts at zero and its top springs to
   the tallest point in view, with compact tick labels such as 1.2K unless `TickFormatter` is set.
3. **Gridlines**: horizontal lines at the value ticks. They follow `ShowGrid`, which is on by default.
4. **Date axis row**: 28 px tall under the plot (the token's `AxisRowHeight`). Its tick labels change with the window
   width, from days to weeks, months and years.
5. **Event markers**: a hairline with a dot in the second series color, labelled left to right. A label that would
   collide with another is skipped, so the marker stays even when its text does not fit.
6. **Crosshair and tooltip**: the crosshair sits on the active point. The tooltip shows the date, the value with its
   `Unit` and any event; it sits above the point, below it when there is no room, and never leaves the plot.
7. **Overview strip** (the overview part of the template): the whole history, drawn once per width, with the part outside the window
   stepped back. It is `OverviewHeight` tall (52 px by default), starts 8 px under the date axis and ends at the value
   gutter, so the two charts share one horizontal scale.
8. **Window and handles**: a ring over the overview with two grips at its edges. The grips are 28 by 44 px hit areas.
   Drag the ring to pan, a grip to resize, empty track to draw a new window, a tap to recenter and a double tap to reset.

### Choosing the window and the presets

`Range` is the visible window as a `ChartTimeRange` (a start and an end, both inclusive). When it is null the window
is the whole series. `Range` is two-way: a drag or a key press writes the new window back to it, and code can move the
window by assigning a new value. A code change glides to the new window on a spring at full motion, and jumps at
reduced or off motion.

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"Window: {e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

Use presets for the windows people ask for most: the last 30 days, the last quarter, the last year and the full
series. A preset is a plain assignment, so the same code also serves a view model. The reset gesture (Escape or 0
on the window) always returns to the full series, whatever the presets are.

### Annotations

`Annotations` marks events on the plot and on the overview strip. Each `ChartAnnotation` has a date, a short label and
an optional description. The label is printed beside the marker when there is room; the description appears in the
tooltip and in screen reader announcements, so it can carry the detail a short label cannot.

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 launch", "New onboarding flow shipped to everyone."),
    new ChartAnnotation(start.AddDays(410), "Press mention", "Featured in a weekly newsletter."),
    new ChartAnnotation(start.AddDays(530), "Pricing change")
];
```

Keep the list to the events that explain the shape of the series. Events are part of the data the reader interprets,
so a release that is not in the list cannot be found from the chart; add it when it ships, not when someone asks.

### Minimum span

`MinSpan` stops the window from shrinking below a span. The default is seven days (`TimeSpan.FromDays(7)`). The limit
applies to drags, handles, the keyboard and code: a range narrower than the minimum widens to it, and a range that
starts or ends outside the data is pulled back inside. The span is also capped by the series length, so a series
shorter than the minimum shows the whole series.

```xml
<arc:BrushChart Label="Requests" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

Pick the smallest span a reader should be able to inspect. A weekly review wants 14 days, so the window always shows
two full weekly cycles and a weekday pattern cannot hide between the points.

### Formatting

`DateFormatter` formats the dates in the tooltip and in the screen reader announcements, and `TickFormatter` formats
the value axis. The default date text is English (en-US), such as "Tue, Sep 15, 2026". The window text uses a short
form instead, such as "Jan 1, 2026 to Apr 10, 2026", and does not change with `DateFormatter`. Replace the defaults when the page uses another culture or a unit-aware number.

```xml
<arc:BrushChart Label="Revenue"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

`ValueFormat` (a .NET format string, such as `N0` or `C0`) applies to the values in the tooltip and announcements, and
`ShowGrid` turns the gridlines off for a quieter plot.

### Binding to a view model

The view model owns the series, the window and the events. The brush chart only draws them and writes the window back
when the reader moves it. With CommunityToolkit.Mvvm:

```csharp
public sealed record DailyUsage(DateTimeOffset Date, double Calls);

public partial class UsageViewModel : ObservableObject
{
    private readonly DateTimeOffset _lastDay = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    [ObservableProperty]
    private IReadOnlyList<DailyUsage> _usage = [];

    [ObservableProperty]
    private ChartTimeRange? _window;

    [ObservableProperty]
    private ObservableCollection<ChartAnnotation> _releases = [];

    [RelayCommand]
    private void ShowLast30Days() => Window = new ChartTimeRange(_lastDay.AddDays(-30), _lastDay);

    [RelayCommand]
    private void ShowAll() => Window = null;
}
```


- `Range` is two-way, so a drag or a key press writes `Window` back and a preset moves the plot. A code change to
  `Window` glides the window on a spring, so set it on a user action rather than on a timer.
- `Annotations` takes any `IEnumerable<ChartAnnotation>`. The markers redraw when the property is assigned, so after
  the list changes, assign it again (a new list or the same collection) to redraw.
- `ItemsSource` is replaced, not appended to. Assign a new `IReadOnlyList<DailyUsage>` when the series is reloaded.
- `XBinding` and `YBinding` read the date and the value. The compiled form `{CompiledBinding Date, DataType=...}` is
  the AOT-safe path; add `x:DataType` to the view when you use it. The examples leave it out for brevity.

### Example: API usage on a billing page

A billing page shows a customer the API calls they made in the current cycle. The reader needs to see the shape of the
cycle at a glance, jump to the last 30 days, and read a spike next to the release that caused it. The two preset
buttons call the view model's commands, which write `Window`; the chart reads and writes the same property, so a
preset and a drag always agree. The release markers come from the same list the product team uses for changelogs,
so the annotations are real events, not decoration. No screenshot is shown here: the chart draws its data only once
the view model supplies the points.

```xml
<StackPanel Width="720" Spacing="12">
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="Last 30 days" Variant="Secondary" Size="Small" Command="{Binding ShowLast30DaysCommand}" />
        <arc:Button Content="All" Variant="Secondary" Size="Small" Command="{Binding ShowAllCommand}" />
    </StackPanel>
    <arc:BrushChart Label="API calls per day"
                    Unit="calls"
                    ValueFormat="N0"
                    ItemsSource="{Binding Usage}"
                    XBinding="{Binding Date}"
                    YBinding="{Binding Calls}"
                    Range="{Binding Window, Mode=TwoWay}"
                    Annotations="{Binding Releases}" />
</StackPanel>
```

### Data shape and performance

The control expects one value per date. It reads each item's date through `XBinding` (or `XSelector`) and its value
through `YBinding` (or `YSelector`). Items whose date cannot be converted are skipped.

When the data changes, the control sorts the points, computes the seven-point average and its band once for the whole
series, and rebuilds the overview. Each data change is a full pass over the series, so replace the collection in one
assignment rather than adding points one at a time. The plot draws only the points in the window plus one on each side. When more
than two points fall into one pixel column, the line keeps only that column's low and high, so spikes survive and the
path stays small. A series of a year of daily points, or several thousand points, is the intended range.

When the data changes, the current window is kept and clamped to the new data and to the minimum span. A bound `Range`
wins over the current window, so set `Range` to the period you want and the reload follows it. The value axis
recalculates for the points in view and springs to the new top.

### Content guidelines

Name the series by what it counts and how often, because the reader sees the label before the axis. Keep the unit
short and put the event detail in the annotation description, not in the label.

| Instead of | Write |
|---|---|
| Chart | Daily active users |
| Users | Active users per day |
| Release | v2 launch |
| Click the chart to zoom | Drag the window to zoom |
| Long description of the release in the label | Short label, with the detail in the description |
| 12000.0 calls | 12,000 calls (`ValueFormat="N0"`) |

Keep the window text and the captions in sentence case, and write dates in the form the rest of the page uses, such as
"Oct 1, 2026". Do not put the window readout in a headline: the caption under the chart is enough.

### Best practices

**Do**

- Set `Range` to the period the page opens on. Past two points per pixel the line keeps only each pixel column's low
  and high, so a reader who needs individual points needs a shorter window; the whole series is only a fallback.
- Give the window presets next to the chart, and bind `Range` two-way so the presets and the drag stay in step.
- Set `MinSpan` to the shortest period the review needs. A weekly review wants 14 days, so the window never hides a
  weekly pattern.
- Add the annotations that explain the shape of the series. Each one is a marker the reader can find without reading
  the table.
- Give the chart a `Label` and a `Unit`, so the summary and the tooltip read correctly to a screen reader user.

**Avoid**

- Negative values or a value axis that must start at a value other than zero. The brush chart always starts at zero, so
  a signed series reads as a flat line near the bottom.
- Two-week or shorter series. Few points make the overview strip hard to read and the window adds nothing; use
  `LineChart`.
- Many overlapping annotations on one day. Labels that collide are skipped, so the reader loses the events they were
  meant to see.
- Writing `Range` from a timer or from a value that changes on every frame. Each write glides the window, so the plot
  never settles.
- Reloading the whole series for a small update. Each data change rebuilds the averages and the overview, so batch the
  changes into one assignment.

### Common questions

**Why does the window snap to seven days when I set `Range`?** `MinSpan` is seven days by default. A window narrower
than the minimum span widens to it, and the end moves so the span holds. Set `MinSpan` to the smallest span you want to
allow.

**Does setting `Range` from code animate?** At full motion it glides on a spring. At reduced or off motion it jumps.
Drags and keys always follow the pointer one to one, so only code changes use the spring.

**What does the value axis start from?** Zero, always. Its top springs to the tallest point in view, so zooming into a
quiet stretch fills the plot. Use `TickFormatter` to change the tick labels, not the scale.

**Why does the chart show "No data yet" with one point?** The brush chart needs at least two points to draw a line and
an overview. `EmptyLabel` replaces the default text, which is "No data yet" for this chart.

**How do I show the window in my own text?** Handle `RangeChanged` and read `e.Range`, which has `Start` and `End` as
`DateTimeOffset` values. Format them with your own pattern, as the example above does. The same window is also
available as `Range`.

**How do I change the date format in the tooltip?** Set `DateFormatter` to a `Func<DateTimeOffset, string>`. It
formats the tooltip and the announcements; the window text keeps its short form.

## API reference

<!-- api: BrushChart -->

### Companion types

| Type | Purpose |
|---|---|
| `ChartAnnotation(DateTimeOffset Date, string Label, string? Description = null)` | An event on the time axis, shown as a marker with a label and a tooltip note |
| `ChartTimeRange(DateTimeOffset Start, DateTimeOffset End)` | The visible window; `Span` returns its length |
| `ChartTimeRangeChangedEventArgs` | The `RangeChanged` payload; its `Range` property is the new window |

## Accessibility

- `Label` names the chart. The window and its two handles are sliders, and their value text is the date.
- The plot is a focusable group. Its automation name is the label, the window readout and a hint to use the left and
  right arrows to read values.
- The reading for a point announces the date, the value with its unit and any event on that day, for example "Sun, Jan
  11, 2026, 110 users. Launch".
- A hidden data table lists every point in the window and its events. Events are marked with a second series color and
  a text label or a tooltip, so they are not identified by color alone.
- The focus ring of the window and its handles is drawn only for keyboard focus.

### Keyboard

The overview has four keyboard stops in this order: the plot, the window, the start handle and the end handle. Tab and
Shift+Tab move between them.

| Keys | Action |
|---|---|
| Plot: `Left` / `Right` | Move the crosshair one point back or forward; the first `Right` reads the last visible point |
| Plot: `PageUp` / `PageDown` | Move the crosshair by one eighth of the visible points |
| Plot: `Home` / `End` | Move the crosshair to the first or last visible point |
| Plot: `Escape` | Clear the crosshair |
| Window: `Left` / `Down`, `Right` / `Up` | Pan one tenth of the window back or forward |
| Window: `Shift` + arrow, `PageUp` / `PageDown` | Pan half the window |
| Window: `+` / `-` | Zoom in or out around the center |
| Window: `Home` / `End` | Move the window to the start or the end of the series |
| Start or end handle: `Left` / `Right` | Move the edge one point (one day in a daily series) |
| Start or end handle: `Shift` + arrow | Move the edge ten points |
| Start handle: `Home` / `End` | Move the start to the first point, or to the minimum span before the end |
| End handle: `Home` / `End` | Move the end to the minimum span after the start, or to the last point |
| Window or handle: `Escape` / `0` | Reset the window to the whole series |

## Motion

- On first view the line draws in from the left once, on a long spring without overshoot.
- Dragging follows the pointer one to one at every motion level. Presets, resets and changes of `Range` slide the window
  on a spring at full motion and jump at reduced or off motion.
- The value axis springs to the highest point in view at full motion. The raw line and the smoothed trend cross-fade as
  the density changes.
- The crosshair glides between points at full motion.
- Reduced motion places the window at once and keeps the crosshair where it is. Off jumps to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | The line draws in from the left once, on a long spring | The finished line is shown | The finished line is shown |
| Drag the window or a handle | Follows the pointer one to one | Follows the pointer one to one | Follows the pointer one to one |
| Preset, reset or new `Range` | The window glides on a spring | The window moves at once | The window moves at once |
| Value axis top | Springs to the highest point in view | Moves at once | Moves at once |
| Crosshair | Glides between points | Moves at once, keeps its place | Jumps to the point |

## Related

- `LineChart` for a few weeks of data, several series or a signed value axis.
- `Sparkline` for a small trend without interaction.
- `Streamgraph` for the composition of several layers over time.
- `AnimatedCounter` for a single total that changes.

| Component | Choose it when |
|---|---|
| `LineChart` | The period is short, several series must be compared or values can be negative |
| `Sparkline` | The trend sits in a table cell or a KPI tile and needs no interaction |
| `Streamgraph` | The question is how the parts of a whole change over time |
| `AnimatedCounter` | One total changes and the number itself is the point |
| `BarChart` | Values are compared by period rather than read along a continuous timeline |
