namespace TKCode.Arc;

/// <summary>One visible row of the chart: a phase row or a task row.</summary>
internal readonly record struct GanttRow(GanttGroup? Group, GanttTask? Task, int GroupTaskCount, bool IsCollapsed);

/// <summary>
/// One dependency arrow. A task inside a folded phase has no bar, so its end of the arrow sits on the phase's summary bar;
/// <see cref="FoldedTask"/> and <see cref="FoldedDependency"/> name that phase.
/// </summary>
internal readonly record struct GanttLink(GanttTask Task, GanttTask Dependency, GanttGroup? FoldedTask, GanttGroup? FoldedDependency);

/// <summary>
/// The pure part of <see cref="GanttTimeline"/>: rows, dates and pixels, dependency conflicts and group summaries.
/// No Avalonia types, so the rules are testable without a window.
/// </summary>
internal sealed class GanttModel
{
    public const double RowHeight = 40;

    public GanttModel(IReadOnlyList<GanttGroup> groups, IReadOnlyList<GanttTask> tasks, IReadOnlySet<string> collapsed)
    {
        Groups = groups;
        Tasks = tasks;
        Collapsed = collapsed;
        var byId = new Dictionary<string, GanttTask>();
        foreach (var t in tasks)
        {
            byId[t.Id] = t;
        }
        ById = byId;
        var rows = new List<GanttRow>();
        foreach (var g in groups)
        {
            var members = tasks.Where(t => t.GroupId == g.Id).ToList();
            var folded = collapsed.Contains(g.Id);
            rows.Add(new GanttRow(g, null, members.Count, folded));
            if (!folded)
            {
                rows.AddRange(members.Select(t => new GanttRow(null, t, 0, false)));
            }
        }
        Rows = rows;
        Links = BuildLinks();
    }

    public IReadOnlyList<GanttGroup> Groups { get; }

    public IReadOnlyList<GanttTask> Tasks { get; }

    public IReadOnlySet<string> Collapsed { get; }

    public IReadOnlyDictionary<string, GanttTask> ById { get; }

    public IReadOnlyList<GanttRow> Rows { get; }

    /// <summary>The dependency arrows to draw; a dependency between two tasks folded into the same phase has none.</summary>
    public IReadOnlyList<GanttLink> Links { get; }

    private List<GanttLink> BuildLinks()
    {
        var groups = new Dictionary<string, GanttGroup>();
        foreach (var g in Groups)
        {
            groups[g.Id] = g;
        }
        var links = new List<GanttLink>();
        foreach (var task in Tasks)
        {
            if (!groups.TryGetValue(task.GroupId, out var taskGroup))
            {
                continue;
            }
            foreach (var id in task.DependsOn)
            {
                if (!ById.TryGetValue(id, out var dep) || !groups.TryGetValue(dep.GroupId, out var depGroup))
                {
                    continue;
                }
                var foldedTask = Collapsed.Contains(taskGroup.Id) ? taskGroup : null;
                var foldedDep = Collapsed.Contains(depGroup.Id) ? depGroup : null;
                if (foldedTask is not null && ReferenceEquals(foldedTask, foldedDep))
                {
                    continue;
                }
                links.Add(new GanttLink(task, dep, foldedTask, foldedDep));
            }
        }
        return links;
    }

    /// <summary>The pixels one day takes at <paramref name="zoom"/>.</summary>
    public static double DayWidth(GanttZoom zoom) => zoom switch
    {
        GanttZoom.Day => 36,
        GanttZoom.Month => 5,
        _ => 14,
    };

    /// <summary>The first and last day of the calendar: the tasks' span padded to whole weeks (Monday to Sunday) plus a week each side.</summary>
    public (DateOnly First, DateOnly Last) Range(DateOnly today)
    {
        var min = today;
        var max = today;
        foreach (var t in Tasks)
        {
            min = t.Start < min ? t.Start : min;
            max = t.End > max ? t.End : max;
        }
        var first = StartOfWeek(min).AddDays(-7);
        var last = StartOfWeek(max).AddDays(13);
        return (first, last);
    }

    public static DateOnly StartOfWeek(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>The days a task covers (a milestone covers one).</summary>
    public static int Span(GanttTask t) => t.IsMilestone ? 1 : t.End.DayNumber - t.Start.DayNumber + 1;

    /// <summary>True when the task starts before a task it depends on has ended (or on the same day).</summary>
    public bool HasConflict(GanttTask t)
    {
        foreach (var id in t.DependsOn)
        {
            if (ById.TryGetValue(id, out var dep) && t.Start <= (dep.IsMilestone ? dep.Start : dep.End))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The tasks (not phases) in reading order that are visible.</summary>
    public IEnumerable<GanttTask> VisibleTasks() => Rows.Where(r => r.Task is not null).Select(r => r.Task!);

    /// <summary>The span and progress of a phase: earliest start, latest end, average progress weighted by days.</summary>
    public (DateOnly Start, DateOnly End, double Progress)? Summary(GanttGroup group)
    {
        var members = Tasks.Where(t => t.GroupId == group.Id).ToList();
        if (members.Count == 0)
        {
            return null;
        }
        var start = members.Min(t => t.Start);
        var end = members.Max(t => t.IsMilestone ? t.Start : t.End);
        var days = members.Sum(Span);
        var done = members.Sum(t => Span(t) * Math.Clamp(t.Progress, 0, 1));
        return (start, end, days == 0 ? 0 : done / days);
    }

    /// <summary>The overall percent done, days-weighted.</summary>
    public double OverallProgress()
    {
        var days = Tasks.Sum(Span);
        return days == 0 ? 0 : Tasks.Sum(t => Span(t) * Math.Clamp(t.Progress, 0, 1)) / days;
    }

    /// <summary>The rubber band for a bar pushed shorter than one day: the drag distance resisted with a soft limit.</summary>
    public static double Resist(double overshoot, double limit = 28) => limit * (1 - Math.Exp(-Math.Max(0, overshoot) / limit));
}
