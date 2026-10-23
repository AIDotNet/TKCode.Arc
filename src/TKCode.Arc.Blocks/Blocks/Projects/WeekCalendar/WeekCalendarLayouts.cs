namespace TKCode.Arc;

/// <summary>Where a timed event sits in its day column: <see cref="Column"/> of <see cref="Columns"/>, <see cref="Span"/> columns wide.</summary>
internal readonly record struct WeekCalendarColumnSlot(int Column, int Columns, int Span)
{
    /// <summary>The left edge as a fraction of the day column.</summary>
    public double Left => Columns > 0 ? (double)Column / Columns : 0;

    /// <summary>The width as a fraction of the day column.</summary>
    public double Width => Columns > 0 ? (double)Span / Columns : 1;
}

/// <summary>
/// Side-by-side columns for the overlapping timed events of one day (Arc, measured with constructed cases; notes §4.2):
/// <list type="number">
/// <item>sort by start, then longer first;</item>
/// <item>only intervals that strictly intersect overlap (an event ending at 2:00 and one starting at 2:00 do not); overlaps
/// chain into clusters;</item>
/// <item>in a cluster each event takes the lowest column that is free at its start; the cluster has as many columns as it
/// used;</item>
/// <item>each event widens to the right over the following columns that stay free for its whole time.</item>
/// </list>
/// No cascading or indenting: every event is a plain column. Pure; no UI.
/// </summary>
internal static class WeekCalendarOverlapLayout
{
    /// <summary>Lays out events given as (start, end) minutes; returns one slot per input, in input order.</summary>
    public static WeekCalendarColumnSlot[] Layout(IReadOnlyList<(double Start, double End)> events)
    {
        var count = events.Count;
        var result = new WeekCalendarColumnSlot[count];
        if (count == 0)
        {
            return result;
        }
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }
        Array.Sort(order, (a, b) =>
        {
            var byStart = events[a].Start.CompareTo(events[b].Start);
            if (byStart != 0)
            {
                return byStart;
            }
            var byLength = (events[b].End - events[b].Start).CompareTo(events[a].End - events[a].Start);
            return byLength != 0 ? byLength : a.CompareTo(b);
        });

        var cluster = new List<int>();
        var columnOf = new int[count];
        var columnEnds = new List<double>();
        var clusterEnd = double.NegativeInfinity;
        foreach (var index in order)
        {
            var (start, end) = events[index];
            if (cluster.Count > 0 && !(start < clusterEnd))
            {
                Finish(cluster, columnEnds.Count, events, columnOf, result);
                cluster.Clear();
                columnEnds.Clear();
                clusterEnd = double.NegativeInfinity;
            }
            var column = -1;
            for (var c = 0; c < columnEnds.Count; c++)
            {
                if (columnEnds[c] <= start)
                {
                    column = c;
                    break;
                }
            }
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(end);
            }
            else
            {
                columnEnds[column] = end;
            }
            columnOf[index] = column;
            cluster.Add(index);
            clusterEnd = Math.Max(clusterEnd, end);
        }
        Finish(cluster, columnEnds.Count, events, columnOf, result);
        return result;
    }

    private static void Finish(List<int> cluster, int columns, IReadOnlyList<(double Start, double End)> events, int[] columnOf,
                               WeekCalendarColumnSlot[] result)
    {
        foreach (var index in cluster)
        {
            var column = columnOf[index];
            var (start, end) = events[index];
            var span = 1;
            for (var next = column + 1; next < columns; next++)
            {
                var blocked = false;
                foreach (var other in cluster)
                {
                    if (columnOf[other] == next && events[other].Start < end && start < events[other].End)
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked)
                {
                    break;
                }
                span++;
            }
            result[index] = new WeekCalendarColumnSlot(column, columns, span);
        }
    }
}

/// <summary>An all-day event placed in the all-day row: its lane and the columns it covers (clipped to the range).</summary>
internal readonly record struct WeekCalendarLanePlacement(int Lane, int FirstColumn, int Span)
{
    /// <summary>The last column it covers.</summary>
    public int LastColumn => FirstColumn + Span - 1;

    /// <summary>False for an event outside the range.</summary>
    public bool IsShown => Span > 0;
}

/// <summary>The all-day row of one range: every event's placement and the lanes used.</summary>
internal sealed class WeekCalendarLanes
{
    public WeekCalendarLanes(WeekCalendarLanePlacement[] placements, int laneCount, int columns)
    {
        Placements = placements;
        LaneCount = laneCount;
        Columns = columns;
    }

    /// <summary>One placement per input event, in input order.</summary>
    public WeekCalendarLanePlacement[] Placements { get; }

    /// <summary>The lanes in use (0 when there are no all-day events).</summary>
    public int LaneCount { get; }

    /// <summary>The number of day columns.</summary>
    public int Columns { get; }

    /// <summary>True when more lanes are needed than <paramref name="limit"/>: the row can collapse.</summary>
    public bool Overflows(int limit) => limit > 0 && LaneCount > limit;

    /// <summary>
    /// The lanes drawn: all of them when <paramref name="expanded"/> or they fit <paramref name="limit"/>; else the limit,
    /// with the last of them holding the "+N more" chips.
    /// </summary>
    public int VisibleLanes(int limit, bool expanded) => expanded || !Overflows(limit) ? LaneCount : limit;

    /// <summary>True when the placement is drawn as a bar (it is in a lane above the chips' lane, or nothing is collapsed).</summary>
    public bool IsBarVisible(int index, int limit, bool expanded)
    {
        var placement = Placements[index];
        if (!placement.IsShown)
        {
            return false;
        }
        return expanded || !Overflows(limit) || placement.Lane < limit - 1;
    }

    /// <summary>Per column, how many events are hidden behind the "+N more" chip of a collapsed row (all zero otherwise).</summary>
    public int[] HiddenPerColumn(int limit, bool expanded)
    {
        var hidden = new int[Columns];
        if (expanded || !Overflows(limit))
        {
            return hidden;
        }
        foreach (var placement in Placements)
        {
            if (!placement.IsShown || placement.Lane < limit - 1)
            {
                continue;
            }
            for (var c = placement.FirstColumn; c <= placement.LastColumn; c++)
            {
                hidden[c]++;
            }
        }
        return hidden;
    }

    /// <summary>The row height: 8 plus 24 per lane drawn, at least one lane (32 when empty).</summary>
    public double RowHeight(int limit, bool expanded, double laneHeight = WeekCalendarAllDayLayout.LaneHeight,
                            double padding = WeekCalendarAllDayLayout.RowPadding) =>
        padding + Math.Max(1, VisibleLanes(limit, expanded)) * laneHeight;
}

/// <summary>
/// Lanes for the all-day row (notes §4.3): events sorted by first day, then longer first, each in the first lane that is
/// free over its days; parts outside the range are clipped to the visible columns. Pure; no UI.
/// </summary>
internal static class WeekCalendarAllDayLayout
{
    /// <summary>The height of a lane.</summary>
    public const double LaneHeight = 24;

    /// <summary>Space around the lanes (4 above the first bar, the rest below the last).</summary>
    public const double RowPadding = 8;

    /// <summary>The top of the first bar.</summary>
    public const double FirstBarTop = 4;

    /// <summary>The height of a bar.</summary>
    public const double BarHeight = 21;

    /// <summary>Arc never showed more than three lanes; the row collapses past this many (our rule, notes §12).</summary>
    public const int DefaultLaneLimit = 3;

    /// <summary>Places events given as dates (start inclusive, end exclusive) in a range of <paramref name="columns"/> days.</summary>
    public static WeekCalendarLanes Layout(IReadOnlyList<(DateOnly Start, DateOnly EndExclusive)> events, DateOnly rangeStart, int columns)
    {
        var count = events.Count;
        var placements = new WeekCalendarLanePlacement[count];
        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }
        Array.Sort(order, (a, b) =>
        {
            var byStart = events[a].Start.CompareTo(events[b].Start);
            if (byStart != 0)
            {
                return byStart;
            }
            var lengthA = events[a].EndExclusive.DayNumber - events[a].Start.DayNumber;
            var lengthB = events[b].EndExclusive.DayNumber - events[b].Start.DayNumber;
            return lengthA != lengthB ? lengthB.CompareTo(lengthA) : a.CompareTo(b);
        });
        var lanes = new List<bool[]>();
        foreach (var index in order)
        {
            var (start, endExclusive) = events[index];
            if (endExclusive <= start)
            {
                endExclusive = start.AddDays(1);
            }
            var first = Math.Max(0, start.DayNumber - rangeStart.DayNumber);
            var last = Math.Min(columns - 1, endExclusive.DayNumber - 1 - rangeStart.DayNumber);
            if (last < first)
            {
                placements[index] = new WeekCalendarLanePlacement(-1, 0, 0);
                continue;
            }
            var lane = 0;
            for (; lane < lanes.Count; lane++)
            {
                var taken = false;
                for (var c = first; c <= last; c++)
                {
                    if (lanes[lane][c])
                    {
                        taken = true;
                        break;
                    }
                }
                if (!taken)
                {
                    break;
                }
            }
            if (lane == lanes.Count)
            {
                lanes.Add(new bool[columns]);
            }
            for (var c = first; c <= last; c++)
            {
                lanes[lane][c] = true;
            }
            placements[index] = new WeekCalendarLanePlacement(lane, first, last - first + 1);
        }
        return new WeekCalendarLanes(placements, lanes.Count, columns);
    }

    /// <summary>The top of a bar in <paramref name="lane"/>.</summary>
    public static double BarTop(int lane) => FirstBarTop + lane * LaneHeight;
}
