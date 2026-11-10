---
title: Metric ticker
summary: A strip of live metrics that scrolls like a stock ticker or stays in columns, rolling each value digit by digit and showing how much it moved and in which direction.
---

<Demo name="LiveTicker" />

## When to use

`MetricTicker` keeps a handful of live numbers in view without taking much room. Each metric has a label and a value;
whenever the value changes, its digits roll to the new number, the change since the previous reading appears next to it
with an up or down arrow, and the cell flashes once in the good or bad tint. Whether a rise is good news is up to the
metric: requests per second going up is good, latency going up is not. The strip either scrolls past in one line, like
a stock ticker with faded edges, or sits still in even columns that wrap on narrow screens.

- **Status bars and page headers**: service health, queue depth and error rates on an operations page.
- **Live dashboards**: a band of key numbers above the charts that reacts as data streams in.
- **Event and sales screens**: orders, revenue and sign-ups ticking up during a launch.

## When not to use

- **One number that matters**: use `AnimatedCounter` or a large stat, not a strip.
- **History and trends**: a ticker shows the last change only; use a `Sparkline` or a `LineChart` for how a value moved
  over time.
- **Numbers people must compare or copy**: scrolling text is hard to read closely; use `Mode="Roll"` or a `Table`.
- **Alerts**: a ticker is ambient. Something that needs action belongs in a toast or a banner.

## Usage

Add `MetricTickerItem` items with a `Label` and a first `Value`, then set `Value` as new readings arrive. The first
value is the starting point; every later one is a change.

```xml
<arc:MetricTicker Label="Service metrics">
    <arc:MetricTickerItem Label="Requests / s" Value="1240" />
    <arc:MetricTickerItem Label="p95 latency" Value="182" Suffix=" ms" IsHigherBetter="False" />
    <arc:MetricTickerItem Label="Error rate" Value="0.42" Decimals="2" Suffix="%" ChangeFormat="Absolute" />
</arc:MetricTicker>
```

```csharp
ticker.Items[0].Value = 1310; // rolls to 1,310 and shows +5.6% in the success color
```

`Decimals`, `Prefix` and `Suffix` format the value. `ChangeFormat` shows the change as a percentage of the previous
value (the default), in the metric's own unit (`Absolute`) or not at all (`None`). `IsHigherBetter="False"` turns a
rise into bad news. `Change` and `PreviousValue` report the last change; `ItemsSource` takes the metrics from a view
model instead of `Items`.

### Scrolling or columns

In `Mode="Scroll"` (the default) the metrics run past at `Speed` pixels per second. The line holds while the pointer is
over it or focus is in it (`PausesOnHover`), and the round pause button stops it until it is pressed again
(`IsPaused`, two way). When all metrics fit, the line does not move. `Mode="Roll"` lays the metrics out in columns of at
least 150 px that wrap onto more lines.

<Demo name="Columns" />

### Reduced motion

Under reduced motion a scrolling ticker stands still and shows its metrics in columns. Values still change, without
rolling, and the flash is a short fade.

<Demo name="ReducedMotion" />

## API reference

<!-- api: MetricTicker -->

<!-- api: MetricTickerItem -->

## Accessibility

- The ticker is a list named by `Label` ("Live metrics"). Each metric is one item whose name says the label, the value
  and the last change in words ("p95 latency, 182 ms, down 3.1%"). The copy that fills the scrolling line is hidden from
  screen readers, so each metric is read once.
- Values are not announced as they change, so a busy feed does not talk over the reader.
- The pause button is a real button named "Pause ticker" or "Play ticker"; Enter and Space press it. Moving content can
  always be stopped, and focus inside the strip holds it too.
- The change carries an arrow and a sign as well as a color.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Scrolling line | Slides at `Speed` px/s, holds on hover and focus | Columns, no movement | Columns, no movement |
| New value | Digits roll | Change at once | Change at once |
| Change flash | The cell's tint fades out over 1.2 s | Same fade | None |

The scrolling loop runs only while the strip is on screen and something has to move.

## Responsive behavior

- Scrolling, the strip fills its width and fades both edges; there is no minimum width.
- In columns, the number of columns follows the width (at least 150 px each) and extra metrics wrap onto new lines;
  labels are cut with an ellipsis when a column is narrow.
- Touch does not hold the line (there is no hover); the pause button does.

## Related

| Component | Choose it when |
|---|---|
| `AnimatedCounter` | One number rolls on its own |
| `Sparkline` | You show how a value moved over time |
| `SplitBar` | The numbers are parts of one total |
| `LiveVisitors` | You want a full realtime visitors card |
