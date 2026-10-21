using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CalendarLimitsAndDisabledDaysDemo : UserControl
{
    public CalendarLimitsAndDisabledDaysDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Limited.MinDate = today.AddDays(-3);
        Limited.MaxDate = today.AddDays(45);
        Limited.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}
