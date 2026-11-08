---
title: Funnel chart
summary: A tapered conversion funnel with the drop-off at each step: split or isolate segments on a spring, and watch dots leave the band at the real rates.
---

<Demo name="SignupFunnel" />

## When to use

`FunnelChart` shows how a group shrinks across ordered steps, and where it shrinks the most. The band gets thinner at each
step, and every step states its count, its retention and how many left.

- **Signup and onboarding** in a product analytics page, to find the step that loses the most new people.
- **Checkout** in a store dashboard, from cart to payment, compared across traffic sources with `View`.
- **Sales pipeline** in a CRM, from lead to closed deal.
- **Course or tutorial completion**, where each lesson is a step.
- **Support journeys**, from opening a chat to solving the problem.

## When not to use

- **Steps that have no fixed order**, or people who can enter at any step: use `BarChart`.
- **A share of a whole at one moment**: use `DonutChart`. A funnel's steps are nested, not parts.
- **Change over time**: use `LineChart`. A funnel is one period; compare periods with one funnel per period.
- **Exact lookups across many steps and segments**: use `DataGrid`.
- **Values that rise from step to step**, such as cumulative revenue: the funnel caps each step at the one before it, so
  the rise would not show. Use `BarChart`.

## Usage

Add the stages in order. A stage with one number sets `Value`.

```xml
<arc:FunnelChart xmlns:arc="https://github.com/AIDotNet/TKCode.Arc" Label="Checkout in May" Unit="carts">
    <arc:FunnelChart.Stages>
        <arc:FunnelStage Key="cart" Label="Filled a cart" Value="900" />
        <arc:FunnelStage Key="address" Label="Entered an address" Value="510" />
        <arc:FunnelStage Key="paid" Label="Paid" Value="330" />
    </arc:FunnelChart.Stages>
</arc:FunnelChart>
```

### One series

Without `Segments` the funnel has one band. Dots stream through it unless `IsFlowEnabled` is off.

<Demo name="SingleSeries" />

### Segments and views

Add `Segments` and give each stage one count per segment in `Values`, in the order of the segments. A stage without
`Values` counts as zero for every segment.

`View` picks what the band shows:

| View | The band shows |
|---|---|
| `total` (default) | All segments as one band in the first series color |
| `split` | One band per segment with a gap between them, in the series colors |
| A segment key | That segment alone, rescaled so that its first step fills the band. Counts and rates become that segment's |

Other names show the total. The preview at the top of this page switches `View` with a `SegmentedControl`. The series
colors repeat after four segments, so keep to four or fewer.

### Reading the numbers

Each step shows its name (two lines at most), its count, its share of the previous step, and a down arrow with the number
that left since the previous step. Focusing a step adds a column highlight and a tooltip with the count, the retention from
the previous step, the retention from the first step and the number dropped. The split view also lists each segment.

A step can never be larger than the one before it. Larger values are capped to the previous step (per segment when there
are segments), negative and non-numeric values count as zero, so drop-off is never negative and retention stays between 0
and 100%. A previous step of zero gives a retention of 0%.

When the first step of the current view is zero, or there are no stages, the chart shows `EmptyLabel` and cannot be
focused.

### Formats

`ValueFormatter` replaces the number format everywhere: counts, drop-off, the tooltip and the spoken reading. `Unit` is
added by the default format only, after a space when it starts with a letter. Percentages are not formatted by it.

```csharp
chart.ValueFormatter = value => value >= 1000 ? $"{value / 1000:0.#}k" : $"{value:0}";
```

### Orientation and size

`Orientation` is `Auto` by default: left to right, and top to bottom when the chart is narrower than 420 px. `Horizontal`
and `Vertical` fix it. `BandHeight` sets the thickness of the first step when it runs left to right. When it runs top to
bottom the band takes about 42% of the width, and each step has a fixed height.

<Demo name="Vertical" />

### Reading the active step in code

`ActiveIndex` is two-way, and `ActiveIndexChanged` reports the step with its numbers. Both are null or zero when the
reading is cleared.

```csharp
chart.ActiveIndexChanged += (_, e) =>
{
    if (e.Index is not null)
    {
        status.Text = $"{e.Stage!.Label}: {e.RetentionFromPrevious:P0} of the previous step";
    }
};
```

### Changing data

`FunnelStage` and `FunnelSegment` are plain objects: the chart does not watch their properties. Assign a new list to
`Stages`, or add and remove items in an observable list. Stages and segments are matched by `Key`, so those that survive
morph to their new size.

## API reference

<!-- api: FunnelChart -->

<!-- api: FunnelStage -->

<!-- api: FunnelSegment -->

## Accessibility

- The chart is a focusable group. Its automation name is the reading of the active step (step number, name, count and both
  retention rates), or `Label` when no step is active. Its help text holds the reading or a summary of every step, and it is
  announced politely when it changes.
- Drop-off is an arrow with a number, and retention is a percentage, so nothing depends on color. In the split view each
  segment is also named in the tooltip.
- The focus ring follows `:focus-visible`.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the chart and read the first step. The chart is skipped while it is empty |
| `Right` / `Down` | Read the next step |
| `Left` / `Up` | Read the previous step |
| `Home` / `End` | Read the first or the last step |
| `Escape` | Clear the reading |

## Motion

- On first view the band grows out of its centre line.
- A view change morphs the band on one spring, and the gap of the split view opens and closes with it. A change during a
  morph keeps the speed of each edge.
- Counts and rates that change roll: the old text leaves in the direction of the change while the new text arrives.
- The highlight and the tooltip glide between steps on their own springs. The tooltip stays inside the chart.
- Dots run only while the chart is on screen, and they keep their place when it returns.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Band grows from the centre line | Short fade of the whole chart | Drawn at once |
| View or data change | Band morphs on a spring, numbers roll | Applied at once | Applied at once |
| Moving between steps | Highlight and tooltip glide | Applied at once | Applied at once |
| Dots | About 40 at once, leaving at the real drop-off | None | None |

## Responsive behavior

- With `Orientation` set to `Auto`, the chart measures its own width and goes top to bottom below 420 px, so it behaves the
  same in a narrow panel and on a phone. The stage names then sit to the right of the band.
- The tooltip opens beside the column and flips to the other side near an edge. Top to bottom, it opens above the step and
  flips below it near the top. It is always kept inside the chart.
- A touch press reads the step under the finger, and dragging moves the reading. The chart neither handles nor captures the
  touch, so a vertical drag in a `ScrollViewer` still scrolls the page, which ends the reading.
- With `Horizontal` in a narrow space, the text of each column is clipped to it; prefer `Auto`.

## Related

- `BarChart` for groups without an order.
- `DonutChart` for a share of a whole.
- `LineChart` for a conversion rate over time.
- `Treemap` for a breakdown by size.
- `DataGrid` for the exact numbers behind the funnel.
