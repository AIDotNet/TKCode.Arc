using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every piece of text a <see cref="SemanticZoom"/> shows or announces, with English defaults. Format strings use
/// <see cref="string.Format(IFormatProvider, string, object[])"/> placeholders as documented on each property. Assign a new
/// instance to <see cref="SemanticZoom.Texts"/> to change them (the control reads them when it next draws or announces).
/// </summary>
public sealed class SemanticZoomTexts
{
    /// <summary>The English defaults (shared; do not change it, assign a new instance instead).</summary>
    public static SemanticZoomTexts Default { get; } = new();

    // Kinds and panel labels -------------------------------------------------------------------------------------------

    /// <summary>The panel's kind label for the organization.</summary>
    public string OrganizationKind { get; set; } = "Company";

    /// <summary>The panel's kind label for a team.</summary>
    public string TeamKind { get; set; } = "Team";

    /// <summary>The panel's kind label for a project.</summary>
    public string ProjectKind { get; set; } = "Project";

    /// <summary>The panel's kind label for a task.</summary>
    public string TaskKind { get; set; } = "Task";

    /// <summary>Panel label of the open task count.</summary>
    public string OpenTasksLabel { get; set; } = "Open tasks";

    /// <summary>Panel label of the blocked task count.</summary>
    public string BlockedLabel { get; set; } = "Blocked";

    /// <summary>Panel label of the next due date.</summary>
    public string NextDueLabel { get; set; } = "Next due";

    /// <summary>Panel label of the progress percentage.</summary>
    public string ProgressLabel { get; set; } = "Progress";

    /// <summary>Panel label of the progress percentage when the scope is a task.</summary>
    public string ChecklistProgressLabel { get; set; } = "Checklist complete";

    /// <summary>Panel label of the people count.</summary>
    public string PeopleLabel { get; set; } = "People";

    /// <summary>Shown when nothing is due.</summary>
    public string NoneText { get; set; } = "None";

    /// <summary>Panel note for the organization and a team: {0} projects, {1} not on track.</summary>
    public string ProjectsNoteFormat { get; set; } = "{0} projects, {1} needing attention";

    /// <summary>Panel note for a project: {0} lead.</summary>
    public string ProjectNoteFormat { get; set; } = "Lead: {0}";

    /// <summary>Panel note for a task: {0} assignee.</summary>
    public string TaskNoteFormat { get; set; } = "Owner: {0}";

    /// <summary>Accessible name of the panel.</summary>
    public string PanelLabel { get; set; } = "Numbers for this view";

    /// <summary>"+N" chip after the visible avatars: {0} hidden people.</summary>
    public string MorePeopleFormat { get; set; } = "+{0}";

    // Statuses ---------------------------------------------------------------------------------------------------------

    /// <summary>Status name of a task not started yet.</summary>
    public string StatusTodo { get; set; } = "To do";

    /// <summary>Status name of a task in progress.</summary>
    public string StatusInProgress { get; set; } = "In progress";

    /// <summary>Status name of a task in review.</summary>
    public string StatusInReview { get; set; } = "In review";

    /// <summary>Status name of a finished task.</summary>
    public string StatusDone { get; set; } = "Done";

    /// <summary>Status name of a blocked task.</summary>
    public string StatusBlocked { get; set; } = "Blocked";

    /// <summary>Health name of a project on track.</summary>
    public string HealthOnTrack { get; set; } = "On track";

    /// <summary>Health name of a project at risk.</summary>
    public string HealthAtRisk { get; set; } = "At risk";

    /// <summary>Health name of a project off track.</summary>
    public string HealthOffTrack { get; set; } = "Off track";

    // Canvas -----------------------------------------------------------------------------------------------------------

    /// <summary>Team header detail line: {0} lead, {1} projects, {2} open tasks.</summary>
    public string TeamLineFormat { get; set; } = "{0} · {1} projects · {2} open tasks";

    /// <summary>Project status line: {0} health, {1} due date.</summary>
    public string ProjectStatusFormat { get; set; } = "{0} · Due {1}";

    /// <summary>Project status line without a due date: {0} health.</summary>
    public string ProjectStatusNoDueFormat { get; set; } = "{0}";

    /// <summary>Project people line with one person: {0} name.</summary>
    public string PeopleOneFormat { get; set; } = "{0}";

    /// <summary>Project people line with two people: {0} first name, {1} = 1.</summary>
    public string PeopleTwoFormat { get; set; } = "{0} and 1 other";

    /// <summary>Project people line with more people: {0} first name, {1} others.</summary>
    public string PeopleManyFormat { get; set; } = "{0} and {1} others";

    /// <summary>Task block corner: {0} ticked, {1} total, {2} due date.</summary>
    public string TaskMetaFormat { get; set; } = "{0}/{1} · {2}";

    /// <summary>Task block corner without a due date: {0} ticked, {1} total.</summary>
    public string TaskMetaNoDueFormat { get; set; } = "{0}/{1}";

    /// <summary>Task details, after the status: {0} due date, {1} points text.</summary>
    public string TaskDetailFormat { get; set; } = "· Due {0} · {1}";

    /// <summary>Task details without a due date: {0} points text.</summary>
    public string TaskDetailNoDueFormat { get; set; } = "· {0}";

    /// <summary>One point: {0}.</summary>
    public string PointFormat { get; set; } = "{0} point";

    /// <summary>Several points: {0}.</summary>
    public string PointsFormat { get; set; } = "{0} points";

    /// <summary>One comment: {0}.</summary>
    public string CommentFormat { get; set; } = "{0} comment";

    /// <summary>Several comments (and none): {0}.</summary>
    public string CommentsFormat { get; set; } = "{0} comments";

    /// <summary>Task details: project and team, {0} project, {1} team.</summary>
    public string TaskPlaceFormat { get; set; } = "{0} · {1}";

    /// <summary>Checklist heading in a task's details.</summary>
    public string ChecklistLabel { get; set; } = "Checklist";

    /// <summary>"x of y" counts: {0} done, {1} total.</summary>
    public string CountOfFormat { get; set; } = "{0} of {1}";

    // Generated task summaries ({0} assignee first name, {1} due date, {2} project, {3} project description, {4} reviewer) --

    /// <summary>Summary of a done task.</summary>
    public string SummaryDoneFormat { get; set; } = "Finished by {0} on {1}. {2}: {3}";

    /// <summary>Summary of a task in progress.</summary>
    public string SummaryInProgressFormat { get; set; } = "{0} is working on it and aims to wrap up by {1}. {2}: {3}";

    /// <summary>Summary of a task in review.</summary>
    public string SummaryInReviewFormat { get; set; } = "{0} handed it to {4} for review. {2}: {3}";

    /// <summary>Summary of a task not started yet.</summary>
    public string SummaryTodoFormat { get; set; } = "Not started yet. {0} plans to begin before {1}. {2}: {3}";

    /// <summary>Summary of a blocked task.</summary>
    public string SummaryBlockedFormat { get; set; } = "Waiting on another team. {0} needs it unblocked before {1}. {2}: {3}";

    /// <summary>Stands in for a missing person in generated text.</summary>
    public string NobodyText { get; set; } = "Nobody";

    // Hover card -------------------------------------------------------------------------------------------------------

    /// <summary>Hover card row label: the lead.</summary>
    public string CardLeadLabel { get; set; } = "Lead";

    /// <summary>Hover card row label: project count.</summary>
    public string CardProjectsLabel { get; set; } = "Projects";

    /// <summary>Hover card note after the project count: {0} projects not on track.</summary>
    public string CardAttentionFormat { get; set; } = "· {0} need attention";

    /// <summary>Hover card row label: open tasks.</summary>
    public string CardOpenTasksLabel { get; set; } = "Open tasks";

    /// <summary>Hover card row label: progress.</summary>
    public string CardProgressLabel { get; set; } = "Progress";

    /// <summary>Hover card row label: finished tasks ("5 of 7").</summary>
    public string CardTasksDoneLabel { get; set; } = "Tasks done";

    /// <summary>Hover card row label: blocked tasks.</summary>
    public string CardBlockedLabel { get; set; } = "Blocked";

    /// <summary>Hover card row label: assignee.</summary>
    public string CardAssigneeLabel { get; set; } = "Owner";

    /// <summary>Hover card row label: due date.</summary>
    public string CardDueLabel { get; set; } = "Due";

    /// <summary>Hover card row label: checklist ("2 of 3").</summary>
    public string CardChecklistLabel { get; set; } = "Checklist";

    /// <summary>Hover card row label: estimate.</summary>
    public string CardEstimateLabel { get; set; } = "Estimate";

    /// <summary>Hover card subtitle of a task: {0} status, {1} project.</summary>
    public string CardTaskSubtitleFormat { get; set; } = "{0} · {1}";

    /// <summary>Hover card footer of a team.</summary>
    public string CardTeamHint { get; set; } = "Click to zoom into this team";

    /// <summary>Hover card footer of a project.</summary>
    public string CardProjectHint { get; set; } = "Click to see its tasks";

    /// <summary>Hover card footer of a task.</summary>
    public string CardTaskHint { get; set; } = "Click to open this task";

    // Overlay controls -------------------------------------------------------------------------------------------------

    /// <summary>Tour button while stopped.</summary>
    public string StartTour { get; set; } = "Start the tour";

    /// <summary>Tour button while playing.</summary>
    public string StopTour { get; set; } = "Stop tour";

    /// <summary>Zoom out button name.</summary>
    public string ZoomOut { get; set; } = "Zoom out";

    /// <summary>Zoom in button name.</summary>
    public string ZoomIn { get; set; } = "Zoom in";

    /// <summary>Fit button name: {0} organization.</summary>
    public string FitFormat { get; set; } = "Show all of {0}";

    /// <summary>Hint at the bottom left, for mouse and trackpad.</summary>
    public string PointerHint { get; set; } = "Scroll to zoom, drag to move, click a card to fly in";

    /// <summary>Hint at the bottom left on touch devices.</summary>
    public string TouchHint { get; set; } = "Pinch to zoom, tap a card to fly in";

    /// <summary>Accessible name of the breadcrumbs.</summary>
    public string BreadcrumbLabel { get; set; } = "Place on the map";

    /// <summary>Accessible name of the minimap.</summary>
    public string MinimapLabel { get; set; } = "Overview of the map";

    /// <summary>Accessible role description of the stage.</summary>
    public string StageRole { get; set; } = "zoomable map";

    /// <summary>Accessible name of the stage: {0} organization, {1} teams, {2} projects, {3} tasks, {4} current scope.</summary>
    public string StageLabelFormat { get; set; } =
        "Map of {0}: {1} teams, {2} projects, {3} tasks. Showing {4}. Zoom in for projects, tasks and people.";

    /// <summary>Keyboard help of the stage.</summary>
    public string StageHelp { get; set; } =
        "Arrow keys move between the items in view, Enter zooms in or ticks a checklist item, Escape zooms out, plus and minus zoom, 0 shows everything.";

    /// <summary>Caption of the hidden project table: {0} organization.</summary>
    public string TableCaptionFormat { get; set; } = "Projects at {0}, team by team";

    /// <summary>One row of the hidden project table: {0} project, {1} team, {2} health, {3} progress, {4} open, {5} blocked, {6} due.</summary>
    public string TableRowFormat { get; set; } = "{0}, {1} team, {2}, {3} complete, {4} open, {5} blocked, due {6}";

    // Announcements ----------------------------------------------------------------------------------------------------

    /// <summary>Organization summary: {0} name, {1} teams, {2} projects, {3} open tasks, {4} percent.</summary>
    public string AnnounceOrganizationFormat { get; set; } = "{0}: {1} teams, {2} projects, {3} open tasks, {4} done";

    /// <summary>Team summary: {0} name, {1} projects, {2} open tasks, {3} percent.</summary>
    public string AnnounceTeamFormat { get; set; } = "{0} team: {1} projects, {2} open tasks, {3} done";

    /// <summary>Project summary: {0} name, {1} health, {2} percent, {3} open tasks, {4} due date.</summary>
    public string AnnounceProjectFormat { get; set; } = "{0}: {1}, {2} done, {3} open tasks, due {4}";

    /// <summary>Task summary: {0} title, {1} status, {2} assignee, {3} due date, {4} ticked, {5} total.</summary>
    public string AnnounceTaskFormat { get; set; } = "{0}: {1}, owner {2}, due {3}, {4} of {5} checklist items ticked";

    /// <summary>After a scope change: {0} summary.</summary>
    public string AnnounceScopeFormat { get; set; } = "Now showing {0}.";

    /// <summary>After a cursor move: {0} summary.</summary>
    public string AnnounceCursorFormat { get; set; } = "{0}. Press Enter to zoom in.";

    /// <summary>Cursor on a checklist item: {0} text, {1} state.</summary>
    public string AnnounceItemFormat { get; set; } = "{0}, {1}. Press Enter to tick or untick.";

    /// <summary>A checklist item was ticked: {0} text, {1} task, {2} ticked, {3} total.</summary>
    public string AnnounceTickedFormat { get; set; } = "{0} ticked. {1}: {2} of {3} done.";

    /// <summary>A checklist item was unticked: {0} text, {1} task, {2} ticked, {3} total, {4} new status (lower case).</summary>
    public string AnnounceUntickedFormat { get; set; } = "{0} unticked. {1}: {2} of {3} done, now {4}.";

    /// <summary>State word of a ticked item.</summary>
    public string TickedText { get; set; } = "ticked";

    /// <summary>State word of an open item.</summary>
    public string NotTickedText { get; set; } = "not ticked";

    /// <summary>Tour started.</summary>
    public string AnnounceTourStarted { get; set; } = "Tour started.";

    /// <summary>Tour stopped.</summary>
    public string AnnounceTourStopped { get; set; } = "Tour stopped.";

    // Helpers ----------------------------------------------------------------------------------------------------------

    internal string KindName(SemanticZoomLevel level) => level switch
    {
        SemanticZoomLevel.Team => TeamKind,
        SemanticZoomLevel.Project => ProjectKind,
        SemanticZoomLevel.Task => TaskKind,
        _ => OrganizationKind
    };

    internal string StatusName(SemanticZoomTaskStatus status) => status switch
    {
        SemanticZoomTaskStatus.InProgress => StatusInProgress,
        SemanticZoomTaskStatus.InReview => StatusInReview,
        SemanticZoomTaskStatus.Done => StatusDone,
        SemanticZoomTaskStatus.Blocked => StatusBlocked,
        _ => StatusTodo
    };

    internal string HealthName(SemanticZoomHealth health) => health switch
    {
        SemanticZoomHealth.AtRisk => HealthAtRisk,
        SemanticZoomHealth.OffTrack => HealthOffTrack,
        _ => HealthOnTrack
    };

    internal static string Format(string format, params object?[] args)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }
}

/// <summary>Formats the dates and percentages a <see cref="SemanticZoom"/> shows. Implement it to change their look.</summary>
public interface ISemanticZoomFormatter
{
    /// <summary>A due date ("Sep 23").</summary>
    string FormatDate(DateOnly date);

    /// <summary>A fraction 0..1 as a percentage ("59%").</summary>
    string FormatPercent(double fraction);
}

/// <summary>The default <see cref="ISemanticZoomFormatter"/>: short month and day ("Sep 23"), whole percentages ("59%").</summary>
public class SemanticZoomFormatter : ISemanticZoomFormatter
{
    /// <summary>The shared default instance.</summary>
    public static SemanticZoomFormatter Default { get; } = new();

    /// <summary>Culture of month names and digits (default en-US).</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");

    /// <inheritdoc />
    public virtual string FormatDate(DateOnly date) => date.ToString("MMM d", Culture);

    /// <inheritdoc />
    public virtual string FormatPercent(double fraction) => ShowpieceFormat.Percent(fraction, 0, false, Culture);
}
