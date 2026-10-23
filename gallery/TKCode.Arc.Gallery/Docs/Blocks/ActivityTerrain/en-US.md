---
title: Activity terrain
summary: A week of hourly activity as a 3D terrain: orbit and zoom it, hover an hour for its numbers, fly to a bar, press it flat into a heat map, and read the total, the peak hours and the busiest day beside it.
---

<Demo name="Preview" />

## When to use

`ActivityTerrain` shows one week of hourly numbers, Monday 00:00 to Sunday 23:59, as 168 bars standing on a floor: seven
rows of days, twenty-four columns of hours. A bar is as tall and as dark as its hour was busy, so the rhythm of the week reads
at a glance: the morning ramp, the lunch dip, the quiet weekend. Hovering a bar lifts it and shows its value, the same hour a
week earlier and the shape of that day; clicking it flies the camera there. Beside the plot, a summary gives the week's total
and its change, the busiest hours and the busiest day.

- **Product analytics.** Visits, sign-ups or searches per hour, to see when people actually use the product and when a
  release or a maintenance window would hurt least.
- **Engineering activity.** Builds, deploys or merged changes per hour, to show when the team ships and whether Friday
  afternoons stay quiet.
- **Operations and on-call.** Alerts or incidents per hour in the danger tone, where one bad afternoon stands out as a single
  red spike and a rise on last week reads as bad news.
- **Support and sales desks.** Chats, tickets or calls per hour, to plan staffing around the peaks the summary lists.
- **A showcase on a dashboard's front page**, where the motion (the intro, the wave on a metric switch, the fly-in) invites
  people to explore a week they would otherwise skim in a table.

## When not to use

- **For more or fewer than seven days by twenty-four hours.** The shape is fixed; use `ActivityHeatmap` for a year of days,
  or a line or bar chart for other periods.
- **When exact comparison matters more than the overall shape.** Perspective makes far bars look smaller; the heat map view
  helps, but a table or a bar chart compares values more precisely.
- **On a page with many charts.** The terrain is large and animated; one per page is plenty. Use a sparkline or a
  `MetricCard` for small multiples.
- **For data that is not a count or an amount per hour** (ratios, averages that can be negative): bar heights start at zero
  and grow with the value.

## Usage

### Anatomy

From top to bottom: an optional title and subtitle; a toolbar with the metric switch (shown when there are two metrics or
more) and the view switch (terrain or heat map); the plot, with the hover card and a "Reset view" button that appears once the
camera has moved; a legend from less to more with a hint about the gestures; and the summary with three blocks, the week's
total and its change, the peak hours and the busiest day. The plot's height is 44% of the block's width, between 280 and 460.

### Metrics

Each `ActivityTerrainMetric` is one measure: 168 values in `Values`, Monday 00:00 first, then hour by hour and day by day
(`day × 24 + hour`). `PreviousValues` holds the week before in the same order; without it the change and the "Previous week"
row are hidden. `Label` names the metric on the switch, `Unit` follows values in the card and the summary, and `Key` lets you
select it with `SelectedMetricKey`.

```csharp
var visits = new ActivityTerrainMetric
{
    Key = "visits",
    Label = "Visits",
    Unit = "visits",
    Values = thisWeek,            // 168 numbers: Monday 00:00, Monday 01:00, … Sunday 23:00
    PreviousValues = lastWeek     // optional
};
terrain.Metrics.Add(visits);
```

Data held as a 7 × 24 grid converts with `ActivityTerrainMetric.Flatten(grid)` (rows are days from Monday, columns hours).
Missing entries count as zero. The terrain works out everything else: the total, the change on the previous week, the peak
hours (largest first, ties by day and hour), the day totals, the busiest day and the share of the week that fell in working
hours.

### Tone and good news

`Tone` colors the bars, the legend and the current hour in the card: `Accent` (the default), `Neutral`, `Success`, `Warning`
or `Danger`. `HigherIsBetter` decides whether a rise shows green or red; set it to false for errors, incidents or wait times.
A metric in the danger tone with one bad afternoon:

<Demo name="Incidents" />

```xml
<arc:ActivityTerrain PeakCount="5"
                     Title="Incident load"
                     Subtitle="Alerts that paged someone, per hour.">
    <arc:ActivityTerrainMetric Key="incidents" Label="Incidents" Unit="incidents" Tone="Danger" HigherIsBetter="False" />
</arc:ActivityTerrain>
```

### Terrain and heat map

`ViewMode` switches between the terrain (`Perspective`) and a flat heat map seen from above (`Top`). In the heat map the bars
press flat into squares, every day is labeled, and dragging no longer turns the view; hovering, clicking and zooming still
work. The view switch in the toolbar sets the same property, and `ViewModeChanged` reports the change.

<Demo name="HeatMap" />

### Hovering and flying in

Hovering a bar, or moving to it with the arrow keys, highlights its hour: the bar lifts, a band runs along its row and column
to the axis labels, the rest of the week fades back, and the card shows the day, the hour, the value, the previous week, the
change and the hour's share of its day, with the day's 24 hours as small bars. `HighlightedCell` holds that hour and
`CellHovered` reports it.

Clicking a bar flies the camera to it (zoom 1.5 at least) and keeps it lifted; clicking empty floor or pressing Escape
returns to the whole week. `FocusedCell` holds the hour, `CellFocused` reports it, and `FlyTo`, `ShowWeek`, `FlyToCommand`
do the same from code. The peak hour buttons in the summary fly to their hour and back.

```csharp
terrain.CellFocused += (_, e) =>
{
    if (e.Cell is { } cell)
    {
        ShowDetails(cell.Day, cell.Hour, e.Value);
    }
};
terrain.FlyTo(new ActivityTerrainCell(Day: 2, Hour: 14));   // Wednesday 14:00
```

### Orbiting and zooming

Drag to turn the view (yaw has no limit, tilt stays between 10° and 84° and springs back after an overdrag), release to let
it glide. Ctrl or ⌘ with the wheel, or a pinch, zooms between 0.7 and 2.8; the plain wheel scrolls the page. `Yaw`, `Pitch`
and `Zoom` report the camera, `IsViewDefault` turns false once it moved, and the "Reset view" button (or `ResetView`,
`ResetViewCommand`) brings it back and raises `ViewReset`.

### Narrow layouts

The layout follows the block's own width. Below 560 the switches stack, the plot is 280 high and turned a little further
(50°), the hour labels shorten to "09", and the summary takes two columns. From 880 the summary moves into a column beside the
plot.

<Demo name="Narrow" />

### Your own data and live updates

Replace `Values` (and `PreviousValues`) whenever new numbers arrive: the bars rise to them in the same wave as a metric switch,
and the summary's numbers roll. Day names come from `DayNames` and `DayAbbreviations`, and `WorkingHours` defines the hours
counted as working time.

<Demo name="LiveData" />

```xml
<arc:ActivityTerrain Title="Support desk"
                     SelectedMetricKey="tickets">
    <arc:ActivityTerrain.WorkingHours>
        <arc:ActivityTerrainWorkingHours FirstDay="0" LastDay="4" FirstHour="8" LastHour="18" />
    </arc:ActivityTerrain.WorkingHours>
    <arc:ActivityTerrainMetric Key="chats" Label="Chats" Unit="chats" />
    <arc:ActivityTerrainMetric Key="tickets" Label="Tickets" Unit="tickets" Tone="Neutral" />
</arc:ActivityTerrain>
```

### Texts

Every visible and announced text is a property with an English default: the legend (`LessText`, `MoreText`), the switches
(`PerspectiveText`, `TopViewText`), `ResetViewText`, the hints (`HintText`, `TouchHintText`, `KeyboardHintText`), the card
(`PreviousWeekText`, `ChangeText`, `ShareFormat`, `HourRangeFormat`), the summary (`SummaryTitleFormat`, `ChangeSuffixText`,
`PeakHoursTitle`, `BusiestDayTitle`, `BusiestDayFormat`) and the announcements. Formats take numbered placeholders; a
metric's own `SummaryTitle` replaces `SummaryTitleFormat` for that metric, and `ValueFormat` (a .NET number format such as
`"N1"`) changes how its values print.

## API reference

<!-- api: ActivityTerrain -->

<!-- api: ActivityTerrainMetric -->

## Accessibility

- The plot is one focusable element. Screen readers hear a one-sentence summary of the week (metric, total, change, busiest
  hour and day) and the keyboard help as its description.
- The week is also available as a table for screen readers: a row per day, named by the day, with a cell per hour ("14:00:
  1,000 visits"), captioned with the metric.
- A polite live region announces the highlighted hour with its value and change, flying in and returning to the week, view
  switches, metric switches and resets.
- The metric and view switches are segmented controls with one tab stop each. The peak hour rows are toggle buttons, named
  with the day, hour and value, pressed while the camera is on their hour. "Reset view" is only in the tab order while shown.
- Keyboard focus shows a 2px ring 2px outside the plot; pointer focus does not.
- Colors are never the only signal: values are in the card, the table and the announcements, and the change carries a sign
  and an arrow.

### Keyboard

| Key | Action |
|---|---|
| ← → ↑ ↓ | The first press highlights the busiest hour; then move by hour (← →) or day (↑ ↓), stopping at the edges |
| Home / End | The first / last hour of the highlighted day |
| Enter / Space | Fly to the highlighted hour |
| Escape | Back to the whole week; pressed again, clear the highlight |
| Shift + ← → | Turn the view by 15° |
| Shift + ↑ ↓ | Tilt the view by 8° (10° to 84°) |
| + / − | Zoom in / out by 1.25 (0.7 to 2.8) |

## Motion

- **Intro.** The first time the plot is on screen the camera swings down from far above (about 3 seconds) while the bars grow
  from the floor, back row and left column first.
- **Metric switch.** Every bar's height and color spring to the new values, each starting a little later from Monday 00:00
  toward Sunday 23:00 (a diagonal wave of about a third of a second); the summary's numbers roll digit by digit and its texts
  cross-fade; the legend changes color over 480ms.
- **Camera.** Fly-in, return, reset and view switches are springs of 1 to 1.4 seconds that keep their speed when interrupted;
  zoom is quicker; a released drag glides and an overdrag springs back inside the tilt limits. While the tilt changes, the fit
  of the terrain to the plot follows on its own spring.
- **Hover.** The bar lifts in about a quarter of a second and the rest of the week fades back; the card fades in (120ms) and
  then follows the bar on a spring as the camera moves.
- **Reduced motion.** No intro and no wave; camera moves, view switches and zooms land at once; drags follow the pointer but do
  not glide; the card only fades. With motion off nothing animates.
- An idle plot draws nothing; frames run only while something moves and only while the plot is on screen.

## Related

- `ActivityHeatmap`: a year of days as a calendar heat map.
- `BarChart`: values per category with exact comparison.
- `MetricCard`: one number with its change, for dashboards with many measures.
