---
title: Task breakdown
summary: Break a large task into subtasks suggested from its description, then edit each title, estimate and assignee, reorder them by dragging or with the keys, and see the total and each person's load before creating them.
---

<Demo name="Preview" />

## When to use

`TaskBreakdown` turns a large task into a plan. The person describes the task; Suggest proposes subtasks from the
description, each with an estimate and an assignee chosen by load; then every subtask can be renamed, re-estimated,
reassigned, dragged into a new order or removed, and more can be added. The footer adds up the hours, shows each person's
load and creates the subtasks.

- **Planning a feature or a launch.** Write what has to happen and get a first list to edit.
- **Splitting a ticket that is too big.** Keep the order and the owners in one place.
- **Connecting a model.** Give `SuggestAction` your own service; the block handles the editing.

## When not to use

- **Ongoing task tracking.** Once created, track the subtasks on a `Kanban board`.
- **Dependencies and dates.** Use `Gantt timeline` when subtasks need start and end dates.
- **One-line tasks.** A breakdown adds nothing to a task that is already small.

## Usage

The block derives from `TemplatedControl`. While `IsSampleDataEnabled` is true and nothing was given, it shows an
invented task, team and suggestion. Give it `TaskTitle`, `Description` (both two-way) and `People`; the list edits
`Subtasks` in place.

```xml
<arc:TaskBreakdown TaskTitle="Move the blog" HoursPerDay="7" IsSampleDataEnabled="False" />
```

```csharp
breakdown.SuggestAction = async (request, cancellation) => await planner.SplitAsync(request.Title, request.Description, cancellation);
breakdown.Created += (_, e) => tracker.CreateSubtasks(e.Title, e.Subtasks);
```

### Suggestions

Without `SuggestAction` the built-in suggester splits the description into steps (lines, bullets, sentences, "then"),
estimates each from the kind of work it names, adds a review step when none is mentioned and spreads the work over the
people by load. `SuggestFromText` runs it without the block. While a suggestion runs `IsSuggesting` is true and the button
shows a spinner; a failure shows a short line and keeps the list.

### Editing and creating

Rows are a `ReorderableList`: drag by the grip, or use the grip's keys. `TotalHours` adds up the estimates; the total line
also gives working days at `HoursPerDay`. `Create` raises `Created` with the subtasks that have a title. Words are in
`Labels` (`TaskBreakdownLabels`).

<Demo name="OwnSuggester" />

## API reference

<!-- api: TaskBreakdown -->

<!-- api: TaskBreakdownSubtask -->

<!-- api: TaskBreakdownRequest -->

## Accessibility

- The title and description fields have labels; each row's title field is named "Subtask N", and the estimate and
  assignee are named too.
- The list's grips follow `ReorderableList`: **↑/↓** move a row, **Space** picks it up and drops it, **Escape** cancels;
  every move is announced.
- Suggestions and creation are announced politely; a failed suggestion is announced assertively.

## Motion

- Rows lift, slide and settle as `ReorderableList` does; the suggest button shows its spinner and cross-fades its label.
- Reduced motion keeps the drag and drops the springs.

## Responsive behavior

- At `CompactWidth` (600 px) or less each row puts its estimate and assignee under its title; padding tightens to 16.
- The footer's load chips wrap.

## Related

- `ReorderableList`, `NumberField` and `Select`, the controls the block is made of.
- `Kanban board` and `Gantt timeline` for tracking the result.
