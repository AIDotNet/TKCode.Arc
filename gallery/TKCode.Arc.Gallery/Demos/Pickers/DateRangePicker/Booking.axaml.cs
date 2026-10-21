using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class DateRangePickerBookingDemo : UserControl
{
    public DateRangePickerBookingDemo()
    {
        InitializeComponent();
        var today = DateOnly.FromDateTime(DateTime.Today);
        Picker.MinDate = today;
        Picker.MaxDate = today.AddMonths(6);
        Picker.Presets =
        [
            new DateRangePreset("This weekend", WeekendFrom),
            new DateRangePreset("Next week", t => new DateRange(t.AddDays(7 - (int)t.DayOfWeek + 1), t.AddDays(7 - (int)t.DayOfWeek + 7))),
            new DateRangePreset("Two weeks", t => new DateRange(t, t.AddDays(13)))
        ];
    }

    private static DateRange WeekendFrom(DateOnly today)
    {
        var saturday = today.AddDays(((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7);
        return new DateRange(saturday, saturday.AddDays(1));
    }
}
