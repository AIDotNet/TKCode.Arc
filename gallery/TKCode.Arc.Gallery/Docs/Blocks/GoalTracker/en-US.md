---
title: Goal tracker
summary: Team goals and key results for a period: progress bars marked where each result should be by today, owners, confidence, inline check-ins with a value and a note, and a timeline against the line to target.
---

<Demo name="Preview" />

## When to use

`GoalTracker` follows a team's goals and their key results over a period such as a quarter. Every key result has a start
value, a target and a current value, so its progress bar shows how far it has come, and a mark on the bar shows where it
should be by today if progress were even. The owner's confidence (on track, at risk, off track) colors the bar. A check-in
records a new value, a confidence and a note in place; the timeline view draws all check-ins against the straight line to
each target.

- **Weekly check-ins.** Owners update their key results without leaving the page.
- **Leadership reviews.** The counts by confidence and the overall bar summarize the period.
- **Spotting drift early.** A bar behind its today mark, or a timeline below its line, needs attention.

## When not to use

- **Task lists.** Use `Kanban board` or `Project board` for work items.
- **Yes/no milestones.** A key result needs a number; use `Release readiness` for a checklist.
- **Company-wide trees of goals.** The block shows one level of goals with their key results.

## Usage

The block derives from `TemplatedControl`. Without goals it shows invented goals for the current quarter. Give it
`Goals`, `PeriodStart` and `PeriodEnd`; owners are `ProjectPerson`s.

```xml
<arc:GoalTracker Title="Support goals" View="Timeline" CurrentUser="Mia" />
```

```csharp
var reply = new GoalTrackerKeyResult("First reply under 2 hours", 35, 90, 62, "%") { Owner = mia };
tracker.Goals.Add(new GoalTrackerGoal("Answer every customer faster", "Support", mia) { KeyResults = [reply] });
tracker.CheckedIn += (_, e) => okrs.Save(e.KeyResult, e.CheckIn);
```

### Check-ins

"Check in" opens an editor under the key result: a value, a confidence and a note. Saving adds a `GoalTrackerCheckIn`
dated `Today` and signed with `CurrentUser`, updates `Current` and `Confidence`, and raises `CheckedIn`. `CheckIn` does
the same from code; `OpenCheckIn` opens the editor. Escape closes it.

### Teams and views

`TeamFilter` (two-way) narrows the goals to one team; the team picker lists the teams the goals name. `View` switches
between the list and the timeline. Words are in `Labels` (`GoalTrackerLabels`).

<Demo name="Timeline" />

## API reference

<!-- api: GoalTracker -->

<!-- api: GoalTrackerGoal -->

<!-- api: GoalTrackerKeyResult -->

<!-- api: GoalTrackerCheckIn -->

<!-- api: GoalTrackerConfidence -->

<!-- api: GoalTrackerView -->

## Accessibility

- Goal titles are level 3 headings; the block's title is level 2.
- Each progress bar is a read-only progress bar from 0 to 100, named by the key result and its value.
- The check-in editor moves the focus to its value field; Escape closes it; the result is announced.
- The view switch and team picker are named "View" and "Team".

## Motion

- Progress bars spring to a new value after a check-in (Smooth, no overshoot).
- Reduced motion and Off place the new value at once.

## Responsive behavior

- At `CompactWidth` (640 px) or less key result titles wrap and the check-in editor stacks its fields; padding tightens to 16.
- The timeline's label gutter never takes more than 40% of the width.

## Related

- `Progress` and `SegmentedControl`.
- `Release readiness` and `Gantt timeline`, the other Projects blocks.
