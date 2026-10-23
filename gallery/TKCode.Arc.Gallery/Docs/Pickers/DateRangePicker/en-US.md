---
title: Date range picker
summary: A range picker that grows from its trigger into two months with presets and a stretching range highlight.
---

<Demo name="Report" />

## When to use

`DateRangePicker` chooses a span of whole days as one value: a start day and an end day. It sits in a filter bar, a
booking form or a toolbar. The closed trigger shows the applied range as one short line, and a click opens a panel
with a preset rail and one or two months, so people can take a common period in one click or pick an exact span.

- **Report filters in an analytics dashboard**: a revenue or conversion report with presets such as Last 7 days,
  Last 30 days and Year to date. The preset rail covers the common periods, and the calendar covers the rest.
- **Stays and leave requests**: a hotel booking or a leave request in an HR portal needs a check-in and a check-out
  day. Set `MinDate` to today so past nights cannot be chosen, and `MaxDate` to the last day that can be booked.
- **Billing periods and invoice exports**: an admin console that exports invoices for a period such as
  `Oct 1 – 31, 2026`. The value changes only on Apply, so a half-picked span never reaches the export query.
- **Compact toolbars and side panels**: a log viewer or an incident timeline whose filter bar shows the window as
  one label until it is changed. `MonthCount="One"` keeps the panel narrow when the toolbar sits in a side column.

## When not to use

- **One day, such as a due date or a birth date**: use `DatePicker`. It holds a single day and has no preset rail
  or range highlight.
- **A month that stays on the page**: use `Calendar` when people scan availability or compare days across a month.
  The range picker only opens on demand, so nothing else on the page is visible while it is open.
- **A time of day, such as a shift start or a meeting slot**: use `TimePicker` for each time. `DateRange` holds
  `DateOnly` ends, so a range picker never carries a time.
- **Only a fixed list of named periods with no custom span**: use `Select`. The preset rail is part of this control;
  a plain list is simpler when the custom calendar is never used.
- **Free typing of a date range**: the trigger has no text entry, so people cannot type `10/05 - 10/09`. If the
  product needs typed dates, give the form two `DatePicker` fields and validate that the end follows the start.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DateRangePicker Label="Date range" />
</UserControl>
```

```csharp
var today = DateOnly.FromDateTime(DateTime.Today);
picker.SelectedRange = new DateRange(today.AddDays(-6), today);
```

`DateRangePicker` derives from Avalonia's `TemplatedControl`, so its look comes from a ControlTheme and its panel is
a popup that the control owns. It is not a text box and it is not Avalonia's date picker: nothing is typed into the
trigger, so there is no caret, no IME and no date parsing. The field adds the label, the description and the error
row from the same `Field` foundation as the other form controls, and the panel is drawn with the Arc month grid. A
`DateRange` is a record struct of two `DateOnly` values, both ends included, so `Days` counts the two ends too.

### Anatomy

1. **Label** (`Label`, `IsLabelVisible`): the accessible name of the trigger and the panel. The label is hidden by
   default; set `IsLabelVisible="True"` to show it above the trigger in the field label style.
2. **Trigger** (the template part named by `DateRangePicker.TriggerPart`): 44 px tall (the Medium control height), 18 px corner radius, a 1 px border on the
   raised surface with the resting shadow. Padding is 14 px before the content and 12 px after it, with 10 px
   between the parts. Hover fills the trigger with a 3% tint of the foreground and strengthens the border to the
   strong border color.
3. **Calendar icon**: 16 px, in the secondary text color.
4. **Value** (the template part named by `DateRangePicker.ValuePart`): the applied range in medium weight, for example `Oct 5 – 9, 2026`. With no range it
   shows `Placeholder` in the secondary text color. Changed words roll in place.
5. **Chevron**: 16 px, in the muted text color, at the end of the trigger.
6. **Description** (`Description`) and **error row** (`ErrorMessage`): help and error copy under the trigger, from
   the field foundation. A non-empty `ErrorMessage` is the error copy; the control has no separate invalid flag.
7. **Panel** (when open): an inset of 10 px (8 px in the compact layout). Two-month layout: a 148 px preset rail (9.25
   rem) with a 10 px gap before the months. Compact layout: the presets become a horizontal row under a rule.
8. **Month navigation**: two 32 px round buttons, previous and next. A button is disabled when the months beyond it
   would fall outside `MinDate` or `MaxDate`; the disabled state is 35% opacity.
9. **Footer**: a 1 px rule above a row with the summary on the left (the day count, such as `12 days`, or `Pick an
   end date` after the first click) and Cancel and Apply on the right. Both buttons are 36 px tall. Apply is at least
   76 px wide and is disabled while there is no draft.

### Layout: one or two months

`MonthCount` chooses the panel. `Auto` (the default) shows two months beside the preset rail when the boundary is at
least 712 px wide, and one month under a horizontally scrolling preset row otherwise. `One` always uses the compact
panel, and `Two` always shows two months beside the rail.

<Demo name="OneMonth" />

Use `Auto` in pages and dialogs that stretch with the window, so the panel shows the most context it can. When the
window is resized across the 712 px boundary, the panel changes between one and two months and the surface springs
to the new shape. Use `One` in narrow side columns and mobile-width layouts where a second month would be cut off.
Use `Two` only when the page is known to be wide enough, because `Two` does not fall back to the compact panel.

The panel stays inside `Boundary`, which defaults to the window. When the trigger sits near the bottom of a
scrolling page, the page scrolls so that the panel is visible, and the panel still opens from the trigger.

### Presets

`Presets` replaces the default list of eight presets. A preset is a label and a function that returns a `DateRange`
from today's date. The function receives the viewer's local today, so a preset such as Last 7 days moves with the
calendar without any code in the page. `DateRangePresets.Default` holds the default list, so a custom list can start
from it and add its own entries.

| Preset | Range |
|---|---|
| Today | Today to today |
| Yesterday | Yesterday to yesterday |
| Last 7 days | Six days before today to today |
| Last 30 days | Twenty-nine days before today to today |
| This month | The first day of this month to today |
| Last month | The whole previous calendar month |
| This quarter | The first day of the current quarter to today |
| Year to date | January 1 to today |

```csharp
picker.Presets =
[
    new DateRangePreset("Last 7 days", t => new DateRange(t.AddDays(-6), t)),
    new DateRangePreset("Next 14 days", t => new DateRange(t, t.AddDays(13))),
    new DateRangePreset("Last quarter", t => new DateRange(t.AddMonths(-3), t.AddDays(-1))),
];
```

A preset is highlighted when the draft equals its range, so the rail shows the current choice when the person picks
a preset or the same days by hand. The highlight glides from one preset to the next. Keep the list to about six
entries: the rail is 32 px per row, and in the compact layout the row scrolls sideways.

### Limits and week start

`MinDate` and `MaxDate` disable the days outside the range and the month buttons that would lead beyond it. The
keyboard also stops at the limits: arrow keys and page keys cannot move the focus onto a disabled day. `FirstDayOfWeek`
sets the first column of the grid; it is Sunday by default and can be Monday.

<Demo name="Booking" />

Set the limits in the view model when they come from a business rule, such as a booking window of six months, and
keep the presets to ranges that fit inside it. A preset that reaches past `MaxDate` is not clipped for you, so
compute the preset from the same limit when a range must stay inside the window.

### Culture

`Culture` sets the month titles, the weekday heads, the range label and the day count. When it is null, the
current UI culture is used. The text the control shows is in properties, so a localized page sets them with the
same translation work as the rest of the form.

<Demo name="Culture" />

| Property | Default | Used for |
|---|---|---|
| `Placeholder` | `Select dates` | The trigger when no range is applied |
| `ApplyText` / `CancelText` | `Apply` / `Cancel` | The footer buttons |
| `NoDatesText` | `No dates` | The trigger and the summary when the draft is empty |
| `PickEndText` | `Pick an end date` | The summary after the first click, before the end is chosen |
| `DayCountFormat` | `{0} day` | The count for one day; the one-day form is used only for 1 |
| `DaysCountFormat` | `{0} days` | The count for two or more days; `{0}` is the number |
| `PreviousMonthLabel` / `NextMonthLabel` | `Previous month` / `Next month` | The accessible names of the navigation buttons |

Set both count formats for each language, because the control picks the one-day form only when the count is 1.
`FirstDayOfWeek` is separate from `Culture`: a Monday week start is a business choice, so the demo sets it
explicitly instead of relying on the culture.

### Driving it from code

`SelectedRange` is the value and `IsDropDownOpen` opens or closes the panel. Setting `SelectedRange` while the
panel is closed updates the trigger at once. `FormattedValue` returns the text the trigger shows, which is the
placeholder when no range is applied, so a status line or an export label can reuse it.

```csharp
picker.SelectedRangeChanged += (_, e) =>
{
    status.Text = e.NewRange is { } range ? $"{range.Days} days" : "No range";
};
```

The selection is a draft until Apply is pressed. `SelectedRangeChanged` is raised only on Apply, and its event args
carry `OldRange` and `NewRange`. Cancel, Escape and a press outside the panel do not raise it.

### Binding to a view model

In an MVVM app the view model owns the range and the page binds it two ways. With CommunityToolkit.Mvvm:

```csharp
public partial class ReportFilterViewModel : ObservableObject
{
    private readonly IReportService _reports;

    public ReportFilterViewModel(IReportService reports)
    {
        _reports = reports;
        _period = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 21));
    }

    [ObservableProperty]
    private DateRange? _period;

    [ObservableProperty]
    private string? _periodError;

    partial void OnPeriodChanged(DateRange? value)
    {
        PeriodError = value is { Days: > 90 } ? "Choose 90 days or fewer." : null;
    }

    [RelayCommand]
    private Task RunReportAsync()
    {
        if (Period is not { } range || PeriodError is not null)
        {
            return Task.CompletedTask;
        }
        return _reports.LoadAsync(range.Start, range.End);
    }
}
```

```xml
<StackPanel Spacing="16" Width="360">
    <arc:DateRangePicker Label="Report period"
                         SelectedRange="{Binding Period, Mode=TwoWay}"
                         ErrorMessage="{Binding PeriodError}" />
    <arc:Button Content="Run report" Command="{Binding RunReportCommand}" />
</StackPanel>
```

`SelectedRange` is two-way. It is written only when the person presses Apply, so `Period` never holds a draft. The
picker has no command of its own: the form's button runs the report, and `SelectedRangeChanged` is needed only when
the page must react to every Apply, such as updating a status line. Compiled bindings need `x:DataType` on the view;
the example leaves it out for brevity.

### Validation

The picker enforces the limits it knows: `MinDate`, `MaxDate` and the disabled days. Rules about the span belong to
the view model, and the result goes to `ErrorMessage`.

| Rule | When to check | Where it shows |
|---|---|---|
| Days outside `MinDate` or `MaxDate` | While picking (the days are disabled) | Nothing: the day cannot be chosen |
| Span longer than the report allows | On Apply, through the `Period` change | `ErrorMessage` under the trigger |
| Period required by the form | On submit | `ErrorMessage`; move focus to the picker |
| Server rejects the period | After the request | `ErrorMessage`; clear it when `Period` changes |

Apply keeps the panel's draft out of the view model, so the rule runs on the committed value. Clear the error when the
person applies a new range; the `OnPeriodChanged` handler above does that because every committed change passes
through it.

### Focus and dismissal

- **Opening**: Enter or Space on the trigger opens the panel and focuses the start of the committed range. With no
  range, focus starts on today.
- **Applying**: Apply commits the draft, closes the panel and returns focus to the trigger. Apply is enabled when a
  draft exists.
- **Cancelling**: Cancel, Escape or a press outside the panel discard the draft. Cancel and Escape return focus to the
  trigger; a press outside does not move focus, because the person has already moved it.
- **Half-picked ranges**: after the first click, Apply commits a one-day range on the start day. The summary says
  `Pick an end date` at that point, so people see the choice before they apply it.
- **Tabbing out**: moving focus out of the panel closes it and discards the draft, so the panel never stays open
  behind other controls.

### Example: hotel stay in a booking form

A booking form for a hotel group. The stay is a single field, the check-in window is limited to the next six months,
and the description tells people what the dates control. The form reads the stay only after Apply, so the price
quote always matches the committed dates.

```xml
<StackPanel Width="560" Spacing="20">
    <arc:DateRangePicker Label="Stay"
                         IsLabelVisible="True"
                         Description="Check-in from today, up to six months ahead."
                         Culture="en-US"
                         FirstDayOfWeek="Monday"
                         Placeholder="Choose check-in and check-out" />
    <arc:Input Label="Guest name" Placeholder="As shown on the passport" />
    <arc:Button Content="Continue to payment" IsDefault="True" />
</StackPanel>
```

The `MinDate` and `MaxDate` limits are set in code from the same today the view model uses, so the booking window
and the disabled days never disagree with the server's rules.

### Content guidelines

Name the field after the business value: `Report period`, Stay, `Billing period`. Keep preset labels short and in
sentence case. Write the button as a verb and an object, and keep the day counts in the formats the product uses.

| Instead of | Write |
|---|---|
| `Date` (for a range) | `Report period`, Stay, `Billing period` |
| `Last 7 Days` | `Last 7 days` |
| `OK` | `Apply` (or `Apply filter` when the button filters a list) |
| `Pick end date` | `Pick an end date` (the default text) |
| `1 days` | Set `DayCountFormat` for one day and `DaysCountFormat` for more |
| `Select a date range from the calendar below` | `Select dates` |

Keep the placeholder short so that it fits beside the calendar icon and the chevron in a 44 px trigger.

### Best practices

**Do**

- Set `Label` to the business value and show it with `IsLabelVisible="True"` in a form. A hidden label still names the
  field for assistive technology, but people scanning a form need the visible name.
- Bind `SelectedRange` two ways and read the committed value only. A draft exists only while the panel is open, so
  the rest of the page never sees a half-picked range.
- Put the presets your reports or bookings use, and keep the list short. People take a preset faster than they
  scroll a calendar.
- Set `MinDate` and `MaxDate` from the business rule. Disabled days explain the limit without a message.
- Use `MonthCount="One"` in narrow columns, where a second month would be cut off.

**Avoid**

- Using the picker for one date. The range highlight and the presets add steps that a single date does not need.
- Expecting `SelectedRange` to follow the keyboard or the pointer while the panel is open. The value changes on
  Apply only, so code that reacts to each day picked will never run; subscribe to `SelectedRangeChanged` instead.
- Relying on the default English texts in a localized product. Set `Placeholder`, `ApplyText`, `CancelText` and both
  count formats together so that the trigger and the footer use the same language.
- Using a range picker inside a scrolling list of many rows. Each row opens its own panel, which is hard to scan;
  move the filter to the header of the list.

### Common questions

**Why does the trigger still show the old range after I picked new days?** The picks are a draft until Apply. Press
Apply to commit them; Cancel, Escape or a press outside discards them and keeps the previous `SelectedRange`.

**Can I open the panel from code?** Yes. Set `IsDropDownOpen` to `True`. The panel opens with focus on the committed
start, as it does for the keyboard. Set it back to `False` to close the panel without applying the draft.

**What happens when the second day is before the first?** The picker orders the two days, so the range runs from the
earlier day to the later day. The same is true in code: `DateRange.Ordered(a, b)` returns the range in order.

**Does a range always have an end?** A `DateRange` always has two days, and both ends are included. If the person
applies after picking only a start day, the range is that single day. To require two days, check
`range.Days > 1` in the view model.

**How do I count the nights in a stay?** `Days` counts both ends, so a stay from March 3 to March 5 has three days and
two nights. Compute the nights as `range.Days - 1` for a booking that charges per night.

## API reference

<!-- api: DateRangePicker -->

<!-- api: DateRange -->

| Companion type | Member | Summary |
|---|---|---|
| `DateRangePreset` | `DateRangePreset(string label, Func<DateOnly, DateRange> range)` | Creates a preset from a label and a function of today |
| `DateRangePreset` | `Label` | Button text, such as `Last 7 days` |
| `DateRangePreset` | `Range` | The range for a given today |
| `DateRangePresets` | `Default` | Arc's eight presets, from Today to Year to date |

## Accessibility

- The trigger exposes its expanded state, and its name includes the current range. `Label` is the field name.
- Each month is a grid named after its title. The days in the range are exposed as selected, and today is marked.
  Each day is named with its long date.
- Enter and Space on a day choose the start and then the end. After a start is chosen, the arrow keys extend the
  range preview.
- The preset list moves with ArrowUp and ArrowDown. In the compact layout, the preset row moves with ArrowLeft and
  ArrowRight.
- Cancel, Escape and a pointer press outside discard the draft and return the focus to the trigger. Apply commits
  it and announces the start, the range and the number of days through a polite live region.
- The navigation buttons are named by `PreviousMonthLabel` and `NextMonthLabel`, so set them in the page language.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` on the trigger | Opens the panel and focuses the start of the committed range, or today |
| `Enter` / `Space` on a day | First press chooses the start; second press chooses the end and makes the draft |
| `Left` / `Right` | Moves the focus one day; with a start chosen, the preview extends to the day |
| `Up` / `Down` | Moves the focus one week |
| `Home` / `End` | Moves to the first or last day of the focused week, which follows `FirstDayOfWeek` |
| `PageUp` / `PageDown` | Moves the focus one month; with `Shift`, one year |
| `Up` / `Down` on a preset (`Left` / `Right` in the compact layout) | Moves between presets |
| `Enter` / `Space` on a preset, Cancel or Apply | Activates the focused button |
| `Tab` / `Shift+Tab` | Moves through the panel; leaving the panel closes it and discards the draft |
| `Escape` | Closes the panel, discards the draft and returns focus to the trigger |

Keyboard focus never lands on a disabled day: the arrow and page keys stop at `MinDate` and `MaxDate`.

## Motion

- The trigger surface grows into the panel, and its corners and size change together. Apply shrinks it back to the
  trigger, and the range label flies back into place.
- The range highlight stretches from day to day, and the end caps glide to their new days. The active preset glides
  between presets.
- Months slide in the direction of travel and fade out behind them. The label words roll in the direction of time.
- Reduced motion jumps the surface size and drops the shared element and the slides. Content fades for 0.08 to
  0.12 seconds. Off jumps to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The panel opens | The trigger grows into the panel on a spring, 0.48 s | The size jumps; content fades in over 0.08 to 0.12 s | Immediate |
| The panel closes after Apply | The panel shrinks back to the trigger in 0.38 s and the label flies home | The size jumps; the summary fades | Immediate |
| The months change | The new months slide in from the direction of travel, 0.4 s | Content cross-fades over 0.08 to 0.12 s | Immediate |
| The trigger value changes | The words roll in the direction of time | Content cross-fades over 0.08 to 0.12 s | Immediate |

## Related

- `DatePicker` for one date in a form field.
- `Calendar` for a month that stays on the page.
- `TimePicker` for a time of day, when a date range needs times.

| Component | Choose it when |
|---|---|
| `DatePicker` | The value is one day, such as a due date or a birth date |
| `Calendar` | The month must stay visible for scanning, such as availability |
| `TimePicker` | The value is a time of day, such as a shift start |
| `Select` | The choices are a short fixed list of named periods, with no custom span |
| `Field` + `FieldFrame` | You build your own trigger that should share the label and message rows |
