using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A reply to an invitation, or the state of one guest on an event.</summary>
public enum MonthCalendarRsvp
{
    /// <summary>Not an invitation (the person's own event, or no reply asked for).</summary>
    None,

    /// <summary>Invited and not answered yet: the event shows outlined and is listed under Invitations.</summary>
    Pending,

    /// <summary>Going.</summary>
    Yes,

    /// <summary>Maybe.</summary>
    Maybe,

    /// <summary>Not going: the title is struck through and the event does not count as busy.</summary>
    No,
}

/// <summary>The preset colors of a calendar (each tuned for the light and the dark theme), or a custom one.</summary>
public enum MonthCalendarTone
{
    /// <summary>Teal.</summary>
    Teal,

    /// <summary>Violet.</summary>
    Violet,

    /// <summary>Amber.</summary>
    Amber,

    /// <summary>Blue.</summary>
    Blue,

    /// <summary>Rose.</summary>
    Rose,

    /// <summary>Green.</summary>
    Green,

    /// <summary>Gray.</summary>
    Neutral,

    /// <summary><see cref="MonthCalendarSource.CustomColor"/> in both themes.</summary>
    Custom,
}

/// <summary>What the main area of a <see cref="MonthCalendar"/> shows.</summary>
public enum MonthCalendarView
{
    /// <summary>The month grid (with the day panel).</summary>
    Calendar,

    /// <summary>The next four weeks as a list grouped by day.</summary>
    Upcoming,

    /// <summary>The invitations waiting for a reply, as cards.</summary>
    Invitations,

    /// <summary>The month grid of one teammate (<see cref="MonthCalendar.TeammateId"/>).</summary>
    Teammate,
}

/// <summary>
/// One event of a <see cref="MonthCalendar"/>. Dates are calendar dates (no time zone): <see cref="StartDate"/> and the
/// inclusive <see cref="EndDate"/>. Without <see cref="StartTime"/> the event lasts all day; with it, it is a timed event
/// (24 hour <see cref="StartTime"/> and <see cref="EndTime"/>). All-day and multi-day events show as tinted bars, timed
/// events as chips with the time in the calendar's color.
/// </summary>
public sealed class MonthCalendarEvent : AvaloniaObject
{
    /// <summary>A stable identifier (selection, focus, undo and the callbacks use it).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string>(nameof(Id), string.Empty);

    /// <summary>The title.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string>(nameof(Title), string.Empty);

    /// <summary>The <see cref="MonthCalendarSource.Id"/> of its calendar; an unknown calendar draws gray.</summary>
    public static readonly StyledProperty<string?> CalendarIdProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(CalendarId));

    /// <summary>The first date.</summary>
    public static readonly StyledProperty<DateOnly> StartDateProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, DateOnly>(nameof(StartDate));

    /// <summary>The last date (inclusive); null for a one-day event.</summary>
    public static readonly StyledProperty<DateOnly?> EndDateProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, DateOnly?>(nameof(EndDate));

    /// <summary>The start time; null makes it an all-day event.</summary>
    public static readonly StyledProperty<TimeOnly?> StartTimeProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, TimeOnly?>(nameof(StartTime));

    /// <summary>The end time; null ends an hour after <see cref="StartTime"/>.</summary>
    public static readonly StyledProperty<TimeOnly?> EndTimeProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, TimeOnly?>(nameof(EndTime));

    /// <summary>A place (a room, an address or a link) shown under the title in lists.</summary>
    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(Location));

    /// <summary>The <see cref="MonthCalendarPerson.Id"/> of the person who sent the invitation.</summary>
    public static readonly StyledProperty<string?> OrganizerIdProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(OrganizerId));

    /// <summary>
    /// The current user's reply (<see cref="MonthCalendar.CurrentUserId"/>): <see cref="MonthCalendarRsvp.Pending"/> marks an
    /// invitation; <see cref="MonthCalendarRsvp.None"/> is the user's own event.
    /// </summary>
    public static readonly StyledProperty<MonthCalendarRsvp> RsvpProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, MonthCalendarRsvp>(nameof(Rsvp));

    /// <summary>The video call service shown with its mark (for example "Video room"); null for no call.</summary>
    public static readonly StyledProperty<string?> VideoProviderProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(VideoProvider));

    /// <summary>The call link the details offer to copy.</summary>
    public static readonly StyledProperty<string?> VideoUrlProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(VideoUrl));

    /// <summary>Free text shown in the details and on invitation cards.</summary>
    public static readonly StyledProperty<string?> NotesProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, string?>(nameof(Notes));

    /// <summary>Whether the event can be moved, answered, recalendared or deleted (false keeps it read only).</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the event shows at all (false hides it everywhere without removing it).</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, bool>(nameof(IsVisible), true);

    /// <summary>Any object of the app's own.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<MonthCalendarEvent, object?>(nameof(Tag));

    /// <summary>Creates an empty event.</summary>
    public MonthCalendarEvent()
    {
    }

    /// <summary>Creates a timed event, or an all-day event when <paramref name="startTime"/> is null.</summary>
    public MonthCalendarEvent(string id, string title, string? calendarId, DateOnly startDate, TimeOnly? startTime = null,
                              TimeOnly? endTime = null)
    {
        Id = id;
        Title = title;
        CalendarId = calendarId;
        StartDate = startDate;
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="CalendarIdProperty"/>
    public string? CalendarId { get => GetValue(CalendarIdProperty); set => SetValue(CalendarIdProperty, value); }

    /// <inheritdoc cref="StartDateProperty"/>
    public DateOnly StartDate { get => GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }

    /// <inheritdoc cref="EndDateProperty"/>
    public DateOnly? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }

    /// <inheritdoc cref="StartTimeProperty"/>
    public TimeOnly? StartTime { get => GetValue(StartTimeProperty); set => SetValue(StartTimeProperty, value); }

    /// <inheritdoc cref="EndTimeProperty"/>
    public TimeOnly? EndTime { get => GetValue(EndTimeProperty); set => SetValue(EndTimeProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="OrganizerIdProperty"/>
    public string? OrganizerId { get => GetValue(OrganizerIdProperty); set => SetValue(OrganizerIdProperty, value); }

    /// <inheritdoc cref="RsvpProperty"/>
    public MonthCalendarRsvp Rsvp { get => GetValue(RsvpProperty); set => SetValue(RsvpProperty, value); }

    /// <inheritdoc cref="VideoProviderProperty"/>
    public string? VideoProvider { get => GetValue(VideoProviderProperty); set => SetValue(VideoProviderProperty, value); }

    /// <inheritdoc cref="VideoUrlProperty"/>
    public string? VideoUrl { get => GetValue(VideoUrlProperty); set => SetValue(VideoUrlProperty, value); }

    /// <inheritdoc cref="NotesProperty"/>
    public string? Notes { get => GetValue(NotesProperty); set => SetValue(NotesProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The guests and their replies (the organizer and the current user may be among them).</summary>
    public AvaloniaList<MonthCalendarAttendee> Attendees { get; } = new();

    /// <summary>True when the event has no start time.</summary>
    public bool IsAllDay => StartTime is null;

    /// <summary>The last date (<see cref="EndDate"/>, or <see cref="StartDate"/> when it is unset or earlier).</summary>
    public DateOnly LastDate => EndDate is { } end && end > StartDate ? end : StartDate;

    /// <summary>True when the event covers more than one date.</summary>
    public bool IsMultiDay => LastDate > StartDate;

    /// <summary>The end time used for timed events (<see cref="EndTime"/>, or an hour after the start).</summary>
    public TimeOnly EffectiveEndTime =>
        EndTime ?? (StartTime is { } start ? (start.Hour >= 23 ? new TimeOnly(23, 59) : start.AddHours(1)) : TimeOnly.MinValue);

    /// <summary>A copy with the same values and guests (undo keeps one).</summary>
    public MonthCalendarEvent Clone()
    {
        var copy = new MonthCalendarEvent
        {
            Id = Id,
            Title = Title,
            CalendarId = CalendarId,
            StartDate = StartDate,
            EndDate = EndDate,
            StartTime = StartTime,
            EndTime = EndTime,
            Location = Location,
            OrganizerId = OrganizerId,
            Rsvp = Rsvp,
            VideoProvider = VideoProvider,
            VideoUrl = VideoUrl,
            Notes = Notes,
            IsEnabled = IsEnabled,
            IsVisible = IsVisible,
            Tag = Tag,
        };
        foreach (var attendee in Attendees)
        {
            copy.Attendees.Add(new MonthCalendarAttendee(attendee.PersonId, attendee.Status));
        }
        return copy;
    }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>One guest of a <see cref="MonthCalendarEvent"/> and their reply.</summary>
public sealed class MonthCalendarAttendee : AvaloniaObject
{
    /// <summary>The <see cref="MonthCalendarPerson.Id"/> of the guest.</summary>
    public static readonly StyledProperty<string> PersonIdProperty =
        AvaloniaProperty.Register<MonthCalendarAttendee, string>(nameof(PersonId), string.Empty);

    /// <summary>The guest's reply (<see cref="MonthCalendarRsvp.Pending"/> shows "Awaiting reply").</summary>
    public static readonly StyledProperty<MonthCalendarRsvp> StatusProperty =
        AvaloniaProperty.Register<MonthCalendarAttendee, MonthCalendarRsvp>(nameof(Status), MonthCalendarRsvp.Pending);

    /// <summary>Creates an empty guest.</summary>
    public MonthCalendarAttendee()
    {
    }

    /// <summary>Creates a guest with a reply.</summary>
    public MonthCalendarAttendee(string personId, MonthCalendarRsvp status)
    {
        PersonId = personId;
        Status = status;
    }

    /// <inheritdoc cref="PersonIdProperty"/>
    public string PersonId { get => GetValue(PersonIdProperty); set => SetValue(PersonIdProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public MonthCalendarRsvp Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }
}

/// <summary>One calendar of a <see cref="MonthCalendar"/>: a name, a color and an icon, drawn wherever its events appear.</summary>
public sealed class MonthCalendarSource : AvaloniaObject
{
    /// <summary>The identifier events refer to (<see cref="MonthCalendarEvent.CalendarId"/>).</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<MonthCalendarSource, string>(nameof(Id), string.Empty);

    /// <summary>The name in the sidebar, the details and the calendar pickers.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<MonthCalendarSource, string>(nameof(Name), string.Empty);

    /// <summary>The color preset.</summary>
    public static readonly StyledProperty<MonthCalendarTone> ToneProperty =
        AvaloniaProperty.Register<MonthCalendarSource, MonthCalendarTone>(nameof(Tone));

    /// <summary>The color used in both themes when <see cref="Tone"/> is <see cref="MonthCalendarTone.Custom"/>.</summary>
    public static readonly StyledProperty<Color?> CustomColorProperty =
        AvaloniaProperty.Register<MonthCalendarSource, Color?>(nameof(CustomColor));

    /// <summary>A 24 × 24 stroke icon (for example an <c>ArcIcons</c> glyph); null draws a calendar.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<MonthCalendarSource, Geometry?>(nameof(Icon));

    /// <summary>Whether its events show (two-way: the sidebar toggle, Only and All change it).</summary>
    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<MonthCalendarSource, bool>(nameof(IsShown), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether the sidebar toggle can be used.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MonthCalendarSource, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the calendar is listed in the sidebar and the pickers (its events still follow <see cref="IsShown"/>).</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<MonthCalendarSource, bool>(nameof(IsVisible), true);

    /// <summary>Creates an empty calendar.</summary>
    public MonthCalendarSource()
    {
    }

    /// <summary>Creates a calendar.</summary>
    public MonthCalendarSource(string id, string name, MonthCalendarTone tone, Geometry? icon = null)
    {
        Id = id;
        Name = name;
        Tone = tone;
        Icon = icon;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ToneProperty"/>
    public MonthCalendarTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <inheritdoc cref="CustomColorProperty"/>
    public Color? CustomColor { get => GetValue(CustomColorProperty); set => SetValue(CustomColorProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <inheritdoc cref="IsShownProperty"/>
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>A person a <see cref="MonthCalendar"/> refers to: the current user, guests, organizers and teammates.</summary>
public sealed class MonthCalendarPerson : AvaloniaObject
{
    /// <summary>The identifier events refer to.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, string>(nameof(Id), string.Empty);

    /// <summary>The full name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, string>(nameof(Name), string.Empty);

    /// <summary>The email address (the account row shows the current user's).</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, string?>(nameof(Email));

    /// <summary>A picture; null shows the initials on a generated color.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, ImageSource?>(nameof(Avatar));

    /// <summary>Whether the person is listed under the sidebar's team calendars (the first five teammates are).</summary>
    public static readonly StyledProperty<bool> IsTeammateProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, bool>(nameof(IsTeammate), true);

    /// <summary>Whether the person's row in the sidebar can be used.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, bool>(nameof(IsEnabled), true);

    /// <summary>Whether the person is listed in the sidebar and the guest picker.</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<MonthCalendarPerson, bool>(nameof(IsVisible), true);

    /// <summary>Creates an empty person.</summary>
    public MonthCalendarPerson()
    {
    }

    /// <summary>Creates a person.</summary>
    public MonthCalendarPerson(string id, string name, string? email = null)
    {
        Id = id;
        Name = name;
        Email = email;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="IsTeammateProperty"/>
    public bool IsTeammate { get => GetValue(IsTeammateProperty); set => SetValue(IsTeammateProperty, value); }

    /// <inheritdoc cref="IsEnabledProperty"/>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <summary>The first word of the name.</summary>
    public string FirstName => Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? Name;

    /// <inheritdoc />
    public override string ToString() => Name;
}
