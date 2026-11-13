using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of an event about one <see cref="MonthCalendarEvent"/> (created, deleted, invoked).</summary>
public sealed class MonthCalendarEventEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MonthCalendarEventEventArgs(RoutedEvent routedEvent, MonthCalendarEvent calendarEvent) : base(routedEvent)
    {
        Event = calendarEvent;
    }

    /// <summary>The event.</summary>
    public MonthCalendarEvent Event { get; }
}

/// <summary>Arguments of <see cref="MonthCalendar.EventChanged"/>: the event after the change and a copy of it from before.</summary>
public sealed class MonthCalendarEventChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MonthCalendarEventChangedEventArgs(RoutedEvent routedEvent, MonthCalendarEvent calendarEvent, MonthCalendarEvent previous)
        : base(routedEvent)
    {
        Event = calendarEvent;
        Previous = previous;
    }

    /// <summary>The event, already changed.</summary>
    public MonthCalendarEvent Event { get; }

    /// <summary>A copy of the event from before the change.</summary>
    public MonthCalendarEvent Previous { get; }
}

/// <summary>
/// Arguments of <see cref="MonthCalendar.EventCreating"/>: set <see cref="ErrorMessage"/> to keep the new event form open
/// and show the message under it (the event is then not added).
/// </summary>
public sealed class MonthCalendarEventCreatingEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MonthCalendarEventCreatingEventArgs(RoutedEvent routedEvent, MonthCalendarEvent calendarEvent) : base(routedEvent)
    {
        Event = calendarEvent;
    }

    /// <summary>The event about to be added (not in <see cref="MonthCalendar.Events"/> yet).</summary>
    public MonthCalendarEvent Event { get; }

    /// <summary>A message that refuses the event; null or empty lets it through.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>Arguments of <see cref="MonthCalendar.RsvpChanged"/>: the user answered an invitation.</summary>
public sealed class MonthCalendarRsvpChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MonthCalendarRsvpChangedEventArgs(RoutedEvent routedEvent, MonthCalendarEvent calendarEvent, MonthCalendarRsvp oldValue,
                                             MonthCalendarRsvp newValue) : base(routedEvent)
    {
        Event = calendarEvent;
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The event.</summary>
    public MonthCalendarEvent Event { get; }

    /// <summary>The reply before.</summary>
    public MonthCalendarRsvp OldValue { get; }

    /// <summary>The reply now.</summary>
    public MonthCalendarRsvp NewValue { get; }
}

/// <summary>Arguments of <see cref="MonthCalendar.MonthChanged"/> and <see cref="MonthCalendar.SelectedDateChanged"/>.</summary>
public sealed class MonthCalendarDateChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MonthCalendarDateChangedEventArgs(RoutedEvent routedEvent, DateOnly oldValue, DateOnly newValue) : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The value before (for the month: its first day).</summary>
    public DateOnly OldValue { get; }

    /// <summary>The value now.</summary>
    public DateOnly NewValue { get; }
}
