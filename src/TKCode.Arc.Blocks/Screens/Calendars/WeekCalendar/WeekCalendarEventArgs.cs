using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="WeekCalendar.ViewChanged"/>.</summary>
public sealed class WeekCalendarViewChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarViewChangedEventArgs(RoutedEvent routedEvent, WeekCalendarView oldView, WeekCalendarView newView,
                                            WeekCalendarView effectiveView) : base(routedEvent)
    {
        OldView = oldView;
        NewView = newView;
        EffectiveView = effectiveView;
    }

    /// <summary>The view chosen before.</summary>
    public WeekCalendarView OldView { get; }

    /// <summary>The view chosen now (<see cref="WeekCalendar.View"/>).</summary>
    public WeekCalendarView NewView { get; }

    /// <summary>The view shown, after narrow calendars map the week views to three days.</summary>
    public WeekCalendarView EffectiveView { get; }
}

/// <summary>Arguments of <see cref="WeekCalendar.RangeChanged"/>: the days shown before and after.</summary>
public sealed class WeekCalendarRangeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarRangeChangedEventArgs(RoutedEvent routedEvent, DateOnly oldStart, DateOnly oldEnd, DateOnly newStart,
                                             DateOnly newEnd, int direction, WeekCalendarRangeChangeReason reason) : base(routedEvent)
    {
        OldStart = oldStart;
        OldEnd = oldEnd;
        NewStart = newStart;
        NewEnd = newEnd;
        Direction = direction;
        Reason = reason;
    }

    /// <summary>The first day shown before.</summary>
    public DateOnly OldStart { get; }

    /// <summary>The last day shown before.</summary>
    public DateOnly OldEnd { get; }

    /// <summary>The first day shown now.</summary>
    public DateOnly NewStart { get; }

    /// <summary>The last day shown now.</summary>
    public DateOnly NewEnd { get; }

    /// <summary>+1 for later days, −1 for earlier days, 0 when the range only changed its length (a view switch).</summary>
    public int Direction { get; }

    /// <summary>What moved the calendar.</summary>
    public WeekCalendarRangeChangeReason Reason { get; }
}

/// <summary>Arguments of <see cref="WeekCalendar.CalendarVisibilityChanged"/>.</summary>
public sealed class WeekCalendarVisibilityChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarVisibilityChangedEventArgs(RoutedEvent routedEvent, WeekCalendarSource calendar, bool isVisible) : base(routedEvent)
    {
        Calendar = calendar;
        IsVisible = isVisible;
    }

    /// <summary>The calendar that was shown or hidden.</summary>
    public WeekCalendarSource Calendar { get; }

    /// <summary>True when its events are shown now.</summary>
    public bool IsVisible { get; }
}

/// <summary>
/// Arguments of the events about one calendar event: <see cref="WeekCalendar.EventInvoked"/> (set
/// <see cref="RoutedEventArgs.Handled"/> to keep the calendar from opening its details), <see cref="WeekCalendar.EventCreated"/>
/// and <see cref="WeekCalendar.EventDeleted"/>.
/// </summary>
public sealed class WeekCalendarEventEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarEventEventArgs(RoutedEvent routedEvent, WeekCalendarEvent calendarEvent) : base(routedEvent)
    {
        Event = calendarEvent;
    }

    /// <summary>The event.</summary>
    public WeekCalendarEvent Event { get; }
}

/// <summary>Arguments of <see cref="WeekCalendar.EventUpdated"/>: the event as it is now and a copy of it before the change.</summary>
public sealed class WeekCalendarEventUpdatedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarEventUpdatedEventArgs(RoutedEvent routedEvent, WeekCalendarEvent calendarEvent, WeekCalendarEvent previous)
        : base(routedEvent)
    {
        Event = calendarEvent;
        Previous = previous;
    }

    /// <summary>The event, already changed.</summary>
    public WeekCalendarEvent Event { get; }

    /// <summary>A copy of the event before the change (<see cref="WeekCalendarEvent.Clone"/>).</summary>
    public WeekCalendarEvent Previous { get; }
}

/// <summary>Arguments of <see cref="WeekCalendar.NewEventRequested"/>.</summary>
public sealed class WeekCalendarNewEventRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public WeekCalendarNewEventRequestedEventArgs(RoutedEvent routedEvent, DateOnly date) : base(routedEvent)
    {
        Date = date;
    }

    /// <summary>The day the new event is for: the focused day of the calendar.</summary>
    public DateOnly Date { get; }
}
