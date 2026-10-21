---
title: Date picker
summary: Choose a date without losing context.
---

<Demo name="DateField" />

## When to use

`DatePicker` is the form field for one calendar date. It looks and behaves like a text field: the person can type a date
or open a calendar popup to pick one, and the value sits next to the other fields with the same label, description and
error rows. Use it wherever a form asks for a day and the surrounding page should keep its context while the choice is
made.

- **Start and due dates in a project or order form**: a project manager sets the start date and the due date of a
  milestone. The two fields sit in one column, and the due date is limited by the start date through `MinDate`.
- **Invoice and billing dates in a finance console**: an accountant enters the issue date of an invoice. Typing
  `Oct 21, 2026` is faster than paging through a calendar for a date in the past, and the popup covers the rest.
- **Booking and appointment forms with limits**: a clinic or a rental desk accepts only dates that the business is open.
  `MinDate`, `MaxDate` and `DisabledDates` apply to the popup and to typed values, so an impossible date never reaches
  the server.
- **Localized forms for several markets**: a multilingual HR system shows the same field in German, Chinese or ISO
  format. The culture sets the month names, the parsing and the default display, and `DisplayFormat` fixes a pattern
  when the business requires one.
- **Filters that need one date**: an operations dashboard asks for a single report date before the data loads. For a
  start and end pair, use `DateRangePicker`.

## When not to use

- **A calendar that stays visible on the page**: use `Calendar` for a booking view or an availability grid that the
  person reads while they compare days. `DatePicker` hides the grid until it is opened.
- **A start and end date with presets**: use `DateRangePicker`. It holds two dates, offers presets such as "Last 7
  days", and applies the range only when the person confirms it.
- **A time of day, or a date and a time together**: use `TimePicker` next to a `DatePicker`. A `DatePicker` value has no
  time part, so a deadline with an hour must be built from two fields.
- **A date that is one of a few named periods**: use `Select` or `SegmentedControl` for choices such as "This quarter",
  "Last quarter" or "Custom". A `DatePicker` forces the person to compute the date that the period name already implies.
- **A free-text date in a message or a note**: use `Input` when the text is prose and will be read by people rather than
  stored as a date. A date field that rejects text it cannot parse is the wrong tool for a sentence.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DatePicker Label="Start date"
                    Description="Type a date or press Alt+Down to open the calendar."
                    SelectedDate="{Binding StartDate, Mode=TwoWay}" />
</UserControl>
```

`DatePicker` derives from `FieldTextBox`, which is Arc's text field base and therefore an Avalonia `TextBox`. The field
keeps the text box behavior: the caret, selection, undo, IME composition and the clipboard. The label, description, error
row, size and placeholder come from the shared field foundation, so a `DatePicker` sits in a form with the same spacing
as an `Input`. The calendar popup is added on top, and the popup's own keys and pointer events never reach the text.

### Anatomy

1. **Label** (`Label`): the field name in the same style as `Input`, above the frame. It names the field for assistive
   technology and is linked to the trigger.
2. **Frame**: a 1 px border with the control radius on the surface color. Hover and focus change the border only, and
   the frame does not change size.
3. **Value**: the shown date, rolled part by part when it changes (see Motion). With no value, the placeholder shows in
   the muted text color; the default placeholder is "Select a date".
4. **Calendar button** (the drop-down button): a 28 px button at the right edge of the frame. The chevron turns 180
   degrees when the popup is open. Clicking it opens and closes the popup.
5. **Description** (`Description`): helper copy under the frame. It fades out while the popup is open, so its text does
   not show beyond the popup's corner.
6. **Error** (`ErrorMessage`): copy in the danger color under the frame. A non-empty value marks the field invalid.
7. **Popup**: the calendar and a footer, placed 8 px below the field. Its width is `min(320px, window width minus 32px)`
   with a 16 px inset. On a window 360 px wide or narrower, the popup matches the field's width and its inset is 12 px.
8. **Footer**: a "Clear" button on the left, disabled when there is no value, and a status line at the right that reads
   "Selected Oct 20, 2026" or "Choose a day".

The popup carries the same `Calendar` as the rest of the library, so the keyboard model, the limits and the month
navigation are the ones documented on the `Calendar` page.

### Value and typed text

`SelectedDate` is a `DateOnly?` and is two-way. A `null` value shows the placeholder. The value changes in three ways:
the person picks a day in the popup, the person types a date and commits it, or code sets the property. A typed date is
committed when the person presses Enter or leaves the field. An empty field commits `null`, which clears the value.

Text that does not parse, or a date outside the limits, is not accepted. The field returns to the last valid value, and
for a date outside the limits it shakes first to show that the entry was refused.

`SelectedDateChanged` is raised after each committed change, with the previous and new dates:

| Member | Type | Meaning |
|---|---|---|
| `OldDate` | `DateOnly?` | The previous date, or `null` when there was none. |
| `NewDate` | `DateOnly?` | The new date, or `null` after Clear or an emptied field. |

```csharp
picker.SelectedDateChanged += (_, e) =>
{
    status.Text = e.NewDate is { } d ? $"Start: {d:yyyy-MM-dd}" : "No start date";
};
```

Code that sets `SelectedDate` directly does not pass through the typed-text rules: a value set in code is shown as it is,
so the view model must keep it inside `MinDate` and `MaxDate`. `FormattedValue` returns the text currently shown, which is
empty when there is no value.

### Limits and disabled days

`MinDate` and `MaxDate` bound the dates that can be picked. `DisabledDates` is a `Func<DateOnly, bool>` that returns
`true` for a day to block. The limits apply to the calendar and to typed dates. `IsDateAllowed(DateOnly date)` returns
the same answer in code, so a form can check a date before it sets the value.

A typed date that is out of range or disabled is rejected: the field shakes and returns to the last valid value.
The calendar also skips those days when the arrow keys move the focus.

<Demo name="LimitsAndDisabledDays" />

Use the limits to express the business rule, not to hide a mistake. A due date that must follow the start date is
`MinDate="{Binding StartDate}"`; a week-day-only delivery is `DisabledDates`. A rule that depends on data the picker
cannot see, such as a closed period, belongs in the view model's validation, shown in `ErrorMessage`.

### Sizes

`Size` sets the field height: `Small` is 36, `Medium` is 44 (the Arc look) and `Large` is 50. A form uses one size for all
of its fields, so a `DatePicker` lines up with an `Input` or a `Select` in the same row.

<Demo name="Sizes" />

### Culture and display format

`Culture` controls the parsing, the calendar's month and weekday names, and the default display. When it is not set, the
current UI culture is used. The default display is the culture's long date without the weekday, with an abbreviated
month. In `en-US` a date shows as `Oct 20, 2026`; in `zh-CN` it shows as `2026年10月20日`.

`DisplayFormat` takes a .NET date pattern and replaces that default for the shown value. Use it when the business fixes a
format for every user, such as the ISO form in an export screen. The pattern does not change the calendar's names, and
the typed text is parsed with the same pattern first.

<Demo name="CulturesAndFormats" />

Parsing tries three things in order: the display pattern, the culture's own date patterns, and then a lenient parse of
the text. That is why the person can type `10/21/2026` in `en-US` and `2026-10-21` in an ISO field. Override
`TryParseDate` in a subclass to accept an extra notation; the default is the behavior described here.

`FirstDayOfWeek` sets the first column of the popup's calendar. It defaults to `Sunday`; the German and Chinese demos
above use `Monday`.

### Error and disabled state

A non-empty `ErrorMessage` marks the field invalid. A disabled field is faded to half opacity and cannot be opened or
typed into.

<Demo name="InvalidAndDisabled" />

Keep the error until the value changes. The copy belongs to the rule that failed, so write it as the fix: "Pick a date
before the due date." A disabled field should say why it is locked in its description, because the faded frame alone
does not explain the restriction.

### Validation

A `DatePicker` checks two things by itself: that typed text parses, and that the date is inside the limits. Everything
else is a business rule, and the form decides when to show it.

| Rule | When it is checked | Where it shows |
|---|---|---|
| The text parses as a date | When the person presses Enter or leaves the field | The field returns to the last valid value; the text is not kept |
| The date is inside `MinDate` and `MaxDate` | When the person presses Enter or leaves the field | The field shakes and returns to the last valid value |
| The date is not disabled | When the person presses Enter or leaves the field | The field shakes and returns to the last valid value |
| Required, or after another date | On submit, or when the related field changes | `ErrorMessage`, on the field the rule is about |
| A rule the server checks, such as a closed period | After the request | `ErrorMessage`; clear it when the value changes |

The rules that the picker checks itself are silent when they fail, because the field simply returns to its last valid
value. Show a message for every rule that the person must act on, so a silent revert is not the only feedback.

```csharp
public partial class InvoiceViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Pick an issue date.")]
    private DateOnly? _issueDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Pick a due date.")]
    private DateOnly? _dueDate;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }
        await _invoices.SaveAsync(IssueDate!.Value, DueDate!.Value);
    }
}
```

### Binding to a view model

The view model owns the dates and the rules, and the field reflects them. `SelectedDate` is two-way, so the view model
receives a date the moment the person commits it, and `MinDate` is one-way, so the due date follows the issue date.

```xml
<StackPanel Width="280" Spacing="16">
    <arc:DatePicker Label="Issue date"
                    SelectedDate="{Binding IssueDate, Mode=TwoWay}" />
    <arc:DatePicker Label="Due date"
                    SelectedDate="{Binding DueDate, Mode=TwoWay}"
                    MinDate="{Binding IssueDate}"
                    ErrorMessage="{Binding DueDateError}" />
    <arc:Button Content="Save invoice" Command="{Binding SaveCommand}" IsDefault="True" />
</StackPanel>
```

Bind `ErrorMessage` to a string from the view model when the error comes from a rule the picker does not know, and clear
the string when the value changes. A validation error from a binding also shows in the field, so use one source for each
rule. The picker's `IsDropDownOpen` is two-way; set it to `False` from the view model to close the popup when the page
changes.

### Example: invoice dates

A billing form in a finance console. The issue date is set, the due date is limited to days on or after the issue date,
and the due date shows a message because it is earlier than the net 30 terms the account requires. The labels are nouns,
the descriptions state the rule, and the error names the fix. The popup is closed in the screenshot, so the scene shows
the field as the person sees it before they open the calendar.

![An invoice dates form with an issue date, a due date error and a save button](images/InvoiceDates.png)

```xml
<Border Width="560"
        Padding="24"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <StackPanel Spacing="20">
        <StackPanel Spacing="4">
            <TextBlock Text="Invoice INV-2026-0412"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Northwind Logistics, net 30 terms"
                       FontSize="{arc:ArcDesignTokenResource TypeSM}"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="16">
            <arc:DatePicker Label="Issue date"
                            Width="256"
                            SelectedDate="2026-10-21"
                            Description="The date the invoice is sent." />
            <arc:DatePicker Label="Due date"
                            Width="256"
                            SelectedDate="2026-10-30"
                            MinDate="2026-10-21"
                            ErrorMessage="Net 30 terms need a due date on Nov 20, 2026." />
        </StackPanel>
        <arc:Input Label="Customer reference"
                   Text="PO-88412"
                   Description="Printed on the invoice." />
        <StackPanel Orientation="Horizontal"
                    Spacing="8"
                    HorizontalAlignment="Right">
            <arc:Button Content="Cancel" Variant="Secondary" />
            <arc:Button Content="Save invoice" />
        </StackPanel>
    </StackPanel>
</Border>
```

The due date states the terms in its error rather than a generic "invalid date", so the person knows which date to
change. The two fields are the same width and share a row, so their labels and descriptions align. The customer
reference is an `Input` because it is an identifier, not a date.

### Content guidelines

- **Label**: a short noun phrase, sentence case, without a colon: "Issue date", "Due date", "Start date".
- **Placeholder**: keep the default "Select a date" for a form that has no other instruction. Change it only when the
  field is part of a group and the wording is clearer in context.
- **Description**: one sentence that says what the date controls, or how to enter it when that is not obvious: "Type a
  date or press Alt+Down to open the calendar."
- **Error**: name the rule and the fix: "Net 30 terms need a due date on Nov 20, 2026." Do not repeat the label.
- **Footer copy**: the popup's footer uses `ClearText`, `EmptyStatusText` and `SelectedStatusFormat`. Keep "Clear" as a
  verb, and make the status state the outcome, for example "Selected {0}".

| Instead of | Write |
|---|---|
| Date | Issue date |
| Invalid date | Enter a date such as Oct 21, 2026. |
| Please enter a due date: | Pick a due date. |
| Wrong format | Use a date on or after Oct 21, 2026. |
| Clear date | Clear |

### Best practices

**Do**

- Set `Culture` to the user's locale, or to the company's locale for a back-office screen, so the month names and the
  parsing agree with what the person expects.
- Keep one `Size` per form, and align the `DatePicker` with its neighbors in the same row.
- Limit a dependent date with `MinDate` bound to the earlier date, so the calendar never offers a date the form would
  refuse.
- Show a message for every rule the person must act on. A rejected typed date reverts silently after a shake; a message
  says why.
- Use `DisplayFormat` only when a format is part of the contract, such as an export or a legal document, and leave the
  culture's default in place everywhere else.

**Avoid**

- Using a `DatePicker` for a time of day or a deadline with an hour. The value has no time part, so the hour is lost.
- Setting `SelectedDate` in code to a date outside `MinDate`, `MaxDate` or `DisabledDates`. Typed values are checked, but
  values set in code are not, so the field would show a date that the rules forbid.
- Putting the calendar popup and a second, visible `Calendar` for the same value on one page. Two views of one date
  invite the person to edit the wrong one.
- Relying on the Alt+Down shortcut alone. The description should say that the calendar opens, because a person who
  does not know the key never finds the popup.
- Swapping error messages on every keystroke. Errors are checked when the value is committed, so a message that changes
  while typing reads as noise.

### Common questions

**Why does the field go back to the old date when I type an invalid one?** A typed value is committed on Enter or when
the field loses focus. If it does not parse, or it is outside the limits or disabled, the value is refused and the field
shows the last valid date. Add an `ErrorMessage` if the person needs to know why.

**How do I clear the value from code?** Set `SelectedDate` to `null`. The field shows the placeholder, and the change is
raised through `SelectedDateChanged` with a `null` new date. The popup's Clear button does the same thing for a person.

**Does the popup close after a pick?** Yes. It closes about 300 ms after the pick, so the value and the footer have time
to confirm it. With reduced or off motion, it closes at once. Clear also closes the popup at once.

**How do I open the popup from code or close it when the page changes?** Set `IsDropDownOpen` to `True` or `False`. Escape
and Alt+Up close the popup from the keyboard; Tab out of the field closes it and commits the typed text.

**How do I show the date in a different format than the one I type?** Set `DisplayFormat` for the shown value. Typing
accepts the display pattern first, then the culture's patterns, so a person can type the form they already know.

## API reference

<!-- api: DatePicker -->

## Accessibility

- `Label` names the field and is linked to the trigger. `Description` is the help text; the trigger's help text includes
  the formatted value and then the description.
- The trigger exposes its expanded state, so a screen reader announces whether the calendar is open. The popup is named
  after the label, for example "Start date calendar".
- Enter, Space and ArrowDown open the calendar and move focus to the selected day, or to today when nothing is selected.
  Inside the calendar the keys work as they do in `Calendar`.
- Escape closes the calendar and returns focus to the field. Tab out of the calendar, or a pointer press outside it,
  also closes it.
- The focus ring is shown for keyboard focus only. A pointer press that opens the calendar does not show it, and the
  caret stays in the text.
- An invalid or refused entry is not kept: the field returns to the last valid value, and the error message, if the form
  sets one, stays in the description row for the person to read.

### Keyboard

| Keys | Action |
|---|---|
| Type, then `Enter` | Commit the typed date; an invalid or disabled date is refused and the field returns to the last valid value |
| `Enter` with no typed change | Open the calendar, or close it when it is open |
| `Alt` + `Down`, or `Down` | Open the calendar and move focus to its tab stop day (or into it when it is already open) |
| `Tab` from the field while open | Commit the typed text and move focus into the calendar |
| `Escape` with a pending typed change | Discard the typed change and show the last valid date |
| `Escape` when the calendar is open | Close the calendar and return focus to the field |
| `Alt` + `Up` in the calendar | Close the calendar and return focus to the field |
| `Tab` out of the calendar | Close the calendar and commit any typed text |

Keys pressed inside the calendar never edit the text, so an arrow key or Backspace on a selected day does not change the
typed value. Inside the calendar, the grid keys work as documented on the `Calendar` page.

## Motion

- Changed parts of the date roll in the direction of time: a later date rises and an earlier date falls. Only the
  parts that change move.
- After a date is picked, the disc slides into place and the popup closes a moment later.
- The popup opens from the calendar icon with a short fade, and closes with a shorter fade.
- Reduced motion swaps the values with a fade and closes the popup at once after a pick. Off jumps to the end state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A part of the date changes | Only the changed parts roll; a later date rises and an earlier one falls | The value fades to the new text | Immediate |
| A width change in one part | Later parts slide along the same curve to make room | The value fades to the new text | Immediate |
| The popup opens | Settles 8 px into place from above and scales up from 95% on a quick spring, while it fades in | Fades in | Immediate |
| The popup closes | Lifts 6 px, scales to 97% and fades out in a short tween | Fades out | Immediate |
| The description while the popup is open | Fades out quickly and returns when the popup closes | Fades out quickly | Immediate |
| The footer status after a pick | Rises into place with a soft blur | Fades in | Immediate |
| The chevron on the calendar button | Turns 180 degrees with a spring | Turns at once | Immediate |

## Related

- `Calendar` for a month view that stays on the page.
- `DateRangePicker` for a start and end date with presets.
- `TimePicker` for the time of day, to pair with a date.
- `Field` and `FieldFrame` give any editor or trigger the same label, frame and message rows.

| Component | Choose it when |
|---|---|
| `Calendar` | The month is the content, and the person compares several days on the page |
| `DateRangePicker` | The person picks a start and an end, often with presets such as "Last 30 days" |
| `TimePicker` | The value is a time of day, or the date needs a time as well |
| `Select` / `SegmentedControl` | The choice is one of a few named periods rather than a date |
| `Input` | The text is an identifier or prose, not a date that must be parsed |
