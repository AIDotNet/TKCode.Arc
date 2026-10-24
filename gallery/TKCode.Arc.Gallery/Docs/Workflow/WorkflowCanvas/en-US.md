---
title: Workflow canvas
summary: "Show a workflow on a dotted canvas and play its runs: each step lights up in turn, the data draws along the edges, and every step reports its result and time."
---

<Demo name="Preview" />

## When to use

- Show how an automation, pipeline or agent workflow is put together: its steps, its branches and where they lead.
- Play a run as it happens, or replay one from history: which step is running, what each step found, how long it took, and which branch the run skipped.
- Explain a product that works for the user in the background (webhooks, data syncs, deploys), where seeing the steps run is the point.
- Let people build or change a workflow themselves: move the steps, connect them, branch on a condition.

## When not to use

- A short, straight sequence of steps a person works through: use `Stepper`.
- Events in time order without a structure: use `Timeline`, or `AgentTimeline` for agent runs.
- An agent's plan as an editable checklist: use `AgentPlan`.
- Quantities flowing between stages: use a Sankey-style chart.

## Usage

Build a `WorkflowGraph` from steps (`WorkflowNode`) and the connections between them (`WorkflowEdge`, or `Connect`), and give it to a `WorkflowCanvas`. Steps without a `Position` are arranged in ranks, top to bottom, and the places are written back to the steps.

```xml
<arc:WorkflowCanvas x:Name="Canvas" />
```

```csharp
var graph = new WorkflowGraph();
var trigger = new WorkflowNode("trigger", "Order placed") { Icon = ArcIcons.Zap };
var lookup = new WorkflowNode("lookup", "Find customer account") { Description = "Plan, seats and owner" };
graph.Nodes.Add(trigger);
graph.Nodes.Add(lookup);
graph.Connect(trigger, lookup);
Canvas.Graph = graph;
```

### Playing a run

Set each step's `State` when it really changes: `Running` when the step starts, `Done` (with `Result` and `Duration`) when it finishes, `Failed` (with `Error`) when it fails, `Skipped` for the steps a branch did not take. Call `ResetRun` before the next run. Edges follow their steps on their own: when a step is done and the next one starts, the data draws along the edge in the accent; the edges into skipped steps turn dashed.

```csharp
lookup.State = WorkflowNodeState.Done;
lookup.Result = "Enterprise, 212 seats";
lookup.Duration = TimeSpan.FromMilliseconds(118);
branch.State = WorkflowNodeState.Running;
```

You can set the next step running at the same moment the previous one finishes: with `QueueRunVisuals` on (the default), the next card keeps showing as waiting until the data has drawn into it, so the run always reads step, edge, step. Only what the card shows waits; `State` is what you set.

### Step states

Each card shows its step's state at the end of the row: nothing while there is no run, an empty ring while waiting, a spinning ring while running, the time and a check when done, an alert mark when failed, a minus when skipped. A running card takes the accent, a failed one a red border, a skipped one fades. `WorkflowNodeCard` also works outside a canvas.

<Demo name="States" />

### A finished run

Set the final states before the canvas appears to show a run from history: nothing plays, and every edge the run took is drawn at once.

<Demo name="FinishedRun" />

### Compact steps

`Appearance="Compact"` lays a card out in two rows, 200 × 84: icon and state on top, title and description below. Use it for a row of actions that fan out from one step, as in the preview.

### Left to right

`LayoutDirection="LeftToRight"` runs the workflow across the canvas: edges leave a card's right side and enter the next card's left side, and arranging places ranks side by side.

<Demo name="LeftToRight" />

### Edge styles

`EdgeStyle` draws the edges as curves (`Bezier`, the default, as in Arc), right angles with rounded corners (`Step`), or straight lines (`Straight`). The data of a run draws along any of them.

<Demo name="EdgeStyles" />

### Panning and zooming

People pan by dragging the canvas (or with the middle button), zoom with Ctrl / ⌘ + wheel, a trackpad pinch or a touch pinch, and with the + and − keys. `WheelMode` decides what plain scrolling does: `Pan` (the default) pans the canvas, which suits a canvas that fills a panel; `ModifierZoom` leaves plain scrolling to the page, which suits a canvas inside a scrolling page. `IsPanZoomEnabled="False"` turns all of it off for a workflow that should only fit, like the preview. A double click on the empty canvas fits the workflow again.

`WorkflowZoomControls` is a small zoom bar: zoom out, the current zoom (click it for 100%), zoom in, and fit. Point its `Target` at the canvas and place it over a corner. From code, `ZoomTo`, `ZoomIn`, `ZoomOut`, `FitToContent` and `ScrollIntoView` move the view with a short ease-out unless you pass `animate: false`.

<Demo name="PanZoom" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" AutoFit="Fit" />
    <arc:WorkflowZoomControls Target="{Binding #Canvas}" Margin="12"
                              HorizontalAlignment="Left" VerticalAlignment="Bottom" />
</Panel>
```

### Arranging

The canvas arranges steps without a `Position` and remembers that it did: when such a workflow grows or loses a step, it is arranged again and the cards glide to their new places. `ArrangeNodes()` arranges every step again with Arc's spacing (52 between ranks, 72 below a step that fans out, 20 between neighbours) and hands steps your code placed back to the canvas.

<Demo name="Arrange" />

### Editing

`IsEditable="True"` lets people rearrange the workflow. Drag a step to move it (the selected steps move together and snap to the dot grid when let go; hold Alt to place them freely), Shift + drag the empty canvas to select every step the rectangle touches, and drag a step near the edge to scroll the view. With the keyboard, Space selects the focused step, Ctrl / ⌘ + A selects all, M starts moving the selection with the arrow keys (Shift for five grid steps, Enter to place, Escape to cancel), and Delete removes the selected steps and edges. A step with `IsLocked` stays where it is and cannot be deleted, but can still be selected.

Ctrl / ⌘ + C copies the selected steps and the edges among them, Ctrl / ⌘ + X cuts them, Ctrl / ⌘ + V pastes them one grid step down and right of where they were copied (in the middle of the view when that place is out of sight), and Ctrl / ⌘ + D duplicates them without the clipboard; pasted steps get new ids and become the selection. Within your app a paste keeps everything a step had except its run; between apps the clipboard carries the steps as JSON, without icons. Copying works on a read-only canvas too. From code: `CopySelectionAsync`, `CutSelectionAsync`, `PasteAsync` and `DuplicateSelection`.

Selecting works on any canvas: a click selects a step, Ctrl / ⌘ + click toggles it, Shift + click adds it, and a click on the empty canvas clears the selection. Read `SelectedNodes`, change it with `Select` and `ClearSelection`, and follow it with `SelectionChanged`. Each finished edit raises `GraphEdited` once, with what changed (`Move`, `Delete`, `Connect`, `Disconnect`, `Paste`, `Undo` or `Redo`), the steps and the edges: save the workflow there.

<Demo name="Editing" />

```csharp
Canvas.GraphEdited += (_, e) =>
{
    // Move: the steps are at their new Position. Delete: they are already out of Canvas.Graph.
    foreach (var node in e.Nodes)
    {
        Debug.WriteLine($"{e.Kind} {node.Id} at {node.Position}");
    }
};
```

A step you move is yours from then on: the canvas no longer arranges it when the workflow changes or narrows, until `ArrangeNodes` hands it back. Edits people make on the canvas never arrange the workflow again; connections your code adds to a workflow the canvas arranged do.

### Connecting steps

On an editable canvas a card shows its ports when you point at it, select it or focus it: the input on its top side and the outputs on its bottom side (left and right when the workflow runs across). Drag from an output onto another step, or close to one of its inputs, to connect them. While you drag, the steps the connection cannot go to fade, and pointing at one tells you why. Drag the end of an edge off its input to move it to another step, or let go on the empty canvas to remove it. Click an edge to select it (Ctrl / ⌘ + click toggles it, Shift + click adds it); Delete removes it. With the keyboard, C starts a connection from the focused step: when it has several outputs, choose one with the arrow keys and press Enter; then choose a step with the arrow keys and press Enter to connect.

A step without ports has one input and one output of its own (the edge's port is then null). Give a step named ports where the workflow branches, such as the "Yes" and "No" of a condition; their labels always show beside them. Turn `HasDefaultInput` off for a trigger, which nothing leads into.

```csharp
var yes = new WorkflowPort(WorkflowPortDirection.Output, "Yes");
var no = new WorkflowPort(WorkflowPortDirection.Output, "No");
check.Outputs.Add(yes);
check.Outputs.Add(no);
graph.Connect(check, yes, approve, null);
graph.Connect(check, no, refund, null);
```

Every connection is checked before it is made. It is refused when it would connect a step to itself, end at an output, repeat a connection, go past a port's `MaxConnections`, join ports whose `DataType`s differ, or close a loop (unless `AllowCycles` is on). `CanConnect` runs the same checks from code and tells you why. Put your own rules in `ConnectionRequested`: set `Cancel`, and a `Reason` the person hears. A connection let go on the empty canvas raises `ConnectionDroppedOnCanvas` with the place, in canvas units: offer a list of steps there and add the chosen one.

<Demo name="Connecting" />

```csharp
Canvas.ConnectionRequested += (_, e) =>
{
    if (e.SourcePort == yes && e.TargetNode == refund)
    {
        e.Cancel = true;
        e.Reason = "Amounts over $500 need a manager's approval first";
    }
};
Canvas.ConnectionDroppedOnCanvas += (_, e) =>
{
    var step = new WorkflowNode("notify", "Notify the team") { Position = new Point(e.Position.X - 150, e.Position.Y) };
    // One edit: a single undo takes the step and its connection back.
    using (graph.History.BeginGroup())
    {
        graph.Nodes.Add(step);
        graph.Connect(e.SourceNode, e.SourcePort, step, null);
    }
};
```

### Undo and redo

`WorkflowGraph.History` keeps the last 100 edits people made on the canvas: moving, deleting, connecting, moving or removing edges, and pasting. Ctrl / ⌘ + Z undoes, Ctrl / ⌘ + Shift + Z or Ctrl + Y redoes, and the canvas's `Undo()` and `Redo()` do the same for your buttons; moved cards glide back. Follow `History.Changed` to update those buttons from `CanUndo` and `CanRedo`.

Changes your code makes to the workflow are recorded only inside `History.BeginGroup()`: everything added, removed or moved until the group is disposed becomes one edit, as in the handler above. If your app keeps its own undo history, turn `History.IsEnabled` off and follow `GraphEdited` instead.

### Toolbar commands

For toolbar buttons and menu items, the canvas has commands: `UndoCommand`, `RedoCommand`, `DeleteSelectionCommand`, `CopyCommand`, `CutCommand`, `PasteCommand`, `DuplicateCommand`, `ZoomInCommand`, `ZoomOutCommand` and `FitCommand`. Each runs only when it would do something, such as undo with an edit to undo or delete with something selected, so a bound button turns itself on and off.

```xml
<arc:Button Content="Undo" Command="{Binding #Canvas.UndoCommand}" />
<arc:Button Content="Delete" Command="{Binding #Canvas.DeleteSelectionCommand}" />
```

### Custom card content

`NodeTemplates` replaces the content of the cards it matches: a `WorkflowNodeTemplate` is written in XAML for one `Kind` of step, its data is the `WorkflowNode`, and the first template that matches a step wins. The card keeps its frame, the run's colors, the selection, the focus ring and its ports; steps no template matches keep the default card. Without a `Size` the card is as tall as its content. Controls in a card, such as an input or a switch, keep their own clicks and keys, so typing in them never moves or connects the step.

<Demo name="CustomNodes" />

```xml
<arc:WorkflowCanvas x:Name="Canvas" IsEditable="True">
    <arc:WorkflowCanvas.NodeTemplates>
        <arc:WorkflowNodeTemplate Kind="email" x:DataType="arc:WorkflowNode">
            <StackPanel Spacing="10">
                <TextBlock Text="{Binding Title}" FontWeight="{arc:ArcDesignTokenResource WeightMedium}" />
                <arc:Input Text="{Binding Description, Mode=TwoWay}" />
                <arc:Switch Content="Attach the invoice" IsChecked="True" />
            </StackPanel>
        </arc:WorkflowNodeTemplate>
    </arc:WorkflowCanvas.NodeTemplates>
</arc:WorkflowCanvas>
```

From code, add any `IDataTemplate` (a `FuncDataTemplate<WorkflowNode>`, for example). A card used on its own takes its template in `ContentTemplate`.

### Large workflows

The canvas is built for hundreds of steps: cards, shadows and the dot grid stay cheap to draw however many show, so a workflow of 500 steps and 800 edges stays responsive to pan, zoom and drag on a desktop. `WorkflowMinimap` shows the whole workflow small, with the part on screen as a frame: press it to go there, drag to pan. Point its `Target` at the canvas and place it over a corner, like the zoom bar.

<Demo name="LargeGraph" />

```xml
<Panel>
    <arc:WorkflowCanvas x:Name="Canvas" MinZoom="0.05" />
    <arc:WorkflowMinimap Target="{Binding #Canvas}" Margin="12"
                         HorizontalAlignment="Right" VerticalAlignment="Bottom" />
</Panel>
```

Lower `MinZoom` (0.25 by default) so that fitting can show all of a large workflow.

### Narrow canvases

Below `NarrowBelowWidth` (520 by default, as in Arc) the canvas switches to Arc's phone layout: one-row cards become 344 × 52, two-row cards 108 × 72, and a workflow the canvas arranged is arranged again with tighter spacing (26, 36 and 10), so it shows at actual size instead of shrinking. Steps your code placed keep their places. Keep the titles of compact steps to a word or two; they have little room on a phone.

<Demo name="Narrow" />

### Fitting and sizes

`AutoFit` decides how the workflow fits the canvas. `ShrinkToFit` (the default) shows it at actual size, centered, and shrinks the whole workflow like a screenshot when it does not fit; `Fit` makes it as large as fits up to `MaxZoom`; `None` keeps the view you set with `Zoom` and `Offset`. Panning, zooming, or setting `Zoom` or `Offset` yourself stops fitting until `FitToContent`. In a container with unlimited height, such as a scrolling page, the canvas is as tall as the fitted workflow. `Padding` (24 by default) keeps the workflow off the edges.

### Placing steps

Give a step a `Position` to place it yourself (the card's top-left corner on the canvas). New steps without a position go in ranks after the steps your code placed.

### Content guidelines

- Name a step by what it does, short, in sentence case: "Find customer account", not "HubSpot lookup step".
- Put what the step works on in `Description` and what it found in `Result`; both stay on one line.
- Keep errors specific and short: "Health check failed in eu-west", not "Error".

## API reference

<!-- api: WorkflowCanvas -->

<!-- api: WorkflowNodeCard -->

<!-- api: WorkflowGraph -->

<!-- api: WorkflowNode -->

<!-- api: WorkflowEdge -->

<!-- api: WorkflowPort -->

<!-- api: WorkflowHistory -->

<!-- api: WorkflowZoomControls -->

<!-- api: WorkflowMinimap -->

<!-- api: WorkflowNodeTemplate -->

<!-- api: WorkflowNodeEventArgs -->

<!-- api: WorkflowEditEventArgs -->

<!-- api: WorkflowSelectionChangedEventArgs -->

<!-- api: WorkflowConnectionRequestedEventArgs -->

<!-- api: WorkflowConnectionDroppedEventArgs -->

## Accessibility

- The canvas is named "Workflow, 6 steps" unless you name it with `AutomationProperties.Name`.
- Each card's name gives its title, its kind, its state in words, the time and the line under the title, for example "Find customer account, Done, 118 ms, Enterprise, 212 seats". A state is never shown by color alone: every state has its own mark.
- The canvas reads as a group and each card as an item of it. Edges, ports, the dot grid and the marks are decorative and hidden from assistive technology; the keyboard's connect mode reaches every connection the pointer can make. The minimap is a pointer shortcut for what the keys already do, so it takes no focus and screen readers skip it.
- A failed step and the end of a run are announced politely: "Create onboarding task failed: Task service timed out", "Run finished: 4 done, 1 failed, 1 skipped". So are edits: "Moved 2 steps", "Deleted 1 step", "Connected Amount over $500? to Manager approval", "Can't connect: This would create a loop", "Pasted 2 steps", "Undone", and how the keyboard's move and connect modes work when they start.
- A selected card adds "selected" to its name and shows an accent border with a soft halo, not only a color change.
- The canvas's help text lists the keys below.

### Keyboard

| Key | Action |
| --- | --- |
| Tab | Moves into the canvas, to the step that had focus last, and out again; the cards are one Tab stop. |
| Arrow keys | Move to the nearest step in that direction. |
| Alt + arrow keys | Follow the connections: along the workflow to the next step, against it to the previous one, across it to a neighbouring step that shares a previous step. |
| Home / End | Move to the first or last step. |
| Enter | Opens the step (`NodeActivated`). |
| + / − | Zoom in or out about the focused step. |
| Shift + 1 | Fits the workflow. |
| Ctrl / ⌘ + 0 | Zooms to 100%. |
| Shift + F10, Menu key | Asks for a context menu (`ContextRequested`, its source is the card). |
| Space | Selects or unselects the focused step. |
| Ctrl / ⌘ + A | Selects every step and edge. |
| Escape | Cancels a move or a connection, or clears the selection. |
| M | On an editable canvas: moves the selection (or the focused step) with the arrow keys; Shift moves five grid steps, Enter places, Escape cancels. |
| Delete / Backspace | On an editable canvas: deletes the selected steps and edges (or the focused step); locked steps stay. |
| C | On an editable canvas: connects the focused step. With several outputs, the arrow keys choose one and Enter goes on; then the arrow keys choose a step in reading order, Enter connects, Escape cancels. |
| Ctrl / ⌘ + Z | On an editable canvas: undoes the last edit. |
| Ctrl / ⌘ + Shift + Z, Ctrl + Y | On an editable canvas: redoes it. |
| Ctrl / ⌘ + C | Copies the selected steps (or the focused one) and the edges among them. |
| Ctrl / ⌘ + X, Ctrl / ⌘ + V | On an editable canvas: cuts them, and pastes. |
| Ctrl / ⌘ + D | On an editable canvas: duplicates the selected steps (or the focused one). |

Keys typed into a control in a card (an input in custom content) belong to that control.

A step reached with the keyboard is brought into view.

## Motion

- A running card's border and fill ease to the accent over the standard duration; a skipped card fades to half opacity.
- A state change swaps the mark at the end of the card: the new one grows from 0.6 while the old one shrinks away, on the snappy spring.
- The running ring turns once every 0.7s, only while it is on screen.
- When a step is done and the next one has started, the data draws along the edge in the accent over 400ms on the in-out curve; the next card shows as running once the data arrives.
- Fitting, zooming with the buttons or keys and bringing a step into view move the view on Arc's standard ease-out over 360ms; dragging, the wheel and pinches follow the hand without easing.
- Arranging glides the cards to their new places the same way, the edges following.
- A dragged card follows the pointer without easing, rides above the others, and settles on the grid on the snappy spring when let go; a cancelled move glides back to where it started.
- A connection being drawn follows the pointer without easing and jumps to the nearest input within 24px, which grows and takes the accent; the steps it cannot go to fade to 40% over the standard duration. Undo and redo glide moved cards back.
- Reduced motion: no color easing, the marks swap with a short fade, the ring stands still, the data appears drawn at once, and the view and the cards jump to where they go. Nothing loops while a workflow is at rest.

## Related

- `Stepper`: a straight sequence of steps a person works through.
- `AgentPlan`: an agent's plan as an editable checklist.
- `AgentTimeline`: a replay of a multi-agent run on one time axis.
- `DataFlow`: an illustration of data moving from sources to destinations.
