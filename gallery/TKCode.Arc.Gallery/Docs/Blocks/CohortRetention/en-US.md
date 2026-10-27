---
title: Cohort retention
summary: How many of each starting group stayed, month by month: a heatmap triangle that morphs into retention curves, headline numbers that compare the newest cohorts with the oldest, segments that repaint every cell, and a cell cursor for the pointer and the keyboard.
---

<Demo name="Preview" />

## When to use

`CohortRetention` answers one question: of the people (accounts, workspaces, installs) who started in a given period, how
many were still active one, two, three months later, and is that getting better? Each row is a cohort, oldest first, and
each column a month since the cohort started. Newer cohorts have been measured for fewer months, so the heatmap is a
triangle; the size-weighted average row under it tells you what a typical cohort looks like.

- **Growth and customer-success dashboards.** Month 1 and month 6 retention at the top, with the change between the three
  newest and the three oldest cohorts, show at a glance whether onboarding work is paying off.
- **Comparing segments.** Plans, channels or regions as segments: switching repaints the triangle in a wave and rolls every
  number, so the difference between two groups is visible while it happens.
- **Shape and level together.** The heatmap shows the level of every cell; the curves show where cohorts flatten out and
  which ones fall faster than the average.

## When not to use

- **A single retention number.** Use a stat or a line chart when there are no cohorts to compare.
- **Funnels and conversion steps.** Steps that people pass in order belong in a funnel or the `Journey flow` block, not in
  months since start.
- **Hundreds of cohorts.** The block is designed for roughly 6 to 20 cohorts and as many periods; aggregate further (months
  instead of weeks) before showing more.
- **Daily figures that need exact lookups.** Retention tables with dozens of columns read better in a `DataGrid`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Its data is a list of `RetentionSegment`s, each with its
`RetentionCohort`s, oldest first. A cohort has a `Label` ("Jun 2026"), an optional `ShortLabel` for narrow layouts
("Jun ’26"), a `Size` (how many started) and `Retained`, how many were still active in month 1, 2, 3 and so on. The number
of values in `Retained` is the number of months measured so far. Every rate, the average row and the headline numbers are
computed by the block.

```xml
<arc:CohortRetention Title="Space retention"
                     Subtitle="Team spaces still editing each month after creation."
                     FirstMetricLabel="First-month retention"
                     SecondMetricLabel="Half-year retention"
                     TotalLabel="Spaces created">
    <arc:RetentionSegment Label="All" Description="all spaces">
        <arc:RetentionCohort Label="Jun 2026" ShortLabel="Jun ’26" Size="412" Retained="{Binding June}" />
        <arc:RetentionCohort Label="Jul 2026" ShortLabel="Jul ’26" Size="398" Retained="{Binding July}" />
    </arc:RetentionSegment>
</arc:CohortRetention>
```

```csharp
retention.Segments.Add(new RetentionSegment("Starter", starterCohorts, "Starter-plan spaces") { Key = "starter" });
retention.SelectSegment("starter");
```

`SegmentsSource` takes a collection you own instead (an `ObservableCollection` is followed). With one segment the segment
switch is hidden. Changing a cohort of the shown segment (its `Retained`, for example) updates the block in place.

### Anatomy

- **Header**: `Title`, `Subtitle` and the segment switch on the right.
- **Headline numbers**: the size-weighted average retention of `FirstMetricMonth` (1) and `SecondMetricMonth` (6), each
  with the change between the newest and the oldest `CompareCohortCount` (3) cohorts that reached that month, in
  percentage points; and the total that started, with the first and last cohort's names. Values roll digit by digit.
- **Toolbar**: the legend (the six heat steps with their thresholds, or the two line styles) and the view switch.
- **Chart**: cohort names and sizes on the left, the cells, the average row and the month axis.

### Heatmap and curves

`View` is `Heatmap` (default) or `Curves`, two-way. In the heatmap each cell shows its rounded percentage and a step of the
accent mixed into the surface: 8, 18, 32, 48, 66 and 86% from the lowest to the highest step, with steps starting at 20,
35, 50, 65 and 80% retained. The step uses the exact rate, so 79.8% reads "80" but keeps the fifth step. Change the
thresholds with `Thresholds` and the mix with `RampMix` (one more value than thresholds). Steps of 50% or more mix draw
their numbers in the accent's foreground color.

In the curves view, the y axis runs from 0 to 100%; each cohort is a thin straight-segment line and the average a thick
accent line on top, with a point per month.

<Demo name="Curves" />

### Inspecting a cell

Point at a cell (or a point in the curves view) to inspect it: a box marks the cell, the rest of the chart outside its row
and column fades, the row's name and the month number turn strong, and a card beside the cell gives the exact rate, how
many were still active out of how many started, and the difference from that month's average. On the average row the
card says how many cohorts the average covers. In the curves view the nearest month is found from x and the nearest
point of that month from y, a crosshair runs to the y axis with the exact value in a tag, and the cohort's line darkens
while the others fade.

`CursorCell` holds the inspected `CohortCell` (two-way, null for none), and `CursorChanged` reports every change with the
cohort, the rate and the counts. A click, or Enter or Space, raises `CellInvoked` and runs `CellCommand`: use it to
open the cohort's people or a detail page.

<Demo name="Events" />

### Weeks, other periods, other thresholds

Nothing in the block is tied to months: the number of columns follows the longest cohort (or `Months`), and every text that
names the period is a property (`MonthFormat`, `MetricLabelFormat`, `AxisTitle`, `AxisTitleShort`, the announcement
formats).

```csharp
retention.Thresholds = [0.10, 0.25, 0.40, 0.55, 0.70];
retention.MonthFormat = "Week {0}";
retention.MetricLabelFormat = "Week {0} retention";
```

<Demo name="Weekly" />

### Narrow containers

The block reads its own width, not the window's. Under 600 the chart switches to a compact geometry: short cohort names,
no size column, 24px rows, and the axis title "Month"; when a column is narrower than 32 the numbers leave the cells.
Under 560 the paddings tighten to 16 and the segment switch moves under the titles. Under 440 the headline numbers stack,
one per row with the value on the right, and the view switch moves above the legend. The headline values scale with the
block's width between 22 and 28.

<Demo name="Compact" />

### Formats and words

`PercentFormatter` (a rate from 0 to 1), `CountFormatter` and `DeltaFormatter` (percentage points) replace the default
"48.5%", "5,199" and "+5.2 pts". Every visible word is a property with an English default: the headline labels, the
legend texts, the card's row labels (`StillActiveText`, `VsAverageText`, `WeightedOverText`), the switch buttons and
their names, the empty text and the announcements. `ShowSizeColumn` and `ShowAverageRow` hide the size column and the
average row (and the average line).

## API reference

<!-- api: CohortRetention -->

<!-- api: RetentionSegment -->

<!-- api: RetentionCohort -->

<!-- api: CohortCell -->

<!-- api: CohortCellEventArgs -->

<!-- api: CohortRetentionView -->

## Accessibility

- The chart is a focusable group named by a summary (the view, the segment, the cohorts' range and the two headline
  values), with the key hint as its help text. The keyboard focus ring is drawn around it.
- Every cursor move is announced politely: "Jun 2026, month 3: 56.2% still active, 236 of 420." The average row says how
  many cohorts it covers. Escape empties the announcement.
- A view change announces what the view shows; a segment change announces the segment and the two headline values.
- A hidden table lists every cohort with its size and every month's rate (or "not measured yet"), with the average row
  last, so a screen reader can read the numbers without the chart.
- Each headline number reads as one phrase with its change in words ("up 5.2 pts newest 3 vs oldest 3 cohorts"), not
  only by color. The legend is named with its thresholds.
- The colors come from the accent and the surface in both themes; numbers switch to the accent's foreground on the dark
  steps.

### Keyboard

- **Tab** moves to the segment switch, the view switch and the chart, each one stop. Tabbing into the chart puts the cursor
  on the oldest cohort's first month.
- **← →** move the cursor along the cohort and stop at its first and last measured month.
- **↑ ↓** move between cohorts and keep the month, or take the cohort's last measured month when it has not reached it.
  **↓** from the newest cohort goes to the average row.
- **Home / End** jump to the cohort's first and last measured month.
- **Escape** clears the cursor; the focus stays on the chart.
- **Enter / Space** raise `CellInvoked` for the cursor's cell.
- In both switches **← →** change the selection at once and wrap around; **Home / End** jump to the ends.

## Motion

- **Heatmap to curves**: one master spring drives the morph; each part takes a slice of it, later along the diagonal
  from the top-left corner. The numbers fade, the cells shrink into points, the points travel from their rows to their
  rates pulling the lines open, the row labels fade out, then the lines and the y grid fade in, all within about a second.
  Going back plays the same slices in reverse: the last cells move first, and each cell moves before it grows.
- **Segment switch**: every value springs to its new rate, starting 9 ms later per diagonal step; the cells repaint in
  160 ms each time a value crosses a threshold, so a cell that climbs three steps repaints three times, and its number turns
  light or dark half way through. The headline numbers roll; the change texts rise out and in.
- **Cursor**: the box, ring and crosshair jump to the new cell; other cells fade in 160 ms; the card fades in where it
  appears (120 ms) and then glides to each new cell on a quick spring, and fades out in 160 ms.
- **Legend**: the old legend rises 6px and fades while the new one rises into place, 240 ms.
- Reduced motion lands the morph, the values and the colors at once and keeps only short fades (card, legend, change
  texts). Off applies everything at once.

## Related

- `Metrics dashboard`, `KPI drilldown` and `Journey flow`, the other Analytics blocks.
- `SegmentedControl`, the switches the block uses.
- `Activity terrain`, another heat map that morphs between two views.
