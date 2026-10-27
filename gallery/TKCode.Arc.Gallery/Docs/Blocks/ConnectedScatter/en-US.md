---
title: Connected scatter
summary: Two measures that move together, drawn as one path: a head point you scrub or play along the months, readouts that count with it, a target line that tints the healthy side, and a switch that morphs the whole path from one series to another.
---

<Demo name="Preview" />

## When to use

`ConnectedScatter` shows how two measures change **together** over time. Each month is a point at (x, y), the points are
joined in time order by a smooth curve with a small arrow on every segment long enough to hold one, and a head point
marks the month you are looking at. Where a line chart would show two separate lines, the path shows the relationship:
which way the pair is travelling, where it turned and where it crossed a threshold.

- **Unit economics.** Acquisition cost against the value of a customer, with a target ratio: the path crosses the
  target line when a channel goes from paying back to losing money.
- **Trade-offs over time.** Price and conversion, latency and throughput, headcount and output: any pair where the
  direction of travel matters more than either value alone.
- **Comparing channels.** The segment switch morphs the same months from one series to the next, so the eye follows
  what changed.
- **Presenting a story.** Play walks the head through the months at a steady pace, and the first appearance draws the
  whole path once.

## When not to use

- **One measure over time.** Use a line or area chart (`Metrics dashboard`, `Revenue explorer`); a path adds nothing
  when the other axis is time.
- **Many series at once.** One path is drawn at a time. Overlapping several loops is unreadable; use the switch, or a
  small multiples layout.
- **Unordered points.** The curve implies an order. For categories without a sequence use a plain scatter plot.
- **Hundreds of points.** The block is designed for roughly 8 to 36 months per series; every point has a dot and an
  arrow, and the scrubber has one stop per month.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its data is a list of `ScatterSeries`; each series has a `Name`, an
optional `Key` and its `ScatterPoint`s in time order (`Label`, `X`, `Y`). Every series should have the same number of
months, so the switch can move each point to its counterpart. The two axes are `ScatterAxis` objects.

```xml
<arc:ConnectedScatter Title="Unit economics by channel" TargetRatio="3">
    <arc:ConnectedScatter.XAxis>
        <arc:ScatterAxis Name="CAC" Title="Customer acquisition cost" Minimum="300" Maximum="1900" Step="200" />
    </arc:ConnectedScatter.XAxis>
    <arc:ConnectedScatter.YAxis>
        <arc:ScatterAxis Name="LTV" Title="12 month customer value" Minimum="1000" Maximum="6000" Step="1000" />
    </arc:ConnectedScatter.YAxis>
    <arc:ScatterSeries Name="Field sales" Key="field-sales">
        <arc:ScatterPoint Label="Jul 2024" X="1150" Y="3600" />
        <arc:ScatterPoint Label="Aug 2024" X="1175" Y="3630" />
    </arc:ScatterSeries>
</arc:ConnectedScatter>
```

```csharp
scatter.Series.Add(new ScatterSeries("Community", points, "community"));
scatter.SelectedSeriesKey = "community";   // the points morph to the new series
```

`SeriesSource` takes a collection you own instead (an `ObservableCollection` is followed). With one series the switch is
hidden.

### Anatomy

- **Header**: `Title`, the description (`Description`, or a sentence written from the axes and the first and last month)
  and the series switch.
- **Readouts**: four cells that count with the head: the month and its place ("18 of 18"), the two values with their
  change since the first month ("+$200 since Jan 2025", or "Same as Jan 2025"), and the ratio with a status dot and
  "Above 3x target" or "Below 3x target". The status is always written out, never only a color.
- **Chart**: the vertical axis title and the legend (the path, the target line, the healthy zone), the plot, and the
  horizontal axis title under it at the right.
- **Transport**: a Play, Pause or Replay button and the scrubber with the quarter labels under it.

### Axes and the target line

`ScatterAxis` ranges and steps are used exactly as written: no rounding to nice numbers and no forced zero, so a zoomed
axis starts where you say. Leave `Minimum`, `Maximum` and `Step` unset (NaN) to derive them from the data of every series, so
the axes stay still while the series changes. Ticks of a step that is a multiple of 1,000 read "$2k"; others use the block's
`ValueFormatter`; `TickFormatter` replaces both.

`TargetRatio` draws the dashed line y = k · x through the origin, labels it ("3x CAC"), tints the side above it and writes
"Healthy, above 3x" in its corner. The ratio readout and the card color follow the same test on the unrounded values. NaN
removes the line, the zone, the status and their legend items.

<Demo name="Target" />

### The head, the scrubber and playback

The head point sits on the real curve, not on a straight line between months. **Pressing** the scrubber slides the head to
the pointer on the Smooth spring; once it has caught up it follows 1:1 while you drag, and **releasing** snaps it to the
nearest month. The keys, a tap on a point and the end of playback also settle the head on a month. `MonthChanged` is raised
at that moment, never frame by frame, with the index, the series and the point; `MonthChangedCommand` runs with the same
arguments. `SelectedIndex` is two-way (−1 means the last month) and `Position` is the continuous position, including the
fractions while the head moves.

**Play** moves the head one month per `PlaybackMonthDuration` (240 ms) at a constant pace from where it is, or from the
first month when it is at the last one. **Pause** stops it between two months; the readouts name the nearest month and the
values are interpolated. At the last month the button becomes **Replay**.

<Demo name="Events" />

### First appearance

The first time a quarter of the block is on screen, the head runs from the first month to the last in
`DrawInDuration` (1.35 s, in-out), drawing the trail behind it; when it lands, the dashed future path, the hollow dots, the
future arrows and the labels fade in together over 240 ms. Set `PlayOnFirstAppear="False"` to start at rest on the last
month, or call `ReplayDrawIn()` to show it again. Reduced and Off motion skip it.

<Demo name="Minimal" />

### Hovering, tapping and the card

Point within 30 px of a month to see a halo and a card with the month and the two values and their ratio. The card does not
move the head; between points it glides. Tapping or clicking a point moves the head there on the spring; on a touch
screen the card shows while the finger is down.

### Labels, notes and narrow containers

The first and the last month are labeled on the outer ends of the path, the last one always in Medium. A `ScatterNote`
adds an event label ("Pricing change") to a month, on the side you choose. The block reads its own width: under 720 the
readouts form a 2 × 2 grid and the plot is 330 high; under 560 the quarter labels shrink to the first, the Januaries and
the last month; under 480 the plot is 300 high, notes, the target label and the zone label go, every second tick of the
horizontal axis is skipped, and the scrubber moves under the button; under 380 the values are 18px.

<Demo name="Narrow" />

### Formats and words

`ValueFormatter` and `RatioFormatter` replace "$1,234" and "4.8x" in the readouts, the card, the table and the
announcements. Every visible word is a property with an English default: the readout names, the "since" and "of" notes,
the status words, the target and zone texts, the legend, the button's texts and names, the screen reader table and the
announcement formats. Month names that read as "Jan 2025" get quarter labels under the scrubber; others get about six
evenly spaced ones.

<Demo name="Weekly" />

## API reference

<!-- api: ConnectedScatter -->

<!-- api: ScatterSeries -->

<!-- api: ScatterPoint -->

<!-- api: ScatterNote -->

<!-- api: ScatterAxis -->

<!-- api: ScatterLabelSide -->

<!-- api: ScatterMonthChangedEventArgs -->

<!-- api: ScatterSeriesChangedEventArgs -->

## Accessibility

- The scrubber is a slider named "Month" from 1 to the number of months, with a value text for the settled month: "Dec 2025:
  CAC $960, LTV $4,640, 4.8x, above the 3x target." It is updated when the head settles, not while it moves or plays.
- A polite live region says the same text for every settled month, and "Series name. …" when the series changes; playback
  is announced where it stops.
- The chart is hidden detail for screen readers, as is the card; a table lists every month with the two values and the
  ratio, with a caption naming the series and the axes.
- Every control has a name; the play button's name follows its state ("Play month by month", "Pause", "Replay from the
  first month"). The target and the status are written as words.
- Colors come from the theme in both light and dark; the dots, arrows and labels keep their contrast on the tinted zone.

### Keyboard

- **Tab** moves to the series switch (one stop, the selected series), the play button and the scrubber. The chart takes no
  focus.
- On the scrubber **← ↓** go one month back and **→ ↑** one month on; **PageUp** goes three months on and **PageDown** three
  back; **Home** and **End** go to the first and the last month. The keys stop at the ends. From a position between two
  months they go to the next whole month in the direction pressed. Each key moves the head on the Smooth spring and
  settles on a month.
- In the series switch **← →** change the selection at once and wrap around; **↑ ↓** go to the previous and the next
  series; **Home** and **End** go to the ends.
- **Enter** or **Space** on the button plays, pauses or replays.
- The focus ring is drawn only for keyboard focus; a pointer press on the scrubber does not show it.

## Motion

- **First run**: the head runs 1.35 s on the in-out curve, the readouts and the scrubber follow it; on landing the future
  layers fade in together over 240 ms.
- **Head**: a key, a tap, a release and a series switch all use the Smooth spring (no overshoot, about 0.65 s); pressing the
  scrubber chases the pointer and then follows it 1:1. While playing or dragging the head's halo grows to 1.25× on a 580 ms
  spring curve.
- **Playback**: constant speed, 240 ms a month; pausing keeps the fractional position.
- **Series switch**: every point, arrow and the curve itself spring to the new series at the same time on the Smooth
  spring; the readouts count; the old labels fade out in 180 ms and the new ones in over 240 ms after that.
- **Hover**: the halo fades in 160 ms; the card fades in place and glides on a 580 ms spring curve between points.
- **Scrubber thumb**: 1.08× on hover, 1.12× pressed; its center dot grows 1.5× on keyboard focus.
- Reduced motion skips the first run and the glides: playback jumps one month every 960 ms, keys and taps jump, a series
  switch replaces the points at once, and only short fades stay. Off applies everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown` and `Revenue explorer`, the other Analytics blocks.
- `SegmentedControl`, the switch the block uses.
- `Line replay`, another block that plays a series through time.
