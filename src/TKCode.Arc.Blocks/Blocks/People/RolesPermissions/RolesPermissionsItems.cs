using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// One role of a <see cref="RolesPermissions"/> matrix: a column with its name, how many people hold it and a line for the
/// header's menu. A locked role (the workspace owner) always has every permission: its cells show a muted check and cannot
/// be changed. Roles the person duplicates in the block are new (<see cref="IsNew"/>) until the changes are saved.
/// </summary>
public sealed class RolesPermissionsRole : AvaloniaObject
{
    /// <summary>The stable id grants refer to (<see cref="RolesPermissionsGrant.RoleKey"/>); it must be unique in the matrix.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, string>(nameof(Key), string.Empty);

    /// <summary>The role's name: the column header and the menu's title.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, string>(nameof(Name), string.Empty);

    /// <summary>How many people hold the role: the header's second line ("4 people") and the reach in the review bar.</summary>
    public static readonly StyledProperty<int> MemberCountProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, int>(nameof(MemberCount));

    /// <summary>The line under the name in the header's menu (who holds it); null shows the member count.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, string?>(nameof(Description));

    /// <summary>Whether the role always has every permission (a lock leads its name and its cells cannot change).</summary>
    public static readonly StyledProperty<bool> IsLockedProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, bool>(nameof(IsLocked));

    /// <summary>True for a role duplicated in the block and not saved yet; it reads "New role" and can be deleted.</summary>
    public static readonly DirectProperty<RolesPermissionsRole, bool> IsNewProperty =
        AvaloniaProperty.RegisterDirect<RolesPermissionsRole, bool>(nameof(IsNew), o => o.IsNew);

    /// <summary>Any object the app wants to keep with the role.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<RolesPermissionsRole, object?>(nameof(Tag));

    private bool _isNew;

    /// <summary>Creates an empty role (XAML).</summary>
    public RolesPermissionsRole()
    {
    }

    /// <summary>Creates a role.</summary>
    /// <param name="key">The stable id.</param>
    /// <param name="name">The name.</param>
    /// <param name="memberCount">How many people hold it.</param>
    /// <param name="description">The line under the name in its menu.</param>
    /// <param name="isLocked">Whether it always has every permission.</param>
    public RolesPermissionsRole(string key, string name, int memberCount = 0, string? description = null, bool isLocked = false)
    {
        Key = key;
        Name = name;
        MemberCount = memberCount;
        Description = description;
        IsLocked = isLocked;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="MemberCountProperty"/>
    public int MemberCount { get => GetValue(MemberCountProperty); set => SetValue(MemberCountProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsLockedProperty"/>
    public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }

    /// <inheritdoc cref="IsNewProperty"/>
    public bool IsNew { get => _isNew; internal set => SetAndRaise(IsNewProperty, ref _isNew, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// A group of permissions in a <see cref="RolesPermissions"/> matrix: a row with a disclosure button and one "whole group"
/// checkbox per role (on when every permission of the group is on, mixed when some are), followed by its permissions.
/// </summary>
public sealed class RolesPermissionsGroup : AvaloniaObject
{
    /// <summary>A stable id the app can match the group by (events); null when it has none.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<RolesPermissionsGroup, string?>(nameof(Key));

    /// <summary>The group's name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<RolesPermissionsGroup, string>(nameof(Name), string.Empty);

    /// <summary>Whether the group's permissions are shown (two-way: the disclosure button writes it back).</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<RolesPermissionsGroup, bool>(nameof(IsExpanded), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Creates an empty group (XAML).</summary>
    public RolesPermissionsGroup()
    {
    }

    /// <summary>Creates a group with its permissions.</summary>
    /// <param name="key">The stable id.</param>
    /// <param name="name">The name.</param>
    /// <param name="permissions">The permissions, in order.</param>
    public RolesPermissionsGroup(string? key, string name, params RolesPermissionsItem[] permissions)
    {
        Key = key;
        Name = name;
        Permissions.AddRange(permissions);
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <summary>The group's permissions, in order.</summary>
    [Content]
    public AvaloniaList<RolesPermissionsItem> Permissions { get; } = new();

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One permission of a <see cref="RolesPermissionsGroup"/>: a row with one switch per role.</summary>
public sealed class RolesPermissionsItem : AvaloniaObject
{
    /// <summary>The stable id grants refer to (<see cref="RolesPermissionsGrant.PermissionKey"/>); unique in the matrix.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<RolesPermissionsItem, string>(nameof(Key), string.Empty);

    /// <summary>The permission's name: the row's label, also read in the switches' names ("Manager: Send invitations").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<RolesPermissionsItem, string>(nameof(Name), string.Empty);

    /// <summary>A longer explanation, read as the row's switches' help text; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<RolesPermissionsItem, string?>(nameof(Description));

    /// <summary>Creates an empty permission (XAML).</summary>
    public RolesPermissionsItem()
    {
    }

    /// <summary>Creates a permission.</summary>
    /// <param name="key">The stable id.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">A longer explanation.</param>
    public RolesPermissionsItem(string key, string name, string? description = null)
    {
        Key = key;
        Name = name;
        Description = description;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>One granted permission: the role <see cref="RoleKey"/> may do <see cref="PermissionKey"/>.</summary>
/// <param name="RoleKey">The <see cref="RolesPermissionsRole.Key"/>.</param>
/// <param name="PermissionKey">The <see cref="RolesPermissionsItem.Key"/>.</param>
public readonly record struct RolesPermissionsGrant(string RoleKey, string PermissionKey);

/// <summary>Where the save of a <see cref="RolesPermissions"/> matrix stands.</summary>
public enum RolesPermissionsSaveState
{
    /// <summary>No save is running; the review bar (if open) offers Discard and Save.</summary>
    Idle,

    /// <summary>The save handler runs: the Save button spins and the matrix does not take changes.</summary>
    Saving,

    /// <summary>The save succeeded; the bar reads "saved" for <see cref="RolesPermissions.SavedHoldDuration"/>, then closes.</summary>
    Saved,

    /// <summary>The save handler threw: the changes are kept, the bar shows the error and Save tries again.</summary>
    Failed,
}

/// <summary>What changed a grant (passed with <see cref="RolesPermissionsGrantChangedEventArgs"/>).</summary>
public enum RolesPermissionsChangeSource
{
    /// <summary>One cell was switched by the person.</summary>
    Cell,

    /// <summary>A group checkbox switched every permission of a group.</summary>
    Group,

    /// <summary>A role's changes were reverted from its menu.</summary>
    Revert,

    /// <summary>Every unsaved change was discarded.</summary>
    Discard,

    /// <summary>Code called <see cref="RolesPermissions.SetGrant"/> or <see cref="RolesPermissions.ToggleGroup"/>.</summary>
    Code,
}

/// <summary>One changed cell of a save: the role, the permission and whether it is granted now.</summary>
/// <param name="RoleKey">The role.</param>
/// <param name="PermissionKey">The permission.</param>
/// <param name="IsGranted">True when it is granted after the save, false when it was taken away.</param>
public readonly record struct RolesPermissionsChange(string RoleKey, string PermissionKey, bool IsGranted);
