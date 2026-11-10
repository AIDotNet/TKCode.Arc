using System.Globalization;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>
/// The built-in planner of <see cref="SmartScheduler"/>: reads plain-English limits ("afternoons, not Friday, keep lunch
/// free, 45 min") into <see cref="SmartSchedulerConstraints"/>, and finds the best meeting slots in a week where every
/// invited person is free. Pure and deterministic; the scheduler calls it unless the app sets its own finder.
/// </summary>
public static partial class SmartSchedulerPlanner
{
    private static readonly string[] s_dayNames = ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"];

    /// <summary>
    /// Reads <paramref name="text"/>: morning(s) and afternoon(s), "not" / "no" / "except" a weekday, "after 10" and
    /// "before 4pm", lunch, "asap" / "soon", "breaks" / "buffer", and a length such as "30 min" or "1 hour". Words it
    /// does not know are ignored.
    /// </summary>
    /// <param name="text">What the person typed.</param>
    /// <param name="weekStart">The first day of the shown week, to map weekday names to day indexes.</param>
    /// <param name="days">How many days the week shows.</param>
    /// <param name="reasons">The words for the chips; null uses English.</param>
    public static SmartSchedulerConstraints Parse(string? text, DateOnly weekStart, int days, SmartSchedulerReasons? reasons = null)
    {
        reasons ??= new SmartSchedulerReasons();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SmartSchedulerConstraints();
        }
        var t = text.ToLowerInvariant();
        var understood = new List<string>();
        double? notBefore = null;
        double? notAfter = null;
        if (MorningRegex().IsMatch(t))
        {
            notAfter = 12;
            understood.Add(reasons.Mornings);
        }
        if (AfternoonRegex().IsMatch(t))
        {
            notBefore = 13;
            understood.Add(reasons.Afternoons);
        }
        var excluded = new List<int>();
        foreach (Match match in NotDayRegex().Matches(t))
        {
            var name = match.Groups["day"].Value;
            var dayOfWeek = Array.FindIndex(s_dayNames, d => d.StartsWith(name, StringComparison.Ordinal));
            if (dayOfWeek < 0)
            {
                continue;
            }
            for (var i = 0; i < days; i++)
            {
                // DayOfWeek: Sunday = 0; our names start at Monday.
                if (((int)weekStart.AddDays(i).DayOfWeek + 6) % 7 == dayOfWeek && !excluded.Contains(i))
                {
                    excluded.Add(i);
                    understood.Add(reasons.NotDay(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s_dayNames[dayOfWeek])));
                }
            }
        }
        foreach (Match match in BoundRegex().Matches(t))
        {
            var hour = Hour(match);
            if (hour is null)
            {
                continue;
            }
            if (match.Groups["word"].Value == "after")
            {
                notBefore = Math.Max(notBefore ?? 0, hour.Value);
                understood.Add(reasons.After(Clock(hour.Value)));
            }
            else
            {
                notAfter = Math.Min(notAfter ?? 24, hour.Value);
                understood.Add(reasons.Before(Clock(hour.Value)));
            }
        }
        var lunch = t.Contains("lunch", StringComparison.Ordinal);
        if (lunch)
        {
            understood.Add(reasons.LunchChip);
        }
        var soon = SoonRegex().IsMatch(t);
        if (soon)
        {
            understood.Add(reasons.Soon);
        }
        var buffer = BufferRegex().IsMatch(t);
        if (buffer)
        {
            understood.Add(reasons.BufferChip);
        }
        int? duration = null;
        if (LengthRegex().Match(t) is { Success: true } length)
        {
            var amount = double.Parse(length.Groups["n"].Value, CultureInfo.InvariantCulture);
            var minutes = length.Groups["unit"].Value.StartsWith('h') ? amount * 60 : amount;
            if (minutes is >= 10 and <= 240)
            {
                duration = (int)Math.Round(minutes / 15) * 15;
                understood.Add(reasons.Length(duration.Value));
            }
        }
        return new SmartSchedulerConstraints
        {
            ExcludedDays = excluded,
            NotBefore = notBefore,
            NotAfter = notAfter,
            AvoidLunch = lunch,
            PreferSoon = soon,
            PreferBuffer = buffer,
            DurationMinutes = duration,
            Understood = understood,
        };
    }

    private static double? Hour(Match match)
    {
        if (!int.TryParse(match.Groups["h"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || hour is < 1 or > 23)
        {
            return null;
        }
        var minutes = match.Groups["m"].Success ? int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        var suffix = match.Groups["ap"].Value;
        if (suffix.StartsWith('p') && hour < 12)
        {
            hour += 12;
        }
        else if (suffix.Length == 0 && hour < 7)
        {
            // "after 2" in a working day means the afternoon.
            hour += 12;
        }
        return hour + minutes / 60.0;
    }

    private static string Clock(double hours)
    {
        var time = new DateTime(2000, 1, 1).AddHours(hours);
        return time.ToString(time.Minute == 0 ? "h tt" : "h:mm tt", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Finds up to <paramref name="count"/> slots on a half-hour grid where every invited person is free and every hard
    /// limit holds, ranked by how well they fit (breaks around them, the quiet of the day, an early time when asked);
    /// at most one slot per day so the options differ.
    /// </summary>
    /// <param name="request">Who, how long, what was asked and the week's shape.</param>
    /// <param name="count">How many slots to return.</param>
    /// <param name="reasons">The words of the reasons; null uses English.</param>
    public static IReadOnlyList<SmartSchedulerSlot> Find(SmartSchedulerRequest request, int count = 3, SmartSchedulerReasons? reasons = null)
    {
        reasons ??= new SmartSchedulerReasons();
        var c = request.Constraints;
        var length = request.DurationMinutes / 60.0;
        var candidates = new List<SmartSchedulerSlot>();
        if (length <= 0 || request.People.Count == 0)
        {
            return candidates;
        }
        for (var day = 0; day < request.Days; day++)
        {
            if (c.ExcludedDays.Contains(day))
            {
                continue;
            }
            for (var start = request.DayStart; start + length <= request.DayEnd + 1e-9; start += 0.5)
            {
                var end = start + length;
                if ((c.NotBefore is { } nb && start < nb - 1e-9) || (c.NotAfter is { } na && end > na + 1e-9))
                {
                    continue;
                }
                if (c.AvoidLunch && start < 13 && end > 12)
                {
                    continue;
                }
                if (!AllFree(request.People, day, start, end))
                {
                    continue;
                }
                var why = new List<string> { reasons.EveryoneFree };
                double score = 100;
                if (c.NotBefore is not null && c.NotAfter is null && c.NotBefore >= 12)
                {
                    why.Add(reasons.Afternoons);
                }
                else if (c.NotAfter is not null && c.NotAfter <= 12 && c.NotBefore is null)
                {
                    why.Add(reasons.Mornings);
                }
                if (AllFree(request.People, day, start - 0.5, start) && AllFree(request.People, day, end, end + 0.5))
                {
                    score += c.PreferBuffer ? 20 : 8;
                    why.Add(reasons.Buffer);
                }
                // Fewer meetings that day for the group is calmer.
                var load = request.People.Sum(p => p.Busy.Where(b => b.Day == day).Sum(b => b.End - b.Start));
                score -= load * 1.5;
                if (c.AvoidLunch)
                {
                    why.Add(reasons.Lunch);
                }
                if (c.PreferSoon)
                {
                    score += (request.Days - day) * 12 - (start - request.DayStart) * 2;
                }
                else
                {
                    // Without a preference, late morning and mid-afternoon read as the friendliest times.
                    score -= Math.Min(Math.Abs(start - 10.5), Math.Abs(start - 14.5)) * 2;
                }
                candidates.Add(new SmartSchedulerSlot { Day = day, Start = start, End = end, Reasons = why, Score = score });
            }
        }
        var ranked = candidates.OrderByDescending(s => s.Score).ThenBy(s => s.Day).ThenBy(s => s.Start).ToList();
        var picked = new List<SmartSchedulerSlot>();
        foreach (var slot in ranked)
        {
            if (picked.Count >= count)
            {
                break;
            }
            if (picked.All(p => p.Day != slot.Day))
            {
                picked.Add(slot);
            }
        }
        // Fewer days than slots wanted: fill with the next best on any day.
        foreach (var slot in ranked)
        {
            if (picked.Count >= count)
            {
                break;
            }
            if (!picked.Contains(slot) && picked.All(p => p.Day != slot.Day || p.End <= slot.Start || p.Start >= slot.End))
            {
                picked.Add(slot);
            }
        }
        if (c.PreferSoon && picked.Count > 0)
        {
            var first = picked.MinBy(s => s.Day * 100 + s.Start)!;
            var index = picked.IndexOf(first);
            picked[index] = first with { Reasons = [.. first.Reasons, reasons.Soonest] };
        }
        else if (picked.Count > 0)
        {
            picked[0] = picked[0] with { Reasons = [.. picked[0].Reasons, reasons.Quiet] };
        }
        return picked;
    }

    /// <summary>Whether everyone in <paramref name="people"/> is free on <paramref name="day"/> from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static bool AllFree(IReadOnlyList<SmartSchedulerPerson> people, int day, double start, double end)
    {
        foreach (var person in people)
        {
            foreach (var busy in person.Busy)
            {
                if (busy.Day == day && busy.Start < end - 1e-9 && busy.End > start + 1e-9)
                {
                    return false;
                }
            }
        }
        return true;
    }

    [GeneratedRegex(@"\bmornings?\b")]
    private static partial Regex MorningRegex();

    [GeneratedRegex(@"\bafternoons?\b")]
    private static partial Regex AfternoonRegex();

    [GeneratedRegex(@"\b(?:not|no|except|avoid|skip)\s+(?:on\s+)?(?<day>mon|tue|wed|thu|fri|sat|sun)[a-z]*")]
    private static partial Regex NotDayRegex();

    [GeneratedRegex(@"\b(?<word>after|before)\s+(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ap>am|pm|a\.m\.|p\.m\.)?")]
    private static partial Regex BoundRegex();

    [GeneratedRegex(@"\b(?:asap|soon|soonest|as soon as possible|earliest)\b")]
    private static partial Regex SoonRegex();

    [GeneratedRegex(@"\b(?:breaks?|buffer|gap|back[- ]to[- ]back)\b")]
    private static partial Regex BufferRegex();

    [GeneratedRegex(@"\b(?<n>\d+(?:\.\d+)?)\s*(?<unit>min|mins|minutes|h|hr|hrs|hour|hours)\b")]
    private static partial Regex LengthRegex();
}
