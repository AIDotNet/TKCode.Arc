---
title: Slope chart
summary: Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
---

## When to use

- A before and after comparison of two moments, such as conversion in Q1 and Q2 by channel.
- Showing who rose or fell and how the order changed between the two moments.
- Three to ten items. Above about ten, the labels crowd each other.

## When not to use

- Three or more moments in time: use `LineChart`.
- Only the latest value of each item: use `BarChart`.
- Parts of one whole rather than a change between two moments: use `DonutChart`.

## Usage

Each `SlopeItem` is one line with a `Key`, a `Label`, a `Start` value and an `End` value. `StartLabel` and `EndLabel`
name the two moments.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SlopeChart Label="Conversion rate by channel"
                    StartLabel="Q1"
                    EndLabel="Q2"
                    HighlightKey="search"
                    ItemsSource="{Binding Channels}" />
</UserControl>
```

### Datasets

Replace `ItemsSource` to switch the dataset. Every line, dot and label slides to its new slope. Change `Label` in the
same step so the text matches the data.

```csharp
conversion.Click += (_, _) =>
{
    chart.Label = "Conversion rate by channel";
    chart.ItemsSource = Conversion;
};
```

### Value and change formats

`ValueFormatter` formats the values at each end. `ChangeFormatter` formats the change beside the rank, and it receives
the change and the item.

```csharp
chart.ValueFormatter = v => $"{v:0.0}%";
chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} pts";
```

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

### Reading an item

`ActiveKey` is the item under the pointer or the keyboard, and it is two-way. `ActiveKeyChanged` reports each change.

```csharp
chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item
    ? $"{item.Label}: {item.Start} → {item.End}"
    : "No item in focus";
```

## API reference

<!-- api: SlopeChart -->

<!-- api: SlopeItem -->

## Accessibility

- `Label` names the chart. The chart is a focusable group with a role description and instructions for the keys.
- Tab moves focus to the chart and reads the item with the highest end value first. The down and right arrow keys move
  to the next item and the up and left arrow keys to the previous one, in order of the end value.
- Home and End go to the first and last item, Escape clears the reading, and a touch press and drag reads the nearest
  line.
- A polite live region announces both values, the change and the rank move. A hidden data table lists the items in
  order of their end value.
- Rank moves use an arrow glyph with a number, so they are not shown by color alone. Lines and labels are hidden from
  assistive technology.

## Motion

- On first view each line draws from its start point to its end point, with a short stagger. The dots and labels arrive
  as the line lands.
- Switching datasets moves the lines, dots, labels and leader lines on a shared morph spring. Rank numbers roll in the
  direction they moved.
- The focused item comes to the front and the others fade. The tooltip slides to the middle of its line.
- Reduced motion draws every line at once and moves them at once on a change. Off jumps to the final state.

## Related

- `LineChart` for three or more moments in time.
- `BarChart` for one value per item at a single moment.
- `Ridgeline` for how the distribution of groups changes.
