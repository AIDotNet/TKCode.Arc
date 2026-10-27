namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The RolesPermissions demos' data (all invented): the Northwind Studio workspace with 31 people, five roles (a locked
/// Founder and four ordinary roles) and four permission groups of 4, 3, 3 and 3 permissions. The saved grants give the
/// Lead almost everything, the Member half of Documents and one Team permission, the Guest one permission and Finance the
/// Payments group, so several group checkboxes start mixed.
/// </summary>
public static class RolesPermissionsDemoData
{
    public const string Workspace = "Northwind Studio";

    public const int MemberCount = 31;

    /// <summary>The five roles, in column order.</summary>
    public static RolesPermissionsRole[] Roles() =>
    [
        new("founder", "Founder", 1, "Lena Hart", isLocked: true),
        new("lead", "Lead", 4, "Omar Diaz, Priya Rao and 2 others"),
        new("member", "Member", 9, "Kai Moreno and 8 others"),
        new("guest", "Guest", 15, "15 people, including 10 contractors"),
        new("finance", "Finance", 2, "Mia Chen and 1 other"),
    ];

    /// <summary>The four permission groups.</summary>
    public static RolesPermissionsGroup[] Groups() =>
    [
        new("documents", "Documents",
            new("doc.read", "Read documents"),
            new("doc.create", "Create documents"),
            new("doc.share", "Change document sharing"),
            new("doc.archive", "Archive documents")),
        new("team", "Team",
            new("team.invite", "Send invitations"),
            new("team.roles", "Assign roles"),
            new("team.remove", "Remove people")),
        new("payments", "Payments",
            new("pay.receipts", "See receipts"),
            new("pay.card", "Update card on file"),
            new("pay.plan", "Switch subscription")),
        new("access", "Access",
            new("acc.saml", "Set up SAML login"),
            new("acc.history", "Read activity history"),
            new("acc.tokens", "Rotate access tokens")),
    ];

    /// <summary>The saved grants (the Founder needs none: a locked role has every permission).</summary>
    public static HashSet<RolesPermissionsGrant> SavedGrants()
    {
        var grants = new HashSet<RolesPermissionsGrant>();
        void Grant(string role, params string[] permissions)
        {
            foreach (var permission in permissions)
            {
                grants.Add(new RolesPermissionsGrant(role, permission));
            }
        }
        Grant("lead", "doc.read", "doc.create", "doc.share", "doc.archive", "team.invite", "team.roles", "team.remove",
            "pay.receipts", "acc.history", "acc.tokens");
        Grant("member", "doc.read", "doc.create", "doc.share", "team.invite");
        Grant("guest", "doc.read");
        Grant("finance", "pay.receipts", "pay.card", "pay.plan");
        return grants;
    }

    /// <summary>The 27 members after the four shown in the header (names only; they fold into "+27").</summary>
    public static IEnumerable<AvatarGroupMember> MoreMembers()
    {
        string[] first = ["Ana", "Ben", "Cleo", "Dev", "Esra", "Finn", "Gia", "Hugo", "Ines", "Jon", "Kira", "Leo", "Maya", "Nico",
            "Oda", "Pia", "Quinn", "Rafi", "Sana", "Theo", "Uma", "Vik", "Wren", "Xia", "Yusuf", "Zoe", "Ari"];
        foreach (var name in first)
        {
            yield return new AvatarGroupMember(name + " Lund");
        }
    }

    /// <summary>Fills a block with the sample workspace.</summary>
    public static void Fill(RolesPermissions block)
    {
        block.WorkspaceName = Workspace;
        block.MemberCount = MemberCount;
        block.Roles.AddRange(Roles());
        block.Groups.AddRange(Groups());
        block.SavedGrants = SavedGrants();
        if (block.Members.Count == 0)
        {
            block.Members.Add(new AvatarGroupMember("Lena Hart"));
            block.Members.Add(new AvatarGroupMember("Omar Diaz"));
            block.Members.Add(new AvatarGroupMember("Priya Rao"));
            block.Members.Add(new AvatarGroupMember("Kai Moreno"));
        }
        block.Members.AddRange(MoreMembers());
    }

    /// <summary>A save that only waits, as a server call would (900 ms, long enough to see the spinner).</summary>
    public static Task SimulatedSave(RolesPermissionsSaveRequest request, CancellationToken cancellation) => Task.Delay(900, cancellation);
}
