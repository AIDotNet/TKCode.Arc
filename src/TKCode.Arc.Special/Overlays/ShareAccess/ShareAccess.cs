using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The share dialog of a document: invite people by email with a role, see everyone who has access and change or remove
/// their role, choose who else can open the link (only the people listed, anyone in the organization or anyone with the
/// link, each with its own role) and copy the link.
/// </summary>
/// <remarks>
/// The control is the dialog's content; show it in a <c>Dialog</c> or a <c>Popover</c>, or inline in a settings page.
/// Typed addresses become chips; anything that is not an address, or someone who already has access, is refused with a
/// message under the field. Invite adds the people to the list as "Invite sent" (after <see cref="InviteHandler"/> when
/// one is set; the button spins while it runs) and raises <see cref="PeopleInvited"/>. Role changes, removals and link
/// changes update <see cref="People"/>, <see cref="LinkScope"/> and <see cref="LinkRole"/> at once and raise their events,
/// so the app saves them; every change is also announced to assistive technology.
/// </remarks>
[TemplatePart(RootPart, typeof(Panel))]
[PseudoClasses(NarrowPseudoClass, InvitingPseudoClass)]
public partial class ShareAccess : TemplatedControl
{
    internal const string RootPart = "PART_Root";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string InvitingPseudoClass = ":inviting";

    /// <summary>The document's name, used in the heading.</summary>
    public static readonly StyledProperty<string> DocumentNameProperty =
        AvaloniaProperty.Register<ShareAccess, string>(nameof(DocumentName), "Untitled");

    /// <summary>The document's link, which the copy button copies.</summary>
    public static readonly StyledProperty<string?> LinkProperty =
        AvaloniaProperty.Register<ShareAccess, string?>(nameof(Link));

    /// <summary>The organization's name, used by the "Anyone at …" link scope.</summary>
    public static readonly StyledProperty<string> OrganizationNameProperty =
        AvaloniaProperty.Register<ShareAccess, string>(nameof(OrganizationName), "your organization");

    /// <summary>Who can open the link besides the people listed (two-way).</summary>
    public static readonly StyledProperty<ShareAccessLinkScope> LinkScopeProperty =
        AvaloniaProperty.Register<ShareAccess, ShareAccessLinkScope>(nameof(LinkScope), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What people who open the link may do (two-way). Owner is not allowed and reads back as Viewer.</summary>
    public static readonly StyledProperty<ShareAccessRole> LinkRoleProperty =
        AvaloniaProperty.Register<ShareAccess, ShareAccessRole>(nameof(LinkRole), defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, role) => role == ShareAccessRole.Owner ? ShareAccessRole.Viewer : role);

    /// <summary>The role the next invite gives (two-way). Owner is not allowed and reads back as Editor.</summary>
    public static readonly StyledProperty<ShareAccessRole> InviteRoleProperty =
        AvaloniaProperty.Register<ShareAccess, ShareAccessRole>(nameof(InviteRole), ShareAccessRole.Editor, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, role) => role == ShareAccessRole.Owner ? ShareAccessRole.Editor : role);

    /// <summary>The address of the person using the dialog; their row says "(you)".</summary>
    public static readonly StyledProperty<string?> CurrentUserEmailProperty =
        AvaloniaProperty.Register<ShareAccess, string?>(nameof(CurrentUserEmail));

    /// <summary>
    /// Sends the invites (for example to your server) before the people are added; the button spins while it runs. When it
    /// throws, the message shows under the field and the chips stay. Null adds the people at once.
    /// </summary>
    public static readonly StyledProperty<Func<IReadOnlyList<string>, ShareAccessRole, CancellationToken, Task>?> InviteHandlerProperty =
        AvaloniaProperty.Register<ShareAccess, Func<IReadOnlyList<string>, ShareAccessRole, CancellationToken, Task>?>(nameof(InviteHandler));

    /// <summary>The words the dialog shows and announces.</summary>
    public static readonly StyledProperty<ShareAccessLabels> LabelsProperty =
        AvaloniaProperty.Register<ShareAccess, ShareAccessLabels>(nameof(Labels), new ShareAccessLabels());

    /// <summary>Raised after people were invited and added to <see cref="People"/>.</summary>
    public static readonly RoutedEvent<ShareAccessInvitedEventArgs> PeopleInvitedEvent =
        RoutedEvent.Register<ShareAccess, ShareAccessInvitedEventArgs>(nameof(PeopleInvited), RoutingStrategies.Bubble);

    /// <summary>Raised after someone's role was changed from their row.</summary>
    public static readonly RoutedEvent<ShareAccessRoleChangedEventArgs> RoleChangedEvent =
        RoutedEvent.Register<ShareAccess, ShareAccessRoleChangedEventArgs>(nameof(RoleChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after someone's access was removed from their row.</summary>
    public static readonly RoutedEvent<ShareAccessRemovedEventArgs> AccessRemovedEvent =
        RoutedEvent.Register<ShareAccess, ShareAccessRemovedEventArgs>(nameof(AccessRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised after <see cref="LinkScope"/> or <see cref="LinkRole"/> changed.</summary>
    public static readonly RoutedEvent<ShareAccessLinkChangedEventArgs> LinkAccessChangedEvent =
        RoutedEvent.Register<ShareAccess, ShareAccessLinkChangedEventArgs>(nameof(LinkAccessChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the Done button is pressed; close the dialog here.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DoneRequestedEvent =
        RoutedEvent.Register<ShareAccess, RoutedEventArgs>(nameof(DoneRequested), RoutingStrategies.Bubble);

    private CancellationTokenSource? _inviteCancel;
    private bool _isInviting;

    static ShareAccess()
    {
        FocusableProperty.OverrideDefaultValue<ShareAccess>(false);
        ClipToBoundsProperty.OverrideDefaultValue<ShareAccess>(false);
    }

    public ShareAccess()
    {
        People.CollectionChanged += (_, _) => SyncRows(animate: true);
    }

    /// <summary>Everyone with access, the owner usually first. The list follows changes to the collection and to each person.</summary>
    public AvaloniaList<ShareAccessPerson> People { get; } = [];

    /// <inheritdoc cref="DocumentNameProperty"/>
    public string DocumentName
    {
        get => GetValue(DocumentNameProperty);
        set => SetValue(DocumentNameProperty, value);
    }

    /// <inheritdoc cref="LinkProperty"/>
    public string? Link
    {
        get => GetValue(LinkProperty);
        set => SetValue(LinkProperty, value);
    }

    /// <inheritdoc cref="OrganizationNameProperty"/>
    public string OrganizationName
    {
        get => GetValue(OrganizationNameProperty);
        set => SetValue(OrganizationNameProperty, value);
    }

    /// <inheritdoc cref="LinkScopeProperty"/>
    public ShareAccessLinkScope LinkScope
    {
        get => GetValue(LinkScopeProperty);
        set => SetValue(LinkScopeProperty, value);
    }

    /// <inheritdoc cref="LinkRoleProperty"/>
    public ShareAccessRole LinkRole
    {
        get => GetValue(LinkRoleProperty);
        set => SetValue(LinkRoleProperty, value);
    }

    /// <inheritdoc cref="InviteRoleProperty"/>
    public ShareAccessRole InviteRole
    {
        get => GetValue(InviteRoleProperty);
        set => SetValue(InviteRoleProperty, value);
    }

    /// <inheritdoc cref="CurrentUserEmailProperty"/>
    public string? CurrentUserEmail
    {
        get => GetValue(CurrentUserEmailProperty);
        set => SetValue(CurrentUserEmailProperty, value);
    }

    /// <inheritdoc cref="InviteHandlerProperty"/>
    public Func<IReadOnlyList<string>, ShareAccessRole, CancellationToken, Task>? InviteHandler
    {
        get => GetValue(InviteHandlerProperty);
        set => SetValue(InviteHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public ShareAccessLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>True while <see cref="InviteHandler"/> runs.</summary>
    public bool IsInviting => _isInviting;

    /// <inheritdoc cref="PeopleInvitedEvent"/>
    public event EventHandler<ShareAccessInvitedEventArgs>? PeopleInvited
    {
        add => AddHandler(PeopleInvitedEvent, value);
        remove => RemoveHandler(PeopleInvitedEvent, value);
    }

    /// <inheritdoc cref="RoleChangedEvent"/>
    public event EventHandler<ShareAccessRoleChangedEventArgs>? RoleChanged
    {
        add => AddHandler(RoleChangedEvent, value);
        remove => RemoveHandler(RoleChangedEvent, value);
    }

    /// <inheritdoc cref="AccessRemovedEvent"/>
    public event EventHandler<ShareAccessRemovedEventArgs>? AccessRemoved
    {
        add => AddHandler(AccessRemovedEvent, value);
        remove => RemoveHandler(AccessRemovedEvent, value);
    }

    /// <inheritdoc cref="LinkAccessChangedEvent"/>
    public event EventHandler<ShareAccessLinkChangedEventArgs>? LinkAccessChanged
    {
        add => AddHandler(LinkAccessChangedEvent, value);
        remove => RemoveHandler(LinkAccessChangedEvent, value);
    }

    /// <inheritdoc cref="DoneRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? DoneRequested
    {
        add => AddHandler(DoneRequestedEvent, value);
        remove => RemoveHandler(DoneRequestedEvent, value);
    }

    /// <summary>
    /// Invites <paramref name="emails"/> with <paramref name="role"/> (the chips typed in the field when null), as the
    /// Invite button does. Addresses that are not valid or already have access are skipped. Returns the people added.
    /// </summary>
    public async Task<IReadOnlyList<ShareAccessPerson>> InviteAsync(IEnumerable<string>? emails = null, ShareAccessRole? role = null)
    {
        var chosenRole = role ?? InviteRole;
        if (chosenRole == ShareAccessRole.Owner)
        {
            chosenRole = ShareAccessRole.Editor;
        }
        var source = emails?.ToList() ?? PendingAddresses();
        var addresses = new List<string>();
        foreach (var text in source)
        {
            if (ShareAccessEmail.Normalize(text) is { } email && FindPerson(email) is null
                && !addresses.Contains(email, StringComparer.OrdinalIgnoreCase))
            {
                addresses.Add(email);
            }
        }
        if (addresses.Count == 0 || _isInviting)
        {
            return [];
        }
        if (InviteHandler is { } handler)
        {
            _inviteCancel?.Cancel();
            _inviteCancel = new CancellationTokenSource();
            SetInviting(true);
            try
            {
                await handler(addresses, chosenRole, _inviteCancel.Token);
            }
            catch (OperationCanceledException)
            {
                return [];
            }
            catch (Exception ex)
            {
                ShowFieldMessage(Labels.InviteFailed(ex.Message));
                Announce(Labels.InviteFailed(ex.Message));
                return [];
            }
            finally
            {
                SetInviting(false);
            }
        }
        var added = new List<ShareAccessPerson>();
        foreach (var email in addresses)
        {
            var person = new ShareAccessPerson { Email = email, Role = chosenRole, IsPending = true };
            added.Add(person);
            People.Add(person);
        }
        ClearInviteField(addresses);
        Announce(Labels.InvitedAnnouncement(added.Count, Labels.Role(chosenRole)));
        RaiseEvent(new ShareAccessInvitedEventArgs(PeopleInvitedEvent, added, chosenRole));
        return added;
    }

    /// <summary>Gives <paramref name="person"/> the role <paramref name="role"/> (the owner's role never changes).</summary>
    public void ChangeRole(ShareAccessPerson person, ShareAccessRole role)
    {
        if (person.Role == ShareAccessRole.Owner || role == ShareAccessRole.Owner || person.Role == role || !People.Contains(person))
        {
            return;
        }
        var old = person.Role;
        person.Role = role;
        var name = string.IsNullOrWhiteSpace(person.Name) ? person.Email : person.Name!;
        Announce(Labels.RoleAnnouncement(name, Labels.Role(role)));
        RaiseEvent(new ShareAccessRoleChangedEventArgs(RoleChangedEvent, person, old, role));
    }

    /// <summary>Takes <paramref name="person"/> out of the list (the owner stays).</summary>
    public void RemovePerson(ShareAccessPerson person) => RemovePerson(person, fromKeyboard: false);

    internal void RemovePerson(ShareAccessPerson person, bool fromKeyboard)
    {
        if (person.Role == ShareAccessRole.Owner || !People.Contains(person))
        {
            return;
        }
        var index = People.IndexOf(person);
        People.Remove(person);
        var name = string.IsNullOrWhiteSpace(person.Name) ? person.Email : person.Name!;
        Announce(Labels.RemovedAnnouncement(name));
        RaiseEvent(new ShareAccessRemovedEventArgs(AccessRemovedEvent, person));
        if (fromKeyboard || _lastInputKeyboard)
        {
            Dispatcher.UIThread.Post(() => FocusAfterRemoval(index), DispatcherPriority.Input);
        }
    }

    /// <summary>Moves keyboard focus to the address field.</summary>
    public bool FocusInvite() => _invite?.FocusEditor(NavigationMethod.Tab) == true;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Teardown();
        if (e.NameScope.Find<Panel>(RootPart) is { } root)
        {
            Build(root);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LinkScopeProperty || change.Property == LinkRoleProperty)
        {
            ShowLinkAccess(animate: true);
            if (!_syncingPickers)
            {
                RaiseEvent(new ShareAccessLinkChangedEventArgs(LinkAccessChangedEvent, LinkScope, LinkRole));
            }
        }
        else if (change.Property == DocumentNameProperty || change.Property == LabelsProperty || change.Property == OrganizationNameProperty
                 || change.Property == LinkProperty || change.Property == CurrentUserEmailProperty)
        {
            RefreshWords();
        }
        else if (change.Property == InviteRoleProperty)
        {
            SyncPickers();
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateNarrow();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _inviteCancel?.Cancel();
        _inviteCancel = null;
    }

    private void SetInviting(bool inviting)
    {
        _isInviting = inviting;
        PseudoClasses.Set(InvitingPseudoClass, inviting);
        if (_inviteButton is not null)
        {
            _inviteButton.IsLoading = inviting;
        }
        UpdateInviteEnabled();
    }

    private ShareAccessPerson? FindPerson(string email)
    {
        foreach (var person in People)
        {
            if (string.Equals(person.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                return person;
            }
        }
        return null;
    }
}
