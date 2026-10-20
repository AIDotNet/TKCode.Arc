using System.Globalization;

namespace TKCode.Arc;

/// <summary>A relative time label: the short form shown ("Now", "9m", "2h") and the long form read aloud.</summary>
internal readonly record struct ActivityRelativeTime(string Short, string Long);

/// <summary>
/// Time and size formatting shared by the Activity controls (S11): day keys and clock times in a time zone, Arc's
/// relative labels, day headings, and <c>formatBytes</c>. Arc formats with <c>Intl.DateTimeFormat</c>; these follow
/// the same shapes from the culture's own patterns, so en-US matches Arc exactly.
/// </summary>
internal static class ActivityTime
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Arc's default locale (en-US).</summary>
    public static CultureInfo DefaultCulture => s_enUs;

    /// <summary><paramref name="at"/> expressed in <paramref name="zone"/>.</summary>
    public static DateTimeOffset InZone(DateTimeOffset at, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(at, zone);

    /// <summary>The calendar day of <paramref name="at"/> in <paramref name="zone"/> (Arc's <c>dayKey</c>).</summary>
    public static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone) => DateOnly.FromDateTime(InZone(at, zone).DateTime);

    /// <summary>
    /// Arc Timeline's relative label: under a minute "Now" / "just now", under an hour "9m" / "9 minutes ago", else
    /// "2h" / "2 hours ago". Times in the future count as now.
    /// </summary>
    public static ActivityRelativeTime Relative(DateTimeOffset at, DateTimeOffset now)
    {
        var minutes = (long)Math.Max(0, Math.Floor((now - at).TotalMinutes));
        if (minutes < 1)
        {
            return new ActivityRelativeTime("Now", "just now");
        }
        if (minutes < 60)
        {
            return new ActivityRelativeTime($"{minutes}m", $"{minutes} {(minutes == 1 ? "minute" : "minutes")} ago");
        }
        var hours = minutes / 60;
        return new ActivityRelativeTime($"{hours}h", $"{hours} {(hours == 1 ? "hour" : "hours")} ago");
    }

    /// <summary>Clock time (Intl <c>hour: numeric, minute: 2-digit</c>): "9:41 AM" in en-US, "09:41" in de-DE.</summary>
    public static string Clock(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture)
    {
        culture ??= s_enUs;
        return InZone(at, zone).ToString(culture.DateTimeFormat.ShortTimePattern, culture);
    }

    /// <summary>Weekday, month and day (Intl <c>weekday: long, month: long, day: numeric</c>): "Monday, October 20".</summary>
    public static string WeekdayMonthDay(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture)
    {
        culture ??= s_enUs;
        var local = InZone(at, zone);
        return $"{local.ToString("dddd", culture)}, {local.ToString(culture.DateTimeFormat.MonthDayPattern, culture)}";
    }

    /// <summary>The full date and time read aloud and shown on hover: "Monday, October 20, 9:41 AM".</summary>
    public static string Full(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture) =>
        $"{WeekdayMonthDay(at, zone, culture)}, {Clock(at, zone, culture)}";

    /// <summary>The weekday name alone (Intl <c>weekday: long</c>): "Monday".</summary>
    public static string Weekday(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture)
    {
        culture ??= s_enUs;
        return InZone(at, zone).ToString("dddd", culture);
    }

    /// <summary>
    /// A short date (Intl <c>weekday: short, month: short, day: numeric</c>, plus the year when asked): "Mon, Oct 20"
    /// or "Mon, Oct 20, 2025".
    /// </summary>
    public static string ShortDate(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture, bool withYear)
    {
        culture ??= s_enUs;
        var local = InZone(at, zone);
        var monthDay = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        var text = $"{local.ToString("ddd", culture)}, {local.ToString(monthDay, culture)}";
        return withYear ? $"{text}, {local.ToString("yyyy", culture)}" : text;
    }

    /// <summary>A medium date with a short time (Intl <c>dateStyle: medium, timeStyle: short</c>): "Oct 20, 2026, 9:41 AM".</summary>
    public static string MediumDateTime(DateTimeOffset at, TimeZoneInfo zone, CultureInfo? culture)
    {
        culture ??= s_enUs;
        var local = InZone(at, zone);
        var monthDay = culture.DateTimeFormat.MonthDayPattern.Replace("MMMM", "MMM", StringComparison.Ordinal);
        return $"{local.ToString(monthDay, culture)}, {local.ToString("yyyy", culture)}, {Clock(at, zone, culture)}";
    }

    /// <summary>Whole calendar days from <paramref name="day"/> to <paramref name="today"/> (0 today, 1 yesterday).</summary>
    public static int DaysBefore(DateOnly day, DateOnly today) => today.DayNumber - day.DayNumber;

    /// <summary>Arc's <c>formatBytes</c>: "512 B", "18 KB", "2.4 MB".</summary>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{Math.Max(0, bytes)} B";
        }
        if (bytes < 1024 * 1024)
        {
            return $"{Math.Round(bytes / 1024.0, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)} KB";
        }
        return $"{(bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture)} MB";
    }

    /// <summary>Upper-cases the first letter (Arc capitalizes Intl's "today" / "yesterday").</summary>
    public static string Capitalize(string text, CultureInfo? culture)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }
        return char.ToUpper(text[0], culture ?? s_enUs) + text[1..];
    }
}
