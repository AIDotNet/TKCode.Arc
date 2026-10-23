using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarPhoneDemo : UserControl
{
    public WeekCalendarPhoneDemo()
    {
        InitializeComponent();
        Calendar.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        Calendar.Events.AddRange(WeekCalendarDemoData.CreateEvents(WeekCalendarDemoData.ThisMonday()));
    }
}
