namespace TKCode.Arc.Gallery.Demos;

/// <summary>Invented groups and memories for the Agent memory demos. Dates are relative to today.</summary>
internal static class AgentMemoryDemoData
{
    public const string AssistantName = "Orbit";

    /// <summary>A capacity that puts the sample memories at about 58 percent.</summary>
    public static int Capacity => (int)Math.Round(Memories().Sum(m => m.Text.Length) / 0.58 / 10) * 10;

    public static MemoryGroup[] Groups() =>
    [
        new() { Key = "about", Label = "About you", EmptyText = "Facts about you show up here once you mention them." },
        new() { Key = "preferences", Label = "Preferences", EmptyText = "No preferences yet. Tell Orbit how you like answers." },
        new() { Key = "projects", Label = "Projects", EmptyText = "No projects yet. Ongoing work you bring up will be listed here." },
        new() { Key = "people", Label = "People", EmptyText = "No people yet. Colleagues you mention often appear here." }
    ];

    public static MemoryItem[] Memories() =>
    [
        Chat("m1", "about", "Leads the data platform team at a regional freight company", 40),
        Chat("m2", "about", "Is training for a spring half marathon", 25),
        Manual("m3", "about", "Writes most services in C# and some tooling in Go", 18),
        Manual("m4", "preferences", "Wants a code sample before the explanation", 35),
        Chat("m5", "preferences", "Prefers tables to long bullet lists", 12),
        Chat("m6", "preferences", "Uses British spelling in written documents", 30),
        Chat("m7", "projects", "Is moving the depot planner to Avalonia this quarter", 9),
        Chat("m8", "projects", "Is drafting a proposal on event sourcing for the billing team", 5),
        Chat("m9", "people", "Mina Okafor reviews every database migration", 14, "Mina Okafor"),
        Chat("m10", "people", "Pairs with Tomás Ferreira on the routing service", 21, "Tomás Ferreira"),
        Manual("m11", "people", "Meets Ingrid Holm for a one-to-one every other Tuesday", 4, "Ingrid Holm")
    ];

    /// <summary>One memory the assistant has just noted.</summary>
    public static CapturedMemory[] Captured() =>
    [
        new()
        {
            Note = "Noted: you run builds with the dotnet CLI, not the IDE",
            Memory = Chat("c1", "preferences", "Runs builds with the dotnet CLI rather than the IDE", 0)
        }
    ];

    private static MemoryItem Chat(string id, string group, string text, int daysAgo, string? person = null) => new()
    {
        Id = id, GroupKey = group, Text = text, SourceKind = MemorySourceKind.Chat,
        SourceDate = DateTimeOffset.Now.AddDays(-daysAgo), PersonName = person
    };

    private static MemoryItem Manual(string id, string group, string text, int daysAgo, string? person = null) => new()
    {
        Id = id, GroupKey = group, Text = text, SourceKind = MemorySourceKind.Manual,
        SourceDate = DateTimeOffset.Now.AddDays(-daysAgo), PersonName = person
    };
}
