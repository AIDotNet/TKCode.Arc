using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class InvitePeopleEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();

    public InvitePeopleEventLogDemo()
    {
        InitializeComponent();
        InvitePeopleDemoData.Fill(Block);
        Block.SampleText = null;
        _ = new InvitePeopleSimulation(Block) { AcceptsOnce = false };

        Block.EntriesChanged += (_, e) => Write($"Chips: {e.Entries.Count} ({e.ValidCount} can be invited)");
        Block.RoleChanged += (_, e) => Write($"{e.Entry.Text}: {e.OldRole?.Name} → {e.NewRole?.Name}");
        Block.InvitesSent += (_, e) => Write($"Sent {e.Request.Invites.Count} (attempt {e.Request.Attempt})");
        Block.InviteResent += (_, e) => Write($"Resent to {e.Invite.Person?.DisplayName}");
        Block.InviteRevoked += (_, e) => Write($"Revoked {e.Invite.Person?.DisplayName}");
        Block.PlanUpgraded += (_, e) => Write($"Plan: {e.OldPlan?.Name} → {e.NewPlan.Name}");
    }

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join("\n", _lines);
    }
}
