---
title: Agent plan
summary: "An agent's plan as an editable checklist: reorder, rename and add steps while it is a draft, then follow the run step by step once it is approved."
---

<Demo name="Preview" />

## When to use

`AgentPlan` is the card an agent shows between "here is what I will do" and "done". While it is a draft, a person
reads the steps and fixes them: renames a title in place, drags a step to another position, removes one, adds one, or
asks the agent for one more. Approving it locks the plan, and the same card turns into a live checklist that follows
the agent through each step, with the time it took, a place for activity lines, and a decision to make when a step
fails.

- **Plan mode for a coding or operations agent**: the agent proposes the steps, a person edits and approves them, and
  the card tracks the work. Nothing runs before the approval.
- **A side panel or a chat message that tracks a multi-step task**: one current step, finished steps that settle back
  to quiet text, and a footer that ends with the total time and a one-line summary.
- **Work that can fail halfway**: a failed step stops the run and shows its reason with Retry and Skip, instead of
  hiding the failure in a log.
- **Trying the whole journey in a page or a demo**: with no data and no status the card plays an invented plan on a
  built-in simulation, so every state can be seen without an agent.

## When not to use

- **A transcript of everything the agent did**: use `ChatThread` or a log view. The plan is a short ordered list of
  intentions, not a record of tool calls and their output.
- **Approving single risky actions one at a time**: a plan is approved once, up front. Ask for each action in its own
  prompt when the person should decide case by case.
- **Independent tasks across several agents**: this is one ordered plan. Use a task list for a queue.
- **An onboarding checklist**: the steps here are an agent's, they cannot be ticked off by the person, and they only
  move forward as the agent works.
- **Reporting the state of one job**: use `Alert`, `Badge` or a table. A plan with a single step says little.

## Usage

With no `Steps` and no `Status`, the card shows an invented draft (adding full-text search to a help center) and runs
the built-in simulation when it is approved.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:AgentPlan MaxWidth="560" />
</UserControl>
```

`AgentPlan` derives from Avalonia's `TemplatedControl` and lives in the Special package; register it with
`UseArcSpecial()` next to `UseArc()`. The card is as wide as its container up to 560px, reads well from about 320px,
and tightens below 420px.

### Anatomy

1. **Header**: the model's mark, "Plan · 6 steps" (the number rolls when steps are added or removed) and the goal under
   it. The model's name sits on the right while the plan is a draft; once it runs, the right side counts "3 of 6".
2. **Progress line**: a 2px line on the header's bottom rule that grows with the finished steps. It is hidden in a draft.
3. **Steps**: a numbered list. Every row has a fixed lead column (the grip, later the status mark), the title, and a
   fixed trail column (the remove button, later the time or the word Skipped). The columns do not change when the plan
   is approved, so titles wrap the same way and never move.
4. **Add step**, and while the list is empty a sentence saying so.
5. **Footer**: Keep planning and Approve plan in a draft; the status and Pause or Resume while running; the total time,
   the summary and Start over when it is done.

The six status marks each have their own shape, so state never rests on color alone: an empty ring (pending), a ring
with a quarter arc that turns once every 800ms (in progress), a ring with two bars (paused), a green disc with a check
that draws itself (done), a red disc with an exclamation mark (failed) and a dashed ring (skipped).

### Editing a draft

Every step title is a text area that grows with its text.

- **Enter** saves the rename and keeps the focus. **Escape** undoes the edits since the field got the focus (or since the
  last Enter). Leaving the field saves it.
- **Backspace** in an empty title removes the step and puts the caret at the end of the step above; in the first step the
  caret goes to the start of the next one. Leaving a field you emptied removes the step too.
- **Alt+Up** and **Alt+Down** in a title move the step one place; the caret stays where it was.
- The grip moves a step with the pointer: the row lifts, follows the pointer and swaps with a neighbour once it has
  passed half that neighbour's height; letting go settles it. Focus the grip and press **Up** or **Down** to move it
  without a pointer.
- The remove button shows while the row is hovered or holds the focus.
- **Add step** appends an empty step with its title focused. With no steps left, Approve plan is disabled; Keep planning
  still works.

Approving trims the titles and drops empty steps. Keep planning asks the agent for one more step: a spinner shows in the
button, then the suggestion is inserted before the last step and its title streams in a few words at a time. When it has
finished it is an ordinary editable step with a "Suggested" note.

### A controlled plan

Set `Status` and the plan is controlled: the control never changes the status, and never touches a step's state,
activity lines, error or time. Every action is reported through an event, and you update the data.

| Event | When | You usually |
|---|---|---|
| `Approved` | Approve plan, with the steps in order, trimmed and without empty ones | Start the agent, set `Status` to Running |
| `Changed` | A draft edit: rename, move, remove, add or a streamed suggestion (`Kind`) | Keep your copy of the draft in step |
| `KeepPlanningRequested` | Keep planning | Set `IsPlanning`, ask the agent, call `StreamSuggestion` |
| `PauseChanged` | Pause (true) or Resume (false) | Pause the agent, set `Status` to Paused or Running |
| `StepAction` | Retry or Skip on a failed step (`Kind`, `Step`) | Run the step again or move on, and update its state |
| `StartOverRequested` | Start over after Done | Reset the steps, set `Status` back to Draft |

Draft edits are made in `Steps` itself (the control renames, moves, removes and adds there) and reported through
`Changed`; only the run is yours.

<Demo name="Controlled" />

```csharp
plan.Approved += (_, e) =>
{
    foreach (var step in plan.Steps) { step.State = AgentPlanStepState.Pending; }
    plan.Status = AgentPlanStatus.Running;
    agent.Start(e.Steps);
};
plan.StepAction += (_, e) => agent.Decide(e.Step, e.Kind);
```

While the agent works, change the steps and the control follows: set one step to `Active` at a time, append to its
`Activities` as it reports them, then set `Done` with a `Duration`. For a failure set `Error` first, then `Failed`, so the
announcement can read the reason. Insert a step with `NoteKind = AddedByAgent` where it belongs and it slides in with a
faint accent wash. Set `Summary` before the status becomes Done; it streams in after "Done in …".

### A plan in the middle of its run

The same data drives a plan that is already running. This one has two finished steps, a step the agent added, and a
failed step waiting for a decision.

<Demo name="MidRun" />

A failed step shows the word Failed, its reason, and Retry and Skip. The footer says which step failed and the Pause
button steps aside. Keyboard focus moves to Retry (it would otherwise fall to the page when Pause disappears); after
Retry or Skip it goes back to Pause.

### The simulation

`AgentPlanSimulation` is the built-in agent that runs when `Status` is null. It is event-driven: one timer waits per
state change, nothing runs while the plan is idle, Pause keeps the time that was left and Resume continues from there.
It answers the same buttons a host would. Its beats follow the measured journey (at 1× speed):

| Beat | Time |
|---|---|
| Approve plan → the first step starts | 280ms |
| Step starts → its first activity line | 372ms |
| Between activity lines | 540ms |
| Last line → step done | 470ms |
| Step done → the next one starts | 290ms |
| Last step done → plan done | 420ms |
| Retry → first new line, then 577ms, then 465ms → done | 555ms |
| Skip → the next step starts | 277ms |

In the sample the agent inserts a step after the third step's second line (417ms later) and the test step fails once
after its last line, then waits for Retry or Skip for as long as it takes. A step that has no script (one you added) runs
`GenericScript`: three activity lines and 30 seconds. Scripts are found by step id, so a renamed step keeps its script and
a removed one is skipped. Start over returns to the draft the run started from, with every suggestion available again.

Give the simulation your own steps, suggestions and summary to demo another journey:

<Demo name="Labels" />

```csharp
plan.Simulation = new AgentPlanSimulation
{
    Steps = [new("a", "Find every link to the old page", new(["Found 14 links"], TimeSpan.FromSeconds(9)))],
    Suggestions = [],
    Summary = "The page now lives at /preferences."
};
```

### Times and counts

A step time shows whole seconds under a minute ("23s") and minutes with two-digit seconds from a minute on ("1m 07s").
The total is the sum of the done steps' times, so skipped and failed steps add nothing. The header counts "{n} of {count}"
where n is the first step that is not done or skipped, and the progress line is done and skipped steps over all steps.
Counts roll in a reserved width with tabular digits, so a changing number never nudges the words around it.

### Narrow containers

Below 420px the header, list and footer tighten, and the draft's two buttons share the row equally. Titles wrap to as many
lines as they need; the columns stay.

<Demo name="Narrow" />

### Reduced motion

Reduced and off motion keep every state, label, focus move and announcement, and drop everything that travels: rows,
activity lines, the failure block and the footer change height at once, the arc does not turn, the check appears
complete, the shimmer rests, an inserted step gets no wash and a suggestion's words appear together. The simulation runs
the same timeline.

### Content guidelines

Write titles as instructions of about 6 to 10 words, and activity lines as short past-tense phrases that start with a
verb the plan knows (`AgentPlanActivityIcons`): Read, Searched, Found, Created, Edited, Ran or Measured. Register more
verbs with `AgentPlanActivityIcons.Set`.

| Instead of | Write |
|---|---|
| Step 1 | Find every link to the old settings page |
| Doing the search | Searched for /settings |
| Tests | Ran 6 tests |
| Something went wrong | 3 of 14 tests failed: typo matching skips two-letter words |
| Finished! | The page now lives at /preferences, and the old address redirects. |

### Best practices

**Do**

- Keep a plan to four to eight steps. A longer plan is a project; split it.
- Show the reason of a failure in the agent's own words and offer Retry and Skip, so the person can decide.
- Pass a model with its own mark so the agent's notes read as the agent's.
- Keep `Id` stable when you rename or reorder; the simulation, announcements and your own logs rely on it.

**Avoid**

- Changing `Status` back and forth while someone is editing a draft.
- Using the plan as a log. Activity lines are a handful of short facts per step, and fold away when the step is done.
- Brand marks for the model. Use a generic glyph and name the model in text.

### Common questions

**Does the plan run anything?** No. It shows a plan and, with no `Status`, plays an invented simulation. Real work is
yours: answer `Approved` and update the steps.

**How do I show a step the agent added?** Insert an `AgentPlanStep` with `NoteKind = AgentPlanNoteKind.AddedByAgent`
while the plan runs. It slides in, gets a wash and a note, and is announced.

**Can people edit while it runs?** No. Rows are fixed once the plan is approved; the only controls are Pause or Resume,
Retry and Skip on a failure, and Start over at the end.

**What if I have no model?** Leave `Model` null (with your own steps): the header shows no mark and no name.

**Why is Approve plan disabled?** The draft has no steps. Add one, or let the agent keep planning.

## API reference

<!-- api: AgentPlan -->

<!-- api: AgentPlanStep -->

<!-- api: AgentPlanLabels -->

<!-- api: AgentPlanSimulation -->

<!-- api: AgentPlanStepScript -->

<!-- api: AgentPlanScriptedStep -->

<!-- api: AgentPlanApprovedEventArgs -->

<!-- api: AgentPlanChangedEventArgs -->

<!-- api: AgentPlanStepActionEventArgs -->

<!-- api: AgentPlanPauseChangedEventArgs -->

<!-- api: AgentModel -->

The enums:

| Type | Values |
|---|---|
| `AgentPlanStatus` | `Draft`, `Running`, `Paused`, `Done` |
| `AgentPlanStepState` | `Pending`, `Active`, `Done`, `Failed`, `Skipped` |
| `AgentPlanNoteKind` | `None`, `Suggested`, `AddedByAgent` |
| `AgentPlanChangeKind` | `Rename`, `Move`, `Remove`, `Add`, `SuggestionStreamed` |
| `AgentPlanStepActionKind` | `Retry`, `Skip` |

`AgentPlanSamples` holds the invented sample (`Steps`, `Suggestions`, `GenericScript`, `Goal`, `Summary`,
`CreateModel()`, `CreateSteps()`), so a page can start from it and change one step.

## Accessibility

- The card is a group named after the model and the goal ("Lumen 2 Pro plan: Add full-text search to the help center").
  The steps are an ordered list, so a screen reader announces the count and each position.
- While the plan runs, every step announces its state in words (Pending, In progress, Done, Failed, Skipped) before its
  title. Each state also has its own shape, so it never rests on color.
- A failed step reads the word Failed, its reason and two labelled buttons (Retry and Skip, each named with the step).
- A title is a labelled text area ("Step 3") described by a hint that explains Alt plus the arrow keys and Escape. Grips
  and remove buttons are named after their position and step ("Move step 3: …").
- The progress line is a progress bar with a text value ("2 of 6 steps finished"); it is hidden from assistive
  technology while the plan is a draft.
- One polite status region announces edits, approval (and that a run is simulated), each finished step, an inserted
  step, a failure, retry, skip, pause and resume, the final summary and a reset. Dragging with the pointer says nothing.
- Focus follows the action: a moved step keeps focus, a removed step hands it to its neighbour (the last one to Add
  step), Add step focuses the new title, Approve plan hands it to Pause, a failure moves it to Retry, Retry and Skip
  hand it back to Pause, Done moves it to Start over and Start over to Approve plan.
- Keyboard focus shows a 2px ring (inset on controls inside a row); pointer focus shows none. Titles show a quiet inset
  line instead.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | In a draft: each step's grip, title and remove button, then Add step, Keep planning and Approve plan. While running: Retry and Skip on a failed step, and Pause or Resume. Done: Start over |
| `Alt+Up` / `Alt+Down` in a title | Move the step one place; the caret stays |
| `Up` / `Down` on a grip | Move the step one place; at the ends nothing happens |
| `Enter` in a title | Save the rename and stay in the field |
| `Escape` in a title | Undo the edits since the field got the focus or since the last save |
| `Backspace` in an empty title | Remove the step and focus the one above |
| `Space`, `Enter`, `Home`, `End` on a grip | Nothing: there is no pick-up mode |

## Motion

- **One spring for every change in size or place**: critically damped, about 260ms, no overshoot. Rows slide and grow
  when a step is inserted, removed or moved, a dragged row's neighbours step aside, an activity line or the failure
  block opens, and the footer and the progress line follow. A dropped row settles on a slightly faster spring.
- A step becoming done draws its check as a stroke (after 45ms, over 200ms) while the disc fades up, the title settles
  from primary to secondary text over 240ms and the time fades in. Its activity lines fold away at the same moment.
- The current step's title shimmers (one pass in 1.8s, then a short rest) beside the turning arc. Pausing stops both.
- A suggestion streams in chunks of three to five words every 34ms, each chunk only fading in; the step the agent inserts
  arrives with an 8% accent wash that holds for about 640ms and fades out over about 1.2s.
- Counts roll with tabular digits in a reserved width. The header's right side, the footer's rows and the status marks
  swap with one short crossfade (the old one out in 60ms, the new one in and up 3px over about 145ms).

| Change | Full | Reduced | Off |
|---|---|---|---|
| Heights and positions | Spring | At once | At once |
| Status swaps | Crossfade with a 3px rise | Short crossfade | At once |
| Spinner and shimmer | Turn and sweep | Still | Still |
| Check | Draws over 200ms | Shown complete | Shown complete |
| Inserted step | Slides in with a wash | Appears | Appears |
| Streamed suggestion | Chunks fade in | All words at once | All words at once |
| Timeline of the simulation | Unchanged | Unchanged | Unchanged |

## Related

- `AgentStatus` for a compact live status of one running agent.
- `ToolCall` for a single tool call with its input, output and approval.
- `AgentTimeline` for the time axis of an orchestrated run.
- `ContextWindow` for how much of a model's context a run uses.
- `ChatThread` when you show the conversation itself.
