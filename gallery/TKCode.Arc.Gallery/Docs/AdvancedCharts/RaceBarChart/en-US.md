---
title: Race bar chart
summary: A ranking that changes over time: bars slide past each other as their values climb, and a timeline plays, pauses or scrubs the periods.
---

<Demo name="TopRegions" />

## When to use

`RaceBarChart` tells the story of who led when. Each period is a ranking, and the chart moves from one ranking to the next
so the reader sees the moment one competitor passes another, which a static table of totals hides. It works best as a
presentation: a report's opening, a yearly review, or a page where people press play and watch.

- **Cumulative totals by region** in a sales or adoption review, where the question is which region pulled ahead and when.
- **Audience by show or channel** on a media dashboard, where weekly numbers rise and fall and the order keeps changing.
- **Market share by product** in a yearly review, with one highlighted product the page is about.
- **Contributions by team** in an engineering retrospective, where the groups give the legend.
- **A public leaderboard** with periods the visitor can scrub through.

## When not to use

- **One ranking at one moment**: use `BarChart`, which compares values without asking anyone to wait.
- **Exact values to look up**: use `Table` or `DataGrid`. The bars move, and values are readable only when they stop.
- **The trend of each competitor**: use `LineChart`, where every period stays on screen together.
- **Parts of a whole over time**: use `Streamgraph`, which keeps the total visible.
- **More than about twenty periods with no story**: a long race becomes a screen saver. Summarize the periods, or use
  `LineChart`.

## Usage

Give the chart its `Items`, one per bar, and its `Frames`, one per period in order. Values in a frame are matched to items
by id, and an id a frame does not mention counts as zero. The chart ranks the items in every frame, shows the top
`BarCount`, and draws the name and value on each bar.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RaceBarChart Label="Trail sign-ups by town" IsAutoPlay="False">
        <arc:RaceBarChart.Items>
            <arc:RaceBarItem Id="elmford" Label="Elmford" />
            <arc:RaceBarItem Id="stonely" Label="Stonely" />
        </arc:RaceBarChart.Items>
        <arc:RaceBarChart.Frames>
            <arc:RaceBarFrame Period="Spring">
                <arc:RaceBarValue Id="elmford" Value="120" />
                <arc:RaceBarValue Id="stonely" Value="90" />
            </arc:RaceBarFrame>
            <arc:RaceBarFrame Period="Summer">
                <arc:RaceBarValue Id="elmford" Value="180" />
                <arc:RaceBarValue Id="stonely" Value="240" />
            </arc:RaceBarFrame>
        </arc:RaceBarChart.Frames>
    </arc:RaceBarChart>
</UserControl>
```

In code, a frame can be built from a dictionary, which is the usual shape when the data comes from a query.

```csharp
chart.Items = towns.Select(t => new RaceBarItem(t.Id, t.Name)).ToList();
chart.Frames = seasons.Select(s => new RaceBarFrame(s.Name, s.SignUpsByTown)).ToList();
```

`RaceBarChart` derives from `TemplatedControl`. One self-drawn surface paints the bars, the axis and the legend, and the
large period label, the play button and the timeline sit around it. `Label` names the chart for assistive technology.

### Anatomy

1. **Legend**: appears when `Groups` is set, above the axis.
2. **Value axis**: gridlines and labels above the bars. The tick count follows the width, about one tick per 84 px.
3. **Bars**: 28 px high with 6 px between them, each with its name and its value.
4. **Period label**: large text at the lower right, behind the bars.
5. **Play button**: 36 px. It shows play, pause, or replay on the last period.
6. **Timeline**: a slider measured in periods with a 36 px touch target, between the names of the first and last period.

### Rising and falling values

Frames do not have to be cumulative. With raw values each period stands alone, so a competitor can fall down the ranking
as well as climb it. `IsLooping` starts over after the last period, and `BarCount` decides how many bars are on screen:
items below that rank slide out, and items that reach it rise and fade in.

<Demo name="RisingAndFalling" />

### Controlling the timeline

`FrameIndex` is two-way and every change glides through the values in between, so your own buttons or a slider can drive
the chart. Set `IsAutoPlay="False"` when the page, not the chart, decides when to start. `IsPlaying` is two-way as well,
and `Play`, `Pause` and `Replay` do the same from code. `FrameChanged` reports each period with its leader, and
`PlayingChanged` reports when playback starts or stops.

<Demo name="ControlledTimeline" />

### Groups and highlight

`Groups` gives the bars a meaning for their color. The first four groups take `Series1` to `Series4` and appear in the
legend. Later groups, and items without a group, draw a neutral bar. Without groups every bar uses `Series1`.
`HighlightId` emphasises the name and value of the item the page is about, without changing its color.

```xml
<arc:RaceBarChart Label="Installs by district" HighlightId="larkfield" BarCount="10" />
```

### Formats and timing

`ValueFormatter` formats the bars and the tooltip. The default is compact numbers such as 1.2K. `TickFormatter` formats
the axis and falls back to `ValueFormatter`. `FrameDuration` sets how long one period takes while playing.

```csharp
chart.ValueFormatter = value => value.ToString("N0");
chart.TickFormatter = value => ArcFormatting.Compact(value);
chart.FrameDuration = TimeSpan.FromSeconds(1);
```

### Reading a bar

Hover a bar, press it with a finger, or use the up and down arrow keys. The other bars step back and a tooltip shows the
name, the period, the value and the rank. The reading follows the item as it moves, and it clears if the item falls out
of the bars on screen.

### Data and performance

Equal values keep the order of `Items`, so ties never flicker and scrubbing back gives the same order. Values that are not
finite count as zero, a negative value draws an empty bar, and a second item with the same id is dropped. Changes to an
observable `Items`, `Frames` or `Groups` list are followed; changes inside a frame are not, so assign a new frame instead.

One loop drives everything, stops when the chart is paused and settled, and pauses off screen. Text is laid out once and
only rebuilt when the displayed digits change, so several hundred items stay smooth; only the bars on screen are drawn.

### Content guidelines

- **Label**: the measure and scope, such as "Rooftop solar installations by district, cumulative".
- **Period**: short and even in length, such as "Mar" or "Q2 2026". It is drawn large.
- **Item label**: a short name. Long names are cut with an ellipsis.

| Instead of | Write |
|---|---|
| Data | Monthly riders by city |
| Period 3 | Mar |
| Group A | North coast |

### Best practices

**Do**

- Keep `BarCount` near the number of competitors people can follow, usually 5 to 12.
- Use `HighlightId` when the page is about one competitor.
- Start with `IsAutoPlay="False"` when the chart is one of many things on the page.

**Avoid**

- Using color for anything but `Groups`. The hue has to mean something.
- Very short `FrameDuration` values. Below about 500 ms nobody can read the values.
- Relying on the animation to state the finding. Say it in words beside the chart.

### Common questions

**Why does the chart not start by itself?** It starts the first time it scrolls into view, and only with full motion.
Under reduced motion it waits for the play button.

**Why did the chart stop between two periods?** It does not: pausing finishes the move to the next period, and stepping or
scrubbing lands on a whole period.

## API reference

<!-- api: RaceBarChart -->

## Accessibility

- The plot is a focusable group named by `Label`. Its help text is the period and the leader, or the bar being read.
- The timeline is a slider with a value text such as "Mar, period 3 of 12", and the play button names its action.
- Stepping announces the period and the leader. Reading a bar announces its name, rank and value. Nothing is announced
  while the chart plays, so a screen reader is not flooded; the state is announced again on pause.
- Every bar shows its name and value, so hue is never the only cue.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the plot, the play button and the timeline |
| `Space` / `Enter` | Play or pause. On the last period, replay from the start |
| `Left` / `Right` | Step one period, gliding. Stepping pauses playback |
| `Up` / `Down` | Move a reading through the bars by rank and show the tooltip |
| `Home` / `End` | Go to the first or the last period |
| `Escape` | Clear the reading |

## Motion

- Values interpolate between periods, and each row moves on its own spring, so an overtake reads as one continuous slide.
  A row that gets a new target keeps its velocity.
- Bars that enter the top set rise from below and fade in. Bars that leave slide out and fade.
- The timeline's thumb follows on a critically damped spring. Axis ticks glide with the scale and crossfade when the step
  changes. The period label rolls with a brief blur.
- Playback pauses off screen and carries on when the chart returns.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Starts playing when `IsAutoPlay` is set | Waits for the play button | Same as Reduced |
| Period changes | Values glide, rows slide on springs | Placed immediately, no interpolation | Same as Reduced |
| Playing | Continuous | One step per `FrameDuration` | Same as Reduced |
| Period label | Rolls with a brief blur | Short crossfade | Replaced at once |

## Responsive behavior

- `RaceBarChart` measures its own width. Below 400 px the names move inside the bars, and past the end of a bar when the
  bar is too short for its name; the value follows the name.
- Below 400 px the period label shrinks one step, and the first and last period names beside the timeline are hidden.
- The tick count adapts to the width of the plot, from 2 to 8 ticks.
- The timeline's whole 36 px band takes a finger, so scrubbing works on a phone. A press on a bar reads it.

## Related

- `BarChart` for one ranking at one moment.
- `LineChart` for following each competitor over time.
- `Streamgraph` for how the layers of a total change over time.

| Component | Choose it when |
|---|---|
| `BarChart` | There is one period to compare |
| `LineChart` | Every period should stay visible at once |
| `Streamgraph` | The total matters along with its parts |
| `DataGrid` | People need to look up exact values |
