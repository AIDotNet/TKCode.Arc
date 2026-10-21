---
title: Time picker
summary: Choose a time with sensible keyboard behavior.
---

<Demo name="TimeField" />

## When to use

- A time of day picked from evenly spaced options, such as a meeting or a reminder.
- A time that is stored as a `TimeOnly` value and shown on the clock of the user's culture.
- A form field that pairs with a `DatePicker` to make a date and time.

## When not to use

- A short list of named times, such as "Morning" or "Evening": use `Select`.
- A date: use `DatePicker`, or `Calendar` for a month that stays on the page.
- A date range: use `DateRangePicker`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TimePicker Label="Meeting time"
                    Description="Times every 15 minutes."
                    SelectedTime="{Binding MeetingTime, Mode=TwoWay}" />
</UserControl>
```

### Step and clock

`MinuteStep` sets the distance between the options. The default is 15 minutes. `ClockFormat` chooses the
display: `TwelveHour` shows "9:15 AM", `TwentyFourHour` shows "09:15", and `Auto` follows the short time
pattern of `Culture`. The value is always a `TimeOnly`, whatever the display.

<Demo name="StepsAndClocks" />

### Empty value

`SelectedTime` is a `TimeOnly?`. When it is `null`, the field shows `Placeholder`.

```xml
<arc:TimePicker Label="Reminder" Placeholder="Select a time" />
```

```csharp
picker.SelectedTime = new TimeOnly(18, 30);
picker.SelectedTimeChanged += (_, e) =>
{
    status.Text = e.NewTime is { } time ? $"Set to {time:HH:mm}" : "No time";
};
```

### Sizes and states

`Size` sets the field height: `Small` is 36, `Medium` is 44 (the Arc look) and `Large` is 50. A non-empty
`ErrorMessage` marks the field invalid. `IsEnabled="False"` disables it.

<Demo name="SizesAndStates" />

## API reference

<!-- api: TimePicker -->

## Accessibility

- `Label` names the field. `Description` and `ErrorMessage` are the help text.
- Focus stays on the trigger while the list is open. The trigger reports its expanded state and its value, so a
  screen reader announces the current time as the list changes.
- Enter, Space or ArrowDown open the list, with the chosen time in the centre and active. ArrowUp and ArrowDown
  move between the times and wrap at both ends.
- Escape closes the list. A pointer press outside it also closes it.
- A focus ring is shown on the trigger for keyboard focus.

## Motion

- The value rolls in the direction of time: a later time rises from below and an earlier one falls from above.
- The list grows from the trigger with a short spring and fades in. It closes almost at once.
- The active row changes its background quickly, and a small dot marks the selected time, so only one row is
  highlighted at a time.
- Reduced motion cross-fades the value and only fades the list in and out. Off jumps to the end state.

## Related

- `DatePicker` to pair a date with a time.
- `Select` for a short list of named choices.
- `DateRangePicker` for a range of dates.
