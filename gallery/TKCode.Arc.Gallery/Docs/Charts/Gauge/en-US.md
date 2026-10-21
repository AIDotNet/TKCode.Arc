---
title: Gauge
summary: Show a value against a known range.
---

<Demo name="Thresholds" />

## When to use

`Gauge` answers one question: where does this reading sit inside a range the reader already knows? It draws a 270
degree ring with the percentage in its center, an optional state name from named bands, and a caption under the ring.
It belongs on an overview tile, next to the number it summarizes, so the reader sees the reading, its state and its
scale in one glance.

- **Disk usage on an admin console**: a workspace owner sees that a database volume holds 72 percent of its 500 GB and
  that the state is Filling up. The detail line "360 of 500 GB" gives the absolute amount, and the ring gives the
  proportion.
- **A health score in a risk or fraud dashboard**: a credit score of 640 on a 300 to 850 scale. `Minimum` and `Maximum`
  map the real range onto the ring, so the reader does not have to convert the score to a percentage.
- **Error budget on a reliability page**: an SRE team sees that 97 percent of the month's error budget is spent. A
  danger tone and a label make the state obvious before the reader studies the number.
- **Quota or capacity tiles on a billing page**: a workspace shows the share of its seats in use. Use it for a single
  reading; a plan with a limit the customer consumes over a period is better served by `UsageMeter`.

## When not to use

- **Progress of a task that is running or finishing**: use `Progress`, which describes steps and completion rather than
  a reading against a scale.
- **A plan or quota with a limit that the user is consuming over a period**: use `UsageMeter`, which shows the amount
  used against the plan and its renewal.
- **A value that changes over time**: use `LineChart` or `Sparkline`. A ring shows one moment, so a trend drawn as a
  series of gauges hides its direction.
- **Parts of a whole rather than one value against a range**: use `DonutChart`, which shows how several parts share one
  total.
- **A single value with its change from the last period and a trend**: use `MetricCard`, which places the change and the
  comparison next to the number.

## Usage

`Value` is the reading. `Minimum` and `Maximum` set the range, which is 0 to 100 by default. `Detail` adds a line
under the reading, such as the amount in absolute units.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Gauge Label="Disk usage"
               Value="72"
               Detail="360 of 500 GB">
        <arc:Gauge.Thresholds>
            <arc:GaugeThreshold From="0" Tone="Success" Label="Healthy" />
            <arc:GaugeThreshold From="60" Tone="Warning" Label="Filling up" />
            <arc:GaugeThreshold From="85" Tone="Danger" Label="Critical" />
        </arc:Gauge.Thresholds>
    </arc:Gauge>
</UserControl>
```

`Gauge` derives from Avalonia's `Control` and draws itself, so it has no template parts to restyle and no child
controls to place. It is a read-only display: it takes no focus and defines no keys. Its look comes from
its theme, and its sizes follow the container.

### Anatomy

1. **Ring**: a 270 degree track with a value arc on top. The stroke is 9 px in the 100 by 90 view box, so it scales with
   the ring. The ring starts at the lower left and runs clockwise, and its radius sits around the center of the box.
2. **Dial**: the ring's box is at most 176 px wide (`DialMaxWidth`) and 0.9 times as tall as it is wide. A narrower
   container shrinks it; the gauge never grows past 176 px.
3. **Number**: the percentage in the display font at the 3XL type size, with a `%` at half that size. Its
   value is `Percent`, the reading rounded to a whole percent of the range.
4. **State line** (`Thresholds`): the name of the band the value is in, in extra-small medium text in the band's tone,
   inside the ring under the number after a 4 px gap. It is hidden when no thresholds exist. Its box is at least 100 px
   wide so the text does not shift the number.
5. **Label** (`Label`): the caption under the ring, in small medium text, 10 px below the dial (`CaptionGap`). It is also
   the accessible name.
6. **Detail** (`Detail`): extra-small regular text in the muted color, under the label. It is hidden when empty.

The ring colors follow the tone: `Accent` uses the accent color with the strong accent for the state text, and
`Success`, `Warning` and `Danger` use their own tokens. A band's tone replaces the ring's `Tone` while the value is in
that band.

### Choosing thresholds

A threshold starts at `From` and holds until the next band begins. The highest band the value reaches sets the color
and names the state. Bands are sorted by `From` when they change, so the order in the markup does not matter, though
writing them in ascending order keeps the markup readable.

Choose bands that describe what the reader should do, not a decorative scale. Three bands are usually enough: a normal
state, one warning state, and one state that needs action. Use the same `From` values and labels on every tile that
measures the same thing, so a Critical disk on one page means the same as a Critical disk on another. Keep the labels
to one or two words, because the state line sits inside the ring under the number.

```csharp
disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "Healthy"));
disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "Filling up"));
disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "Critical"));
```

Bands use the gauge's value units, not percentages of the range. For a score from 300 to 850, a band at `From="670"`
means a score of 670, not 670 percent. Without thresholds the ring uses `Tone` only, and the reader gets no state name, so
add the bands whenever the state matters.

### Binding to a view model

The gauge displays a value; it does not write one back. `Value`, `Minimum`, `Maximum`, `Label` and `Detail` bind one
way, so the view model owns the reading and the gauge reflects every change. With CommunityToolkit.Mvvm:

```csharp
public partial class StorageViewModel : ObservableObject
{
    [ObservableProperty]
    private double _usedGigabytes;

    [ObservableProperty]
    private double _capacityGigabytes = 500;

    [ObservableProperty]
    private double _usedPercent;

    partial void OnUsedGigabytesChanged(double value)
    {
        UsedPercent = Math.Round(value / CapacityGigabytes * 100);
        DetailText = $"{value:0} of {CapacityGigabytes:0} GB";
    }

    [ObservableProperty]
    private string _detailText = string.Empty;
}
```

```xml
<arc:Gauge Label="Disk usage"
           Value="{Binding UsedPercent}"
           Detail="{Binding DetailText}">
    <arc:Gauge.Thresholds>
        <arc:GaugeThreshold From="0" Tone="Success" Label="Healthy" />
        <arc:GaugeThreshold From="60" Tone="Warning" Label="Filling up" />
        <arc:GaugeThreshold From="85" Tone="Danger" Label="Critical" />
    </arc:Gauge.Thresholds>
</arc:Gauge>
```

- Update `Value` and `Detail` together in the view model, so the number and its detail never disagree on screen. The
  page above derives both from one change.
- The bands are a list, not a binding target. Set them once in markup or in code at startup, and change the
  `From` values only when the policy changes.
- A value outside `Minimum` and `Maximum` is clamped on screen. Validate the reading in the data layer, because the
  gauge will show the clamped value without an error.

### Custom range

`Minimum` and `Maximum` map any range onto the ring. The number reads as a percentage of that range, so a score of 575
on a 300 to 850 scale shows 50 percent. Use the real range when the reader thinks in the real unit, and state the
range in the detail line so the number has a reference.

<Demo name="CustomRange" />

### Tone without thresholds

Without thresholds the ring uses `Tone`: `Accent`, `Success`, `Warning` or `Danger`. Use a fixed tone for a reading whose
state is always the same kind, such as an uptime figure that is always good news, and use thresholds when the state
changes with the value.

<Demo name="Tones" />

### Example: storage and health tiles for an admin console

An Acme Analytics workspace owner opens the admin console and sees three readings in one row: disk usage for the
analytics database, the health score of the ingestion pipeline, and the share of the monthly error budget that is spent.
Each tile has a label that names the measure and a detail line with the absolute amount. The first two tiles also name
their state from their own bands. The health score maps its real range from 300 to 850, so the ring shows 62 percent of the range while the detail
line keeps the score itself, 640. The error budget has no
bands: its tone is fixed to danger because it is already past the line that the team agreed to act on.

The example is 720 px wide, so the three tiles sit side by side at the default ring size.

![Three gauges for disk usage, a health score and the error budget, each with a state name](images/StorageHealth.png)

```xml
<StackPanel Width="720" Spacing="20">
    <TextBlock Text="Platform health, Acme Analytics"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Grid ColumnDefinitions="*,*,*">
        <arc:Gauge Grid.Column="0"
                   Label="Disk usage"
                   Value="72"
                   Detail="360 of 500 GB">
            <arc:Gauge.Thresholds>
                <arc:GaugeThreshold From="0" Tone="Success" Label="Healthy" />
                <arc:GaugeThreshold From="60" Tone="Warning" Label="Filling up" />
                <arc:GaugeThreshold From="85" Tone="Danger" Label="Critical" />
            </arc:Gauge.Thresholds>
        </arc:Gauge>
        <arc:Gauge Grid.Column="1"
                   Label="Ingestion health"
                   Value="640"
                   Minimum="300"
                   Maximum="850"
                   Detail="640 of 850">
            <arc:Gauge.Thresholds>
                <arc:GaugeThreshold From="300" Tone="Danger" Label="Degraded" />
                <arc:GaugeThreshold From="600" Tone="Warning" Label="Watch" />
                <arc:GaugeThreshold From="720" Tone="Success" Label="Healthy" />
            </arc:Gauge.Thresholds>
        </arc:Gauge>
        <arc:Gauge Grid.Column="2"
                   Label="Error budget used"
                   Value="97"
                   Tone="Danger"
                   Detail="97% of the monthly budget spent" />
    </Grid>
</StackPanel>
```

### Content guidelines

The label names the measure and the detail names the amount. A reader should be able to read the tile without the
surrounding page: "Disk usage" with "360 of 500 GB" says what it is, while "Storage" says nothing about the state.

| Instead of | Write |
|---|---|
| Storage | Disk usage |
| 360/500 | 360 of 500 GB |
| Score: 640 | Health score |
| Almost out of space | Filling up |
| Very bad, act now | Critical |

Band labels are state names of one or two words in sentence case, such as Healthy, Filling up, Watch and Critical. Do not
use exclamation marks or a sentence in a band label, because the label is announced with the number and shares a short
line with it. Put the recommended action in the surrounding page, not in the gauge.

### Best practices

**Do**

- Set `Minimum` and `Maximum` to the real range of the measure, and show the absolute amount in `Detail`, so the reader
  can judge the percentage.
- Name a state with `Thresholds` whenever the state matters to the reader. The state name is read with the number, so a
  color is never the only signal.
- Keep the same bands and labels for the same measure on every page that shows it.
- Update `Value` and `Detail` together, from one source, so the number and its explanation never disagree.
- Place gauges in a row of equal columns, so the readings compare at a glance.

**Avoid**

- A gauge for a value that changes over time or that the reader must watch as a trend. Use `LineChart` or `Sparkline`.
- A gauge as the only display of a number the reader must act on. The gauge is a summary; keep the exact figure in the
  detail or in the page's table.
- Many bands with near-identical labels. A reader cannot tell "Watch" from "Attention" at a glance, so use three or four
  bands at most.
- A custom range on a reading that people think of as a percentage. The ring then shows a number the reader does not
  use.
- Thresholds whose `From` values are in the wrong units, such as percentages on a 300 to 850 scale. The band then never
  matches the reading.

### Common questions

**Why does the state name change a moment after the value?** The ring and the number move on a spring, and the band is
chosen by the number on screen. The state switches when the count crosses the band boundary, so the name and the
number always agree. At reduced or off motion the change is immediate.

**What does the gauge show for a value outside the range?** It clamps the value to `Minimum` and `Maximum` for the ring
and the number. The `Percent` property is clamped too, so a value of 1000 on a 0 to 100 gauge reads 100 percent.

**Can I read the state in code?** Yes. `CurrentThreshold` returns the band for the value, or null when there are no
bands or the value is below the first band. `Percent` gives the whole percentage the ring shows.

**Is the gauge keyboard accessible?** The gauge takes no focus and defines no keys, so it is read as part of the page.
Its value is exposed as a read-only progress bar with a range, and its help text is the percentage with the state, such
as "72%, Filling up".

**Why is there no state when I set `Tone` alone?** `Tone` sets the ring color only. The state line and the state name
come from `Thresholds`, so add at least one band to show a name.

## API reference

<!-- api: Gauge -->

<!-- api: GaugeThreshold -->

### Companion type: GaugeTone

| Member | Ring and state color |
|---|---|
| `Accent` | The accent color; the state text uses the strong accent (the default) |
| `Success` | The success color |
| `Warning` | The warning color |
| `Danger` | The danger color |

## Accessibility

- `Label` names the gauge, and the value is exposed as a read-only range with the minimum and the maximum.
- The help text is the value with the current band label, such as "72%, Filling up", so the state is read with the
  number. Without thresholds it is the percentage alone.
- The gauge is a live region set to polite: a new reading is announced without moving focus.
- States are named in text by the threshold labels. Color is never the only sign of a state.
- The ring and the animated reading are hidden from assistive technology; the screen reader gets the name, the range and
  the help text instead.

### Keyboard

The gauge takes no focus and defines no keys. Read it with the page's normal reading order, or with a screen reader's
review commands.

## Motion

- Once, when the gauge is half in view, the ring fills to its value on a smooth spring without overshoot.
- A new value moves the ring to its new length, and the percentage counts to the new number at the same time.
- When the count crosses a threshold, the color and the band label switch at that moment, following the count.
- Reduced motion puts the ring and the number at the target value at once. Off behaves the same as Reduced.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view (half in view) | The ring fills once to its value, smooth spring, no overshoot | The ring and number are at the value at once | The ring and number are at the value at once |
| New `Value` | The ring springs to the new length and the number counts | Changes at once | Changes at once |
| Count crosses a band | The color and the state name switch with the count | Switch at once | Switch at once |
| `Tone` changes | The ring color crossfades | Changes at once | Changes at once |

## Related

- `Progress` for the completion of a task.
- `UsageMeter` for a quota that the user consumes over a period.
- `DonutChart` for parts of a whole rather than one value against a range.
- `MetricCard` for a single value with its change and a trend.

| Component | Choose it when |
|---|---|
| `Progress` | A task is running or finishing and the reader waits for it |
| `UsageMeter` | A plan or quota is consumed over a billing period |
| `DonutChart` | Several parts make up one total |
| `Sparkline` / `LineChart` | The value's movement over time is the point |
| `MetricCard` | One number needs its change and a trend next to it |
