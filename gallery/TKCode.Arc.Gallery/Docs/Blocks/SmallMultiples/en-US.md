---
title: Small multiples
summary: A grid of small line charts, one per group, read as a single picture: one crosshair moves through every panel, the y axes switch between each group's own range and a common one, panels re-sort by value, growth or name, and flat groups can be faded.
---

<Demo name="Preview" />

## When to use

`SmallMultiples` answers "how do these segments compare over the same stretch of time?" Every series gets its own small
panel with the same x axis and the same encoding, so a reader takes in the whole grid at once and nobody has to follow
eight crossing lines on one plot.

- **One measure, many groups.** Revenue per market, signups per plan, tickets per support channel: the same figure kept
  for several groups over the same months or weeks.
- **Finding the movers.** Every panel states how far it moved since an earlier period, the grid can be ordered by that,
  and groups that barely moved can be faded into the background.
- **Reading one period everywhere.** Pointing at any panel moves a crosshair through all of them, and the readout above the
  grid shows that period and the total.

## When not to use

- **A few series on one plot.** When two or three lines are best compared directly, `LineChart` draws them together with
  one crosshair and legend toggles.
- **One metric to explore in depth.** Use a single chart with zoom or a drill-down; small multiples trade detail for
  overview.
- **A trend inside a table.** Put a `Sparkline` in the row instead.
- **Precise figures.** If readers need the numbers more than the shapes, a table serves them better.

## Usage

The block derives from Avalonia's `TemplatedControl`. Give it periods (oldest first) and one `SmallMultiplesSeries` per
panel with one value per period. Four to twelve panels work best: the grid has four columns from a 640px container and two
below.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SmallMultiples Title="Revenue by market"
                        Description="Monthly net revenue"
                        SeriesLegendText="Market"
                        GhostLegendText="All markets, scaled to fit" />
</UserControl>
```

```csharp
chart.Periods.AddRange(months.Select(m => new SmallMultiplesPeriod(m.ToString("MMM yyyy"), m.ToString("MMMM yyyy"))));
chart.Series.Add(new SmallMultiplesSeries("us", "United States", usRevenue));
chart.Series.Add(new SmallMultiplesSeries("ca", "Canada", caRevenue));
```

`SeriesSource` and `PeriodsSource` take collections you own instead (an `ObservableCollection` is followed). A series' `Key`
keeps its panel, and its animation, when the list is rebuilt.

### Anatomy

- **Header**: `Title`, `Description` and three controls: the sort (Latest, Growth, Name), the scale (Independent, Shared)
  and the "Dim flat trends" switch. Below 640 they wrap under the heading.
- **Readout**: the period under the crosshair (or the latest), its position ("11 of 24") or "Latest", the total of every
  series, and the legend: the solid line is a series, the dashed one the total.
- **Panels**: the name, the value at the crosshair, the growth with an arrow and "vs" the comparison period, the y ticks in a
  right gutter, the dashed ghost of the total, the area, the line and its end dot.

### Growth and flat trends

Growth compares the latest value with the value `GrowthLag` periods earlier (12 by default, the same month a year before;
with fewer periods, the first one). A growth smaller than `FlatThreshold` either way (0.05 is 5%) counts as flat: the
panel says "Flat" with its percentage and, while `IsFlatDimmed` is on, its content fades to 38% while the grid lines stay.

### Scales

`Scale` chooses how panels compare. **Independent** fits each series to its own range, so the shapes are comparable;
**Shared** starts every panel at zero on the largest range, so the sizes are. Switching springs the axes and the lines
together. The ghost always uses its own range: it shows the overall shape, not a size to compare against.

<Demo name="SharedScale" />

### Your own units

Values are read as thousands of US dollars by default ($412k, $1.71M). Pass `ValueFormatter` for the headings, the total
and screen readers, and `TickFormatter` for the compact axis ticks. This demo shows weekly counts with growth against four
weeks earlier, at 560 wide.

<Demo name="Signups" />

### Following the crosshair

`CrosshairIndex` is the period under the crosshair, or null when it is hidden; `CrosshairIndexChanged` reports each move.
`SetCrosshairIndex` moves it from code. `SortChanged` and `ScaleChanged` report the controls; `Sort`, `Scale` and
`IsFlatDimmed` are two-way.

<Demo name="Crosshair" />

### Other words

Every text is a property with an English default: the options (`LatestSortText`, `GrowthSortText`, `NameSortText`,
`IndependentScaleText`, `SharedScaleText`, `DimFlatText`), the readout (`LatestText`, `PositionFormat`, `TotalText`), the
legend (`SeriesLegendText`, `GhostLegendText`), the trend (`FlatText`, `CompareFormat`, `UpFormat`, `DownFormat`), the
screen reader names (`SortLabel`, `ScaleLabel`, `PanelsLabel`, `KeyboardHint`) and the announcements.

## API reference

<!-- api: SmallMultiples -->

<!-- api: SmallMultiplesSeries -->

<!-- api: SmallMultiplesPeriod -->

<!-- api: SmallMultiplesSort -->

<!-- api: SmallMultiplesScale -->

## Accessibility

- **Tab** moves through the sort control, the scale control, the dim switch and then the panels, which are one tab stop.
- On the panels, **Left** and **Right** move the crosshair one period in every panel; **Page up** and **Page down** move it
  twelve; **Home** and **End** jump to the first or latest period; **Escape** clears it. Focusing the panels with the keyboard
  places it on the latest period.
- In the sort and scale controls the arrow keys choose the previous or next option.
- Keyboard moves are read out politely: the period, the total and the group in the lead. Changing the order, the scale or
  the fading is read out too.
- Each panel is named by its series, its value at the crosshair and its trend in words ("Up 15.3% vs Oct 2025", "Flat").
- Fading is never the only signal: every panel also shows an arrow and a signed percentage (with a real minus sign), or the
  word "Flat". Keyboard focus draws the shared focus ring around the panels.

## Motion

- **Switching the scale**: both ends of each panel's y range ride the smooth spring (about 400 ms, no overshoot). Everything
  in the plot is drawn from that range on every frame, so tick labels travel with their values while the line stretches or
  flattens; the new scale's ticks fade in over 240 ms as the old ones fade out.
- **Sorting**: each panel starts from where it was and springs into its new slot, carrying its own divider lines with it.
- **Crosshair**: its first position is shown at once; later moves glide on a short spring, and each panel's dot follows its
  line between points.
- **Fading**: a flat panel's content fades over 240 ms and the grid lines are left alone.
- With reduced motion, rescaling, reordering and crosshair moves happen at once. Off applies every change at once.

## Related

- `LineChart` for a few series on one plot with a gliding crosshair.
- `Sparkline` for a compact trend beside a value.
- `Line replay`, the other Line charts block, for playing one series back in time.
