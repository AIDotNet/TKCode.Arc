---
title: Activity heatmap
summary: See a year of activity at a glance, one square per day.
---

## When to use

- Daily rhythm over a long period, such as contributions or workouts across a year.
- Continuity and gaps matter more than exact comparison: streaks and quiet weeks are the story.
- A calendar-shaped overview where each day is a square that can be read and selected.

## When not to use

- Exact comparison between days: use `BarChart`.
- Picking a date for input: use `Calendar`.
- A trend of a value over time: use `LineChart`.

## Usage

Each `ActivityDay` is one square: a `Date` and a `Count`. Days that are missing count as zero. `Period` completes the
summary, such as "1,284 contributions in 2025".

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActivityHeatmap Label="Contributions in 2025"
                         Period="2025"
                         ItemsSource="{Binding Days}" />
</UserControl>
```

### Years and controls

Replace `ItemsSource`, `Period` and `Label` together when the year changes. `Actions` places controls beside the summary,
such as a year switch. The grid recolors in a sweep instead of redrawing.

```xml
<arc:ActivityHeatmap Label="Contributions in 2025" Period="2025" ItemsSource="{Binding Days}">
    <arc:ActivityHeatmap.Actions>
        <StackPanel Orientation="Horizontal" Spacing="4">
            <arc:Button Content="2024" Variant="Ghost" Size="Small" Command="{Binding ShowPrevious}" />
            <arc:Button Content="2025" Variant="Ghost" Size="Small" Command="{Binding ShowCurrent}" />
        </StackPanel>
    </arc:ActivityHeatmap.Actions>
</arc:ActivityHeatmap>
```

### Units and weeks

`UnitOne` and `UnitOther` set the noun for the count. `FirstDayOfWeek` sets the first row of each week column, which is
Sunday by default. `Culture` formats dates and numbers.

```xml
<arc:ActivityHeatmap Label="Workouts, last six months"
                     Period="the last six months"
                     UnitOne="workout"
                     UnitOther="workouts"
                     FirstDayOfWeek="Monday"
                     MaxWidth="520"
                     ItemsSource="{Binding Workouts}" />
```

### Thresholds

`Thresholds` holds the upper bounds of levels one to three. A count above the last bound is level four. By default the
bounds are quarters of the busiest day.

```csharp
workouts.Thresholds = [1, 2, 3];
```

### Selecting a day

`SelectedDate` is the selected day and is two-way. `DateSelected` reports a day chosen with a click, Enter or Space.

```csharp
heatmap.DateSelected += (_, e) => selected.Text =
    $"Selected {e.Date:MMM d, yyyy}: {e.Count} contributions (level {e.Level})";
```

## API reference

<!-- api: ActivityHeatmap -->

<!-- api: ActivityDay -->

## Accessibility

- `Label` names the grid, such as "Contributions in 2025". The summary completes the name with the total and the period.
- The up and down arrow keys move one day, and the left and right arrow keys move one week. Home and End go to the first
  and last day, and Enter or Space selects the focused day.
- Escape hides the tooltip. A single tooltip glides between the days, and it is not repeated for each square.
- Each day is a named grid cell. The cells are hidden from assistive technology as drawing, and the summary and the grid
  structure carry the information.
- The total is announced in a status region, and the day under the pointer or the focus is announced politely. The
  legend is a row of toggles, one per level, so a level is not identified by its color alone.

## Motion

- On first view the cells wave in once, with the total duration capped so that a large grid stays quick.
- A new year or range recolors the grid in a diagonal sweep, with the cells staggered by column.
- The tooltip glides between cells on a snappy spring, and its text rolls to the new day.
- The total counts to its new value on a smooth spring.
- Reduced motion removes the wave, the sweep and the glide, and uses a short fade of 160 ms. Off jumps to the final state.

## Related

- `BarChart` for exact comparison between periods.
- `Calendar` for choosing a date.
- `LineChart` for a trend of a value over time.
- `Sparkline` for a compact trend beside a value.
