using System.Collections.ObjectModel;
using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.ViewModels;

/// <summary>
/// The Team page: the members and invitations (TeamMembers edits <see cref="Members"/> and <see cref="Invites"/> in
/// place) and the role matrix. The blocks call <see cref="SendInvitesAsync"/> and <see cref="SaveGrantsAsync"/> and show
/// their progress and errors themselves.
/// </summary>
public sealed class TeamPageViewModel
{
    private readonly IWorkspaceService _workspace;

    public TeamPageViewModel(IWorkspaceService workspace)
    {
        _workspace = workspace;
        Members = new ObservableCollection<TeamMembersPerson>(workspace.GetMembers());
        Invites = new ObservableCollection<TeamMembersInvite>(workspace.GetInvites());
        Grants = workspace.GetGrants();
    }

    public string WorkspaceName => _workspace.WorkspaceName;

    public int SeatLimit => _workspace.SeatLimit;

    public ObservableCollection<TeamMembersPerson> Members { get; }

    public ObservableCollection<TeamMembersInvite> Invites { get; }

    public IReadOnlyList<RolesPermissionsRole> Roles => _workspace.GetRoles();

    public IReadOnlyList<RolesPermissionsGroup> PermissionGroups => _workspace.GetPermissionGroups();

    public IReadOnlySet<RolesPermissionsGrant> Grants { get; private set; }

    public Task SendInvitesAsync(TeamMembersSendRequest request, CancellationToken cancellation) =>
        _workspace.SendInvitesAsync(request, cancellation);

    public async Task SaveGrantsAsync(RolesPermissionsSaveRequest request, CancellationToken cancellation)
    {
        await _workspace.SaveGrantsAsync(request, cancellation);
        Grants = request.Grants;
    }
}
