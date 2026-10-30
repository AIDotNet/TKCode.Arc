namespace TKCode.Arc;

/// <summary>An invite the simulated join of a <see cref="TeamSwitcherSimulation"/> accepts, and the workspace it joins.</summary>
public sealed class TeamSwitcherSimulationInvite
{
    /// <summary>The code, such as "ABCD-1234"; it matches ignoring case, on its own or as the last part of a pasted link.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>The workspace the code joins.</summary>
    public TeamSwitcherWorkspace? Workspace { get; set; }
}

/// <summary>
/// Simulated workspaces for a <see cref="TeamSwitcher"/> that has no <see cref="TeamSwitcher.Workspaces"/>, for demos and
/// prototypes: the list it shows, the invites its simulated join accepts (the join view notes them) and what its simulated
/// create makes. The app's own workspaces set it aside entirely. Switching, creating and joining without the app's delegates take <see cref="TeamSwitcher.SimulatedDelay"/>
/// and are announced as simulated; nothing is sent. Create one per switcher: workspaces created or joined are added to that
/// switcher.
/// </summary>
public sealed class TeamSwitcherSimulation
{
    /// <summary>The workspaces the switcher shows, in order (the first nine get Cmd/Ctrl+1 to 9).</summary>
    public IReadOnlyList<TeamSwitcherWorkspace> Workspaces { get; set; } = [];

    /// <summary>The invites the simulated join accepts.</summary>
    public IReadOnlyList<TeamSwitcherSimulationInvite> Invites { get; set; } = [];

    /// <summary>The plan of a workspace the simulated create makes, such as "Free"; null shows none.</summary>
    public string? CreatedPlan { get; set; }

    /// <summary>Your role in a workspace the simulated create makes, such as "Owner"; null shows none.</summary>
    public string? CreatedRole { get; set; }
}
