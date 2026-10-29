using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// One member of the workspace shown by <see cref="TeamMembers"/>: an avatar (photo or initials, with a presence dot), the
/// name and email, when the person was last active, and the role, which can be changed from the row's menu (the owner's
/// cannot).
/// </summary>
public sealed class TeamMembersPerson : AvaloniaObject
{
    /// <summary>A stable key (rows keep their state across list changes); null uses the email.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<TeamMembersPerson, string?>(nameof(Id));

    /// <summary>The display name; the avatar's initials come from it when there is no photo.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<TeamMembersPerson, string?>(nameof(Name));

    /// <summary>The email address, shown under the name and matched by the search and the invite field.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<TeamMembersPerson, string?>(nameof(Email));

    /// <summary>The photo; null (or a photo that fails to load) shows the initials.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<TeamMembersPerson, ImageSource?>(nameof(Avatar));

    /// <summary>Whether the presence dot shows on the avatar.</summary>
    public static readonly StyledProperty<bool> IsOnlineProperty =
        AvaloniaProperty.Register<TeamMembersPerson, bool>(nameof(IsOnline));

    /// <summary>When the person was last active, already formatted ("Active now", "Active 3 hours ago").</summary>
    public static readonly StyledProperty<string?> LastActiveProperty =
        AvaloniaProperty.Register<TeamMembersPerson, string?>(nameof(LastActive));

    /// <summary>The key of the person's role among <see cref="TeamMembers.Roles"/> (two-way: the row's menu writes it).</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<TeamMembersPerson, string?>(nameof(Role), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The owner: the row shows the owner label instead of a role menu and has no remove button.</summary>
    public static readonly StyledProperty<bool> IsOwnerProperty =
        AvaloniaProperty.Register<TeamMembersPerson, bool>(nameof(IsOwner));

    /// <summary>The person looking at the page: a muted "You" follows the name.</summary>
    public static readonly StyledProperty<bool> IsCurrentUserProperty =
        AvaloniaProperty.Register<TeamMembersPerson, bool>(nameof(IsCurrentUser));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="IsOnlineProperty"/>
    public bool IsOnline { get => GetValue(IsOnlineProperty); set => SetValue(IsOnlineProperty, value); }

    /// <inheritdoc cref="LastActiveProperty"/>
    public string? LastActive { get => GetValue(LastActiveProperty); set => SetValue(LastActiveProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="IsOwnerProperty"/>
    public bool IsOwner { get => GetValue(IsOwnerProperty); set => SetValue(IsOwnerProperty, value); }

    /// <inheritdoc cref="IsCurrentUserProperty"/>
    public bool IsCurrentUser { get => GetValue(IsCurrentUserProperty); set => SetValue(IsCurrentUserProperty, value); }

    /// <summary>The name, or the email when there is no name.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Email ?? string.Empty : Name!;

    internal string Key => Id ?? Email ?? Name ?? string.Empty;
}

/// <summary>
/// An invite that was sent and not accepted yet: shown by <see cref="TeamMembers"/> under the members with a dashed avatar
/// (initials from the address), the address, "role · detail" and the Resend and Revoke buttons. It takes a seat.
/// </summary>
public sealed class TeamMembersInvite : AvaloniaObject
{
    /// <summary>A stable key; null uses the email.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<TeamMembersInvite, string?>(nameof(Id));

    /// <summary>The invited address.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<TeamMembersInvite, string?>(nameof(Email));

    /// <summary>The key of the role the person will get.</summary>
    public static readonly StyledProperty<string?> RoleProperty =
        AvaloniaProperty.Register<TeamMembersInvite, string?>(nameof(Role));

    /// <summary>The text after the role ("Invited Oct 7"); the block writes its own after a send or a resend.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<TeamMembersInvite, string?>(nameof(Detail));

    /// <summary>The avatar's letters; null takes the first letters of the address's name part (split at . _ -), at most two.</summary>
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<TeamMembersInvite, string?>(nameof(Initials));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="InitialsProperty"/>
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    /// <summary>The letters the avatar shows: <see cref="Initials"/>, or derived from the address.</summary>
    public string DisplayInitials => string.IsNullOrWhiteSpace(Initials) ? TeamMembersEmailParser.InitialsOf(Email) : Initials!;

    internal string Key => Id ?? Email ?? string.Empty;
}

/// <summary>A role people can be given: its key, the name shown on triggers and a line describing it in the menu.</summary>
public sealed class TeamMembersRoleOption : AvaloniaObject
{
    /// <summary>The key stored in <see cref="TeamMembersPerson.Role"/> and <see cref="TeamMembersInvite.Role"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<TeamMembersRoleOption, string?>(nameof(Key));

    /// <summary>The name shown on the triggers, in the menu and in "role · detail".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TeamMembersRoleOption, string?>(nameof(Label));

    /// <summary>The line under the name in the menu; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TeamMembersRoleOption, string?>(nameof(Description));

    /// <summary>Creates an empty option.</summary>
    public TeamMembersRoleOption()
    {
    }

    /// <summary>Creates an option.</summary>
    public TeamMembersRoleOption(string key, string label, string? description = null)
    {
        Key = key;
        Label = label;
        Description = description;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The label, or the key when there is no label.</summary>
    public string DisplayLabel => Label ?? Key ?? string.Empty;
}

/// <summary>What <see cref="TeamMembers.SendInvitesAction"/> is asked to send.</summary>
public sealed class TeamMembersSendRequest
{
    /// <summary>Creates a request.</summary>
    public TeamMembersSendRequest(IReadOnlyList<string> emails, string? role, IReadOnlyList<TeamMembersInvite> invites)
    {
        Emails = emails;
        Role = role;
        Invites = invites;
    }

    /// <summary>The addresses, in the order they were added.</summary>
    public IReadOnlyList<string> Emails { get; }

    /// <summary>The role key chosen in "Invite as".</summary>
    public string? Role { get; }

    /// <summary>
    /// The invites the block adds to the list when the action succeeds, one per address; the action may fill in their
    /// <see cref="TeamMembersInvite.Id"/> or <see cref="TeamMembersInvite.Detail"/>.
    /// </summary>
    public IReadOnlyList<TeamMembersInvite> Invites { get; }
}

/// <summary>What <see cref="TeamMembers.ChangeRoleAction"/> is asked to change.</summary>
public sealed class TeamMembersRoleChange
{
    /// <summary>Creates a change.</summary>
    public TeamMembersRoleChange(TeamMembersPerson member, string? oldRole, string? newRole)
    {
        Member = member;
        OldRole = oldRole;
        NewRole = newRole;
    }

    /// <summary>The member whose role changes.</summary>
    public TeamMembersPerson Member { get; }

    /// <summary>The role key before the change.</summary>
    public string? OldRole { get; }

    /// <summary>The role key chosen.</summary>
    public string? NewRole { get; }
}

/// <summary>The server-side actions of <see cref="TeamMembers"/>, named in <see cref="TeamMembersActionFailedEventArgs"/>.</summary>
public enum TeamMembersAction
{
    /// <summary>Sending the invites in the field.</summary>
    Send,

    /// <summary>Sending a pending invite again.</summary>
    Resend,

    /// <summary>Withdrawing a pending invite.</summary>
    Revoke,

    /// <summary>Removing a member.</summary>
    Remove,

    /// <summary>Changing a member's role.</summary>
    ChangeRole,
}

/// <summary>Arguments of <c>TeamMembers.InvitesSent</c>: the invites that went out (already in the list).</summary>
public sealed class TeamMembersInvitesSentEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TeamMembersInvitesSentEventArgs(RoutedEvent routedEvent, object source, IReadOnlyList<TeamMembersInvite> invites, string? role)
        : base(routedEvent, source)
    {
        Invites = invites;
        Role = role;
    }

    /// <summary>The new invites, in order.</summary>
    public IReadOnlyList<TeamMembersInvite> Invites { get; }

    /// <summary>The role key they were sent with.</summary>
    public string? Role { get; }
}

/// <summary>Arguments of <c>TeamMembers.InviteResent</c> and <c>TeamMembers.InviteRevoked</c>.</summary>
public sealed class TeamMembersInviteEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TeamMembersInviteEventArgs(RoutedEvent routedEvent, object source, TeamMembersInvite invite)
        : base(routedEvent, source)
    {
        Invite = invite;
    }

    /// <summary>The invite.</summary>
    public TeamMembersInvite Invite { get; }
}

/// <summary>Arguments of <c>TeamMembers.MemberRemoved</c>: the member was removed (and is no longer in the list).</summary>
public sealed class TeamMembersMemberEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TeamMembersMemberEventArgs(RoutedEvent routedEvent, object source, TeamMembersPerson member)
        : base(routedEvent, source)
    {
        Member = member;
    }

    /// <summary>The member.</summary>
    public TeamMembersPerson Member { get; }
}

/// <summary>Arguments of <c>TeamMembers.MemberRoleChanged</c>.</summary>
public sealed class TeamMembersRoleChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TeamMembersRoleChangedEventArgs(RoutedEvent routedEvent, object source, TeamMembersPerson member, string? oldRole, string? newRole)
        : base(routedEvent, source)
    {
        Member = member;
        OldRole = oldRole;
        NewRole = newRole;
    }

    /// <summary>The member.</summary>
    public TeamMembersPerson Member { get; }

    /// <summary>The previous role key.</summary>
    public string? OldRole { get; }

    /// <summary>The new role key.</summary>
    public string? NewRole { get; }
}

/// <summary>Arguments of <c>TeamMembers.ActionFailed</c>: an action's handler threw; the block shows the error and keeps the old state.</summary>
public sealed class TeamMembersActionFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TeamMembersActionFailedEventArgs(RoutedEvent routedEvent, object source, TeamMembersAction action, object? subject,
        Exception exception, string message)
        : base(routedEvent, source)
    {
        Action = action;
        Subject = subject;
        Exception = exception;
        Message = message;
    }

    /// <summary>Which action failed.</summary>
    public TeamMembersAction Action { get; }

    /// <summary>
    /// What it was about: the <see cref="TeamMembersSendRequest"/>, the <see cref="TeamMembersInvite"/>, the
    /// <see cref="TeamMembersPerson"/> or the <see cref="TeamMembersRoleChange"/>.
    /// </summary>
    public object? Subject { get; }

    /// <summary>The exception the handler threw.</summary>
    public Exception Exception { get; }

    /// <summary>The error text the block shows.</summary>
    public string Message { get; }
}
