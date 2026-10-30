---
title: Tool call
summary: A tool call row for an agent transcript: the tool, what it did and how it went, opening into the call's input and output, with approval and retry in place.
---

<Demo name="Preview" />

## When to use

`ToolCall` is one line of an agent transcript: the tool's mark, what the call did in plain words, quiet meta such as
"4 results · 1.2s" and a status icon. `ToolCallGroup` collects every call of one assistant turn under a header that
reads "Used 4 tools". A click opens a call into its input and output, so the transcript stays readable and the detail
stays one click away.

- **Showing what an agent did** inside a chat or an agent log, with the arguments and the result a click away.
- **Human in the loop**: a call that must be allowed or denied before it runs, such as posting a message or writing
  to a database, with Deny and Allow in the row.
- **Recoverable failures**: a timed out query shows its reason and a Retry button where it happened.
- **Debug and audit views** where people copy the exact input and output of a call.

## When not to use

- **A plan with steps and progress**: use `AgentPlan`; a tool call is one action, not a checklist.
- **One status for a whole run**: use `AgentStatus`; the group header only summarizes the calls it holds.
- **A queue of approvals across many runs**: build a list of your own; a row decides one call in its place.
- **The tools an agent may use**: that is configuration, not history.

## Usage

Give a row a `ToolCallItem`. Tools are data, not brands: a name for screen readers, a 24 × 24 stroked mark (a Lucide
glyph) and the phrases for each status. With no item the row is empty: a wrench and "No tool call" in the muted color,
with no status and nothing to open (the last row of the statuses demo below). A group with no calls takes no space.

```xml
<arc:ToolCall>
    <arc:ToolCallItem Id="tickets" ToolName="Tracker"
                      ToolMark="{x:Static arc:ArcIcons.Inbox}" Status="Success"
                      Title="Searched tickets in finance/close" Summary="4 results" Duration="0:0:1.18" />
</arc:ToolCall>
```

On its own a row sits in a 1px Surface frame (radius 16, 4px in); inside a group it is flush with no frame.

For an agent turn, add the calls to a `ToolCallGroup` as the agent starts them and change them in place:

```csharp
var group = new ToolCallGroup();
group.Items.Add(call);                  // a new row grows in from zero height
call.Status = ToolCallStatus.Success;   // the row keeps its place and its open state
group.IsActive = agentIsWorking;        // keeps "Using" between calls
```

### Anatomy

1. **Row**: 40px high. The mark (16px), the title (one line, it truncates), the meta, the status icon and a chevron.
2. **Reason line**: under the title of a failed call (its reason) or a call waiting for approval (what allowing will
   do), 12px. A row with one is 58px high.
3. **Buttons**: Retry on a failed call, Deny and Allow on one waiting for approval, 28px high.
4. **Input and Output**: a mono key and value list, and the result as a list, a table, tinted JSON or text. Each has a
   copy button that confirms in place with a check for 1.9 seconds.
5. **Group header**: overlapping marks, one per tool, then "Using 3 tools", "Used 4 tools" or "Waiting for approval",
   and on the right "1 failed" or the total duration.

### Statuses

<Demo name="Statuses" />

Each status has its own icon shape, so color is never the only cue: a spinner (running), a check that draws itself
once (success), an alert (error), a shield with a question mark (approval) and a ban (denied). A running title
shimmers. An approval row sits on an accent outline over a faint accent tint. A denied row is muted and its meta reads
"Denied".

`ToolCallItem` carries a phrase per status: `Title` is the settled past tense ("Searched …"), `RunningTitle` the
present tense, `ApprovalTitle` a request ("Post a status note to …") and `ErrorTitle` the failure
("Could not read …"). A missing phrase falls back to `Title`.

### Outputs

<Demo name="Outputs" />

| Output | Looks like | Folds | Copy gives |
|---|---|---|---|
| `ToolCallListOutput` | A lead in mono digits, a title, a tag; 32px rows | After 5 rows | One line per entry |
| `ToolCallTableOutput` | Header and mono cells; 30px rows; scrolls sideways in its well | After 5 rows | Tab separated rows with the header |
| `ToolCallJsonOutput` | Indented JSON, keys, strings, numbers and literals tinted; 20px lines | After 10 lines | The JSON text |
| `ToolCallTextOutput` | One paragraph that keeps its line breaks | Never | The text |

JSON is built on `System.Text.Json.Nodes` and tinted by a small tokenizer of our own, so nothing uses reflection.
Folded content hides behind "Show 25 more lines", which turns into "Show less". When the summary is missing it is
derived from the output: "N results", "N rows", "N fields". `OutputTemplate` replaces the built-in view of a
successful call's result with your own.

### A group driven by your own data

<Demo name="OwnData" />

The group raises `ApproveRequested`, `DenyRequested` and `RetryRequested` (they bubble from the row, carrying the
item). Update the call yourself, usually to running and then to success, and set `Handled`. The control never changes
a status you drive.

When nobody handles an action, the control keeps a local behaviour so a page without wiring still works: **Deny**
marks the call denied; **Allow** and **Retry** go to the `ToolCallSimulation` that appended the call, which runs it on.
On any other data they change nothing. A simulated run says so through the row's live region ("This is sample data, so
the call is only simulated.").

### Opening and folding

Opening the header staggers the rows in, 20ms apart, each settling 6px down. A call appended while the group is open
grows in from zero height instead. While the agent works the group stays open; 450ms after every call has finished
(`AutoCollapse`, `AutoCollapseDelay`) it folds itself away so the answer reads right under "Used 4 tools". Once the
person has opened or closed the group or a row, it never folds by itself. A call that needs a decision opens a folded
group. `ExpandedChanged` tells whether the change was automatic.

### The simulation

`ToolCallSimulation` plays a scripted turn into an observable list on a clock: calls are appended about 225ms apart,
run for their time and settle; a failing call waits for `Retry`; a call that needs approval waits for `Approve` or
`Deny`. The package ships no script: you write one from `ToolCallSimulationStep`s, each a `ToolCallItem` (the run
appends a copy) with an outcome and its timing. Give the simulation to `ToolCallGroup.Simulation`: while the group has no
calls of its own, it shows the simulation's `Items`, says "Using" while it `IsWorking`, starts the run from the top when
it comes on screen and stops it when it leaves. Calls in `Items` or `ItemsSource` win over the simulation.

```csharp
var lookup = new ToolCallItem("orders", "Orders", "Looked up order 1042", ToolCallStatus.Success, ArcIcons.Search)
{
    RunningTitle = "Looking up order 1042",
    Summary = "1 result",
    Duration = TimeSpan.FromMilliseconds(900)
};
group.Simulation = new ToolCallSimulation(
[
    new ToolCallSimulationStep(lookup) { Delay = TimeSpan.FromMilliseconds(600) }
]);
group.Simulation.Start();                       // Run again: plays from the top
```

In XAML, set `<arc:ToolCallGroup.Simulation>` to a `ToolCallSimulation` whose `Script` comes from `{x:Static}`, as the
preview's Code tab shows; give each group its own simulation (it keeps the playback state), the script can be shared.
Pass a `TimeProvider` to step it in tests. The transcript around the group in the preview (the question, the sentence,
the answer and Run again) is demo content written by the gallery, and so are the finance team's calls; none of it is
part of the control.

### Narrow containers

<Demo name="Narrow" />

Below 440px of the row's own width the meta keeps only the duration, the reason wraps, the buttons move under it and
Input and Output drop their indent. Below about 370px the meta goes. Wide JSON and tables scroll inside their well.

### Reduced motion

At the Reduced and Off levels the stagger, the spinner, the shimmer travel and every spring stop: panels, rows and
statuses change at once. Labels, announcements, focus and all content stay.

### Content guidelines

- Write titles as verb phrases: past tense when settled ("Read the export job log"), present while running.
- Name the tool by what it is for ("Tracker", "Warehouse"). It is read before the title.
- A reason line says what went wrong or what allowing will do, in one sentence.
- Pick the output that reads best: a list for search hits, a table for rows, JSON for structured data, text for a
  sent message or a short log.

### Best practices

- Keep `Id` stable: a row keeps its open state while its status changes.
- Change items in place; add new calls to the end.
- Set `IsActive` while the agent works so the header does not flicker between "Using" and "Used".
- Use a Lucide glyph for each tool mark; the mark follows the text color unless you set `ToolMarkBrush`.

### Common questions

**Can I use a brand logo as the mark?** The mark is any 24 × 24 stroked geometry. Brand logos are yours to license;
the demos use Lucide glyphs only.

**Does Allow run the tool?** No. It raises `ApproveRequested`; your code runs the tool and updates the status.

**Why is the Output empty for a running call?** There is no result yet; the section says so.

## API reference

<!-- api: ToolCall -->

<!-- api: ToolCallGroup -->

<!-- api: ToolCallItem -->

<!-- api: ToolCallArgument -->

<!-- api: ToolCallOutput -->

<!-- api: ToolCallListOutput -->

<!-- api: ToolCallListEntry -->

<!-- api: ToolCallTableOutput -->

<!-- api: ToolCallJsonOutput -->

<!-- api: ToolCallTextOutput -->

<!-- api: ToolCallLabels -->

<!-- api: ToolCallSimulation -->

<!-- api: ToolCallSimulationStep -->

<!-- api: ToolCallActionEventArgs -->

<!-- api: ToolCallExpandedChangedEventArgs -->

<!-- api: ToolCallSimulationEventArgs -->

The enums:

| Type | Values |
|---|---|
| `ToolCallStatus` | `Running`, `Success`, `Error`, `Approval`, `Denied` |
| `ToolCallSimulationOutcome` | `Succeeds`, `FailsUntilRetried`, `NeedsApproval` |
| `ToolCallSimulationState` | `Idle`, `Working`, `WaitingForRetry`, `WaitingForApproval`, `Finished` |
| `ToolCallSimulationPhase` | `Started`, `CallAppended`, `CallSucceeded`, `CallFailed`, `WaitingForApproval`, `Retried`, `Allowed`, `Denied`, `Finished` |

## Accessibility

- The group header and every row are buttons that report whether they are expanded. A row's name reads the tool, the
  phrase and the status: "Tracker: Searched tickets in finance/close Done, 4 results, 1.2s".
- Status is never color alone: each status has its own icon, failed and waiting calls show a reason line, and the name
  spells the status out.
- Each row announces status changes through a polite live region, including that a simulated call is only simulated.
- Retry, Allow and Deny are named with the call they act on: "Allow: Post a status note to #finance-ops". When a
  button goes away focus moves to its row instead of dropping to the page.
- Input and Output are named groups; the copy buttons confirm through the live region without changing width.
- Pointer focus draws no ring; keyboard focus shows a 2px ring inside the row so it is never clipped.
- Every word is in `ToolCallLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the group header, each row, a row's Retry, Deny or Allow, and in an open row its copy buttons and the fold button |
| Enter, Space | Opens or closes the group or a row; on a button, presses it |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening a row or the group | Height springs, content fades over 160ms | At once | At once |
| Rows when the group opens | Stagger 20ms apart, settle 6px | None | None |
| A row appended to an open group | Grows from zero height | At once | At once |
| Status icon | Scale and fade swap; the check draws over 330ms | At once | At once |
| Running title and "Using 3 tools" | Shimmer, one pass each 1.8s | Still | Still |
| Title or header text change | Rises from a soft blur over 240ms | Fades over 130ms | At once |
| A new tool mark in the header | Widens on a spring | At once | At once |
| Chevron | Turns on a spring | At once | At once |
| Approval outline | Fades over 240ms | At once | At once |

## Related

- `AgentStatus` for the live status of a whole run.
- `AgentPlan` for the plan an agent writes before it acts.
- `AgentTimeline` for a replay of a multi-agent run on one time axis.
