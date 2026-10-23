namespace TKCode.Arc;

/// <summary>A run of consecutive days: the range a <see cref="WeekCalendar"/> pane shows.</summary>
internal readonly record struct WeekCalendarDays(DateOnly Start, int Count)
{
    /// <summary>The last day (inclusive).</summary>
    public DateOnly End => Start.AddDays(Math.Max(1, Count) - 1);

    /// <summary>True when <paramref name="day"/> is one of the days.</summary>
    public bool Contains(DateOnly day) => day >= Start && day <= End;

    /// <summary>The column of <paramref name="day"/> (may be outside 0..Count−1).</summary>
    public int ColumnOf(DateOnly day) => day.DayNumber - Start.DayNumber;

    /// <summary>The day in column <paramref name="column"/>.</summary>
    public DateOnly DayAt(int column) => Start.AddDays(column);
}

/// <summary>
/// The range, step and cursor rules of the week calendar (Arc measurements, notes §4.1): which days each view shows,
/// how far the previous / next buttons move, where the cursor day goes, when Today is disabled and which view a narrow
/// calendar shows. Pure; no UI.
/// </summary>
internal static class WeekCalendarRange
{
    /// <summary>The number of days a view shows.</summary>
    public static int DaysOf(WeekCalendarView view) => view switch
    {
        WeekCalendarView.WorkWeek => 5,
        WeekCalendarView.Day => 1,
        WeekCalendarView.ThreeDay => 3,
        _ => 7,
    };

    /// <summary>How many days the previous and next buttons move: a week, one day, or three days.</summary>
    public static int StepOf(WeekCalendarView view) => view switch
    {
        WeekCalendarView.Day => 1,
        WeekCalendarView.ThreeDay => 3,
        _ => 7,
    };

    /// <summary>
    /// The view a calendar shows for the chosen <paramref name="view"/>: narrow calendars show three days for the week
    /// views; wide ones show the week for the three-day and agenda views (Arc offers them only when narrow).
    /// </summary>
    public static WeekCalendarView Effective(WeekCalendarView view, bool compact) => compact
        ? view switch
        {
            WeekCalendarView.Week or WeekCalendarView.WorkWeek => WeekCalendarView.ThreeDay,
            _ => view,
        }
        : view switch
        {
            WeekCalendarView.ThreeDay or WeekCalendarView.Agenda => WeekCalendarView.Week,
            _ => view,
        };

    /// <summary>The views offered in the view switch, in order: Week / Work week / Day, or 3 days / Day / Agenda when narrow.</summary>
    public static WeekCalendarView[] Offered(bool compact) => compact
        ? [WeekCalendarView.ThreeDay, WeekCalendarView.Day, WeekCalendarView.Agenda]
        : [WeekCalendarView.Week, WeekCalendarView.WorkWeek, WeekCalendarView.Day];

    /// <summary>The first day of the week that holds <paramref name="date"/>.</summary>
    public static DateOnly WeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var back = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        return date.AddDays(-back);
    }

    /// <summary>
    /// The Monday of the work week around <paramref name="date"/>: Monday to Friday of its week when the week starts on a
    /// weekend day or a Monday, else (weeks starting mid-week) the Monday on or before the date.
    /// </summary>
    public static DateOnly WorkWeekStart(DateOnly date, DayOfWeek firstDay)
    {
        var weekStart = WeekStart(date, firstDay);
        var toMonday = ((int)DayOfWeek.Monday - (int)firstDay + 7) % 7;
        if (toMonday <= 2)
        {
            return weekStart.AddDays(toMonday);
        }
        return WeekStart(date, DayOfWeek.Monday);
    }

    /// <summary>The days <paramref name="view"/> shows for <paramref name="date"/> when nothing else is known.</summary>
    public static WeekCalendarDays For(WeekCalendarView view, DateOnly date, DayOfWeek firstDay) => view switch
    {
        WeekCalendarView.WorkWeek => new WeekCalendarDays(WorkWeekStart(date, firstDay), 5),
        WeekCalendarView.Day => new WeekCalendarDays(date, 1),
        WeekCalendarView.ThreeDay => new WeekCalendarDays(date, 3),
        _ => new WeekCalendarDays(WeekStart(date, firstDay), 7),
    };

    /// <summary>
    /// The days to show when the cursor day becomes <paramref name="date"/>: the current range when it still fits the view
    /// and holds the date, else the view's range for the date.
    /// </summary>
    public static WeekCalendarDays Settle(WeekCalendarView view, WeekCalendarDays? current, DateOnly date, DayOfWeek firstDay)
    {
        if (current is { } range && range.Count == DaysOf(view) && range.Contains(date))
        {
            if (view is WeekCalendarView.ThreeDay or WeekCalendarView.Day)
            {
                return range;
            }
            // Week views keep their alignment with the first day of the week.
            var aligned = For(view, date, firstDay);
            if (aligned.Start == range.Start)
            {
                return range;
            }
        }
        return For(view, date, firstDay);
    }

    /// <summary>Keeps the cursor day inside <paramref name="range"/> (a work week on a Saturday focuses its Friday).</summary>
    public static DateOnly Clamp(WeekCalendarDays range, DateOnly date)
    {
        if (date < range.Start)
        {
            return range.Start;
        }
        return date > range.End ? range.End : date;
    }

    /// <summary>
    /// The previous (−1) or next (+1) range: the start moves by the view's step and the cursor moves the same number of
    /// days (a Monday cursor lands on the next Monday), kept inside the new range.
    /// </summary>
    public static (WeekCalendarDays Range, DateOnly Cursor) Step(WeekCalendarView view, WeekCalendarDays range, DateOnly cursor, int direction)
    {
        var step = StepOf(view) * Math.Sign(direction);
        var next = new WeekCalendarDays(range.Start.AddDays(step), DaysOf(view));
        return (next, Clamp(next, cursor.AddDays(step)));
    }

    /// <summary>
    /// Moves the cursor by <paramref name="delta"/> days (the arrow keys). Inside the range only the cursor moves; past its
    /// edge the next range comes in and the cursor lands on its first day (moving forward) or its last day (backward).
    /// </summary>
    public static (WeekCalendarDays Range, DateOnly Cursor, int Direction) MoveCursor(WeekCalendarView view, WeekCalendarDays range,
                                                                                    DateOnly cursor, int delta)
    {
        var target = cursor.AddDays(delta);
        if (range.Contains(target))
        {
            return (range, target, 0);
        }
        var direction = Math.Sign(delta);
        var (next, _) = Step(view, range, cursor, direction);
        return (next, direction > 0 ? next.Start : next.End, direction);
    }

    /// <summary>
    /// True when the Today button has nothing to do: today is shown, or (work week on a weekend) the range already is this
    /// week's Monday to Friday.
    /// </summary>
    public static bool IsTodayShown(WeekCalendarView view, WeekCalendarDays range, DateOnly today, DayOfWeek firstDay)
    {
        if (range.Contains(today))
        {
            return true;
        }
        return view == WeekCalendarView.WorkWeek && For(view, today, firstDay).Start == range.Start;
    }

    /// <summary>The direction of travel between two ranges: +1 later, −1 earlier, 0 same start.</summary>
    public static int Direction(DateOnly oldStart, DateOnly newStart) => Math.Sign(newStart.DayNumber - oldStart.DayNumber);
}
