using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>What someone may do with the shared document.</summary>
public enum ShareAccessRole
{
    /// <summary>Can open and read.</summary>
    Viewer,

    /// <summary>Can read and leave comments.</summary>
    Commenter,

    /// <summary>Can change the document and share it.</summary>
    Editor,

    /// <summary>Owns the document; the role cannot be changed or removed here.</summary>
    Owner
}

/// <summary>Who can open the document with its link, besides the people listed.</summary>
public enum ShareAccessLinkScope
{
    /// <summary>Only the people with access.</summary>
    Restricted,

    /// <summary>Anyone signed in to the organization.</summary>
    Organization,

    /// <summary>Anyone who has the link.</summary>
    Anyone
}

/// <summary>A person with access to the document, shown as one row of a <see cref="ShareAccess"/>.</summary>
public class ShareAccessPerson : AvaloniaObject
{
    /// <summary>The person's name, or null to show the email address alone.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<ShareAccessPerson, string?>(nameof(Name));

    /// <summary>The person's email address; it identifies them (compared without regard to case).</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<ShareAccessPerson, string>(nameof(Email), string.Empty);

    /// <summary>A picture for the avatar, or null for initials.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<ShareAccessPerson, ImageSource?>(nameof(Avatar));

    /// <summary>What the person may do.</summary>
    public static readonly StyledProperty<ShareAccessRole> RoleProperty =
        AvaloniaProperty.Register<ShareAccessPerson, ShareAccessRole>(nameof(Role));

    /// <summary>True while the invitation has not been accepted yet; the row says so under the address.</summary>
    public static readonly StyledProperty<bool> IsPendingProperty =
        AvaloniaProperty.Register<ShareAccessPerson, bool>(nameof(IsPending));

    /// <inheritdoc cref="NameProperty"/>
    public string? Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email
    {
        get => GetValue(EmailProperty);
        set => SetValue(EmailProperty, value);
    }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar
    {
        get => GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    /// <inheritdoc cref="RoleProperty"/>
    public ShareAccessRole Role
    {
        get => GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    /// <inheritdoc cref="IsPendingProperty"/>
    public bool IsPending
    {
        get => GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
    }

    internal event Action<ShareAccessPerson>? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this);
    }
}

/// <summary>Arguments of <see cref="ShareAccess.PeopleInvited"/>: the addresses invited and the role they were given.</summary>
public sealed class ShareAccessInvitedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShareAccessInvitedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ShareAccessPerson> people, ShareAccessRole role)
        : base(routedEvent)
    {
        People = people;
        Role = role;
    }

    /// <summary>The people added to the list, in the order they were typed.</summary>
    public IReadOnlyList<ShareAccessPerson> People { get; }

    /// <summary>The role they were invited with.</summary>
    public ShareAccessRole Role { get; }
}

/// <summary>Arguments of <see cref="ShareAccess.RoleChanged"/>.</summary>
public sealed class ShareAccessRoleChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShareAccessRoleChangedEventArgs(RoutedEvent routedEvent, ShareAccessPerson person, ShareAccessRole oldRole, ShareAccessRole newRole)
        : base(routedEvent)
    {
        Person = person;
        OldRole = oldRole;
        NewRole = newRole;
    }

    /// <summary>The person whose role changed.</summary>
    public ShareAccessPerson Person { get; }

    /// <summary>The role before.</summary>
    public ShareAccessRole OldRole { get; }

    /// <summary>The role now.</summary>
    public ShareAccessRole NewRole { get; }
}

/// <summary>Arguments of <see cref="ShareAccess.AccessRemoved"/>.</summary>
public sealed class ShareAccessRemovedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShareAccessRemovedEventArgs(RoutedEvent routedEvent, ShareAccessPerson person)
        : base(routedEvent)
    {
        Person = person;
    }

    /// <summary>The person who no longer has access (already taken out of <see cref="ShareAccess.People"/>).</summary>
    public ShareAccessPerson Person { get; }
}

/// <summary>Arguments of <see cref="ShareAccess.LinkAccessChanged"/>.</summary>
public sealed class ShareAccessLinkChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ShareAccessLinkChangedEventArgs(RoutedEvent routedEvent, ShareAccessLinkScope scope, ShareAccessRole role)
        : base(routedEvent)
    {
        Scope = scope;
        Role = role;
    }

    /// <summary>Who can open the link now.</summary>
    public ShareAccessLinkScope Scope { get; }

    /// <summary>What they may do once they open it (ignored while <see cref="Scope"/> is Restricted).</summary>
    public ShareAccessRole Role { get; }
}

/// <summary>
/// Every word a <see cref="ShareAccess"/> shows or announces, in English by default. Replace the instance or set its
/// properties to translate; texts with names in them are functions so the word order can change.
/// </summary>
public sealed class ShareAccessLabels
{
    /// <summary>The dialog's heading: the document's name, "Share “Trail notes”".</summary>
    public Func<string, string> Title { get; set; } = name => $"Share “{name}”";

    /// <summary>The name of the address field.</summary>
    public string InviteField { get; set; } = "Add people by email";

    /// <summary>The placeholder of the address field.</summary>
    public string InvitePlaceholder { get; set; } = "Add people by email";

    /// <summary>The name of the role picker beside the address field.</summary>
    public string InviteRole { get; set; } = "Role for new people";

    /// <summary>The invite button.</summary>
    public string Invite { get; set; } = "Invite";

    /// <summary>The heading over the list of people.</summary>
    public string People { get; set; } = "People with access";

    /// <summary>The heading over the link settings.</summary>
    public string General { get; set; } = "General access";

    /// <summary>The name of a role.</summary>
    public Func<ShareAccessRole, string> Role { get; set; } = role => role switch
    {
        ShareAccessRole.Viewer => "Viewer",
        ShareAccessRole.Commenter => "Commenter",
        ShareAccessRole.Editor => "Editor",
        _ => "Owner"
    };

    /// <summary>The name of a link scope; the organization's name is passed in.</summary>
    public Func<ShareAccessLinkScope, string, string> Scope { get; set; } = (scope, organization) => scope switch
    {
        ShareAccessLinkScope.Restricted => "Restricted",
        ShareAccessLinkScope.Organization => $"Anyone at {organization}",
        _ => "Anyone with the link"
    };

    /// <summary>The line under the link scope; the organization's name is passed in.</summary>
    public Func<ShareAccessLinkScope, string, string> ScopeDescription { get; set; } = (scope, organization) => scope switch
    {
        ShareAccessLinkScope.Restricted => "Only people with access can open it with the link",
        ShareAccessLinkScope.Organization => $"Anyone signed in at {organization} can open it with the link",
        _ => "Anyone on the internet with the link can open it"
    };

    /// <summary>The name of the link scope picker.</summary>
    public string ScopePicker { get; set; } = "Who can open the link";

    /// <summary>The name of the link role picker.</summary>
    public string LinkRolePicker { get; set; } = "What people with the link can do";

    /// <summary>The menu entry that takes someone's access away.</summary>
    public string Remove { get; set; } = "Remove access";

    /// <summary>The name of a person's role button: the person and the role, "Role of Mira Holt: Editor".</summary>
    public Func<string, string, string> RoleButton { get; set; } = (person, role) => $"Role of {person}: {role}";

    /// <summary>Added after the current user's name.</summary>
    public string You { get; set; } = "(you)";

    /// <summary>The line under someone invited who has not joined yet.</summary>
    public string Pending { get; set; } = "Invite sent";

    /// <summary>The copy link button.</summary>
    public string CopyLink { get; set; } = "Copy link";

    /// <summary>The copy link button after a copy.</summary>
    public string Copied { get; set; } = "Link copied";

    /// <summary>The done button.</summary>
    public string Done { get; set; } = "Done";

    /// <summary>The message for an address that is not one: the text typed.</summary>
    public Func<string, string> NotAnEmail { get; set; } = text => $"“{text}” is not an email address";

    /// <summary>The message for someone who already has access: their address.</summary>
    public Func<string, string> AlreadyShared { get; set; } = email => $"{email} already has access";

    /// <summary>Announced after an invite: how many people and the role.</summary>
    public Func<int, string, string> InvitedAnnouncement { get; set; } =
        (count, role) => count == 1 ? $"Invited 1 person as {role}" : $"Invited {count} people as {role}";

    /// <summary>Announced after a role change: the person and the new role.</summary>
    public Func<string, string, string> RoleAnnouncement { get; set; } = (person, role) => $"{person} is now {role}";

    /// <summary>Announced after someone's access was removed.</summary>
    public Func<string, string> RemovedAnnouncement { get; set; } = person => $"Removed access for {person}";

    /// <summary>Announced when the invite handler failed: its message.</summary>
    public Func<string, string> InviteFailed { get; set; } = message => $"Could not send the invite. {message}";
}
