---
title: Availability picker
summary: Find a meeting time across several people's calendars, see who is free at each time, and book the one that works.
---

<Demo name="Preview" />

## When to use

`AvailabilityPicker` helps one person choose a meeting time for a group. A strip of days says how many proposed times
suit everyone on each day; the selected day lists its times with one mark per attendee, green when they are free and grey
when they are busy; the footer sums up the choice and books it. Choosing another day glides the highlight there, slides
the times in from that side and selects the first time that works for everyone.

- **Scheduling a meeting from an app's own data.** An interview loop, a design review or a client call where your service
  already knows the attendees' calendars and proposes a handful of times.
- **Booking flows with a confirmation step.** The reader compares a few days, picks a time and confirms; a new choice undoes
  the confirmation, so changing one's mind needs no extra button.
- **Times that may cost somebody a meeting.** A time with busy attendees can still be booked. The row says who is busy and
  the footer says who will need to move a meeting.

## When not to use

- **Choosing one date.** Use `Calendar` or `DatePicker`.
- **Typing any time.** Use `TimePicker`; the picker only offers the times you give it.
- **Answering an invitation.** The picker books a time for a group; showing and answering one invitation is a different
  surface.
- **Long ranges.** The day strip shows every day in equal columns, which suits a working week (five to seven days). For a
  month, let the reader choose the week elsewhere and give the picker that week.

## Usage

### Days, times and attendees

`Attendees` are the people invited, as `ProjectPerson`s: the header shows their avatars, and every time gets one mark per
attendee in this order. `Days` are `AvailabilityDay`s with a `Date` and their `Slots`. Each `AvailabilitySlot` has a
`Start` (a time of day), an optional `End` (otherwise `Duration` after the start) and the `BusyAttendees` for that time.
Times are shown sorted by start. A day's `OpenCount`, shown on its card, counts the bookable times that suit everyone.

```csharp
var noor = new ProjectPerson("Noor Haddad", "noor");
var tomas = new ProjectPerson("Tomás Ferreira", "tomas");
var ingrid = new ProjectPerson("Ingrid Solberg", "ingrid");
picker.Attendees.AddRange([noor, tomas, ingrid]);

var monday = new DateOnly(2026, 3, 9);
picker.Days.Add(new AvailabilityDay(monday,
    new AvailabilitySlot(new TimeSpan(9, 0, 0)),
    new AvailabilitySlot(new TimeSpan(11, 0, 0)),
    new AvailabilitySlot(new TimeSpan(13, 30, 0), tomas),
    new AvailabilitySlot(new TimeSpan(15, 30, 0))));
```

```xml
<arc:AvailabilityPicker x:Name="Picker"
                        Title="Roadmap check-in"
                        Duration="0:30:00"
                        TimeZoneName="Lisbon time" />
```

`Title` is the heading; the line under it is the duration and `TimeZoneName` ("30 min · Lisbon time"), or your own
`Subtitle`. Use `DaysSource` and `AttendeesSource` for collections you own: an `ObservableCollection` is observed, and
changes to a day, a time or a person show at once. A time's `Note` replaces its generated line; `IsEnabled="False"` dims a
time that cannot be booked (already taken, in the past) and keeps it from being chosen.

### Selection

`SelectedDay` and `SelectedSlot` are two-way. At first the picker selects the first day (or the day of a `SelectedSlot` you
set) and, unless you set a time, picks one. When the day changes and the selected time is not on it, `AutoSelectSlot`
decides: `FirstEveryoneFree` (the default) takes the first time that suits everyone, or the first bookable time when there
is none; `First` takes the first bookable time; `None` leaves the choice to the reader. Choices are not remembered per day.

Setting `SelectedSlot` to a time of another day selects that day too. `SelectDay` and `SelectSlot` do the same from code;
`SelectDay(day, animate: false)` moves without motion.

`SelectedDayChanged` reports the old and new day, the `Direction` (1 later, −1 earlier) and the time picked with it;
`SelectedSlotChanged` reports the old and new time. Both say whether the reader made the change (`IsUserInitiated`).

<Demo name="Bound" />

### Booking

The button books the selection: `ConfirmRequested` comes first and can `Cancel` (a failed availability check, a missing
permission); then `IsConfirmed` turns true, `Confirmed` is raised and `ConfirmCommand` runs. Their arguments carry the
day and the time, the real `Start` and `End`, the `Conflicts` (busy attendees) and the texts the footer shows. Once booked
the button reads "Booked" with a check, stays focusable and does nothing; choosing another day or time undoes the booking
and raises `ConfirmationReset`. `Confirm` and `ResetConfirmation` do the same from code.

```csharp
picker.ConfirmRequested += (_, e) =>
{
    // Someone took the room in the meantime: keep the selection unbooked.
    e.Cancel = !IsRoomFree(e.Start, e.End);
};
picker.Confirmed += (_, e) => SendInvites(e.Start, e.End, e.Conflicts);

static bool IsRoomFree(DateTime start, DateTime end) => true;
static void SendInvites(DateTime start, DateTime end, IReadOnlyList<ProjectPerson> conflicts) { }
```

Setting `IsConfirmed` from code shows the booked state without announcing it, which suits restoring a booking made
earlier.

### Framed or flat

`IsFramed` (the default) draws a bordered, rounded surface with a line above the times and a muted footer, for a picker
that sits among other content. Set it to false when it fills a page or a panel of its own.

<Demo name="Flat" />

### Narrow containers

The picker reads its own width, not the window's. Below 520 the paddings tighten, the day cards get smaller, the times take
one column and the footer stacks over a full-width button; at 360 and below the legend and the cards' counts are hidden.
`IsLegendVisible` hides the legend at any width.

<Demo name="Narrow" />

### Harder data

A day where every time clashes for somebody selects its first bookable time, and the footer says who will need to move a
meeting. Several busy attendees are listed by their short names ("Noor and Ingrid are busy"). A day with fewer rows makes
the list's height spring to its new size; a day without times says so in place of the list.

<Demo name="TrickyData" />

### Texts

Every text has an English default and a property: the button (`ConfirmText`, `ConfirmedText`), the lines
(`EveryoneFreeText`, `BusyFormat`, `BusyManyFormat`, `ConflictFormat`, `ConfirmedLabelText`, `NoSlotText`,
`NoSlotsText`), the counts and the legend (`OpenCountFormat`, `FreeLegendText`, `BusyLegendText`, `AndWord`), the date and
time formats (`DayNameFormat`, `DayNumberFormat`, `DayLabelFormat`, `TimeFormat`, `RangeFormat`, `SelectionFormat`,
`DurationFormat`, `SubtitleFormat`) and what screen readers hear (`AttendeesAutomationFormat`, `WeekAutomationFormat`,
`DayAutomationFormat`, `TimesAutomationFormat`, `SlotAutomationFormat`, `ConfirmedAnnouncementFormat`). Dates and times are
formatted with the current culture.

```xml
<arc:AvailabilityPicker Title="Point d’équipe"
                        ConfirmText="Réserver ce créneau"
                        ConfirmedText="Réservé"
                        EveryoneFreeText="Tout le monde est libre"
                        BusyFormat="{}{0} est occupé"
                        DayLabelFormat="{}{0:dddd d MMMM}"
                        RangeFormat="{}{0} à {1}" />
```

## API reference

<!-- api: AvailabilityPicker -->

<!-- api: AvailabilityDay -->

<!-- api: AvailabilitySlot -->

<!-- api: AvailabilityDayChangedEventArgs -->

<!-- api: AvailabilitySlotChangedEventArgs -->

<!-- api: AvailabilitySelectionEventArgs -->

<!-- api: AvailabilityConfirmRequestedEventArgs -->

## Accessibility

- **Keyboard.** Every day card, every time and the button is a tab stop, in reading order: the days from left to right,
  the times row by row, then the button. Enter or Space chooses the focused day or time, or books the selection; choosing
  keeps the focus where it is, and after booking the focus stays on the button, which then does nothing. The arrow keys are
  not used.
- The picker is a group named by its title, which is a level 2 heading; the date above the times is a level 3 heading.
- The avatars are one image named "Invited: Noor Haddad, Tomás Ferreira and Ingrid Solberg".
- The day strip is a group named "Days from 9 March". Each card is a toggle button whose pressed state is the selection,
  named with the date and how many times suit everyone.
- The times are a list named "Times for Monday, 9 March". Each time is a toggle button named with its range and its line
  ("13:30 to 14:00, Tomás is busy"); a time that cannot be booked is disabled. The marks and the check are not read.
- Booking is announced politely ("Booked Monday, 9 March, 13:30 to 14:00."); the announcement is cleared when a new
  choice undoes it. Day and time changes are not announced: the focused control already says them.

## Motion

- **Day highlight**: one fill under all the cards glides to the chosen day on a gentle spring (stiffness 340, damping 34,
  no overshoot). The cards' text and borders change color over 240 ms; a pressed card shrinks to 97% (the highlight with it).
- **Times**: the new list comes in 16 px from the side of travel on a smooth spring while it fades in over 160 ms, and the old
  list leaves 16 px the other way and fades out over 160 ms at the same time. Quick changes stack: an entering list turns
  around from where it is. When the new day has a different number of rows, the height springs.
- **Selection**: the chosen time's fill and border change over 160 ms; its check slides in from the right on a critically
  damped spring and pushes the marks aside, fading in over 160 ms. A pressed time shrinks to 98%.
- **Texts**: the heading and the footer's two lines swap only when they change: the old text rises 4 px into a 2 px blur and
  fades while the new one rises from 4 px below out of the blur, both in 160 ms.
- **Button**: its label and width change with the button's own blur swap and width spring.
- Reduced motion moves the highlight at once, drops the old list immediately and fades the new one in over 120 ms, and
  crossfades the texts in 120 ms; colors and the check change at once. Off applies every change at once.

## Related

- `Calendar` and `DatePicker` for choosing a single date.
- `TimePicker` for typing a time.
- `Button`, which the picker uses to book.
- `GlidingIndicator`, the primitive behind the day highlight.
