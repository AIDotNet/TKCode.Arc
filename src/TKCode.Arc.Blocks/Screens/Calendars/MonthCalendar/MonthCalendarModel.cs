using System.Globalization;

namespace TKCode.Arc;

/// <summary>One piece of an event inside one week row of the month grid.</summary>
/// <param name="Event">The event.</param>
/// <param name="StartColumn">The first column it covers in the row (0-based, among the shown columns).</param>
/// <param name="EndColumn">The last column it covers (inclusive).</param>
/// <param name="Lane">Its lane (0 is the top one).</param>
/// <param name="IsBar">True for all-day and multi-day events (tinted bars); false for timed chips.</param>
/// <param name="ClipStart">The event starts before this row (the bar runs to the row's left edge, square there).</param>
/// <param name="ClipEnd">The event goes on after this row.</param>
internal readonly record struct MonthLaneItem(
    MonthCalendarEvent Event, int StartColumn, int EndColumn, int Lane, bool IsBar, bool ClipStart, bool ClipEnd);

/// <summary>An "N more" button in one column of a week row.</summary>
internal readonly record struct MonthMoreItem(int Column, int Lane, int Count, DateOnly Date);

/// <summary>The packed lanes of one week row.</summary>
internal sealed class MonthWeekLayout
{
    public List<MonthLaneItem> Items { get; } = new();

    public List<MonthMoreItem> More { get; } = new();
}

/// <summary>A row of the day panel's schedule: an event or a free gap.</summary>
internal readonly record struct MonthScheduleEntry(MonthCalendarEvent? Event, TimeOnly Start, TimeOnly End)
{
    public bool IsGap => Event is null;
}

/// <summary>The day panel's summary of one date.</summary>
internal sealed class MonthDaySummary
{
    public List<MonthCalendarEvent> AllDay { get; } = new();

    public List<MonthCalendarEvent> Timed { get; } = new();

    public List<MonthScheduleEntry> Schedule { get; } = new();

    public int EventCount => AllDay.Count + Timed.Count;

    public TimeSpan Booked { get; set; }

    public TimeSpan Free { get; set; }
}

/// <summary>
/// The month calendar's pure rules (Arc Month calendar, design reference: notes §2.2 and §2.3): the weeks of a month, which
/// events cover a date, lane packing with "N more", the day summary (booked time, free gaps inside the working hours) and the
/// text formats. No UI; unit tested.
/// </summary>
internal static class MonthCalendarModel
{
    /// <summary>The lane pitch: a 19 high chip and its 2 bottom margin.</summary>
    public const double LanePitch = 21;

    /// <summary>The space above the first lane: the cell's 4 top padding, the 24 day number and 1.</summary>
    public const double LaneTop = 29;

    /// <summary>The first day of the week that contains <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var offset = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.AddDays(-offset);
    }

    /// <summary>The first date of the grid and the number of week rows a month needs (4 to 6).</summary>
    public static (DateOnly First, int Weeks) GridOf(DateOnly month, DayOfWeek firstDay)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var first = WeekStart(start, firstDay);
        var last = start.AddMonths(1).AddDays(-1);
        var weeks = (last.DayNumber - first.DayNumber) / 7 + 1;
        return (first, weeks);
    }

    /// <summary>The days of a week shown as columns (weekends dropped when <paramref name="showWeekends"/> is false).</summary>
    public static List<DateOnly> Columns(DateOnly weekStart, bool showWeekends)
    {
        var days = new List<DateOnly>(7);
        for (var i = 0; i < 7; i++)
        {
            var day = weekStart.AddDays(i);
            if (!showWeekends && IsWeekend(day))
            {
                continue;
            }
            days.Add(day);
        }
        return days;
    }

    public static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>True when the event covers <paramref name="day"/>.</summary>
    public static bool Covers(MonthCalendarEvent e, DateOnly day) => day >= e.StartDate && day <= e.LastDate;

    /// <summary>All-day and multi-day events draw as bars.</summary>
    public static bool IsBar(MonthCalendarEvent e) => e.IsAllDay || e.IsMultiDay;

    /// <summary>How many lanes fit in a week row of <paramref name="rowHeight"/>.</summary>
    public static int LaneCount(double rowHeight) => Math.Max(1, (int)Math.Floor((rowHeight - LaneTop - 1 + 0.001) / LanePitch));

    /// <summary>
    /// Packs one week row: bars first (earlier start first, then the longer one), each in the first lane free across its days;
    /// then timed events per day in time order, each in the first free lane of its day. A day with more items than lanes keeps
    /// <c>lanes − 1</c> of them and puts "N more" in the last lane; a bar is cut where it would fall into that hidden part.
    /// </summary>
    public static MonthWeekLayout Pack(IReadOnlyList<DateOnly> columns, IEnumerable<MonthCalendarEvent> events, int lanes)
    {
        var layout = new MonthWeekLayout();
        if (columns.Count == 0)
        {
            return layout;
        }
        var first = columns[0];
        var last = columns[^1];
        var bars = new List<(MonthCalendarEvent E, int S, int End, bool CS, bool CE)>();
        var timed = new List<(MonthCalendarEvent E, int Col)>();
        foreach (var e in events)
        {
            if (e.LastDate < first || e.StartDate > last)
            {
                continue;
            }
            if (IsBar(e))
            {
                var s = -1;
                var end = -1;
                for (var c = 0; c < columns.Count; c++)
                {
                    if (Covers(e, columns[c]))
                    {
                        if (s < 0)
                        {
                            s = c;
                        }
                        end = c;
                    }
                }
                if (s >= 0)
                {
                    bars.Add((e, s, end, e.StartDate < columns[s], e.LastDate > columns[end]));
                }
            }
            else
            {
                for (var c = 0; c < columns.Count; c++)
                {
                    if (columns[c] == e.StartDate)
                    {
                        timed.Add((e, c));
                        break;
                    }
                }
            }
        }
        bars.Sort((a, b) =>
        {
            var byStart = a.E.StartDate.CompareTo(b.E.StartDate);
            if (byStart != 0)
            {
                return byStart;
            }
            var byLength = b.E.LastDate.CompareTo(a.E.LastDate);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.E.Title, b.E.Title);
        });
        timed.Sort((a, b) =>
        {
            var byStart = a.E.StartTime!.Value.CompareTo(b.E.StartTime!.Value);
            return byStart != 0 ? byStart : string.CompareOrdinal(a.E.Title, b.E.Title);
        });

        var occupied = new List<bool[]>();
        bool Free(int lane, int s, int e)
        {
            while (occupied.Count <= lane)
            {
                occupied.Add(new bool[columns.Count]);
            }
            for (var c = s; c <= e; c++)
            {
                if (occupied[lane][c])
                {
                    return false;
                }
            }
            return true;
        }
        void Take(int lane, int s, int e)
        {
            for (var c = s; c <= e; c++)
            {
                occupied[lane][c] = true;
            }
        }

        var placed = new List<(MonthCalendarEvent E, int S, int End, int Lane, bool Bar, bool CS, bool CE)>();
        foreach (var b in bars)
        {
            var lane = 0;
            while (!Free(lane, b.S, b.End))
            {
                lane++;
            }
            Take(lane, b.S, b.End);
            placed.Add((b.E, b.S, b.End, lane, true, b.CS, b.CE));
        }
        foreach (var t in timed)
        {
            var lane = 0;
            while (!Free(lane, t.Col, t.Col))
            {
                lane++;
            }
            Take(lane, t.Col, t.Col);
            placed.Add((t.E, t.Col, t.Col, lane, false, false, false));
        }

        // Per column: how many items cover it and the highest lane allowed to show.
        var counts = new int[columns.Count];
        foreach (var p in placed)
        {
            for (var c = p.S; c <= p.End; c++)
            {
                counts[c]++;
            }
        }
        var cap = new int[columns.Count];
        for (var c = 0; c < columns.Count; c++)
        {
            cap[c] = counts[c] > lanes ? lanes - 1 : lanes;
        }
        var shown = new int[columns.Count];
        foreach (var p in placed)
        {
            // Split into runs of columns where the lane is allowed.
            var runStart = -1;
            for (var c = p.S; c <= p.End + 1; c++)
            {
                var ok = c <= p.End && p.Lane < cap[c];
                if (ok && runStart < 0)
                {
                    runStart = c;
                }
                else if (!ok && runStart >= 0)
                {
                    var runEnd = c - 1;
                    layout.Items.Add(new MonthLaneItem(p.E, runStart, runEnd, p.Lane, p.Bar,
                        p.CS || runStart > p.S, p.CE || runEnd < p.End));
                    for (var k = runStart; k <= runEnd; k++)
                    {
                        shown[k]++;
                    }
                    runStart = -1;
                }
            }
        }
        for (var c = 0; c < columns.Count; c++)
        {
            if (counts[c] > lanes)
            {
                layout.More.Add(new MonthMoreItem(c, lanes - 1, counts[c] - shown[c], columns[c]));
            }
        }
        return layout;
    }

    /// <summary>The events that show on <paramref name="day"/>: all-day ones (by start, then title) and timed ones (by time).</summary>
    public static MonthDaySummary Summarize(DateOnly day, IEnumerable<MonthCalendarEvent> events, TimeOnly workStart,
                                            TimeOnly workEnd, TimeSpan minimumGap)
    {
        var summary = new MonthDaySummary();
        foreach (var e in events)
        {
            if (!Covers(e, day))
            {
                continue;
            }
            if (IsBar(e))
            {
                summary.AllDay.Add(e);
            }
            else
            {
                summary.Timed.Add(e);
            }
        }
        summary.AllDay.Sort((a, b) =>
        {
            var c = a.StartDate.CompareTo(b.StartDate);
            return c != 0 ? c : string.CompareOrdinal(a.Title, b.Title);
        });
        summary.Timed.Sort((a, b) =>
        {
            var c = a.StartTime!.Value.CompareTo(b.StartTime!.Value);
            return c != 0 ? c : string.CompareOrdinal(a.Title, b.Title);
        });

        // Busy time: every timed event except declined ones; booked: only those not waiting for a reply.
        var busy = new List<(TimeOnly S, TimeOnly E)>();
        var booked = TimeSpan.Zero;
        foreach (var e in summary.Timed)
        {
            if (e.Rsvp == MonthCalendarRsvp.No)
            {
                continue;
            }
            var s = e.StartTime!.Value;
            var end = e.EffectiveEndTime;
            if (end <= s)
            {
                continue;
            }
            busy.Add((s, end));
            if (e.Rsvp != MonthCalendarRsvp.Pending)
            {
                booked += end - s;
            }
        }
        summary.Booked = booked;

        var gaps = FreeGaps(busy, workStart, workEnd, minimumGap);
        var free = TimeSpan.Zero;
        foreach (var g in gaps)
        {
            free += g.E - g.S;
        }
        summary.Free = free;

        var entries = new List<MonthScheduleEntry>();
        foreach (var e in summary.Timed)
        {
            entries.Add(new MonthScheduleEntry(e, e.StartTime!.Value, e.EffectiveEndTime));
        }
        foreach (var g in gaps)
        {
            entries.Add(new MonthScheduleEntry(null, g.S, g.E));
        }
        entries.Sort((a, b) =>
        {
            var c = a.Start.CompareTo(b.Start);
            if (c != 0)
            {
                return c;
            }
            return a.IsGap == b.IsGap ? 0 : a.IsGap ? 1 : -1;
        });
        summary.Schedule.AddRange(entries);
        return summary;
    }

    /// <summary>The free stretches of at least <paramref name="minimum"/> between the working hours, around the busy ones.</summary>
    public static List<(TimeOnly S, TimeOnly E)> FreeGaps(IEnumerable<(TimeOnly S, TimeOnly E)> busy, TimeOnly workStart,
                                                          TimeOnly workEnd, TimeSpan minimum)
    {
        var sorted = busy.OrderBy(b => b.S).ToList();
        var gaps = new List<(TimeOnly, TimeOnly)>();
        var cursor = workStart;
        foreach (var (s, e) in sorted)
        {
            if (e <= cursor)
            {
                continue;
            }
            var gapEnd = s < workEnd ? s : workEnd;
            if (gapEnd > cursor && gapEnd - cursor >= minimum)
            {
                gaps.Add((cursor, gapEnd));
            }
            if (e > cursor)
            {
                cursor = e;
            }
            if (cursor >= workEnd)
            {
                break;
            }
        }
        if (cursor < workEnd && workEnd - cursor >= minimum)
        {
            gaps.Add((cursor, workEnd));
        }
        return gaps;
    }

    // Formats ----------------------------------------------------------------------------------------------------------

    private static bool UsesDesignator(CultureInfo culture) =>
        culture.DateTimeFormat.ShortTimePattern.Contains('t') && !string.IsNullOrEmpty(culture.DateTimeFormat.AMDesignator);

    /// <summary>A chip's time: "9:30a", "10a", "4p" (12-hour cultures) or "09:30" (24-hour cultures).</summary>
    public static string ChipTime(TimeOnly t, CultureInfo culture)
    {
        if (!UsesDesignator(culture))
        {
            return t.ToString("H:mm", culture);
        }
        var hour = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
        var suffix = t.Hour < 12 ? "a" : "p";
        return t.Minute == 0
            ? hour.ToString(culture) + suffix
            : hour.ToString(culture) + ":" + t.Minute.ToString("00", culture) + suffix;
    }

    /// <summary>A list row's time: "9:15 AM" (the culture's short time, with a plain space before the designator).</summary>
    public static string RowTime(TimeOnly t, CultureInfo culture) =>
        t.ToString(culture.DateTimeFormat.ShortTimePattern, culture).Replace('\u202F', ' ').Replace('\u00A0', ' ');

    /// <summary>A length: "15m", "1h", "1h 30m", "0m".</summary>
    public static string Duration(TimeSpan span)
    {
        var minutes = (int)Math.Round(span.TotalMinutes);
        if (minutes <= 0)
        {
            return "0m";
        }
        var h = minutes / 60;
        var m = minutes % 60;
        if (h == 0)
        {
            return m + "m";
        }
        return m == 0 ? h + "h" : h + "h " + m + "m";
    }

    /// <summary>The weekday letters or abbreviations of the shown columns.</summary>
    public static string WeekdayName(DayOfWeek day, CultureInfo culture, bool narrow)
    {
        var name = culture.DateTimeFormat.GetAbbreviatedDayName(day);
        if (!narrow || name.Length == 0)
        {
            return name;
        }
        return culture.TextInfo.ToUpper(name[..1]);
    }

    public static string Format(DateOnly date, string pattern, CultureInfo culture) =>
        date.ToDateTime(TimeOnly.MinValue).ToString(pattern, culture);
}
