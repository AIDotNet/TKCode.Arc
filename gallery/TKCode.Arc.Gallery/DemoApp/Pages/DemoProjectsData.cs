using System.Globalization;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>A milestone of a project: its label, its date and whether it is done.</summary>
public sealed record DemoMilestone(string Label, DateTime Date, bool IsDone);

/// <summary>What the Projects page knows about a teammate beyond <see cref="DemoPerson"/> (uiarc.dev's studio data).</summary>
public sealed record DemoPersonProfile(string Id, bool IsOnline, int Hours, string Place, int Reviews, string Bio);

/// <summary>
/// A row of the Projects list: one of <see cref="DemoData.Projects"/> with its team, money, summary and milestones. The
/// status is the only thing that changes (Request client review moves it to In review).
/// </summary>
public sealed class DemoProjectRow
{
    public DemoProjectRow(DemoProject project, IReadOnlyList<string> team, decimal budget, decimal spent, string summary,
                          IReadOnlyList<DemoMilestone> milestones)
    {
        Project = project;
        Status = project.Status;
        Team = team;
        Budget = budget;
        Spent = spent;
        Summary = summary;
        Milestones = milestones;
    }

    public DemoProject Project { get; }

    public string Id => Project.Id;

    public string Name => Project.Name;

    public string Client => Project.Client;

    public DemoPerson Lead => DemoData.Person(Project.Lead);

    public DemoProjectStatus Status { get; set; }

    public int Progress => Project.Progress;

    public DateTime Due => Project.Due;

    /// <summary>The due date as uiarc.dev stores it (and sorts it): <c>2026-09-30</c>.</summary>
    public string DueIso => Due.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Person ids, the lead first.</summary>
    public IReadOnlyList<string> Team { get; }

    public decimal Budget { get; }

    public decimal Spent { get; }

    public string Summary { get; }

    public IReadOnlyList<DemoMilestone> Milestones { get; }
}

/// <summary>The Projects page's data: the people's profiles, each project's details, and the Kestrel project and the board.</summary>
public static partial class DemoData
{
    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The lead hover card's profile of each teammate.</summary>
    public static IReadOnlyList<DemoPersonProfile> Profiles { get; } =
    [
        new("emma", true, 36, "Lisbon · 4:10 PM", 24, "Leads product work across the studio and runs the Tuesday critique."),
        new("marcus", true, 43, "Brooklyn · 11:10 AM", 18, "Owns the Northwind onboarding and prototypes every flow before it ships."),
        new("ava", false, 30, "London · 4:10 PM", 11, "Runs discovery and usability studies, and turns interviews into decisions."),
        new("jasmine", true, 38, "Toronto · 11:10 AM", 15, "Builds the Harbor component library and keeps tokens in step with code."),
        new("sofia", false, 16, "Barcelona · 5:10 PM", 9, "Shapes identities from first sketch to the final logo suite."),
        new("ryan", true, 34, "Berlin · 5:10 PM", 7, "Designs the moments between screens: transitions, launches, and loaders."),
        new("olivia", true, 29, "Mexico City · 9:10 AM", 13, "Writes interface copy that sounds like a person, not a policy."),
        new("chloe", false, 39, "Tokyo · 12:10 AM", 16, "Designs dense data tools and audits components for accessibility."),
        new("hannah", true, 32, "Lagos · 4:10 PM", 6, "Keeps timelines honest and clients informed, one weekly note at a time."),
        new("daniel", false, 8, "Milan · 5:10 PM", 5, "Bridges Figma and code with prototypes that feel like the real thing.")
    ];

    public static DemoPersonProfile Profile(string id) => Profiles.First(profile => profile.Id == id);

    /// <summary>The six projects in table order, as fresh rows (each page instance gets its own state).</summary>
    public static List<DemoProjectRow> ProjectRows() =>
    [
        Row("northwind", ["marcus", "olivia", "ryan", "hannah"], 62500, 41200,
            "Onboarding is six days behind plan after the scope change on September 8.",
            [M("Research synthesis", 8, 14, true), M("Information architecture", 8, 28, true), M("Onboarding flows", 9, 25),
             M("Usability test", 9, 28), M("Handoff", 9, 30)]),
        Row("atlas", ["emma", "jasmine", "chloe", "ryan"], 48200, 30100,
            "Checkout flows were approved yesterday. Motion specs are next.",
            [M("Checkout flows", 9, 21, true), M("Motion specs", 9, 29), M("Engineering review", 10, 2), M("Launch assets", 10, 9)]),
        Row("fieldnotes", ["sofia", "ryan", "olivia"], 26400, 22800,
            "The client approved the logo suite. Brand guidelines are in review.",
            [M("Logo suite", 9, 22, true), M("Brand guidelines", 9, 25), M("Final files", 9, 28)]),
        Row("harbor", ["jasmine", "chloe", "daniel", "emma"], 71000, 24600,
            "Tokens 0.9 shipped this morning with dark mode surfaces.",
            [M("Token foundations", 9, 22, true), M("Core components", 10, 16), M("Accessibility audit", 10, 30),
             M("Documentation site", 11, 13)]),
        Row("tidewater", ["ava", "hannah"], 18900, 10400,
            "Six of ten interviews are recorded. Synthesis starts next week.",
            [M("Recruiting", 9, 4, true), M("Interviews", 9, 30), M("Synthesis", 10, 14), M("Readout", 10, 21)]),
        Row("mono", ["chloe", "marcus", "daniel"], 19700, 18900,
            "Launched on September 12. Conversion is up 14 percent in the first week.",
            [M("Product pages", 8, 21, true), M("Checkout", 9, 4, true), M("Launch", 9, 12, true)])
    ];

    public const string KestrelId = "kestrel";

    /// <summary>The project New project adds.</summary>
    public static DemoProjectRow Kestrel() =>
        new(new DemoProject(KestrelId, "Kestrel website", "Kestrel Coffee", "emma", DemoProjectStatus.OnTrack, 0, new DateTime(2026, 12, 4)),
            ["emma", "olivia"], 24000, 0, "Kickoff is booked for Monday. The brief is in the shared folder.",
            [M("Kickoff", 9, 28), M("Site map", 10, 9), M("Visual design", 11, 6), M("Launch", 12, 4)]);

    /// <summary>uiarc.dev's status ids, which the Status column sorts by.</summary>
    public static string StatusId(DemoProjectStatus status) => status switch
    {
        DemoProjectStatus.AtRisk => "at-risk",
        DemoProjectStatus.Review => "review",
        DemoProjectStatus.Shipped => "shipped",
        _ => "on-track"
    };

    /// <summary><c>Sep 30</c>.</summary>
    public static string ShortDate(DateTime date) => date.ToString("MMM d", s_en);

    /// <summary><c>$62,500</c>.</summary>
    public static string Usd(decimal amount) => amount.ToString("C0", s_en);

    private static DemoProjectRow Row(string id, string[] team, decimal budget, decimal spent, string summary, DemoMilestone[] milestones) =>
        new(Projects.First(project => project.Id == id), team, budget, spent, summary, milestones);

    private static DemoMilestone M(string label, int month, int day, bool done = false) => new(label, new DateTime(2026, month, day), done);

    /// <summary>
    /// Fills the Board view's <see cref="ProjectBoard"/> with Arc's defaults: the "Product work" sprint of the block's own
    /// people (Emma, Marcus, Ava; Emma is "me"), six tasks and the four draft titles of Add task.
    /// </summary>
    public static void FillBoard(ProjectBoard board)
    {
        var emma = new ProjectPerson("Emma Collins", "emma-collins");
        var marcus = new ProjectPerson("Marcus Johnson", "marcus-johnson");
        var ava = new ProjectPerson("Ava Mitchell", "ava-mitchell");
        board.Title = "Product work";
        board.ProgressFormat = "{0} of {1} done this sprint";
        board.Stages.AddRange([
            new ProjectBoardStage("planned", "Planned"),
            new ProjectBoardStage("progress", "In progress"),
            new ProjectBoardStage("review", "Review"),
            new ProjectBoardStage("done", "Done", isDone: true)
        ]);
        board.Team.AddRange([emma, marcus, ava]);
        board.CurrentUser = emma;
        board.Tasks.AddRange([
            Task("flows", "Map the first-run journey", "Onboarding", "planned", emma, 3, "Today", soon: true),
            Task("copy", "Write empty state copy", "Onboarding", "planned", ava, 1, "Tomorrow"),
            Task("tokens", "Refine surface tokens", "Design system", "progress", emma, 5, "Thu"),
            Task("motion", "Prototype menu motion", "Design system", "progress", marcus, 2, "Fri"),
            Task("audit", "Accessibility pass", "Onboarding", "review", marcus, 7, "Today", soon: true),
            Task("notes", "Publish research notes", "Research", "done", ava, 2, "Sep 14")
        ]);
        board.DraftTitles.AddRange(["Outline next experiment", "Draft release notes", "Plan usability sessions", "Clean up icon set"]);
        board.DraftArea = "Research";
        board.NoDateText = "No date";
    }

    private static ProjectBoardTask Task(string key, string title, string area, string stage, ProjectPerson owner, int comments,
                                         string due, bool soon = false) =>
        new(title, stage) { Key = key, Area = area, Owner = owner, CommentCount = comments, DueText = due, IsDueSoon = soon };
}
