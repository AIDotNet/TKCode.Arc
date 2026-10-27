using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamMembersBoundDemo : UserControl
{
    private readonly ObservableCollection<TeamMembersPerson> _members = new(TeamMembersDemoData.Members());
    private readonly ObservableCollection<TeamMembersInvite> _invites = new(TeamMembersDemoData.Invites());
    private readonly List<string> _log = new();

    public TeamMembersBoundDemo()
    {
        InitializeComponent();
        Block.Description = TeamMembersDemoData.Description;
        Block.WorkspaceName = TeamMembersDemoData.Workspace;
        Block.SeatLimit = 8;
        // Your own collections: the block adds sent invites to them and removes revoked invites and removed members.
        Block.MembersSource = _members;
        Block.InvitesSource = _invites;
        TeamMembersDemoData.Simulate(Block);

        Block.InvitesSent += (_, e) => Write($"InvitesSent: {string.Join(", ", e.Invites.Select(i => i.Email))} as {e.Role}");
        Block.InviteResent += (_, e) => Write($"InviteResent: {e.Invite.Email}");
        Block.InviteRevoked += (_, e) => Write($"InviteRevoked: {e.Invite.Email}");
        Block.MemberRemoved += (_, e) => Write($"MemberRemoved: {e.Member.Name}");
        Block.MemberRoleChanged += (_, e) => Write($"MemberRoleChanged: {e.Member.Name} {e.OldRole} → {e.NewRole}");
        Block.PropertyChanged += (_, e) =>
        {
            if (e.Property == TeamMembers.SeatsUsedProperty || e.Property == TeamMembers.SeatLimitProperty)
            {
                UpdateSeats();
            }
            else if (e.Property == TeamMembers.SearchTextProperty)
            {
                Write($"SearchText: “{Block.SearchText}”");
            }
        };

        AddSeat.Click += (_, _) => Block.SeatLimit++;
        // An invite accepted elsewhere: the invite leaves the list and a member joins.
        Join.Click += (_, _) =>
        {
            if (_invites.FirstOrDefault(i => i.Email?.StartsWith("owen", StringComparison.Ordinal) == true) is not { } invite)
            {
                return;
            }
            _invites.Remove(invite);
            _members.Add(new TeamMembersPerson { Name = "Owen Park", Email = invite.Email, Role = invite.Role, LastActive = "Joined just now", IsOnline = true });
        };
        Find.Click += (_, _) => Block.SearchText = Block.SearchText == "coastal" ? string.Empty : "coastal";
        UpdateSeats();
    }

    private void UpdateSeats() => Seats.Text = $"{Block.SeatsUsed} of {Block.SeatLimit} seats used, {Block.SeatsRemaining} free";

    private void Write(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 6)
        {
            _log.RemoveAt(_log.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _log);
    }
}
