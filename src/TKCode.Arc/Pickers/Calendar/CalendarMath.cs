namespace TKCode.Arc;

/// <summary>Date helpers shared by Calendar, DatePicker and DateRangePicker (six fixed weeks per month).</summary>
internal static class CalendarMath
{
    public static DateOnly MonthStart(DateOnly date) => new(date.Year, date.Month, 1);

    public static int MonthIndex(DateOnly date) => date.Year * 12 + date.Month - 1;

    public static DateOnly FromIndex(int index)
    {
        var year = (int)Math.Floor(index / 12.0);
        var month = index - year * 12;
        return new DateOnly(year, month + 1, 1);
    }

    public static DateOnly AddMonths(DateOnly month, int amount) => MonthStart(month).AddMonths(amount);

    /// <summary>Moves by whole months keeping the day, clamped to the shorter month (Jan 31 + 1 month = Feb 28).</summary>
    public static DateOnly ShiftMonths(DateOnly date, int amount)
    {
        var target = MonthStart(date).AddMonths(amount);
        var day = Math.Min(date.Day, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateOnly(target.Year, target.Month, day);
    }

    /// <summary>First date shown on the 6×7 grid of <paramref name="month"/>.</summary>
    public static DateOnly GridStart(DateOnly month, DayOfWeek firstDay)
    {
        var first = MonthStart(month);
        var offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        return first.AddDays(-offset);
    }

    public static int Column(DateOnly date, DayOfWeek firstDay) => ((int)date.DayOfWeek - (int)firstDay + 7) % 7;

    public static bool SameMonth(DateOnly a, DateOnly b) => a.Year == b.Year && a.Month == b.Month;
}
