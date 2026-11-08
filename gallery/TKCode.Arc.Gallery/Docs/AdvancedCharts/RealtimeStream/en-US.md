---
title: Realtime stream
summary: A live line chart that scrolls as readings arrive, holds still under the pointer so a point can be read, and marks readings flagged as anomalies.
---

<Demo name="ServiceLatency" />

## When to use

`RealtimeStream` shows a value as it is being measured. The newest reading enters at the right, the line scrolls left,
and the reader can stop it at any moment to read a point. Use it when someone is watching the number now and wants to
see where it is heading.

- **Service health** on an operations screen: request latency, error rate or throughput, with the deploys and retry
  bursts your alerting flags marked on the line.
- **Queues and workers** in a back-office console: how deep a job queue is right now, on a fixed axis so the height of
  the line means the same thing all day.
- **Sensors and equipment** in an industrial or facilities dashboard: temperature, pressure or soil moisture, one reading
  every second or so.
- **A live session** in a trading, audio or streaming tool: bitrate, price or viewer count during the session itself.

## When not to use

- **A history people explore**, such as last month's revenue: use `LineChart`, which draws the whole range at once.
- **A long, dense series with zooming** and a selected range: use `BrushChart`.
- **A small trend inside a table row or a card**: use `Sparkline`. A stream needs the width to scroll in.
- **A single current number with no trend**: use `Gauge` or `AnimatedCounter`.
- **Finding the anomalies**: the chart marks what you flag and does not judge the readings itself. Run detection in your
  own system and set `Anomaly` on the readings it flags.

## Usage

Give the chart a `Label`, a `Unit` and readings. `Append` is the cheapest way to feed it: it takes a time, a value and an
optional anomaly text, and allocates nothing of its own.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream x:Name="Latency"
                        Label="Checkout latency"
                        Unit=" ms" />
</UserControl>
```

```csharp
// Called for every reading, on the UI thread.
Latency.Append(reading.Time, reading.Milliseconds);

// Your alerting flagged this one: the text becomes its marker.
Latency.Append(reading.Time, reading.Milliseconds, "Retry burst");
```

### Anatomy

- **Header**: `Label`, the newest value in rolling digits and `Unit`.
- **Status button**: a dot and `LiveLabel` while the view follows the feed, a pause glyph and `PausedLabel` while it holds
  still. The shape and the word carry the state, not only the color. Pressing it toggles `IsPaused`.
- **Plot**: the line with a soft area under it, horizontal grid lines and value labels. A dot rides the leading edge.
- **Time labels**: round moments under the plot that scroll with the line. There are fewer of them on a narrow plot.
- **Markers**: a diamond on each flagged reading with its text in a small chip. The diamond and the words make it readable
  without color.
- **Tooltip**: the time and value of the reading under the pointer or the keyboard, plus its anomaly text.

### Feeding from a collection

`Points` takes any list of `RealtimeStreamPoint`. An `ObservableCollection` is watched: added items are appended and
nothing else is read again. A feed that removes its oldest items to bound its own memory does not disturb the chart,
which keeps what it needs and trims the rest. Replacing or clearing the collection reads it again.

<Demo name="QueueDepth" />

### Window and interval

`WindowDuration` is how much time the plot shows. `Interval` is how often readings usually arrive, and the view trails the
newest reading by this much. So the line always has somewhere to go, and it grows smoothly toward the next reading instead
of jumping when it comes. Set `Interval` close to the real pace of your feed. If it is much shorter, the line waits at
the newest reading until the next one arrives.

### Value axis

By default the axis follows the readings in view, with a little room and round numbers, and eases to each new range. Set
`Minimum` and `Maximum` to fix it, or set just one to fix that end. A fixed axis suits a value with known limits, such as
a percentage or the capacity of a queue.

### Anomalies

Set `Anomaly` on a `RealtimeStreamPoint`, or pass the text to `Append`. The marker pops in when the line reaches the
reading. A screen reader hears each flagged reading once, when it arrives. Readings in the initial `Points` are history:
they are marked but not announced.

### Pausing

The view holds still while `IsPaused` is true, while the pointer rests on the plot (`IsPauseOnHover`), during a touch and
while the keyboard is reading points. Readings that arrive meanwhile are kept and the header stays current. On resume the
line glides back to live. `IsHeld` tells you whether the view holds still for any reason, and `PausedChanged` reports each
change with its `Reason`.

Bind `IsPaused` when your screen has its own control for it. Turning off `IsPauseOnHover` keeps the chart moving under the
pointer, which suits a wall display or a chart that sits under a working cursor.

<Demo name="ControlledPause" />

### Formats

`ValueFormatter` replaces the default number format everywhere, and the header then shows its text instead of rolling
digits, because digits can only roll for plain numbers. `TimeFormatter` receives the time in the offset your readings
carry; the default is `HH:mm:ss`.

```csharp
Latency.ValueFormatter = value => $"{value / 1000:0.00} s";
Latency.TimeFormatter = time => time.ToString("mm:ss");
```

### Binding to a view model

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RealtimeStream Label="Render queue"
                        Unit=" jobs"
                        Points="{Binding Readings}"
                        IsPaused="{Binding IsReviewing}"
                        Minimum="0"
                        Maximum="500" />
</UserControl>
```

```csharp
public ObservableCollection<RealtimeStreamPoint> Readings { get; } = new();

// Marshal to the UI thread first: the chart reads the change as it happens.
Dispatcher.UIThread.Post(() => Readings.Add(new RealtimeStreamPoint(time, depth)));
```

### Data shape and performance

- Readings arrive in ascending time. A reading older than the newest one, and a value that is not finite, is ignored.
- Only readings in the window are drawn, found by binary search. Older ones are dropped after two windows, so an endless
  feed uses a steady amount of memory. While the view holds still the chart keeps up to 200,000 readings.
- A scrolling frame moves geometry built when the last reading arrived. It allocates nothing of its own and does not
  resolve colors again. The loop stops when the chart is off screen and when there is nothing left to move.
- `Append` with a time and a value does not allocate a point. Prefer it over a collection for fast feeds.

### Content guidelines

- Name what is measured and where in `Label`: "Checkout API, p95 latency", not "Latency".
- Keep marker text to two or three words, such as "Cache flush". The tooltip and screen readers repeat it.
- Write the unit the way it should read after a number. It follows the value directly, so use `" jobs"` with its space
  and `"%"` without one.

### Best practices

- Flag only what someone would act on. A line full of markers stops meaning anything.
- Fix the axis when height should carry meaning, and let it follow the data when the shape matters more.
- Load recent history through `Points` so the plot is not empty on first view.
- Keep `IsPauseOnHover` on for anything people read closely. A moving target is hard to point at.

### Common questions

**Why does the line stop before the newest reading?** That is `Interval`: the view trails the newest reading so it can keep
moving until the next one. Under reduced motion it shows the newest reading at once, because it is not scrolling.

**Does hovering set `IsPaused`?** No. `IsPaused` is your request and stays false. Read `IsHeld`, or `PausedChanged`, for the
state of the view.

**Can it draw several series?** One line per chart. Stack charts when you need to compare live values.

## API reference

<!-- api: RealtimeStream -->

<!-- api: RealtimeStreamPoint -->

## Accessibility

- The plot is a focusable group named after `Label`, with the keys in its name. The drawing itself is hidden from
  assistive technology, and its data is spoken through the help text.
- The header value is real text: a screen reader reads the label and the current value, whatever the digits are doing.
- The status button is named with the state and what pressing it does, such as "Live. Pause updates".
- A flagged reading is announced once, politely, when it arrives. Point readings are announced as the keyboard moves.
- State is never carried by color alone: dot or pause glyph with a word on the button, diamond with text on the markers.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus into or out of the plot. Entering holds the view and reads the newest visible point; leaving lets go. The plot is skipped while it has no readings |
| `Left` / `Right` | Read the previous or next point |
| `PageUp` / `PageDown` | Move ten points back or forward |
| `Home` / `End` | Read the oldest or newest visible point |
| `Space` / `Enter` | Pause or resume |
| `Escape` | Clear the reading and resume |

## Motion

- The line scrolls at display refresh, and its leading edge moves between readings instead of stepping.
- The value axis eases to its new range.
- A marker pops in with one overshoot when the line reaches it.
- Resuming glides back to live, and digits roll in the header.
- Under reduced motion scrolling stops and the plot redraws only when a reading arrives. Off behaves the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New reading | The line keeps scrolling toward it | Drawn at the right edge at once | Same as Reduced |
| Axis range changes | Eases to the new range | Jumps | Same as Reduced |
| Flagged reading | Marker pops with one overshoot | Marker appears | Same as Reduced |
| Resume | Glides back to live | Jumps to live | Same as Reduced |

## Responsive behavior

- `RealtimeStream` fills its width and redraws on resize. `PlotHeight` stays what you set.
- Time labels thin out to keep their distance. A 30-second window shows a label every 5 seconds on a wide plot and every
  10 or more on a phone.
- Below 420 px the value column narrows and shows fewer labels.
- Marker text that would overlap a newer marker is left out, and the tooltip still shows it.
- On touch, press and drag to read and hold; lifting the finger resumes. Hover only applies to a mouse or pen.

## Related

- `LineChart` for a finished series that people explore.
- `BrushChart` for a dense series with zooming and a selected range.
- `Sparkline` for a tiny trend inside a row or a card.
- `Gauge` for one current value against its limits.

| Component | Choose it when |
|---|---|
| `LineChart` | The data is complete and several series are compared |
| `BrushChart` | One long series needs zooming and a range selection |
| `Sparkline` | The trend is a small detail next to other content |
| `Gauge` | Only the current value against its limits matters |
| `AnimatedCounter` | Only the current number matters and it has no limits |
