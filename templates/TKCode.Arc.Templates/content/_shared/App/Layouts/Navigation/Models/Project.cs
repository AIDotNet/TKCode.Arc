using TKCode.Arc;

namespace ArcStarterApp.Models;

/// <summary>One row of the Projects page.</summary>
/// <param name="Name">The project's name.</param>
/// <param name="Team">The team that owns it.</param>
/// <param name="Owner">The person responsible, shown as an avatar.</param>
/// <param name="Status">The status badge's text.</param>
/// <param name="Tone">The status badge's color.</param>
/// <param name="Updated">When it last changed, as shown.</param>
public sealed record Project(string Name, string Team, string Owner, string Status, BadgeTone Tone, string Updated);

/// <summary>Sample data. Replace it with your own source (a database, a web API).</summary>
public static class SampleProjects
{
    public static IReadOnlyList<Project> All { get; } =
    [
        new("Website redesign", "Marketing", "Ada Lovelace", "In review", BadgeTone.Info, "Today"),
        new("Mobile onboarding", "Product", "Alan Turing", "In progress", BadgeTone.Neutral, "Wed"),
        new("Billing migration", "Platform", "Grace Hopper", "Done", BadgeTone.Success, "Last week"),
    ];

    /// <summary>A new project, as the page's "New project" button adds it.</summary>
    public static Project CreateNew(int number) =>
        new($"Untitled project {number}", "Unassigned", "Ada Lovelace", "Draft", BadgeTone.Neutral, "Just now");
}
