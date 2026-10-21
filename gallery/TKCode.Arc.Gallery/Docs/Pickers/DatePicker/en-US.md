---
title: Date picker
summary: Choose a date without losing context.
---

<Demo name="DateField" />

## When to use

- A date field in a form, such as a start date or a due date.
- A date that the user can also type, checked against limits as it is entered.
- A date that must stay inside a range or skip some days, through `MinDate`, `MaxDate` and `DisabledDates`.

## When not to use

- A calendar that stays visible on the page: use `Calendar`.
- A start and end date: use `DateRangePicker`.
- A time of day, or a date and time together: use `TimePicker` next to a `DatePicker`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DatePicker Label="Start date"
                    Description="Type a date or press Alt+Down to open the calendar."
                    SelectedDate="{Binding StartDate, Mode=TwoWay}" />
</UserControl>
```

### Limits and disabled days

`MinDate` and `MaxDate` bound the dates that can be picked. `DisabledDates` is a `Func<DateOnly, bool>` that
returns `true` for a day to block. The limits apply to the calendar and to typed dates. A typed date that is
out of range is rejected: the field shakes and returns to the last valid value.

<Demo name="LimitsAndDisabledDays" />

### Sizes

`Size` sets the field height: `Small` is 36, `Medium` is 44 (the Arc look) and `Large` is 50.

<Demo name="Sizes" />

### Culture and format

`Culture` controls the parsing, the month names and the weekday initials. `DisplayFormat` takes a .NET date
pattern and replaces the culture's default display.

<Demo name="CulturesAndFormats" />

### Error and disabled state

A non-empty `ErrorMessage` marks the field invalid. A disabled field is faded to half opacity.

<Demo name="InvalidAndDisabled" />

### Reacting to changes

`SelectedDateChanged` gives the old and the new date. `ClearText`, `EmptyStatusText` and
`SelectedStatusFormat` set the footer copy of the popup.

```csharp
picker.SelectedDateChanged += (_, e) =>
{
    status.Text = e.NewDate is { } d ? $"Start: {d:yyyy-MM-dd}" : "No start date";
};
```

## API reference

<!-- api: DatePicker -->

## Accessibility

- `Label` names the field and is linked to the trigger. `Description` is the help text.
- The trigger exposes its expanded state. The popup is named after the label, for example "Start date calendar".
- Enter, Space and ArrowDown open the calendar and move the focus to the selected day, or to today when nothing is
  selected. Inside the calendar the keys work as in `Calendar`.
- Escape closes the calendar and returns the focus to the field. Tab out of the calendar, or a pointer press
  outside it, also closes it.
- A focus ring is shown for keyboard focus only. A pointer that opens the calendar does not show it.

## Motion

- Changed parts of the date roll in the direction of time: a later date rises and an earlier date falls. Only the
  parts that change move.
- After a date is picked, the disc slides into place and the popup closes a moment later.
- The popup grows from the calendar icon with a short fade, and closes with a shorter fade.
- Reduced motion swaps the values with a fade and closes the popup at once after a pick. Off jumps to the end state.

## Related

- `Calendar` for a month view that stays on the page.
- `DateRangePicker` for a start and end date with presets.
- `TimePicker` for the time of day, to pair with a date.
- `Field` and `FieldFrame` give any editor or trigger the same label, frame and message rows.
