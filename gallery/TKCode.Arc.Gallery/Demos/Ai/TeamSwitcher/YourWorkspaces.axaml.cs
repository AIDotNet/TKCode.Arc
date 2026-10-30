using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>App-supplied workspaces with delegates that stand in for a real service.</summary>
public partial class TeamSwitcherYourWorkspacesDemo : UserControl
{
    public TeamSwitcherYourWorkspacesDemo()
    {
        InitializeComponent();

        // Loading a workspace: prefetch its routes, swap the session. Keep it short; the trigger shows a loading state.
        Switcher.SwitchHandler = (workspace, token) => Task.Delay(TimeSpan.FromMilliseconds(500), token);

        Switcher.CreateHandler = async (name, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700), token);
            if (name.Length < 3)
            {
                throw new InvalidOperationException("Use at least three characters.");
            }
            return new TeamSwitcherWorkspace("ws-" + Guid.NewGuid().ToString("N")[..8], name, "Free", 1, "Owner");
        };

        Switcher.JoinHandler = async (invite, token) =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(700), token);
            if (!invite.Trim().EndsWith("KESTREL-2026", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("That invite has expired. Ask for a new one.");
            }
            return new TeamSwitcherWorkspace("kestrel", "Kestrel Labs", "Pro", 23, "Member");
        };

        // Keep your own list in sync; the switcher de-duplicates by id.
        Switcher.WorkspaceAdded += (_, e) =>
        {
            Switcher.Workspaces.Add(e.Workspace);
            Log.Text = (e.Kind == TeamSwitcherAddKind.Created ? "Created " : "Joined ") + e.Workspace.Name + ".";
        };
        Switcher.ValueChanged += (_, e) => Log.Text = "Now in " + e.Workspace.Name + " (id: " + e.Id + ").";
    }
}
