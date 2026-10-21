---
title: Waffle chart
summary: A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
---

<Demo name="EnergyMix" />

## When to use

- A share that people need to count, such as the share of answers or the energy mix in percent.
- The same categories compared across two datasets, where cells that stay in place show what changed.
- A small share that must stay visible, where a pie or a ring would show only a sliver.

## When not to use

- A share that changes over time: use `LineChart` or `Streamgraph`.
- Many categories compared exactly: use `BarChart`.
- A part of a nested whole with drill-down: use `Treemap`.

## Usage

Each `WaffleCategory` is one group of cells. The values are weights: the cells are shared out in proportion to them. The
first category fills from the bottom left, one column at a time.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:WaffleChart Label="Electricity generation"
                     Unit="TWh"
                     ItemsSource="{Binding Mix}" />
</UserControl>
```

Categories can also be added directly through `Items`.

```csharp
var neutral = new WaffleChart { Label = "Browser share", AccentKey = WaffleChart.NoAccent };
neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
neutral.Items.Add(new WaffleCategory("other", "Other", 16.0));
```

### Datasets

Replace `ItemsSource` to switch the dataset. Cells that keep their group stay where they are, and the others fly to
their new block.

```csharp
energy.ItemsSource = Mix2015;
```

### Selectors and non-category data

When the source is not a `WaffleCategory`, set `KeySelector` and `ValueSelector`. `LabelSelector` reads the label of each
item, and `Decimals` sets the number of decimal places a share shows.

<Demo name="Selectors" />

### Grid and accent

`Rows` and `Columns` set the grid, which is ten by ten by default. `AccentKey` names the category that takes the first
series color. Other categories take the secondary colors and then neutral tones. Set `AccentKey` to `NoAccent` to use
neutral tones only.

<Demo name="Neutral" />

```xml
<arc:WaffleChart Label="Quota used" Rows="5" Columns="20" AccentKey="used" ItemsSource="{Binding Quota}" />
```

### Pinning a category

Pinning a legend row keeps its cells highlighted. `ActiveKey` holds the pinned key and is two-way.

```csharp
energy.ActiveKeyChanged += (_, e) => pinned.Text = $"Pinned: {e.Category?.Label ?? "none"}";
```

### Empty

With no categories, or a total of zero, the grid shows `EmptyLabel` in place of the cells.

<Demo name="Empty" />

## API reference

<!-- api: WaffleChart -->

<!-- api: WaffleCategory -->

## Accessibility

- `Label` names the chart. The grid is a focusable group with a role description and instructions for the keys.
- Tab moves focus to the grid first, then to each legend row. Each legend row is a toggle with a text name.
- In the grid, the arrow keys move the readout one cell at a time. PageUp and PageDown jump to the first cell of the
  previous or next category, and Home and End go to the first and last filled cell. Escape clears the readout.
- Enter or Space on a legend row pins or releases its category.
- A polite live region announces the category, the share and the value under the readout. A hidden data table lists each
  category with its value, share and cell count.
- The cells are hidden from assistive technology. Long legend names are shortened on screen, but the full name is kept
  in the tooltip, the live region and the data table.

## Motion

- Cells keep their place where they can. The rest fly to their new block in a staggered wave, and a cell in flight
  shrinks a little and changes color on the way.
- The legend shares count to their new values on a critically damped spring.
- On first view the cells appear one after another from the bottom left.
- Reduced motion places the cells and the shares at once and removes the shrink, the appearance and the stagger. Off
  behaves the same as Reduced.

## Related

- `DonutChart` for a share in one ring with a total in the center.
- `BarChart` for exact comparison between categories.
- `Progress` for one task's completion, and `UsageMeter` for a quota against its limit.
- `Treemap` when the categories are nested and need drill-down.
