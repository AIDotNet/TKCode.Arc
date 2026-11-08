using ArcStarterApp.Services;
using TKCode.Arc;

namespace ArcStarterApp.SampleData;

/// <summary>An invented workspace: its people, roles, permissions and billing. The "server" only waits.</summary>
public sealed class SampleWorkspaceService : IWorkspaceService
{
    private readonly HashSet<RolesPermissionsGrant> _grants = CreateGrants();

    public string WorkspaceName => "Northwind";

    public int SeatLimit => 10;

    public string BillingEmail => "billing@northwind.example";

    public IReadOnlyList<TeamMembersPerson> GetMembers() =>
    [
        new() { Id = "ada", Name = "Ada Lovelace", Email = "ada@northwind.example", IsOnline = true, LastActive = "Active now", IsOwner = true, IsCurrentUser = true },
        new() { Id = "grace", Name = "Grace Hopper", Email = "grace@northwind.example", IsOnline = true, LastActive = "Active now", Role = "admin" },
        new() { Id = "alan", Name = "Alan Kay", Email = "alan@northwind.example", LastActive = "Active 2 hours ago", Role = "member" },
        new() { Id = "barbara", Name = "Barbara Liskov", Email = "barbara@northwind.example", LastActive = "Active yesterday", Role = "member" },
        new() { Id = "ken", Name = "Ken Thompson", Email = "ken@northwind.example", LastActive = "Active Nov 2", Role = "viewer" },
    ];

    public IReadOnlyList<TeamMembersInvite> GetInvites() =>
    [
        new() { Id = "inv-1", Email = "margaret@northwind.example", Role = "member", Detail = "Invited Nov 6" },
    ];

    public Task SendInvitesAsync(TeamMembersSendRequest request, CancellationToken cancellation) => Task.Delay(600, cancellation);

    public IReadOnlyList<RolesPermissionsRole> GetRoles() =>
    [
        new("owner", "Owner", 1, "Ada Lovelace", isLocked: true),
        new("admin", "Admin", 1, "Grace Hopper"),
        new("member", "Member", 2, "Alan Kay and Barbara Liskov"),
        new("viewer", "Viewer", 1, "Ken Thompson"),
    ];

    public IReadOnlyList<RolesPermissionsGroup> GetPermissionGroups() =>
    [
        new("customers", "Customers",
            new("customers.read", "See customers"),
            new("customers.edit", "Edit customers"),
            new("customers.export", "Export customer data")),
        new("team", "Team",
            new("team.invite", "Invite people"),
            new("team.roles", "Change roles")),
        new("billing", "Billing",
            new("billing.read", "See invoices"),
            new("billing.manage", "Change plan and payment")),
    ];

    public IReadOnlySet<RolesPermissionsGrant> GetGrants() => new HashSet<RolesPermissionsGrant>(_grants);

    public async Task SaveGrantsAsync(RolesPermissionsSaveRequest request, CancellationToken cancellation)
    {
        await Task.Delay(500, cancellation);
        _grants.Clear();
        _grants.UnionWith(request.Grants);
    }

    public BillingPlan GetPlan() => new()
    {
        Name = "Business", Price = 299, Seats = SeatLimit, Interval = BillingInterval.Month, Status = BillingPlanStatus.Active,
        RenewsOn = new DateTime(2026, 12, 1),
    };

    public BillingPaymentMethod GetPaymentMethod() => new() { Brand = "Visa", Last4 = "4242", ExpiryMonth = 8, ExpiryYear = 2028 };

    public IReadOnlyList<BillingInvoice> GetInvoices()
    {
        var invoices = new List<BillingInvoice>();
        for (var month = 0; month < 6; month++)
        {
            var date = new DateTime(2026, 11, 1).AddMonths(-month);
            invoices.Add(new BillingInvoice
            {
                Number = $"NW-{date:yyyy-MM}", Date = date, Amount = 299, Status = BillingInvoiceStatus.Paid, Description = "Business plan",
            });
        }
        return invoices;
    }

    private static HashSet<RolesPermissionsGrant> CreateGrants()
    {
        var grants = new HashSet<RolesPermissionsGrant>();
        void Grant(string role, params string[] permissions)
        {
            foreach (var permission in permissions)
            {
                grants.Add(new RolesPermissionsGrant(role, permission));
            }
        }
        Grant("admin", "customers.read", "customers.edit", "customers.export", "team.invite", "team.roles", "billing.read", "billing.manage");
        Grant("member", "customers.read", "customers.edit", "team.invite");
        Grant("viewer", "customers.read");
        return grants;
    }
}
