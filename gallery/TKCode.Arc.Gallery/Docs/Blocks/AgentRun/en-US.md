---
title: Agent run
summary: One AI agent run shown live: the goal, a timeline of thinking, tool calls with their input and result, files touched and messages, the clock, cost and tokens, and Stop or Retry.
---

<Demo name="Preview" />

## When to use

`AgentRun` is the page you watch while an AI agent works on a task you gave it. The goal sits on top, so you always know
what the agent is trying to do. Under it a timeline grows one step at a time: what the agent thought, which tool it
called with what input and what came back, which files it created or changed, and what it finally told you. A clock,
the money spent and the tokens used run in the corner, a bar shows how many steps are done, and one button stops the
run or starts it again.

- **Coding, data or operations agents** that take minutes and many tool calls, where people want to see progress and
  step in.
- **The detail page of a task** opened from a list or a board of agent work.
- **Reviewing a finished run** later: the same view shows a completed, stopped or failed run from stored steps.

## When not to use

- **A chat reply with a few tool calls.** Use `AI chat` or `AI answer`, which show tools inline in the conversation.
- **Many runs at once.** Use `Agent tasks` for the overview and open one run from there.
- **Long logs with thousands of lines.** Show raw logs in a log viewer; a step here is a meaningful action.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it plays a built-in sample run so you can try
it. In an app, give it the goal, the steps as an `ObservableCollection<AgentRunStep>`, the state, the cost and the
tokens, and handle Stop and Retry:

```xml
<arc:AgentRun Goal="{Binding Goal}"
              Steps="{Binding Steps}"
              State="{Binding State}"
              Cost="{Binding Cost}"
              Tokens="{Binding Tokens}" />
```

```csharp
run.StopRequested += (_, _) => agent.Cancel();
run.RetryRequested += (_, _) => StartNewRun();

// As your agent works: add a step when it starts, update it when it ends.
var step = new AgentRunStep { Kind = AgentRunStepKind.ToolCall, Title = "Find the route", ToolName = "search_code",
                              Input = query, Status = AgentRunStepStatus.Running };
steps.Add(step);
step.Output = result;
step.Duration = TimeSpan.FromSeconds(1.3);
step.Status = AgentRunStepStatus.Done;
```

### Steps

Each `AgentRunStep` has a kind, a title and a status. A thinking step carries its reasoning in `Text`, a tool call its
`ToolName`, `Input` and `Output`, a file edit its `Files` (path, created, changed or deleted, lines added and removed),
and a message its `Text`. Pressing a step's header opens its detail; messages are always open. A step that arrives
while the run is on screen rises into place, and the timeline keeps following the newest step until you scroll up.

### State, clock and cost

`State` is two-way. While it is `Running` the clock counts on its own from `Elapsed`, and the button reads Stop.
Pressing Stop raises `StopRequested`, then marks the run and any running step as stopped. A finished run offers Run
again and a stopped or failed one Retry; both raise `RetryRequested`. A failed run shows `FailureMessage` in the foot.

<Demo name="Failed" />

### A finished run

Set the stored steps, the state and the totals to show a run that already ended.

<Demo name="Completed" />

## API reference

<!-- api: AgentRun -->

<!-- api: AgentRunStep -->

<!-- api: AgentRunFile -->

<!-- api: AgentRunLabels -->

## Accessibility

- The run is a region named "Agent run". The goal is a heading.
- **Tab** reaches each step header and the Stop or Retry button. **Up** and **Down** move between step headers, **Home**
  and **End** jump to the first and the last; **Enter** or **Space** opens or closes a step.
- A step header reads its kind, title, status and time ("Tool call: Find the route, done, 1.3s") and reports itself as
  expanded or collapsed.
- A polite live region says when a step starts and when the run ends ("Completed in 1m 12s").
- Status is shown by a glyph and a word, never color alone.

## Motion

- A new step rises 6px while it fades in and its height opens.
- A step's detail opens on a spring with a short fade; the chevron turns on a snappy spring.
- The state dot and the step markers change color over 240ms.
- Reduced motion keeps short fades without movement; Off applies every change at once.

## Responsive behavior

- The run is at most 720 wide and follows its container below that.
- At 480 or less the clock, cost and tokens move under the state line and the side paddings tighten.
- The timeline scrolls past 420 high; titles and paths shorten with an ellipsis.

## Related

- `Agent tasks`, a board of runs, each of which can open this view.
- `Research run` for research agents that read sources and write a report.
- `AI chat` for conversations that call tools.
