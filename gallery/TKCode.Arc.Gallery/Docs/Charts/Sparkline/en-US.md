---
title: Sparkline
summary: Show a compact trend beside a value.
---

## When to use

- A small trend inside a card, a table row or a KPI block, next to its headline value.
- A trend whose exact points matter less than its direction and shape.
- A value that the reader can scrub to see the earlier points, without a full axis.

## When not to use

- Discrete periods that need an axis for comparison: use `BarChart`.
- A single latest value with its change and no trend: use `MetricCard`.
- A trend that needs axes, several series or a legend: use `LineChart`.

## Usage

`ItemsSource` holds the values from oldest to newest. `Value` and `Change` form the headline and the aside shown at rest.

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

### Tones

`Tone` colors the line and the change. Use `Success` or `Danger` to show the direction of a metric, and `Warning` for a
value that needs attention.

```xml
<arc:Sparkline Label="Churn" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="Queue depth" Value="1,204" Change="Rising" Tone="Warning" ItemsSource="{Binding Queue}" />
```

### Point labels

`PointLabels` gives one label for each point. When the reader scrubs the line, the label of that point is read and shown
instead of the position.

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

### Without a headline

When `Value` is not set, the scrubbed value and its point are shown beside the label. New data morphs the line.

```xml
<arc:Sparkline Label="Visits" Width="300" ItemsSource="{Binding Visits}" />
```

### Dense tables

Set `IsInteractive="False"` in a table with many rows. The sparkline then has no tab stop and no pointer handling. Set
`ShowArea="False"` to keep the line quiet.

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

## API reference

<!-- api: Sparkline -->

## Accessibility

- `Label` names the chart. The drawing itself is hidden from assistive technology.
- When the chart is interactive, the plot is a focusable slider. Its value text reads the point label and the value.
- The left and right arrow keys, and also the up and down arrow keys, move one point. PageUp and PageDown move about a
  sixth of the range, Home and End go to the first and last point, and Escape stops scrubbing and returns to the latest
  value.
- Set `PointLabels` so that a scrubbed point is read with its date, not with its position in the list.
- The headline and the change are plain text, so they are read as they are.

## Motion

- On first view the line draws in once, and the end dot appears as the line lands.
- While scrubbing, the cursor moves between points on a snappy spring, and the points after the cursor dim.
- The headline and the aside roll to the new text, and the width of the headline eases to fit it.
- Reduced motion shows the whole line at once and moves the cursor without a spring. Off jumps to the final state.

## Related

- `BarChart` for discrete periods that need an axis.
- `LineChart` for a trend with axes, several series or a legend.
- `MetricCard` for a single value with its change and no trend.
- `BrushChart` for a dense series that needs zooming.
