using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The members page of a workspace's settings. A header (title, a line of text and a seat meter: "7 of 10 seats" over a thin
/// bar that turns to the warning color when every seat is taken); an invite form where typed or pasted addresses become
/// chips (Enter, comma, semicolon or space commits; Backspace on an empty field removes the last chip; invalid text stays
/// with an error, duplicates drop silently, members and pending invites drop with an error), a role picker and a send
/// button that counts the invites and refuses to go over the free seats; the members (avatar, name, email, last active,
/// role menu, remove with an inline confirmation) and the pending invites (resend, revoke), both filtered by one search;
/// and a footer that reports the last action. Sending flies each address from its chip into a new pending row while the
/// seat count rolls. Every server call is an async handler you supply; a handler that throws keeps the old state and
/// shows the error.
/// <code>
/// &lt;arc:TeamMembers Title="Members" Description="Choose who can use this workspace." WorkspaceName="Northwind Lab"
///                  SeatLimit="10" MembersSource="{Binding Members}" InvitesSource="{Binding Invites}"
///                  SendInvitesAction="{Binding Send}" /&gt;
/// </code>
/// </summary>
public partial class TeamMembers : TemplatedControl
{
    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<TeamMembers, string?>(nameof(Title), "Members");

    /// <summary>The line under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<TeamMembers, string?>(nameof(Description));

    /// <summary>The workspace's name, used in the removal confirmation and its status line; null leaves it out of them.</summary>
    public static readonly StyledProperty<string?> WorkspaceNameProperty =
        AvaloniaProperty.Register<TeamMembers, string?>(nameof(WorkspaceName));

    /// <summary>The members, when you give them as a list to follow; it wins over <see cref="Members"/>. When it is a writable list the block removes members from it.</summary>
    public static readonly StyledProperty<IEnumerable?> MembersSourceProperty =
        AvaloniaProperty.Register<TeamMembers, IEnumerable?>(nameof(MembersSource));

    /// <summary>The pending invites, when you give them as a list to follow; it wins over <see cref="Invites"/>. When it is a writable list the block adds and removes invites in it.</summary>
    public static readonly StyledProperty<IEnumerable?> InvitesSourceProperty =
        AvaloniaProperty.Register<TeamMembers, IEnumerable?>(nameof(InvitesSource));

    /// <summary>How many seats the workspace has; members and pending invites each take one.</summary>
    public static readonly StyledProperty<int> SeatLimitProperty =
        AvaloniaProperty.Register<TeamMembers, int>(nameof(SeatLimit), 10);

    /// <summary>The role key new invites get (two-way; the picker next to the invite field writes it).</summary>
    public static readonly StyledProperty<string?> InviteRoleProperty =
        AvaloniaProperty.Register<TeamMembers, string?>(nameof(InviteRole), "member", defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The search text (two-way); it filters members and invites by name or email.</summary>
    public static readonly StyledProperty<string> SearchTextProperty =
        AvaloniaProperty.Register<TeamMembers, string>(nameof(SearchText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Every word the block shows or announces; replace it to translate or reword.</summary>
    public static readonly StyledProperty<TeamMembersLabels> LabelsProperty =
        AvaloniaProperty.Register<TeamMembers, TeamMembersLabels>(nameof(Labels), new TeamMembersLabels());

    /// <summary>Draws the block as a bordered card with rounded corners (true); false lays it flat at the container's width.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsFramed), true);

    /// <summary>Whether the invite form shows.</summary>
    public static readonly StyledProperty<bool> IsInviteVisibleProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsInviteVisible), true);

    /// <summary>Whether the search field shows in the members section's head.</summary>
    public static readonly StyledProperty<bool> IsSearchVisibleProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsSearchVisible), true);

    /// <summary>Whether the seat meter shows in the header.</summary>
    public static readonly StyledProperty<bool> IsSeatMeterVisibleProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsSeatMeterVisible), true);

    /// <summary>Whether the footer with the status line shows (the status is announced either way).</summary>
    public static readonly StyledProperty<bool> IsFooterVisibleProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsFooterVisible), true);

    /// <summary>
    /// A view for people who cannot manage the team: no invite form, roles as plain text, no remove, resend or revoke
    /// buttons. The search still works.
    /// </summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<TeamMembers, bool>(nameof(IsReadOnly));

    /// <summary>
    /// Sends the invites: awaited when Send is pressed (or <see cref="SendInvitesAsync"/> is called) while the button spins.
    /// An exception keeps the chips and shows its message; a cancellation keeps the chips silently. Null sends at once.
    /// </summary>
    public static readonly StyledProperty<Func<TeamMembersSendRequest, CancellationToken, Task>?> SendInvitesActionProperty =
        AvaloniaProperty.Register<TeamMembers, Func<TeamMembersSendRequest, CancellationToken, Task>?>(nameof(SendInvitesAction));

    /// <summary>Sends a pending invite again while its Resend button spins; an exception shows an error in the footer. Null resends at once.</summary>
    public static readonly StyledProperty<Func<TeamMembersInvite, CancellationToken, Task>?> ResendInviteActionProperty =
        AvaloniaProperty.Register<TeamMembers, Func<TeamMembersInvite, CancellationToken, Task>?>(nameof(ResendInviteAction));

    /// <summary>Withdraws a pending invite while its Revoke button spins; an exception keeps the row. Null withdraws at once.</summary>
    public static readonly StyledProperty<Func<TeamMembersInvite, CancellationToken, Task>?> RevokeInviteActionProperty =
        AvaloniaProperty.Register<TeamMembers, Func<TeamMembersInvite, CancellationToken, Task>?>(nameof(RevokeInviteAction));

    /// <summary>Removes a member after the inline confirmation, while Remove spins; an exception keeps the row. Null removes at once.</summary>
    public static readonly StyledProperty<Func<TeamMembersPerson, CancellationToken, Task>?> RemoveMemberActionProperty =
        AvaloniaProperty.Register<TeamMembers, Func<TeamMembersPerson, CancellationToken, Task>?>(nameof(RemoveMemberAction));

    /// <summary>Saves a role change; the new role shows at once and goes back if it throws. Null keeps the change at once.</summary>
    public static readonly StyledProperty<Func<TeamMembersRoleChange, CancellationToken, Task>?> ChangeRoleActionProperty =
        AvaloniaProperty.Register<TeamMembers, Func<TeamMembersRoleChange, CancellationToken, Task>?>(nameof(ChangeRoleAction));

    /// <summary>Turns a handler's exception into the error text; null uses the exception's message (or a built-in text when it has none).</summary>
    public static readonly StyledProperty<Func<Exception, string>?> ErrorFormatterProperty =
        AvaloniaProperty.Register<TeamMembers, Func<Exception, string>?>(nameof(ErrorFormatter));

    /// <summary>Replaces the built-in address check (one @, a dotted domain, a last part of at least two letters).</summary>
    public static readonly StyledProperty<Func<string, bool>?> EmailValidatorProperty =
        AvaloniaProperty.Register<TeamMembers, Func<string, bool>?>(nameof(EmailValidator));

    /// <summary>Run after invites went out, with the <see cref="TeamMembersInvitesSentEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> InvitesSentCommandProperty =
        AvaloniaProperty.Register<TeamMembers, ICommand?>(nameof(InvitesSentCommand));

    /// <summary>Run after an invite was sent again, with the <see cref="TeamMembersInviteEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> InviteResentCommandProperty =
        AvaloniaProperty.Register<TeamMembers, ICommand?>(nameof(InviteResentCommand));

    /// <summary>Run after an invite was withdrawn, with the <see cref="TeamMembersInviteEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> InviteRevokedCommandProperty =
        AvaloniaProperty.Register<TeamMembers, ICommand?>(nameof(InviteRevokedCommand));

    /// <summary>Run after a member was removed, with the <see cref="TeamMembersMemberEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> MemberRemovedCommandProperty =
        AvaloniaProperty.Register<TeamMembers, ICommand?>(nameof(MemberRemovedCommand));

    /// <summary>Run after a role change was saved, with the <see cref="TeamMembersRoleChangedEventArgs"/> as the parameter.</summary>
    public static readonly StyledProperty<ICommand?> MemberRoleChangedCommandProperty =
        AvaloniaProperty.Register<TeamMembers, ICommand?>(nameof(MemberRoleChangedCommand));

    /// <summary>Seats taken: members plus pending invites.</summary>
    public static readonly DirectProperty<TeamMembers, int> SeatsUsedProperty =
        AvaloniaProperty.RegisterDirect<TeamMembers, int>(nameof(SeatsUsed), o => o.SeatsUsed);

    /// <summary>Seats free: <see cref="SeatLimit"/> minus <see cref="SeatsUsed"/> (never below zero).</summary>
    public static readonly DirectProperty<TeamMembers, int> SeatsRemainingProperty =
        AvaloniaProperty.RegisterDirect<TeamMembers, int>(nameof(SeatsRemaining), o => o.SeatsRemaining);

    /// <summary>How many invites Send would send now: the chips plus a valid address being typed.</summary>
    public static readonly DirectProperty<TeamMembers, int> SendCountProperty =
        AvaloniaProperty.RegisterDirect<TeamMembers, int>(nameof(SendCount), o => o.SendCount);

    /// <summary>True while the send handler runs.</summary>
    public static readonly DirectProperty<TeamMembers, bool> IsSendingProperty =
        AvaloniaProperty.RegisterDirect<TeamMembers, bool>(nameof(IsSending), o => o.IsSending);

    /// <summary>The footer's text: the result of the last action (it stays until the next one).</summary>
    public static readonly DirectProperty<TeamMembers, string> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<TeamMembers, string>(nameof(StatusText), o => o.StatusText);

    #endregion

    #region Events

    /// <summary>Raised when invites went out (they are in the list already).</summary>
    public static readonly RoutedEvent<TeamMembersInvitesSentEventArgs> InvitesSentEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersInvitesSentEventArgs>(nameof(InvitesSent), RoutingStrategies.Bubble);

    /// <summary>Raised when an invite was sent again.</summary>
    public static readonly RoutedEvent<TeamMembersInviteEventArgs> InviteResentEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersInviteEventArgs>(nameof(InviteResent), RoutingStrategies.Bubble);

    /// <summary>Raised when an invite was withdrawn.</summary>
    public static readonly RoutedEvent<TeamMembersInviteEventArgs> InviteRevokedEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersInviteEventArgs>(nameof(InviteRevoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a member was removed.</summary>
    public static readonly RoutedEvent<TeamMembersMemberEventArgs> MemberRemovedEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersMemberEventArgs>(nameof(MemberRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when a role change was saved.</summary>
    public static readonly RoutedEvent<TeamMembersRoleChangedEventArgs> MemberRoleChangedEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersRoleChangedEventArgs>(nameof(MemberRoleChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a handler threw (the block keeps the old state and shows the error).</summary>
    public static readonly RoutedEvent<TeamMembersActionFailedEventArgs> ActionFailedEvent =
        RoutedEvent.Register<TeamMembers, TeamMembersActionFailedEventArgs>(nameof(ActionFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when invites went out (they are in the list already).</summary>
    public event EventHandler<TeamMembersInvitesSentEventArgs>? InvitesSent
    {
        add => AddHandler(InvitesSentEvent, value);
        remove => RemoveHandler(InvitesSentEvent, value);
    }

    /// <summary>Raised when an invite was sent again.</summary>
    public event EventHandler<TeamMembersInviteEventArgs>? InviteResent
    {
        add => AddHandler(InviteResentEvent, value);
        remove => RemoveHandler(InviteResentEvent, value);
    }

    /// <summary>Raised when an invite was withdrawn.</summary>
    public event EventHandler<TeamMembersInviteEventArgs>? InviteRevoked
    {
        add => AddHandler(InviteRevokedEvent, value);
        remove => RemoveHandler(InviteRevokedEvent, value);
    }

    /// <summary>Raised when a member was removed.</summary>
    public event EventHandler<TeamMembersMemberEventArgs>? MemberRemoved
    {
        add => AddHandler(MemberRemovedEvent, value);
        remove => RemoveHandler(MemberRemovedEvent, value);
    }

    /// <summary>Raised when a role change was saved.</summary>
    public event EventHandler<TeamMembersRoleChangedEventArgs>? MemberRoleChanged
    {
        add => AddHandler(MemberRoleChangedEvent, value);
        remove => RemoveHandler(MemberRoleChangedEvent, value);
    }

    /// <summary>Raised when a handler threw (the block keeps the old state and shows the error).</summary>
    public event EventHandler<TeamMembersActionFailedEventArgs>? ActionFailed
    {
        add => AddHandler(ActionFailedEvent, value);
        remove => RemoveHandler(ActionFailedEvent, value);
    }

    #endregion

    private int _seatsUsed;
    private int _seatsRemaining;
    private int _sendCount;
    private bool _isSending;
    private string _statusText = string.Empty;

    /// <summary>The members, in order, when no <see cref="MembersSource"/> is set.</summary>
    [Content]
    public AvaloniaList<TeamMembersPerson> Members { get; } = new();

    /// <summary>The pending invites, in order, when no <see cref="InvitesSource"/> is set.</summary>
    public AvaloniaList<TeamMembersInvite> Invites { get; } = new();

    /// <summary>The roles people can be given (owners excepted); empty uses Admin, Member and Viewer.</summary>
    public AvaloniaList<TeamMembersRoleOption> Roles { get; } = new();

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="WorkspaceNameProperty"/>
    public string? WorkspaceName { get => GetValue(WorkspaceNameProperty); set => SetValue(WorkspaceNameProperty, value); }

    /// <inheritdoc cref="MembersSourceProperty"/>
    public IEnumerable? MembersSource { get => GetValue(MembersSourceProperty); set => SetValue(MembersSourceProperty, value); }

    /// <inheritdoc cref="InvitesSourceProperty"/>
    public IEnumerable? InvitesSource { get => GetValue(InvitesSourceProperty); set => SetValue(InvitesSourceProperty, value); }

    /// <inheritdoc cref="SeatLimitProperty"/>
    public int SeatLimit { get => GetValue(SeatLimitProperty); set => SetValue(SeatLimitProperty, value); }

    /// <inheritdoc cref="InviteRoleProperty"/>
    public string? InviteRole { get => GetValue(InviteRoleProperty); set => SetValue(InviteRoleProperty, value); }

    /// <inheritdoc cref="SearchTextProperty"/>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TeamMembersLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="IsInviteVisibleProperty"/>
    public bool IsInviteVisible { get => GetValue(IsInviteVisibleProperty); set => SetValue(IsInviteVisibleProperty, value); }

    /// <inheritdoc cref="IsSearchVisibleProperty"/>
    public bool IsSearchVisible { get => GetValue(IsSearchVisibleProperty); set => SetValue(IsSearchVisibleProperty, value); }

    /// <inheritdoc cref="IsSeatMeterVisibleProperty"/>
    public bool IsSeatMeterVisible { get => GetValue(IsSeatMeterVisibleProperty); set => SetValue(IsSeatMeterVisibleProperty, value); }

    /// <inheritdoc cref="IsFooterVisibleProperty"/>
    public bool IsFooterVisible { get => GetValue(IsFooterVisibleProperty); set => SetValue(IsFooterVisibleProperty, value); }

    /// <inheritdoc cref="IsReadOnlyProperty"/>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <inheritdoc cref="SendInvitesActionProperty"/>
    public Func<TeamMembersSendRequest, CancellationToken, Task>? SendInvitesAction
    {
        get => GetValue(SendInvitesActionProperty);
        set => SetValue(SendInvitesActionProperty, value);
    }

    /// <inheritdoc cref="ResendInviteActionProperty"/>
    public Func<TeamMembersInvite, CancellationToken, Task>? ResendInviteAction
    {
        get => GetValue(ResendInviteActionProperty);
        set => SetValue(ResendInviteActionProperty, value);
    }

    /// <inheritdoc cref="RevokeInviteActionProperty"/>
    public Func<TeamMembersInvite, CancellationToken, Task>? RevokeInviteAction
    {
        get => GetValue(RevokeInviteActionProperty);
        set => SetValue(RevokeInviteActionProperty, value);
    }

    /// <inheritdoc cref="RemoveMemberActionProperty"/>
    public Func<TeamMembersPerson, CancellationToken, Task>? RemoveMemberAction
    {
        get => GetValue(RemoveMemberActionProperty);
        set => SetValue(RemoveMemberActionProperty, value);
    }

    /// <inheritdoc cref="ChangeRoleActionProperty"/>
    public Func<TeamMembersRoleChange, CancellationToken, Task>? ChangeRoleAction
    {
        get => GetValue(ChangeRoleActionProperty);
        set => SetValue(ChangeRoleActionProperty, value);
    }

    /// <inheritdoc cref="ErrorFormatterProperty"/>
    public Func<Exception, string>? ErrorFormatter { get => GetValue(ErrorFormatterProperty); set => SetValue(ErrorFormatterProperty, value); }

    /// <inheritdoc cref="EmailValidatorProperty"/>
    public Func<string, bool>? EmailValidator { get => GetValue(EmailValidatorProperty); set => SetValue(EmailValidatorProperty, value); }

    /// <inheritdoc cref="InvitesSentCommandProperty"/>
    public ICommand? InvitesSentCommand { get => GetValue(InvitesSentCommandProperty); set => SetValue(InvitesSentCommandProperty, value); }

    /// <inheritdoc cref="InviteResentCommandProperty"/>
    public ICommand? InviteResentCommand { get => GetValue(InviteResentCommandProperty); set => SetValue(InviteResentCommandProperty, value); }

    /// <inheritdoc cref="InviteRevokedCommandProperty"/>
    public ICommand? InviteRevokedCommand { get => GetValue(InviteRevokedCommandProperty); set => SetValue(InviteRevokedCommandProperty, value); }

    /// <inheritdoc cref="MemberRemovedCommandProperty"/>
    public ICommand? MemberRemovedCommand { get => GetValue(MemberRemovedCommandProperty); set => SetValue(MemberRemovedCommandProperty, value); }

    /// <inheritdoc cref="MemberRoleChangedCommandProperty"/>
    public ICommand? MemberRoleChangedCommand
    {
        get => GetValue(MemberRoleChangedCommandProperty);
        set => SetValue(MemberRoleChangedCommandProperty, value);
    }

    /// <inheritdoc cref="SeatsUsedProperty"/>
    public int SeatsUsed { get => _seatsUsed; private set => SetAndRaise(SeatsUsedProperty, ref _seatsUsed, value); }

    /// <inheritdoc cref="SeatsRemainingProperty"/>
    public int SeatsRemaining { get => _seatsRemaining; private set => SetAndRaise(SeatsRemainingProperty, ref _seatsRemaining, value); }

    /// <inheritdoc cref="SendCountProperty"/>
    public int SendCount { get => _sendCount; private set => SetAndRaise(SendCountProperty, ref _sendCount, value); }

    /// <inheritdoc cref="IsSendingProperty"/>
    public bool IsSending { get => _isSending; private set => SetAndRaise(IsSendingProperty, ref _isSending, value); }

    /// <inheritdoc cref="StatusTextProperty"/>
    public string StatusText { get => _statusText; private set => SetAndRaise(StatusTextProperty, ref _statusText, value); }
}
