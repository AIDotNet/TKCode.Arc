---
title: Agent routines
summary: Scheduled agent routines: cron schedules in plain words with the next run, a strip of recent outcomes, a switch to pause or resume, Run now, and the instructions and run history on demand.
---

<Demo name="Preview" />

## When to use

`AgentRoutines` manages work AI agents do on a schedule: a morning digest, a nightly check, a weekly report. Each row
shows the routine's name and agent, its schedule in plain words ("Weekdays at 08:00") with the next run, and a strip of
its recent outcomes, green for succeeded, red and taller for failed, short and grey for skipped. A switch pauses or
resumes it and Run now starts it at once. Opening a row shows the instructions the agent follows and the recent runs
with their results.

- **Scheduled agents** in an automation or assistant product.
- **Operations dashboards** where people check that recurring jobs run and step in when one fails.

## When not to use

- **One-off tasks.** Use `Agent tasks`.
- **Editing a schedule.** This block shows schedules; edit them in a form of your own.
- **Plain cron jobs without an agent.** A table of jobs fits better.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it shows sample routines and Run now plays a
short stand-in run. In an app, give it your routines and start runs on your backend:

```xml
<arc:AgentRoutines Routines="{Binding Routines}" />
```

```csharp
list.RunHandler = (routine, cancellation) => api.RunNowAsync(routine.Id, cancellation); // returns an AgentRoutineRun
list.EnabledChanged += (_, e) => api.SetEnabled(e.Routine.Id, e.Routine.IsEnabled);
```

### Schedules

`Schedule` is a five-field cron expression: minute, hour, day of month, month, day of week (Sunday is 0). Fields take
numbers, ranges, lists and steps. `AgentRoutineSchedule.Describe` turns common forms into words and `Next` gives the next
run; both are public, so a server can use the same rules. A paused routine shows Paused instead of the next run.

### Runs

`Runs` lists past runs, newest first, each with when it started, the outcome, how long it took and a summary. The strip
shows the last `HistoryLength` runs. A run started with Run now goes through `RunHandler`; its result is added in front
of `Runs`, and a handler that throws records a failed run with the message.

<Demo name="Events" />

## API reference

<!-- api: AgentRoutines -->

<!-- api: AgentRoutine -->

<!-- api: AgentRoutineRun -->

<!-- api: AgentRoutineSchedule -->

<!-- api: AgentRoutinesLabels -->

## Accessibility

- The list is a region named "Routines"; the title is a heading.
- **Tab** reaches each routine's switch, header and Run now. **Space** flips the switch; **Enter** or **Space** opens a
  routine or starts a run.
- The switch reads "Run Morning inbox digest on schedule"; the header reads the name, the schedule and the next run, and
  reports itself as expanded or collapsed.
- The outcome strip reads as a summary ("8 of 10 recent runs succeeded").
- A polite live region says when a run started with Run now ends.

## Motion

- A routine's detail opens on a spring with a short fade; the chevron turns on a snappy spring.
- A paused routine fades its header to 60%.
- Reduced motion keeps short fades; Off applies every change at once.

## Responsive behavior

- The list is at most 760 wide and follows its container below that.
- At 560 or less the outcome strip and Run now move under the schedule.
- Names and schedules shorten with an ellipsis.

## Related

- `Agent tasks` for one-off work handed to agents.
- `Agent run` for one run in detail.
- `Switch`, the control that pauses a routine.
