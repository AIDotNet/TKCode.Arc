using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarPreviewDemo : UserControl
{
    public WeekCalendarPreviewDemo()
    {
        InitializeComponent();
        Calendar.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        // Built around this week, so the current time line has past events behind it and coming ones ahead.
        Calendar.Events.AddRange(WeekCalendarDemoData.CreateEvents(WeekCalendarDemoData.ThisMonday()));
    }
}
