namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Project board demos' sprint (all invented): the "Launch prep" board of a three-person team, six tasks spread
/// 2 / 2 / 1 / 1 over four stages, the current user owning one in Planned and one in In progress, and four draft titles
/// for the Add button. Avatars are generated from the names (initials on a soft tint).
/// </summary>
public static class ProjectBoardDemoData
{
    public const string Title = "Launch prep";

    public const string DraftArea = "Insights";

    /// <summary>The draft titles the Add button cycles through.</summary>
    public static readonly string[] DraftTitles =
    [
        "Map churn interview themes",
        "Draft the pricing FAQ",
        "Plan the beta feedback call",
        "Tidy the empty-state icons",
    ];

    /// <summary>Rhea, Tomas and Ines; Rhea is the current user.</summary>
    public static (ProjectPerson Rhea, ProjectPerson Tomas, ProjectPerson Ines) CreateTeam() =>
        (new ProjectPerson("Rhea Calloway", "rhea"), new ProjectPerson("Tomas Vierra", "tomas"), new ProjectPerson("Ines Okafor", "ines"));

    /// <summary>Planned, In progress, Review and Done (the last one finishes a task).</summary>
    public static List<ProjectBoardStage> CreateStages() =>
    [
        new("planned", "Planned"),
        new("progress", "In progress"),
        new("review", "Review"),
        new("done", "Done", isDone: true),
    ];

    /// <summary>The six tasks of the sprint.</summary>
    public static List<ProjectBoardTask> CreateTasks(ProjectPerson rhea, ProjectPerson tomas, ProjectPerson ines) =>
    [
        Task("Sketch the coupon entry flow", "planned", "Checkout", "Today", rhea, 4, soon: true),
        Task("Write error copy for card declines", "planned", "Checkout", "Tomorrow", ines, 2),
        Task("Split the settings API", "progress", "Platform", "Wed", rhea, 6),
        Task("Add retry to webhook sender", "progress", "Platform", "Fri", tomas, 1),
        Task("Contrast audit for receipts", "review", "Checkout", "Today", tomas, 8, soon: true),
        Task("Ship the weekly digest email", "done", "Insights", "Oct 3", ines, 3),
    ];

    /// <summary>Fills <paramref name="board"/> with the sprint: title, stages, team, current user, tasks and drafts.</summary>
    public static void Fill(ProjectBoard board)
    {
        var (rhea, tomas, ines) = CreateTeam();
        board.Title = Title;
        board.Stages.AddRange(CreateStages());
        board.Team.AddRange([rhea, tomas, ines]);
        board.CurrentUser = rhea;
        board.Tasks.AddRange(CreateTasks(rhea, tomas, ines));
        board.DraftTitles.AddRange(DraftTitles);
        board.DraftArea = DraftArea;
    }

    private static ProjectBoardTask Task(string title, string stage, string area, string due, ProjectPerson owner, int comments, bool soon = false) =>
        new(title, stage)
        {
            Area = area,
            DueText = due,
            IsDueSoon = soon,
            Owner = owner,
            CommentCount = comments,
        };
}
