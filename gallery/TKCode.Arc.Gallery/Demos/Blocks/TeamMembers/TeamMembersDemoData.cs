using AtomUI.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The TeamMembers demos' data (all invented): the Northwind Lab workspace with ten seats, five members (the owner is the
/// current user, two are online) and two pending invites. Portraits come from the placeholder service other Gallery demos
/// use; without a network the avatars show initials.
/// </summary>
public static class TeamMembersDemoData
{
    public const string Workspace = "Northwind Lab";

    public const string Description = "Choose who can work in the Northwind Lab workspace.";

    public const int SeatLimit = 10;

    /// <summary>The five members, the owner first.</summary>
    public static TeamMembersPerson[] Members() =>
    [
        new() { Id = "lena", Name = "Lena Hart", Email = "lena@northwind.example", Avatar = Photo(47), IsOnline = true, LastActive = "Active now", IsOwner = true, IsCurrentUser = true },
        new() { Id = "theo", Name = "Theo Brandt", Email = "theo@northwind.example", Avatar = Photo(12), IsOnline = true, LastActive = "Active now", Role = "admin" },
        new() { Id = "ines", Name = "Ines Kowal", Email = "ines@northwind.example", Avatar = Photo(45), LastActive = "Active 3 hours ago", Role = "member" },
        new() { Id = "ravi", Name = "Ravi Malhotra", Email = "ravi@northwind.example", Avatar = Photo(60), LastActive = "Active yesterday", Role = "member" },
        new() { Id = "mei", Name = "Mei Tanaka", Email = "mei@northwind.example", Avatar = Photo(32), LastActive = "Active Oct 2", Role = "viewer" },
    ];

    /// <summary>The two pending invites.</summary>
    public static TeamMembersInvite[] Invites() =>
    [
        new() { Id = "inv-owen", Email = "owen.park@northwind.example", Role = "member", Detail = "Invited Oct 7" },
        new() { Id = "inv-sara", Email = "sara.lind@coastal.studio", Role = "viewer", Detail = "Invited Oct 8" },
    ];

    /// <summary>Fills a block with the workspace's people.</summary>
    public static void Fill(TeamMembers block)
    {
        block.Description = Description;
        block.WorkspaceName = Workspace;
        block.SeatLimit = SeatLimit;
        block.Members.AddRange(Members());
        block.Invites.AddRange(Invites());
    }

    /// <summary>Waits like a server would: about 700 ms to send invites and 600 ms to resend one.</summary>
    public static void Simulate(TeamMembers block)
    {
        block.SendInvitesAction = (_, cancellation) => Task.Delay(700, cancellation);
        block.ResendInviteAction = (_, cancellation) => Task.Delay(600, cancellation);
    }

    private static ImageSource? Photo(int index) =>
        ImageSource.TryParse($"https://i.pravatar.cc/160?img={index}", out var source) ? source : null;
}
