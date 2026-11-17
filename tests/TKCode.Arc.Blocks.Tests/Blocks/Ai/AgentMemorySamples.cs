namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Invented sample data for <see cref="AgentMemory"/> (the spec's section 10): an assistant called "Orbit", eleven
/// memories in the four default groups, one captured memory, and a capacity that starts at about 58%. Nothing here comes
/// from Arc's preview. Dates are relative to <see cref="Today"/> (the Gallery passes the real today).
/// </summary>
internal static class AgentMemorySamples
{
    /// <summary>The assistant's name in the samples.</summary>
    public const string AssistantName = "Orbit";

    /// <summary>523 characters of samples out of 900: 58%, so two long additions cross the 90% warning.</summary>
    public const int Capacity = 900;

    /// <summary>The fixed "today" of the tests and snapshots.</summary>
    public static readonly DateTimeOffset Today = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The eleven memories, newest first.</summary>
    public static MemoryItem[] Memories(DateTimeOffset? today = null)
    {
        var now = today ?? Today;
        MemoryItem Memory(string id, string group, string text, MemorySourceKind source, int daysAgo, string? person = null) => new()
        {
            Id = id,
            GroupKey = group,
            Text = text,
            SourceKind = source,
            SourceDate = now.AddDays(-daysAgo),
            PersonName = person
        };

        return
        [
            Memory("people-3", "people", "Meets Ingrid Holm for a one-to-one every other Tuesday", MemorySourceKind.Manual, 4, "Ingrid Holm"),
            Memory("projects-2", "projects", "Is drafting a proposal on event sourcing for the billing team", MemorySourceKind.Chat, 5),
            Memory("projects-1", "projects", "Is moving the depot planner to Avalonia this quarter", MemorySourceKind.Chat, 9),
            Memory("preferences-2", "preferences", "Prefers tables to long bullet lists", MemorySourceKind.Chat, 12),
            Memory("people-1", "people", "Mina Okafor reviews every database migration", MemorySourceKind.Chat, 14, "Mina Okafor"),
            Memory("about-3", "about", "Writes most services in C# and some tooling in Go", MemorySourceKind.Manual, 18),
            Memory("people-2", "people", "Pairs with Tomás Ferreira on the routing service", MemorySourceKind.Chat, 21, "Tomás Ferreira"),
            Memory("about-2", "about", "Is training for a spring half marathon", MemorySourceKind.Chat, 25),
            Memory("preferences-3", "preferences", "Uses British spelling in written documents", MemorySourceKind.Chat, 30),
            Memory("preferences-1", "preferences", "Wants a code sample before the explanation", MemorySourceKind.Manual, 35),
            Memory("about-1", "about", "Leads the data platform team at a regional freight company", MemorySourceKind.Chat, 40)
        ];
    }

    /// <summary>The memory the assistant has just noted (it files under Preferences).</summary>
    public static CapturedMemory Captured(DateTimeOffset? today = null) => new()
    {
        Note = "Noted: you run builds with the dotnet CLI, not the IDE",
        Memory = new MemoryItem
        {
            Id = "captured-1",
            GroupKey = "preferences",
            Text = "Runs builds with the dotnet CLI rather than the IDE",
            SourceKind = MemorySourceKind.Chat,
            SourceDate = today ?? Today
        }
    };

    /// <summary>A card with the samples: Orbit, capacity 900, the eleven memories and (optionally) the captured one.</summary>
    public static AgentMemory Create(bool captured = true, DateTimeOffset? today = null)
    {
        var memory = new AgentMemory { AssistantName = AssistantName, Capacity = Capacity };
        foreach (var item in Memories(today))
        {
            memory.Memories.Add(item);
        }
        if (captured)
        {
            memory.Captured.Add(Captured(today));
        }
        return memory;
    }
}
