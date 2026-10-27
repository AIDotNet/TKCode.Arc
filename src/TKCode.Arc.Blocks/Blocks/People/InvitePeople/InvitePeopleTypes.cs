using AtomUI.Controls;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A person <see cref="InvitePeople"/> knows: someone in the workspace directory, a member of the team, or the person an
/// invite went to. People are matched by <see cref="Email"/> (case-insensitive), so the same address in
/// <see cref="InvitePeople.Directory"/> and <see cref="InvitePeople.Members"/> is the same person.
/// </summary>
public class InvitePeoplePerson : AvaloniaObject
{
    /// <summary>The full name ("Clara Weiss"); suggestions match it as a substring, a pasted full name finds the person.</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<InvitePeoplePerson, string?>(nameof(Name));

    /// <summary>The email address; the person's identity and what an invite is sent to.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<InvitePeoplePerson, string?>(nameof(Email));

    /// <summary>The job title shown at the end of a suggestion ("Brand designer"); it is not searched.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InvitePeoplePerson, string?>(nameof(Title));

    /// <summary>The portrait; null shows the initials on the muted surface.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<InvitePeoplePerson, ImageSource?>(nameof(Avatar));

    /// <summary>Creates an empty person (set the properties in XAML).</summary>
    public InvitePeoplePerson()
    {
    }

    /// <summary>Creates a person with a name, an email address and an optional title and portrait.</summary>
    public InvitePeoplePerson(string? name, string? email, string? title = null, ImageSource? avatar = null)
    {
        Name = name;
        Email = email;
        Title = title;
        Avatar = avatar;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <summary>The name, or the email address when there is no name.</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name! : Email ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

/// <summary>A role an invited person can get (a line of the role menu).</summary>
public class InvitePeopleRole : AvaloniaObject
{
    /// <summary>The role's name, shown on the chip's role button and in the pending row ("Member").</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<InvitePeopleRole, string?>(nameof(Name));

    /// <summary>The second line of the role menu (what the role can do).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InvitePeopleRole, string?>(nameof(Description));

    /// <summary>True for the role new chips start with (the first role marked default wins; none marked = the first role).</summary>
    public static readonly StyledProperty<bool> IsDefaultProperty =
        AvaloniaProperty.Register<InvitePeopleRole, bool>(nameof(IsDefault));

    /// <summary>Creates an empty role (set the properties in XAML).</summary>
    public InvitePeopleRole()
    {
    }

    /// <summary>Creates a role with a name and a description.</summary>
    public InvitePeopleRole(string name, string? description = null, bool isDefault = false)
    {
        Name = name;
        Description = description;
        IsDefault = isDefault;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsDefaultProperty"/>
    public bool IsDefault { get => GetValue(IsDefaultProperty); set => SetValue(IsDefaultProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}

/// <summary>A plan with a seat limit. <see cref="InvitePeople.Plans"/> lists them smallest first: the upgrade path.</summary>
public class InvitePeoplePlan : AvaloniaObject
{
    /// <summary>The plan's name ("Basic"); the seat line says "{name} plan".</summary>
    public static readonly StyledProperty<string?> NameProperty =
        AvaloniaProperty.Register<InvitePeoplePlan, string?>(nameof(Name));

    /// <summary>How many seats the plan has: members, pending invites and new invites together.</summary>
    public static readonly StyledProperty<int> SeatLimitProperty =
        AvaloniaProperty.Register<InvitePeoplePlan, int>(nameof(SeatLimit), 10);

    /// <summary>Creates an empty plan (set the properties in XAML).</summary>
    public InvitePeoplePlan()
    {
    }

    /// <summary>Creates a plan with a name and a seat limit.</summary>
    public InvitePeoplePlan(string name, int seatLimit)
    {
        Name = name;
        SeatLimit = seatLimit;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string? Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="SeatLimitProperty"/>
    public int SeatLimit { get => GetValue(SeatLimitProperty); set => SetValue(SeatLimitProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Name ?? string.Empty;
}

/// <summary>Where an invite in the pending list is.</summary>
public enum InvitePeopleInviteStatus
{
    /// <summary>Being sent (a spinner and "Sending").</summary>
    Sending,

    /// <summary>Delivered; the row says when (<see cref="InvitePeoplePendingInvite.SentText"/>, or "just now").</summary>
    Sent,

    /// <summary>Accepted: a check and "Joined"; the row leaves a moment later and the person joins the team.</summary>
    Joined
}

/// <summary>An invite waiting to be accepted: a row of the pending list. It takes a seat until it is revoked or accepted.</summary>
public class InvitePeoplePendingInvite : AvaloniaObject
{
    /// <summary>The invited person (a directory person, or one made from a typed address).</summary>
    public static readonly StyledProperty<InvitePeoplePerson?> PersonProperty =
        AvaloniaProperty.Register<InvitePeoplePendingInvite, InvitePeoplePerson?>(nameof(Person));

    /// <summary>The role the person will join with.</summary>
    public static readonly StyledProperty<InvitePeopleRole?> RoleProperty =
        AvaloniaProperty.Register<InvitePeoplePendingInvite, InvitePeopleRole?>(nameof(Role));

    /// <summary>Sending, sent or joined.</summary>
    public static readonly StyledProperty<InvitePeopleInviteStatus> StatusProperty =
        AvaloniaProperty.Register<InvitePeoplePendingInvite, InvitePeopleInviteStatus>(nameof(Status), InvitePeopleInviteStatus.Sent);

    /// <summary>When it was sent, in words ("2 days ago"); null says "just now". The row shows "Sent {SentText}".</summary>
    public static readonly StyledProperty<string?> SentTextProperty =
        AvaloniaProperty.Register<InvitePeoplePendingInvite, string?>(nameof(SentText));

    /// <summary>Creates an empty invite (set the properties in XAML).</summary>
    public InvitePeoplePendingInvite()
    {
    }

    /// <summary>Creates an invite for a person with a role, already sent at <paramref name="sentText"/>.</summary>
    public InvitePeoplePendingInvite(InvitePeoplePerson person, InvitePeopleRole? role, string? sentText = null,
                                     InvitePeopleInviteStatus status = InvitePeopleInviteStatus.Sent)
    {
        Person = person;
        Role = role;
        SentText = sentText;
        Status = status;
    }

    /// <inheritdoc cref="PersonProperty"/>
    public InvitePeoplePerson? Person { get => GetValue(PersonProperty); set => SetValue(PersonProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public InvitePeopleRole? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public InvitePeopleInviteStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="SentTextProperty"/>
    public string? SentText { get => GetValue(SentTextProperty); set => SetValue(SentTextProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Person?.DisplayName ?? string.Empty;
}

/// <summary>Why a typed or pasted entry cannot be invited.</summary>
public enum InvitePeopleEntryError
{
    /// <summary>No problem: the entry can be invited.</summary>
    None,

    /// <summary>There is no @ (a name that is not in the directory, a word).</summary>
    NotEmail,

    /// <summary>Nothing before the @.</summary>
    MissingLocal,

    /// <summary>Nothing after the @.</summary>
    MissingDomain,

    /// <summary>The domain has no dot ("nadia@lumenlabs").</summary>
    IncompleteDomain,

    /// <summary>Anything else that is not a valid address (spaces, two @, a one-letter ending, a <c>mailto:</c> prefix).</summary>
    Invalid,

    /// <summary>The person is already on the team.</summary>
    AlreadyMember,

    /// <summary>The person already has a pending invite.</summary>
    AlreadyInvited
}

/// <summary>
/// A chip in the field: a person to invite with a role, or an entry that cannot be invited (with the text as typed and
/// the reason). Selecting an invalid chip puts its text back into the field for editing.
/// </summary>
public sealed class InvitePeopleEntry : AvaloniaObject
{
    /// <summary>The role the person will be invited with (valid entries only).</summary>
    public static readonly StyledProperty<InvitePeopleRole?> RoleProperty =
        AvaloniaProperty.Register<InvitePeopleEntry, InvitePeopleRole?>(nameof(Role));

    internal InvitePeopleEntry(InvitePeoplePerson? person, string text, InvitePeopleEntryError error, InvitePeopleRole? role)
    {
        Person = person;
        Text = text;
        Error = error;
        Role = role;
    }

    /// <summary>
    /// The person to invite (a directory person, or a new one made from the address). Entries blocked because the person
    /// is on the team or invited already keep the person; entries that are not an address have none.
    /// </summary>
    public InvitePeoplePerson? Person { get; }

    /// <summary>For a person the name (or the address when there is no name); otherwise the text as it was typed.</summary>
    public string Text { get; }

    /// <summary>Why the entry cannot be invited, or <see cref="InvitePeopleEntryError.None"/>.</summary>
    public InvitePeopleEntryError Error { get; }

    /// <summary>True when the entry can be invited.</summary>
    public bool IsValid => Error == InvitePeopleEntryError.None;

    /// <summary>The email address the invite goes to (valid entries), or null.</summary>
    public string? Email => Person?.Email;

    /// <inheritdoc cref="RoleProperty"/>
    public InvitePeopleRole? Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc />
    public override string ToString() => Text;
}
