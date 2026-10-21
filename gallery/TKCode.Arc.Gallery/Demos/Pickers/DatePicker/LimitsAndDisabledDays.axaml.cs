using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DatePickerLimitsAndDisabledDaysDemo : UserControl
{
    public DatePickerLimitsAndDisabledDaysDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Due.MinDate = today;
        Due.MaxDate = today.AddDays(56);
        Due.DisabledDates = d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}
