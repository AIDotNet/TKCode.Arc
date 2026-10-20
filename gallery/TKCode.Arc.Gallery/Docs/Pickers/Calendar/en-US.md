---
title: Calendar
summary: Browse dates in a clear, compact month view.
---

## When to use

- A month view that stays on the page, such as a booking or availability screen.
- Choosing one date where the calendar itself is the content.
- Limiting the selectable dates with `MinDate`, `MaxDate` or `DisabledDates`.

## When not to use

- A date field in a form that opens a popup: use `DatePicker`.
- A start and end date: use `DateRangePicker`.
- A time of day: use `TimePicker`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Calendar ShowToday="True" />
</UserControl>
```

### Selected date

`SelectedDate` is a `DateOnly?` and only its date part is used. It is two-way, so a bound value follows the
user's choice. `DateSelected` is raised for a click, for Enter or Space, and for the Today button.

```xml
<arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}" />
```

```csharp
calendar.DateSelected += (_, e) => status.Text = $"Selected {e.Date:D}";
```

### Limits and disabled days

`MinDate` and `MaxDate` bound the navigation and the selection. `DisabledDates` is a `Func<DateOnly, bool>`
that returns `true` for a day that must not be picked. Disabled days are skipped when the arrow keys move
the focus.

```csharp
calendar.MinDate = today.AddDays(-3);
calendar.MaxDate = today.AddDays(45);
calendar.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
calendar.FirstDayOfWeek = DayOfWeek.Monday;
```

### Displayed month

`DisplayMonth` sets the month shown. It is two-way and only the month and year are used. `GoToMonth` moves
the view from code to a date's month.

```xml
<arc:Calendar DisplayMonth="{Binding ViewMonth, Mode=TwoWay}" />
```

### Labels and culture

`Culture` sets the language of the month and weekday names. `TodayLabel`, `PreviousMonthLabel` and
`NextMonthLabel` replace the button text and the accessible names.

```xml
<arc:Calendar TodayLabel="Heute" PreviousMonthLabel="Vorheriger Monat" NextMonthLabel="Nächster Monat" />
```

## API reference

<!-- api: Calendar -->

## Accessibility

- The calendar is named by its month title. The previous, next and Today buttons have names, and the
  previous and next buttons are disabled in the month that holds `MinDate` or `MaxDate`.
- Each day is a button with its full date as the name. The selected day is exposed as selected, and today
  is marked with its status.
- The arrow keys move the focus by day and week, and cross months. Home and End go to the first and last day
  of the week. PageUp and PageDown move by month, and Shift adds a year.
- Enter and Space select the focused day. Only one day is in the Tab order at a time.
- The month change is announced politely. Months that slide out of view are not focusable and are hidden from
  assistive technology.

## Motion

- The months slide on one strip in the direction of travel. The title slides with them, and the selected disc
  glides from day to day. The numbers change color as the disc passes them.
- A long jump, such as Today or Shift + PageDown, starts from the month next to the target and does not pass
  the months in between.
- Reduced motion jumps the months and the disc straight to the end state and replaces the title in place.
  Off does the same.

## Related

- `DatePicker` for a date field that opens the calendar in a popup.
- `DateRangePicker` for a start and end date with presets.
- `TimePicker` for choosing a time of day.
