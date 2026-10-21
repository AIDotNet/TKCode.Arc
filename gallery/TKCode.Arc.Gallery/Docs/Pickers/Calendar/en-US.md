---
title: Calendar
summary: Browse dates in a clear, compact month view.
---

<Demo name="SingleDate" />

## When to use

`Calendar` shows one month of dates on the page, with the selected day, the navigation and the limits visible at all
times. It fits screens where the calendar is the content: a booking page, an availability view or a scheduling panel
in which the person compares several days before choosing one. A date field that opens a popup is a different job,
covered by `DatePicker`.

- **Stay dates on a booking page**: a guest chooses a check-in day in a hotel or rental flow. The whole month stays on
  screen, so the guest can see weekends, sold-out nights and the edges of the booking window without opening anything.
- **Appointment slots in a clinic or service console**: an operations user picks the day first and then the time. The
  month view shows which days are open before the time list is loaded for the chosen day.
- **Availability and leave planning**: a manager in an internal tool marks a day for a shift or a leave request. The
  month stays visible while the person edits the rest of the page, which a popup would hide.
- **Report and export ranges that start from one day**: a finance user picks the statement date for an invoice run.
  Limits such as `MaxDate` keep the choice inside the closing period of the books.
- **Keyboard-first forms in admin consoles**: the grid has a single tab stop, arrow keys, and Page keys for months and
  years, so an operator who never uses the mouse can still move through a month quickly.

## When not to use

- **A date field in a form**: use `DatePicker`. It is a field that opens the calendar in a popup, so the form keeps its
  height and the label, description and error rows stay aligned with the other fields.
- **A start and end date**: use `DateRangePicker`. It holds two dates, shows presets such as "Last 7 days", and applies
  the range only when the person confirms it.
- **A time of day or a date with a time**: use `TimePicker` next to a `DatePicker`. `Calendar` only chooses a day; its
  value has no time part.
- **Dates listed as records**: use `DataGrid` for invoices, orders or shipments sorted by a date column. A calendar
  shows one month at a time and cannot list the details of many records.
- **A short pick from a few known dates**: use `Select` or `RadioGroup` when the options are named values, such as
  "This week" or "Next billing cycle", rather than days on a grid.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Calendar ShowToday="True" />
</UserControl>
```

`Calendar` derives from Avalonia's `TemplatedControl`, so it takes the usual layout, `IsEnabled`, `Focusable` and
styling properties. Its month and selection motion, its keyboard model and its limits are part of this library. Every public property,
method and event is listed in the API reference.

### Anatomy

1. **Month title**: the name of the displayed month and year in the culture's year-month pattern (for example
   "October 2026" in `en-US`). It uses the base type size with medium weight and sits left in the header row, which is
   at least 34 px high with 14 px below it.
2. **Today button** (`ShowToday`, `TodayLabel`): a ghost pill with the label text, 32 px high and 11 px of side padding.
   It is visible only when `ShowToday` is `True`.
3. **Previous and next buttons**: two 32 px round ghost buttons with a 16 px chevron. They sit at the right edge of the
   header with 2 px between them. A button that would move past `MinDate` or `MaxDate` is disabled and drawn at 36%
   opacity.
4. **Weekday row**: the short weekday names in the muted text color, in the extra-small type size with medium weight.
   It starts on `FirstDayOfWeek` and has 6 px below it.
5. **Day grid**: six rows of seven days, 4 px apart. Each day is 14 px text in the small type size. The six rows are
   always present, so a month with five weeks shows days from the neighboring months in the last row.
6. **Selection disc**: a filled circle in the accent color behind the selected day, with its number in the accent
   foreground color. Hovering an unselected day fills it with the surface-muted color; hovering the selected day uses
   the stronger accent.
7. **Focus ring**: a 2 px ring in the focus color around the day that holds keyboard focus.

The width is `min(100%, 328px)`: the calendar never grows beyond 328 px and it is left aligned in its container. Because
the height is fixed by the six rows, the calendar takes the same space in every month, and nothing below it moves when
the person navigates.

### Selected date

`SelectedDate` is a `DateOnly?`, and only its date part is used. It is two-way by default, so a bound value follows
the day the person picks and a value set in code moves the selection. Use `null` for no selection; the calendar then
has no selected day and its roving tab stop moves to today, or to the first open day of the month.

`DateSelected` is raised only when the person picks a day: a click, Enter or Space on a focused day, or the Today
button. Setting `SelectedDate` from code does not raise it. Subscribe to the bound property when code changes must
also trigger a reaction, and to `DateSelected` when the reaction is for a user action only.

```xml
<arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}" />
```

```csharp
calendar.DateSelected += (_, e) => status.Text = $"Selected {e.Date:D}";
```

The event carries one value:

| Member | Type | Meaning |
|---|---|---|
| `Date` | `DateOnly` | The day the person picked. It is never a disabled day. |

### Limits and disabled days

`MinDate` and `MaxDate` are the earliest and latest selectable days. `DisabledDates` is a `Func<DateOnly, bool>` that
returns `true` for each day that must not be picked, such as weekends, booked nights or public holidays. A day is
disabled when any of the three rules applies.

<Demo name="LimitsAndDisabledDays" />

The rules apply in three places: a disabled day cannot be clicked or activated with Enter or Space, the arrow and Page
keys never land on it, and the navigation buttons stop at the month that holds the limit. When a keyboard move would
land on a disabled day, the focus steps past it in the direction of travel: with Saturday and Sunday disabled,
pressing Right on a Friday moves the focus to Monday.

### Displayed month

`DisplayMonth` is a `DateOnly?` that names the displayed month. Any day in the month works; the calendar normalizes the
value to the first of the month. It is two-way: when the person moves to another month with the buttons or the keyboard,
`DisplayMonth` is updated, so a bound view model always knows the month on screen.

```xml
<arc:Calendar DisplayMonth="{Binding ViewMonth, Mode=TwoWay}" />
```

When no `DisplayMonth` is set, the calendar starts on the month of `SelectedDate`, and on the current month when nothing
is selected. After the control has started, changing `SelectedDate` in code does not turn the view to its month; set
`DisplayMonth` or call `GoToMonth` for that. `GoToMonth(DateOnly date, bool raise = true)` slides to the month that
contains `date`, and with `raise` left at its default it also updates `DisplayMonth`.

### Labels and culture

`Culture` sets the language of the month names, the weekday names and the long date in the accessible names. When it is
not set, the current UI culture is used, so an application that follows the operating system language needs no
setting. `FirstDayOfWeek` chooses the first column; it defaults to `Sunday`, and a European or ISO layout usually sets
`Monday`.

`TodayLabel`, `PreviousMonthLabel` and `NextMonthLabel` replace the text and the names. The Today label is visible
text; the previous and next labels are only accessible names, because those buttons show chevrons. Translate all three
together so a screen reader does not switch languages in the middle of the control.

```xml
<arc:Calendar Culture="de-DE"
              FirstDayOfWeek="Monday"
              TodayLabel="Heute"
              PreviousMonthLabel="Vorheriger Monat"
              NextMonthLabel="Nächster Monat"
              ShowToday="True" />
```

### Focus and the roving tab stop

The grid has one tab stop: the day last focused, else the selected day, else today, else the first open day of the
shown month. Tab moves into the grid on that day and out of it to the next control, so a long form does not need
forty-two stops. `FocusTabbableDay(NavigationMethod method = NavigationMethod.Directional)` moves focus to that day and
returns `false` when no day can take it, for example when the control is not shown yet or every day in the month is
disabled. Host it in a popup or a dialog, and call it after the control is attached, so the arrow keys work at once.

### Binding to a view model

A booking page owns its dates and its availability rules in the view model. The calendar binds to the dates and
receives the rule as a function, so the rule can read the view model's data.

```csharp
public partial class StayViewModel : ObservableObject
{
    private readonly HashSet<DateOnly> _bookedNights;

    public StayViewModel(IReadOnlySet<DateOnly> bookedNights)
    {
        _bookedNights = bookedNights.ToHashSet();
        MinStayDate = DateOnly.FromDateTime(DateTime.Today);
    }

    [ObservableProperty]
    private DateOnly? _stayDate;

    [ObservableProperty]
    private DateOnly? _viewMonth;

    [ObservableProperty]
    private string? _reservationNotice;

    public DateOnly MinStayDate { get; }

    public DateOnly MaxStayDate => MinStayDate.AddMonths(6);

    public Func<DateOnly, bool> IsBooked => date => _bookedNights.Contains(date);

    [RelayCommand]
    private void Continue()
    {
        if (StayDate is { } checkIn)
        {
            ReservationNotice = $"Holding check-in {checkIn:MMM d, yyyy} for 15 minutes.";
        }
    }
}
```

```xml
<StackPanel Width="328" Spacing="12">
    <arc:Calendar SelectedDate="{Binding StayDate, Mode=TwoWay}"
                  DisplayMonth="{Binding ViewMonth, Mode=TwoWay}"
                  MinDate="{Binding MinStayDate}"
                  MaxDate="{Binding MaxStayDate}"
                  DisabledDates="{Binding IsBooked}" />
    <arc:Button Content="Continue" Command="{Binding ContinueCommand}" />
    <TextBlock Text="{Binding ReservationNotice}" />
</StackPanel>
```

`SelectedDate` and `DisplayMonth` are two-way, so the view model receives every pick and every month change.
`MinDate`, `MaxDate` and `DisabledDates` are one-way inputs that the view model sets when its rules change. Setting
a property to a new value is enough: the calendar refreshes its grid and its navigation buttons when a limit changes.
The calendar has no command of its own. The `Continue` button calls the view model's command, which reads `StayDate`
and sets `ReservationNotice`, a string that a `TextBlock` below the button can bind to.

The date rules are checked on user input only. A `SelectedDate` set by binding or code is not compared with
`MinDate`, `MaxDate` or `DisabledDates`, so the view model must not set a date that is outside them. Keep the
limits and the value consistent in the view model.

### Example: hotel stay dates

A rental booking page for Northwind Lodge. The guest chooses a check-in day in November 2026. The calendar starts on
the month of the selected night, the earliest day is the first of November, so the previous month button is disabled,
and the summary row below states the decision in plain words. The calendar has no visible Today button in this scene,
because the scene is fixed to a date and should look the same on every machine.

The month view is the whole control: no separate title or legend is needed, because the header names the month and the
disc shows the choice.

![A stay dates card with a November 2026 calendar, a selected check-in day and a continue button](images/StayDates.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Stay dates"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Harbor View Suite, room 412"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:Calendar SelectedDate="2026-11-12"
                      MinDate="2026-11-01"
                      MaxDate="2026-12-31" />
        <Grid ColumnDefinitions="*,Auto">
            <TextBlock Text="Check in Nov 12, 2026, 3 nights"
                       VerticalAlignment="Center"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <arc:Button Grid.Column="1" Content="Continue" />
        </Grid>
    </StackPanel>
</Border>
```

The decisions in this example are deliberate. The limits come from the booking window, so a guest cannot choose a past
night or one beyond the year-end. The summary repeats the choice in words, which matters for a screen reader and for a
guest who scrolls back to the page. The button is the single primary action; the calendar itself has no action buttons.

### Content guidelines

- **Month title**: the calendar writes it for you in the culture's pattern; do not add a second title with the same
  month in the same card. Name the purpose in the heading above the calendar, such as "Stay dates" or "Leave request".
- **Today label**: keep the default "Today" for an English interface, or use the word the product uses for the current
  day. It is a visible button, so keep it to one word.
- **Previous and next labels**: say the destination, "Previous month" or "Next month", because the buttons are announced
  by name only.
- **Summary text under the calendar**: write the outcome in full, with the date as digits and the month name in the
  culture's format, such as "Check in Nov 12, 2026, 3 nights". State the consequence, not the mechanism.
- **Disabled days**: explain them nearby when a whole category is blocked, for example "Weekends are not bookable".
  The reason belongs on the page, because the grid itself does not say why a day is blocked.

| Instead of | Write |
|---|---|
| Date | Stay dates |
| Choose a date | Check-in date |
| Next | Next month |
| Selected: 12 | Check in Nov 12, 2026 |
| Sat, Sun unavailable | Weekends are not bookable for this suite. |

### Best practices

**Do**

- Set `MinDate` and `MaxDate` to the real booking or reporting window. The navigation then stops at the edge, and a
  person cannot page into a month with nothing to pick.
- Use `DisabledDates` for rules that are data, such as booked nights, and `MinDate` and `MaxDate` for the window. The
  two rules read the same way in the grid and in the keyboard model.
- Bind `SelectedDate` two-way and react to the bound value in the view model, so the summary and the save action
  always see the same day as the grid.
- Choose `FirstDayOfWeek` for the region of the product, because the first column is what people scan first.
- Keep one calendar per decision. Two calendars side by side on one page make the person compare two months at once,
  which is better served by `DateRangePicker`.

**Avoid**

- Using `DateSelected` as the only source of the value. It is raised for user actions only, so a value set in code
  would never reach a view model that waits for the event.
- Setting a `SelectedDate` that breaks `MinDate`, `MaxDate` or `DisabledDates` in code. The calendar does not
  correct it, so the grid can show a selected day that the person could never pick.
- Putting the calendar inside a form that also has a date field. Two date inputs for the same value confuse the person
  about which one is current; use `DatePicker` in forms.
- Turning `ShowToday` on for a calendar whose window does not include today. The button then leads outside the limits.
- Adding a second, duplicate month title above the calendar. The header already names the month, and the duplicate
  text does not move with it when the person navigates.

### Common questions

**Why does the grid always show six weeks?** Each month uses six rows, so the grid has 42 days, and days from the
neighboring months fill the empty cells. The height never changes when the person navigates, so the content below the
calendar does not jump from one month to the next.

**Why did my handler not run when I set `SelectedDate` in code?** `DateSelected` is raised only for a pick by the
person: a click, Enter or Space on a focused day, or the Today button. Use the bound `SelectedDate` property for code
changes, and use `DateSelected` only for the actions that the person takes.

**How do I move the view to the month of a given date?** Call `GoToMonth(new DateOnly(2027, 3, 1))` for a navigation
that animates, or set `DisplayMonth` to a day in that month. `GoToMonth` also updates `DisplayMonth` unless you pass
`raise: false`.

**Can a limit change while the calendar is open?** Yes. Setting `MinDate`, `MaxDate`, `DisabledDates`, `FirstDayOfWeek`
or `Culture` refreshes the grid, and the navigation buttons are updated to match. A selection that is now disabled stays
selected until the person picks another day, so clear it in code when the rule makes it invalid.

**What happens when the calendar stays open past midnight?** The calendar follows the local date. When the day changes,
the highlighted today and the Today button are updated, so a calendar left on screen overnight does not show the old day
as today.

## API reference

<!-- api: Calendar -->

## Accessibility

- The calendar is named by its month title, for example "October 2026", so the person hears what the grid shows when
  focus enters it.
- Each day is a button named with its full date in the culture's long format, such as "Tuesday, October 20, 2026".
  The selected day carries the help text "selected", and today carries "today".
- The previous, next and Today buttons have names: the `PreviousMonthLabel`, `NextMonthLabel`, and the Today label
  followed by today's full date. The previous and next buttons are disabled in the month that holds `MinDate` or
  `MaxDate`, so they are announced as unavailable there.
- Only one day is in the Tab order at a time. Arrow keys, Home, End and the Page keys move the focus inside the grid;
  Tab leaves the grid for the next control.
- A change of month is announced politely. The months that slide out of view are not focusable and are hidden from
  assistive technology, so the focus never lands on a day the person cannot see.
- Disabled days stay in the grid, so the person can see that the day exists but cannot be picked.

### Keyboard

| Keys | Action |
|---|---|
| `Left` / `Right` | Move the focus one day back or forward; crosses into the neighboring month |
| `Up` / `Down` | Move the focus one week back or forward |
| `Home` / `End` | Move to the first or last day of the week, using `FirstDayOfWeek` |
| `PageUp` / `PageDown` | Move the focus one month back or forward; the day is kept, clamped to the month's length |
| `Shift` + `PageUp` / `PageDown` | Move the focus one year back or forward |
| `Enter` / `Space` | Select the focused day; a disabled day is ignored |

Moves stop at `MinDate` and `MaxDate`, and they step past disabled days in the direction of travel. A move onto a new
month changes `DisplayMonth` and moves the focus in the same frame the month begins to slide. Tab and Shift+Tab leave
the grid; the arrow keys are handled by the calendar only when the focus is on a day.

## Motion

- The months slide on one strip in the direction of travel. The title slides with them, and the selected disc
  glides from day to day. The numbers change color as the disc passes them.
- A long jump, such as Today or Shift + PageDown, starts from the month next to the target and does not pass
  the months in between.
- Reduced motion jumps the months and the disc straight to the end state and replaces the title in place.
  Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Moving one month (buttons, PageUp, PageDown) | The month strip slides on a short spring with no bounce, in the direction of travel | The new month appears at once | Immediate |
| Month title | The old title slides out in the direction of travel while the new one fades in | The title is replaced in place | Immediate |
| Selected disc between days | Glides in a straight line across the grid | Jumps to the new day | Jumps to the new day |
| First appearance of the selection | Grows from 60% scale with a quick spring while it fades in | Shown at once | Immediate |
| Day numbers under the disc | Change color as the disc passes them | Change color at once | Immediate |
| Long jump (Today, Shift + Page keys) | Starts from the month next to the target, skipping the months between | Jumps | Immediate |

Motion never delays the keyboard. A second arrow press while the strip is still sliding retargets the same spring,
so the calendar follows the newest position without queuing the earlier moves.

## Related

- `DatePicker` for a date field that opens the calendar in a popup, typically in a form.
- `DateRangePicker` for a start and end date with presets.
- `TimePicker` for choosing a time of day.
- `DataGrid` for dated records that need columns and sorting.

| Component | Choose it when |
|---|---|
| `DatePicker` | A form needs one date in a field with a label, description and error row |
| `DateRangePicker` | The person picks a start and an end, often with presets such as "Last 30 days" |
| `TimePicker` | The value is a time of day, or the time is paired with a date |
| `DataGrid` | The dates belong to many records that need sorting and columns |
| `Select` | The choices are named options rather than days |
