namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="MonthCalendar"/> shows or reads to screen readers, in English by default. Replace the instance
/// (<see cref="MonthCalendar.Labels"/>) to translate or reword. Texts with <c>{0}</c>, <c>{1}</c> … are composite format
/// strings; dates inside them are formatted with <see cref="MonthCalendar.Culture"/> and the date patterns below.
/// </summary>
public sealed class MonthCalendarLabels
{
    // Sidebar ----------------------------------------------------------------------------------------------------------

    /// <summary>The sidebar's screen reader name ({0}: the workspace).</summary>
    public string SidebarName { get; init; } = "{0} calendar";

    /// <summary>The sidebar search placeholder and name.</summary>
    public string Search { get; init; } = "Search";

    /// <summary>The sidebar search's empty result ({0}: the text).</summary>
    public string NoMatches { get; init; } = "No matches for “{0}”";

    /// <summary>The month view in the sidebar and the first breadcrumb.</summary>
    public string Calendar { get; init; } = "Calendar";

    /// <summary>The list of the next four weeks.</summary>
    public string Upcoming { get; init; } = "Upcoming";

    /// <summary>The meta text of the upcoming list.</summary>
    public string UpcomingMeta { get; init; } = "Next four weeks";

    /// <summary>The invitations view.</summary>
    public string Invitations { get; init; } = "Invitations";

    /// <summary>The meta text of the invitations view ({0}: how many).</summary>
    public string InvitationsMeta { get; init; } = "{0} waiting for a reply";

    /// <summary>The invitations view when none are left.</summary>
    public string NoInvitations { get; init; } = "You are all caught up.";

    /// <summary>The heading of the user's calendars.</summary>
    public string MyCalendars { get; init; } = "My calendars";

    /// <summary>The heading of the teammates' calendars.</summary>
    public string TeamCalendars { get; init; } = "Team calendars";

    /// <summary>The add-a-teammate button's name.</summary>
    public string AddTeammate { get; init; } = "Add a teammate";

    /// <summary>A calendar toggle's name ({0}: name, {1}: events this month).</summary>
    public string CalendarToggleName { get; init; } = "{0}, {1} events this month";

    /// <summary>The pill that shows only one calendar.</summary>
    public string Only { get; init; } = "Only";

    /// <summary>The Only pill's name ({0}: calendar).</summary>
    public string ShowOnly { get; init; } = "Show only {0}";

    /// <summary>The pill that shows every calendar again (when only one is shown).</summary>
    public string All { get; init; } = "All";

    /// <summary>The All pill's name.</summary>
    public string ShowAll { get; init; } = "Show all calendars";

    /// <summary>The collapse button of the sidebar.</summary>
    public string CollapseSidebar { get; init; } = "Collapse sidebar";

    /// <summary>The expand button of the collapsed sidebar.</summary>
    public string ExpandSidebar { get; init; } = "Expand sidebar";

    /// <summary>The menu button that opens the sidebar on narrow screens.</summary>
    public string OpenNavigation { get; init; } = "Open navigation";

    /// <summary>The close button of the sidebar sheet.</summary>
    public string CloseNavigation { get; init; } = "Close navigation";

    /// <summary>The account menu: start weeks on Monday.</summary>
    public string StartWeeksOnMonday { get; init; } = "Start weeks on Monday";

    /// <summary>The account menu: show weekends.</summary>
    public string ShowWeekends { get; init; } = "Show weekends";

    /// <summary>The account menu and the dialog title: keyboard shortcuts.</summary>
    public string KeyboardShortcuts { get; init; } = "Keyboard shortcuts";

    /// <summary>The account menu: sign out.</summary>
    public string SignOut { get; init; } = "Sign out";

    // Header -----------------------------------------------------------------------------------------------------------

    /// <summary>The breadcrumb of a teammate's calendar.</summary>
    public string TeamCalendarsCrumb { get; init; } = "Team calendars";

    /// <summary>The header count ({0}: events).</summary>
    public string EventCount { get; init; } = "{0} events";

    /// <summary>The header count while filtering ({0}: matches, {1}: events).</summary>
    public string MatchCount { get; init; } = "{0} of {1} match";

    /// <summary>The filter field placeholder and name.</summary>
    public string FilterEvents { get; init; } = "Filter events";

    /// <summary>The Today button.</summary>
    public string Today { get; init; } = "Today";

    /// <summary>The previous month button's name.</summary>
    public string PreviousMonth { get; init; } = "Previous month";

    /// <summary>The next month button's name.</summary>
    public string NextMonth { get; init; } = "Next month";

    /// <summary>The New event button.</summary>
    public string NewEvent { get; init; } = "New event";

    /// <summary>The panel toggle's name while the panel shows.</summary>
    public string HideDay { get; init; } = "Hide day";

    /// <summary>The panel toggle's name while the panel is hidden.</summary>
    public string ShowDay { get; init; } = "Show day";

    /// <summary>The panel's screen reader name.</summary>
    public string DayPanel { get; init; } = "Day";

    /// <summary>The resize handle's name.</summary>
    public string ResizeDayPanel { get; init; } = "Resize day panel";

    /// <summary>The close button of the panel sheet.</summary>
    public string CloseDay { get; init; } = "Close day";

    // Grid -------------------------------------------------------------------------------------------------------------

    /// <summary>A day button's name ({0}: the long date, {1}: ", today" or empty, {2}: the event count text).</summary>
    public string DayName { get; init; } = "{0}{1}, {2}";

    /// <summary>The today part of a day name.</summary>
    public string TodaySuffix { get; init; } = ", today";

    /// <summary>A day with no events.</summary>
    public string NoEvents { get; init; } = "no events";

    /// <summary>A day with one event.</summary>
    public string OneEvent { get; init; } = "1 event";

    /// <summary>A day with several events ({0}).</summary>
    public string ManyEvents { get; init; } = "{0} events";

    /// <summary>The "N more" text ({0}).</summary>
    public string More { get; init; } = "{0} more";

    /// <summary>The "N more" name ({0}: count, {1}: the long date).</summary>
    public string MoreName { get; init; } = "{0} more events on {1}";

    /// <summary>Appended to the name of an invitation waiting for a reply.</summary>
    public string AwaitingYourReplySuffix { get; init; } = ", awaiting your reply";

    /// <summary>Appended to the name of a declined event.</summary>
    public string DeclinedSuffix { get; init; } = ", declined";

    // Day panel --------------------------------------------------------------------------------------------------------

    /// <summary>The badge next to today's title.</summary>
    public string TodayBadge { get; init; } = "Today";

    /// <summary>The day meta ({0}: events text, {1}: booked, {2}: free).</summary>
    public string DayMeta { get; init; } = "{0} · {1} booked · {2} free";

    /// <summary>The all-day group label.</summary>
    public string AllDay { get; init; } = "All day";

    /// <summary>The schedule group label.</summary>
    public string Schedule { get; init; } = "Schedule";

    /// <summary>A free gap ({0}: start, {1}: end).</summary>
    public string FreeGap { get; init; } = "Free {0} to {1}";

    /// <summary>The button that blocks a free gap.</summary>
    public string BlockFocusTime { get; init; } = "Block focus time";

    /// <summary>The title of an event made from a free gap.</summary>
    public string FocusTime { get; init; } = "Focus time";

    /// <summary>The panel's footer button ({0}: the short date, "Nov 12").</summary>
    public string NewEventOn { get; init; } = "New event on {0}";

    /// <summary>The empty schedule.</summary>
    public string NothingPlanned { get; init; } = "Nothing planned";

    /// <summary>A date range ({0}: first, {1}: last).</summary>
    public string DateRange { get; init; } = "{0} to {1}";

    /// <summary>The when-line of an all-day event ({0}: the date or range).</summary>
    public string AllDayWhen { get; init; } = "{0}, all day";

    /// <summary>The when-line of a timed event ({0}: date, {1}: start, {2}: end).</summary>
    public string TimedWhen { get; init; } = "{0}, {1} to {2}";

    /// <summary>The length fact ({0}: length, {1}: end time).</summary>
    public string DurationFact { get; init; } = "{0} · ends {1}";

    /// <summary>The hidden name of the video fact.</summary>
    public string VideoCall { get; init; } = "Video call";

    /// <summary>The copy button.</summary>
    public string CopyLink { get; init; } = "Copy link";

    /// <summary>The copy button after copying.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The reply group of an unanswered invitation ({0}: who invited).</summary>
    public string InvitedYou { get; init; } = "{0} invited you";

    /// <summary>The reply group once answered.</summary>
    public string YourReply { get; init; } = "Your reply";

    /// <summary>The reply: going.</summary>
    public string Going { get; init; } = "Going";

    /// <summary>The reply: maybe.</summary>
    public string Maybe { get; init; } = "Maybe";

    /// <summary>The reply: not going (the segment).</summary>
    public string No { get; init; } = "No";

    /// <summary>A guest who declined.</summary>
    public string Declined { get; init; } = "Declined";

    /// <summary>A guest who has not answered.</summary>
    public string AwaitingReply { get; init; } = "Awaiting reply";

    /// <summary>A row of the user's own invitation ("Awaiting your reply").</summary>
    public string AwaitingYourReply { get; init; } = "Awaiting your reply";

    /// <summary>The guests group ({0}: guests, {1}: going).</summary>
    public string GuestsSummary { get; init; } = "{0} guests · {1} going";

    /// <summary>The organizer under a guest's name.</summary>
    public string Organizer { get; init; } = "Organizer";

    /// <summary>Appended to the current user's name in the guest list.</summary>
    public string YouSuffix { get; init; } = " (you)";

    /// <summary>The notes group.</summary>
    public string Notes { get; init; } = "Notes";

    /// <summary>The calendar picker label.</summary>
    public string CalendarField { get; init; } = "Calendar";

    /// <summary>The delete button's name ({0}: title).</summary>
    public string DeleteEvent { get; init; } = "Delete {0}";

    // New event form ---------------------------------------------------------------------------------------------------

    /// <summary>The title field.</summary>
    public string Title { get; init; } = "Title";

    /// <summary>The title placeholder (and the error when it is empty).</summary>
    public string AddTitle { get; init; } = "Add a title";

    /// <summary>The all-day checkbox.</summary>
    public string AllDayCheckbox { get; init; } = "All day";

    /// <summary>The start time field.</summary>
    public string Starts { get; init; } = "Starts";

    /// <summary>The end time field.</summary>
    public string Ends { get; init; } = "Ends";

    /// <summary>The location field.</summary>
    public string Location { get; init; } = "Location";

    /// <summary>The location placeholder.</summary>
    public string LocationPlaceholder { get; init; } = "Room, address or link";

    /// <summary>The video link checkbox.</summary>
    public string AddVideoLink { get; init; } = "Add a video link";

    /// <summary>The video link checkbox's description.</summary>
    public string AddVideoLinkHint { get; init; } = "Guests get the link with the invite";

    /// <summary>The name of the video service new events get.</summary>
    public string DefaultVideoProvider { get; init; } = "Video room";

    /// <summary>The guests field.</summary>
    public string Guests { get; init; } = "Guests";

    /// <summary>The guests hint with nobody picked.</summary>
    public string GuestsHint { get; init; } = "Invite teammates, or keep it to yourself";

    /// <summary>The guests hint ({0}: first names joined by ", ").</summary>
    public string GuestsInvite { get; init; } = "{0} will get an invite";

    /// <summary>The form's cancel button.</summary>
    public string Cancel { get; init; } = "Cancel";

    /// <summary>The form's save button.</summary>
    public string SaveEvent { get; init; } = "Save event";

    /// <summary>The ending time must come after the start.</summary>
    public string EndBeforeStart { get; init; } = "End after the start";

    // Toasts and announcements -----------------------------------------------------------------------------------------

    /// <summary>Undo in the toast.</summary>
    public string Undo { get; init; } = "Undo";

    /// <summary>View in the toast.</summary>
    public string View { get; init; } = "View";

    /// <summary>The toast's dismiss button.</summary>
    public string Dismiss { get; init; } = "Dismiss";

    /// <summary>A move ({0}: title, {1}: short date).</summary>
    public string MovedTo { get; init; } = "Moved {0} to {1}";

    /// <summary>A delete ({0}: title).</summary>
    public string Deleted { get; init; } = "Deleted {0}";

    /// <summary>An undone delete or move ({0}: title).</summary>
    public string Restored { get; init; } = "Restored {0}";

    /// <summary>A new event ({0}: title, {1}: short date).</summary>
    public string Added { get; init; } = "Added {0} on {1}";

    /// <summary>A focus block ({0}: start, {1}: end).</summary>
    public string Blocked { get; init; } = "Blocked {0} to {1} for focus";

    /// <summary>Going ({0}: title).</summary>
    public string Accepted { get; init; } = "Accepted {0}";

    /// <summary>Maybe ({0}: title).</summary>
    public string RepliedMaybe { get; init; } = "Replied maybe to {0}";

    /// <summary>Not going ({0}: title).</summary>
    public string DeclinedToast { get; init; } = "Declined {0}";

    /// <summary>A calendar change ({0}: title, {1}: calendar).</summary>
    public string MovedToCalendar { get; init; } = "Moved {0} to {1}";

    // Invitations and upcoming -----------------------------------------------------------------------------------------

    /// <summary>The invitation card's sender line ({0}: who).</summary>
    public string InvitedYouCard { get; init; } = "{0} invited you";

    /// <summary>The accept button.</summary>
    public string Accept { get; init; } = "Accept";

    /// <summary>The decline button.</summary>
    public string Decline { get; init; } = "Decline";

    /// <summary>The button that opens the invitation in the month.</summary>
    public string ViewInCalendar { get; init; } = "View in calendar";

    /// <summary>A guest count ({0}).</summary>
    public string GuestCount { get; init; } = "{0} guests";

    /// <summary>The list heading for today.</summary>
    public string TodayHeading { get; init; } = "Today";

    /// <summary>The list heading for tomorrow.</summary>
    public string TomorrowHeading { get; init; } = "Tomorrow";

    /// <summary>The phone list's add button.</summary>
    public string Add { get; init; } = "Add";

    /// <summary>The upcoming list when nothing is planned.</summary>
    public string NothingUpcoming { get; init; } = "Nothing in the next four weeks.";

    // Shortcuts --------------------------------------------------------------------------------------------------------

    /// <summary>Shortcut row: arrows.</summary>
    public string ShortcutMoveDays { get; init; } = "Move between days";

    /// <summary>Shortcut row: Enter.</summary>
    public string ShortcutOpenDay { get; init; } = "Open the focused day";

    /// <summary>Shortcut row: Page up / down.</summary>
    public string ShortcutMonths { get; init; } = "Previous or next month";

    /// <summary>Shortcut row: T.</summary>
    public string ShortcutToday { get; init; } = "Jump to today";

    /// <summary>Shortcut row: N.</summary>
    public string ShortcutNewEvent { get; init; } = "New event on the selected day";

    /// <summary>Shortcut row: /.</summary>
    public string ShortcutFilter { get; init; } = "Filter events";

    /// <summary>Shortcut row: Alt and arrows.</summary>
    public string ShortcutMoveEvent { get; init; } = "Move the focused event";

    /// <summary>Shortcut row: Delete.</summary>
    public string ShortcutDeleteEvent { get; init; } = "Delete the focused event";

    /// <summary>Shortcut row: Escape.</summary>
    public string ShortcutBack { get; init; } = "Back to the day";

    /// <summary>Shortcut row: Ctrl or Cmd + K.</summary>
    public string ShortcutSearch { get; init; } = "Search the sidebar";

    /// <summary>The key-cap text of the arrows row.</summary>
    public string ArrowKeys { get; init; } = "Arrow keys";

    /// <summary>The key-cap text of the months row.</summary>
    public string PageKeys { get; init; } = "Page up, Page down";

    /// <summary>The key-cap text of the move row.</summary>
    public string AltArrows { get; init; } = "Alt and arrows";

    /// <summary>The close button of dialogs.</summary>
    public string Close { get; init; } = "Close";

    // Date patterns ----------------------------------------------------------------------------------------------------

    /// <summary>The month title ("November 2026").</summary>
    public string MonthTitleFormat { get; init; } = "MMMM yyyy";

    /// <summary>The narrow month title ("Nov 2026").</summary>
    public string MonthTitleNarrowFormat { get; init; } = "MMM yyyy";

    /// <summary>The day panel title and day names ("Thursday, November 12").</summary>
    public string LongDateFormat { get; init; } = "dddd, MMMM d";

    /// <summary>Dates in the details and toasts ("Thu, Nov 12").</summary>
    public string ShortDateFormat { get; init; } = "ddd, MMM d";

    /// <summary>Dates without a weekday ("Nov 12").</summary>
    public string MonthDayFormat { get; init; } = "MMM d";
}
