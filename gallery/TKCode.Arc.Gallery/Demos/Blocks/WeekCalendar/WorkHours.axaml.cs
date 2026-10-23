using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarWorkHoursDemo : UserControl
{
    public WeekCalendarWorkHoursDemo()
    {
        InitializeComponent();
        Calendar.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        Calendar.Events.AddRange(WeekCalendarDemoData.CreateEvents(WeekCalendarDemoData.ThisMonday()));
    }
}
