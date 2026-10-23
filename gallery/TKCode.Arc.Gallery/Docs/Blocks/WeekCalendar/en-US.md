---
title: Week calendar
summary: A week planner where you drag to add, move and resize events and undo the last change, with a month and calendar toggles beside the grid, an all-day row, and three-day or agenda views when narrow.
---

<Demo name="Preview" />

## When to use

`WeekCalendar` is a full planning surface for a week at a time, the way a desktop calendar app shows it. A toolbar moves
between weeks and switches the view; a sidebar holds a month to jump around in and the calendars to show or hide; the grid
has a header of days, an all-day row and the hours, with each day's events laid out side by side where they overlap, the
current time drawn across today, and the past dimmed. People edit in place: drag across empty time to add an event and name
it, drag events between days and times, drag their bottom edge to change their length, click one to edit its title, day,
times and calendar, and undo the last change from a toast or with Ctrl+Z.

- **Scheduling and time blocking.** People see their meetings, focus blocks and trips in one week, how the days fill up, and
  rearrange them by dragging.
- **Team and resource calendars.** Each calendar (a team, a room, a person) has its own color and toggle, so readers can
  compare or narrow down without leaving the page.
- **Bookings seen in another place's time.** Events are instants; `TimeZone` shows them on any clock, and the corner of
  the header says which one.

## When not to use

- **Picking one date.** Use `Calendar` or `DatePicker`; this block is about what happens on the days, not choosing one.
- **Months and years.** The block shows one to seven days. A month grid of events or a year overview is a different design.
- **A short list of upcoming events.** A plain list or `Timeline` reads better than a grid with mostly empty hours.
- **Thousands of events at once.** Pass the weeks around the one shown; the block indexes every event it is given.

## Usage

### Events and calendars

Each `WeekCalendarEvent` has an `Id`, a `Title`, a `Start` and an `End` (exclusive) as `DateTimeOffset` instants, a
`CalendarId`, an optional `Location` and `Attendees` (`ProjectPerson`s; without a picture they get a generated avatar). An
event with `IsAllDay` covers whole dates instead: the dates of `Start` and `End` as written, `End` exclusive, so a three-day
hotel stay starting on Wednesday ends at midnight after Friday. Each `WeekCalendarSource` has an `Id`, a `Name`, a `Color`
(`Blue`, `Green`, `Violet`, `Amber`, `Rose`, `Teal`, `Neutral`, or `Custom` with `CustomColor`) and `IsVisible`, which its
toggle in the sidebar changes. Events of an unknown calendar are gray.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WeekCalendar x:Name="Planner">
        <arc:WeekCalendar.Calendars>
            <arc:WeekCalendarSource Id="studio" Name="Studio" Color="Blue" />
            <arc:WeekCalendarSource Id="home" Name="Home" Color="Green" />
            <arc:WeekCalendarSource Id="health" Name="Health" Color="Rose" IsVisible="False" />
        </arc:WeekCalendar.Calendars>
    </arc:WeekCalendar>
</UserControl>
```

```csharp
var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
DateTimeOffset At(int day, int hour, int minute = 0) =>
    new(new DateTime(2026, 10, day, hour, minute, 0), zone.GetUtcOffset(new DateTime(2026, 10, day, hour, minute, 0)));

Planner.TimeZone = zone;
Planner.Events.Add(new WeekCalendarEvent("brief", "Write launch brief", At(19, 9, 30), At(19, 11, 30), "studio"));
Planner.Events.Add(new WeekCalendarEvent("lunch", "Lunch with Noor", At(19, 12), At(19, 13), "home") { Location = "Corner Bistro" });
Planner.Events.Add(new WeekCalendarEvent("hotel", "Hotel: Casa Azul",
    new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero), "home")
    { IsAllDay = true });
```

Use `EventsSource` and `CalendarsSource` for collections you own; an `ObservableCollection` is observed, and so is every
property of every event and calendar. Changes made in the same moment are applied together, and the grid updates in place.

```csharp
var events = new ObservableCollection<WeekCalendarEvent>();
calendar.EventsSource = events;

// Later, when a booking comes in, it appears at once and the day's events make room for it.
var start = new DateTimeOffset(DateTime.Today.AddHours(15));
events.Add(new WeekCalendarEvent("call-42", "Client call", start, start.AddMinutes(45), "studio") { Location = "Video room" });
```

### Editing

Editing is on unless `IsReadOnly` is set. Everything snaps to 15 minutes and lasts 15 minutes at least.

- **Draw an event.** Press on empty time and drag (4 pixels start a drag). The 15-minute slot you pressed is the anchor and the
  moving end rounds to the nearest quarter hour, so drawing works upwards too; the draft stays on the day you pressed. When
  you let go, the quick create popover opens beside the draft: type a title and press Enter (an empty title saves as
  "Untitled event"), or pick a calendar first. A click on empty time drafts an hour from the half hour under the pointer.
  New events start in `DefaultCalendarId`, or the first calendar shown.
- **Move an event.** Drag it: it hops a slot at a time and keeps the point you grabbed under the pointer, across days too.
  The other events of the day make room when you drop it. A drag that ends in the slot it started in changes nothing.
- **Resize an event.** Drag its bottom edge (the grip shows on hover); the end rounds to the quarter hour.
- **Edit an event.** Click it (or press Enter on it) for its details: the title, the date, the start and end in 15-minute
  steps, the place, the people and the calendar. Times, the date and the calendar apply as you pick them; the title and the
  place when the popover closes. All-day events show their dates as text and can change title and calendar.
- **Delete and undo.** Delete or Backspace on the focused or selected event, or Delete in its details, removes it. A toast
  rises from the bottom of the calendar for six seconds (`UndoDuration`) with Undo; Ctrl+Z (Cmd+Z on a Mac) does the same.
  Undo has one level and covers the last create, move, resize, time, date or calendar change, or delete; a run of keyboard
  moves of one event, or the changes made in one details popover, undo together. `CanUndo` says whether there is one.
- **The grid scrolls** when you drag near its top or bottom, faster the closer you get, and keeps scrolling while the pointer
  stands still.
- A press outside an open popover only closes it. Escape closes a popover, cancels a drag (the event springs back), and then
  clears the selection. `SelectedEvent` is the selected event, two-way.

<Demo name="Editing" />

### Saving changes

The calendar edits your data directly: it writes `Start`, `End`, `CalendarId`, `Title` and `Location` on the
`WeekCalendarEvent` objects and adds or removes events in `Events`, or in `EventsSource` when that is a writable list (an
`ObservableCollection` is observed; a plain `List` is re-read). Then it raises `EventCreated` (also when undo brings back a
deleted event), `EventUpdated` with a `Previous` copy of the event from before the change (undo raises it again with the
values swapped), or `EventDeleted` (also when undo removes a new event). Persist from those handlers.

```csharp
calendar.EventCreated += async (_, e) => await api.CreateAsync(e.Event);
calendar.EventUpdated += async (_, e) => await api.UpdateAsync(e.Event, e.Previous);
calendar.EventDeleted += async (_, e) => await api.DeleteAsync(e.Event.Id);
```

A read-only `EventsSource` (an array, a `ReadOnlyObservableCollection`) cannot gain or lose events: the calendar still moves
and edits the event objects, but for a new or deleted event it only raises the event, and your handler changes the collection
it exposes. Set `IsReadOnly` to turn editing off altogether: no drawing, dragging, quick create, deleting or undo, and the
details show as text. New event (and its `NewEventRequested` event) drafts at the next free hour of the focused day from 9 AM;
set `Handled` to open your own form instead. `BeginCreate`, `OpenDetails`, `CloseEditor`, `DeleteEvent`, `Undo` and
`CancelDrag` do the same from code.

```xml
<arc:WeekCalendar IsReadOnly="{Binding IsArchived}" DefaultCalendarId="studio" UndoDuration="0:0:8" />
```

### Touch

On touch screens a long press (about 380 ms without moving) starts drawing on empty time or lifts an event to move it;
otherwise a vertical swipe scrolls the hours. Below 640 wide a sideways swipe changes the days: the neighbouring days come
along under your finger, and letting go past a fifth of the width (or with a flick) carries on to them without a jump; a
shorter swipe springs back. Popovers open under the event there (or over it near the bottom), kept 8 pixels inside the sides.

### Views and navigation

`View` is `Week` (seven days from `FirstDayOfWeek`, Monday by default), `WorkWeek` (Monday to Friday) or `Day`; it is two-way.
`Date` is the focused day, also two-way: the range shown always holds it, and setting a day outside it moves the calendar
there. Today, previous and next move by a week (a day in the day view, three days in the three-day view) and keep the
focused weekday; the month in the sidebar jumps to the range around any day. `GoToToday`, `Next`, `Previous`, `GoTo`,
`FocusDay` and `ScrollToTime` do the same from code. `RangeStart`, `RangeEnd`, `RangeTitle` and `EffectiveView` report what is
shown; `RangeChanged` says what moved it, in which direction.

The grid opens at the hour of the first event in view (8 AM without one), or at `InitialScrollTime`. Today also scrolls the
current time to a third of the way down. `StartHour` and `EndHour` limit the hours shown; events outside them are cut at the
edge.

<Demo name="WorkHours" />

### Narrow containers

The calendar reads its own width. From 1100 it shows the sidebar (`IsSidebarVisible` forces it on or off); below that the
calendars move into a Calendars popover in the toolbar. Below 900 the title and the header dates get smaller and New event
shows only its plus; below 760 the Calendars button shows only its colors; below 680 the toolbar takes two rows. Below 640 the
views become 3 days, Day and Agenda: the week views show three days from the focused day, and the agenda lists the week by
day, with all-day events first and "Nothing planned" on free days, each day's heading held at the top while its events
scroll. Event blocks drop their place, avatars and time as they get smaller, and titles wrap to three lines when there is
height.

<Demo name="Phone" />

### Time zones

Events are absolute times; `TimeZone` (null: the computer's) decides the clock they are drawn on, the current time line and
"today". The corner of the header shows the zone's offset ("GMT+8", "GMT+5:30", "GMT"). `Culture` sets the language of dates
and times: English merges what both ends of a range share ("Sep 28 to Oct 4, 2026", "9:30 – 11:30 AM"); Chinese uses
year-month-day titles and a 24-hour clock. `Now` fixes the current time for screenshots and tests.

<Demo name="TimeZones" />

### The all-day row

All-day events are bars across their days, packed into lanes. When more than `AllDayLaneLimit` lanes (3) are needed, the last
lane shows "+N more" for each day with hidden events, and a chevron under "All day" expands the row; `IsAllDayExpanded` is the
two-way state.

<Demo name="BusyAllDay" />

### Texts

Every word has an English default in `WeekCalendarLabels`: the toolbar, the view names, the screen reader names of days,
events and all-day bars (functions, so the order of their parts can change), and the announcements. Replace `Labels` to
translate; dates and times inside come from `Culture`.

```csharp
calendar.Culture = CultureInfo.GetCultureInfo("de-DE");
calendar.Labels = new WeekCalendarLabels
{
    Today = "Heute",
    WeekView = "Woche",
    WorkWeekView = "Arbeitswoche",
    DayView = "Tag",
    AllDay = "Ganztägig",
    NewEvent = "Neuer Termin",
    TimedEvent = (title, day, start, end, calendar, place) => $"{title}, {day}, {start} bis {end}, {calendar}",
};
```

### Performance

- Every event block, cell and header day is self-drawn and cached; nothing is laid out while ranges slide, only moved.
- A drag moves only the dragged block; the day's other events are laid out again once, when it is dropped, and settle with
  transforms.
- Ranges are drawn into at most three pooled panes; a pane that slid out is reused for the next range with its blocks, so
  paging through weeks builds nothing new.
- The current time moves once a minute, on a timer that stops when the calendar leaves the window or `Now` is set.

## API reference

<!-- api: WeekCalendar -->

<!-- api: WeekCalendarEvent -->

<!-- api: WeekCalendarSource -->

<!-- api: WeekCalendarLabels -->

<!-- api: WeekCalendarEventUpdatedEventArgs -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `RangeChanged` | The days shown changed | `OldStart`, `OldEnd`, `NewStart`, `NewEnd`, `Direction`, `Reason` |
| `ViewChanged` | `View` changed | `OldView`, `NewView`, `EffectiveView` |
| `CalendarVisibilityChanged` | A calendar's toggle was used | `Calendar`, `IsVisible` |
| `EventInvoked` | An event was clicked or activated with Enter or Space | `Event` |
| `NewEventRequested` | New event was pressed; `Handled` keeps the calendar from drafting | `Date` (the focused day) |
| `EventCreated` | A new event was saved, or undo brought a deleted one back | `Event` |
| `EventUpdated` | An event was moved, resized or edited, or a change was undone | `Event`, `Previous` |
| `EventDeleted` | An event was deleted, or undo removed a new one | `Event` |

## Accessibility

- **Keyboard.** Tab reaches the toolbar (Today, previous, next, the view switch, Calendars, New event), the month (one tab
  stop; arrows move between days, Page Up and Page Down between months, Enter picks a day), the calendar toggles, the
  all-day bars, the grid (one tab stop: the focused day) and then each event in time order.
- **In the grid**, Left and Right move to the previous or next day; past the first or last day the next range slides in and
  the focus lands on its first or last day. On an event, Up and Down go to the previous or next event and Left and Right to the
  day before or after. T goes to today, D opens the focused day and W its week. The focus stays on the focused day after T, D
  and W and after a range slide.
- **Editing keys.** N drafts an event at the next free hour of the focused day and opens the quick create popover; Enter on a
  day does the same, Enter on an event opens its details, and Enter in the quick create title saves. Delete or Backspace
  deletes the focused or selected event. Alt with Up or Down moves the focused event by 15 minutes, Alt with Left or Right by a
  day; add Shift to Up or Down to change its length. The focus stays on the event. Ctrl+Z (Cmd+Z) undoes the last change.
  Escape closes a popover, cancels a drag, and then clears the selection.
- **Popovers** are dialogs named "New event" and "Event details". The focus moves into the title and Tab stays inside until
  Escape, Done or Cancel; then it comes back to the event (or to the day of a cancelled draft). The calendar choice is a
  radio group with one tab stop; the arrow keys pick a calendar. After a delete the focus goes to the event's day; after Undo
  on the toast it comes back to the calendar.
- The grid is a table named by its range ("Oct 19 to 25, 2026") with a help text about the keys; each day is a cell named
  "Thursday, October 22, today, 6 events". Events are buttons named "title, day, start to end, calendar, place"; all-day bars
  "title, all day, first day to last day, calendar"; agenda rows leave out the day their heading already says. The header days
  are buttons named "Show Monday, October 19"; the month's days carry "Today" as their status.
- The calendar toggles are check boxes named by the calendar; the sidebar is named "Calendar navigation".
- A polite live region announces range changes ("Oct 26 to Nov 1, 2026") and view changes ("Work week, Oct 19 to 23, 2026",
  "Day view, Thursday, October 22"), and every edit: an event added, moved, resized or rescheduled, a new calendar or title,
  a delete, a restore, an undo, a cancelled drag and a cleared selection. The toast shows the same words. All of them are in
  `WeekCalendarLabels`.
- Calendar colors are never the only cue: every event says its calendar's name.

## Motion

- **Ranges**: the header row, the all-day row and the grid slide one width together on one spring (critically damped, about
  10% left after 340 ms, at rest after about a second). Pressing next again mid-slide keeps every pane going from where it is,
  with its speed; up to three ranges are on screen at once. Jumps of several weeks also slide one width.
- **Views**: the old view fades out in 160 ms while the new one fades in over 240 ms, in place.
- **The all-day row** grows or folds in 240 ms when expanded or collapsed.
- **Hover and focus**: event fills, the half-hour slot under the pointer, header and month days fade in 160 ms. The view
  switch's highlight glides between options.
- **Dragging**: a dragged event hops between 15-minute slots and days on a fast critically damped spring (at rest in about a
  third of a second), restarting from where it is drawn at every slot; resizing changes its length a slot at a time with no
  motion. After a drop, an edit in the details, a keyboard move, an undo or a delete, the events that moved settle on the
  Smooth spring; a cancelled drag springs back on it.
- **Popovers** fade in over 160 ms while scaling from 0.96 on the Smooth spring, growing from the edge that faces their event;
  they leave in 120 ms. **The undo toast** rises 12 pixels on the Smooth spring and leaves in 160 ms, dropping 8; a newer change
  replaces it with both moving at once. Swipes on touch screens continue on the range spring from where the finger let go.
- Reduced motion turns range slides into the same crossfade, moves the view switch's highlight at once, drops the hops and
  springs of events (they move at once), and keeps only the fades of the popovers and the toast; Off applies every change at
  once.

## Related

- `Calendar` and `DatePicker` for choosing a date.
- `SegmentedControl` for the view switch on its own.
- `Popover` for the Calendars list of narrower calendars.
- `Select`, `DatePicker` and `Input` for the fields of the details popover on their own.
- `Timeline` for a feed of events in time order.
