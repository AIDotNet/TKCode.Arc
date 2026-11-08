---
title: Sankey flow
summary: Flows between stages drawn as ribbons with drifting particles: hover or focus a node or ribbon to trace its share through the whole journey.
---

<Demo name="TrialJourney" />

## When to use

`SankeyFlow` shows how a quantity splits and merges as it moves through stages. Each node is a stop on the way, each ribbon is a flow, and a ribbon's thickness is its value, so the reader sees at once where most of it goes and where it leaks away.

- **A product journey**, from the channel a trial came from, through onboarding, to the plan it ended on. Tracing a channel shows which plans it really feeds.
- **A budget**, from revenue sources through departments to what the money pays for.
- **Energy or material balance**, from supply through storage to use, in a utilities or operations report.
- **Support routing**, from the way a ticket arrived, through the team that took it, to how it closed.

## When not to use

- **One line of stages that only narrows**, such as a signup funnel with no branches: use `BarChart`, which compares each step's size exactly.
- **Parts of one whole with no stages**: use `DonutChart` or `Treemap`.
- **A change over time**: use `LineChart` or `Streamgraph`. A flow chart shows one period.
- **A graph whose shape is the point**, with steps and branches and no quantity: use `WorkflowCanvas`.
- **More than a few hundred flows**: ribbons become too thin to read. Group the small ones into an "Other" node first.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SankeyFlow Label="Signups by source" Unit=" people" TotalLabel="all signups">
        <arc:SankeyFlow.Nodes>
            <arc:SankeyFlowNode Id="web" Label="Website" />
            <arc:SankeyFlowNode Id="app" Label="Mobile app" />
            <arc:SankeyFlowNode Id="kept" Label="Still active" />
            <arc:SankeyFlowNode Id="left" Label="Left" />
        </arc:SankeyFlow.Nodes>
        <arc:SankeyFlow.Links>
            <arc:SankeyFlowLink Source="web" Target="kept" Value="420" />
            <arc:SankeyFlowLink Source="web" Target="left" Value="180" />
            <arc:SankeyFlowLink Source="app" Target="kept" Value="260" />
            <arc:SankeyFlowLink Source="app" Target="left" Value="40" />
        </arc:SankeyFlow.Links>
    </arc:SankeyFlow>
</UserControl>
```

`SankeyFlow` derives from Avalonia's `Control` and draws itself. Add nodes and links inline as above, or assign any list to `Nodes` and `Links` from code or a binding. Replacing a list, or changing an observable list, redraws the chart.

### Anatomy

1. **Stage names** (`Columns`): one line above the plot, or above each row in the upright layout.
2. **Nodes**: bars whose length is the larger of what enters and what leaves. Every column of a chart shares one value scale, so bars and ribbons compare across the whole chart.
3. **Ribbons**: one per pair of nodes. A ribbon is as thick as its value at both ends, and its color runs from its source stage to its target stage.
4. **Particles**: small dots drifting along each ribbon in the direction of the flow.
5. **Labels**: the node's name and value beside its bar. When neighbors are close, the value line and then the label give way; the tooltip still has them.
6. **Particle switch**: under the plot, at the right.
7. **Tooltip**: what is traced, its value and its share of the total.

### Columns

A node without a `Column` goes one column after the furthest node that flows into it, so a node that nothing flows into starts in the first column. Set `Column` to place a node yourself, for example to line up an end state with the other outcomes. `Columns` names the stages from the first column on.

```xml
<arc:SankeyFlow Label="Home energy">
    <arc:SankeyFlow.Nodes>
        <arc:SankeyFlowNode Id="battery" Label="Home battery" Column="1" />
    </arc:SankeyFlow.Nodes>
</arc:SankeyFlow>
```

### Links and cycles

A link's `Source` and `Target` are node ids. A link end with no matching node gets a node named after its id, so a quick chart needs only links. Links between the same two nodes add up. Self loops and values that are not positive are ignored.

A cycle does not break the chart: the link that closes it curves back to its target. A trace follows that link for one lap and stops. `Column` can also send a link backward, and it draws the same way.

### Formats and totals

`Unit` follows each value, and `ValueFormatter` replaces the whole format. `TotalLabel` names the whole in the share line, so "42% of the total" can read "42% of the city budget". The total is what enters the journey: the outflow of every node that nothing flows into.

<Demo name="CityBudget" />

### Tracing

Hover a node or a ribbon, focus it with the keyboard, or tap it. Its share lights up in the accent color through every stage before and after it while everything else dims, and each node passes on the same proportion of itself that reached it. The band's thickness is the share, so a node that fed only part of a later stage covers only that part of it.

### Particles

Particles carry no data of their own beyond direction and rough weight: a ribbon carries more of them the heavier it is, up to 34. Turn them off with `IsParticlesEnabled`, which is two-way and raises `ParticlesChanged`. The switch writes the same property. Set `IsParticleToggleVisible` to `False` to remove the switch, and `ParticleToggleLabel` to translate it.

<Demo name="ParticlesOff" />

```csharp
chart.ParticlesChanged += (_, e) => settings.ShowParticles = e.IsEnabled;
```

### Binding to a view model

With CommunityToolkit.Mvvm the view model owns the lists and replaces them for a new period. Nodes and links that keep their ids morph to their new size and place.

```csharp
public partial class JourneyViewModel : ObservableObject
{
    [ObservableProperty]
    private IList<SankeyFlowLink> _flows = [];

    [ObservableProperty]
    private bool _showParticles = true;

    public void Load(IEnumerable<(string From, string To, double Count)> rows)
        => Flows = rows.Select(row => new SankeyFlowLink(row.From, row.To, row.Count)).ToList();
}
```

```xml
<arc:SankeyFlow Label="Trial journey"
                Links="{Binding Flows}"
                IsParticlesEnabled="{Binding ShowParticles, Mode=TwoWay}" />
```

### Data shape and performance

Layout runs once for each data or size change. Drawing uses cached geometry, and particles live on a layer of their own, so a particle frame repaints only the dots and allocates nothing in the chart's own code. A chart of a few hundred links stays fast, and its particles are limited to a few hundred in all.

### Content guidelines

- **Chart label**: what flows, and when: "Lumen Notes trials, third quarter". It names the chart for assistive technology.
- **Node label**: a short noun phrase. A long name is cut off with an ellipsis, most tightly in the upright layout.
- **Stage name**: one word where possible, such as "Channel" or "Outcome".
- **Unit**: include the leading space for a word (" trials"), and leave it out for a symbol ("%").

| Instead of | Write |
|---|---|
| Node 1 | Search ads |
| Step 2 | Onboarding |
| Dropped | Lapsed |
| Total | all trials |

### Best practices

**Do**

- Keep what enters a node equal to what leaves it, except where the journey really gains or loses, so the shares add up.
- Give ids that stay the same across periods, so the chart morphs rather than replaces.
- Group small flows into an "Other" node when ribbons get thinner than a pixel or two.

**Avoid**

- Using the ribbon colors to carry meaning. They mark stages, and tracing uses the accent color and dimming.
- Counting one person twice in a stage. Shares are read against the total, and overlaps inflate them.
- Many columns in a narrow panel. Four or five stages read well; below 420 px each one takes a row.

### Common questions

**Why does a share read 100% on more than one node?** Each node's share is its value against the total, so a stage that everything passes through reads 100%.

**Why is a label missing?** Nearby labels give way so they do not overlap. Hover or focus the node to read it in the tooltip.

**Why can I not Tab to the chart?** The chart takes focus only when it has flows. An empty chart shows `EmptyLabel`.

## API reference

<!-- api: SankeyFlow -->

<!-- api: SankeyFlowNode -->

<!-- api: SankeyFlowLink -->

## Accessibility

- The chart is one focusable group, named by `Label`. While a node or ribbon is traced, the name announces its value, its share and its destinations, politely.
- The help text carries a summary of every node and the keys.
- Tracing uses the accent color plus dimming, not hue alone, and the focused node has a ring.
- A tap traces on touch screens; a second tap clears.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the chart and trace the first node. The chart is skipped while it has no flows |
| `Up` / `Down` | Previous or next node of the column. In the upright layout, previous or next row |
| `Left` / `Right` | Nearest node of the previous or next column. In the upright layout, previous or next node of the row |
| `Home` / `End` | First node of the first or last column |
| `Escape` | Clear the trace |

## Motion

- Nodes and ribbons grow in, column after column, on first view.
- When data changes they morph on one shared spring; new ones grow from nothing and removed ones shrink away.
- Focus pours the traced share outward along the ribbons on a spring while the rest dims.
- Particles run on `LiveLoop`, so they pause while the chart is off screen. No ribbon carries more than 34.
- Reduced motion draws the end state at once and removes both the particles and the switch.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Grows in by column | Short fade | Drawn at once |
| Data changes | Morph on a spring | Drawn at once | Drawn at once |
| Trace | Share pours along the ribbons | Drawn at once | Drawn at once |
| Particles | Drift along the ribbons | Removed, with the switch | Removed, with the switch |

## Responsive behavior

- Below 420 px wide the chart runs from top to bottom: columns become rows, labels sit beneath their nodes, and stage names sit above each row.
- It measures its own width, so a chart in a narrow panel changes the same way as one on a phone.
- The upright layout is at least 112 px per row, so it can be taller than `PlotHeight`.
- The arrow keys follow the layout: up and down move between rows.
- With a finger, a tap traces and a second tap clears, since touch has no hover.

## Related

- `BarChart` for an exact comparison of a few values.
- `Treemap` for parts of a whole in a hierarchy.
- `Streamgraph` for layers that change over time.
- `WorkflowCanvas` for a graph of steps with no quantities.

| Component | Choose it when |
|---|---|
| `BarChart` | Each step has one value to compare exactly |
| `DonutChart` | The data is a few parts of one whole |
| `Treemap` | The parts nest in a hierarchy |
| `Streamgraph` | The layers change over time |
| `WorkflowCanvas` | The shape of the process matters, not a quantity |
