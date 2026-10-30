namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented workspaces the TeamSwitcher demos simulate, and the two invites their simulated join accepts. Every name
/// here is invented. Each property returns fresh workspaces, so every switcher gets its own.
/// </summary>
internal static class TeamSwitcherDemoData
{
    /// <summary>The plan of a workspace the simulated create makes.</summary>
    public const string CreatedPlan = "Free";

    /// <summary>Your role in a workspace the simulated create makes.</summary>
    public const string CreatedRole = "Owner";

    /// <summary>Six invented workspaces.</summary>
    public static IReadOnlyList<TeamSwitcherWorkspace> Workspaces =>
    [
        new("northwind", "Northwind", "Enterprise", 214, "Admin"),
        new("northwind-labs", "Northwind Labs", "Pro", 12, "Owner"),
        new("contoso", "Contoso", "Business", 86, "Member"),
        new("fabrikam", "Fabrikam", "Pro", 31, "Admin"),
        new("wide-world", "Wide World Importers", "Free", 4, "Owner"),
        new("proseware", "Proseware", "Enterprise", 1280, "Guest"),
    ];

    /// <summary>The invite codes the simulated join accepts, and the workspace each one joins.</summary>
    public static IReadOnlyList<TeamSwitcherSimulationInvite> Invites =>
    [
        new() { Code = "ADATUM-4821", Workspace = new("adatum", "Adatum", "Pro", 18, "Member") },
        new() { Code = "TAILSPIN-0917", Workspace = new("tailspin", "Tailspin Toys", "Business", 57, "Member") },
    ];
}
