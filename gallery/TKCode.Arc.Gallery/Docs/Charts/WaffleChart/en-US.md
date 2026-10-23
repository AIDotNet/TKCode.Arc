---
title: Waffle chart
summary: A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
---

<Demo name="EnergyMix" />

## When to use

A waffle chart shows a whole as a grid of countable units, so a reader can see how many of the 100 cells belong to each
group at a glance. It fits a summary card or a dashboard tile where the question is "what share is this?" and the
answer must stay visible even when the share is small. Each cell is one percent by default, and the legend under or
beside the grid gives the exact figures.

- **Invoice status on a billing summary**: the share of this month's invoices that are paid, pending, overdue or
  failed in an Acme Analytics account page. Readers count the cells faster than they compare slices of a pie.
- **Support queue mix on an operations dashboard**: the share of open tickets by priority or channel. A small share,
  such as 4 percent urgent tickets, still shows as a visible block instead of a sliver.
- **Energy or budget mix in a report**: the share of electricity generation by source or of a department budget by
  cost centre, shown next to the same chart for the previous year. Cells that keep their group stay in place, so the
  change between the two datasets reads as movement.
- **Quota and seat usage in an admin console**: 412 of 500 seats used by a workspace. A 5 by 20 grid makes each cell
  one seat, which is more useful than a percentage when the unit is a seat, a license or a credit.
- **Survey answers in a product study**: a yes, no and not sure split for one question, with one decimal place in the
  share. Each answer is a readable group, and the grid needs no axis.

## When not to use

- **A share that changes over time**: use `LineChart` for a trend, or `Streamgraph` when several shares change together.
  A waffle chart shows one moment, so a series of dates has no place to go.
- **Many categories that must be compared exactly**: use `BarChart`. Past about six categories the cells of the
  smaller groups become too few to compare, and the tones start to repeat.
- **A part of a nested whole with drill-down**: use `Treemap`, which shows nesting and lets a reader move from a group
  to its parts.
- **One share shown with a total in the middle**: use `DonutChart`, which puts the total in the center of the ring
  and reads well in a narrow space.
- **One task's completion or one quota against its limit**: use `Progress` for a task and `UsageMeter` for a quota with
  a plan limit. Both give one value and a clear limit, which a grid of cells does not show as directly.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WaffleChart Label="Electricity generation"
                     Unit="TWh"
                     ItemsSource="{Binding Mix}" />
</UserControl>
```

`WaffleChart` derives from Avalonia's `Control` and paints its cells itself, with one focusable grid and one legend row
per category. It is not an items control: the categories come from `ItemsSource` or `Items`, and the chart draws them.
You size and lay it out like any other control, and the chart keeps its own keyboard model and accessible summary.

Categories come from `ItemsSource` or from the inline `Items` collection. When `ItemsSource` is set, `Items` is ignored.

```csharp
var neutral = new WaffleChart { Label = "Browser share", AccentKey = WaffleChart.NoAccent };
neutral.Items.Add(new WaffleCategory("chrome", "Chrome", 65.4));
neutral.Items.Add(new WaffleCategory("safari", "Safari", 18.6));
neutral.Items.Add(new WaffleCategory("other", "Other", 16.0));
```

The chart does not draw a title. Put the title in a `TextBlock` above it and pass the same words to `Label`, which is
the name that assistive technology reads.

### Anatomy

1. **Grid** (`Rows` by `Columns`): the cells. Each cell is one unit of the whole, and the default of 10 by 10 gives
   one cell per percent. The grid is square for ten by ten, with a 3 px gap between cells, and is at most 300 px wide.
2. **Legend** (`IsLegendVisible`): one row per category with a 10 px swatch, the category name and the share. The
   share uses the Medium weight in the primary text color, the name the secondary text color. Rows are at least 36 px
   high and sit 2 px apart.
3. **Legend position**: beside the grid when the chart is 420 px wide or more, with the grid at up to 232 px and a
   24 px gap. Below it when narrower, with the grid centered and the legend up to 320 px wide, below a 24 px gap.
4. **Cursor**: the cell under the pointer or the keyboard reading. It has a 2 px gap in the surface color and a 1.5 px
   ring in the foreground color at 55% opacity.
5. **Tooltip**: a card with the swatch, the category name and the share on the first line, and the formatted value
   (with `Unit`) on a second line in the muted text color. It sits above the cell, or below it when there is no room.
6. **Empty label** (`EmptyLabel`): centered muted text in place of the grid when there is no data. The empty chart is
   160 px high.

The shapes of the cells carry a second channel besides color. The accent category is a square cell with a 22% corner
radius. The neutral categories cycle through a square, a round cell and a square with a thin inner ring, so two
categories with similar tones still differ on screen and in grayscale.

### Choosing the grid

The grid holds `Rows` times `Columns` cells, and every category gets whole cells in proportion to its value. Choose the
size so that one cell means something a reader can count.

| Unit | Grid | Why |
|---|---|---|
| Percent of a whole | 10 by 10 (default) | One cell is one percent, which readers can count without help. |
| Seats or licenses in a plan | 5 by 20 or 10 by 50 | One cell is one seat or one unit of the quota. The grid stays wide and short. |
| Survey answers with 1 decimal | 10 by 10 with `Decimals="1"` | The cells still stand for percent, and the legend gives the precise figure. |

Cells are shared with the largest remainder method. The counts always add up to the grid, so 1, 1 and 1 across three
categories give 34, 33 and 33 cells. Values need not sum to 100: the chart computes each share from the total. Negative
values count as zero.

### Accent and colors

`AccentKey` names the category that takes the first series color. Left unset, the first category is the accent. The
next three categories take the secondary series colors in order, and the categories after them take neutral tones.

<Demo name="Neutral" />

```xml
<arc:WaffleChart Label="Quota used" Rows="5" Columns="20" AccentKey="used" ItemsSource="{Binding Quota}" />
```

Set `AccentKey` to the category you want to stand out, such as the overdue invoices or the failed payments. A
category with its own `Color` keeps that color, whatever its rank. Changing `AccentKey` at run time recolors the cells
in place: a cell does not move, it changes color on the same stagger as a data change.

`WaffleChart.NoAccent` removes the accent. The first three categories still take the secondary series colors in order,
and only the fourth and later categories take neutral tones. Use it when the categories are equal peers, as in the
browser share example, and expect color from the first three.

The palette has eight distinct tones: the accent, three secondary series colors and four neutral steps. Beyond eight
categories the tones repeat, and the shapes cycle every three neutral categories. Combine the small categories into
one "Other" before you reach that point.

### Datasets

Replace `ItemsSource` to switch the dataset. Cells that keep their group stay where they are, and the others fly to
their new block. A category is identified by its `Key`, so keep keys stable across datasets: "coal" in 2015 and "coal"
in 2023 are the same cells, while "Coal (2015)" is a new group.

```csharp
energy.ItemsSource = Mix2015;
```

Setting `ItemsSource` to a new collection rebuilds the chart once. A collection that raises change notifications, such
as `ObservableCollection<T>`, updates the chart on every add, remove or replace while it is attached.

### Selectors and non-category data

When the source is not a `WaffleCategory`, set `KeySelector` and `ValueSelector`. `LabelSelector` reads the label of
each item, and `Decimals` sets the number of decimal places a share shows.

<Demo name="Selectors" />

```xml
<arc:WaffleChart Label="Survey answers"
                 KeyMemberBinding="{Binding Name}"
                 ValueMemberBinding="{Binding Votes}"
                 ItemsSource="{Binding Answers}" />
```

A selector takes precedence over the matching member binding, and a member binding takes precedence over the
properties of a `WaffleCategory` item. When an item has no label, the label is its key. When an item has no key, the
control uses the item's `ToString()`, so give every item a key.

### Pinning a category

A pinned category keeps its cells highlighted and dims the others. Clicking a legend row pins the category, and
clicking it again releases it. `ActiveKey` holds the pinned key and is two-way by default, so a view model can read the
pin and set it from code.

```csharp
energy.ActiveKeyChanged += (_, e) => pinned.Text = $"Pinned: {e.Category?.Label ?? "none"}";
```

While a category is pinned, hovering or focusing another one shows that category instead, and releasing the pin
returns the highlight to the pinned category.

### Empty

With no categories, or a total of zero, the grid shows `EmptyLabel` in place of the cells. The grid is not focusable
while it is empty, and the legend is hidden.

<Demo name="Empty" />

### Binding to a view model

A chart with items and a pinned key is a view model concern. The view model owns the categories and the pin, and the
chart shows them. The `ItemsSource` and `ActiveKey` bindings are the ones that matter: `ActiveKey` is two-way by
default, so a click on a legend row writes the key back to the view model, and the view model can pin a category from
code by setting it.

```csharp
public sealed partial class InvoiceSummaryViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<WaffleCategory> _statuses = [];

    [ObservableProperty]
    private string? _pinnedStatusKey;

    [RelayCommand]
    private void PinOverdue() => PinnedStatusKey = "overdue";
}
```

```xml
<arc:WaffleChart Label="Invoice status, March 2026"
                 Unit="invoices"
                 ItemsSource="{Binding Statuses}"
                 ActiveKey="{Binding PinnedStatusKey}" />
```

Replace the whole `Statuses` list when the data changes, rather than editing it one item at a time. Each change to a
collection rebuilds the chart, so a list that changes item by item costs one rebuild per item. Use `ObservableCollection<T>`
only when the list is edited in place and each edit should show at once.

### Data shape and performance

Each item becomes a `WaffleCategory` with a `Key`, a `Label`, a `Value` and an optional `Color`. Every change reads the
items again, allocates the cells and moves the grid, so the work follows the number of cells (100 by default) and the
number of categories. The chart is one control with one drawing surface, not one control per cell. Refresh the data at
the pace a reader can follow, such as once a minute on a dashboard, and smooth a value that changes many times a second
before it reaches the chart.

Shares are computed from the total of the values, so pass raw counts or amounts rather than percentages you have already
rounded. Keep the values finite and non-negative in the source: a `NaN` or an infinite value breaks the total, and a
negative value is counted as zero.

### Example: billing summary on an account page

A billing summary card on an Acme Analytics account page shows the status of the invoices issued in March 2026. The
overdue group is the accent, because it is the one the finance team acts on. The legend sits beside the grid since the
card is wide. The title is a `TextBlock` above the chart, and the chart's `Label` repeats the title for assistive
technology. Shares show one decimal place so that the 6.9 percent overdue share is distinct from the 8.4 percent
pending share.

The view model holds the statuses as an immutable list. Each new month replaces the list, so the chart rebuilds once
and the cells fly to their new groups.

```csharp
public sealed record InvoiceStatusCount(string Key, string Label, double Count);

public interface IInvoiceService
{
    Task<InvoiceStatusCount[]> CountByStatusAsync(DateOnly month);
}

public sealed partial class InvoiceSummaryViewModel : ObservableObject
{
    private readonly IInvoiceService _invoices;

    public InvoiceSummaryViewModel(IInvoiceService invoices) => _invoices = invoices;

    [ObservableProperty]
    private IReadOnlyList<WaffleCategory> _statuses = [];

    [ObservableProperty]
    private string? _pinnedStatusKey;

    [RelayCommand]
    private async Task LoadAsync(DateOnly month)
    {
        var counts = await _invoices.CountByStatusAsync(month);
        Statuses = counts.Select(c => new WaffleCategory(c.Key, c.Label, c.Count)).ToList();
    }
}
```

![Invoice status for March 2026 with the overdue group as the accent and a legend beside the grid](images/InvoiceStatus.png)

```xml
<Border Width="620"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Invoices, March 2026"
                       FontSize="16"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Acme Analytics, 1,250 invoices issued this month"
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:WaffleChart Label="Invoice status, March 2026"
                         Unit="invoices"
                         Decimals="1"
                         AccentKey="overdue">
            <arc:WaffleChart.Items>
                <arc:WaffleCategory Key="paid" Label="Paid" Value="1012" />
                <arc:WaffleCategory Key="pending" Label="Pending" Value="105" />
                <arc:WaffleCategory Key="overdue" Label="Overdue" Value="86" />
                <arc:WaffleCategory Key="failed" Label="Failed" Value="47" />
            </arc:WaffleChart.Items>
        </arc:WaffleChart>
        <TextBlock Text="Updated 31 March 2026, 09:00 UTC"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</Border>
```

### Content guidelines

Labels and units are read aloud and shown in the tooltip, so write them as you would in a table of the same data.

| Instead of | Write |
|---|---|
| `No data` (the default empty text on a billing card) | `No invoices in this period` |
| `Chrome Browser Application Version 124` | `Chrome` (a long name is shortened on screen; the full name stays in the tooltip) |
| `Category A`, `Group 2` | The real category: `Paid`, `Overdue`, `Wind and solar` |
| TWH or twh as the unit | TWh, the unit written the way the source writes it |
| A bare title such as Chart as the label | `Invoice status, March 2026`, which names what the whole is |
| `Decimals="2"` on every chart | `Decimals="0"`, with one decimal only when two shares round to the same whole number |

Use title case for category names and sentence case for the chart's `Label`. Keep category names short: the legend
shortens a name that does not fit, and a shortened name is harder to match with the tooltip.

### Best practices

**Do**

- Keep the number of categories at six or fewer. Past eight the tones repeat, and a reader cannot tell the groups
  apart by color or by shape.
- Use stable `Key` values across datasets, so the cells stay in place and the change between two datasets reads
  as movement.
- Set `AccentKey` to the one category the reader should act on. A single accent gives the eye one place to start.
- Give the chart a title in a `TextBlock` and the same words in `Label`, so sighted and assistive technology users
  see the same question.
- Pass a `ValueFormatter` when the raw value needs a culture or a unit that `Unit` cannot express. The reason: the
  default value text uses the en-US grouping separator.

**Avoid**

- Using the chart for exact comparison between groups. A waffle chart reads a share well, but a bar chart reads the
  difference between two groups better, and the cells round each share to the nearest cell.
- Negative values, which the chart treats as zero. A net figure, such as a refund that cancels a payment, needs its
  own value before it reaches the chart.
- Changing `Rows` or `Columns` at run time for a live dashboard. The cell count changes, so every cell is reassigned
  at once, and the motion of the data change is lost.
- A chart with no title. The chart draws only the grid and the legend, and a reader cannot tell what the cells count.
- Hiding the legend (`IsLegendVisible="False"`) when no other element shows the figures. The cells show the shape of
  the whole, but only the legend gives each category's name and share.

### Common questions

**The chart shows "No data" although I set `Items`.** `ItemsSource` takes precedence. When `ItemsSource` is set, the
inline `Items` collection is ignored, so either set `ItemsSource` or add to `Items`, not both.

**Why does a category have one cell fewer than I expected?** Cells are shared with the largest remainder method. Each
category first gets the whole cells of its share, then the leftover cells go to the categories with the largest
fractions. Three equal categories get 34, 33 and 33 cells, because the first one takes the leftover cell.

**How do I keep one category's color whatever its position?** Give the category its own `Color`, or set `AccentKey` to
its key. A category with a `Color` keeps it, and the accent takes the first series color wherever it appears in the
data.

**How do I read the pinned category in code?** Handle `ActiveKeyChanged` and read `e.Key` or `e.Category`, which are
null when the pin is released. Or bind `ActiveKey` two-way to a view model property, as the example above does.

**Why does a share show a dot as the decimal separator in my German app?** The control formats shares with en-US rules,
and so is the default value text. Set `ValueFormatter` to format raw values with your own culture. The chart has no
formatter for shares, so when you need localized shares, read `Categories` and `CellCounts` and show your own text
beside the chart.

## API reference

<!-- api: WaffleChart -->

<!-- api: WaffleCategory -->

| Member | Type | Description |
|---|---|---|
| `Key` | `string?` | The pinned category key, or null when the pin is released |
| `Category` | `WaffleCategory?` | The pinned category, or null when the pin is released |

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

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move into the grid, which starts the reading at the first cell, then to each legend row. An empty chart is skipped |
| Arrow keys | Move the reading one cell up, down, left or right; the reading stops at the edge of the grid |
| `PageDown` / `PageUp` | Jump to the first cell of the next or previous category. From the middle of a category, `PageUp` goes to its first cell |
| `Home` / `End` | Go to the first cell, or to the last filled cell |
| `Escape` | Clear the reading |
| `Enter` / `Space` (legend row) | Pin the category, or release it when it is pinned |

The reading is announced as the category name, its share and its value, such as "Coal, 35%, 35 TWh".

## Motion

- Cells keep their place where they can. The rest fly to their new block in a staggered wave, and a cell in flight
  shrinks a little and changes color on the way.
- The legend shares count to their new values on a critically damped spring.
- On first view the cells appear one after another from the bottom left.
- Reduced motion places the cells and the shares at once and removes the shrink, the appearance and the stagger. Off
  behaves the same as Reduced.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Dataset replaced | Cells that keep their category stay in place; the others fly in a staggered wave, dip to 62% scale and change color | Placed at once | Placed at once |
| Legend share | Rolls to the new value on a smooth spring | Set at once | Set at once |
| First view | Cells appear column by column from the bottom left, scaling up from 40% with a staggered fade | Shown at once | Shown at once |
| Pinned or focused category | Other categories fade to 26% opacity over the standard duration | Change at once | Change at once |
| Accent change | Cells change color on the same stagger as a data change | Changed at once | Changed at once |

## Related

- `DonutChart` for a share in one ring with a total in the center.
- `BarChart` for exact comparison between categories.
- `Progress` for one task's completion, and `UsageMeter` for a quota against its limit.
- `Treemap` when the categories are nested and need drill-down.

| Component | Choose it when |
|---|---|
| `DonutChart` | One share with a total in the center, in a narrow space |
| `BarChart` | Readers must compare the size of categories exactly |
| `LineChart` / `Streamgraph` | The shares change over time |
| `Treemap` | Categories are nested and need drill-down |
| `Progress` / `UsageMeter` | One task's completion, or one quota against its limit |
