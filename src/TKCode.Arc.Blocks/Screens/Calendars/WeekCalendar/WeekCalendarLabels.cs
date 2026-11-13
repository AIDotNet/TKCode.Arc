namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="WeekCalendar"/> shows or reads to screen readers, in English by default. Replace the instance
/// (<see cref="WeekCalendar.Labels"/>) to translate or reword. Dates and times inside the formatted texts come from
/// <see cref="WeekCalendar.Culture"/>; the functions only arrange them, so the word order can change.
/// </summary>
public sealed class WeekCalendarLabels
{
    /// <summary>The button that goes to today.</summary>
    public string Today { get; init; } = "Today";

    /// <summary>The name of the previous button in the week, work week and agenda views.</summary>
    public string PreviousWeek { get; init; } = "Previous week";

    /// <summary>The name of the next button in the week, work week and agenda views.</summary>
    public string NextWeek { get; init; } = "Next week";

    /// <summary>The name of the previous button in the day view.</summary>
    public string PreviousDay { get; init; } = "Previous day";

    /// <summary>The name of the next button in the day view.</summary>
    public string NextDay { get; init; } = "Next day";

    /// <summary>The name of the previous button in the three-day view.</summary>
    public string PreviousThreeDays { get; init; } = "Previous three days";

    /// <summary>The name of the next button in the three-day view.</summary>
    public string NextThreeDays { get; init; } = "Next three days";

    /// <summary>The week view in the view switch.</summary>
    public string WeekView { get; init; } = "Week";

    /// <summary>The work week view in the view switch.</summary>
    public string WorkWeekView { get; init; } = "Work week";

    /// <summary>The day view in the view switch.</summary>
    public string DayView { get; init; } = "Day";

    /// <summary>The three-day view in the view switch.</summary>
    public string ThreeDayView { get; init; } = "3 days";

    /// <summary>The agenda view in the view switch.</summary>
    public string AgendaView { get; init; } = "Agenda";

    /// <summary>The screen reader name of the view switch.</summary>
    public string ViewSwitch { get; init; } = "View";

    /// <summary>The main button (its name when only the plus shows).</summary>
    public string NewEvent { get; init; } = "New event";

    /// <summary>The heading of the calendars list and the button that opens it on narrower calendars.</summary>
    public string Calendars { get; init; } = "Calendars";

    /// <summary>The screen reader name of the sidebar.</summary>
    public string Sidebar { get; init; } = "Calendar navigation";

    /// <summary>The month's previous button in the sidebar.</summary>
    public string PreviousMonth { get; init; } = "Previous month";

    /// <summary>The month's next button in the sidebar.</summary>
    public string NextMonth { get; init; } = "Next month";

    /// <summary>The label of the all-day row.</summary>
    public string AllDay { get; init; } = "All day";

    /// <summary>The name of the button that shows every all-day lane.</summary>
    public string ShowAllDayEvents { get; init; } = "Show all all-day events";

    /// <summary>The name of the button that folds the all-day row back.</summary>
    public string ShowFewerAllDayEvents { get; init; } = "Show fewer all-day events";

    /// <summary>The chip on a collapsed all-day row; the argument is the number of events it hides.</summary>
    public Func<int, string> MoreEvents { get; init; } = count => $"+{count} more";

    /// <summary>The agenda's line for a day without events.</summary>
    public string NothingPlanned { get; init; } = "Nothing planned";

    /// <summary>The tag beside today's heading in the agenda.</summary>
    public string TodayTag { get; init; } = "Today";

    /// <summary>The time column of an all-day event in the agenda.</summary>
    public string AllDayTime { get; init; } = "All day";

    /// <summary>The help text of the grid, read once when it gets focus.</summary>
    public string GridHelp { get; init; } =
        "Use the left and right arrow keys to move between days, T for today, D for the day view and W for the week view. Tab reaches the events.";

    /// <summary>The name of a header day button; the argument is the day ("Monday, September 21").</summary>
    public Func<string, string> ShowDay { get; init; } = day => $"Show {day}";

    /// <summary>The name of a day cell: the day, whether it is today and how many events it has.</summary>
    public Func<string, bool, int, string> DayCell { get; init; } =
        (day, isToday, count) => (isToday ? $"{day}, today, " : $"{day}, ") + (count == 1 ? "1 event" : $"{count} events");

    /// <summary>The name of a timed event: title, day, start, end, calendar and location (null when none).</summary>
    public Func<string, string, string, string, string?, string?, string> TimedEvent { get; init; } =
        (title, day, start, end, calendar, location) =>
            Join(title, day, $"{start} to {end}", calendar, location);

    /// <summary>The name of an all-day event: title, first day, last day (null for one day), calendar.</summary>
    public Func<string, string, string?, string?, string> AllDayEvent { get; init; } =
        (title, first, last, calendar) => Join(title, "all day", last is null ? first : $"{first} to {last}", calendar, null);

    /// <summary>The name of an agenda row: title, start, end, calendar and location (no date; the day's heading says it).</summary>
    public Func<string, string, string, string?, string?, string> AgendaEvent { get; init; } =
        (title, start, end, calendar, location) => Join(title, $"{start} to {end}", null, calendar, location);

    /// <summary>The name of an all-day agenda row: title, calendar and location.</summary>
    public Func<string, string?, string?, string> AgendaAllDayEvent { get; init; } =
        (title, calendar, location) => Join(title, "all day", null, calendar, location);

    /// <summary>Announced when the days shown change; the argument is the new title.</summary>
    public Func<string, string> RangeAnnouncement { get; init; } = title => title;

    /// <summary>Announced when the view changes: the view's name and the title.</summary>
    public Func<string, string, string> ViewAnnouncement { get; init; } = (view, title) => $"{view}, {title}";

    /// <summary>Announced when the day view opens; the argument is the day.</summary>
    public Func<string, string> DayViewAnnouncement { get; init; } = day => $"Day view, {day}";

    /// <summary>Announced when a calendar is shown or hidden: its name and whether it is shown now.</summary>
    public Func<string, bool, string> CalendarToggled { get; init; } = (name, shown) => shown ? $"{name} shown" : $"{name} hidden";

    /// <summary>The title of an event without one.</summary>
    public string Untitled { get; init; } = "Untitled event";

    // Editing ------------------------------------------------------------------------------------------------------------

    /// <summary>The help text of the grid when editing is on, read after <see cref="GridHelp"/>.</summary>
    public string EditingHelp { get; init; } =
        "Drag across empty time to add an event, drag an event to move it, or drag its bottom edge to change its length. " +
        "N adds an event on the focused day, Enter opens the focused event, Delete removes it, and Alt with the arrow keys moves it.";

    /// <summary>The title drawn on the block of an event being drafted.</summary>
    public string DraftTitle { get; init; } = "New event";

    /// <summary>The screen reader name of the quick create popover.</summary>
    public string QuickCreateName { get; init; } = "New event";

    /// <summary>The screen reader name of the details popover.</summary>
    public string DetailsName { get; init; } = "Event details";

    /// <summary>The hint in the empty title box of the quick create popover.</summary>
    public string TitlePlaceholder { get; init; } = "Add a title";

    /// <summary>The screen reader name of the title boxes.</summary>
    public string TitleLabel { get; init; } = "Title";

    /// <summary>The screen reader name of the location box in the details.</summary>
    public string LocationLabel { get; init; } = "Location";

    /// <summary>The quiet hint beside the quick create buttons.</summary>
    public string SaveHint { get; init; } = "Enter to save";

    /// <summary>The quick create popover's save button.</summary>
    public string Save { get; init; } = "Save";

    /// <summary>The quick create popover's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The details popover's close button (text).</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The details popover's delete button.</summary>
    public string Delete { get; init; } = "Delete";

    /// <summary>The screen reader name of the details popover's close (×) button.</summary>
    public string CloseDetails { get; init; } = "Close";

    /// <summary>The screen reader name of the calendar choice in the popovers.</summary>
    public string CalendarChoice { get; init; } = "Calendar";

    /// <summary>The screen reader name of the date field in the details.</summary>
    public string DateLabel { get; init; } = "Date";

    /// <summary>The screen reader name of the start time field in the details.</summary>
    public string StartLabel { get; init; } = "Starts";

    /// <summary>The screen reader name of the end time field in the details.</summary>
    public string EndLabel { get; init; } = "Ends";

    /// <summary>The word between the start and end fields.</summary>
    public string RangeSeparator { get; init; } = "to";

    /// <summary>The undo toast's undo button.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>The screen reader name of the undo toast's close button.</summary>
    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>A length in minutes: "45 min", "1 hr", "1 hr 45 min".</summary>
    public Func<int, string> Duration { get; init; } = minutes =>
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0 ? $"{rest} min" : rest == 0 ? $"{hours} hr" : $"{hours} hr {rest} min";
    };

    /// <summary>The attendees line of the details: the first person's name and how many others there are.</summary>
    public Func<string, int, string> Attendees { get; init; } = (first, others) =>
        others == 0 ? first : others == 1 ? $"{first} and 1 other" : $"{first} and {others} others";

    /// <summary>Announced (and shown in the toast) when an event is added: title, day, start and end.</summary>
    public Func<string, string, string, string, string> CreatedAnnouncement { get; init; } =
        (title, day, start, end) => $"{title} added on {day}, {start} to {end}";

    /// <summary>Announced when an event is dragged to another time or day: title, day and start.</summary>
    public Func<string, string, string, string> MovedAnnouncement { get; init; } =
        (title, day, start) => $"{title} moved to {day}, {start}";

    /// <summary>Announced when the details change an event's day or times: title, day and time range.</summary>
    public Func<string, string, string, string> RescheduledAnnouncement { get; init; } =
        (title, day, range) => $"{title} is now on {day}, {range}";

    /// <summary>Announced when an event's bottom edge is dragged: title and time range.</summary>
    public Func<string, string, string> ResizedAnnouncement { get; init; } =
        (title, range) => $"{title} now runs {range}";

    /// <summary>Announced when the keyboard moves or resizes an event: title, day, start and end.</summary>
    public Func<string, string, string, string, string> NudgedAnnouncement { get; init; } =
        (title, day, start, end) => $"{title}, {day}, {start} to {end}";

    /// <summary>Announced when an event moves to another calendar: title and calendar.</summary>
    public Func<string, string, string> CalendarChangedAnnouncement { get; init; } =
        (title, calendar) => $"{title} moved to {calendar}";

    /// <summary>Announced when an event gets a new title; the argument is the title.</summary>
    public Func<string, string> RenamedAnnouncement { get; init; } = title => $"Title changed to {title}";

    /// <summary>Announced (and shown in the toast) when an event is deleted; the argument is its title.</summary>
    public Func<string, string> DeletedAnnouncement { get; init; } = title => $"{title} deleted";

    /// <summary>Announced when undo brings a deleted event back; the argument is its title.</summary>
    public Func<string, string> RestoredAnnouncement { get; init; } = title => $"{title} restored";

    /// <summary>Announced when undo reverts a change; the argument is what was announced for the change.</summary>
    public Func<string, string> UndoneAnnouncement { get; init; } = text => $"Reverted: {text}";

    /// <summary>Announced when Escape cancels a drag.</summary>
    public string DragCanceledAnnouncement { get; init; } = "Change canceled";

    /// <summary>Announced when Escape clears the selection.</summary>
    public string SelectionClearedAnnouncement { get; init; } = "No event selected";

    private static string Join(string first, string? second, string? third, string? fourth, string? fifth)
    {
        var parts = new List<string>(5) { first };
        foreach (var part in new[] { second, third, fourth, fifth })
        {
            if (!string.IsNullOrWhiteSpace(part))
            {
                parts.Add(part);
            }
        }
        return string.Join(", ", parts);
    }
}
