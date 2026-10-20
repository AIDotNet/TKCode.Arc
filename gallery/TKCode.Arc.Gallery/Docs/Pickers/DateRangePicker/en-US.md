---
title: Date range picker
summary: A range picker that grows from its trigger into two months with presets and a stretching range highlight.
---

## When to use

- Report and analytics filters with presets such as Last 7 days or This quarter.
- A start and end date for a booking or a leave request.
- A compact toolbar where the range is shown as one short label until it is changed.

## When not to use

- A single date: use `DatePicker`.
- A month view that stays on the page: use `Calendar`.
- A start and end time of day: use `TimePicker`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DateRangePicker Label="Date range" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

### Presets

`Presets` replaces the default list of eight presets. Each preset is a label and a function that returns a
`DateRange` from the current day. `DateRangePresets.Default` holds the default list, so a custom list can
extend it.

```csharp
picker.Presets =
[
    new DateRangePreset("This weekend", t => WeekendFrom(t)),
    new DateRangePreset("Two weeks", t => new DateRange(t, t.AddDays(13))),
];
```

### Limits and week start

`MinDate` and `MaxDate` disable the days outside the range and the month buttons beyond it. `FirstDayOfWeek`
sets the first column of the grid. `Culture` sets the month titles, the weekday heads and the range label.

```xml
<arc:DateRangePicker Label="Stay"
                     FirstDayOfWeek="Monday"
                     Culture="en-US" />
```

```csharp
picker.MinDate = today;
picker.MaxDate = today.AddMonths(6);
```

### One or two months

`MonthCount` chooses the panel layout. `Auto` shows two months with a preset column when the window is wide
enough, and one month with a scrolling preset row otherwise. `One` always uses the compact panel.

```xml
<arc:DateRangePicker Label="Compact" MonthCount="One" />
```

### Reacting to Apply

The selection is a draft until Apply is pressed. `SelectedRangeChanged` is raised only on Apply, and
`FormattedValue` gives the text of the current value.

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} days" : "No range";
};
```

## API reference

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

## Accessibility

- The trigger exposes its expanded state, and its name includes the current range. `Label` is the field name.
- Each month is a grid named after its title. The days in the range are exposed as selected, and today is marked.
- Enter and Space on a day choose the start and then the end. After a start is chosen, the arrow keys extend the
  range preview.
- The preset list moves with ArrowUp and ArrowDown. In the compact layout, the preset row moves with ArrowLeft and
  ArrowRight.
- Cancel, Escape and a pointer press outside discard the draft and return the focus to the trigger. Apply commits
  it and announces the start, the range and the number of days.

## Motion

- The trigger surface grows into the panel, and its corners and size change together. Apply shrinks it back to the
  trigger, and the range label flies back into place.
- The range highlight stretches from day to day, and the end caps glide to their new days. The active preset glides
  between presets.
- Months slide in the direction of travel and fade out behind them. The label words roll in the direction of time.
- Reduced motion jumps the surface size and drops the shared element and the slides. Content fades for 0.08 to
  0.12 seconds. Off jumps to the end state.

## Related

- `DatePicker` for one date in a form field.
- `Calendar` for a month that stays on the page.
- `TimePicker` for a time of day, when a date range needs times.
