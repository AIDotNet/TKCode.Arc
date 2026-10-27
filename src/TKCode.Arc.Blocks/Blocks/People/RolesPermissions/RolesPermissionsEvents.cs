using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// What a save of a <see cref="RolesPermissions"/> matrix carries: passed to <see cref="RolesPermissions.SaveHandler"/> and
/// the save events. Everything is a snapshot taken when the save started.
/// </summary>
public sealed class RolesPermissionsSaveRequest
{
    /// <summary>Creates a request.</summary>
    /// <param name="grants">Every grant the matrix will hold after the save.</param>
    /// <param name="previousGrants">The saved grants before this save.</param>
    /// <param name="changes">The cells that differ from the saved grants, in column then row order.</param>
    /// <param name="roles">Every role, in column order.</param>
    /// <param name="addedRoles">The roles duplicated since the last save.</param>
    /// <param name="changeCount">The count the review bar shows.</param>
    public RolesPermissionsSaveRequest(IReadOnlySet<RolesPermissionsGrant> grants, IReadOnlySet<RolesPermissionsGrant> previousGrants,
                                       IReadOnlyList<RolesPermissionsChange> changes, IReadOnlyList<RolesPermissionsRole> roles,
                                       IReadOnlyList<RolesPermissionsRole> addedRoles, int changeCount)
    {
        Grants = grants;
        PreviousGrants = previousGrants;
        Changes = changes;
        Roles = roles;
        AddedRoles = addedRoles;
        ChangeCount = changeCount;
    }

    /// <summary>Every grant the matrix holds after the save (locked roles are not listed: they have every permission).</summary>
    public IReadOnlySet<RolesPermissionsGrant> Grants { get; }

    /// <summary>The saved grants before this save.</summary>
    public IReadOnlySet<RolesPermissionsGrant> PreviousGrants { get; }

    /// <summary>The cells that differ from <see cref="PreviousGrants"/> (a new role lists every permission it has).</summary>
    public IReadOnlyList<RolesPermissionsChange> Changes { get; }

    /// <summary>Every role, in column order.</summary>
    public IReadOnlyList<RolesPermissionsRole> Roles { get; }

    /// <summary>The roles duplicated since the last save (they are no longer new once the save succeeds).</summary>
    public IReadOnlyList<RolesPermissionsRole> AddedRoles { get; }

    /// <summary>The number of unsaved changes the review bar showed (a new role counts once, plus its own edits).</summary>
    public int ChangeCount { get; }
}

/// <summary>Arguments of <c>RolesPermissions.GrantChanged</c>: one cell changed value.</summary>
public class RolesPermissionsGrantChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsGrantChangedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsRole role,
                                                 RolesPermissionsItem permission, bool isGranted, bool isUnsaved,
                                                 RolesPermissionsChangeSource changeSource)
        : base(routedEvent, source)
    {
        Role = role;
        Permission = permission;
        IsGranted = isGranted;
        IsUnsaved = isUnsaved;
        ChangeSource = changeSource;
    }

    /// <summary>The role (column).</summary>
    public RolesPermissionsRole Role { get; }

    /// <summary>The permission (row).</summary>
    public RolesPermissionsItem Permission { get; }

    /// <summary>Whether the role has the permission now.</summary>
    public bool IsGranted { get; }

    /// <summary>Whether the cell now differs from the saved value.</summary>
    public bool IsUnsaved { get; }

    /// <summary>What changed it.</summary>
    public RolesPermissionsChangeSource ChangeSource { get; }
}

/// <summary>Arguments of <c>RolesPermissions.GroupToggled</c>: a group checkbox switched a whole group for one role.</summary>
public class RolesPermissionsGroupToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsGroupToggledEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsGroup group,
                                                 RolesPermissionsRole role, bool isGranted, int changedCount)
        : base(routedEvent, source)
    {
        Group = group;
        Role = role;
        IsGranted = isGranted;
        ChangedCount = changedCount;
    }

    /// <summary>The group.</summary>
    public RolesPermissionsGroup Group { get; }

    /// <summary>The role (column).</summary>
    public RolesPermissionsRole Role { get; }

    /// <summary>True when every permission of the group was switched on, false when every one was switched off.</summary>
    public bool IsGranted { get; }

    /// <summary>How many cells actually changed.</summary>
    public int ChangedCount { get; }
}

/// <summary>Arguments of <c>RolesPermissions.RoleDuplicated</c>.</summary>
public class RolesPermissionsRoleDuplicatedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsRoleDuplicatedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsRole sourceRole,
                                                   RolesPermissionsRole role)
        : base(routedEvent, source)
    {
        SourceRole = sourceRole;
        Role = role;
    }

    /// <summary>The role that was copied.</summary>
    public RolesPermissionsRole SourceRole { get; }

    /// <summary>The new role (inserted right of the source, <see cref="RolesPermissionsRole.IsNew"/> true).</summary>
    public RolesPermissionsRole Role { get; }
}

/// <summary>Arguments of <c>RolesPermissions.RoleDeleted</c>: a new role was removed before it was saved.</summary>
public class RolesPermissionsRoleDeletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsRoleDeletedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsRole role, bool isDiscard)
        : base(routedEvent, source)
    {
        Role = role;
        IsDiscard = isDiscard;
    }

    /// <summary>The removed role.</summary>
    public RolesPermissionsRole Role { get; }

    /// <summary>True when Discard removed it with every other unsaved change; false for the menu's Delete.</summary>
    public bool IsDiscard { get; }
}

/// <summary>Arguments of <c>RolesPermissions.ChangesReverted</c>: one role's changes were reverted, or every change discarded.</summary>
public class RolesPermissionsRevertedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsRevertedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsRole? role, int count)
        : base(routedEvent, source)
    {
        Role = role;
        Count = count;
    }

    /// <summary>The role whose changes were reverted; null when every change was discarded.</summary>
    public RolesPermissionsRole? Role { get; }

    /// <summary>How many unsaved changes went away.</summary>
    public int Count { get; }
}

/// <summary>Arguments of <c>RolesPermissions.SaveStarting</c>; set <see cref="Cancel"/> to keep the save from running.</summary>
public class RolesPermissionsSaveStartingEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsSaveStartingEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsSaveRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What would be saved.</summary>
    public RolesPermissionsSaveRequest Request { get; }

    /// <summary>Set to true to keep the save from starting.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Arguments of <c>RolesPermissions.Saved</c>: the save handler finished without an error.</summary>
public class RolesPermissionsSavedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsSavedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsSaveRequest request)
        : base(routedEvent, source)
    {
        Request = request;
    }

    /// <summary>What was saved.</summary>
    public RolesPermissionsSaveRequest Request { get; }
}

/// <summary>Arguments of <c>RolesPermissions.SaveFailed</c>: the save handler threw; the changes are kept.</summary>
public class RolesPermissionsSaveFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public RolesPermissionsSaveFailedEventArgs(RoutedEvent routedEvent, object source, RolesPermissionsSaveRequest request,
                                               Exception exception)
        : base(routedEvent, source)
    {
        Request = request;
        Exception = exception;
    }

    /// <summary>What the save tried to store.</summary>
    public RolesPermissionsSaveRequest Request { get; }

    /// <summary>What the handler threw.</summary>
    public Exception Exception { get; }
}
