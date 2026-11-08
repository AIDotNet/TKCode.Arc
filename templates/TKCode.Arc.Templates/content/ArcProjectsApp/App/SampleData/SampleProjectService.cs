using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>Invented project data (SampleBoard, SampleTimeline, SampleCalendar, SampleRelease). Saving keeps the changes in memory only.</summary>
public sealed class SampleProjectService : IProjectService
{
    private IReadOnlyList<KanbanCard>? _cards;
    private IReadOnlyList<GanttTask>? _tasks;

    public string ProjectName => SampleBoard.Title;

    public IReadOnlyList<ProjectPerson> GetPeople() => SampleBoard.CreatePeople();

    public IReadOnlyList<KanbanColumn> GetColumns() => SampleBoard.CreateColumns();

    public IReadOnlyList<KanbanLabel> GetLabels() => SampleBoard.CreateLabels();

    public IReadOnlyList<KanbanCard> GetCards() => _cards ??= SampleBoard.CreateCards();

    public void SaveCards(IReadOnlyList<KanbanCard> cards) => _cards = cards;

    public IReadOnlyList<GanttGroup> GetTimelineGroups() => SampleTimeline.Groups();

    public IReadOnlyList<GanttTask> GetTimelineTasks() => _tasks ??= SampleTimeline.Tasks();

    public void SaveTimeline(IReadOnlyList<GanttTask> tasks) => _tasks = tasks;

    public IReadOnlyList<WeekCalendarSource> GetCalendars() => SampleCalendar.CreateCalendars();

    public IReadOnlyList<WeekCalendarEvent> GetEvents(DateOnly monday) => SampleCalendar.CreateEvents(monday);

    public IReadOnlyList<ReleaseCheck> GetReleaseChecks() => SampleRelease.RolloutChecks();

    public IReadOnlyList<ProjectPerson> GetReleaseOwners() => SampleRelease.RolloutOwners();

    public Task ReleaseAsync(ReleaseRequest request, CancellationToken cancellation) => Task.Delay(1200, cancellation);
}
