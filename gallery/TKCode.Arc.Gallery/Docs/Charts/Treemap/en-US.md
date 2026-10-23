---
title: Treemap
summary: A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
---

<Demo name="RevenueByRegion" />

## When to use

`Treemap` shows a hierarchy as nested rectangles. The size of each tile is its share of the whole, and a second
measure can deepen its color, so one view answers "where is the money (or the load, or the seats) concentrated, and
which parts of that are growing?" It is built for trees with many leaves, where a bar per leaf would be too many bars,
and it lets the reader drill into a branch and back out without leaving the screen.

- **Revenue by region, country and plan in a finance review**: the tiles show the size of each market, and the shade
  shows its growth. A reader sees that Americas carries most of the revenue and that India grows fastest, without a
  table of percentages.
- **Storage use by folder in an admin console**: a disk-usage view where each folder is a tile, and a click on a large
  folder drills into its subfolders. The breadcrumb shows the path back to the volume.
- **Seats and license use by customer segment in a billing view**: the same tree shows seats rather than revenue. The
  reader switches the measure and the tiles change size in place, so the two views read as one story.
- **Incidents by service and component in a reliability report**: tiles sized by incident count, shaded by the change
  against last quarter, with drill-down from the platform to the service that caused most of the pages.
- **Spend by team and cost center in a cloud cost review**: a team tile opens to show its accounts, and the breadcrumb
  keeps the path to the total visible while the reader works through the detail.

## When not to use

- **Exact comparison of values that are close to each other**: people judge the size of a rectangle less precisely than
  the length of a bar. Use `BarChart`, where the values share a baseline.
- **A few parts of one whole where the angle is enough**: four or five segments read well as a ring. Use `DonutChart`.
- **A change over time**: a treemap shows one moment. Use `LineChart` for a trend or `Streamgraph` for the mix over
  time.
- **A plain list of navigation steps**: the path back up the hierarchy is a breadcrumb on its own, not a chart. Use
  `Breadcrumb` when the page needs one.
- **A hierarchy the reader must read item by item, such as a file tree with thousands of entries**: use `TreeView` for
  browsing, or `DataGrid` for the exact values. A treemap hides small tiles, and their labels disappear when they do not fit.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

`Treemap` derives from Avalonia's `Control`. It takes its data from one of
three sources, in this order of precedence: `Root` (a `TreemapNode` tree), `ItemsSource` (your own objects, read with the
selectors or the member bindings), or `Items` (inline `TreemapNode` objects, for a small fixed set of leaves). The
tiles take their size from `Value` and their shade from `ColorValue`. A branch has no value of its own: its size is the
sum of its children. `Label` names the chart; when you set `Root`, the root node's own label is the first breadcrumb.

### Anatomy

1. **Breadcrumb**: the path from the root to the node in view. Each crumb is 32 units tall. The last
   crumb is the current level. The crumbs slide in from the left as the view changes.
2. **Tile stage** (`StageRadius`, 12 units): the rectangle that holds the tiles. Tiles are clipped to its rounded corners.
3. **Tiles**: the squarified layout. Neighboring tiles are separated by a 2 unit gap. A branch with children reserves a
   22 unit header for its name, and its children sit inside it with a 3 unit inset.
4. **Tile labels**: the name of each tile, laid out at the tile's own size. A label is shown only when it fits the tile,
   so small tiles stay unlabeled and their names appear in the tooltip.
5. **Color scale** (shown when `ColorLabel` is set): the name of the shading measure with its scale from the lowest to the
   highest value. The space between the breadcrumb, the tiles and the scale is 12 units.
6. **Tooltip**: the path of the tile's parents, the tile's name and its formatted value, and its share of the parent,
   such as "75% of Europe".

### Choosing a tree source

| Source | Use it when |
|---|---|
| `Root` with `TreemapNode` | The tree is built in code or comes from a service as a nested model you convert once |
| `ItemsSource` with the selectors or member bindings | Your domain objects already form the tree, and you want no copy |
| `Items` in XAML | A small fixed set of leaves, such as the folders of a demo or a static report |

With `ItemsSource`, `KeySelector` (or `KeyMemberBinding`) must return a unique id for each item, because the id is
what keeps a tile's identity across data changes. `ChildrenSelector` (or `ChildrenMemberBinding`) returns the nested
items of a branch, and a leaf returns no children.

### Node tree

Set `Root` to a tree of `TreemapNode` objects when the data is built in code. A node takes an `Id`, a `Label`, an
optional `Value` for a leaf, an optional `ColorValue` for the shade, and its `Children`. Every `Id` must be unique in the
tree: a tile that keeps its `Id` morphs to its new size when the data changes.

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

A branch's `ColorValue` is the size-weighted mean of its children, so a branch that has no color of its own still shows
the growth of the region it stands for. `TreemapNode.Source` returns the original data item when the node was read from
`ItemsSource`.

Without `ColorLabel` every tile is drawn at one depth. Use `Items` for a flat set of top-level leaves:

<Demo name="OneLevel" />

```csharp
chart.Items.Add(new TreemapNode("video", "Video", 112));
chart.Items.Add(new TreemapNode("photos", "Photos", 48));
chart.Items.Add(new TreemapNode("docs", "Documents", 12));
```

### Shading and color

`ColorLabel` turns on the second measure. Each top-level branch takes one of the first four series hues, in order, and
every tile inside it deepens with its `ColorValue` in a few steps from light to deep. A fifth and later branch stays
neutral rather than reuse a hue, so keep the number of top-level branches at four or fewer when the hue matters.
A tile with no `ColorValue` takes a neutral shade, and a negative value is shaded on the same scale as a positive one, so
a shrinking region reads as the light end of the scale.

`ColorDomainMinimum` and `ColorDomainMaximum` fix the ends of the shading range. Their default is `NaN`, which means the
smallest and the largest leaf value. Fix them when two treemaps on one screen must share a scale, or when the scale
should stay the same as the data changes, such as a growth range of minus 10 to plus 40 percent.

### Formatting

`ValueFormatter` formats the sizes and `ColorFormatter` formats the shading measure. The default size format groups the
digits and shows at most one decimal place. Both formatters are used in the tooltip, the color scale and the accessible
reading, so they must agree with the labels you put in `Label` and `ColorLabel`.

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
and color in place instead of being replaced. The total rolls to its new value, and the breadcrumb keeps the same path
when the ids in it still exist.

```csharp
void ShowSeats()
{
    chart.Label = "Seats";
    chart.ValueFormatter = v => $"{v:#,0}";
    chart.Root = seatsRoot;
}
```

### Focus and zoom

Clicking a tile zooms the view into the top-level branch that holds it: the branch fills the view, its siblings move
out, and its children open inside it. `FocusedNodeId` holds the id of the node in view, `null` is the root, and the
property is two-way, so the view model can keep the drill-down state across a refresh. Setting it from code zooms to the
node, which is the way to open a view at a given branch.

`FocusedNodeChanged` reports every change of the view, including a zoom out from the breadcrumb or the keyboard. Its
`Node` is the node now in view. Its `Id` is that node's id, so it is the root's id (such as `all`) when the root is in
view, while `FocusedNodeId` is `null` for the root.

```xml
<arc:Treemap Label="Revenue" Root="{Binding Tree}" FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

```csharp
chart.FocusedNodeChanged += (_, e) => status.Text = $"In view: {e.Node?.Label}";
```

### Binding to a view model

In an MVVM app the view model owns the tree, the measure and the drill-down state. With CommunityToolkit.Mvvm, the view
model is an `ObservableObject` and a measure change is one command that replaces the label, the formatter and the tree
together.

```csharp
public interface IRegionData
{
    TreemapNode BuildRevenueTree();

    TreemapNode BuildSeatsTree();
}

public partial class RevenueMapViewModel : ObservableObject
{
    private readonly TreemapNode _revenue;
    private readonly TreemapNode _seats;

    public RevenueMapViewModel(IRegionData data)
    {
        _revenue = data.BuildRevenueTree();
        _seats = data.BuildSeatsTree();
        Tree = _revenue;
    }

    [ObservableProperty]
    private TreemapNode? _tree;

    [ObservableProperty]
    private string? _focusedId;

    [ObservableProperty]
    private string _measureLabel = "Revenue";

    [RelayCommand]
    private void ShowRevenue()
    {
        MeasureLabel = "Revenue";
        Tree = _revenue;
    }

    [RelayCommand]
    private void ShowSeats()
    {
        MeasureLabel = "Seats";
        Tree = _seats;
    }
}
```

```xml
<arc:Treemap Label="{Binding MeasureLabel}"
             ColorLabel="Growth"
             Root="{Binding Tree}"
             FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

`Root`, `FocusedNodeId` and `PlotHeight` are the properties to bind. `FocusedNodeId` is two-way: the reader's drill-down
flows back to the view model, which can save it or restore it. `Label` is one-way, and the formatter, which is code, is
set in the view code-behind or in the view model's formatting service next to the tree it applies to.

### Data shape and performance

Each tile's size is computed from the leaves below it, and the layout is recomputed once per focused node, not once per
frame. Arc's guidance is a few hundred nodes on one view; beyond that, drill in with `FocusedNodeId` and show one level
at a time.

- **Leaves**: a leaf needs a `Value` greater than zero to take space. A leaf with no value, or a value of zero, takes no
  space, so check the data for empty items before you pass it in.
- **Depth**: each branch adds a header band and an inset, so deep trees leave less room for the leaves.
  Keep the tree to two or three levels when the tiles must carry labels.
- **Ids**: keep ids stable across refreshes. A new id is a new tile, and a reused id is the same tile, which moves to its
  new size.
- **Update frequency**: a refresh once a minute reads as an update. A tree that changes on every tick keeps the tiles
  moving, and the reader cannot follow a single tile.
- **Formatting**: sizes and shades are formatted by `ValueFormatter` and `ColorFormatter`. Use the same currency and
  percentage conventions on every screen, so that a shared view shows one number format.

### Example: revenue by region in a finance review

A finance lead opens the quarterly review with one picture of where the revenue sits and how fast each region grows.
The regions are the top level, so each region takes one of the four series hues, and the countries inside a region are
its tiles. The tile size is revenue in thousands of dollars, and the shade is growth against the same quarter last year,
so Germany is large and flat while India is smaller and much darker. The reader drills into Europe to see its countries
and uses the breadcrumb to return. Values are literal here because the scene is static; a live view binds `Root` or
`ItemsSource` as shown above.

![Revenue by region for Q2 2026, sized by revenue and shaded by growth](images/RevenueByRegion.png)

```xml
<StackPanel Width="720" Spacing="12">
    <arc:Treemap Label="Revenue, $K, Q2 2026"
                 ColorLabel="Growth, %"
                 PlotHeight="320">
        <arc:Treemap.Items>
            <arc:TreemapNode Id="amer" Label="Americas">
                <arc:TreemapNode Id="us" Label="United States" Value="420" ColorValue="18" />
                <arc:TreemapNode Id="ca" Label="Canada" Value="80" ColorValue="12" />
                <arc:TreemapNode Id="br" Label="Brazil" Value="65" ColorValue="31" />
            </arc:TreemapNode>
            <arc:TreemapNode Id="emea" Label="Europe">
                <arc:TreemapNode Id="de" Label="Germany" Value="310" ColorValue="7" />
                <arc:TreemapNode Id="uk" Label="United Kingdom" Value="240" ColorValue="-2" />
                <arc:TreemapNode Id="fr" Label="France" Value="90" ColorValue="5" />
            </arc:TreemapNode>
            <arc:TreemapNode Id="apac" Label="Asia Pacific">
                <arc:TreemapNode Id="jp" Label="Japan" Value="190" ColorValue="14" />
                <arc:TreemapNode Id="in" Label="India" Value="65" ColorValue="44" />
                <arc:TreemapNode Id="au" Label="Australia" Value="65" ColorValue="4" />
            </arc:TreemapNode>
        </arc:Treemap.Items>
    </arc:Treemap>
</StackPanel>
```

The labels state the units, because the default number format shows only the digits. A growth value of minus 2 shades
the United Kingdom tile at the light end of the scale, which is how a shrinking market reads without a red color.

### Content guidelines

Name the whole by the measure, not by the view, such as "Revenue, $K, Q2 2026". Put the units in the label or the
formatter, because a bare "420" reads as nothing on a screen with three measures. Keep the tile names short enough to
fit the smallest tile you expect to label.

| Instead of | Write |
|---|---|
| `Label="Chart"` | `Label="Revenue, $K, Q2 2026"` |
| `ColorLabel="g"` | `ColorLabel="Growth, %"` |
| `Label="All regions"` on the root when the chart is "Revenue" | `Label="Revenue"` on the chart, and a root named "All regions" |
| `Label="United Kingdom (UK)"` | `Label="United Kingdom"` |
| `Label="Seats (number)"` | `Label="Seats"` |
| A tile named with a code, such as `uk-ent` | A readable name for the plan or the segment |

### Best practices

**Do**

- Put the measure and its unit in `Label`, and format the sizes with `ValueFormatter` when a bare number would be
  ambiguous. The labels are what a screen reader reads first.
- Give every tree a stable `Id` for each node. The id decides whether a tile morphs or is replaced when the data changes.
- Keep top-level branches to four or fewer when the hue carries meaning, because the fifth and later branches are neutral.
- Set `ColorDomainMinimum` and `ColorDomainMaximum` when two treemaps must share a scale, so that the same shade means
  the same growth on both.
- Bind `FocusedNodeId` two-way when the drill-down must survive a refresh, so the reader does not lose the branch they
  were reading.

**Avoid**

- A treemap with many small leaves that all need labels. Tiles that are too small show no label, so the reader cannot
  read them without the tooltip.
- Deep trees of five or six levels. Every level takes a header and an inset, and the leaves become slivers.
- Shading for a measure without a direction, such as headcount. A color scale for a value that has no good or bad side
  invites a false reading.
- A tree that is rebuilt with new ids on every refresh. The tiles are replaced, not morphed, and the drill-down is lost.
- Using the shade as the only way to tell regions apart. The hues, the labels and the reading carry the identity.

### Common questions

**Why is a tile missing its label?** The label appears only when it fits the tile. Make the chart larger, raise
`PlotHeight`, or drill into the branch so the tile grows. The tile's name and value are still in its tooltip and in its
reading when it has focus.

**Why do the tiles of one region share a hue?** Hue is per top-level branch: every tile inside a region takes that
region's hue. The depth of the shade inside it comes from each tile's own `ColorValue`, on one scale for the whole chart.

**How do I open the treemap already zoomed into a branch?** Set `FocusedNodeId` to the branch's id, for example
`FocusedNodeId="emea"`. `null` shows the root.

**Why does `FocusedNodeChanged` report the root's id, not `null`?** The event reports the node in view, so the root
reports its own id, such as `all`. `FocusedNodeId` is `null` for the root, so compare with the node's `Id` and do not
test the event's `Id` for `null`.

**Can a branch have a value?** No. A branch's size is the sum of its children and its shade is the size-weighted mean of
its children's `ColorValue`. Put the value on the leaves.

## API reference

<!-- api: Treemap -->


<!-- api: TreemapNode -->

## Accessibility

- `Label` names the chart. The stage announces the chart name, the node in view and the keys to use, such as "Revenue,
  All regions. Arrow keys move between tiles, Enter zooms in, Escape zooms out." A tile that has focus is read with its
  path, value and share of its parent, such as "Europe, Germany, 15, 75% of Europe".
- The stage is a polite live region. Zoom changes and the focused tile's reading are announced without moving focus.
- The breadcrumb is a navigation landmark, and its last crumb marks the current level.
- A hidden data table lists every node, so the hierarchy can be read without seeing the tiles.
- Tile shade is never the only signal. The reading includes the color measure's value, and the color scale names it.
- Labels are shown only when they fit the tile. A tile too small for its label is still read through the stage reading
  when it has focus, and through the tooltip when the pointer is over it.

### Keyboard

The stage is one tab stop. Arrow keys move between the top-level tiles of the node in view, and the breadcrumb is a
second tab stop for the path.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus between the tile stage and the breadcrumb |
| `Arrow` keys | Move to the nearest top-level tile in that direction |
| `Home` / `End` | Move to the first or the last top-level tile |
| `Enter` / `Space` | Zoom into the focused branch and focus its first child |
| `Escape` / `Backspace` | Zoom out one level and focus the node you left |
| In the breadcrumb: `Left` / `Right` | Move between the crumbs |
| In the breadcrumb: `Home` / `End` | Move to the root crumb or the current level |
| In the breadcrumb: `Enter` / `Space` | Zoom out to the focused crumb |

Enter on a leaf does nothing, because a leaf has no children to open. Escape at the root clears the focused tile.

## Motion

- Zooming in fills the view with the chosen tile in one affine move: its siblings fly out and its children open inside
  it. Zooming out follows the same path in reverse.
- Changing the measure moves each tile to its new size and color in place, and the total rolls to its new value.
- Labels keep their size while the tiles zoom. A label appears only when it fits its tile.
- The tooltip follows the pointer on a snappy spring, and it stays inside the stage.
- Reduced motion places the tiles at their final positions at once. Off behaves the same as Reduced.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Zoom in or out | One affine move on a smooth spring of about 0.62 s; siblings fly out and children open | Tiles jump to their final positions | Immediate |
| Measure or data change | Each tile moves and changes color in place on a smooth spring; the total rolls | Tiles jump to their final positions | Immediate |
| Breadcrumb change | The crumbs slide to their new places | Immediate | Immediate |
| Tooltip | Follows the pointer on a snappy spring | Immediate | Immediate |

## Related

- `BarChart` for exact comparison of values that are close to each other.
- `DonutChart` for a few parts of one whole.
- `Breadcrumb` for a navigation path that is not part of a chart.

| Component | Choose it when |
|---|---|
| `BarChart` | Values are close to each other and the reader must compare them exactly |
| `DonutChart` | There are a few parts of one whole and the share is the message |
| `LineChart` | The question is how a total changed over time |
| `Streamgraph` | The mix of parts changes over time and the flow is the story |
| `Breadcrumb` | A navigation path is needed on a page that has no treemap |
| `DataGrid` | The reader needs every node with its exact values, sorted and filtered |
