---
title: MRR waterfall
summary: A recurring-revenue bridge from the start of a period to its end: floating bars for new business, expansion, reactivation, contraction and churn, a side panel that lists the accounts behind any bar, and period and unit switches that move every bar, tick and number on one spring.
---

<Demo name="Preview" />

## When to use

`MrrWaterfall` answers "where did our recurring revenue go this period?". The first bar is the revenue at the start, the
last bar the revenue at the end, and the bars between them float: each one starts where the previous step stopped, rising
for revenue won (new business, expansion, reactivation) and falling for revenue lost (contraction, churn). Above the chart,
the ending revenue and its change sit beside three ratios that finance teams ask for first: net revenue retention, the
quick ratio and logo churn.

- **Board and finance reports.** A month or a quarter in one picture, with the accounts behind each step one click away.
- **Revenue operations reviews.** Switch between months and the quarter to see which kind of movement changed, and use the
  percent-of-start unit to compare periods of different size.
- **Customer-success follow-up.** Select the churn bar to see who left and why, then select expansion to see who grew.

## When not to use

- **A trend over many periods.** The bridge shows one period at a time; use a line or area chart for twelve months.
- **Revenue that does not start from a known level.** Every bar depends on the starting revenue; without it use a bar chart.
- **Hundreds of accounts per step.** The panel lists the largest accounts and groups the rest in one row; it is not a table.
  Link from the row to an account list instead.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its data is a list of `MrrPeriod`s. A period has the starting revenue
(`StartingMrr`), the number of paying accounts at the start and the end, its names and dates (`Label`, `LongLabel`,
`Caption`, `StartDateText`, `EndDateText`) and the `Movements` in bar order. Each `MrrMovement` has a `Kind`, a positive
`Amount` (the kind decides the sign), an `AccountCount` and the named `Accounts` behind it. The ending revenue, the net
change and every ratio are computed by the block.

```xml
<arc:MrrWaterfall Title="Recurring revenue bridge" Scope="All tiers">
    <arc:MrrPeriod Label="Q2" LongLabel="Q2 2026" Caption="Apr 1 – Jun 30, 2026"
                   StartDateText="Apr 1" EndDateText="Jun 30"
                   StartingMrr="286400" StartingAccounts="892" EndingAccounts="948">
        <arc:MrrMovement Kind="New" Label="New business" ShortLabel="New" CompactLabel="New"
                         Amount="39800" AccountCount="116" CountNoun="new customers" />
    </arc:MrrPeriod>
</arc:MrrWaterfall>
```

```csharp
bridge.Periods.AddRange(periods);
bridge.SelectedPeriod = periods[^1];
bridge.SelectedIndex = 5; // the churn bar
```

`PeriodsSource` takes a collection you own instead (an `ObservableCollection` is followed, and editing a period of the
shown list updates the block in place). With one period the period switch is hidden.

### Anatomy

- **Header**: `Title`, the subtitle (`Scope` followed by the period's `Caption`), and the period and unit switches.
- **Summary**: the ending revenue rolling digit by digit, its change since the start (arrow, amount and percentage, tinted by
  direction), and the three ratios.
- **Chart**: a value axis, seven bars with their labels, connectors that carry each step's end to the next bar, and a marker
  under the selected bar.
- **Panel**: the accounts behind the selected bar, or the previewed one.

### The axis and the bars

The value axis does not start at zero, because the steps would be invisible next to the totals. Its domain reaches 0.6 of
the bridge's span below the lowest level and 0.05 above the highest, and its step is the value from 1, 2, 2.5 and 5 times a
power of ten nearest to a sixth of that range. The first and last bars are therefore cut: a slanted break mark across each
shows it. Bars are 56 wide at most, or 56% of their band, and every edge sits on a whole pixel.

### Selecting a bar and previewing one

Click a bar, or press an arrow key, and the panel lists the accounts behind it: the largest `MaxNamedAccounts` (5) by
amount, then one row for the remainder. Each row has a bar for its share of the step. The start and end bars list the
`StartingBreakdown` and `EndingBreakdown` instead (plan tiers) without a remainder row. `SelectedIndex` is two-way, survives a
change of period and unit, and raises `SelectionChanged` and `SelectionCommand`.

With a mouse, pointing at a bar previews its list without selecting it: the bar gets a plate, the other bars and labels fade,
and the hint changes to say that a click keeps the list. `HoveredIndex` reports it. Touch only selects.

### Period and unit

`SelectedPeriod` and `ValueMode` are two-way. Changing either moves the bars, the connectors, the axis, the labels and the
panel rows together: see Motion.

<Demo name="Percent" />

In `PercentOfStart` every value is a share of the period's starting revenue, so the start bar is 100%. The headline stays in
currency. The ticks, labels and panel amounts follow the unit.

### Words, symbols and formats

All text comes from `Labels` (`MrrWaterfallLabels`): switch names, headline labels, panel hints, the sentences read to
screen readers. `CurrencySymbol` changes the symbol everywhere; `CurrencyFormatter`, `CompactFormatter` and
`PercentFormatter` replace the number formats of the chart and the spoken texts. Numbers that roll use the symbol and
the built-in formats.

<Demo name="SinglePeriod" />

### Narrow containers

The block reads its own width. Under 800 the panel moves under the chart; under 560 the headline and the statistics shrink and
the statistics share the row; when a band is narrower than 72 the bar names shorten to `CompactLabel` and the bar labels lose
the currency symbol; under 48 the names use 10px type.

<Demo name="Narrow" />

### Events

<Demo name="Events" />

## API reference

<!-- api: MrrWaterfall -->

<!-- api: MrrPeriod -->

<!-- api: MrrMovement -->

<!-- api: MrrAccount -->

<!-- api: MrrWaterfallLabels -->

<!-- api: MrrSelectionChangedEventArgs -->

<!-- api: MrrPeriodChangedEventArgs -->

<!-- api: MrrValueModeChangedEventArgs -->

<!-- api: MrrMovementKind -->

<!-- api: MrrValueMode -->

## Accessibility

- The chart is a focusable group named by a sentence that gives the period, the starting revenue, every step and the ending
  revenue, with the key hint as its help text. The focus ring surrounds the selected bar's column.
- Every selection is announced politely with the bar's name, its signed amount and its account count: "Churn, −$18,460, 79
  customers left". A change of period announces what moved.
- A hidden table lists the movement, the amount, its share of the starting revenue and the account count for each bar.
- The panel is named for the bar it shows ("Accounts behind churn"). The switches are named, and the headline reads as one
  phrase with its change in words, not only by color.

### Keyboard

- **Tab** moves through the period switch, the unit switch and the chart, one stop each.
- **← ↑** and **→ ↓** move the selection to the previous or next bar and wrap around from the last bar to the first. Moving
  selects, so the panel follows.
- **Home / End** select the first and last bar.
- In both switches the arrow keys change the selection and wrap.

## Motion

- **Period or unit**: the bar ends, connectors, labels and the axis domain share one critically damped spring and settle in
  about 600 ms. Labels count along ("+$21.9k" to "+$39.8k"). Ticks that stay slide with the domain; new ticks fade in
  (240 ms after 60 ms), dropped ones fade out in 160 ms while they slide. A change of unit replaces every tick.
- **Panel rows**: rows are matched by account. A kept row slides to its new place on the same spring, a new row fades and
  rises in (240 ms, 35 ms apart), a removed row fades out in 120 ms where it stood, and the share bars grow on the spring.
  Numbers in the rows roll digit by digit.
- **Selection marker**: a fast, non-bouncing spring (about 400 ms for the width of the chart); the bar name turns strong in
  160 ms.
- **Hover and focus**: other bars fade to 28% and labels to 32% in 160 ms; the plate appears at once.
- **Texts**: the subtitle, headline label, change text, panel heading and row details rise out and in (160 ms out, 240 ms in).
- Reduced motion lands everything at once except that dropped ticks still fade for 160 ms. Off applies every change at once.

## Related

- `Metrics dashboard`, `Cohort retention` and the other Analytics blocks.
- `SegmentedControl`, the switches the block uses.
