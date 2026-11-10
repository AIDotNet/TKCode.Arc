---
title: Agent orchestrator
summary: A lead agent handing work to sub-agents, shown live: a graph where every task, question and result travels as a dot between the agents, a message log, and a detail panel with each agent's task, current step and result.
---

<Demo name="Preview" />

## When to use

Agent orchestrator shows one multi-agent run while it happens. A lead agent takes a goal, splits it into tasks and
hands each task to a sub-agent. The card draws the lead at the top and a node per sub-agent below it, with a line
from the lead to each one. Every message between two agents travels along its line as a dot: down for a task, back up
for a result, sideways when two sub-agents ask each other something. Each node shows the agent's name, role, a status
(idle, thinking, working, waiting, done or failed) and what it is doing now. Under the graph a log lists every message
in order, and a detail panel shows the selected agent's task, current step, message counts and result.

- **A run view in an agent product**: people see who is working on what, and where a run is stuck.
- **A trace you replay**: feed recorded steps as a `Script` and play, pause or reset them.
- **A live run**: post each message from your own events with `Post`; the agents update from the message kind.
- **A demo of a multi-agent feature**: with no data the card plays an invented sample run.

## When not to use

- **A single assistant answering**: use `AiChat` or `AiAnswer`; there is nothing to delegate.
- **A large workflow with many steps and branches**: the graph is built for one lead and up to about six sub-agents.
  Use a flow editor or a table for big pipelines.
- **Logs to search or filter**: the log is a readable story of one run, not a log viewer. Use a table.
- **Running the agents**: the card shows a run. Calling models and tools is your code.

## Usage

`AgentOrchestrator` derives from Avalonia's `TemplatedControl`. Agents are `OrchestratorAgent` objects with an id, a name,
a role and a model. The first agent is the lead unless `LeadId` names another one.

```xml
<arc:AgentOrchestrator Goal="Plan the team offsite" IsAutoStart="False">
    <arc:OrchestratorAgent Id="lead" Name="Atlas" Role="Planner" />
    <arc:OrchestratorAgent Id="venue" Name="Scout" Role="Venues" />
    <arc:OrchestratorAgent Id="budget" Name="Ledger" Role="Budget" />
</arc:AgentOrchestrator>
```

### Anatomy

- **Header**: the title with a status pill (Running, Paused, Complete, Failed), the goal, a progress line ("2 of 5 agents
  done · 9 messages"), and the Reset and Run buttons.
- **Graph**: the lead with a Lead tag, the sub-agents, the lines between them and the travelling dots.
- **Message log**: sender, receiver, kind, run time and text of every message, newest at the end.
- **Detail panel**: the selected agent (the lead until you choose another) with its status, model, message counts,
  task, current step and result.

### Driving the run from your code

Without a `Script`, your code drives the run. `Post` adds a message and applies what its kind means; `SetStatus` changes
an agent directly; `Complete` and `Fail` end the run.

<Demo name="Programmatic" />

```csharp
orchestrator.Start();
orchestrator.Post(new OrchestratorMessage
{
    From = "lead", To = "venue", Kind = OrchestratorMessageKind.Delegate, Text = "Find three venues near the lake"
});
orchestrator.SetStatus("lead", OrchestratorAgentStatus.Waiting, "Waiting on two agents");
```

| Kind | What it changes |
|---|---|
| `Delegate` | The receiver's task becomes the text and it starts working |
| `Progress` | The sender's current step becomes the text |
| `Question` | The sender waits |
| `Answer` | The receiver (who asked) works again |
| `Result` | The sender is done; its result is the text |
| `Error` | The sender failed; its result is the text |

A message can only be posted once; post `message.Copy()` to send the same text again. Agents are `AvaloniaObject`s, so
setting `Status`, `Task`, `Activity` or `Result` on them directly also updates the card.

### Playing a script

`Script` is a list of `OrchestratorStep`s: after its `Delay`, a step posts a message or sets an agent's status.
`OrchestratorStep.Send` and `OrchestratorStep.Set` build them. `Speed` scales every delay, `IsAutoStart` plays the script
when the card is shown, and the Run button pauses, resumes and plays it again. A paused step starts its wait again on
resume. With the sample agents and no script, the card plays `AgentOrchestratorSamples.Script()`.

<Demo name="Recovery" />

### Selecting an agent

Click a node, or focus it and press Enter or Space. `SelectedAgentId` is two-way; null shows the lead.

### Narrow containers

The card follows its own width. When the sub-agents no longer fit side by side, they stack under the lead.

<Demo name="Narrow" />

### Wording

Every word comes from `Labels` (`AgentOrchestratorLabels`). Texts with values are functions, so the word order can change.

```csharp
orchestrator.Labels = new AgentOrchestratorLabels
{
    Title = "Équipe d'agents",
    StatusName = status => status == OrchestratorAgentStatus.Done ? "Terminé" : status.ToString()
};
```

### Content guidelines

- **Name agents by what they do**, with a short role: "Scout · Market research".
- **One sentence per message**, written as the agent would say it: "Checked two of three rival stores".
- **Keep tasks short**: the node shows two lines of what an agent is doing.

## API reference

<!-- api: AgentOrchestrator -->

<!-- api: OrchestratorAgent -->

### OrchestratorMessage

| Property | Description |
|---|---|
| `From`, `To` | The ids of the sender and the receiver |
| `Kind` | `Delegate`, `Progress`, `Question`, `Answer`, `Result` or `Error` |
| `Text` | One sentence |
| `Sequence` | Its place in the run, from 1 (set when posted) |
| `Elapsed` | Run time when it was posted (set when posted) |

### OrchestratorStep

| Property | Description |
|---|---|
| `Delay` | Wait after the previous step, at speed 1 |
| `Message` | A message to post, or null |
| `AgentId`, `Status`, `Activity`, `Result` | An agent to change, or null |

### Methods

| Method | Does |
|---|---|
| `Start()` | Starts the run (resumes a paused one, starts a finished one again) |
| `Pause()`, `Resume()`, `Toggle()` | Pause, resume, or what the Run button does |
| `Reset()` | Clears the run: every agent idle, no messages |
| `Complete()`, `Fail()` | Ends the run |
| `Post(message)` | Posts a message |
| `SetStatus(id, status, activity)` | Changes an agent |

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `MessagePosted` | A message was posted | `Message` |
| `AgentStatusChanged` | An agent's status changed | `Agent`, `OldStatus`, `NewStatus` |
| `RunStateChanged` | The run started, paused, ended or was reset | `OldState`, `NewState` |

## Accessibility

- The graph is a list named "Agents"; each node is a list item named with the agent's name, role, status and current
  step ("Scout, Market research, Working: Checked two of three rival stores"), and the name updates as the run goes on.
- The status is never color alone: each status has its own icon (spinner, hourglass, check, cross) and a word.
- A polite live region says each status change ("Scout is done") and the start, pause and end of the run.
- The log rows are named "Atlas to Scout, Task: …"; the Run button's name follows its word, and Reset has a name and a tooltip.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Reset, Run, then the graph (one stop), then the log |
| `ArrowLeft` / `ArrowRight` | Previous or next node (in the stacked layout also `ArrowUp` / `ArrowDown`) |
| `ArrowUp` / `ArrowDown` | From a sub-agent to the lead, from the lead to the selected sub-agent |
| `Home` / `End` | The lead, the last sub-agent |
| `Enter` or `Space` | Selects the focused node |

## Motion

- Each message sends a dot along its line in 900 ms on an ease-in-out curve, fading in and out at its ends, with a soft halo.
- A line to a working agent turns to the accent; done lines turn green, failed lines red and dashed.
- Working and thinking nodes show a turning spinner; node borders and backgrounds change over 160 to 240 ms.
- The status pill crossfades its word and turns its spinner while running.
- Reduced motion shows no dots and keeps every state change; borders change at once. Off applies everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Message | Dot travels 900 ms | No dot | No dot |
| Node status | Border fades; spinner turns | Immediate | Immediate |
| Line state | Immediate | Immediate | Immediate |

## Responsive behavior

- The card is capped at 960 px and follows its own width, not the window's.
- When every sub-agent fits side by side at 156 px or more, they fan out under the lead. Otherwise they stack under it
  as a tree with a trunk line on the left, and the nodes show one line of text.
- At 640 px or less (`:compact`) the paddings tighten and the detail panel moves above the log.
- The log is 260 px high, so the card keeps its height while a run plays; it scrolls inside and follows new messages unless you scrolled up.

## Related

- `AiChat` for one assistant in a conversation.
- `AgentMemory` for what an assistant remembers.
- `ModelArena` for comparing two models on one prompt.

| Component | Choose it when |
|---|---|
| `AiChat` | One assistant talks with one person |
| `ModelArena` | Two models answer the same prompt |
| `Timeline` | Events in time order without agents |
