---
title: Budget variance
summary: Budget against actual spend by department as bullet charts on one shared scale, with signed variances, a period switch that springs every bar, budget lines that open in place and a total row.
---

<Demo name="Preview" />

## When to use

`BudgetVariance` answers one question at a glance: which teams spent more than planned, which spent less, and by how much.
Every row is a bullet chart on the same scale, so the budget ticks line up down the table and the longest overrun stands
out without reading a number.

- **Month end and budget reviews.** Compare planned and actual operating spend by department or cost center, then open a
  department to see which lines moved it.
- **A controller's dashboard.** Switch between the month, the quarter and the year so far; every bar and figure moves to
  the new window together.
- **Any plan against actual with a tolerance.** Headcount cost, cloud spend by team or a project's phases, where a few
  percent either way is fine and more is worth a look.

## When not to use

- **Explaining how one total changed.** A waterfall that walks from the start value to the end value reads better.
- **Trends over many periods.** The block compares one window at a time; use `LineChart` or `BarChart` for a series.
- **Income.** Every figure is read as an expense, so spending under budget is favorable. Revenue against a target reads the
  other way round.
- **Dozens of cost centers.** When sorting and filtering matter more than the picture, use `Table`.

## Usage

The block derives from Avalonia's `TemplatedControl`. `Periods` are the windows of the switch; each `BudgetPeriod` has an
id, a short label, the line shown under the title and a phrase for screen reader sentences. `Departments` hold
`BudgetLine`s, and each line has one `BudgetAmount` (budget and actual) per period id. Department figures and the total are
summed from the lines, so they always reconcile.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BudgetVariance SelectedPeriod="month" ExpandedDepartment="eng">
        <arc:BudgetVariance.Periods>
            <arc:BudgetPeriod Id="month" Label="Month" Description="March 2026" Phrase="in March" />
            <arc:BudgetPeriod Id="quarter" Label="Quarter" Description="January to March 2026" Phrase="in the first quarter" />
        </arc:BudgetVariance.Periods>
        <arc:BudgetDepartment Id="eng" Name="Engineering" Owner="Ravi Menon">
            <arc:BudgetLine Id="cloud" Name="Cloud hosting">
                <arc:BudgetAmount Period="month" Budget="176000" Actual="213700" />
                <arc:BudgetAmount Period="quarter" Budget="528000" Actual="613400" />
            </arc:BudgetLine>
        </arc:BudgetDepartment>
    </arc:BudgetVariance>
</UserControl>
```

In code, `new BudgetLine("cloud", "Cloud hosting").With("month", 176_000, 213_700)` adds the amounts of a window and
returns the line.

### Anatomy

- **Header**: `Title`, the period's description (or `Description`) and the period switch.
- **Column header**: Department, the scale over the charts (0%, 50% and 100% of budget), Actual, Budget and Variance.
- **Total row**: every department summed, with the department count under its name.
- **Department rows**: the owner's avatar (initials unless `OwnerAvatar` is set), the name and owner, the bullet chart, the
  figures and a chevron. The open department has a muted fill and its lines under it, indented past the avatar.
- **Legend**: what the bar, the tick, the red part and the band mean.

### Reading a bullet chart

- The **bands** behind each bar mark the tolerance: up to `RangeLow` of budget, from there to `RangeHigh`, and beyond.
  They use the text color at low opacity, so the block stays neutral in any accent.
- The **bar** is the actual spend. The part past the budget **tick** is drawn in the danger color.
- The **scale** is shared by every row and every window: at least 120% of budget, rounded up to a tenth so the largest
  figure of any period fits. The tick never moves when the window changes.
- The **variance** is a signed amount and a share ending in "over", "under" or "on plan". A variance smaller than
  `OnPlanThreshold` of budget (half a percent by default) is on plan and stays neutral; over is red, under is green.

<Demo name="Tolerance" />

### Periods and departments

`SelectedPeriod` (two-way) names the window shown; the switch changes it. `ExpandedDepartment` (two-way) names the open
department, or null; `ToggleDepartment(id)` opens one or closes it. Opening a department closes the one that was open.
`PeriodChanged` reports a window change and `DepartmentToggled` every open and close, the closing one first.

<Demo name="Events" />

### Narrow containers

The block reads its own width. From 760 it is a table with aligned columns. Below that each row puts the name, the actual
of budget and the variance on top, and the bullet chart across the full width under them, so the chart is never squeezed.
Long names wrap instead of being cut.

<Demo name="Narrow" />

### Text and currency

`Currency` is put before every amount ("$" by default); amounts shorten to k and M. Every other word comes from `Labels`,
a `BudgetVarianceLabels`: replace it to localize the block, including the sentences screen readers hear.

## API reference

<!-- api: BudgetVariance -->

<!-- api: BudgetPeriod -->

<!-- api: BudgetDepartment -->

<!-- api: BudgetLine -->

<!-- api: BudgetAmount -->

<!-- api: BudgetVarianceKind -->

<!-- api: BudgetVarianceLabels -->

## Accessibility

- Each department row is a button that reports expanded or collapsed, and its name is a full sentence: what was spent of
  which budget, in which window, the variance and whether it is favorable. The total and each budget line carry the same
  sentence; the charts and the counting numbers themselves are not read, so nothing is heard twice or mid-count.
- Changing the window announces the window and the total through a polite live region.
- Variance never relies on color: amounts carry a sign and every share ends in over, under or on plan.
- Pointer focus draws no ring; keyboard focus fills the row and shows Arc's focus ring.

### Keyboard

- **Tab** moves to the period switch, then into the departments, then out of the block.
- On the period switch, **Left** and **Right** change the window.
- In the departments, **Up** and **Down** move to the previous or next one, and **Home** and **End** to the first or last.
- **Enter** or **Space** opens or closes the department in focus.

## Motion

- **Changing the window**: every bar springs to its new length on one critically damped spring (smooth, no overshoot), and
  every amount and share counts to its new value on the same spring. The red overspend is the same bar clipped to the
  space past the tick, so the color appears exactly as the bar crosses it.
- **Opening a department**: its lines grow in height and fade in together without overshoot; closing is faster. The
  chevron turns on Arc's spring curve and takes the accent while open.
- Every animation can be interrupted: a new window or toggle starts from where the bars and numbers are.
- Reduced motion jumps the bars and numbers to their values and opens the lines at full height. Off applies every change
  at once.

## Related

- `Market terminal`, the other Finance block.
- `SegmentedControl` and `Avatar`, the controls the block is made of.
- `Progress` for a single bar against a goal.
