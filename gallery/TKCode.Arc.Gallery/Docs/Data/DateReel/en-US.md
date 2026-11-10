---
title: Date reel
summary: A date picker made of turning wheels for day, month and year. Flick a wheel and it coasts, then comes to rest on a row.
---

<Demo name="Preview" />

## When to use

`DateReel` picks a date with three wheels side by side, as on a phone: one for the day, one for the month and one for
the year. A drag turns a wheel under the finger; let go with some speed and it coasts on, slows down and settles on the
nearest row, with the picked row in the band across the middle. The day and month wheels loop; the year wheel stops at
its ends with a little give.

- **Dates far from today**: birthdays and anniversaries, where paging a calendar month by month is slow.
- **Touch screens and narrow layouts**: big rows that are easy to flick.
- **Forms where the day of the week does not matter**.

## When not to use

- **Dates near today, or ranges**: a month view shows the week and the gaps; use `Calendar` or `DateRangePicker`.
- **Typing from memory on a desktop**: a date field you can type into is faster.
- **Availability**: the wheels cannot show which days are free.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DateReel SelectedDate="2026-03-14" />
</UserControl>
```

`DateReel` is in the Special package; register it with `UseArcSpecial()` next to `UseArcControls()`. `SelectedDate` is
today by default and can be bound both ways; `SelectedDateChanged` carries the old and new dates. The day wheel always
has as many days as the month, so turning to February on the 31st moves the day to the 28th or 29th.

### Order, names and range

`Order` puts the wheels in month-day-year, day-month-year or year-month-day order. `CultureName` and
`UsesFullMonthNames` set the month names. `MinDate` and `MaxDate` bound the dates: the year wheel runs from the first
year to the last, rows that lead outside the range are faded, and a wheel that comes to rest there turns back.
`VisibleItemCount` shows 3, 5, 7 or 9 rows.

<Demo name="Birthday" />

### Best practices

**Do**

- Start near the likely answer: a birthday picker can start in the 1990s rather than today.
- Set `MinDate` and `MaxDate` so impossible dates cannot be picked.
- Name the wheels with `DayLabel`, `MonthLabel` and `YearLabel` when the date has a purpose, like "Birth year".

**Avoid**

- Very long year ranges without a sensible start: flicking through two centuries is tiring.
- Using it for times; use `TimeDial` or `TimePicker`.

## API reference

<!-- api: DateReel -->

## Accessibility

- Each wheel is a spinner named by its label ("Day", "Month", "Year"), with its row as the value and the row's text as
  help text.
- Each wheel is a tab stop. Pointer focus draws no ring; keyboard focus shows the focus ring around the middle row of
  that wheel.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves between the wheels. |
| Up / Down | Turns the wheel one row back or on. |
| Page Up / Page Down | Turns five rows. |
| Home / End | Goes to the first or the last row. |
| Left / Right | Moves to the wheel on the left or right. |

## Motion

- A drag follows the finger one to one. On release the wheel's speed is projected forward to the row it would stop on,
  and a spring takes it there from that speed, so the flick and the settling are one motion.
- The year wheel stretches with resistance past its ends and springs back.
- Rows bend away on a drum: they move closer together, flatten and fade toward the edges.
- Each wheel draws its rows itself from cached text, so turning allocates nothing.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Flick | Coasts and settles on a spring | Jumps to the row | Jumps to the row |
| Keys, wheel, tap | Springs to the row | Jumps | Jumps |
| Out of range | Turns back on a spring | Jumps back | Jumps back |

## Responsive behavior

- The wheels share the width, the month wheel a little more. Rows are 36 px high, so five rows take 180 px.
- Touch drags on a wheel turn it; a horizontal swipe still reaches the page.
- The mouse wheel turns a wheel one row per notch, and a touchpad builds up rows as it scrolls.

## Related

- `Calendar` and `DatePicker` show a month at a time.
- `TimeDial` picks a time by turning.

| Component | Choose it when |
|---|---|
| `DateReel` | Dates far from today, on touch or in a narrow space |
| `DatePicker` | Dates near today, where the week matters |
| `DateRangePicker` | A stay or a period with a start and an end |
