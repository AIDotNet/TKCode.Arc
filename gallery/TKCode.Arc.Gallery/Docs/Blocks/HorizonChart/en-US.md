---
title: Horizon chart
summary: Many daily series compared by how far each strays from its own baseline, folded into one thin band row each: the band count folds and unfolds every row on one spring, rows re-sort by name, peak or latest value, and one crosshair reads the same day across every row for the pointer, touch and the keyboard.
---

<Demo name="Preview" />

## When to use

`HorizonChart` answers "which of these many series is off, when, and by how much" on one screen. Every row is a daily
series measured against its own baseline (a forecast, a target, an average); the distance above the baseline fills in one
colour and the distance below in another, and the distance is folded into bands that are stacked on one thin strip, so a
row costs 36px instead of a full chart. Fourteen rows of 119 days fit in about 550px.

- **Fleets of similar things.** Stores, hubs, regions, services or campaigns against their own plan: the eye finds the rows
  with a dark band and the day it happened.
- **Spotting shared events.** The crosshair crosses every row, so an outage, a promotion or a holiday that touches several
  rows shows as one vertical line with the same day's value written at the end of each row.
- **Dense dashboards.** Where a line chart per row would be too tall, and a table of numbers too flat.

## When not to use

- **A few series.** With three or four rows, a line chart shows the same thing with a scale you can read.
- **Absolute levels.** The rows are deviations from a baseline; the baseline's own level is not drawn. Show levels in a line
  or area chart.
- **Reading exact values off the shape.** The strips are for finding; the numbers come from the crosshair and the value
  column.
- **Many hundreds of days or more than about 40 rows.** Aggregate (weeks instead of days) before showing more: the block
  does not.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its data is a list of `HorizonSeries` (a name and one signed value
per day: positive above the baseline, negative below it) and the `Days` they line up with, oldest first. A `HorizonDay`
carries a `Label` ("Sep 27"), a `LongLabel` ("Sunday, September 27") and an optional `Date`; when only the date is set the
labels are formatted from it.

```xml
<arc:HorizonChart Title="Orders against forecast"
                  Description="Daily orders at each hub compared with its winter forecast"
                  BaselineLabel="winter forecast"
                  Days="{Binding Days}">
    <arc:HorizonSeries Name="Northgate" Values="{Binding NorthgateValues}" />
    <arc:HorizonSeries Name="Harbor Point" Values="{Binding HarborPointValues}" />
</arc:HorizonChart>
```

```csharp
chart.Days = Enumerable.Range(0, 119).Select(i => new HorizonDay(start.AddDays(i))).ToArray();
chart.Series.Add(new HorizonSeries("Northgate", values, key: "northgate"));
chart.Bands = 2;
chart.Sort = HorizonSort.Latest;
```

`SeriesSource` takes a collection you own instead (an `ObservableCollection` is followed). The axis shows a tick on the
first day and on every first of a month for days that carry a date; set `Ticks` (index and label) to place your own. A
value of NaN draws as zero and reads as a dash.

### How the bands work

Each row is one polygon, drawn as many times as there are bands. `Extent` is the distance that fills every band: by default
the largest absolute value rounded up to a multiple of 12 (36 when the largest swing is 35). With three bands each band
spans a third of the extent, so a value of 12 fills the first band, 24 the second and 36 the third; each layer is drawn
with alpha `1 − 0.08^(1/bands)`, which stacks to 0.92 where all bands are full. Darker colour therefore means further from
the baseline, and one band is an ordinary area chart.

The band count is a continuous value while it folds: every row's four layers and the alpha follow one spring (a
critically damped spring that settles in about 600ms), so a change in the middle of a fold carries on from where it is.

<Demo name="Palette" />

### Sorting

`Sort` is `Name` (A to Z), `Peak` (the row's largest absolute value first, the default) or `Latest` (the last day's value,
highest first). Ties keep the data order. On a change every row moves to its new place on one spring at the same time and
the numbers move with it; the crosshair and the band count are not affected.

### The crosshair

Move the pointer over the band column (the name and value columns do not react) to put the crosshair on a day: the date
above the chart and the value at the end of every row change to that day at once, and the status line says "Picked day".
The line appears where the pointer is, then follows it on a quick spring; when the pointer leaves it fades and the
readout returns to the most recent day. On touch, a tap or a horizontal drag sets the crosshair and it stays after you
lift; a tap beside the chart clears it. `SelectedDayIndex` is two-way, `SelectDay` and `ClearSelection` drive it from
code, and `DayChanged` and `DayCommand` report every change.

<Demo name="Events" />

### Formatting values and other units

`ValueFormatter` formats the row values, the scale's end labels and the spoken texts; the default writes a signed whole
percent with a real minus sign. Set `Extent` when you want a round band edge that does not depend on the data.

```csharp
chart.ValueFormatter = v => v switch { > 0 => $"+{v:0} ms", < 0 => $"−{-v:0} ms", _ => "0 ms" };
chart.Extent = 60;
```

<Demo name="Latency" />

### Narrow containers

The block reads its own width. Up to 900 wide the name and value columns narrow (116 and 52); up to 560 the paddings drop
to 16, rows are 34px with 12px text and 84 / 44px columns, the switches share one line (or one each when they do not fit)
and the colour scale takes the whole line.

<Demo name="Compact" />

### Words

`Labels` holds every word the block writes or speaks (the switch labels, the status lines, the caption and its formats,
the announcements and the table's column names) with English defaults; assign a new `HorizonChartLabels` to translate or
reword it. `AboveColor` and `BelowColor` replace the Series1 and Series3 colours, and `IsSortSwitchVisible`,
`IsBandSwitchVisible` and `IsLegendVisible` hide parts when the page provides its own controls.

## API reference

<!-- api: HorizonChart -->

<!-- api: HorizonSeries -->

<!-- api: HorizonDay -->

<!-- api: HorizonTick -->

<!-- api: HorizonChartLabels -->

<!-- api: HorizonDayChangedEventArgs -->

<!-- api: HorizonSortChangedEventArgs -->

<!-- api: HorizonBandsChangedEventArgs -->

<!-- api: HorizonSort -->

## Accessibility

- The chart is a focusable group named "{title}. {n} rows" with the key hint as its help text. The focus ring is drawn
  inside the chart, where the block's rounded clip cannot cut it.
- Keyboard moves of the crosshair are announced politely with the day and the highest and lowest rows: "Tuesday, March 16.
  Top Foxhollow +8%, bottom Northgate −31%." Sorting and a changed band count are announced too; moving the pointer is not.
- A hidden table lists every row in the current order with its latest, highest and lowest value and the day of each, so a
  screen reader can read the numbers without the shapes.
- The colour scale is an image with a spoken description ("Color scale from −36% under the forecast to +36% over it, 3
  bands per side"), and its caption says what one band is. Direction is also carried by the signed numbers, not only by
  colour.
- Both switches are named groups; their buttons are toggle buttons.

### Keyboard

- **Tab** goes to the sort switch, the band switch and the chart, one stop each. Tabbing into the chart puts the crosshair
  on the latest day.
- **← →** move the crosshair one day; **Shift + ← →** one week. It stops at the first and last day.
- **Home / End** jump to the first and last day.
- **Escape** hides the crosshair; the focus stays on the chart and the next arrow starts from the latest day.
- In both switches **← ↑** and **→ ↓** change the selection at once and wrap around; **Home / End** jump to the ends.

## Motion

- **Folding**: one spring (critically damped, 600ms to rest) carries every row's band count to its new value; the layers and
  their alpha follow it on every frame, nothing is rebuilt. A new target in mid-flight keeps the velocity. The colour
  scale's cells grow or shrink and change opacity over 480ms with an in-out curve, starting with the fold; its caption
  changes at once.
- **Sorting**: each row springs from where it was to where it belongs (about 700ms, no overshoot), all rows starting
  together; the rows paint an opaque ground, so a row sliding past another covers it.
- **Crosshair**: it appears in place and fades in over 160ms, follows the pointer or key on a quick critically damped
  spring (about 250ms to rest), and fades out in 160ms where it is when you leave. The text always changes at once.
- **Switches**: the selected pill glides on a non-overshooting spring.
- Nothing plays when the block first appears. Reduced motion and Off land every fold, reorder, crosshair move and scale
  change at once.

## Related

- `Metric explorer` and `Revenue explorer`, the other Analytics blocks with a crosshair across a time axis.
- `Cohort retention`, which also compares many rows over time with a colour scale.
- `SegmentedControl`, the switches the block uses.
