namespace TKCode.Arc.Gallery.Demos;

/// <summary>Sample plan for the Gantt timeline demos: a "Studio relaunch" with four phases, written for this gallery.</summary>
internal static class GanttTimelineDemoData
{
    public static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    private static DateOnly D(int offset) => Today.AddDays(offset);

    public static void Fill(GanttTimeline timeline)
    {
        timeline.Title = "Studio relaunch";
        timeline.Today = Today;
        var mira = new ProjectPerson("Mira Okafor", "mira");
        var jon = new ProjectPerson("Jon Reyes", "jon");
        var tali = new ProjectPerson("Tali Brandt", "tali");
        var sev = new ProjectPerson("Sev Lindqvist", "sev");
        timeline.Groups.Clear();
        timeline.Groups.Add(new GanttGroup("plan", "Planning"));
        timeline.Groups.Add(new GanttGroup("make", "Making"));
        timeline.Groups.Add(new GanttGroup("test", "Testing"));
        timeline.Groups.Add(new GanttGroup("ship", "Launch"));
        timeline.Tasks.Clear();
        timeline.Tasks.Add(new GanttTask("brief", "Creative brief", "plan", D(-24), D(-17), 1) { Assignee = mira });
        timeline.Tasks.Add(new GanttTask("scope", "Scope and budget", "plan", D(-16), D(-9), 1) { Assignee = jon, DependsOn = ["brief"] });
        timeline.Tasks.Add(new GanttTask("sign", "Kickoff sign-off", "plan", D(-8), D(-8), 1) { Assignee = mira, IsMilestone = true, DependsOn = ["scope"] });
        timeline.Tasks.Add(new GanttTask("ident", "Identity system", "make", D(-7), D(4), 0.7) { Assignee = tali, DependsOn = ["sign"] });
        timeline.Tasks.Add(new GanttTask("site", "Site build", "make", D(5), D(22), 0.2) { Assignee = sev, DependsOn = ["ident"] });
        timeline.Tasks.Add(new GanttTask("copy", "Copy and photos", "make", D(-6), D(12), 0.35) { Assignee = jon });
        timeline.Tasks.Add(new GanttTask("qa", "Cross-device checks", "test", D(23), D(30), 0) { Assignee = tali, DependsOn = ["site", "copy"] });
        timeline.Tasks.Add(new GanttTask("fix", "Fix pass", "test", D(31), D(36), 0) { Assignee = sev, DependsOn = ["qa"] });
        timeline.Tasks.Add(new GanttTask("go", "Go live", "ship", D(37), D(37), 0) { Assignee = mira, IsMilestone = true, DependsOn = ["fix"] });
        timeline.Tasks.Add(new GanttTask("note", "Announcement", "ship", D(38), D(41), 0) { Assignee = jon, DependsOn = ["go"] });
    }
}
