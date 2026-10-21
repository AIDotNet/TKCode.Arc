---
title: Treemap
summary: A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
---

<Demo name="RevenueByRegion" />

## When to use

- A hierarchy with many leaves, such as revenue by region, country and plan, where the share of each part matters.
- A second measure that should be read beside the size, such as growth, shown as the depth of each tile's color.
- Drilling into a branch and back out, where the breadcrumb keeps the path visible.

## When not to use

- Exact comparison of values that are close to each other: use `BarChart`.
- A few parts of a whole where the angle is enough: use `DonutChart`.
- A plain list of navigation steps: use `Breadcrumb` on its own.

## Usage

The tiles take their size from `Value` and their shade from `ColorValue`. A branch has no value of its own: its size
is the sum of its children. `Label` names the whole, which is also the first breadcrumb.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Treemap Label="Revenue"
                 ColorLabel="Growth"
                 ItemsSource="{Binding Regions}"
                 KeyMemberBinding="{Binding Code}"
                 LabelMemberBinding="{Binding Name}"
                 ValueMemberBinding="{Binding Revenue}"
                 ColorValueMemberBinding="{Binding Growth}"
                 ChildrenMemberBinding="{Binding Countries}" />
</UserControl>
```

### Node tree

Set `Root` to a tree of `TreemapNode` objects when the data is built in code. Each `Id` must be unique in the tree:
a tile that keeps its `Id` morphs to its new size when the data changes.

```csharp
var root = new TreemapNode("all", "All regions", children:
[
    new TreemapNode("amer", "Americas", children:
    [
        new TreemapNode("us", "United States", 420, 18),
        new TreemapNode("ca", "Canada", 80, 12)
    ]),
    new TreemapNode("emea", "Europe", children:
    [
        new TreemapNode("de", "Germany", 190, 7),
        new TreemapNode("uk", "United Kingdom", 150, -2)
    ])
]);

chart.Label = "Revenue";
chart.Root = root;
```

Without `ColorLabel` every tile is drawn at one depth. Use `Items` for a flat set of top-level leaves:

<Demo name="OneLevel" />

```csharp
chart.Items.Add(new TreemapNode("video", "Video", 112));
chart.Items.Add(new TreemapNode("photos", "Photos", 48));
chart.Items.Add(new TreemapNode("docs", "Documents", 12));
```

### Formatting and shading

`ValueFormatter` formats the sizes and `ColorFormatter` formats the shading measure. `ColorDomainMinimum` and
`ColorDomainMaximum` fix the ends of the shading range; by default they are the smallest and largest leaf values.

```xml
<arc:Treemap Label="Seats"
             ColorLabel="Growth"
             ValueFormatter="{Binding FormatSeats}"
             ColorFormatter="{Binding FormatGrowth}"
             ColorDomainMinimum="-10"
             ColorDomainMaximum="40" />
```

### Switching measures

Change `Label`, `ValueFormatter` and `Root` together. Tiles with the same `Id` keep their identity, so they change size
and color in place instead of being replaced.

```csharp
void ShowSeats()
{
    chart.Label = "Seats";
    chart.ValueFormatter = v => $"{v:#,0}";
    chart.Root = seatsRoot;
}
```

### Focus and zoom

Clicking a branch fills the view with it. `FocusedNodeId` holds the id of the node in view (`null` is the root), and it
can be set from code to zoom to a node. `FocusedNodeChanged` reports every change.

```xml
<arc:Treemap Label="Revenue" Root="{Binding Tree}" FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

```csharp
chart.FocusedNodeChanged += (_, e) => status.Text = $"In view: {e.Node?.Label}";
```

## API reference

<!-- api: Treemap -->

<!-- api: TreemapNode -->

## Accessibility

- `Label` names the chart. Each tile is a focusable group whose name gives its path, value and share of its parent.
- Arrow keys move to the nearest tile in that direction, Home and End go to the first and last top-level tile, and
  Enter or Space zooms into the focused branch.
- Escape or Backspace zooms out one level. The breadcrumb is a navigation landmark and marks the current level.
- Zoom changes and the focused tile's path, value and shading are announced politely.
- A hidden data table lists every node, so the hierarchy can be read without seeing the tiles.

## Motion

- Zooming in fills the view with the chosen tile in one affine move: its siblings fly out and its children open inside
  it. Zooming out follows the same path in reverse.
- Changing the measure moves each tile to its new size and color in place, and the total rolls to its new value.
- Labels keep their size while the tiles zoom. A label appears only when it fits its tile.
- Reduced motion places the tiles at their final positions at once. Off behaves the same as Reduced.

## Related

- `BarChart` for exact comparison of values that are close to each other.
- `DonutChart` for a few parts of a whole.
- `Breadcrumb` for a navigation path that is not part of a chart.
