---
title: Pivot builder
summary: Build a pivot table by dragging fields into rows, columns and values, or with each chip's keyboard menu; pick each value's aggregation and the table regroups at once with row, column and grand totals.
---

<Demo name="Preview" />

## When to use

`PivotBuilder` lets a person summarize a list of records without writing a query. The fields of the records wait in a list;
dragging one into Rows makes a row for each of its values, into Columns a column for each value, and into Values an
aggregate (sum, count, average, min or max) in every cell. The table under the zones regroups at once and adds a total for
every row, every column and the whole.

- **Ad hoc reports.** Revenue by region and quarter, tickets by team and month.
- **Exploring a data export.** Try a few groupings before building a dashboard.
- **Teaching pivots.** Every change shows its result immediately.

## When not to use

- **Fixed reports.** When the grouping never changes, show a `Table` or a chart directly.
- **Millions of records.** The block groups in memory on the UI thread; aggregate on the server first.
- **Editing records.** The table shows aggregates; use `DataGrid` for cell editing.

## Usage

The block derives from `TemplatedControl`. Without records it pivots invented sales. Give it `ItemsSource` and `Fields`;
each `PivotBuilderField` reads one value from a record (`PivotBuilderField.Create<T>` is typed and AOT safe).

```xml
<arc:PivotBuilder ShowTotals="False" IsFramed="False" />
```

```csharp
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("team", "Team", t => t.Team));
pivot.Fields.Add(PivotBuilderField.Create<Ticket>("hours", "Hours", t => t.Hours, isNumeric: true, format: "N1"));
pivot.RowFields.Add("team");
pivot.ValueFields.Add(new PivotBuilderValue("hours", PivotBuilderAggregation.Average));
pivot.ItemsSource = tickets;
```

### Layout

`RowFields`, `ColumnFields` and `ValueFields` hold the layout and change as the person drags; change them from code to set
a starting layout. `MoveField` and `SetAggregation` do what the chips do and raise `LayoutChanged`. Non-numeric fields in
Values are counted. `Result` holds the computed pivot for your own export. Words are in `Labels` (`PivotBuilderLabels`).

<Demo name="OwnRecords" />

## API reference

<!-- api: PivotBuilder -->

<!-- api: PivotBuilderField -->

<!-- api: PivotBuilderValue -->

<!-- api: PivotBuilderAggregation -->

<!-- api: PivotBuilderZone -->

## Accessibility

- Every chip is a button named by its field and zone. **Enter** or **Space** opens its menu: move to another zone, move
  left or right, choose the aggregation, or remove. **Delete** removes it from the layout.
- The zones are named Fields, Rows, Columns and Values; the table is the Arc `Table` with its caption.
- Every move is announced ("Region moved to Columns").

## Motion

- A dragged chip follows the pointer by its transform and lifts with the raised shadow; the zone under it tints in 160 ms.
- Reduced motion keeps the drag (it is direct manipulation) and the tints without easing.

## Responsive behavior

- At `CompactWidth` (760 px) or less the field list moves above the zones; padding tightens to 16.
- Zone chips wrap; wide tables scroll inside the Arc `Table`.

## Related

- `Table`, the grid the block fills.
- `DataGrid` for editable spreadsheets.
- `Metric explorer` and `Revenue explorer`.
