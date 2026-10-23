---
title: Data flow
summary: Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.
---

<Demo name="EventPipeline" />

## When to use

`DataFlow` explains how events move through a data platform: from the products and services that emit them, through
an event stream and a batch transform, into a warehouse and the live views built on top of it. It is an illustration,
not a monitor. It sits on a documentation page, an onboarding screen or an empty state, where the reader needs to
understand the pipeline before any real data exists.

- **Onboarding for an analytics product**: a first-run screen that shows where events come from (a web app, a mobile
  app, a billing webhook) before the customer connects their first source. The diagram answers "where does my data
  go" without a setup wizard.
- **Data platform documentation**: a page that describes an ingestion architecture, such as a Kafka topic that feeds a
  validation job and a lakehouse table. The caption and the labels name the real components of the platform.
- **Empty states in an operations console**: a "no events yet" panel on a pipeline page. The animation tells the user
  that the pipeline is wired up and waiting for traffic, which a blank table does not.
- **Explaining a filter step**: a privacy or quality rule that drops part of the traffic before it is stored. The
  red packets that fade out at the transform node show what the rule removes, without a table of rules.
- **Release notes and changelog pages**: a short visual for a feature that adds a stage to the pipeline, such as a new
  enrichment step, placed next to the text that describes it.

## When not to use

- **Plotting real volumes over time**: use `LineChart` or `BarChart` from the Charts category. The counters in
  `DataFlow` tell a story; they are not measurements, and the illustration does not accept your metrics.
- **A fixed sequence of steps with a current position**: use `Stepper` for a setup wizard or an ingestion job that the
  user moves through with Next and Back. `DataFlow` loops on its own and has no way to move between steps.
- **A live status of your pipeline**: use `Alert` or `Badge` with the actual state of each job, and a table for lag
  and failures. A looping diagram cannot report that a job is failing.
- **A page with many animated illustrations**: each one redraws its canvas on every frame while it plays, so keep only
  one or two running on a screen and use the reduced-motion poster for the rest.
- **A process with branches decided by users or by data**: use `Timeline` for a history of what happened, or a flow
  diagram in the documentation. `DataFlow` shows one fixed route with one filter branch.

## Usage

Playback starts when the illustration is loaded. The caption under the diagram follows the story, and the play button
pauses it. The control needs no data to run: it ships with a default story about a web app, a mobile app and webhooks
feeding a warehouse and a dashboard.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DataFlow Label="Event pipeline" />
</UserControl>
```

`DataFlow` derives from `Illustration`, which derives from Avalonia's `TemplatedControl`. It is therefore a normal
control: it has a `Label` that names it for assistive technology, it takes focus, it can be styled through its
theme and it can be placed in any layout. The shared illustration base supplies the scene clock, the looping
script, the caption, the play button and the reduced-motion poster, so the control itself only describes the
diagram and its numbers.

### Anatomy

1. **Sources** (left column, `Sources`): one to four cards of 150 by 52 scene units with a rounded 12 px corner, an
   icon, a title and a detail line. The defaults are Web app (page views), Mobile (taps and sessions) and Webhooks
   (partner events). Sources are spread evenly down the column; the spacing shrinks as the count grows.
2. **Event stream** (`Labels.Stream`, `Labels.StreamDetail`): the queue that receives every event. A meter under the
   card fills as events arrive and empties when the transform takes a batch. The meter is capped at 24 events.
3. **Throughput readout**: the events per second under the stream card, smoothed over half-second samples and
   formatted with `Labels.EventsPerSecond`.
4. **Transform** (`Labels.Transform`, `Labels.TransformDetail`): the batch step, with a busy ring while a batch is in
   flight. Filtered events are removed here.
5. **Warehouse** (`Labels.Warehouse`, `Labels.WarehouseDetail`): the upper branch on the right. Its readout under the
   card counts the rows stored, formatted with `Labels.Rows`.
6. **Dashboards** (`Labels.Realtime`, `Labels.RealtimeDetail`): the lower branch on the right, with a readout that
   counts updates pushed to the live views, formatted with `Labels.Updates`.
7. **Caption line** (`IsCaptionVisible`): one line of secondary text under the diagram, in the small type size and
   the secondary text color. It changes with each step.
8. **Play button** (`IsPlaybackButtonVisible`): a round button at the bottom right of the frame, hidden in the still
   poster.
9. **Frame**: a surface card with a 1 px border in the border color and the surface corner radius, on a dotted
   backdrop. The padding is 20 px on the sides and top and 14 px at the bottom. The scene scales to the width the
   control is given, so the diagram keeps its proportions in a narrow column.

Packets are drawn in the accent color. Packets on a filtered path turn to the danger color and fade out, and the
routes that carry events to the live views use the success color.

### Throughput and filtering

`EventsPerSecond` sets how fast the sources emit events. It takes values from 1 to 60 and defaults to 18; values
outside the range are clamped. `FilterRatio` sets the share of events the transform removes. It takes values from 0 to
0.5 and defaults to 0.14, so about one event in seven is filtered by default.

```xml
<arc:DataFlow EventsPerSecond="8" FilterRatio="0.3" />
```

The transform takes one batch of up to eight events each second, so a rate above eight events per second makes the
meter climb toward its capacity. The illustration explains the shape of the pipeline; it does not simulate backpressure,
lag or dropped events, and it should not be used to estimate capacity.

### Sources and labels

`Sources` takes one to four `DataFlowSource` values. Each value has a `Title` and an optional `Detail` line. A list
with no items falls back to the default sources, and a list longer than four uses its first four entries. The
`Labels` property takes a `DataFlowLabels` object with every visible word of the diagram: the node titles and details,
the readout formats and the step captions. Set only the properties you need; the others keep their English defaults.

<Demo name="OwnSources" />

Changing `Sources` or `Labels` rebuilds the diagram, so set both when the page loads, not on every data refresh.

### Playback and speed

`IsPlaying` plays and pauses the story. It is two way: the control writes the value when the person presses the play
button or Space, and an app can read it or bind it. `Speed` scales the scene clock and takes values from 0.25 to 4.
`Play()`, `Pause()` and `Restart()` do the same from code. `Restart()` resets the counters and returns to the first
step.

```xml
<arc:DataFlow Speed="2" IsPlaying="{Binding IsPipelineRunning}" />
```

```csharp
flow.Speed = 0.5;
flow.Restart();
```

The scene clock pauses while the control is off-screen, hidden or detached, so an illustration on a scrolled-away
tab costs nothing until it is visible again.

### Following the story

`Step` and `StepCount` describe the position in the loop. The story has three steps, and the caption changes with
each one:

| Step | Caption | What is highlighted |
|---|---|---|
| 0 | The ingest caption with the source count | The connections from the sources to the stream |
| 1 | The transform caption: validated and enriched, bad events filtered out | The connection into the transform |
| 2 | The store caption: clean events land in the warehouse and update live dashboards | The connections to the warehouse and to the dashboards |

`StepChanged` gives the new step and its caption. `RowsStored` and `EventsFiltered` count what has reached the
warehouse and what the transform removed since the story started. Both are read-only; the illustration keeps them
for its own story.

```csharp
flow.StepChanged += (_, e) => status.Text = $"Step {e.Step + 1} of {flow.StepCount}: {e.Caption}";
```

### Reduced motion

Under reduced motion (or Off) the illustration is a still poster of its steady state. Every connection is highlighted,
one packet rests halfway along each route to show the direction of travel, and the caption summarizes the whole story.
The play button is hidden because there is nothing to play. Use the poster on pages where motion is not wanted, and
in print or static exports.

<Demo name="ReducedMotion" />

### Driving it from data

`DataFlow` does not read a stream of events; it draws the story it was given. In an application, bind the sources
and the labels from a view model when they come from configuration, and keep the playback state with them. The
example below uses CommunityToolkit.Mvvm and shows the pipeline of an analytics product that is still being set up.

```csharp
public partial class PipelineSetupViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isPipelineRunning = true;

    [ObservableProperty]
    private double _speed = 1;

    public IReadOnlyList<DataFlowSource> Sources { get; } =
    [
        new("Checkout", "orders"),
        new("Mobile app", "sessions"),
        new("Billing webhooks", "invoices")
    ];

    public DataFlowLabels Labels { get; } = new()
    {
        Warehouse = "Lakehouse",
        WarehouseDetail = "parquet",
        Realtime = "Alerts",
        RealtimeDetail = "on-call"
    };
}
```

```xml
<arc:DataFlow Sources="{Binding Sources}"
              Labels="{Binding Labels}"
              IsPlaying="{Binding IsPipelineRunning}"
              Speed="{Binding Speed}" />
```

Bind `Sources` and `Labels` once for each configuration. A new list or labels object rebuilds the diagram, so do not
replace them on a timer. Use `IsPlaying` to tie the story to the page state, for example to pause it while a
settings dialog is open.

### Example: data platform overview

A settings page for an analytics product. The illustration sits above a short description of the connected sources,
so the reader sees the same three sources that the text names. The warehouse is a lakehouse table in Parquet, and
the second branch feeds an alerting view for the on-call team. The labels use the platform's own names, and the
detail lines say what each stage does rather than repeating the title.

![A data pipeline with three sources, a lakehouse store and an alerting branch](images/DataPipeline.png)

```xml
<StackPanel Width="760" Spacing="16">
    <TextBlock Text="Event pipeline"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Web, mobile and webhook events are validated every second and stored in the lakehouse."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:DataFlow Label="Event pipeline for Acme Analytics">
        <arc:DataFlow.Labels>
            <arc:DataFlowLabels Stream="Event queue"
                                StreamDetail="buffered"
                                Transform="Validate"
                                TransformDetail="schema · enrich"
                                Warehouse="Lakehouse"
                                WarehouseDetail="parquet"
                                Realtime="Alerts"
                                RealtimeDetail="on-call" />
        </arc:DataFlow.Labels>
    </arc:DataFlow>
</StackPanel>
```

The example keeps the default sources, which is why the caption above names web, mobile and webhook events. To show
the platform's own sources, assign `Sources` in code as shown in the view model above. The screenshot is the still
steady-state frame, so its readouts (12,480 rows, 3,120 updates) are the illustration's own poster values, not measurements.

### Content guidelines

Write the node titles as the nouns of your architecture (Event queue, Lakehouse, Alerts), and keep the detail lines
short, lowercase and specific (schema · enrich, parquet, on-call). Name real sources, not types: "Checkout orders"
tells the reader more than "Source 1". Write the captions and the labels in the same tense as the rest of the page.

| Instead of | Write |
|---|---|
| Data Source 1 | Checkout |
| Processing | Validate |
| Database | Lakehouse |
| Real-time | Alerts |
| Events are processed in our amazing pipeline | Events are validated every second and stored in the lakehouse |
| 18 events/s throughput achieved | 18 events/s |
| Your data is flowing | Waiting for the first events |

Keep the labels at or under about 16 characters so the node cards keep one line; a longer title wraps and the card
looks crowded. Do not write a sentence into a detail line. Use `Summary` for the description of the whole story and
keep it to one sentence.

### Best practices

**Do**

- Name the real components of your platform in `Labels`, because readers compare the diagram with the architecture
  page they came from. A generic "Processing" node teaches nothing.
- Use two or three sources. The default spacing is widest at two and three, and four cards leave little room for the
  detail lines at a narrow width.
- Keep the story at its default rate on documentation pages. A rate of 18 events per second reads as activity without
  drawing attention away from the text.
- Pause the story with `IsPlaying` when it is off the page the reader is focused on, and let the poster frame replace
  it for users who turn off motion.

**Avoid**

- Using the counters as metrics. `RowsStored` and the readouts count the story the illustration tells, so a dashboard
  that shows them as real volumes would report numbers that do not come from your system.
- Placing several playing illustrations on one screen. Each one redraws its canvas on every frame, so the cost adds up
  and the screen competes for attention.
- Changing `Sources` or `Labels` on a timer or on every data refresh. Each change throws the diagram away and builds
  it again, so the story never gets to play out.
- Writing a sentence into a label. The cards are small, and long text wraps and crowds the frame.
- Relying on the diagram for status. It shows the shape of a pipeline, not whether one job is healthy, so report
  failures with `Alert` or a table.

### Common questions

**How do I rename the three steps in the caption?** Set the `Labels` properties `TransformStep` and `StoreStep`, and
`Summary` for the whole story. The first step's caption is built from `Ingest`, which takes the number of sources, so
change that function if the wording should differ.

**Why does the story always show the same numbers?** The illustration uses a fixed seed, so every run plays the same
way. The counters start at the same values on each restart, and the sources emit at intervals that are jittered but
repeatable. This keeps documentation screenshots and tests stable.

**Can I feed the readouts with my own counts?** No. `RowsStored` and `EventsFiltered` are read-only, and the readout
values are kept by the illustration. To show real volumes, use `LineChart` or `AnimatedCounter` with the numbers from
your metrics store.

**How do I show a different number of sources without changing the code?** Bind `Sources` to a collection from
configuration, as shown in the view model above. Values past the fourth are ignored, and an empty list brings back the
three default sources.

**Does the event pool grow during a long run?** No. Packets come from a fixed pool that is reused, so a story that
runs for hours keeps the same number of packet objects.

## API reference

<!-- api: DataFlow -->

<!-- api: DataFlowLabels -->

Sources use the `DataFlowSource` record.

| Property | Type | Meaning |
|---|---|---|
| `Title` | `string` | The name shown on the source card, such as "Web app" |
| `Detail` | `string?` | The optional second line on the card, such as "page views"; null shows only the title |

## Accessibility

- `Label` is the accessible name of the illustration. Name the pipeline in it, for example "Event pipeline for Acme
  Analytics", because the diagram itself has no text a reader can navigate.
- The illustration takes focus and shows a 2px focus ring around the frame when it is focused.
- The current caption is exposed as the help text, so a screen reader hears the step's description when the control
  is focused.
- Caption changes are not announced. The story loops, and announcing each step would repeat it endlessly.
- The labels and readouts are the text the diagram shows. Keep them meaningful on their own, because a reader who
  cannot see the animation still gets the node titles and the caption.
- Under reduced motion the poster frame shows the steady state with the summary caption, which gives the same
  information without any movement.

### Keyboard

The illustration takes focus with Tab. Once it has focus, these keys control the story:

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the illustration |
| `Space` or `Enter` | Play or pause the story |
| `R` | Start the story over from its first step |

## Motion

- Events travel along the connections as small packets. The stream's meter fills as events arrive and empties as
  batches leave, and the readouts count up.
- Each step lights the connections that belong to it, and the other connections fade back.
- Filtered packets turn red and fade out shortly after they leave the transform.
- The play button and the caption change with a short transition, and the step's nodes emphasize as they become active.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Events travel along the connections | Animated packets on every route | Still poster, one packet resting on each route | Still poster |
| The current step's connections light up | Fade in over 0.4 s | Shown as the steady state (all lit) | Shown as the steady state |
| Filtered events | Turn red and fade out near the transform | Not shown | Not shown |
| Readouts (events/s, rows, updates) | Count up during the story | Show the poster values | Show the poster values |
| Play button | Visible; toggles playback | Hidden | Hidden |

## Related

- `PaymentFlow` for a single request that is approved or declined, with the same frame and playback controls.
- `MultiRegionFailover` for traffic that moves between regions when one fails.
- `VectorSearch` for a question answered from its nearest documents.

| Component | Choose it when |
|---|---|
| `LineChart` / `BarChart` | You plot real measurements over time |
| `Stepper` | The user moves through a fixed sequence with Next and Back |
| `Timeline` | You show a history of events in order, with their times |
| `Alert` | You state the status of a real pipeline or job in text |
| `PaymentFlow` | The story is one request that is approved or declined |
| `VectorSearch` | The story is a question answered by its nearest documents |
