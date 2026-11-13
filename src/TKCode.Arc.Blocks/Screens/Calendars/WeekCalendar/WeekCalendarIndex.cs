namespace TKCode.Arc;

/// <summary>The part of a timed event that falls on one day (an event across midnight has one per day).</summary>
internal sealed class WeekCalendarSegment
{
    public WeekCalendarSegment(WeekCalendarEvent calendarEvent, DateOnly day, double startMinute, double endMinute,
                               DateTime wallStart, DateTime wallEnd, DateTimeOffset end)
    {
        Event = calendarEvent;
        Day = day;
        StartMinute = startMinute;
        EndMinute = endMinute;
        WallStart = wallStart;
        WallEnd = wallEnd;
        EndInstant = end;
    }

    /// <summary>The event.</summary>
    public WeekCalendarEvent Event { get; }

    /// <summary>The day.</summary>
    public DateOnly Day { get; }

    /// <summary>The first minute of the day it covers (0–1440).</summary>
    public double StartMinute { get; }

    /// <summary>The minute of the day it ends at (exclusive, up to 1440).</summary>
    public double EndMinute { get; }

    /// <summary>The event's start in the calendar's zone.</summary>
    public DateTime WallStart { get; }

    /// <summary>The event's end in the calendar's zone.</summary>
    public DateTime WallEnd { get; }

    /// <summary>The event's end as an instant (an event is past once this is before now).</summary>
    public DateTimeOffset EndInstant { get; }

    /// <summary>True when the event started on an earlier day.</summary>
    public bool ContinuesBefore => WallStart < Day.ToDateTime(TimeOnly.MinValue);

    /// <summary>True when the event goes on after this day.</summary>
    public bool ContinuesAfter => WallEnd > Day.AddDays(1).ToDateTime(TimeOnly.MinValue);
}

/// <summary>An all-day event and the dates it covers.</summary>
internal sealed record WeekCalendarAllDayEntry(WeekCalendarEvent Event, DateOnly Start, DateOnly EndExclusive)
{
    /// <summary>The last day it covers.</summary>
    public DateOnly LastDay => EndExclusive.AddDays(-1);

    /// <summary>True when it covers <paramref name="day"/>.</summary>
    public bool Covers(DateOnly day) => day >= Start && day < EndExclusive;
}

/// <summary>
/// The events of a week calendar indexed by day in the calendar's time zone: timed events split at midnight, all-day events
/// as date spans, hidden calendars left out. Rebuilt once per change of the events, the calendars or the zone; panes only
/// query it. Pure; no UI.
/// </summary>
internal sealed class WeekCalendarIndex
{
    private readonly Dictionary<DateOnly, List<WeekCalendarSegment>> _timed = new();
    private readonly List<WeekCalendarAllDayEntry> _allDay = new();

    /// <summary>An empty index.</summary>
    public static readonly WeekCalendarIndex Empty = new();

    /// <summary>The number of events indexed.</summary>
    public int EventCount { get; private set; }

    /// <summary>Indexes <paramref name="events"/> (those <paramref name="isVisible"/> accepts) in <paramref name="zone"/>.</summary>
    public static WeekCalendarIndex Build(IEnumerable<WeekCalendarEvent> events, Func<WeekCalendarEvent, bool> isVisible, TimeZoneInfo zone)
    {
        var index = new WeekCalendarIndex();
        foreach (var e in events)
        {
            if (!isVisible(e))
            {
                continue;
            }
            index.EventCount++;
            if (e.IsAllDay)
            {
                var start = DateOnly.FromDateTime(e.Start.DateTime);
                var endWall = e.End.DateTime;
                var end = DateOnly.FromDateTime(endWall);
                if (endWall.TimeOfDay > TimeSpan.Zero)
                {
                    end = end.AddDays(1);
                }
                if (end <= start)
                {
                    end = start.AddDays(1);
                }
                index._allDay.Add(new WeekCalendarAllDayEntry(e, start, end));
                continue;
            }
            var wallStart = WeekCalendarTime.ToWall(e.Start, zone);
            var endInstant = e.End > e.Start ? e.End : e.Start.AddMinutes(WeekCalendarTimeGrid.SnapMinutes);
            var wallEnd = WeekCalendarTime.ToWall(endInstant, zone);
            if (wallEnd <= wallStart)
            {
                wallEnd = wallStart.AddMinutes(WeekCalendarTimeGrid.SnapMinutes);
            }
            var day = DateOnly.FromDateTime(wallStart);
            var lastDay = DateOnly.FromDateTime(wallEnd.AddTicks(-1));
            // A very long event is capped at 62 days so a typo cannot fill memory.
            for (var guard = 0; day <= lastDay && guard < 62; day = day.AddDays(1), guard++)
            {
                var dayStart = day.ToDateTime(TimeOnly.MinValue);
                var from = wallStart > dayStart ? (wallStart - dayStart).TotalMinutes : 0;
                var to = wallEnd < dayStart.AddDays(1) ? (wallEnd - dayStart).TotalMinutes : 1440;
                if (!index._timed.TryGetValue(day, out var list))
                {
                    list = new List<WeekCalendarSegment>();
                    index._timed[day] = list;
                }
                list.Add(new WeekCalendarSegment(e, day, from, to, wallStart, wallEnd, endInstant));
            }
        }
        foreach (var list in index._timed.Values)
        {
            list.Sort(static (a, b) =>
            {
                var byStart = a.StartMinute.CompareTo(b.StartMinute);
                return byStart != 0 ? byStart : (b.EndMinute - b.StartMinute).CompareTo(a.EndMinute - a.StartMinute);
            });
        }
        index._allDay.Sort(static (a, b) =>
        {
            var byStart = a.Start.CompareTo(b.Start);
            if (byStart != 0)
            {
                return byStart;
            }
            return (a.EndExclusive.DayNumber - a.Start.DayNumber).CompareTo(b.EndExclusive.DayNumber - b.Start.DayNumber);
        });
        return index;
    }

    private static readonly List<WeekCalendarSegment> s_none = new();

    /// <summary>The timed segments of <paramref name="day"/>, by start, longer first.</summary>
    public IReadOnlyList<WeekCalendarSegment> Timed(DateOnly day) => _timed.TryGetValue(day, out var list) ? list : s_none;

    /// <summary>
    /// The timed segments of <paramref name="day"/> that fall inside the hours shown, clipped to them: (segment, start, end).
    /// </summary>
    public List<(WeekCalendarSegment Segment, double Start, double End)> TimedInHours(DateOnly day, WeekCalendarTimeGrid grid)
    {
        var result = new List<(WeekCalendarSegment, double, double)>();
        foreach (var segment in Timed(day))
        {
            var start = Math.Max(segment.StartMinute, grid.StartMinute);
            var end = Math.Min(segment.EndMinute, grid.EndMinute);
            if (end > start)
            {
                result.Add((segment, start, end));
            }
        }
        return result;
    }

    /// <summary>The all-day events that cover any of <paramref name="days"/>, by first day, shorter first.</summary>
    public List<WeekCalendarAllDayEntry> AllDay(WeekCalendarDays days)
    {
        var result = new List<WeekCalendarAllDayEntry>();
        var endExclusive = days.End.AddDays(1);
        foreach (var entry in _allDay)
        {
            if (entry.Start < endExclusive && entry.EndExclusive > days.Start)
            {
                result.Add(entry);
            }
        }
        return result;
    }

    /// <summary>The all-day events on one day.</summary>
    public IEnumerable<WeekCalendarAllDayEntry> AllDayOn(DateOnly day) => _allDay.Where(e => e.Covers(day));

    /// <summary>How many events (timed and all-day) a day has.</summary>
    public int CountOn(DateOnly day) => Timed(day).Count + _allDay.Count(e => e.Covers(day));

    /// <summary>The earliest start minute of a timed event inside the hours shown on any of <paramref name="days"/>.</summary>
    public int? EarliestMinute(WeekCalendarDays days, WeekCalendarTimeGrid grid)
    {
        double? earliest = null;
        for (var i = 0; i < days.Count; i++)
        {
            foreach (var (_, start, _) in TimedInHours(days.DayAt(i), grid))
            {
                if (earliest is null || start < earliest)
                {
                    earliest = start;
                }
            }
        }
        return earliest is { } e ? (int)Math.Floor(e) : null;
    }
}
