using TKCode.Arc;

namespace ArcStarterApp.Services;

/// <summary>
/// The workspace: its people, roles and permissions, and its billing. The Team and Billing pages show Arc's blocks
/// (TeamMembers, RolesPermissions, BillingOverview), so the data is in the blocks' own types. Replace
/// <c>SampleData/SampleWorkspaceService.cs</c> with your API.
/// </summary>
public interface IWorkspaceService
{
    string WorkspaceName { get; }

    int SeatLimit { get; }

    IReadOnlyList<TeamMembersPerson> GetMembers();

    IReadOnlyList<TeamMembersInvite> GetInvites();

    /// <summary>Sends the invitations of <paramref name="request"/>; throw to show an error in the block.</summary>
    Task SendInvitesAsync(TeamMembersSendRequest request, CancellationToken cancellation);

    IReadOnlyList<RolesPermissionsRole> GetRoles();

    IReadOnlyList<RolesPermissionsGroup> GetPermissionGroups();

    IReadOnlySet<RolesPermissionsGrant> GetGrants();

    /// <summary>Stores the changed grants of <paramref name="request"/>.</summary>
    Task SaveGrantsAsync(RolesPermissionsSaveRequest request, CancellationToken cancellation);

    BillingPlan GetPlan();

    BillingPaymentMethod GetPaymentMethod();

    IReadOnlyList<BillingInvoice> GetInvoices();

    string BillingEmail { get; }
}
