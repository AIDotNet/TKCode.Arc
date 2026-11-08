using Avalonia.Collections;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>The board: the block edits these lists in place (moves, quick add, edits); the page saves them after a change.</summary>
public sealed class BoardPageViewModel(IProjectService projects)
{
    public string Title { get; } = projects.ProjectName;

    public AvaloniaList<KanbanColumn> Columns { get; } = new(projects.GetColumns());

    public AvaloniaList<ProjectPerson> People { get; } = new(projects.GetPeople());

    public AvaloniaList<KanbanLabel> Labels { get; } = new(projects.GetLabels());

    public AvaloniaList<KanbanCard> Cards { get; } = new(projects.GetCards());

    public void Save() => projects.SaveCards([.. Cards]);
}

/// <summary>The timeline: the block works on a copy of <see cref="Tasks"/> and reports the result, which <see cref="Save"/> keeps.</summary>
public sealed class TimelinePageViewModel(IProjectService projects)
{
    public AvaloniaList<GanttGroup> Groups { get; } = new(projects.GetTimelineGroups());

    public AvaloniaList<GanttTask> Tasks { get; } = new(projects.GetTimelineTasks());

    public void Save(IReadOnlyList<GanttTask> tasks) => projects.SaveTimeline(tasks);
}

/// <summary>The calendar: this week's events and the calendars they belong to.</summary>
public sealed class CalendarPageViewModel(IProjectService projects)
{
    private static readonly DateOnly s_monday = MondayOf(DateOnly.FromDateTime(DateTime.Today));

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public AvaloniaList<WeekCalendarSource> Calendars { get; } = new(projects.GetCalendars());

    public AvaloniaList<WeekCalendarEvent> Events { get; } = new(projects.GetEvents(s_monday));

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

/// <summary>The release gate: its checks, its owners and what starting the release does.</summary>
public sealed class ReleasePageViewModel(IProjectService projects)
{
    public IReadOnlyList<ReleaseCheck> Checks { get; } = projects.GetReleaseChecks();

    public IReadOnlyList<ProjectPerson> Owners { get; } = projects.GetReleaseOwners();

    /// <summary>The block awaits this when the release starts, and shows its progress and any failure.</summary>
    public Func<ReleaseRequest, CancellationToken, Task> Release => projects.ReleaseAsync;
}
