using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>A sample plan for the timeline: a "Studio relaunch" in four phases, dated around today.</summary>
public static class SampleTimeline
{
    public const string Title = "Studio relaunch";

    private static DateOnly D(int offset) => DateOnly.FromDateTime(DateTime.Today).AddDays(offset);

    public static IReadOnlyList<GanttGroup> Groups() =>
    [
        new("plan", "Planning"),
        new("make", "Making"),
        new("test", "Testing"),
        new("ship", "Launch"),
    ];

    public static IReadOnlyList<GanttTask> Tasks()
    {
        var mira = new ProjectPerson("Mira Okafor", "mira");
        var jon = new ProjectPerson("Jon Reyes", "jon");
        var tali = new ProjectPerson("Tali Brandt", "tali");
        var sev = new ProjectPerson("Sev Lindqvist", "sev");
        return
        [
            new("brief", "Creative brief", "plan", D(-24), D(-17), 1) { Assignee = mira },
            new("scope", "Scope and budget", "plan", D(-16), D(-9), 1) { Assignee = jon, DependsOn = ["brief"] },
            new("sign", "Kickoff sign-off", "plan", D(-8), D(-8), 1) { Assignee = mira, IsMilestone = true, DependsOn = ["scope"] },
            new("ident", "Identity system", "make", D(-7), D(4), 0.7) { Assignee = tali, DependsOn = ["sign"] },
            new("site", "Site build", "make", D(5), D(22), 0.2) { Assignee = sev, DependsOn = ["ident"] },
            new("copy", "Copy and photos", "make", D(-6), D(12), 0.35) { Assignee = jon },
            new("qa", "Cross-device checks", "test", D(23), D(30), 0) { Assignee = tali, DependsOn = ["site", "copy"] },
            new("fix", "Fix pass", "test", D(31), D(36), 0) { Assignee = sev, DependsOn = ["qa"] },
            new("go", "Go live", "ship", D(37), D(37), 0) { Assignee = mira, IsMilestone = true, DependsOn = ["fix"] },
            new("note", "Announcement", "ship", D(38), D(41), 0) { Assignee = jon, DependsOn = ["go"] },
        ];
    }
}
