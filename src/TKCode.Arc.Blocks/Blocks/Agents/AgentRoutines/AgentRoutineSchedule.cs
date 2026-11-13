using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Five-field cron schedules (minute, hour, day of month, month, day of week) as <see cref="AgentRoutines"/> shows them:
/// the next run time and a plain English description. Fields take <c>*</c>, numbers, ranges (<c>1-5</c>), lists
/// (<c>1,3,5</c>) and steps (<c>*/15</c>, <c>8-18/2</c>); day of week counts Sunday as 0 (7 is Sunday too). When both the
/// day of month and the day of week are set, a day matching either runs, as in classic cron.
/// </summary>
public static class AgentRoutineSchedule
{
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
    private static readonly string[] ShortDayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    /// <summary>Whether <paramref name="cron"/> is a valid five-field schedule.</summary>
    public static bool IsValid(string? cron) => Parse(cron) is not null;

    /// <summary>
    /// The first time after <paramref name="after"/> (to the minute, in its offset) that <paramref name="cron"/> runs;
    /// null for an invalid schedule or one that never runs within about four years (such as February 30).
    /// </summary>
    public static DateTimeOffset? Next(string? cron, DateTimeOffset after)
    {
        if (Parse(cron) is not { } fields)
        {
            return null;
        }
        var start = new DateTimeOffset(after.Year, after.Month, after.Day, after.Hour, after.Minute, 0, after.Offset).AddMinutes(1);
        var day = start.Date;
        for (var d = 0; d < 366 * 4 + 1; d++, day = day.AddDays(1))
        {
            if (!fields.Months[day.Month] || !DayMatches(fields, day))
            {
                continue;
            }
            for (var hour = 0; hour < 24; hour++)
            {
                if (!fields.Hours[hour])
                {
                    continue;
                }
                for (var minute = 0; minute < 60; minute++)
                {
                    if (!fields.Minutes[minute])
                    {
                        continue;
                    }
                    var time = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, after.Offset);
                    if (time >= start)
                    {
                        return time;
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// <paramref name="cron"/> in plain English: "Every day at 09:00", "Weekdays at 08:30", "Every Monday at 07:00",
    /// "Mon, Wed and Fri at 18:00", "Monthly on day 1 at 06:00", "Every hour at :15", "Every 2 hours at :00",
    /// "Every 15 minutes". A schedule with no short form returns the expression itself.
    /// </summary>
    public static string Describe(string? cron)
    {
        var parts = Split(cron);
        if (parts is null || Parse(cron) is null)
        {
            return cron ?? string.Empty;
        }
        var (minute, hour, dom, month, dow) = (parts[0], parts[1], parts[2], parts[3], parts[4]);
        if (month != "*")
        {
            return cron!.Trim();
        }
        if (IsNumber(minute) && IsNumber(hour))
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"{int.Parse(hour, CultureInfo.InvariantCulture):00}:{int.Parse(minute, CultureInfo.InvariantCulture):00}");
            if (dom == "*" && dow == "*")
            {
                return $"Every day at {at}";
            }
            if (dom == "*" && Days(dow) is { } days)
            {
                if (days.SequenceEqual([1, 2, 3, 4, 5]))
                {
                    return $"Weekdays at {at}";
                }
                if (days.SequenceEqual([0, 6]))
                {
                    return $"Weekends at {at}";
                }
                if (days.Count == 1)
                {
                    return $"Every {DayNames[days[0]]} at {at}";
                }
                var names = days.Select(d => ShortDayNames[d]).ToArray();
                return $"{string.Join(", ", names[..^1])} and {names[^1]} at {at}";
            }
            if (dow == "*" && IsNumber(dom))
            {
                return $"Monthly on day {dom} at {at}";
            }
            return cron!.Trim();
        }
        if (dom != "*" || dow != "*")
        {
            return cron!.Trim();
        }
        if (IsNumber(minute) && hour == "*")
        {
            return string.Create(CultureInfo.InvariantCulture, $"Every hour at :{int.Parse(minute, CultureInfo.InvariantCulture):00}");
        }
        if (IsNumber(minute) && hour.StartsWith("*/", StringComparison.Ordinal) && IsNumber(hour[2..]))
        {
            return string.Create(CultureInfo.InvariantCulture, $"Every {hour[2..]} hours at :{int.Parse(minute, CultureInfo.InvariantCulture):00}");
        }
        if (minute.StartsWith("*/", StringComparison.Ordinal) && IsNumber(minute[2..]) && hour == "*")
        {
            return $"Every {minute[2..]} minutes";
        }
        if (minute == "*" && hour == "*")
        {
            return "Every minute";
        }
        return cron!.Trim();
    }

    #region Parsing

    private sealed record Fields(bool[] Minutes, bool[] Hours, bool[] DaysOfMonth, bool[] Months, bool[] DaysOfWeek, bool DomAny, bool DowAny);

    private static bool DayMatches(Fields fields, DateTime day)
    {
        var dom = fields.DaysOfMonth[day.Day];
        var dow = fields.DaysOfWeek[(int)day.DayOfWeek];
        if (fields.DomAny && fields.DowAny)
        {
            return true;
        }
        if (fields.DomAny)
        {
            return dow;
        }
        return fields.DowAny ? dom : dom || dow;
    }

    private static string[]? Split(string? cron)
    {
        if (string.IsNullOrWhiteSpace(cron))
        {
            return null;
        }
        var parts = cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 5 ? parts : null;
    }

    private static Fields? Parse(string? cron)
    {
        if (Split(cron) is not { } parts)
        {
            return null;
        }
        var minutes = Field(parts[0], 0, 59);
        var hours = Field(parts[1], 0, 23);
        var dom = Field(parts[2], 1, 31);
        var months = Field(parts[3], 1, 12);
        var dow = Field(parts[4], 0, 7);
        if (minutes is null || hours is null || dom is null || months is null || dow is null)
        {
            return null;
        }
        if (dow[7])
        {
            dow[0] = true;
        }
        return new Fields(minutes, hours, dom, months, dow, parts[2] == "*", parts[4] == "*");
    }

    /// <summary>The values a field allows, as flags indexed by value (null when the field is invalid).</summary>
    private static bool[]? Field(string text, int min, int max)
    {
        var allowed = new bool[max + 1];
        foreach (var item in text.Split(','))
        {
            var step = 1;
            var range = item;
            var slash = item.IndexOf('/');
            if (slash >= 0)
            {
                if (!int.TryParse(item.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    return null;
                }
                range = item[..slash];
            }
            int from, to;
            if (range == "*")
            {
                (from, to) = (min, max);
            }
            else if (range.IndexOf('-') is var dash and > 0)
            {
                if (!int.TryParse(range.AsSpan(0, dash), NumberStyles.None, CultureInfo.InvariantCulture, out from) ||
                    !int.TryParse(range.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out to))
                {
                    return null;
                }
            }
            else
            {
                if (!int.TryParse(range, NumberStyles.None, CultureInfo.InvariantCulture, out from))
                {
                    return null;
                }
                to = slash >= 0 ? max : from;
            }
            if (from < min || to > max || from > to)
            {
                return null;
            }
            for (var value = from; value <= to; value += step)
            {
                allowed[value] = true;
            }
        }
        return allowed;
    }

    private static bool IsNumber(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);

    /// <summary>The days of a day-of-week field made only of numbers, lists and ranges, sorted (Sunday 0); null otherwise.</summary>
    private static List<int>? Days(string text)
    {
        if (text.Contains('/') || text.Contains('*'))
        {
            return null;
        }
        var flags = Field(text, 0, 7);
        if (flags is null)
        {
            return null;
        }
        if (flags[7])
        {
            flags[0] = true;
        }
        var days = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            if (flags[i])
            {
                days.Add(i);
            }
        }
        return days;
    }

    #endregion
}
