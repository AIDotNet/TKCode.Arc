---
title: Gantt timeline
summary: A project timeline of phases and tasks on a calendar: drag bars to move or resize them, dependency arrows that turn red on conflicts, day, week and month zoom, folding phases and undo.
---

<Demo name="Preview" />

## When to use

`GanttTimeline` plans work across weeks or months where tasks depend on each other. Tasks sit in phases; each phase row
shows one summary bar, folds to hide its tasks, and counts them. Bars show their progress, arrows join a task to the
tasks it depends on, and a today line marks where the plan stands.

- **Roadmaps people reschedule.** Drag a bar to move it, or one of its ends to change a date; changes snap to whole days
  and can be undone.
- **Plans with dependencies.** An arrow turns red when a task starts before one it depends on has ended.
- **Phase-based plans.** Fold finished phases into their summary bar to keep the rest in view.

## When not to use

- **Status without dates.** Use a project board.
- **Time slots inside days.** Use a week calendar.
- **A read-only list of dated events.** A plain timeline is lighter.
- **Hundreds of tasks.** Arrows are redrawn every frame while something moves; keep plans to a few dozen tasks.

## Usage

Set `Groups` (the phases, in order) and `Tasks`. A `GanttTask` has an `Id`, a `Name`, the `GroupId` of its phase, a `Start`
and an `End` (the last day, included), a `Progress` from 0 to 1, an optional `Assignee`, the ids it `DependsOn`, and
`IsMilestone` for a one-day marker. `Today` places the today line and the first scroll position.

The timeline keeps its own copy of the tasks and reports each edit through `TasksChanged`; `Undo` and `Reset` in the footer
step back through those edits. `Zoom` switches between `Day`, `Week` and `Month`, keeping the middle of the view fixed.
`CollapsedGroups` holds the ids of folded phases.

### Keyboard

| Key | Action |
|---|---|
| Tab | Moves into the chart; one bar is a tab stop |
| Arrow Down / Up | Moves to the next or previous visible bar |
| Alt + Arrow Left / Right | Moves the focused task one day |
| Alt + Shift + Arrow Left / Right | Changes the focused task's end date by one day |
| Enter / Space | Selects the bar and highlights its arrows |
| Escape | Clears the selection |

## API reference

<!-- api: GanttTimeline -->

## Accessibility

Bars are buttons named "Task bar" or "Milestone", with a pressed state for selection and a label that gives the name, the dates, the percent done and a warning when a dependency is broken. Folded rows are hidden from assistive technology. The footer status line is a polite live region that reports each move, undo and conflict. The calendar header, grid lines, arrows and summary bars are decorative.

## Motion

After a drag the bar springs to its snapped day from where the pointer released it. Shortening a bar below one day
resists with a soft rubber band. Zoom glides on a spring around the middle of the view, and Today glides the today line into view. Folding a phase slides its rows up
into the phase row. With reduced motion everything jumps to its target.

## Responsive behavior

- The chart measures its own width, not the window's. Below 560 px the task column narrows from 248 px
  (`TaskColumnWidth`) to 132 px (`NarrowTaskColumnWidth`), and the calendar takes the rest of the width.
- The calendar scrolls sideways and downward inside the chart, with a scroll bar on each side that does not fit. A Shift and wheel gesture scrolls it sideways.
- The heading and the zoom switches share one row. At phone width the heading is cut short beside them, so give the
  timeline a short `Title`.
- Touch: task rows are 40 px high (`GanttModel.RowHeight`) and the zoom switches are 32 px (`SwitchButtonHeight`), below
  a comfortable touch target. Give the timeline the full width and a fixed height, and use `DefaultChartHeight` only
  where the parent gives none.

## Related

- Project board for status columns without dates.
- Week calendar for time slots inside days.
- Kanban board for cards that move between columns.
