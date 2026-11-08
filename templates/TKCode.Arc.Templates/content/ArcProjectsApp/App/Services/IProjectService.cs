using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>
/// The project's data, in the types of Arc's blocks (KanbanBoard, GanttTimeline, WeekCalendar, ReleaseReadiness).
/// SampleData/SampleProjectService.cs makes it up; replace it with your tracker's API and register yours in
/// Services/AppServices.cs. The pages save what the blocks change through the Save methods.
/// </summary>
public interface IProjectService
{
    string ProjectName { get; }

    IReadOnlyList<ProjectPerson> GetPeople();

    IReadOnlyList<KanbanColumn> GetColumns();

    IReadOnlyList<KanbanLabel> GetLabels();

    IReadOnlyList<KanbanCard> GetCards();

    /// <summary>Called after a card was moved, added or edited on the board.</summary>
    void SaveCards(IReadOnlyList<KanbanCard> cards);

    IReadOnlyList<GanttGroup> GetTimelineGroups();

    IReadOnlyList<GanttTask> GetTimelineTasks();

    /// <summary>Called after a task was moved or resized on the timeline.</summary>
    void SaveTimeline(IReadOnlyList<GanttTask> tasks);

    IReadOnlyList<WeekCalendarSource> GetCalendars();

    IReadOnlyList<WeekCalendarEvent> GetEvents(DateOnly monday);

    IReadOnlyList<ReleaseCheck> GetReleaseChecks();

    IReadOnlyList<ProjectPerson> GetReleaseOwners();

    /// <summary>Starts the release; throw to show the failure in the block.</summary>
    Task ReleaseAsync(ReleaseRequest request, CancellationToken cancellation);
}
