using Avalonia.Layout;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/team: Arc's TeamMembers block (row "members") over its RolesPermissions block (row "roles"), each with the
/// default data and wording Arc renders them with. No toolbar.
/// </summary>
public sealed class DemoTeamPage : DemoPageView
{
    public DemoTeamPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        AddRow(DemoRowLayout.Stack, "members", BuildMembers());
        AddRow(DemoRowLayout.Stack, "roles", BuildRoles());
    }

    // ---- Members -----------------------------------------------------------------------------------------------------

    private const string Workspace = "Fieldnote";

    private const int SeatLimit = 10;

    /// <summary>"an admin", "a member": the role name as the status line says it.</summary>
    private static string WithArticle(string role) =>
        role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "an admin" : "a " + role.ToLowerInvariant();

    private static TeamMembers BuildMembers()
    {
        var members = new TeamMembers
        {
            WorkspaceName = Workspace,
            Title = "Members",
            Description = $"Manage who can work in the {Workspace} workspace.",
            SeatLimit = SeatLimit,
            InviteRole = "member",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // Arc's demo has no server: sending takes 700 ms and a resend 600 ms, so the loading states show.
            SendInvitesAction = (_, token) => Task.Delay(700, token),
            ResendInviteAction = (_, token) => Task.Delay(600, token),
            Labels = new TeamMembersLabels
            {
                InviteHeading = "Invite people",
                Placeholder = "name@example.com",
                Hint = "Separate addresses with commas or press Enter. Invites expire in 7 days",
                InviteAsName = "Invite as",
                SendOne = "Send invite",
                SendMany = count => $"Send {count} invites",
                MembersHeading = "Members",
                PendingHeading = "Pending invites",
                SearchPlaceholder = "Search by name or email",
                SearchName = "Search people",
                Owner = "Owner",
                You = "You",
                SeatsOf = limit => $"of {limit} seats",
                SeatsValue = (used, limit) => $"{used} of {limit} seats used",
                SeatsName = "Seats used",
                Invalid = text => $"“{text}” is not a complete email address",
                AlreadyMember = name => $"{name} is already a member",
                AlreadyInvited = email => $"{email} already has a pending invite",
                OverLimit = (left, extra) =>
                    $"{left} {(left == 1 ? "seat" : "seats")} left. Remove {extra} {(extra == 1 ? "address" : "addresses")} to send",
                Full = limit => $"All {limit} seats are taken",
                FullWithChips = limit => $"All {limit} seats are taken. Remove someone or revoke an invite first",
                NoMembersMatch = query => $"No members match “{query}”",
                ClearSearch = "Clear search",
                NoInvitesMatch = "No invites match your search",
                NoInvites = "No pending invites",
                RemoveTitle = name => $"Remove {name}?",
                RemoveDetail = (_, workspace) => $"They lose access to {workspace ?? Workspace} right away",
                Cancel = "Cancel",
                Remove = "Remove",
                Resend = "Resend",
                Sent = "Sent",
                Revoke = "Revoke",
                InviteDetail = (role, detail) => string.IsNullOrWhiteSpace(detail) ? role : $"{role} · {detail}",
                InvitedJustNow = "Invited just now",
                ResentJustNow = "Resent just now",
                StatusIdle = "Changes apply right away",
                StatusInvitedOne = (email, role) => $"Invited {email} as {WithArticle(role)}",
                StatusInvitedMany = (count, role) => $"Invited {count} people as {role.ToLowerInvariant()}s",
                StatusChipRemoved = email => $"Removed {email} from the invite",
                StatusRevoked = email => $"Invite to {email} revoked",
                StatusResent = email => $"Invite resent to {email}",
                StatusMemberRemoved = (name, workspace) => $"{name} was removed from {workspace ?? Workspace}",
                StatusRoleChanged = (name, role) => $"{name} is now {WithArticle(role)}",
                RoleFor = name => $"Role for {name}",
                RemoveName = name => $"Remove {name}",
                RemoveChipName = email => $"Remove {email}"
            }
        };
        members.Roles.AddRange(
        [
            new TeamMembersRoleOption("admin", "Admin", "Manages members, billing, and settings"),
            new TeamMembersRoleOption("member", "Member", "Creates and edits projects"),
            new TeamMembersRoleOption("viewer", "Viewer", "Views and comments, no editing")
        ]);
        members.Members.AddRange(
        [
            Person("emma", "Emma Collins", "owner", "Active now", online: true, owner: true),
            Person("ava", "Ava Mitchell", "admin", "Active now", online: true),
            Person("marcus", "Marcus Johnson", "member", "Active 2 hours ago"),
            Person("jasmine", "Jasmine Brooks", "member", "Active yesterday"),
            Person("sofia", "Sofia Ramirez", "viewer", "Active Sep 14")
        ]);
        members.Invites.AddRange(
        [
            new TeamMembersInvite { Id = "jordan", Email = "jordan.blake@fieldnote.example", Role = "member", Detail = "Invited Sep 19", Initials = "JB" },
            new TeamMembersInvite { Id = "priya", Email = "priya.shah@harborline.studio", Role = "viewer", Detail = "Invited Sep 20", Initials = "PS" }
        ]);
        return members;
    }

    private static TeamMembersPerson Person(string id, string name, string role, string lastActive, bool online = false, bool owner = false) => new()
    {
        Id = id,
        Name = name,
        Email = $"{id}@fieldnote.example",
        Role = role,
        LastActive = lastActive,
        IsOnline = online,
        IsOwner = owner,
        IsCurrentUser = owner
    };

    // ---- Roles and permissions ---------------------------------------------------------------------------------------

    private static RolesPermissions BuildRoles()
    {
        var roles = new RolesPermissions
        {
            Title = "Roles and permissions",
            WorkspaceName = "Kestrel Analytics",
            MemberCount = 28,
            IsRoleMenuEnabled = true,
            CanDuplicateRoles = true,
            IsFooterVisible = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            // No server either: saving takes 900 ms and the "saved" bar stays 1.6 s.
            SaveHandler = (_, token) => Task.Delay(900, token),
            SavedHoldDuration = TimeSpan.FromSeconds(1.6),
            SavedGrants = SavedGrants(),
            Labels = new RolesPermissionsLabels
            {
                PermissionHeader = "Permission",
                SubtitleFormat = "{0} · {1} members across {2} roles",
                SubtitleNoWorkspaceFormat = "{0} members across {1} roles",
                MembersName = "Workspace members",
                PersonFormat = "{0} person",
                PeopleFormat = "{0} people",
                NewRole = "New role",
                NotSavedYet = "Not saved yet",
                CopyRole = "Duplicate role",
                UndoEdits = "Revert changes",
                RemoveRole = "Delete role",
                CopyNameFormat = "{0} copy",
                CopyNameNumberedFormat = "{0} copy {1}",
                LockedLegend = "Owners always have every permission",
                UnsavedLegend = "Unsaved change",
                PendingWord = "unsaved",
                ChangeOne = "change",
                ChangeMany = "changes",
                TwoRolesFormat = "{0} and {1}",
                ThreeRolesFormat = "{0}, {1} and {2}",
                ManyRolesFormat = "{0}, {1} and {2} more",
                ReachFormat = "{0} · affects {1} people",
                ReachOneFormat = "{0} · affects 1 person",
                NoMembersFormat = "{0} · no members yet",
                SavedTitle = "Permissions saved",
                SavedText = "Saved",
                SavedButton = "Saved",
                Discard = "Discard",
                Save = "Save changes",
                ReviewBarName = "Unsaved permission changes",
                GroupCellFormat = "{0}: all {1} permissions",
                CellFormat = "{0}: {1}",
                AlwaysOnSuffix = ", always on",
                UnsavedSuffix = ", unsaved",
                GrantedFormat = "{0} can now {1}",
                RevokedFormat = "{0} can no longer {1}",
                GroupGrantedFormat = "Turned on all {1} permissions for {0}",
                GroupRevokedFormat = "Turned off all {1} permissions for {0}",
                LockedFormat = "{0} always has every permission",
                DuplicatedFormat = "Duplicated {0} as {1}, not saved yet",
                RemovedFormat = "Removed {0}",
                RevertedFormat = "Reverted changes to {0}",
                DiscardedFormat = "Discarded {0}",
                SavingFormat = "Saving {0}",
                SavedFormat = "Saved {0}."
            }
        };
        roles.Roles.AddRange(
        [
            new RolesPermissionsRole("owner", "Owner", 1, "Emma Collins", isLocked: true),
            new RolesPermissionsRole("admin", "Admin", 3, "Jasmine Brooks, Ava Mitchell and 1 other"),
            new RolesPermissionsRole("editor", "Editor", 8, "Marcus Johnson and 7 others"),
            new RolesPermissionsRole("viewer", "Viewer", 14, "14 people, including 9 guests"),
            new RolesPermissionsRole("billing", "Billing", 2, "Sofia Ramirez and 1 other")
        ]);
        roles.Groups.AddRange(
        [
            new RolesPermissionsGroup("projects", "Projects",
                new("projects.view", "View projects"),
                new("projects.create", "Create projects"),
                new("projects.edit", "Edit project settings"),
                new("projects.delete", "Delete projects")),
            new RolesPermissionsGroup("members", "Members",
                new("members.invite", "Invite members"),
                new("members.roles", "Change member roles"),
                new("members.remove", "Remove members")),
            new RolesPermissionsGroup("billing", "Billing",
                new("billing.invoices", "View invoices"),
                new("billing.payment", "Manage payment methods"),
                new("billing.plan", "Change plan")),
            new RolesPermissionsGroup("security", "Security",
                new("security.sso", "Configure single sign-on"),
                new("security.audit", "View audit log"),
                new("security.keys", "Manage API keys"))
        ]);
        foreach (var name in new[] { "Emma Collins", "Jasmine Brooks", "Ava Mitchell", "Marcus Johnson", "Sofia Ramirez" })
        {
            roles.Members.Add(new AvatarGroupMember(name));
        }
        for (var i = 6; i <= 28; i++)
        {
            roles.Members.Add(new AvatarGroupMember($"Workspace member {i}"));
        }
        return roles;
    }

    /// <summary>The saved grants of the four roles that are not locked (the Owner has every permission).</summary>
    private static HashSet<RolesPermissionsGrant> SavedGrants()
    {
        var grants = new HashSet<RolesPermissionsGrant>();
        void Grant(string role, params string[] permissions)
        {
            foreach (var permission in permissions)
            {
                grants.Add(new RolesPermissionsGrant(role, permission));
            }
        }
        Grant("owner", "projects.view", "projects.create", "projects.edit", "projects.delete", "members.invite", "members.roles",
            "members.remove", "billing.invoices", "billing.payment", "billing.plan", "security.sso", "security.audit", "security.keys");
        Grant("admin", "projects.view", "projects.create", "projects.edit", "projects.delete", "members.invite", "members.roles",
            "members.remove", "billing.invoices", "security.audit", "security.keys");
        Grant("editor", "projects.view", "projects.create", "projects.edit", "members.invite");
        Grant("viewer", "projects.view");
        Grant("billing", "billing.invoices", "billing.payment", "billing.plan");
        return grants;
    }
}
