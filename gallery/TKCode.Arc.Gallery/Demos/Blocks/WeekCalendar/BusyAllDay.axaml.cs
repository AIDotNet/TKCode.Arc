using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarBusyAllDayDemo : UserControl
{
    public WeekCalendarBusyAllDayDemo()
    {
        InitializeComponent();
        Calendar.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        Calendar.Events.AddRange(WeekCalendarDemoData.CreateBusyAllDayWeek(WeekCalendarDemoData.ThisMonday()));
        Log.Text = "Press the chevron under All day, or a +3 more chip, to see every lane.";
        Calendar.PropertyChanged += (_, e) =>
        {
            if (e.Property == WeekCalendar.IsAllDayExpandedProperty)
            {
                Log.Text = Calendar.IsAllDayExpanded ? "All-day row expanded: every lane shows." : "All-day row folded to three lanes.";
            }
        };
        Calendar.RangeChanged += (_, e) => Log.Text = $"Showing {Calendar.RangeTitle} ({e.Reason}).";
        Calendar.EventInvoked += (_, e) => Log.Text = $"Opened {e.Event.Title}.";
    }
}
