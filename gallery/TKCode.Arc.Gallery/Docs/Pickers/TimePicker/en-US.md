---
title: Time picker
summary: Choose a time with sensible keyboard behavior.
---

<Demo name="TimeField" />

## When to use

`TimePicker` chooses one time of day from an evenly spaced list. The list is generated every few minutes (15 by
default), so a time is always one step away from its neighbors. It sits in forms where the time is a value of its
own: a meeting slot, a reminder, a shift start, a pickup window. The value is a `TimeOnly`, and the clock shows it in
the format of the person's culture.

- **Meeting and appointment slots**: a calendar invite or a clinic booking where the times are fixed steps, such as
  every 15 minutes between 8:00 and 18:00. The list offers only the starts the grid supports, so people choose a
  time the booking system accepts.
- **Reminders and scheduled jobs**: a "send the weekly digest at" setting in an admin console. The value is a time of
  day with no date, and the picker shows it on the clock the person reads.
- **Shift and availability editors**: a staffing tool where a shift starts at 06:30 and ends at 14:30. Pair two
  pickers with `MinuteStep="30"` and validate that the end follows the start.
- **Date and time forms**: a delivery window or an event start that combines a `DatePicker` for the day with a
  `TimePicker` for the time. The two values are stored separately and combined in code.

## When not to use

- **A short list of named times, such as Morning or Evening**: use `Select`. A list of names does not need a scrolling
  list of times, and "Morning" has no single `TimeOnly` value.
- **A date**: use `DatePicker`, or `Calendar` when the month must stay visible on the page.
- **A date range**: use `DateRangePicker`. A range of whole days has no time of day and no step list.
- **Free typing of a time that must be exact to the second**: the picker is a list of steps. Use `Input` with a
  validation rule when people must type seconds or times between steps, and parse the text in the view model.
- **A duration, such as a 90 minute meeting or a 2 hour timer**: use `NumberField` for the number of minutes. A
  duration is not a time of day, and the list would suggest a clock that does not apply.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TimePicker Label="Meeting time"
                    Description="Times every 15 minutes."
                    SelectedTime="{Binding MeetingTime, Mode=TwoWay}" />
</UserControl>
```

`TimePicker` derives from Avalonia's `TemplatedControl` and implements the Arc picker automation. The trigger is a
`FieldFrame` inside a `Field`, so the label, the description and the error row come from the same foundation as the
input and the date pickers. The list is a drawn panel in a popup that the control owns. It draws only the rows that
are visible, so even a one minute step (1440 options) does not create 1440 controls. Because the control is a list, it
does not parse typed text: the value only changes when a row is chosen or when code sets `SelectedTime`.

### Anatomy

1. **Label** (`Label`): above the trigger, in the field label style. It is also the accessible name of the field.
   With `IsLabelVisible="False"` the label is hidden and still names the field.
2. **Trigger frame** (the template part named by `TimePicker.FramePart`): a 1 px border on the field surface. Its height follows `Size`: 36 px for `Small`,
   44 px for `Medium` (the default) and 50 px for `Large`. Hover, press and open change the border to the strong
   border color and the background to the muted surface; an invalid field shows the danger border.
3. **Clock icon**: 16 px, in the muted text color, with a 9 px gap before the value.
4. **Value** (the template part named by `TimePicker.ValuePart`): the time in 14 px text with a 19.6 px line height (14 px × 1.4). `Large` uses the base
   type size. With no time it shows `Placeholder` in the muted text color.
5. **Chevron**: 16 px, in the muted text color. It turns 180 degrees while the list is open.
6. **Description** (`Description`) and **error row** (`ErrorMessage`): help copy and error copy under the frame. A
   non-empty `ErrorMessage` makes the field invalid, and the frame shows the danger border.
7. **List** (the template part named by `TimePicker.PopupPart`): opens 6 px below the trigger with the trigger's width. It has a 1 px border, the panel
   radius, a 5 px inset and the raised surface. It is at most `MaxDropDownHeight` tall (250 px by default), then
   scrolls.
8. **Option rows**: 36 px tall, with a 12 px inset and rounded corners. The row under the pointer or the keyboard
   gets the muted surface. The chosen time has a 6 px dot at the right edge, so only one row is ever highlighted as
   the chosen one.

### Step and clock

`MinuteStep` sets the distance between the options. The default is 15 minutes, and Arc uses 1, 5, 10, 15 or 30. The
number of options is 1440 divided by the step, rounded up.

| `MinuteStep` | Options | Use it for |
|---|---|---|
| 5 | 288 | Call slots and short appointments |
| 15 | 96 | Meetings, reminders and most schedules (the default) |
| 30 | 48 | Shift starts and opening hours |

`ClockFormat` chooses the display. `Auto` (the default) follows the short time pattern of `Culture`: a culture that
writes the hour as "H" shows 24 hours, and any other culture shows 12 hours with AM and PM. `TwelveHour` shows
"9:15 AM" and `TwentyFourHour` shows "09:15" whatever the culture. The AM and PM words come from the culture, and
fall back to AM and PM when the culture has none. The value is always a `TimeOnly`, whatever the display.

<Demo name="StepsAndClocks" />

Use `Auto` in an application that serves several regions, so that each person reads times the way they write them.
Force `TwentyFourHour` in an operations console, a timetable or a log view where the 24-hour clock is the standard.
Use `TwelveHour` only when the product's copy is 12-hour everywhere, because a mixed display confuses schedules.

### Empty value and default

`SelectedTime` is a `TimeOnly?`. Arc's default is 09:00 (the property starts at 9:00 AM), so a new picker shows a time
even when nothing was chosen. When the value is `null`, the field shows `Placeholder`, which defaults to `Select a
time`. Use `null` for a field that the person must set, and set a real default only when a time is usually right.

```xml
<arc:TimePicker Label="Reminder" Placeholder="Select a time" SelectedTime="{x:Null}" />
```

```csharp
picker.SelectedTime = new TimeOnly(18, 30);
picker.SelectedTimeChanged += (_, e) =>
{
    status.Text = e.NewTime is { } time ? $"Set to {time:HH:mm}" : "No time";
};
```

A time that is not on a step is kept as it is: with a 15 minute step, `09:07` shows `9:07 AM` on the trigger, and the
list highlights the option at or just before it (09:00). Only the choices from the list land on the step.

<Demo name="SizesAndStates" />

### Sizes and states

`Size` sets the height of the trigger: `Small` is 36 px, `Medium` is 44 px (the Arc look) and `Large` is 50 px. Keep one
size per form, so the time fields line up with the input and the date fields beside them. A non-empty `ErrorMessage`
marks the field invalid, and `IsEnabled="False"` disables it: the field is still visible and reads its value, but
nothing opens.

### Binding to a view model

The view model owns the time and the rule that goes with it. With CommunityToolkit.Mvvm:

```csharp
public partial class ShiftViewModel : ObservableObject
{
    private readonly IShiftService _shifts;

    public ShiftViewModel(IShiftService shifts)
    {
        _shifts = shifts;
    }

    [ObservableProperty]
    private TimeOnly? _shiftStart = new TimeOnly(6, 30);

    [ObservableProperty]
    private TimeOnly? _shiftEnd = new TimeOnly(14, 30);

    [ObservableProperty]
    private string? _shiftEndError;

    partial void OnShiftStartChanged(TimeOnly? value) => ValidateShift();

    partial void OnShiftEndChanged(TimeOnly? value) => ValidateShift();

    private void ValidateShift()
    {
        ShiftEndError = ShiftStart is { } start && ShiftEnd is { } end && end <= start
            ? $"End must be after the start, {start:HH:mm}."
            : null;
    }

    [RelayCommand]
    private Task SaveShiftAsync()
    {
        if (ShiftStart is not { } start || ShiftEnd is not { } end || ShiftEndError is not null)
        {
            return Task.CompletedTask;
        }
        return _shifts.SaveAsync(start, end);
    }
}
```

```xml
<StackPanel Width="560" Spacing="20">
    <arc:TimePicker Label="Shift start"
                    SelectedTime="{Binding ShiftStart, Mode=TwoWay}"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:TimePicker Label="Shift end"
                    SelectedTime="{Binding ShiftEnd, Mode=TwoWay}"
                    ErrorMessage="{Binding ShiftEndError}"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:Button Content="Save shift" Variant="Primary" Command="{Binding SaveShiftCommand}" />
</StackPanel>
```

`SelectedTime` is the only property that carries the value, and it is two-way by default, so the explicit
`Mode=TwoWay` is optional. `SelectedTimeChanged` fires with the old and new times when the value changes by a choice
or by code; use it for side effects such as updating a summary, and bind the value otherwise. The control has no
command of its own. Compiled bindings need `x:DataType` on the view; the example leaves it out for brevity.

### Validation

Times are checked against the rules of the schedule, not against the picker. Put each rule in the view model, and show
the first message in `ErrorMessage` under the field that it concerns.

| Rule | When to check | Where it shows |
|---|---|---|
| End after start | When either time changes | `ErrorMessage` on the end field |
| Within opening hours | When the time changes | `ErrorMessage` on the field the time belongs to |
| Required | On submit, then while the value is `null` | `ErrorMessage`; focus the first empty field |
| Server rejects the time | After the request | `ErrorMessage` on the field it is about; clear it when the value changes |

Clear the message when the value changes, as the `OnShiftStartChanged` and `OnShiftEndChanged` handlers do, so the
error never stays on a value that was already fixed.

### Focus and dismissal

The trigger keeps focus while the list is open. The list is a popup with light dismiss, so a press outside it closes
the list. Choosing a row or pressing Escape closes it too; in each case the focus stays on the trigger and nothing else
moves. Enter or Space on a row chooses it and closes the list, so a keyboard user never needs a separate confirm button.

### Driving it from code

The list is generated from `MinuteStep`, so changing the step or the culture regenerates it while the picker is
closed. The options can be read from code when a page needs them as text:

```csharp
var labels = new List<string>();
for (var i = 0; i < picker.OptionCount; i++)
{
    labels.Add(picker.FormatTime(picker.OptionAt(i)));
}
```

`FormatTime` returns the text the trigger and the list use for the current `ClockFormat` and `Culture`, so a
summary line matches the picker exactly. To combine the time with a `DatePicker` value, use the date's own
conversion in the app: `date.ToDateTime(time)`.

### Example: shift start and end

A staffing tool where a manager sets the start and the end of a shift in half hour steps. The two fields share a size
and a width, the end field shows the error when it is not after the start, and the description on the start field
says what the step means. The save button is the only action, so the form has one primary button.

![Shift start and end times with an error on the end time](images/ShiftTimes.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:TimePicker Label="Shift start"
                    Description="Shifts start on the hour or the half hour."
                    SelectedTime="06:30"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:TimePicker Label="Shift end"
                    ErrorMessage="End must be after the start, 06:30."
                    SelectedTime="06:00"
                    MinuteStep="30"
                    Width="240"
                    HorizontalAlignment="Left" />
    <arc:Button Content="Save shift" Variant="Primary" HorizontalAlignment="Left" />
</StackPanel>
```

### Content guidelines

Name the field for the event or the value: `Meeting time`, `Shift start`, Reminder. Keep the placeholder as a short
verb phrase, and write the description as a fact about the step, not a restatement of the label.

| Instead of | Write |
|---|---|
| `Time` | `Meeting time`, `Shift start`, Reminder |
| `Pick a time please` | `Select a time` (the default placeholder) |
| `Times are 15 min apart.` | `Times every 15 minutes.` |
| `Invalid time` | `End must be after the start, 06:30.` |
| `12:00 AM` in a 24-hour product | `00:00` |

Write error messages with the rule and the fix: say what the time must be, not only that it is wrong.

### Best practices

**Do**

- Use `MinuteStep` for the real granularity of the schedule. A 15 minute list suits meetings, and a 30 minute list
  stops people from picking times the business does not offer.
- Keep `Size` the same as the other fields in the form, so that a row of a date and a time lines up.
- Use `null` for a value the person must choose, and show the placeholder, so that the default 09:00 is never saved
  by accident.
- Validate ranges in the view model and show the message on the field it concerns, so the person knows which time to
  change.
- Use `Auto` when the product serves several regions, and `TwentyFourHour` when the domain uses it.

**Avoid**

- Using the picker for a list of named periods. A list of "Morning" or "Evening" entries should be a `Select`,
  because a time picker suggests a clock that the periods do not have.
- Storing a time as text. The value is a `TimeOnly`; format it only for display, so comparisons and sorting stay
  correct.
- Using a step other than the ones Arc defines (1, 5, 10, 15 and 30). Stick to them so that the list reads as a clock
  people already know.
- Showing the error only on submit when the rule can be checked as soon as a time changes. The person can fix a
  shift end at once when the message is next to the field.
- Expecting one value for the date and the time. The control holds only a `TimeOnly`, so keep the day in a
  `DatePicker` and join the two in code with `date.ToDateTime(time)`.

### Common questions

**Why does the field show 9:00 AM when I did not set a time?** The control's default is 09:00, which Arc uses so that
the field always reads as a time. Set `SelectedTime` to `null` to show the placeholder instead.

**Can people type a time?** No. The trigger opens a list, and the value changes only when a row is chosen or code sets
`SelectedTime`. Use `Input` with a rule when typed input is required.

**What happens when the time is not on the step?** The value is kept as set, so `09:07` stays `09:07` and shows as
`9:07 AM`. The list highlights the option at or before the time, which is 09:00 with a 15 minute step. Round the value
in the view model if the business needs an exact step.

**How do I store the value as "HH:mm" text?** The value is a `TimeOnly`, so the text is only a format of it:
`SelectedTime?.ToString("HH:mm")` in the app, or `picker.FormatTime(time)` for the display text.

**Does the list scroll the page?** No. The list scrolls by itself, and opening it centres the chosen time in the list,
so the page does not move.

## API reference

<!-- api: TimePicker -->

## Accessibility

- `Label` names the field. `Description` and `ErrorMessage` are the help text.
- Focus stays on the trigger while the list is open. The trigger reports its expanded state and its value, so a
  screen reader announces the current time as the list changes.
- The trigger is exposed as a combo box whose name is the label and whose value is the formatted time, or the
  placeholder when there is no time.
- Enter, Space or ArrowDown open the list, with the chosen time in the centre and active. ArrowUp and ArrowDown
  move between the times and wrap at both ends.
- Escape closes the list. A pointer press outside it also closes it.
- A focus ring is shown on the trigger for keyboard focus.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` (list closed) | Opens the list with the chosen time active |
| `Enter` / `Space` (list open) | Chooses the active time and closes the list |
| `ArrowDown` / `ArrowUp` (list closed) | Opens the list |
| `ArrowDown` / `ArrowUp` (list open) | Moves the active time down or up, wrapping from the last time to the first and back |
| `Home` / `End` (list open) | Moves to the first time (12:00 AM or 00:00) or the last time of the day |
| `Escape` (list open) | Closes the list and keeps focus on the trigger |

The pointer does the same as the keyboard: moving over a row makes it active, and a click chooses it.

## Motion

- The value rolls in the direction of time: a later time rises from below and an earlier one falls from above.
- The list grows from the trigger with a short spring and fades in. It closes almost at once.
- The active row changes its background quickly, and a small dot marks the selected time, so only one row is
  highlighted at a time.
- Reduced motion cross-fades the value and only fades the list in and out. Off jumps to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The value changes | The time rolls in the direction of time, with a soft blur | Cross-fade | Immediate |
| The list opens | Grows from the trigger, 6 px up and 0.97 scale to full size, on a snappy spring with a fast fade | Fades in | Immediate |
| The list closes | Lifts 4 px and scales to 0.98, then closes almost at once | Fades out | Immediate |
| The active row moves with the keyboard | The list glides to keep the row in view on a smooth spring | Positioned at once | Immediate |

## Related

- `DatePicker` to pair a date with a time.
- `Select` for a short list of named choices.
- `DateRangePicker` for a range of dates.

| Component | Choose it when |
|---|---|
| `DatePicker` | The value is a day, and a time is stored separately or joined in code |
| `DateRangePicker` | The value is a span of whole days |
| `Select` | The choices are a short list of named times or periods |
| `Input` | People must type a value that the list cannot express, with a validation rule |
| `NumberField` | The value is a duration in minutes, not a time of day |
