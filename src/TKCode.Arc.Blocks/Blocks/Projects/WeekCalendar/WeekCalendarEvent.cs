using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One event of a <see cref="WeekCalendar"/>. <see cref="Start"/> and <see cref="End"/> are instants (store them in UTC or
/// with their offset); the calendar shows them in its <see cref="WeekCalendar.TimeZone"/>. An all-day event covers whole
/// dates: the dates of <see cref="Start"/> and <see cref="End"/> as written, with <see cref="End"/> exclusive (midnight
/// after the last day); its time zone is ignored. Changing any property moves or redraws the event at once.
/// </summary>
public sealed class WeekCalendarEvent : AvaloniaObject
{
    /// <summary>A stable id: focus, selection and callbacks follow it across rebuilds.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, string>(nameof(Id), string.Empty);

    /// <summary>The title shown on the event.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, string>(nameof(Title), string.Empty);

    /// <summary>When the event starts.</summary>
    public static readonly StyledProperty<DateTimeOffset> StartProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, DateTimeOffset>(nameof(Start), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>When the event ends (exclusive); an end at or before the start shows a 15 minute event.</summary>
    public static readonly StyledProperty<DateTimeOffset> EndProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, DateTimeOffset>(nameof(End), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The <see cref="WeekCalendarSource.Id"/> of its calendar; an unknown id draws the event in gray.</summary>
    public static readonly StyledProperty<string?> CalendarIdProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, string?>(nameof(CalendarId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>True shows the event in the all-day row, spanning its dates.</summary>
    public static readonly StyledProperty<bool> IsAllDayProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, bool>(nameof(IsAllDay));

    /// <summary>Where it takes place: a line with a pin on tall events, the second line in the agenda.</summary>
    public static readonly StyledProperty<string?> LocationProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, string?>(nameof(Location));

    /// <summary>The people invited; tall events show up to four avatars, the agenda three and a count.</summary>
    public static readonly StyledProperty<AvaloniaList<ProjectPerson>> AttendeesProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, AvaloniaList<ProjectPerson>>(nameof(Attendees));

    /// <summary>The screen reader name; null composes "title, day, start to end, calendar, location".</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, string?>(nameof(AutomationName));

    /// <summary>Any object the app wants to keep with the event.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<WeekCalendarEvent, object?>(nameof(Tag));

    public WeekCalendarEvent()
    {
        SetCurrentValue(AttendeesProperty, new AvaloniaList<ProjectPerson>());
    }

    /// <summary>Creates a timed event.</summary>
    /// <param name="id">The stable id.</param>
    /// <param name="title">The title.</param>
    /// <param name="start">The start.</param>
    /// <param name="end">The end (exclusive).</param>
    /// <param name="calendarId">The calendar's id.</param>
    public WeekCalendarEvent(string id, string title, DateTimeOffset start, DateTimeOffset end, string? calendarId = null) : this()
    {
        Id = id;
        Title = title;
        Start = start;
        End = end;
        CalendarId = calendarId;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="StartProperty"/>
    public DateTimeOffset Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }

    /// <inheritdoc cref="EndProperty"/>
    public DateTimeOffset End { get => GetValue(EndProperty); set => SetValue(EndProperty, value); }

    /// <inheritdoc cref="CalendarIdProperty"/>
    public string? CalendarId { get => GetValue(CalendarIdProperty); set => SetValue(CalendarIdProperty, value); }

    /// <inheritdoc cref="IsAllDayProperty"/>
    public bool IsAllDay { get => GetValue(IsAllDayProperty); set => SetValue(IsAllDayProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string? Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="AttendeesProperty"/>
    public AvaloniaList<ProjectPerson> Attendees { get => GetValue(AttendeesProperty); set => SetValue(AttendeesProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The length of the event (zero or more).</summary>
    public TimeSpan Duration => End > Start ? End - Start : TimeSpan.Zero;

    /// <summary>
    /// A copy with the same values and a new attendee list holding the same people (what
    /// <see cref="WeekCalendarEventUpdatedEventArgs.Previous"/> reports).
    /// </summary>
    public WeekCalendarEvent Clone()
    {
        var copy = new WeekCalendarEvent
        {
            Id = Id,
            Title = Title,
            Start = Start,
            End = End,
            CalendarId = CalendarId,
            IsAllDay = IsAllDay,
            Location = Location,
            AutomationName = AutomationName,
            Tag = Tag,
        };
        if (Attendees is { } people)
        {
            copy.Attendees.AddRange(people);
        }
        return copy;
    }

    /// <inheritdoc />
    public override string ToString() => Title;
}

/// <summary>
/// A calendar of a <see cref="WeekCalendar"/> ("Work", "Home"): its events take its color, and its toggle in the sidebar
/// (or the Calendars popover) shows or hides them.
/// </summary>
public sealed class WeekCalendarSource : AvaloniaObject
{
    /// <summary>The id events refer to through <see cref="WeekCalendarEvent.CalendarId"/>.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<WeekCalendarSource, string>(nameof(Id), string.Empty);

    /// <summary>The name in the calendars list and in event names read by screen readers.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<WeekCalendarSource, string>(nameof(Name), string.Empty);

    /// <summary>The palette color; <see cref="WeekCalendarColor.Custom"/> uses <see cref="CustomColor"/>.</summary>
    public static readonly StyledProperty<WeekCalendarColor> ColorProperty =
        AvaloniaProperty.Register<WeekCalendarSource, WeekCalendarColor>(nameof(Color));

    /// <summary>Any color for <see cref="WeekCalendarColor.Custom"/>; fills and text are mixed from it like the presets.</summary>
    public static readonly StyledProperty<Color?> CustomColorProperty =
        AvaloniaProperty.Register<WeekCalendarSource, Color?>(nameof(CustomColor));

    /// <summary>False hides the calendar's events (two-way; the toggle in the calendars list sets it).</summary>
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<WeekCalendarSource, bool>(nameof(IsVisible), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Any object the app wants to keep with the calendar.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<WeekCalendarSource, object?>(nameof(Tag));

    public WeekCalendarSource()
    {
    }

    /// <summary>Creates a calendar.</summary>
    /// <param name="id">The id events refer to.</param>
    /// <param name="name">The name.</param>
    /// <param name="color">The palette color.</param>
    public WeekCalendarSource(string id, string name, WeekCalendarColor color)
    {
        Id = id;
        Name = name;
        Color = color;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ColorProperty"/>
    public WeekCalendarColor Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <inheritdoc cref="CustomColorProperty"/>
    public Color? CustomColor { get => GetValue(CustomColorProperty); set => SetValue(CustomColorProperty, value); }

    /// <inheritdoc cref="IsVisibleProperty"/>
    public bool IsVisible { get => GetValue(IsVisibleProperty); set => SetValue(IsVisibleProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}
