---
title: Sunburst
summary: A hierarchy drawn as concentric rings: click a segment to make it the new centre, with a breadcrumb back, a rolling total and a legend of shares.
---

<Demo name="CostBreakdown" />

## When to use

`Sunburst` shows how a total divides across several levels at once. Each ring is one level of the tree, and the angle of
a segment is its share of the node at the centre, so the reader sees both the parts and the parts of the parts on one
circle, then clicks a segment to make it the new centre.

- **Annual costs in a budget review**: departments on the first ring, cost types on the second and suppliers on the
  third, with a click to look inside one department.
- **Storage use in an admin console**: folders within folders, where the centre names the folder in view and rolls its
  total as the reader moves through the tree.
- **Support tickets by product, area and cause**: three short levels whose shares matter more than their exact values.
- **Household or project spending by category**: a few top-level groups, where the legend gives the exact amounts beside
  the ring.

## When not to use

- **Exact comparison of close values**: people judge an angle less precisely than a length. Use `BarChart`.
- **One level of parts**: a single ring needs none of the zooming. Use `DonutChart`.
- **Many small leaves that all need a name**: a rectangle holds a label better than a thin arc. Use `Treemap`.
- **A change over time**: a sunburst shows one moment. Use `LineChart` or `Streamgraph`.
- **Browsing a large tree item by item**: use `TreeView`, or `DataGrid` when the exact values matter.

## Usage

Set `Root` to a tree of `SunburstNode` objects, or write the top-level nodes inline. A node has an `Id`, a `Label`, a
`Value` when it is a leaf, and its `Children`. A branch's value is always the sum of its children.

```csharp
chart.Label = "Annual costs";
chart.ValueFormatter = v => $"${v:#,0}K";
chart.Root = new SunburstNode("all", "All costs", children:
[
    new SunburstNode("people", "People", children:
    [
        new SunburstNode("crew", "Crew", 2060),
        new SunburstNode("shore", "Shore staff", 430)
    ]),
    new SunburstNode("fleet", "Fleet", 2975)
]);
```

Inline nodes sit under a root that the chart makes for you, named by `Label`.

### Anatomy

1. **Breadcrumb**: the path from the root to the centre. Choosing an earlier crumb zooms back to it.
2. **Rings**: up to `Depth` levels below the centre, all the same thickness. Top-level branches take the series hues in
   data order, and each level below is a lighter tint of its branch.
3. **Centre**: the current node's label and total, with the digits rolling when the total changes. Clicking it zooms out
   one level.
4. **Arc labels**: shown along the arc or across the ring, only where the text fits. The others stay in the tooltip.
5. **Legend** (`IsLegendVisible`): the centre's children with their values and share of the centre.
6. **Tooltip**: the segment's path, value and share of its parent.

### Controlled focus

`FocusedId` is the id of the node at the centre, and it is two-way. `null` means the root, so a zoom back to the root
writes `null`. Setting the id of a leaf shows its parent at the centre, because a leaf has nothing to open, and an id that
is not in the tree shows the root. `FocusedIdChanged` reports every move with the new node and what caused it.

<Demo name="ControlledFocus" />

```xml
<arc:Sunburst Label="Annual costs"
              Root="{Binding Costs}"
              FocusedId="{Binding CentreId, Mode=TwoWay}" />
```

```csharp
chart.FocusedIdChanged += (_, e) =>
    status.Text = $"{e.Node.Label} from {e.ChangeSource}";
```

`Id` in the event arguments is always a real id, the root's own at the root. `ChangeSource` is `Segment`, `Breadcrumb`,
`Legend`, `Center` or `Code`.

### Depth and legend

`Depth` sets how many rings show at once. Rings share the room between the hole and the edge, so when the centre has
fewer levels below it than `Depth`, the rings grow to fill the circle. `IsLegendVisible="False"` gives the whole width to
the ring. `Size` is the largest diameter, and the ring scales down to fit its container.

<Demo name="CustomDepth" />

### Formatting and empty state

`ValueFormatter` is used in the centre, legend, tooltip and accessible reading. The default groups the digits and keeps
at most one decimal. The digits of the first number in the formatted text roll, and the text around them stays still, so
`$1,284K` rolls `1,284`. A formatted value with no number is shown as plain text.

When the total is zero, the chart shows `EmptyLabel` in an empty ring, and the breadcrumb and legend are hidden.

### Data shape and performance

Layout runs once per data change and each arc is drawn by the chart itself, so a few hundred nodes stay smooth. Give every
node a stable, unique `Id`: a node that keeps its id moves to its new angles when `Root` is replaced. A node with no id
gets one from its place in the tree, and a repeated id is skipped. Replace `Root` to show changed values, since the chart
does not watch the children of a node. Negative and missing values count as zero. Segments follow the order of your data.

## API reference

<!-- api: Sunburst -->

<!-- api: SunburstNode -->

## Accessibility

- `Label` names the chart. The rings are one focusable group, a polite live region whose name gives the centre and the
  keys to use.
- With a segment in focus, the automation name reads its path, value and share of its parent, such as
  "People, Crew, $2,060K, 82.7% of People".
- Legend rows are buttons named with the label, value and share. Each is a tab stop.
- The breadcrumb is a navigation landmark, and its last crumb is exposed as the current level.
- Hue is never the only signal: labels, the legend and the reading carry the same information.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the rings and highlight the first segment; then the legend rows |
| `Left` / `Right` | Previous or next sibling, wrapping around |
| `Home` / `End` | First or last sibling |
| `Down` | First child; the centre moves if that child is past the last ring |
| `Up` | Parent; from the first ring, zoom out one level |
| `Enter` / `Space` | Zoom into the focused branch and focus its first child |
| `Escape` / `Backspace` | Zoom out one level and focus the node you left; at the root, clear the highlight |

Enter on a leaf does nothing, because a leaf has no children to open.

## Motion

- Every arc moves to its new angles and radii on one shared spring, so a zoom reads as one move. Interrupting a zoom keeps
  each arc's speed.
- Rings resize as levels appear or leave, and arcs that leave close against the centre or the outer edge.
- Arc labels fade out as the arcs move and fade in once they settle.
- On first view the arcs open clockwise from 12 o'clock, and the centre's digits roll up.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Zoom in or out | Arcs move on a spring of about 0.62 s | Arcs jump; labels fade in | Immediate |
| Data change | Arcs move to their new angles; the total rolls | Arcs jump; labels fade in | Immediate |
| First view | Arcs open on a spring of about 0.9 s | The chart fades in | Immediate |
| Tooltip | Follows the pointer on a snappy spring | Immediate | Immediate |

## Responsive behavior

- The chart reads its own width, not the window's. With room for the ring and `LegendMinWidth` (200 px) beside it, the
  legend sits to the right. Otherwise it moves below the ring.
- The ring is at most `Size` across and shrinks to fit. A legend beside the ring scrolls when it has more rows than fit
  the ring's height.
- A tap acts like a click: on a branch it zooms, and on a leaf it shows the tooltip. Tapping the centre or a breadcrumb
  zooms out.

## Related

- `Treemap` for a hierarchy with many leaves that need labels.
- `DonutChart` for one level of parts.
- `Breadcrumb` for a navigation path outside a chart.

| Component | Choose it when |
|---|---|
| `Treemap` | The tree has many small leaves and area reads better than angle |
| `DonutChart` | There is one level, and the share is the message |
| `BarChart` | Values are close and must be compared exactly |
| `TreeView` | The reader browses the tree item by item |
| `DataGrid` | The reader needs every node with its exact value |
