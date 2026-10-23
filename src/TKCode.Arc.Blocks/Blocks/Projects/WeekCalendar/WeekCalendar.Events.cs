using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>Events raised from the calendar's parts.</summary>
public partial class WeekCalendar
{
    /// <summary>
    /// An event was clicked or activated: raises <see cref="EventInvoked"/> and runs <see cref="EventInvokedCommand"/>. The
    /// editing stage opens the event's details when the event was not handled.
    /// </summary>
    internal WeekCalendarEventEventArgs InvokeEvent(WeekCalendarEvent calendarEvent, Control? source = null)
    {
        var args = new WeekCalendarEventEventArgs(EventInvokedEvent, calendarEvent);
        RaiseEvent(args);
        if (EventInvokedCommand is { } command && command.CanExecute(calendarEvent))
        {
            command.Execute(calendarEvent);
        }
        OnEventInvoked(args, source);
        return args;
    }

    /// <summary>The editing stage opens the details popover beside <paramref name="source"/> unless <paramref name="args"/> was handled.</summary>
    partial void OnEventInvoked(WeekCalendarEventEventArgs args, Control? source);

    /// <summary>Sets <c>:sliding</c> while panes move.</summary>
    internal void SetSliding(bool sliding) => PseudoClasses.Set(SlidingPseudoClass, sliding);
}
