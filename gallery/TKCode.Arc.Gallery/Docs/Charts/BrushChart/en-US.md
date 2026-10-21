---
title: Brush chart
summary: A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
---

<Demo name="OverviewAndDetail" />

## When to use

- A daily series that covers a year or more, where the reader needs to zoom into a period.
- Incident reviews and other timelines with annotated events that must be read in place.
- A dataset with thousands of points, where the overview strip keeps the full range visible.

## When not to use

- A few weeks of data or several series to compare: use `LineChart`.
- A small trend that needs no interaction: use `Sparkline`.
- Negative values or a logarithmic scale: the value axis always starts at zero, so these do not fit.

## Usage

`ItemsSource` holds the points in ascending date order. Dates are read from the `ChartPoint` X value, and the value
axis is the point value. The overview strip under the plot shows the full range, and the window selects what the plot
shows.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BrushChart Label="Daily active users"
                    Unit="users"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

### Annotations

`Annotations` marks events on the plot and on the overview strip. Each annotation has a date, a label and an optional
description that appears in the tooltip.

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 launch", "New onboarding flow shipped to everyone."),
    new ChartAnnotation(start.AddDays(410), "Press mention", "Featured in a weekly newsletter."),
    new ChartAnnotation(start.AddDays(530), "Pricing change")
];
```

### Presets and the window

`Range` is the visible window and is two-way. Assign a new value to move the window; it slides there on a spring. Use
`RangeChanged` to show the window in text.

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"Window: {e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

### Minimum span

`MinSpan` stops the window from shrinking below a span. The default is seven days.

```xml
<arc:BrushChart Label="Requests" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

### Formatting

`DateFormatter` formats the dates in the tooltip and the window text, and `TickFormatter` formats the value axis.

```xml
<arc:BrushChart Label="Revenue"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

## API reference

<!-- api: BrushChart -->

## Accessibility

- `Label` names the chart. The window and its two handles are sliders, and their value text is the date.
- The plot is a focusable group. Its readout announces the date, the value and any event on that day.
- In the plot, the left and right arrow keys move the crosshair one point. In the window, the left and right arrow keys
  pan by a tenth of the window, Shift or PageUp and PageDown pan by half, and the plus and minus keys zoom around the
  centre.
- On a handle, the left and right arrow keys move one point and Shift moves ten. Home and End go to the first and last
  point, and Escape or 0 resets to the full range.
- A hidden data table lists every point in the window and its events. Events are marked with a second series color and
  a text label or a tooltip, so they are not identified by color alone.

## Motion

- On first view the line draws in from the left once, on a long spring without overshoot.
- Dragging follows the pointer one to one. Presets, resets and changes of `Range` slide the window on a spring.
- The value axis springs to the highest point in view. The raw line and the smoothed trend cross-fade as the density
  changes.
- The crosshair glides between points.
- Reduced motion places the window at once and keeps the crosshair without moving it. Off jumps to the final state.

## Related

- `LineChart` for a few weeks of data or several series.
- `Sparkline` for a small trend without interaction.
- `Streamgraph` for the composition of several layers over time.
- `AnimatedCounter` for a single total that changes.
