---
title: Agent tasks
summary: A board of tasks handed to AI agents: Queued, Running, Needs you and Done columns of cards with the owner agent, progress or question, an agent filter, and a press that opens the task's run.
---

<Demo name="Preview" />

## When to use

`AgentTasks` is the overview of work people have handed to AI agents. Each task is a card in one of four columns:
waiting to start, running, needing a person (a question to answer or an error to look at), and done. A running card
shows its progress and current step, a waiting card the agent's question, and every card the agent that owns it and
when it last changed. Chips at the top narrow the board to one agent. Pressing a card opens that task's run.

- **A team's agent workspace**, where several agents work in parallel on delegated tasks.
- **An inbox of agent work** that needs a person from time to time.

## When not to use

- **One run in detail.** Use `Agent run`.
- **Approving actions** an agent wants to take. Use `Agent approvals`.
- **Projects planned and moved by people.** Use `Kanban board` or `Project board`, which support dragging.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it shows sample tasks whose running ones
progress on their own. In an app, give it your tasks (an `ObservableCollection<AgentTaskItem>` is followed as tasks
come and go) and open a run when a card is pressed:

```xml
<arc:AgentTasks Tasks="{Binding Tasks}" />
```

```csharp
board.TaskOpened += (_, e) => navigation.OpenRun(e.Task.Id);
task.Progress = 0.4;
task.Status = AgentTaskStatus.NeedsInput; // the card moves to Needs you
```

### Columns and cards

The column follows `Status`: `Queued`, `Running`, `NeedsInput` and `Failed` (both in Needs you, Failed with a red tag),
and `Done`. A running card shows `Progress` as a bar and a percentage; `Note` holds the current step, the question or the
error. `Updated` shows as a relative time. `AgentFilter` (two-way) keeps the board to one agent; the chips list every
agent on the board.

<Demo name="OpenRun" />

## API reference

<!-- api: AgentTasks -->

<!-- api: AgentTaskItem -->

<!-- api: AgentTasksLabels -->

## Accessibility

- The board is a region named "Delegated tasks"; each column is named with its title and count.
- **Tab** reaches the chips and the cards. **Up** and **Down** move within a column; **Left** and **Right** move to the
  nearest card of the next column; **Home** and **End** go to the ends of a column. **Enter** or **Space** opens a run.
- A card reads its title, agent, status and progress or note, and hints "Press Enter to open the run".
- A polite live region says when a task moves to another column.

## Motion

- Cards scale slightly while pressed and their border changes on hover; done cards fade a little.
- Reduced motion keeps color changes short; Off applies them at once.

## Responsive behavior

- The board fills its container.
- At 720 or less the columns stack, and Up and Down continue into the next column.
- Titles take two lines at most; notes shorten with an ellipsis.

## Related

- `Agent run`, the view a card opens.
- `Agent approvals` for actions waiting for a person.
- `Agent routines` for scheduled agent work.
