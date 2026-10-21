---
title: Donut chart
summary: A donut whose arcs morph between datasets, with the active value rolling into the center.
---

<Demo name="ToggleSegments" />

## When to use

`DonutChart` shows how a whole divides into a few parts. The ring gives the shape of the split at a glance, the
legend beside it gives each part's value and share, and the center reads the total or the part under the pointer.
It fits a card or a panel on a dashboard, where the question is "what makes up this number" and the answer has two to
six parts.

- **Visit sources on a marketing overview**: Acme Analytics shows where this week's 48,200 visits came from (search,
  direct, social, email). The legend keeps every source readable, and small sources fold into Other instead of
  becoming slivers.
- **Spend by category on a finance page**: a controller reviews the month's spend as salaries, cloud hosting, marketing
  and software. Pinning a category in the legend keeps its value in the center while the controller reads the table.
- **Storage use on an admin console**: a workspace owner sees which projects take the most storage. Parts below a
  few percent group into Other, and the owner can hide a project to see how the rest of the ring changes.
- **Ticket status mix in a support dashboard**: the share of open, waiting and resolved tickets, switched between this
  month and this quarter with a dataset change that moves the arcs instead of redrawing the ring.

## When not to use

- **Many parts, or an exact comparison between them**: use `BarChart`. A ring makes twelve slices hard to compare, and
  the eye reads lengths better than angles.
- **A single value against a known limit**: use `Gauge` for a reading against a range with thresholds, or `UsageMeter`
  for a quota that people watch fill up.
- **A change over time**: use `LineChart`. A ring shows one moment, so a trend drawn as a series of rings hides the
  order.
- **Shares that people count, such as seats or cells of a grid**: use `WaffleChart`, which shows one cell per percent
  and reads as a count.
- **Parts nested inside parts that need drill-down**: use `Treemap`, which keeps the hierarchy and sizes the rectangles
  by value.

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

`DonutChart` derives from `ChartBase`, the base of the Arc chart family, so `Label`, `Unit`, `ValueFormat`,
`EmptyLabel` and `ItemsSource` behave as they do on the other charts. The donut adds the ring, the synced legend, the
center readout and the grouping rules. Because the ring is self-drawn, every arc is one drawing and a dataset change
moves the existing arcs instead of re-creating controls.

### Anatomy

1. **Ring**: the arcs, drawn with a 3 px gap between neighbours and 4 px rounded corners. The ring is 208 px across by
   default (`Diameter`) and 24 px thick (`RingThickness`). In a narrower container it scales down to fit.
2. **Center readout**: three short lines in the middle of the ring. At rest the top line is the total label
   (`TotalLabel`), the middle line the total value, and the bottom line the unit. Under a pointer or a pinned segment
   the lines show the segment's label, its value and its share with the total, for example "60% of total". The label and
   unit lines have a fixed height (12 px text at a 1.4 line height), so a change never moves the number.
3. **Legend rows**: one row per visible segment, with a color swatch, the label, the value and the share. The legend
   sits beside the ring when the container is at least 460 px wide, and below it otherwise.
4. **Other segment**: the grouped small parts. Its row names the parts it holds, and its tooltip and announcement list
   them.
5. **Focus ring**: drawn only for keyboard focus, around the legend row or, without a legend, around the ring.

Segment colors come from the series palette and are assigned by key the first time a key is seen, so a category keeps
its color when the dataset changes. Share text uses whole percents, and a share above zero but under one percent reads
"<1%".

### Sizes and grouping

`Diameter` and `RingThickness` set the ring. Parts whose share is below `GroupBelow` (a fraction of the total, 0.04 by
default) and parts beyond `MaxSegments` (6 by default, counting Other) fold into the Other segment. Grouping needs at
least two parts to fold: a single small part stays on its own, because grouping one part would hide it for nothing.

```xml
<arc:DonutChart Label="Storage"
                Diameter="160"
                RingThickness="18"
                GroupBelow="0.05"
                MaxSegments="5"
                ItemsSource="{Binding Storage}" />
```

Use a larger `GroupBelow` for a dashboard tile where small slices only add noise, and a smaller one for a finance page
where every category must be visible. Raise `MaxSegments` only when the legend has room: each extra row costs one line of
height beside or below the ring.

### Datasets

Replace `ItemsSource` to switch the dataset. The arcs keep their order and move from their current angles to the new
ones, so the segments do not re-mount. A segment whose key is new appears at its place in the order; a segment that
leaves closes to nothing before it drops out.

```csharp
month.Click += (_, _) => chart.ItemsSource = s_month;
quarter.Click += (_, _) => chart.ItemsSource = s_quarter;
```

Keep the keys the same across the datasets (`"search"` in both months, not `"Search"` in one and `"search-ads"` in the
other). A changed key is a different segment: it loses its color history and its hidden state.

### Legend and hidden segments

The legend hides and shows segments by default. Set `ShowLegend="False"` for a ring alone. `HiddenKeys` lists the
hidden keys and is two-way, so the view model can keep it. A hidden segment closes and the rest of the ring
redistributes, and the total in the center counts only the visible segments. The last visible segment cannot be
hidden, so the ring never becomes empty by accident.

<Demo name="RingOnly" />

```xml
<arc:DonutChart Label="Visits by source"
                HiddenKeys="{Binding HiddenKeys}"
                ShowLegend="True"
                ItemsSource="{Binding Sources}" />
```

A ring without a legend is a keyboard stop of its own: the arrow keys read each segment in turn. Use it when space
is tight and the ring already carries its meaning, such as a storage gauge on a card with a caption beside it.

### Pinning a segment

Set `LegendAction="Select"` to pin a segment instead of hiding it. `ActiveKey` holds the pinned key and is two-way.
Clicking a segment on the ring also pins it, in either mode. A pinned segment stays in the center readout until it is
released or another segment is pinned. `ActiveKeyChanged` fires when the pin changes, and the view model can read
`ActiveKey` in its handler or bind it.

<Demo name="Select" />

```xml
<arc:DonutChart Label="Spend by category"
                LegendAction="Select"
                ValueFormat="$#,##0"
                ActiveKey="{Binding PinnedCategory}"
                ItemsSource="{Binding Spend}" />
```

### Binding to a view model

The view model owns the data, the hidden keys and the pinned category. The chart shows them and writes the reader's
choices back. With CommunityToolkit.Mvvm:

```csharp
public partial class SpendViewModel : ObservableObject
{
    private static readonly IReadOnlyList<ChartPoint> s_october =
    [
        new ChartPoint("Salaries", 9800) { Key = "salaries" },
        new ChartPoint("Cloud hosting", 4120) { Key = "cloud" },
        new ChartPoint("Marketing", 2250) { Key = "marketing" },
        new ChartPoint("Software", 1350) { Key = "software" },
        new ChartPoint("Office", 640) { Key = "office" },
        new ChartPoint("Travel", 380) { Key = "travel" }
    ];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _spend = s_october;

    [ObservableProperty]
    private IReadOnlyList<string> _hiddenKeys = [];

    [ObservableProperty]
    private string? _pinnedCategory;
}
```

```xml
<arc:DonutChart Label="Spend by category"
                ItemsSource="{Binding Spend}"
                HiddenKeys="{Binding HiddenKeys}"
                ActiveKey="{Binding PinnedCategory}"
                LegendAction="Select"
                ValueFormat="$#,##0" />
```

- `HiddenKeys` and `ActiveKey` are two-way. A legend toggle writes the new list back, so the view model sees every
  change, including changes made with the keyboard.
- Replace the `HiddenKeys` list instead of editing it in place, because the chart reacts to the property assignment.
- `ItemsSource` is replaced on reload. Keep the keys stable so the arcs move instead of re-creating.
- `ValueFormat` (`$#,##0`) applies to the legend, the center and the announcements. Set it once on the chart rather
  than formatting the strings in the view model.

### Example: spend by category on a billing page

A finance controller opens the month's spend and sees where the money went before reading the table below. The ring
shows the split, the legend lists each category with its value and share, and the Other row groups the two small
categories so they do not clutter the ring. Office and travel together stay under the 4 percent threshold, so they fold
into Other, and the legend names both categories on that row. The view model pins the cloud hosting segment (`PinnedCategory` set to
`"cloud"`), so the center shows its value and share instead of the total. The chart is 620 px wide, so the legend sits beside the ring rather than
below it. The view model above supplies the points, so this page has no screenshot: the chart draws nothing from
markup alone.

```xml
<StackPanel Width="620" Spacing="16">
    <TextBlock Text="Spend by category, October 2026"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:DonutChart Label="Spend by category"
                    ValueFormat="$#,##0"
                    LegendAction="Select"
                    ActiveKey="{Binding PinnedCategory}"
                    ItemsSource="{Binding Spend}" />
</StackPanel>
```

### Content guidelines

Name each segment by what it is, and let the chart title say what the whole is. The legend and the center read the
segment label aloud, so a short noun phrase works better than a sentence.

| Instead of | Write |
|---|---|
| Visits | Visits by source |
| Other stuff | Other (a legend row lists its members) |
| Search engines and paid ads | Search |
| 0.4% | <1% (the chart writes this for small shares) |
| 48200 visits | 48,200 visits (`ValueFormat="N0"`) |
| Click a slice to see details | Click a segment to pin it |

Keep `TotalLabel` a single word such as Total, Visits or Spend, since it is the label on the center line at rest. Use
sentence case for labels, and spell out the unit that the reader needs, such as "visits" or "USD", in `Unit` rather
than repeating it in each label.

### Best practices

**Do**

- Use two to six segments. Each additional segment shrinks the arcs the reader must tell apart, and the legend is the
  reliable way to compare them.
- Give every point a stable `Key` that does not depend on the label. A key that changes on a rename gives the segment a
  new color and drops its hidden state.
- Keep the legend on for a financial or operational page. The legend is the keyboard path and the only place that
  shows each value and share.
- Set `GroupBelow` and `MaxSegments` deliberately so that Other holds only the parts too small to read.
- Pin the segment the page is about, through `ActiveKey`, so the center answers the question the page asks.

**Avoid**

- Rings for a long list or for a precise comparison. Use `BarChart` when the reader must rank the parts.
- Data that does not sum to a meaningful whole, such as overlapping categories. A segment that is counted in two places
  makes the shares wrong and the total meaningless.
- Hiding segments in the view model as the only way to show a subset. Filter the data in the view model instead when the
  hidden parts should not be counted in the total at all.
- Long labels on a narrow legend. The label is trimmed with an ellipsis, and the full name is kept in the tooltip and the
  announcement.
- Changing the keys between datasets. The arcs then close and re-open instead of moving to their new sizes.

### Common questions

**Why is a small segment missing from the ring?** It folded into Other, because its share was below `GroupBelow` (4
percent by default) or it ranked beyond `MaxSegments`. Hover the Other row or read its name in the legend to see its
members. A single small part is never folded into Other, so a part that is missing and not in Other has a value of zero
or less.

**Can the total stay on screen while a segment is pinned?** No. The center shows the pinned segment, its value and its
share instead of the total. If the page must show both, put the total in your own text next to the chart and bind it to
the same `Total` value you read from the view model.

**Does the total include hidden segments?** No. `Total` sums the visible segments, so hiding "Search" lowers the total
and the shares recalculate. Announcements read the new total after each toggle.

**How do I tell the chart to ignore a category?** Remove it from the data, or give it a value of zero or less. Values
that are zero or negative are ignored and never draw a segment.

**Can I change the ring size on a narrow card?** Set `Diameter` to the size you want, and the ring scales down to fit a
narrower container on its own. A card that is narrower than the legend breakpoint stacks the legend below the ring.

## API reference

<!-- api: DonutChart -->

### Legend action

| Member | Meaning |
|---|---|
| `DonutLegendAction.Toggle` | A legend row shows or hides its segment (the default) |
| `DonutLegendAction.Select` | A legend row pins its segment as `ActiveKey`; clicking it again releases the pin |

## Accessibility

- `Label` names the chart. Each legend row is a toggle whose name includes its value, its share and, for Other, the
  parts it holds. A hidden row reads "hidden" after its name.
- The legend is the keyboard path. Each row is a stop and a toggle for its own segment.
- Showing or hiding a segment is announced politely with the new total, for example "Search hidden. Total 50 visits."
- Without a legend the ring is focusable, and its name tells the reader to use the arrow keys to read each segment.
- The center readout is a raw element for assistive technology; the same reading reaches the reader through the
  announcement and the hidden summary.
- A hidden data table and summary list every visible part with its value and share.
- The focus ring appears only for keyboard focus.

### Keyboard

With the legend shown, each legend row is a stop. Without the legend, the ring is one stop and the arrow keys move
between its segments.

| Keys | Action |
|---|---|
| Legend: `Down` / `Right` | Move to the next row; the last row wraps to the first |
| Legend: `Up` / `Left` | Move to the previous row; the first row wraps to the last |
| Legend: `Home` / `End` | Move to the first or last row |
| Legend: `Enter` / `Space` | Show or hide the segment; in `Select` mode, pin or release it |
| Legend: `Escape` | Release the pinned segment (only when one is pinned) |
| Ring (no legend): `Right` / `Down`, `Left` / `Up` | Preview the next or previous visible segment, wrapping at the ends |
| Ring (no legend): `Home` / `End` | Preview the first or last visible segment |
| Ring (no legend): `Enter` / `Space` | Pin or release the previewed segment |
| Ring (no legend): `Escape` | Clear the preview and release the pin |

A segment previewed by the keyboard shows in the center, so the reader hears and sees the same value. Moving focus
away from the chart clears the preview.

## Motion

- On first view all segments sweep out from the top at once, and each trailing edge follows a beat later.
- A new dataset or a hidden segment morphs the start and end angles from where the arcs are now, so the ring keeps its
  motion and its order. Arcs carry their speed into the new position.
- The active segment moves outward along its middle and thickens slightly while the others dim. The pointer keeps the
  hover across gaps between segments.
- The center reading rolls to the active segment, and the total and the shares follow the new values.
- Reduced motion shows the arcs, the outward move and the text at their final state at once. Off behaves the same as
  Reduced.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Segments sweep out from the top, each trailing edge a beat behind | Shown at the final state | Shown at the final state |
| New dataset or hidden segment | Arcs morph from their current angles, keeping their speed | Shown at the final state | Shown at the final state |
| Active segment | Moves outward and thickens, the others dim | Shown at the final state | Shown at the final state |
| Center reading | Rolls to the new reading like a drum | Changes at once | Changes at once |

## Related

- `BarChart` when the parts are many or need exact comparison.
- `WaffleChart` for a share expressed as a count of cells, one per percent.
- `Treemap` when the parts are nested and need drill-down.
- `Gauge` for one value against a known range.

| Component | Choose it when |
|---|---|
| `BarChart` | There are more than six parts, or the reader must rank them |
| `WaffleChart` | The share is best read as a count of cells |
| `Treemap` | The parts nest inside groups and need drill-down |
| `Gauge` | One value is read against a range and thresholds |
| `UsageMeter` | A quota or a limit is the point of the panel |
| `LineChart` | The question is how the split changes over time |
