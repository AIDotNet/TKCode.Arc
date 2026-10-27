using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RolesPermissionsBoundDemo : UserControl
{
    private readonly List<string> _log = new();

    public RolesPermissionsBoundDemo()
    {
        InitializeComponent();
        RolesPermissionsDemoData.Fill(Matrix);
        Matrix.SaveHandler = RolesPermissionsDemoData.SimulatedSave;

        // Grants is two-way: the block writes a new set on every change, and a set written here shows at once.
        GrantAll.Click += (_, _) =>
        {
            var grants = new HashSet<RolesPermissionsGrant>(Matrix.Grants ?? Matrix.SavedGrants ?? new HashSet<RolesPermissionsGrant>());
            foreach (var group in Matrix.Groups)
            {
                foreach (var permission in group.Permissions)
                {
                    grants.Add(new RolesPermissionsGrant("guest", permission.Key));
                }
            }
            Matrix.Grants = grants;
        };
        FoldAll.Click += (_, _) =>
        {
            var fold = Matrix.Groups.Any(g => g.IsExpanded);
            foreach (var group in Matrix.Groups)
            {
                group.IsExpanded = !fold;
            }
            FoldAll.Content = fold ? "Unfold all groups" : "Fold all groups";
        };
        Copy.Click += (_, _) => Matrix.DuplicateRole("member");

        Matrix.GrantChanged += (_, e) => Add($"{e.Role.Name}: {e.Permission.Name} {(e.IsGranted ? "on" : "off")} ({e.ChangeSource})");
        Matrix.GroupToggled += (_, e) => Add($"{e.Role.Name}: {e.Group.Name} {(e.IsGranted ? "all on" : "all off")}, {e.ChangedCount} cells");
        Matrix.RoleDuplicated += (_, e) => Add($"Copied {e.SourceRole.Name} as {e.Role.Name}");
        Matrix.RoleDeleted += (_, e) => Add($"Removed {e.Role.Name}");
        Matrix.ChangesReverted += (_, e) => Add(e.Role is null ? $"Discarded {e.Count}" : $"Undid {e.Count} for {e.Role.Name}");
        Matrix.Saved += (_, e) => Add($"Saved {e.Request.ChangeCount}: {e.Request.Changes.Count} cells, {e.Request.AddedRoles.Count} new roles");
        Matrix.PropertyChanged += (_, e) =>
        {
            if (e.Property == RolesPermissions.PendingChangeCountProperty || e.Property == RolesPermissions.SaveStateProperty)
            {
                Status.Text = $"Pending: {Matrix.PendingChangeCount} · Save: {Matrix.SaveState}";
            }
        };
        Status.Text = "Pending: 0 · Save: Idle";
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 6)
        {
            _log.RemoveAt(_log.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _log);
    }
}
