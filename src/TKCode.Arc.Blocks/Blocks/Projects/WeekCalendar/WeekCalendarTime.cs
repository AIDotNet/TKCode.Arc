using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Time zones and the culture-aware texts of the week calendar: wall-clock times in the calendar's zone, our own short
/// offset label ("GMT+8", "GMT+5:30", "GMT"), range titles ("Sep 21 to 27, 2026"), merged time ranges ("9:30 – 11:30
/// AM") and the names read by screen readers. English (en-US) follows Arc's measured forms; Chinese, Japanese and Korean
/// use year-month-day forms; other cultures use their own month names with en dashes. Pure; no UI.
/// </summary>
internal static class WeekCalendarTime
{
    private const string EnDash = "–";

    // Time zones ---------------------------------------------------------------------------------------------------------

    /// <summary>The wall-clock time of <paramref name="instant"/> in <paramref name="zone"/>.</summary>
    public static DateTime ToWall(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(instant, zone).DateTime, DateTimeKind.Unspecified);

    /// <summary>
    /// The instant of the wall-clock time <paramref name="wall"/> in <paramref name="zone"/> (a time skipped by a clock
    /// change moves forward by the gap), written with <paramref name="offset"/> when given (so stored UTC values stay UTC).
    /// </summary>
    public static DateTimeOffset FromWall(DateTime wall, TimeZoneInfo zone, TimeSpan? offset = null)
    {
        var local = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }
        var instant = new DateTimeOffset(local, zone.GetUtcOffset(local));
        return offset is { } o ? instant.ToOffset(o) : instant;
    }

    /// <summary>The offset of <paramref name="zone"/> at noon of <paramref name="day"/>.</summary>
    public static TimeSpan OffsetOn(TimeZoneInfo zone, DateOnly day) =>
        zone.GetUtcOffset(day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified));

    /// <summary>"GMT+8", "GMT+5:30", "GMT−3:30" (with a hyphen-minus), "GMT" for zero.</summary>
    public static string OffsetLabel(TimeSpan offset)
    {
        var minutes = (int)Math.Round(offset.TotalMinutes);
        if (minutes == 0)
        {
            return "GMT";
        }
        var sign = minutes < 0 ? "-" : "+";
        minutes = Math.Abs(minutes);
        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0
            ? string.Create(CultureInfo.InvariantCulture, $"GMT{sign}{hours}")
            : string.Create(CultureInfo.InvariantCulture, $"GMT{sign}{hours}:{rest:00}");
    }

    /// <summary>The minute of the day of a wall-clock time (0–1440).</summary>
    public static double MinuteOfDay(DateTime wall) => wall.TimeOfDay.TotalMinutes;

    // Culture classes ----------------------------------------------------------------------------------------------------

    /// <summary>True for cultures that write dates as year-month-day with characters (zh, ja, ko).</summary>
    public static bool IsCjk(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName is "zh" or "ja" or "ko";

    /// <summary>True when the culture writes times with AM / PM.</summary>
    public static bool Is12Hour(CultureInfo culture) => culture.DateTimeFormat.ShortTimePattern.Contains('t');

    private static string Year(CultureInfo culture) => IsCjk(culture) ? culture.TwoLetterISOLanguageName == "ko" ? "년" : "年" : string.Empty;

    private static string MonthMark(CultureInfo culture) => culture.TwoLetterISOLanguageName == "ko" ? "월" : "月";

    private static string DayMark(CultureInfo culture) => culture.TwoLetterISOLanguageName == "ko" ? "일" : "日";

    // Dates ----------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The title of a range of days. One day: "Thursday, November 5" (no year, as Arc). Several: "Sep 21 to 27, 2026",
    /// "Sep 28 to Oct 4, 2026", "Dec 28, 2026 to Jan 3, 2027"; Chinese "2026年9月21日至27日", "2026年9月28日至10月4日",
    /// "2026年12月28日至2027年1月3日".
    /// </summary>
    public static string RangeTitle(DateOnly start, DateOnly end, CultureInfo culture)
    {
        if (start == end)
        {
            return DayTitle(start, culture);
        }
        var f = culture.DateTimeFormat;
        if (IsCjk(culture))
        {
            var connector = culture.TwoLetterISOLanguageName == "zh" ? "至" : "～";
            var y = Year(culture);
            var m = MonthMark(culture);
            var d = DayMark(culture);
            var head = $"{start.Year}{y}{start.Month}{m}{start.Day}{d}";
            if (start.Year != end.Year)
            {
                return $"{head}{connector}{end.Year}{y}{end.Month}{m}{end.Day}{d}";
            }
            return start.Month != end.Month
                ? $"{head}{connector}{end.Month}{m}{end.Day}{d}"
                : $"{head}{connector}{end.Day}{d}";
        }
        var to = culture.TwoLetterISOLanguageName == "en" ? " to " : $" {EnDash} ";
        string Short(DateOnly date) => f.GetAbbreviatedMonthName(date.Month) + " " + date.Day.ToString(culture);
        if (start.Year != end.Year)
        {
            return $"{Short(start)}, {start.Year.ToString(culture)}{to}{Short(end)}, {end.Year.ToString(culture)}";
        }
        if (start.Month != end.Month)
        {
            return $"{Short(start)}{to}{Short(end)}, {end.Year.ToString(culture)}";
        }
        return $"{Short(start)}{to}{end.Day.ToString(culture)}, {end.Year.ToString(culture)}";
    }

    /// <summary>"Thursday, November 5"; Chinese "11月5日 星期四".</summary>
    public static string DayTitle(DateOnly day, CultureInfo culture)
    {
        var f = culture.DateTimeFormat;
        if (IsCjk(culture))
        {
            return $"{day.Month}{MonthMark(culture)}{day.Day}{DayMark(culture)} {f.GetDayName(day.DayOfWeek)}";
        }
        return f.GetDayName(day.DayOfWeek) + ", " + f.GetMonthName(day.Month) + " " + day.Day.ToString(culture);
    }

    /// <summary>The name of a day read by screen readers: "Monday, September 21" (the same as <see cref="DayTitle"/>).</summary>
    public static string DayName(DateOnly day, CultureInfo culture) => DayTitle(day, culture);

    /// <summary>"Sat, Sep 26"; Chinese "9月26日 周六".</summary>
    public static string ShortDayName(DateOnly day, CultureInfo culture)
    {
        var weekday = culture.DateTimeFormat.GetAbbreviatedDayName(day.DayOfWeek);
        return IsCjk(culture) ? $"{ShortDate(day, culture)} {weekday}" : $"{weekday}, {ShortDate(day, culture)}";
    }

    /// <summary>"Sep 21"; Chinese "9月21日".</summary>
    public static string ShortDate(DateOnly day, CultureInfo culture)
    {
        if (IsCjk(culture))
        {
            return $"{day.Month}{MonthMark(culture)}{day.Day}{DayMark(culture)}";
        }
        return culture.DateTimeFormat.GetAbbreviatedMonthName(day.Month) + " " + day.Day.ToString(culture);
    }

    /// <summary>"September 2026"; Chinese "2026年9月".</summary>
    public static string MonthTitle(int year, int month, CultureInfo culture)
    {
        if (IsCjk(culture))
        {
            return $"{year}{Year(culture)}{month}{MonthMark(culture)}";
        }
        return culture.DateTimeFormat.GetMonthName(month) + " " + year.ToString(culture);
    }

    /// <summary>The header's weekday: "Mon"; Chinese "周一".</summary>
    public static string ShortWeekday(DayOfWeek day, CultureInfo culture) => culture.DateTimeFormat.GetAbbreviatedDayName(day);

    /// <summary>The mini month's one-letter weekday: "M"; Chinese "一".</summary>
    public static string WeekdayLetter(DayOfWeek day, CultureInfo culture)
    {
        var shortest = culture.DateTimeFormat.GetShortestDayName(day);
        if (shortest.Length <= 1)
        {
            return shortest;
        }
        // Chinese "周一" reads as "一" in a month grid; alphabetic names keep their first letter.
        return IsCjk(culture) ? shortest[^1..] : shortest[..1].ToUpper(culture);
    }

    /// <summary>The full weekday: "Monday".</summary>
    public static string Weekday(DayOfWeek day, CultureInfo culture) => culture.DateTimeFormat.GetDayName(day);

    // Times ----------------------------------------------------------------------------------------------------------------

    private static string Meridiem(int hour, CultureInfo culture)
    {
        var f = culture.DateTimeFormat;
        var text = hour < 12 ? f.AMDesignator : f.PMDesignator;
        return string.IsNullOrEmpty(text) ? (hour < 12 ? "AM" : "PM") : text;
    }

    private static string Clock12(int minuteOfDay, bool keepZeroMinutes, CultureInfo culture)
    {
        var hour = minuteOfDay / 60 % 24;
        var minute = minuteOfDay % 60;
        var h12 = hour % 12 == 0 ? 12 : hour % 12;
        return minute == 0 && !keepZeroMinutes
            ? h12.ToString(culture)
            : h12.ToString(culture) + culture.DateTimeFormat.TimeSeparator + minute.ToString("00", culture);
    }

    private static string Clock24(int minuteOfDay, CultureInfo culture)
    {
        var hour = minuteOfDay / 60 % 24;
        var minute = minuteOfDay % 60;
        return hour.ToString(culture) + culture.DateTimeFormat.TimeSeparator + minute.ToString("00", culture);
    }

    private static int Minute(double minuteOfDay) => (int)Math.Round(Math.Clamp(minuteOfDay, 0, 24 * 60));

    /// <summary>A short time: "9 AM", "1:30 PM"; 24-hour cultures "9:00", "13:30".</summary>
    public static string ShortTime(double minuteOfDay, CultureInfo culture)
    {
        var m = Minute(minuteOfDay);
        return Is12Hour(culture) ? Clock12(m, false, culture) + " " + Meridiem(m / 60 % 24, culture) : Clock24(m, culture);
    }

    /// <summary>A full time: "9:00 AM", "10:02 PM"; 24-hour cultures "9:00", "22:02".</summary>
    public static string FullTime(double minuteOfDay, CultureInfo culture)
    {
        var m = Minute(minuteOfDay);
        return Is12Hour(culture) ? Clock12(m, true, culture) + " " + Meridiem(m / 60 % 24, culture) : Clock24(m, culture);
    }

    /// <summary>An hour line's label: "1 AM", "12 PM"; 24-hour cultures "13:00".</summary>
    public static string HourLabel(int hour, CultureInfo culture) => ShortTime(hour * 60, culture);

    /// <summary>
    /// A time range on an event, with the AM / PM said once when both ends share it: "9:30 – 11:30 AM", "12 – 1 PM",
    /// "10 AM – 12 PM", "11 AM – 12:30 PM"; 24-hour cultures "9:30 – 11:30". An end at midnight reads as 12 AM.
    /// </summary>
    public static string TimeRange(double startMinute, double endMinute, CultureInfo culture)
    {
        var s = Minute(startMinute);
        var e = Minute(endMinute);
        if (!Is12Hour(culture))
        {
            return Clock24(s, culture) + " " + EnDash + " " + Clock24(e, culture);
        }
        var sm = Meridiem(s / 60 % 24, culture);
        var em = Meridiem(e / 60 % 24, culture);
        if (sm == em && e > s)
        {
            return Clock12(s, false, culture) + " " + EnDash + " " + Clock12(e, false, culture) + " " + em;
        }
        return Clock12(s, false, culture) + " " + sm + " " + EnDash + " " + Clock12(e, false, culture) + " " + em;
    }
}
