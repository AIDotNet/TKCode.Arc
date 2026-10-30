---
title: Agent status
summary: A live status pill for a running agent: its mark in a spinning ring, the current step and a timer, opening into the recent steps with pause, stop and approval controls.
---

<Demo name="Preview" />

## When to use

`AgentStatus` is the smallest honest answer to "is the agent still working, and on what?". It is a pill that sits in a
header, a title bar or above a composer: the agent's mark inside a thin ring that turns while it works, the current
activity with its icon, and how long the run has taken. One press opens the same pill in place into a floating panel
with the latest steps, a segmented progress bar and the controls to pause, stop, approve or retry. Nothing below it
moves.

- **A header or title bar that should show a run at a glance**: what the agent is doing right now and for how long,
  without a log taking over the page.
- **Approval gates**: a command, a deploy or an email that waits for a person. The pill turns into a request with Deny
  and Approve right in it, so the answer is one press away from anywhere in the app.
- **Background runs people check on now and then**: the pill stays small; the panel shows the last few steps and the
  controls only when someone asks.
- **Runs that fail and can be retried**: the pill closes its ring around a cross, says why, and offers Retry.
- **Several agents in one app**: each header slot can hold its own pill; an idle pill costs no animation frames.

## When not to use

- **The run is the page**: a transcript with every step, tool call and output needs room. Use `ToolCall` rows in a
  chat thread, or a full run view; the pill's panel lists only the latest steps.
- **A queue of approvals across agents**: build a list of requests; one pill shows one agent's run.
- **A plan the user edits before the run**: use `AgentPlan`, which reorders and edits steps.
- **Comparing many agents on one time axis**: use `AgentTimeline`.
- **One shimmering word with no timer and no controls**: use `TextShimmer`.

## Usage

With nothing set, the pill rests at "Ready": the default mark, named "Agent", with no ring and no timer. The package
ships no sample run. The preview above plays a simulated run that the gallery writes: a coding agent named after
Claude Sonnet 5.5 works through six steps and stops on an approval that waits for as long as it takes. Its switch
moves to a second run (a research agent named after Gemini 4 Argon, whose third step fails once, so Retry can be seen)
and Replay starts over; the switch, the Replay button and the runs belong to the demo, not to the pill. The model
names are real; the tasks, files and counts are invented.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentStatus />
</UserControl>
```

`AgentStatus` derives from Avalonia's `TemplatedControl`. It lives in the Special package: register it with
`UseArcSpecial()` next to `UseArcControls()`.

In an app, drive it from your agent's events. Set `Status`, keep `Activity` to the latest tool call in the present
tense, list `Steps`, give a `StartedAt` that stays fixed for the whole run, and handle the action events. The pill never
changes its own status when it has data: pressing Approve raises `ApproveRequested`, and your code decides what happens.

```xml
<arc:AgentStatus Status="{Binding Run.Status}"
                 Activity="{Binding Run.Activity}"
                 StartedAt="{Binding Run.StartedAt}"
                 Agent="{Binding Run.Agent}" />
```

Each action is a routed event with typed arguments (`PauseRequested`, `ResumeRequested`, `StopRequested`,
`ApproveRequested`, `DenyRequested`, `RetryRequested`, `OpenRunRequested`).

```csharp
status.PauseRequested += (_, e) => agent.Pause(run.Id);
status.ApproveRequested += (_, e) => agent.Approve(run.Id);
```

```csharp
status.Steps.Add(new AgentStatusStep("scan", "Scanned 9 modules", AgentStatusStepState.Done, ArcIcons.Search)
{
    Duration = TimeSpan.FromMilliseconds(1917)
});
status.Activity = new AgentStatusActivity("Checking specs", ArcIcons.Search, progressValue: 8, progressTotal: 12);
```

### Every state

The pill has seven states. Each pairs the ring and glyph with words, so a state never relies on color. Press a pill to
open its panel.

<Demo name="States" />

| State | Ring and mark | Label | Timer | Buttons |
|---|---|---|---|---|
| `Idle` | The mark alone, no ring | "Ready" in the secondary color | Hidden | Open run in the panel |
| `Working` | A track and a short arc turning once every 0.9 s | The activity with its icon; the text shimmers | Counts | Pause and Stop in the panel |
| `NeedsInput` | The arc leaves; an accent dot pops in with one ping | The request in the text color | Keeps counting | Deny and Approve inline; Approve and Deny in the panel |
| `Paused` | The arc freezes and turns gray, the mark dims | "Paused" with a pause icon | Frozen | Resume and Stop in the panel |
| `Done` | The ring closes in the success color and the mark becomes a drawn check | "Done in 2m 23s · 5 files touched" | Hidden | Open run |
| `Failed` | The ring closes in the danger color around a cross | The failure | Hidden | Retry inline and in the panel |
| `Stopped` | No ring; a small square replaces the mark | "Stopped after 7s" in the secondary color | Hidden | Open run |

A finished run (`Done` or `Stopped`) settles back to "Ready" after `SettleAfter` (4 seconds by default; null keeps the
result on screen). The panel keeps the last run's steps and counter, and `Settled` is raised.

### Anatomy

1. **Orb**: a 24px disc. The agent's mark is drawn at 12px in the middle of a 1.5px ring. The ring is a track with a
   short arc while working, a closed circle when done or failed, and nothing while idle or stopped.
2. **Label**: a 16px icon and one line of 14px text. A new activity rolls in from below while the old one leaves
   upward. A count such as "8 of 12" changes its digits in place and is part of the shimmer. When the label is wider
   than its room, its right edge fades over 24px instead of wrapping.
3. **Timer**: minutes and seconds in tabular figures ("2:20"), shown while the run is live.
4. **Inline buttons**: Deny and Approve while input is needed, Retry after a failure, 30px high.
5. **Panel**: opened from the pill. A line with the agent's name and "Step 3 of 6" (or "6 of 6 steps" once the run has
   ended), a progress bar with one 3px segment per step, the latest five steps with an icon, a label that wraps and a
   time, and a footer with the buttons.

### The panel

The panel grows from the pill's own surface: width and height spring together while the header row stays where it is,
and the content fades in a beat later. It floats in the window's overlay layer, so a header that clips its children or
a scroller cannot cut it off, and nothing below it moves. It is as wide as 384px or the pill, whichever is wider, and
never wider than the container.

`PanelAlignment` chooses which edge the panel keeps: `Start` for a pill against the start of a header, `End` for one at
the end, `Center` (default) to grow evenly on both sides.

<Demo name="HeaderBar" />

`IsExpanded` is two way. Pressing the pill, Escape and a press outside change it; set it from code or bind it to open
the panel yourself. `ExpandedChanged` reports every change. `Expand()`, `Collapse()` and `Toggle()` do the same from
code.

```xml
<arc:AgentStatus IsExpanded="{Binding IsRunOpen, Mode=TwoWay}" PanelAlignment="End" />
```

The panel lists the latest `MaxSteps` steps (five by default) that are not pending. Pending steps still count in the
progress bar. A new step fades in at the bottom while the older ones slide up and the oldest fades out at the top.
Steps are matched by `Id`, so a step that changes from "Running tests" to "Passed 12 of 12 tests" keeps its row: the old
label fades out and the new one takes its place.

### Your own run

Write step labels in the present tense while they run and in the past tense once they are done, so the words carry the
state. A step's `StartedAt` makes its time count in whole seconds while it runs; a finished step shows its `Duration`
with one decimal ("1.9s"). Failed and skipped steps show no time.

The timer counts from `StartedAt` and leaves out the time spent paused or failed by itself. Give it the run's real start
once and keep it fixed; give `EndedAt` when your backend knows it, otherwise the moment the status turned done or stopped
is used.

<Demo name="HostDriven" />

```csharp
pill.ApproveRequested += (_, e) =>
{
    agent.Approve(run.Id);          // your backend
    pill.Activity = new AgentStatusActivity("Deploying build 2.4.1", ArcIcons.CloudUpload);
    pill.Status = AgentStatusRunState.Working;
};
```

`Notice` is a one-off message for screen readers, such as the result of an action; it joins the next announcement.
Every word the pill shows or announces comes from `Labels` (an `AgentStatusLabels`), including the sentences with
numbers, so the pill can be translated or reworded.

### Offering only what you can honor

A button shows only while its action is allowed by `Actions` and the run's state asks for it: Pause while working,
Resume while paused, Stop while working or paused, Approve and Deny while input is needed, Retry after a failure, and
Open run in every state. `Actions` is a flags value and defaults to all of them. It is the counterpart of showing a
control only when its handler exists; set it to what your app really handles.

<Demo name="Actions" />

```xml
<arc:AgentStatus Actions="Approve, Deny, OpenRun" />
```

The first two footer buttons keep their place when the state changes: Pause becomes Resume, Approve becomes Pause, Stop
becomes Deny. It is the same button each time, so keyboard focus stays on it. When the focused button goes away for good
(Pause after the run finished), focus moves to the pill, never to the page. Open run confirms with a check and the word
"Opened" for about 1.6 seconds.

### The simulation

A simulated run is for previews, onboarding and tests; your app writes it. `AgentRunScenario` holds the agent, the
steps, the outcome and the notices; each `AgentRunScenarioStep` has a present-tense label, a past-tense label, an icon, a
duration, an optional count, an optional approval gate and an optional failure on its first attempt.
`AgentRunSimulation` is a driver with no UI that plays the scenario on a timer: it lists the steps, ticks counts, waits
for an approval or a failure for as long as it takes, and applies Pause, Resume, Stop, Approve, Deny, Retry and Open
run to its own state. Give one to `AgentStatus.Simulation` and the pill starts it once it is on screen, shows it and
routes its buttons back; read its `Status`, `Activity`, `Steps` and `Notice` yourself to drive any other view.

```csharp
var run = new AgentRunScenario
{
    Agent = new AgentModel("Release bot", ArcIcons.Sparkles),
    Outcome = "2 files touched",
    Steps =
    [
        new() { Id = "read", ActiveLabel = "Reading 12 files", DoneLabel = "Read 12 files",
                Icon = ArcIcons.Search, Duration = TimeSpan.FromSeconds(2) },
        new() { Id = "push", ActiveLabel = "Pushing the branch", DoneLabel = "Pushed the branch",
                Icon = ArcIcons.CloudUpload, Duration = TimeSpan.FromSeconds(1.5),
                Request = "Waiting on you to push", WaitingLabel = "Approval needed to push" }
    ]
};
var simulation = new AgentRunSimulation(run);
pill.Simulation = simulation;
simulation.Replay();                                   // clears the run and starts again after 0.5 s
simulation.Scenario = otherRun;                        // starts over after 0.9 s
```

In XAML, create one simulation per pill inside `<arc:AgentStatus.Simulation>` and set its `Scenario` from
`{x:Static}`, as the preview's Code tab shows (the parameterless simulation has an empty run, which stays at Ready). The start delay is 0.9 seconds (`StartDelay`) and 0.5 seconds after `Replay()` (`ReplayDelay`). A
finished or stopped run stays as it is until the pill settles it. The timers run on a `TimeProvider`, so a test can step
the run with a manual clock; the pill takes its own `TimeProvider` property for the timer and the settle delay. Setting
`Simulation` back to null leaves the pill at Ready; the run itself goes on until you `Dispose()` it.

### Narrow containers

The pill follows its container's width, not the window's, and never grows past it. Below 400px of available width the
inline Deny, Approve and Retry become 30px icon buttons with accessible names, the timer steps aside while they show, and
Open run in the panel becomes an icon button. A long label fades at its right edge, and the panel's step labels wrap
instead of being cut off.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion every state, text, focus and announcement stays. Widths and the panel change at once, a new label
fades in place without rolling, the ring does not spin, the dot does not ping, the check and the closed ring appear
drawn, and the working text does not shimmer.

<Demo name="ReducedMotion" />

### Content guidelines

Keep the pill's label short; the panel carries the history. Write the activity as what the agent is doing now ("Reading
12 files"), the request as a sentence that asks ("Waiting on you to deploy to staging"), the failure as what went wrong
("Fetch failed · 2 pages did not load"), and the outcome without a verb for the time ("5 files touched": the pill writes
"Done in 1m 12s · 5 files touched").

| Instead of | Write |
|---|---|
| Processing | Checking specs |
| The agent is currently reading the files of the project | Reading 12 files |
| Waiting | Waiting on you to deploy to staging |
| Error | Fetch failed · 2 pages did not load |
| Run complete! All done successfully | 5 files touched |

### Best practices

**Do**

- Use `NeedsInput` for every approval gate (a command, a push, a payment, an email) and phrase the label as the request.
- Keep `StartedAt` fixed for the whole run and let the pill leave out paused and failed time.
- Pass only the actions your backend supports in `Actions`.
- Put the pill in a container that sets its width, such as a header slot, so it can fade long labels and size its panel.
- Give a mark its brand color through `AgentModel.MarkBrush`; a default mark is drawn in the text color.

**Avoid**

- Using it as a log. It lists the latest steps and drops the rest.
- Rewriting `Activity` for every count tick. Change only the count (`new AgentStatusActivity(label, icon, 9, 12)`): the
  digits change in place and screen readers are not interrupted.
- Putting a long sentence in the label. It fades out at the edge, and the full text is in the pill's accessible name.
- Several pills of the same run on one screen.

### Common questions

**Why does the panel not push the page down?** It floats in the window's overlay layer on top of the page, starting at
the pill's footprint. The pill keeps its place in the layout.

**Can a header with `ClipToBounds` hold the pill?** Yes. The collapsed pill is inside the header; the open panel is not.

**How do I show the timer for a run that already started?** Set `StartedAt` to the real start. The pill counts from there.

**What if I give `Status` and a `Simulation`?** Your data wins: while `Status`, `Activity`, `StartedAt` or a step is
set, the pill shows them and leaves the simulation alone (it does not start it, and the buttons only raise their
events). `Agent` alone does not count: it names the simulated run's agent.

**Why does a count not roll like a new activity?** Only a new label and icon roll. A count changes in place, so it can
tick several times a second.

## API reference

<!-- api: AgentStatus -->

<!-- api: AgentStatusActivity -->

<!-- api: AgentStatusStep -->

<!-- api: AgentStatusLabels -->

<!-- api: AgentStatusActionEventArgs -->

<!-- api: AgentStatusExpandedChangedEventArgs -->

<!-- api: AgentRunSimulation -->

<!-- api: AgentRunScenario -->

<!-- api: AgentRunScenarioStep -->

The enums:

| Type | Values |
|---|---|
| `AgentStatusRunState` | `Idle`, `Working`, `NeedsInput`, `Paused`, `Done`, `Failed`, `Stopped` |
| `AgentStatusStepState` | `Pending`, `Active`, `Waiting`, `Done`, `Failed`, `Skipped` |
| `AgentStatusActions` | `None`, `Pause`, `Resume`, `Stop`, `Approve`, `Deny`, `Retry`, `OpenRun`, `All` (flags) |
| `AgentStatusPanelAlignment` | `Start`, `Center` (default), `End` |

The agent is an `AgentModel` (name, a 24 by 24 stroked mark and its brush), shared with the other agent components.

## Accessibility

- The pill is a button with an expanded state. Its name reads the agent, the activity with its count and the elapsed
  time ("Claude Sonnet 5.5: Checking specs 8 of 12, 0:09 elapsed"); a finished run reads its result.
- A polite live region announces each new activity and each outcome ("Claude Sonnet 5.5: done in 14 seconds, 5 files
  touched"), and the result of an action through `Notice`. It does not speak counts or timer ticks.
- The open panel is a region named "{agent} run details"; the progress bar is a progress bar named "Run progress" with a
  value such as "4 of 6 steps finished".
- Status never relies on color: each state has a word (Ready, Paused, Done in, Stopped after) and a different ring or
  glyph; steps use the present tense while running and the past tense when finished.
- The icon buttons of narrow containers have accessible names. A truncated label stays complete in the pill's name and
  in the panel's step list.
- Keyboard focus shows a 2px ring, inset on the pill and outside the buttons; pointer focus draws none.
- Focus is kept: a button that changes role keeps focus, and when the focused control goes away focus moves to the pill.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the pill, then to the inline Deny and Approve or Retry. With the panel open: Pause or Resume, Stop or Deny, Open run, then on to the next control in the page |
| `Enter` / `Space` on the pill | Open or close the panel |
| `Enter` / `Space` on a button | Press it |
| `Escape` | From anywhere inside the open panel, close it and return focus to the pill |

A press outside the open panel closes it and leaves focus where the press lands. The panel is not modal and does not trap
focus.

## Motion

- A new activity rolls in over 180ms from 12px below while the old one rolls out upward in 140ms; the pill's width
  follows on a spring without bounce and the label's right edge fades while it is wider than its room.
- The panel springs width and height together from the pill's footprint (about 0.75 s to settle); the content fades in
  after 80ms over 200ms and out in 75ms when it closes.
- The ring turns once every 0.9 s while working, freezes in place when paused and picks up from the same angle. When
  input is needed the arc leaves and the dot springs in with one 0.9 s ping. When a run ends the ring draws closed in
  0.44 s while the mark shrinks away and a check (or cross) draws itself in 0.28 s after a short beat.
- The working label carries a shimmer in a 2.4 s loop (1.7 s sweep, then a pause); the count and the active step's label
  shimmer with it.
- New steps fade in over 155ms, older ones slide up on the smooth spring, and the progress segments fill from the left
  over 480ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New activity | Rolls and fades | Fades in 100ms in place | Replaced |
| Pill width and panel size | Springs | Instant | Instant |
| Ring, shimmer, ping | Animated | Still | Still |
| Done and failed | Ring and check draw | Shown drawn, short fade | Shown drawn |
| Timer and counts | Change in place | Change in place | Change in place |

## Related

- `ToolCall` for a single call in a transcript, with its input and output.
- `AgentPlan` for the steps an agent will take, editable before the run.
- `AgentTimeline` for a replay of several agents on one time axis.
- `TextShimmer` for a single shimmering word.

| Component | Choose it when |
|---|---|
| `AgentStatus` | One agent's run should be visible in a header and open on demand |
| `ToolCall` | You list calls in a transcript |
| `AgentPlan` | The user edits and approves the steps first |
| `AgentTimeline` | Several agents and handoffs share a time axis |
| `Alert` | You report a real job's state in text |
