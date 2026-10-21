---
title: Donut chart
summary: A donut whose arcs morph between datasets, with the active value rolling into the center.
---

<Demo name="ToggleSegments" />

## When to use

- Two to six parts that make up one whole, such as traffic sources or spending categories.
- A total in the center that should change as the reader points at a part.
- A dataset switch, such as this month and this quarter, where the arcs should morph to their new sizes.

## When not to use

- Many parts or a need for exact comparison: use `BarChart`.
- A single value against a known limit: use `Gauge`, or `UsageMeter` for a quota.
- A change over time: use `LineChart`.

## Usage

Each `ChartPoint` is one segment. Give every point a stable `Key` so the segments keep their identity when the data
changes. Values that are zero or less are ignored.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DonutChart Label="Visits by source"
                    Unit="visits"
                    TotalLabel="Total"
                    OtherLabel="Other"
                    ItemsSource="{Binding Sources}" />
</UserControl>
```

### Datasets

Replace `ItemsSource` to switch the dataset. The arcs keep their order and move from their current angles to the new
ones, so the segments do not re-mount.

```csharp
month.Click += (_, _) => chart.ItemsSource = s_month;
quarter.Click += (_, _) => chart.ItemsSource = s_quarter;
```

### Size and grouping

`Diameter` and `RingThickness` set the ring. Parts below `GroupBelow` (a fraction of the total, 0.04 by default) and
parts beyond `MaxSegments` (6 by default, including Other) fold into the Other segment.

```xml
<arc:DonutChart Label="Storage"
                Diameter="160"
                RingThickness="18"
                GroupBelow="0.05"
                MaxSegments="5"
                ItemsSource="{Binding Storage}" />
```

### Legend and hidden segments

The legend hides and shows segments by default. Set `ShowLegend="False"` for a ring alone. `HiddenKeys` lists the
hidden keys and is two-way, so the view model can keep it.

<Demo name="RingOnly" />

```xml
<arc:DonutChart Label="Visits by source"
                HiddenKeys="{Binding HiddenKeys}"
                ShowLegend="True"
                ItemsSource="{Binding Sources}" />
```

### Pinning a segment

Set `LegendAction="Select"` to pin a segment instead of hiding it. `ActiveKey` holds the pinned key and is two-way.

<Demo name="Select" />

```xml
<arc:DonutChart Label="Spend by category"
                LegendAction="Select"
                ValueFormat="$#,##0"
                ActiveKey="{Binding PinnedCategory}"
                ItemsSource="{Binding Spend}" />
```

## API reference

<!-- api: DonutChart -->

## Accessibility

- `Label` names the chart. Each legend row is a toggle whose name includes its value, its share and, for Other, the
  parts it holds.
- The legend is the keyboard path. Down and Right move to the next row, Up and Left to the previous row, and Home and
  End go to the first and last row. Enter or Space shows or hides a segment; in Select mode it pins or unpins it.
- Escape clears a pinned segment.
- A change of visible segments is announced politely with the new total.
- Without a legend the ring is focusable: the arrow keys read each segment and Enter pins it.
- The ring and the center reading are hidden from assistive technology. A hidden data table lists every part.
- The focus ring appears only for keyboard focus.

## Motion

- On first view all segments sweep out from the top at once, and each end follows a beat later.
- A new dataset or a hidden segment morphs the start and end angles from where the arcs are now, so the ring keeps its
  motion and its order.
- The active segment moves outward along its middle and thickens slightly while the others dim. The pointer keeps the
  hover across gaps between segments.
- The center reading rolls to the active segment, and the total and the shares count to their new values.
- Reduced motion shows the arcs, the outward move and the text at their final state at once. Off behaves the same as
  Reduced.

## Related

- `BarChart` when the parts are many or need exact comparison.
- `WaffleChart` for a share expressed as a count of cells, one per percent.
- `Treemap` when the parts are nested and need drill-down.
- `Gauge` for one value against a known range.
