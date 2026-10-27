---
title: Revenue explorer
summary: Monthly recurring revenue as stacked new, expansion and churn layers with three headline numbers and an overview strip: drag its window or handles and the paths, axes and numbers slide to the new range, with range presets, series chips, a previous-period comparison and a crosshair readout for the pointer and the keyboard.
---

<Demo name="Preview" />

## When to use

`RevenueExplorer` answers the monthly questions of a subscription business in one card: how much recurring revenue
(MRR) do we have now, how much did new customers and existing customers add each month, how much did we lose, and is
that better than the period before? The chart stacks new and expansion revenue above the zero line and draws churn below
it, so the net movement of a month is the distance between the layers. Under the chart an overview strip holds every month;
its window is the range the chart, the three headline numbers and the table show.

- **Revenue review pages.** The closing MRR, the average net new per month and the churn rate, each with its change, sit
  above the chart and follow the range.
- **Moving through time.** Drag the overview window to scrub through the history, or pull a handle to zoom in; the
  paths, the axis labels and the numbers slide there instead of redrawing.
- **Checking a trend against its past.** "Compare" draws the previous period of the same length as two dashed lines, and
  the readout adds the previous month's net new.

## When not to use

- **One metric over time.** A line chart (or the `Metric explorer` block) is lighter when there is no stack and no
  zoom.
- **A bridge from one total to another.** `MRR waterfall` shows how the opening MRR becomes the closing one.
- **Daily or hourly data.** The month is the unit here: the window has at least three months and labels are months.
- **More than a few years of months.** Thirty-six to sixty months work well; beyond that aggregate to quarters.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its data is a list of `RevenueMonth`s, oldest first, one per
calendar month without gaps: `New` and `Expansion` (zero or more), `Churn` (negative; a positive number is taken as its
negative) and optionally the month's closing `Mrr`. Leave `Mrr` out and the block adds each month's net new to the
previous month, starting from `OpeningMrr`, the revenue before the first month. `OpeningMrr` is also the base of the
first month's churn rate and of the first change.

```xml
<arc:RevenueExplorer Title="Subscription revenue" OpeningMrr="1050000">
    <arc:RevenueMonth Month="2024-03-01" New="41200" Expansion="14800" Churn="-17600" />
    <arc:RevenueMonth Month="2024-04-01" New="43900" Expansion="15300" Churn="-18200" />
</arc:RevenueExplorer>
```

```csharp
revenue.OpeningMrr = 1_050_000;
revenue.Months.AddRange(months);          // or: revenue.MonthsSource = observableMonths;
revenue.SetRange(12, 23);                 // months 12 to 23, sliding there
revenue.ApplyPreset(3);                   // the newest three months, as the "3M" button does
```

`MonthsSource` takes a collection you own (an `ObservableCollection` is followed); changing a month's numbers in place
rebuilds the block. Until you choose a range the newest `DefaultRangeMonths` (12) show.

### Anatomy

- **Header.** `Title`, the range as text under it (`Mar – Aug 2026`; the first year is left out inside one year), and
  the range buttons on the right. The buttons select the newest 3, 6, 12 months and everything; when the range is
  anything else none is selected. Replace them with your own `Presets`.
- **Headline numbers.** The closing MRR with its change since the month before the range; the average net new per month
  with its change against the previous range of the same length; the churn rate (churned revenue over the opening revenue
  of the months) with its change in points. Values roll digit by digit, the arrows turn between up and down, and the
  texts rise out and in. A change whose base is missing (the first months, `All`) is left out. Churn going down is good
  and drawn green.
- **Toolbar.** Three series chips (`New`, `Expansion`, `Churn`) and the compare switch.
- **Chart.** Y labels left, month labels under the plot (January shows its year), and the layers.
- **Overview.** Every month as small layers, year ticks, the window with a handle on each side.

### The range

`RangeStart` and `RangeEnd` (month indexes, both included, two-way) are the range; `RangeChanged` reports every change
with its source: `Preset`, `Drag`, `Keyboard`, `Click` or `Api`. Dragging the window moves it one to one with the
pointer while the range is the nearest whole months, so the numbers change as it crosses a month; the chart chases the window
on a spring and, on release, the window settles on the whole month. A handle moves one edge and never comes closer than
`MinRangeMonths` (3) to the other. A click on the strip outside the window moves the window's centre there.

<Demo name="LongHistory" />

### Series and comparison

`IsNewVisible`, `IsExpansionVisible` and `IsChurnVisible` (two-way) switch the layers: a hidden layer collapses to zero
and the layers above it fall, the y axis re-fits, and the chip's swatch empties. The domain follows the visible stack: its
top is 18% above the highest stack plus 8% of the deepest churn, and its bottom 20% below the deepest churn. With every series
off the axes stay and the chart says `EmptyText`.

`IsCompareEnabled` draws two dashed lines for the months of the previous period (the stacked new and expansion top, and
churn) and brings them into the y domain. In the readout a row adds that month's net new. Months with no previous period
(the first ones) have no dashes.

<Demo name="Compare" />

### Inspecting a month

Point at the chart: a crosshair and three dots mark the nearest month of the range and a panel shows the three series, the net
new and, when comparing, the previous period's net. The panel sits 14px right of the crosshair and flips to its left when it
would leave the chart; it glides between months and fades in and out. `ScrubIndex` (two-way) is the inspected month and
`ScrubChanged` reports it.

<Demo name="Events" />

### Narrow containers

The block reads its own width. Under 600 the plot is 216 high; under 560 the paddings tighten to 16 and the headline numbers
get less room; under 440 the range buttons take their own row and share it equally, the numbers stack with their value on the
right, and the chips shrink. When months are closer than 40px the axis shows every other (every third…) month.

<Demo name="Narrow" />

### Words, formats and currency

Every visible word is a property with an English default (`NewLabel`, `MrrLabel`, `CompareText`, `SinceFormat`,
`VersusFormat`, the screen reader names, the announcement formats). Amounts are compact currency (`$63.3k`, `$2.56M`):
change the symbol with `CurrencySymbol` and the language with `CultureName`, or replace the text with `ValueFormatter`
(readout, table, announcements), `AxisFormatter`, `MonthFormatter`, `MonthYearFormatter` and `RangeFormatter`. The
headline numbers always follow `CurrencySymbol`: their digits roll, which needs the symbol and the unit as separate pieces.

<Demo name="Euro" />

## API reference

<!-- api: RevenueExplorer -->

<!-- api: RevenueMonth -->

<!-- api: RevenueRangePreset -->

<!-- api: RevenueRangeChangedEventArgs -->

<!-- api: RevenueSeriesChangedEventArgs -->

<!-- api: RevenueCompareChangedEventArgs -->

<!-- api: RevenueScrubChangedEventArgs -->

<!-- api: RevenueSeries -->

<!-- api: RevenueRangeSource -->

## Accessibility

- The chart is a slider named "Inspect a month" over the months of the range; its value text carries every number of the
  month ("Mar 2026: new $72.5k, expansion $31.5k, churn −$34.1k, net new $69.9k"), and its help text summarises the range,
  the closing MRR and the average net new.
- The overview window and its two handles are sliders ("Selected range", "Range start", "Range end") whose value text is
  the range or the month.
- The range buttons and the series chips are toggle buttons (pressed state); the compare switch is a switch. Each headline
  number reads as one phrase with its change in words ("MRR, $2.56M, up +42.3% since Feb 2025").
- A hidden table lists every month of the range with its movements and closing MRR, and follows the range.
- Moving the range or the readout with the keyboard is announced politely; the readout and the crosshair are hidden from
  screen readers because the same text is the slider's value.
- The focus ring is drawn 2px outside the chip, switch, plot area (radius 12), window (radius 10) or handle (square).

### Keyboard

- **Tab** visits the range buttons (one stop; **← →** change the range), the three chips, the compare switch, the chart,
  the overview window, the start handle and the end handle.
- **Chart:** focusing it opens the readout on the last month; **← ↓** and **→ ↑** step one month, **Home / End** jump to the
  ends of the range, **Escape** closes the readout and keeps the focus (the next arrow starts from the last month again).
  **Shift** does not change the step here.
- **Window:** **← ↓** and **→ ↑** move one month (**Shift** three), **Home / End** to the ends of the history; the width is
  kept. **Start handle:** moves the first month, never closer than three months to the last; **End handle** likewise.
- **Enter / Space** press the chips, the switch and the range buttons.

## Motion

- **Range changes** (a preset, a click on the strip, a key): the chart's x domain, the window, the handles and the month
  labels follow one Smooth spring and arrive together; the paths are rebuilt every frame (no path morph). The y domain
  springs too; new gridlines fade in after 50ms while sliding in, and removed ones fade out in 160ms. The range text, the
  headline texts and the deltas rise 0.45em out and in; the numbers roll; the preset pill glides on a Gentle spring.
- **Dragging**: the window follows the pointer exactly and the chart keeps chasing it; when released the window settles on
  the whole month with a Gentle spring (about half a second). The grip scales to 1.15 while pressed; the window border goes
  from the strong border to the muted text colour on hover and the accent while held.
- **Series**: each layer's thickness springs between 0 and 1 (Smooth) and its line fades with it; the empty text fades in
  after 120ms. The chip's swatch fills in 240ms and scales 1 ↔ 0.8 on a 580ms curve.
- **Compare**: one spring blends each dashed line from the current line to the previous period while it fades in (it stays
  opaque for the first 40% of the way back).
- **Readout**: the crosshair and the dots jump to each month; the panel fades in with a 0.97 scale in 160ms, glides on a
  spring and fades out again (the reference left the panel behind; this one leaves cleanly).
- **Reduced motion** lands every spring at once and keeps the short fades (readout, tick, text swaps as 120ms
  cross-fades). **Off** applies everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown` and `Metric explorer`, the other Analytics blocks.
- `MRR waterfall`, the bridge from opening to closing MRR.
- `SegmentedControl`, the public range switch the presets are modelled on.
