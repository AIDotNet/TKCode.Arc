---
title: Data flow
summary: Events stream from their sources through a queue and a batch transform into a warehouse and live dashboards.
---

<Demo name="EventPipeline" />

## When to use

- Explaining how events move from producers to storage and dashboards on a landing page or in product docs.
- Onboarding and empty states that need to show a pipeline without real data.
- Showing how a filter step removes part of the traffic before it is stored.

## When not to use

- Plotting real numbers over time: use `LineChart` or another chart from the Charts category.
- A fixed sequence of steps with a current position: use `Stepper`.
- A page with many animated illustrations: each one redraws its canvas every frame, so keep only a few playing at
  once.

## Usage

Playback starts when the illustration is loaded. The caption under the diagram follows the story, and the play
button pauses it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataFlow Label="Event pipeline" />
</UserControl>
```

### Throughput and filtering

`EventsPerSecond` sets how fast the sources emit events (1 to 60, default 18). `FilterRatio` sets the share of
events the transform removes (0 to 0.5, default 0.14). Filtered events fade out at the transform node.

```xml
<arc:DataFlow EventsPerSecond="8" FilterRatio="0.3" />
```

### Sources and labels

`Sources` takes one to four `DataFlowSource` values (a title and an optional detail line). `Labels` takes a
`DataFlowLabels` object with every visible word of the diagram.

<Demo name="OwnSources" />

### Playback and speed

`IsPlaying` plays and pauses the story (two way), and `Speed` scales the scene clock (0.25 to 4). `Play()`,
`Pause()` and `Restart()` do the same from code.

```xml
<arc:DataFlow Speed="2" IsPlaying="{Binding IsPipelineRunning}" />
```

```csharp
flow.Speed = 0.5;
flow.Restart();
```

### Following the story

`Step` and `StepCount` describe the position in the loop (three steps). `StepChanged` gives the new step and its
caption, and `RowsStored` and `EventsFiltered` count what has reached the warehouse and what was removed.

```csharp
flow.StepChanged += (_, e) => status.Text = $"Step {e.Step + 1} of {flow.StepCount}: {e.Caption}";
```

### Reduced motion

Under reduced motion (or Off) the illustration is a still poster of its steady state, and the caption sums up the story.

<Demo name="ReducedMotion" />

## API reference

<!-- api: DataFlow -->

<!-- api: DataFlowLabels -->

## Accessibility

- `Label` is the accessible name. The current caption is exposed as the help text.
- The illustration is focusable and has a 2px focus ring.
- Space or Enter plays and pauses the story, and R starts it over.
- Caption changes are not announced, because a looping story would repeat them without end.

## Motion

- Events travel along the edges as small packets, the buffer fills in the event stream and the readouts count up.
- Each step lights the connections that are active, and the other connections fade back.
- Reduced motion and Off show a still frame of the steady state, with every connection highlighted, and the caption
  summarizes the whole story. The play button is hidden in both modes.

## Related

- `PaymentFlow` for a single request that is approved or declined.
- `MultiRegionFailover` for traffic that moves between regions.
- `VectorSearch` for a question answered from its nearest documents.
