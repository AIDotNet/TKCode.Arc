using System.Collections;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A workspace's roles × permissions matrix. Columns are roles, rows are permissions in collapsible groups; each cell is a
/// switch and each group row has one tri-state checkbox per role that switches the whole group (the changed cells ripple
/// one after another). Locked roles (the owner) always have everything. Changes are not applied at once: changed cells and
/// their role headers get an unsaved dot, and a review bar slides up with the count, the roles they touch and the people
/// they reach, Discard and Save. Save awaits <see cref="SaveHandler"/> with a spinner, reads "saved" for a moment and
/// closes; a failed save keeps the changes and offers to try again. Each role header opens a menu to copy the role
/// (inserted on its right as a new role), undo its edits, or remove a new role.
/// <code>
/// &lt;arc:RolesPermissions Title="Roles &amp; access" WorkspaceName="Northwind Studio"
///                       RolesSource="{Binding Roles}" GroupsSource="{Binding Groups}"
///                       SavedGrants="{Binding Saved}" Grants="{Binding Current, Mode=TwoWay}" /&gt;
/// </code>
/// </summary>
public partial class RolesPermissions : TemplatedControl
{
    #region Properties

    /// <summary>The heading.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RolesPermissions, string?>(nameof(Title), "Roles & access");

    /// <summary>The workspace named in the subtitle ("Northwind Studio · 31 people in 5 roles"); null leaves it out.</summary>
    public static readonly StyledProperty<string?> WorkspaceNameProperty =
        AvaloniaProperty.Register<RolesPermissions, string?>(nameof(WorkspaceName));

    /// <summary>The member count in the subtitle; null counts the members shown in the header.</summary>
    public static readonly StyledProperty<int?> MemberCountProperty =
        AvaloniaProperty.Register<RolesPermissions, int?>(nameof(MemberCount));

    /// <summary>The workspace members shown as avatars in the header (<see cref="AvatarGroupMember"/>, names, ...), when you give them as a list; it wins over <see cref="Members"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> MembersSourceProperty =
        AvaloniaProperty.Register<RolesPermissions, IEnumerable?>(nameof(MembersSource));

    /// <summary>The roles (columns), when you give them as a list to follow (observed when it raises collection changes); it wins over <see cref="Roles"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> RolesSourceProperty =
        AvaloniaProperty.Register<RolesPermissions, IEnumerable?>(nameof(RolesSource));

    /// <summary>The permission groups (rows), when you give them as a list to follow; it wins over <see cref="Groups"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> GroupsSourceProperty =
        AvaloniaProperty.Register<RolesPermissions, IEnumerable?>(nameof(GroupsSource));

    /// <summary>
    /// The saved grants: the baseline unsaved dots compare against. A successful save writes the new baseline back
    /// (two-way). Locked roles need no grants.
    /// </summary>
    public static readonly StyledProperty<IReadOnlySet<RolesPermissionsGrant>?> SavedGrantsProperty =
        AvaloniaProperty.Register<RolesPermissions, IReadOnlySet<RolesPermissionsGrant>?>(nameof(SavedGrants),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The current grants, saved or not (two-way: every change writes a new set). Null starts from <see cref="SavedGrants"/>.
    /// </summary>
    public static readonly StyledProperty<IReadOnlySet<RolesPermissionsGrant>?> GrantsProperty =
        AvaloniaProperty.Register<RolesPermissions, IReadOnlySet<RolesPermissionsGrant>?>(nameof(Grants),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Stores the changes: awaited when Save is pressed (or <see cref="SaveAsync"/> is called) with what to save and a
    /// token that <see cref="CancelSave"/> or the block leaving the window cancels. While it runs the matrix takes no
    /// changes. An exception keeps the changes and shows the failure; a cancellation returns to the review bar quietly.
    /// Null saves at once.
    /// </summary>
    public static readonly StyledProperty<Func<RolesPermissionsSaveRequest, CancellationToken, Task>?> SaveHandlerProperty =
        AvaloniaProperty.Register<RolesPermissions, Func<RolesPermissionsSaveRequest, CancellationToken, Task>?>(nameof(SaveHandler));

    /// <summary>How long the review bar reads "saved" before it closes; null uses the token's 1.6 s.</summary>
    public static readonly StyledProperty<TimeSpan?> SavedHoldDurationProperty =
        AvaloniaProperty.Register<RolesPermissions, TimeSpan?>(nameof(SavedHoldDuration));

    /// <summary>Turns a failed save's exception into the bar's second line; null shows the exception's message.</summary>
    public static readonly StyledProperty<Func<Exception, string>?> ErrorFormatterProperty =
        AvaloniaProperty.Register<RolesPermissions, Func<Exception, string>?>(nameof(ErrorFormatter));

    /// <summary>Whether the role headers open their menu (copy, undo edits, remove).</summary>
    public static readonly StyledProperty<bool> IsRoleMenuEnabledProperty =
        AvaloniaProperty.Register<RolesPermissions, bool>(nameof(IsRoleMenuEnabled), true);

    /// <summary>Whether the role menu offers to copy a role.</summary>
    public static readonly StyledProperty<bool> CanDuplicateRolesProperty =
        AvaloniaProperty.Register<RolesPermissions, bool>(nameof(CanDuplicateRoles), true);

    /// <summary>Whether the footer with the two legends shows.</summary>
    public static readonly StyledProperty<bool> IsFooterVisibleProperty =
        AvaloniaProperty.Register<RolesPermissions, bool>(nameof(IsFooterVisible), true);

    /// <summary>Draws a bordered card with radius 16 (true); false lays the block flat with a page margin (16–32 by width).</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<RolesPermissions, bool>(nameof(IsFramed), true);

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<RolesPermissionsLabels> LabelsProperty =
        AvaloniaProperty.Register<RolesPermissions, RolesPermissionsLabels>(nameof(Labels), new RolesPermissionsLabels());

    /// <summary>The number the review bar shows: one per changed cell, plus one per new role.</summary>
    public static readonly DirectProperty<RolesPermissions, int> PendingChangeCountProperty =
        AvaloniaProperty.RegisterDirect<RolesPermissions, int>(nameof(PendingChangeCount), o => o.PendingChangeCount);

    /// <summary>True while there is anything to save.</summary>
    public static readonly DirectProperty<RolesPermissions, bool> HasUnsavedChangesProperty =
        AvaloniaProperty.RegisterDirect<RolesPermissions, bool>(nameof(HasUnsavedChanges), o => o.HasUnsavedChanges);

    /// <summary>Where the save stands.</summary>
    public static readonly DirectProperty<RolesPermissions, RolesPermissionsSaveState> SaveStateProperty =
        AvaloniaProperty.RegisterDirect<RolesPermissions, RolesPermissionsSaveState>(nameof(SaveState), o => o.SaveState);

    /// <summary>The last failed save's error line; null unless <see cref="SaveState"/> is Failed.</summary>
    public static readonly DirectProperty<RolesPermissions, string?> ErrorTextProperty =
        AvaloniaProperty.RegisterDirect<RolesPermissions, string?>(nameof(ErrorText), o => o.ErrorText);

    #endregion

    #region Events

    /// <summary>Raised for every cell that changes value, by the person or by code.</summary>
    public static readonly RoutedEvent<RolesPermissionsGrantChangedEventArgs> GrantChangedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsGrantChangedEventArgs>(nameof(GrantChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a group checkbox switched a group for a role (after the cells' <see cref="GrantChanged"/>).</summary>
    public static readonly RoutedEvent<RolesPermissionsGroupToggledEventArgs> GroupToggledEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsGroupToggledEventArgs>(nameof(GroupToggled), RoutingStrategies.Bubble);

    /// <summary>Raised when a role was copied.</summary>
    public static readonly RoutedEvent<RolesPermissionsRoleDuplicatedEventArgs> RoleDuplicatedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsRoleDuplicatedEventArgs>(nameof(RoleDuplicated), RoutingStrategies.Bubble);

    /// <summary>Raised when a new role was removed (its menu, or Discard).</summary>
    public static readonly RoutedEvent<RolesPermissionsRoleDeletedEventArgs> RoleDeletedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsRoleDeletedEventArgs>(nameof(RoleDeleted), RoutingStrategies.Bubble);

    /// <summary>Raised when a role's edits were undone, or every change was discarded.</summary>
    public static readonly RoutedEvent<RolesPermissionsRevertedEventArgs> ChangesRevertedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsRevertedEventArgs>(nameof(ChangesReverted), RoutingStrategies.Bubble);

    /// <summary>Raised before a save runs; set <see cref="RolesPermissionsSaveStartingEventArgs.Cancel"/> to keep it from starting.</summary>
    public static readonly RoutedEvent<RolesPermissionsSaveStartingEventArgs> SaveStartingEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsSaveStartingEventArgs>(nameof(SaveStarting), RoutingStrategies.Bubble);

    /// <summary>Raised when the save handler finished without an error.</summary>
    public static readonly RoutedEvent<RolesPermissionsSavedEventArgs> SavedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsSavedEventArgs>(nameof(Saved), RoutingStrategies.Bubble);

    /// <summary>Raised when the save handler threw.</summary>
    public static readonly RoutedEvent<RolesPermissionsSaveFailedEventArgs> SaveFailedEvent =
        RoutedEvent.Register<RolesPermissions, RolesPermissionsSaveFailedEventArgs>(nameof(SaveFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="GrantChangedEvent"/>
    public event EventHandler<RolesPermissionsGrantChangedEventArgs>? GrantChanged
    {
        add => AddHandler(GrantChangedEvent, value);
        remove => RemoveHandler(GrantChangedEvent, value);
    }

    /// <inheritdoc cref="GroupToggledEvent"/>
    public event EventHandler<RolesPermissionsGroupToggledEventArgs>? GroupToggled
    {
        add => AddHandler(GroupToggledEvent, value);
        remove => RemoveHandler(GroupToggledEvent, value);
    }

    /// <inheritdoc cref="RoleDuplicatedEvent"/>
    public event EventHandler<RolesPermissionsRoleDuplicatedEventArgs>? RoleDuplicated
    {
        add => AddHandler(RoleDuplicatedEvent, value);
        remove => RemoveHandler(RoleDuplicatedEvent, value);
    }

    /// <inheritdoc cref="RoleDeletedEvent"/>
    public event EventHandler<RolesPermissionsRoleDeletedEventArgs>? RoleDeleted
    {
        add => AddHandler(RoleDeletedEvent, value);
        remove => RemoveHandler(RoleDeletedEvent, value);
    }

    /// <inheritdoc cref="ChangesRevertedEvent"/>
    public event EventHandler<RolesPermissionsRevertedEventArgs>? ChangesReverted
    {
        add => AddHandler(ChangesRevertedEvent, value);
        remove => RemoveHandler(ChangesRevertedEvent, value);
    }

    /// <inheritdoc cref="SaveStartingEvent"/>
    public event EventHandler<RolesPermissionsSaveStartingEventArgs>? SaveStarting
    {
        add => AddHandler(SaveStartingEvent, value);
        remove => RemoveHandler(SaveStartingEvent, value);
    }

    /// <inheritdoc cref="SavedEvent"/>
    public event EventHandler<RolesPermissionsSavedEventArgs>? Saved
    {
        add => AddHandler(SavedEvent, value);
        remove => RemoveHandler(SavedEvent, value);
    }

    /// <inheritdoc cref="SaveFailedEvent"/>
    public event EventHandler<RolesPermissionsSaveFailedEventArgs>? SaveFailed
    {
        add => AddHandler(SaveFailedEvent, value);
        remove => RemoveHandler(SaveFailedEvent, value);
    }

    #endregion

    private int _pendingChangeCount;
    private bool _hasUnsavedChanges;
    private RolesPermissionsSaveState _saveState;
    private string? _errorText;

    /// <summary>The roles (columns) in order, when no <see cref="RolesSource"/> is set. Copies are inserted here.</summary>
    public AvaloniaList<RolesPermissionsRole> Roles { get; } = new();

    /// <summary>The permission groups (rows) in order, when no <see cref="GroupsSource"/> is set.</summary>
    [Content]
    public AvaloniaList<RolesPermissionsGroup> Groups { get; } = new();

    /// <summary>The workspace members shown as avatars in the header, when no <see cref="MembersSource"/> is set.</summary>
    public AvaloniaList<object> Members { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="WorkspaceNameProperty"/>
    public string? WorkspaceName { get => GetValue(WorkspaceNameProperty); set => SetValue(WorkspaceNameProperty, value); }

    /// <inheritdoc cref="MemberCountProperty"/>
    public int? MemberCount { get => GetValue(MemberCountProperty); set => SetValue(MemberCountProperty, value); }

    /// <inheritdoc cref="MembersSourceProperty"/>
    public IEnumerable? MembersSource { get => GetValue(MembersSourceProperty); set => SetValue(MembersSourceProperty, value); }

    /// <inheritdoc cref="RolesSourceProperty"/>
    public IEnumerable? RolesSource { get => GetValue(RolesSourceProperty); set => SetValue(RolesSourceProperty, value); }

    /// <inheritdoc cref="GroupsSourceProperty"/>
    public IEnumerable? GroupsSource { get => GetValue(GroupsSourceProperty); set => SetValue(GroupsSourceProperty, value); }

    /// <inheritdoc cref="SavedGrantsProperty"/>
    public IReadOnlySet<RolesPermissionsGrant>? SavedGrants { get => GetValue(SavedGrantsProperty); set => SetValue(SavedGrantsProperty, value); }

    /// <inheritdoc cref="GrantsProperty"/>
    public IReadOnlySet<RolesPermissionsGrant>? Grants { get => GetValue(GrantsProperty); set => SetValue(GrantsProperty, value); }

    /// <inheritdoc cref="SaveHandlerProperty"/>
    public Func<RolesPermissionsSaveRequest, CancellationToken, Task>? SaveHandler
    {
        get => GetValue(SaveHandlerProperty);
        set => SetValue(SaveHandlerProperty, value);
    }

    /// <inheritdoc cref="SavedHoldDurationProperty"/>
    public TimeSpan? SavedHoldDuration { get => GetValue(SavedHoldDurationProperty); set => SetValue(SavedHoldDurationProperty, value); }

    /// <inheritdoc cref="ErrorFormatterProperty"/>
    public Func<Exception, string>? ErrorFormatter { get => GetValue(ErrorFormatterProperty); set => SetValue(ErrorFormatterProperty, value); }

    /// <inheritdoc cref="IsRoleMenuEnabledProperty"/>
    public bool IsRoleMenuEnabled { get => GetValue(IsRoleMenuEnabledProperty); set => SetValue(IsRoleMenuEnabledProperty, value); }

    /// <inheritdoc cref="CanDuplicateRolesProperty"/>
    public bool CanDuplicateRoles { get => GetValue(CanDuplicateRolesProperty); set => SetValue(CanDuplicateRolesProperty, value); }

    /// <inheritdoc cref="IsFooterVisibleProperty"/>
    public bool IsFooterVisible { get => GetValue(IsFooterVisibleProperty); set => SetValue(IsFooterVisibleProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public RolesPermissionsLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PendingChangeCountProperty"/>
    public int PendingChangeCount { get => _pendingChangeCount; private set => SetAndRaise(PendingChangeCountProperty, ref _pendingChangeCount, value); }

    /// <inheritdoc cref="HasUnsavedChangesProperty"/>
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => SetAndRaise(HasUnsavedChangesProperty, ref _hasUnsavedChanges, value); }

    /// <inheritdoc cref="SaveStateProperty"/>
    public RolesPermissionsSaveState SaveState { get => _saveState; private set => SetAndRaise(SaveStateProperty, ref _saveState, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => _errorText; private set => SetAndRaise(ErrorTextProperty, ref _errorText, value); }
}
