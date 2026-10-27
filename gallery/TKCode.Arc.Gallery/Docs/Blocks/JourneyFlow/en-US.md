---
title: Journey flow
summary: "A journey as a Sankey chart: point at a step or a flow to trace that cohort through every stage, select a step to see where its people came from and went next, and switch segments to watch every flow morph."
---

<Demo name="Preview" />

## When to use

`JourneyFlow` shows how a group of people moves through a sequence of stages and where they leave. Stages run left to
right; each stage holds a step on the main path and, under it, the people who stopped there; ribbons between neighbouring
stages are as thick as the number of people who took that way. The goal in the last stage is green.

- **Funnels with exits.** Visit, sign-up, onboarding, activation, plan: the drop at every step is a visible ribbon, not
  only a percentage.
- **Comparing segments.** Channels, cohorts or months that share one structure. The switch over the chart morphs every
  bar, ribbon and count to the other segment, so the change itself is what people see.
- **Asking "who are these people".** Pointing at a step traces everyone who passed through it across the whole journey:
  where they came from and how they ended up. Pointing at a single ribbon does the same for the people who took that way.

## When not to use

- **Loops, skips or many paths.** Links only join neighbouring stages, and nodes stack in the order you give them. For
  journeys where people go back or jump ahead, use a graph view.
- **More than three nodes a stage.** Each node needs 44 pixels under the one above it for its label; four or five nodes in
  a column leave little room for the bars.
- **A single conversion rate.** A KPI tile or a small bar says "6% subscribed" in less space.
- **Exact numbers first.** The chart is for shape and proportion; the "Flow table" under it carries the exact counts, and a
  plain table is better when numbers are the point.

## Usage

The block derives from Avalonia's `TemplatedControl` and draws its chart itself. Give it the stages, the nodes (each with
the key of its stage) and one or more segments, each a list of links from a node to a node in the next stage.

```xml
<arc:JourneyFlow Title="Trial to paid journeys"
                 SubtitleFormat="{}{0} visits in October, from first page to plan"
                 GoalCaption="of visitors picked a paid plan">
    <arc:JourneyFlow.Stages>
        <arc:JourneyStage Key="landing" Header="Landing" ShortHeader="Land" />
        <arc:JourneyStage Key="trial" Header="Trial" />
        <arc:JourneyStage Key="plan" Header="Plan" />
    </arc:JourneyFlow.Stages>
    <arc:JourneyFlow.Nodes>
        <arc:JourneyNode Key="arrived" StageKey="landing" Header="Arrived" Description="Opened a course page" />
        <arc:JourneyNode Key="started" StageKey="trial" Header="Started trial" />
        <arc:JourneyNode Key="left" StageKey="trial" Header="Left" Kind="Exit" DropOffLabel="Left before the trial" />
        <arc:JourneyNode Key="subscribed" StageKey="plan" Header="Subscribed" Kind="Goal" />
        <arc:JourneyNode Key="lapsed" StageKey="plan" Header="Lapsed" Kind="Exit" />
    </arc:JourneyFlow.Nodes>
    <arc:JourneySegment Key="all" Header="All learners">
        <arc:JourneyLink SourceKey="arrived" TargetKey="started" Value="5320" />
        <arc:JourneyLink SourceKey="arrived" TargetKey="left" Value="11470" />
        <arc:JourneyLink SourceKey="started" TargetKey="subscribed" Value="1025" />
        <arc:JourneyLink SourceKey="started" TargetKey="lapsed" Value="4295" />
    </arc:JourneySegment>
</arc:JourneyFlow>
```

### Anatomy

- **Header**: `Title`, the subtitle from `SubtitleFormat` with the visitors count rolling in place of `{0}`, and the
  segment switch (shown for two or more segments).
- **Chart**: one column per stage with a guide line and the stage name under it; node bars 8 wide, stacked from the top 44
  apart; two label lines over each bar (name, then "count · share" of visitors); ribbons between stages. A node's count
  is the larger of what flows in and what flows out, so the first stage's total is the visitors. The scale is the same
  for every stage, so the fullest stage fills the height.
- **Side panel**: beside the chart from 860 pixels, under it below. With no selection it is an overview: the goal count
  and its share of visitors, the biggest drop-offs (exits before the last stage that nobody moves on from, with their share
  of the step before) and the last stage's outcomes. With a selected step it shows that step: its count, share of visitors,
  where its people arrived from and where they moved on to.
- **Flow table**: a disclosure under the chart that lists every flow with its share of the step it left.

### Node kinds

`Kind` sets the colours: a `Step` is drawn in the strong accent with flows into it in Accent at 10%; an `Exit` in muted grey
with faint flows (4%); a `Goal` in the success colour with green flows (16%). Exits are what the overview lists as
drop-offs, and goals are what it counts.

### Tracing and selection

Pointing at a step or its label, or at a ribbon, traces that cohort: the rest of the chart fades, every flow the cohort
took is covered by an accent band of its size, and the part of each bar the cohort makes up turns solid; labels of steps
the cohort never reached fade too. A tooltip follows the pointer with the count and two shares. Clicking a step selects
it: it stays traced while you point elsewhere, the side panel switches to it, and the selection survives a segment change.
Click it again, click the empty chart, press Escape or use the panel's Clear button to clear it. Clicking a ribbon does
not select.

`SelectedNode` and `SelectedSegment` are two-way. `SelectedNodeChanged`, `SelectedSegmentChanged` and `TraceChanged`
report the changes (the last one for the previewed node or link, pointer or keyboard), `NodeSelectedCommand` and
`SegmentSelectedCommand` run on user actions, and `GetCohort` returns the traced counts per link for your own summaries.

<Demo name="Selection" />

```csharp
journey.SelectNode("hooked");
journey.SelectedSegment = journey.Segments.First(s => s.Key == "social");
journey.TraceChanged += (_, e) =>
{
    if (e.Node is { } node)
    {
        var cohort = journey.GetCohort(node);
        status.Text = $"{node.Header}: {cohort.Values.Sum():N0} people across {cohort.Count} flows";
    }
};
```

### Narrow containers

The block reads its own width, not the window's. Under 860 the side panel moves under the chart with its two groups side
by side; at 559 or less the switch spans the whole width, paddings tighten to 16 and the groups stack; at 479 or less the
labels get smaller. The chart never scrolls or rotates: it lays itself out again at every width, 404 high from a 720
pixel chart, 372 from 480 and 340 below. Under 600 the labels drop the share, and under 440 the top node of each stage
shows only its count while the stage names switch to `ShortHeader`.

<Demo name="Narrow" />

### Numbers and words

`ValueFormatter` formats every count (labels, tooltip, panel, table, spoken names), `ShareFormatter` every share given as a
fraction, and `LabelShareFormatter` the rounded share on the chart labels. The panel's numbers roll digit by digit even
with your own formatter: the text before and after the digits stays put. Every phrase is a property with an English
default: `CountFormat` ("{0} users"), `ShareOfFormat`, `VisitorsShareText`, `VisitorsCaption`, `GoalCaption`,
`OverviewHint`, `DropOffsHeader`, `OutcomesHeader`, `InboundHeader`, `OutboundHeader`, `JourneyStartText`, `JourneyEndText`,
`ClearText`, `TableHeader`, `TableCaptionFormat`, `TableColumnHeaders` and `AnnouncementFormat`. The tooltip's
"Share of …" row names the step everyone in the stage came through; set `JourneyStage.ShareBaseLabel` or
`JourneyNode.ShareLabel` to word it yourself.

With one segment the switch is hidden; `IsTableVisible` and `IsSidePanelVisible` remove the table and the panel.

<Demo name="Tickets" />

### Live counts

Changing a link's `Value` in the shown segment morphs the chart in place, like a segment switch. `StagesSource`,
`NodesSource` and `SegmentsSource` take collections you own (an observable collection is followed) instead of the
`Stages`, `Nodes` and `Segments` lists.

<Demo name="Live" />

## API reference

<!-- api: JourneyFlow -->

<!-- api: JourneyStage -->

<!-- api: JourneyNode -->

<!-- api: JourneyLink -->

<!-- api: JourneySegment -->

<!-- api: JourneyNodeKind -->

<!-- api: JourneyNodeChangedEventArgs -->

<!-- api: JourneySegmentChangedEventArgs -->

<!-- api: JourneyTraceChangedEventArgs -->

## Accessibility

### Keyboard

- **Tab** stops on the segment switch, then once on the chart (the last focused step, the first one to begin with), then
  on the panel's Clear button when a step is selected, then on "Flow table".
- On the switch, **←/↑** and **→/↓** move to the previous and next segment and wrap; the chart follows at once.
  **Home** and **End** go to the first and last.
- On the chart, **↑/↓** move within a stage (stopping at the ends), **←/→** move to the step of the neighbouring stage
  whose middle is nearest, **Home** and **End** go to the first and the last step. A focused step is traced and its
  tooltip sits beside it.
- **Enter** or **Space** selects the focused step, or clears it when it is already selected. **Escape** clears the
  selection and keeps the focus where it is.
- After Clear, the focus goes back to the step that was selected.

### Screen readers

- Each step is a toggle button named "Started trial: 5,320 users, 31.7% of visitors. Began the free trial." whose pressed
  state is the selection. The steps form one group named by `ChartLabel`, and its help text reads the whole chart stage by
  stage.
- The segment switch is a group named by `SegmentsLabel`; its segments report which one is pressed.
- A segment change is announced politely ("Search: 8,050 visits and 510 paid plans."). Tracing and selecting are not
  announced; the focused step's name already says it all.
- The tooltip is drawn and hidden from screen readers. Rolling numbers read their final value, never the rolling one.
- The side panel is a region named by `PanelLabel`; empty row slots are hidden. The Flow table button reports whether it
  is expanded, and the table reads as text with a caption.
- The focus ring is 2 pixels wide, 2 pixels outside the step's hit box.

## Motion

- **Segment switch**: every count rides a spring (stiffness 70, damping 16.4, no overshoot, about 0.8 s to settle) and
  the whole chart is laid out again from the moving counts each frame, so bars, ribbons and labels move as one. The label
  counts follow the same spring 65 ms later. The visitors in the subtitle and the panel's numbers roll digit by digit on
  the smooth spring, the share bars glide, and the panel's titles fade out in 120 ms while the new ones rise 4 pixels in
  240 ms. The thumb of the switch glides on a quick spring.
- **Tracing**: the rest of the chart fades in 160 ms; the cohort's bands and bar overlays grow on a quick spring and, when
  you move to another step, change from where they are rather than starting over. The tooltip fades in over 120 ms,
  follows the pointer without lag, and cross-fades its content when the traced thing changes.
- **Selecting**: the step's ring fades in over 160 ms; the panel's texts swap as above.
- **Flow table**: the height opens on the smooth spring and the chevron turns over on the quick one.
- Reduced motion places the chart and the numbers at once and keeps only the short fades (tooltip, panel titles).
  Off applies every change at once. Nothing animates when the block first appears.

## Related

- `Cohort retention` and `Metrics dashboard`, the other Analytics blocks.
- `SegmentedControl` for a standalone switch.
- `Sortable data table` when the numbers matter more than the shape.
