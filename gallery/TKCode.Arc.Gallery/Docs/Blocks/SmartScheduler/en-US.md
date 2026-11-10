---
title: Smart scheduler
summary: Find a meeting time by saying what matters in plain words: the best slots appear on a week next to everyone's busy times, each with its reasons, ready to book.
---

<Demo name="Preview" />

## When to use

`SmartScheduler` finds a meeting time for a group. Pick who comes and how long it takes, then say what matters in plain
words: "afternoons, not Friday, keep lunch free". The scheduler reads those words back as chips, compares everyone's
calendars and shows the best few slots on a week view, where each cell is shaded by how many people are busy then. Each
proposal says why it was picked ("Everyone is free", "Leaves a break before and after"). Choose one on the week or in the
list and book it.

- **Team meetings.** A weekly sync, a review, an interview loop across several calendars.
- **Customer calls.** Invite colleagues and let the planner respect a "before 4pm" limit.
- **Scheduling assistants.** Plug in your own finder and keep the same week view and booking flow.

## When not to use

- **Booking a single person's free time.** A booking page with a date picker is simpler.
- **Picking a time by hand on a full calendar.** Use the `Week calendar` or `Availability picker` blocks.
- **Recurring series with complex rules.** The planner looks at one week at a time.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any data it shows an invented team and week
(`SmartSchedulerSamples`). The words are read by `SmartSchedulerPlanner.Parse` and the slots found by
`SmartSchedulerPlanner.Find` unless you set `SlotFinder`.

```xml
<arc:SmartScheduler WeekStart="2026-11-16" ConstraintsText="Afternoons, not Friday" />
```

```csharp
scheduler.People = team.Select(p => new SmartSchedulerPerson
{
    Id = p.Id, Name = p.Name, Detail = p.Role,
    Busy = calendar.BusyTimes(p.Id, week).Select(b => new SmartSchedulerBusy(b.Day, b.Start, b.End)).ToArray(),
}).ToArray();
scheduler.BookAction = (e, cancellation) => calendar.InviteAsync(e.Start, e.End, e.People, cancellation);
scheduler.SlotBooked += (_, e) => toast.Show($"Booked {e.Start:t}");
```

### What the planner understands

Mornings and afternoons; "not", "no", "except" or "avoid" with a weekday; "after 10", "after 2pm", "before 4pm"; lunch;
"asap" or "as soon as possible"; "breaks" or "buffer"; and a length such as "45 min" or "1 hour", which also sets the
length choice. Everything it understood is shown as a chip under the box, so people see what was read. The planner tries
every half hour of the working day (`DayStartHour` to `DayEndHour`) on every day of the week, keeps the slots where every
invited person is free and every limit holds, and ranks them: a break before and after, a lighter day for the group and
the time of day count, and "as soon as possible" favors the earliest. It proposes `SlotCount` slots, at most one per day
when it can.

<Demo name="Soonest" />

### States

`State` goes from `Idle` to `Finding` and then `Proposed` or `NoTimes`; booking moves through `Booking` to `Booked`, after
which "Find another time" starts over. `Slots`, `SelectedSlot` and `Constraints` report what is shown. `FindAsync()`,
`SelectSlot(index)`, `BookAsync()`, `SetInvited(person, value)` and `StartOver()` do from code what the controls do.
Changing who is invited or the length clears the proposals, since they may no longer fit.

### Words

`Labels` (`SmartSchedulerLabels`) holds the headings, the buttons, the status lines, the slot and hour formats, and
`Reasons` with the words of the planner's reasons and chips.

## API reference

<!-- api: SmartScheduler -->

<!-- api: SmartSchedulerPerson -->

<!-- api: SmartSchedulerBusy -->

<!-- api: SmartSchedulerSlot -->

<!-- api: SmartSchedulerConstraints -->

<!-- api: SmartSchedulerState -->

<!-- api: SmartSchedulerLabels -->

## Accessibility

- The scheduler is a named region; the title is a level 2 heading and the form's sections level 3 headings.
- Each person is a checkbox named by their name and described by their detail; the length is a segmented control.
- The week is one tab stop read as an image with the selected option as its help text. **Left**, **Right**, **Up** and
  **Down** move between the proposed slots, **Home** and **End** jump to the first and last.
- Each proposal is a button named "Option 1: Tue, Nov 17 · 2:00–2:30 PM", described by its reasons and reporting
  "selected".
- "Comparing calendars…", the first option, "no time works" and the booking result are announced politely.

## Motion

- The selection ring glides between slots on a snappy spring; proposals tint on hover and selection in 160 ms.
- The find and book buttons show their spinner while they work.
- Reduced motion and Off move the ring at once.

## Responsive behavior

- The scheduler is at most 1040 wide. Wide, the form is a 300 wide column beside the week. At 760 or less (`:compact`) the
  form moves above the week and the padding tightens from 24 to 16.
- The week keeps its height and divides its width between the days; hour labels stay in a 48 wide gutter.
- Chips wrap; proposals wrap their reasons. Nothing depends on hover.

## Related

- `Availability picker` for choosing a time by hand across calendars.
- `Week calendar` for a full planner with drag and drop.
- `Textarea`, `Checkbox` and `SegmentedControl`, the controls of its form.
