---
title: Slope chart
summary: Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
---

<Demo name="BeforeAndAfter" />

## When to use

`SlopeChart` compares two moments for a small set of items. Each item is one line from its value in the first period to
its value in the second, on one shared value axis, so the reader sees who rose, who fell and how the order changed in a
single glance. It fits a review page where the question is "what changed between these two dates", such as a quarterly
business review or a cohort comparison.

- **Conversion by acquisition channel, Q1 against Q2** in a growth review. Organic search that moved from fourth to
  second is visible without a table, and the rank move beside each value states it in words.
- **Retention by plan for two cohorts** in a SaaS board pack. Six plans on two axes show which plans improved and which
  slipped.
- **Revenue by region, this year against last year** in a sales review. The slopes show growth, and crossing lines show
  regions that overtook one another.
- **Satisfaction score by support team before and after a process change** in an operations report. The chart shows
  whether the change moved every team or only some.
- **Market share of a few vendors in two quarters** in a competitive review, where the order matters as much as the size.

## When not to use

- **Three or more moments in time**, such as monthly values for a year: use `LineChart`, which keeps the time axis.
- **Only the latest value of each item**, with no comparison: use `BarChart`.
- **Parts of one whole** rather than a change between two moments: use `DonutChart`.
- **More than about ten items**: the labels crowd each other on the two axes. Use `BarChart` for a longer list, sorted by
  the second value.
- **A distribution of many observations per group**, such as response times by region: use `Ridgeline`.

## Usage

Each `SlopeItem` is one line with a `Key`, a `Label`, a `Start` value and an `End` value. `StartLabel` and `EndLabel`
name the two moments; they default to "Before" and "After".

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SlopeChart Label="Conversion rate by channel"
                    StartLabel="Q1"
                    EndLabel="Q2"
                    HighlightKey="search"
                    ItemsSource="{Binding Channels}" />
</UserControl>
```

`SlopeChart` derives from Avalonia's `Control` and draws itself, so the lines, labels and rank moves are one drawing that
moves smoothly when the data changes. The whole box takes the pointer, and the chart is focusable as one group.

### Anatomy

1. **Column headings** (`StartLabel`, `EndLabel`): one heading above each value axis, centered on it.
2. **Value axes**: two 1 px vertical lines in the border color. Both columns share one value scale, so a steeper line is
   a larger change.
3. **Lines**: 2 px round-capped strokes in a neutral tone. The item named by `HighlightKey` takes the first series color,
   and when one item is in focus the others fade.
4. **Dots**: a 4 px dot at each end, outlined in the surface color so it reads over the line.
5. **Start labels**: the name and then the value, right aligned 10 px to the left of the start axis.
6. **End labels**: the value 10 px to the right of the end axis, followed by the rank move when `IsRankVisible` is on:
   an arrow with the number of places, or "=" when the rank did not change.
7. **Leader lines**: a thin line from each label to its dot. Labels pushed aside to avoid a collision stay connected by
   these lines.
8. **Tooltip**: the name, both values, the change and the rank text, such as "up 1 to rank 1". It is at least 168 px wide.

The plot height is 44 px per item and at least 200 px when `PlotHeight` is not set. Below 440 px of width the label
columns narrow and the names are shortened; the full names stay in the tooltip and the data table.

### Datasets

Replace `ItemsSource` to switch the dataset. Every line, dot and label slides to its new place. Change `Label` in the same
step so the text matches the data.

```csharp
conversion.Click += (_, _) =>
{
    chart.Label = "Conversion rate by channel";
    chart.ItemsSource = Conversion;
};
```

Items keep their line across datasets when they keep the same `Key`, so the line morphs instead of being replaced. Use
keys that name the entity, such as a channel id, not a row number that changes when the list is re-sorted.

### Value and change formats

`ValueFormatter` formats the values at each end. `ChangeFormatter` formats the change beside the rank, and it receives
the change and the item.

```csharp
chart.ValueFormatter = v => $"{v:0.0}%";
chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} pts";
```

By default values are grouped with at most one decimal, and the change is signed with a true minus sign, such as "+2" or
"−2". Set the formatters once on the chart so that every item reads the same way.

### Highlight and rank

`HighlightKey` draws one item in the first series color, and the others stay neutral. `IsRankVisible` turns the rank
moves on or off.

```xml
<arc:SlopeChart Label="Close values"
                StartLabel="2023"
                EndLabel="2024"
                IsRankVisible="False"
                MaxWidth="420"
                ItemsSource="{Binding Values}" />
```

Turn the rank off when the order does not matter to the question, or when the rank would only repeat what the slope
already shows.

### Reading an item

`ActiveKey` is the item under the pointer or the keyboard, and it is two-way. `ActiveKeyChanged` reports each change, with
the item in `Item`.

```csharp
chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item
    ? $"{item.Label}: {item.Start} → {item.End}"
    : "No item in focus";
```

### Binding to a view model

With CommunityToolkit.Mvvm the view model owns the items and the key of the item in focus. Each row is a record, and the
chart reads its fields through member bindings, so the rows do not need to be `SlopeItem` objects.

```csharp
public sealed record ChannelRate(string Key, string Name, double Q1, double Q2);

public partial class ChannelsViewModel : ObservableObject
{
    [ObservableProperty]
    private ChannelRate[] _channels = [];

    [ObservableProperty]
    private string? _activeChannel;
}
```

```xml
<arc:SlopeChart Label="Conversion rate by channel"
                StartLabel="Q1"
                EndLabel="Q2"
                ItemsSource="{Binding Channels}"
                KeyMemberBinding="{Binding Key}"
                LabelMemberBinding="{Binding Name}"
                StartMemberBinding="{Binding Q1}"
                EndMemberBinding="{Binding Q2}"
                ActiveKey="{Binding ActiveChannel, Mode=TwoWay}" />
```

- `ActiveKey` is two-way: the chart writes the key of the item in focus, and the view model can focus an item from code.
- The selectors (`KeySelector`, `LabelSelector`, `StartSelector`, `EndSelector`) win over the member bindings when both
  are set, which is useful when a value needs a calculation.
- The examples use plain `Binding` for brevity. Compiled bindings need `x:DataType` on the view.

### Data shape and label collisions

Each item needs a stable `Key`, a `Label`, and both values. Both columns use one value axis, so a line's slope compares
the two moments directly. Give every item both values, because each one is a number that is drawn at its place.

When two labels would overlap in one column, they move apart by the label gap (20 px) and stay connected to their dots by
leader lines. Touching labels form a group that settles around the place its members want to be. The demo below has four
close values in the first column; the names stay readable because the labels spread instead of stacking.

<Demo name="LabelCollisions" />

### Example: channel conversion in a growth review

A growth review for Acme Analytics compares the conversion rate of six acquisition channels in Q1 and Q2. The chart
highlights organic search, the channel the team invested in, so the reader's eye has one target. Email still leads in
both quarters, which the slope shows without a label. The caption states the finding, so the card reads without hovering.

![A slope chart of six channels, with organic search highlighted and moving from fourth to second](images/ChannelConversion.png)

```xml
<Border xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
        Width="680"
        Padding="24"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Conversion rate by channel" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Percent of sessions that bought, Q1 and Q2 2026. Organic search moved from fourth to second."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:SlopeChart Label="Conversion rate by channel"
                        StartLabel="Q1"
                        EndLabel="Q2"
                        HighlightKey="search">
            <arc:SlopeChart.Items>
                <arc:SlopeItem Key="search" Label="Organic search" Start="3.1" End="4.6" />
                <arc:SlopeItem Key="email" Label="Email" Start="5.8" End="5.2" />
                <arc:SlopeItem Key="paid" Label="Paid social" Start="2.2" End="3.4" />
                <arc:SlopeItem Key="referral" Label="Referral" Start="4.4" End="4.1" />
                <arc:SlopeItem Key="direct" Label="Direct" Start="3.6" End="3.7" />
                <arc:SlopeItem Key="display" Label="Display ads" Start="1.2" End="0.9" />
            </arc:SlopeChart.Items>
        </arc:SlopeChart>
    </StackPanel>
</Border>
```

The highlighted line is the story, and the rank moves beside each value say which channels rose or fell. Email and
Referral slipped slightly, and Display ads has the lowest value in both quarters. The values are plain numbers in the
screenshot, so the caption carries the unit; in an app, set `ValueFormatter` to add the percent sign.

### Content guidelines

- **Chart label**: the measure and its scope, such as "Conversion rate by channel". The label names the chart for
  assistive technology and the summary.
- **Column headings**: the real names of the two moments, such as "Q1" and "Q2" or "2025" and "2026". Use "Before" and
  "After" only when the moments are events, such as a launch.
- **Item labels**: the entity name in sentence case, short enough to sit beside its value: "Organic search", "Paid
  social". Long names are kept in the tooltip and the data table.
- **Changes**: write the change with its sign and unit, such as "+1.5 pts", so the reader does not need the axis.

| Instead of | Write |
|---|---|
| Before / After | Q1 / Q2 |
| Conversion | Conversion rate by channel, Q1 and Q2 |
| +1.5 | +1.5 pts |
| SEO | Organic search |

### Best practices

**Do**

- Keep the same unit on both moments. A slope between two different measures is not a comparison, and a reader cannot
  tell that from the picture.
- Keep the list to about three to ten items, so the two labels in each column stay readable.
- Highlight the one item the story is about with `HighlightKey`. The other lines stay neutral, so the highlight means
  something.
- Give each item a stable `Key` that names the entity. A dataset change then morphs the same line instead of replacing it.

**Avoid**

- Plotting more than two moments as slopes. A third point makes the line look like a trend it is not; use `LineChart`.
- Highlighting several items. The first series color stops meaning anything when every line uses it.
- Hiding the change when the reader must compare magnitudes. Keep the change or the rank visible in that case.
- Changing `Label` and the data in different steps. The title then describes the previous dataset for a moment.

### Common questions

**Why do the column headings say "Before" and "After"?** Those are the defaults for `StartLabel` and `EndLabel`. Set them
to the real moments, such as "Q1" and "Q2".

**Why do the keyboard and the summary list the items in a different order from my data?** The keyboard and the summary
follow the end value, highest first, while the drawing keeps your data order. Sort the data by the end value when the two
orders should match.

**What does a flat dataset look like?** When every value is the same, the axis spans one unit on either side, so the
lines sit in the middle of the plot.

**What happens to the reading when the item in focus is removed?** `ActiveKey` is cleared to null when its item is no
longer in `ItemsSource`, so the reading stops describing an item that is gone.

## API reference

<!-- api: SlopeChart -->

<!-- api: SlopeItem -->

## Accessibility

- `Label` names the chart. The chart is a focusable group with a role description and instructions for the keys.
- Tab moves focus to the chart and reads the item with the highest end value first. The down and right arrow keys move
  to the next item and the up and left arrow keys to the previous one, in order of the end value.
- Home and End go to the first and last item, Escape clears the reading, and a touch press and drag reads the nearest
  line.
- A polite live region announces both values, the change and the rank move, such as "Search, Q1 3, Q2 5, +2, up 1 to
  rank 1". A hidden data table lists the items in order of their end value.
- Rank moves use an arrow glyph with a number, so they are not shown by color alone. Lines and labels are hidden from
  assistive technology.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the chart; the item with the highest end value is read first |
| `Down` / `Right` | Move to the next item in end-value order |
| `Up` / `Left` | Move to the previous item in end-value order |
| `Home` / `End` | Move to the first (highest end value) or the last item |
| `Escape` | Clear the reading |

From rest, any arrow key moves to the first item. The mouse reads the line nearest the pointer and clears the reading when
it leaves the chart.

## Motion

- On first view each line draws from its start point to its end point, with a short stagger. The dots and labels arrive
  as the line lands.
- Switching datasets moves the lines, dots, labels and leader lines on a shared morph spring. Rank numbers roll in the
  direction they moved.
- The focused item comes to the front and the others fade. The tooltip slides to the middle of its line.
- Reduced motion draws every line at once and moves them at once on a change. Off jumps to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Lines draw from start to end with a short stagger; dots and labels land with each line | Every line is drawn at once | Final state |
| Dataset change | Lines, dots, labels and leaders move on a shared spring; rank numbers roll | Moves at once | Final state |

## Related

- `LineChart` for three or more moments in time.
- `BarChart` for one value per item at a single moment.
- `Ridgeline` for how the distribution of groups changes.

| Component | Choose it when |
|---|---|
| `LineChart` | There are three or more moments, and the time axis matters |
| `BarChart` | Each item has one value at a single moment, or the list is longer than about ten |
| `Ridgeline` | Each group is a distribution of many observations, not one value |
| `DonutChart` | The data is parts of one whole, not a change between two moments |
| `Sparkline` | A compact trend sits beside a single number |
