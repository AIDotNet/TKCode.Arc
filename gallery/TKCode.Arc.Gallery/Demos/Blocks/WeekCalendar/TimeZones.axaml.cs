using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WeekCalendarTimeZonesDemo : UserControl
{
    public WeekCalendarTimeZonesDemo()
    {
        InitializeComponent();
        var newYork = WeekCalendarDemoData.Zone("America/New_York", TimeSpan.FromHours(-4), "New York");
        var kolkata = WeekCalendarDemoData.Zone("Asia/Kolkata", new TimeSpan(5, 30, 0), "Kolkata");
        // One list of events, planned in New York; each calendar shows them on its own clock.
        var events = WeekCalendarDemoData.CreateEvents(WeekCalendarDemoData.ThisMonday(newYork), newYork);
        NewYork.TimeZone = newYork;
        Kolkata.TimeZone = kolkata;
        NewYork.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        Kolkata.Calendars.AddRange(WeekCalendarDemoData.CreateCalendars());
        NewYork.EventsSource = events;
        Kolkata.EventsSource = events;
        // Both open at 8 AM New York time: 5:30 PM in Kolkata.
        NewYork.InitialScrollTime = new TimeOnly(8, 0);
        Kolkata.InitialScrollTime = new TimeOnly(17, 30);
    }
}
