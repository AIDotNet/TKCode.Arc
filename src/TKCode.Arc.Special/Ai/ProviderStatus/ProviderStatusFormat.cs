namespace TKCode.Arc;

/// <summary>One cell of an uptime bar: a day of the window, real or padded.</summary>
internal readonly record struct ProviderStatusDayCell(DateOnly Date, double? Uptime, ProviderStatusHealth? Health, string? Note)
{
    /// <summary>True when the day has no data (padding, a gap or a null uptime without a health).</summary>
    public bool IsEmpty => Health is null;
}

/// <summary>The pure rules of <see cref="ProviderStatus"/>: the 30 day window, derived health, sums and ordering.</summary>
internal static class ProviderStatusFormat
{
    /// <summary>How many days the bar shows.</summary>
    public const int WindowDays = 30;

    /// <summary>Below this uptime a day is degraded.</summary>
    public const double DegradedBelow = 99.9;

    /// <summary>Below this uptime a day is an outage.</summary>
    public const double OutageBelow = 99;

    /// <summary>A day's health: its own, or derived from its uptime; null when it has neither.</summary>
    public static ProviderStatusHealth? HealthOf(ProviderStatusDay day)
    {
        if (day.Health is { } health)
        {
            return health;
        }
        if (day.Uptime is not { } uptime || !double.IsFinite(uptime))
        {
            return null;
        }
        return uptime < OutageBelow ? ProviderStatusHealth.Outage
            : uptime < DegradedBelow ? ProviderStatusHealth.Degraded
            : ProviderStatusHealth.Operational;
    }

    /// <summary>
    /// The 30 days ending on the last day of <paramref name="days"/> (or on <paramref name="today"/> without days), oldest
    /// first. A date with no entry is No data, so short or gappy histories still fill the bar.
    /// </summary>
    public static ProviderStatusDayCell[] Window(IReadOnlyList<ProviderStatusDay>? days, DateOnly today)
    {
        var cells = new ProviderStatusDayCell[WindowDays];
        var last = today;
        if (days is { Count: > 0 })
        {
            last = days[^1].Date;
        }
        if (last < DateOnly.MinValue.AddDays(WindowDays))
        {
            last = DateOnly.MinValue.AddDays(WindowDays);
        }
        var first = last.AddDays(-(WindowDays - 1));
        var byDate = new Dictionary<DateOnly, ProviderStatusDay>();
        if (days is not null)
        {
            // Only the newest 30 entries can land in the window; a later duplicate wins.
            for (var i = Math.Max(0, days.Count - WindowDays * 2); i < days.Count; i++)
            {
                var day = days[i];
                if (day.Date >= first && day.Date <= last)
                {
                    byDate[day.Date] = day;
                }
            }
        }
        for (var i = 0; i < WindowDays; i++)
        {
            var date = first.AddDays(i);
            cells[i] = byDate.TryGetValue(date, out var day)
                ? new ProviderStatusDayCell(date, day.Uptime is { } u && double.IsFinite(u) ? Math.Clamp(u, 0, 100) : null, HealthOf(day), day.Note)
                : new ProviderStatusDayCell(date, null, null, null);
        }
        return cells;
    }

    /// <summary>The mean uptime of the days with data, or null when none has any.</summary>
    public static double? Average(ProviderStatusDayCell[] cells)
    {
        var sum = 0.0;
        var count = 0;
        foreach (var cell in cells)
        {
            if (cell.Uptime is { } uptime)
            {
                sum += uptime;
                count++;
            }
        }
        return count == 0 ? null : sum / count;
    }

    /// <summary>How many days of the window are degraded and how many are outages.</summary>
    public static (int Degraded, int Outage) Counts(ProviderStatusDayCell[] cells)
    {
        var degraded = 0;
        var outage = 0;
        foreach (var cell in cells)
        {
            if (cell.Health == ProviderStatusHealth.Degraded)
            {
                degraded++;
            }
            else if (cell.Health == ProviderStatusHealth.Outage)
            {
                outage++;
            }
        }
        return (degraded, outage);
    }

    /// <summary>How many providers are operational, degraded and failing.</summary>
    public static (int Operational, int Degraded, int Outage) Health(IReadOnlyList<ProviderStatusItem> providers)
    {
        var operational = 0;
        var degraded = 0;
        var outage = 0;
        foreach (var provider in providers)
        {
            switch (provider.Health)
            {
                case ProviderStatusHealth.Degraded:
                    degraded++;
                    break;
                case ProviderStatusHealth.Outage:
                    outage++;
                    break;
                default:
                    operational++;
                    break;
            }
        }
        return (operational, degraded, outage);
    }

    /// <summary>
    /// The updates newest first: by time when both have one, otherwise by stage (Resolved last posted), then the later one
    /// in the list first.
    /// </summary>
    public static List<ProviderStatusUpdate> Newest(ProviderStatusIncident incident)
    {
        var indexed = new List<(ProviderStatusUpdate Update, int Index)>(incident.Updates.Count);
        for (var i = 0; i < incident.Updates.Count; i++)
        {
            indexed.Add((incident.Updates[i], i));
        }
        indexed.Sort((a, b) =>
        {
            if (a.Update.At is { } at && b.Update.At is { } bt && at != bt)
            {
                return bt.CompareTo(at);
            }
            if (a.Update.Stage != b.Update.Stage)
            {
                return b.Update.Stage.CompareTo(a.Update.Stage);
            }
            return b.Index.CompareTo(a.Index);
        });
        var result = new List<ProviderStatusUpdate>(indexed.Count);
        foreach (var (update, _) in indexed)
        {
            result.Add(update);
        }
        return result;
    }

    /// <summary>Whether the incident's newest update says it is over.</summary>
    public static bool IsResolved(ProviderStatusIncident incident)
    {
        var newest = Newest(incident);
        return newest.Count > 0 && newest[0].Stage == ProviderStatusStage.Resolved;
    }

    /// <summary>The time shown for an update: its preformatted text, its time, or nothing.</summary>
    public static string TimeOf(ProviderStatusUpdate update, ProviderStatusLabels labels) =>
        update.AtText ?? (update.At is { } at ? labels.Time(at) : string.Empty);

    /// <summary>The latency text of a provider.</summary>
    public static string Latency(double? ms, ProviderStatusLabels labels) =>
        ms is { } value && double.IsFinite(value) && value >= 0 ? labels.Latency(value) : labels.Missing;

    /// <summary>The 30 day uptime text.</summary>
    public static string Uptime(double? percent, ProviderStatusLabels labels) =>
        percent is { } value ? labels.Percent(value) : labels.Missing;

    /// <summary>The first letter of a name for its monogram.</summary>
    public static string Monogram(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                return char.ToUpperInvariant(c).ToString();
            }
        }
        return name.Trim()[..1];
    }
}
