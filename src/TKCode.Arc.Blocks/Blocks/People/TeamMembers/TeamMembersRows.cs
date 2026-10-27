using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Pseudo-classes and data shared by the two kinds of list rows: the divider above the row (BorderSubtle, from the text's
/// left edge to the right padding; none on the first row, hidden while this row or the one above is hovered), the hover
/// tint and the responsive paddings.
/// </summary>
[PseudoClasses(FirstPseudoClass, DividerHiddenPseudoClass, CompactPseudoClass)]
internal abstract class TeamMembersRowBase : TemplatedControl
{
    public const string FirstPseudoClass = ":first";
    public const string DividerHiddenPseudoClass = ":divider-hidden";
    public const string CompactPseudoClass = ":compact";

    protected TeamMembersRowBase(TeamMembers owner)
    {
        Owner = owner;
    }

    public TeamMembers Owner { get; }

    /// <summary>The first visible row has no divider.</summary>
    public bool IsFirst
    {
        set => PseudoClasses.Set(FirstPseudoClass, value);
    }

    /// <summary>Hides the divider (this row or the one above is hovered).</summary>
    public bool IsDividerHidden
    {
        set => PseudoClasses.Set(DividerHiddenPseudoClass, value);
    }

    /// <summary>The block is 600 wide or less: side paddings of 16.</summary>
    public bool IsCompact
    {
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    /// <summary>True while the pointer is over the row (reported to the block for the dividers).</summary>
    public bool IsHovered { get; private set; }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        IsHovered = true;
        Owner.OnRowHoverChanged();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsHovered)
        {
            return;
        }
        IsHovered = false;
        Owner.OnRowHoverChanged();
    }

    protected static string Format(Func<string, string>? format, string value) => format?.Invoke(value) ?? value;
}

/// <summary>
/// One member: the avatar with its presence dot, the name ("You" after the current user) and email, when the person was last
/// active, the role (a menu, or the owner label) and a remove button. Pressing remove turns the row into an inline
/// confirmation (same row, tinted with the danger color, "Remove name?" and Cancel / Remove); Escape or Cancel goes back.
/// </summary>
[TemplatePart(LayoutPart, typeof(TeamMembersMemberRowPanel))]
[TemplatePart(AvatarPart, typeof(Avatar))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(YouPart, typeof(TextBlock))]
[TemplatePart(EmailPart, typeof(TextBlock))]
[TemplatePart(ActiveInlinePart, typeof(TextBlock))]
[TemplatePart(ActivePart, typeof(TextBlock))]
[TemplatePart(RolePart, typeof(TeamMembersRolePicker))]
[TemplatePart(OwnerTextPart, typeof(TextBlock))]
[TemplatePart(RemovePart, typeof(Button))]
[TemplatePart(ConfirmTitlePart, typeof(TextBlock))]
[TemplatePart(ConfirmDetailPart, typeof(TextBlock))]
[TemplatePart(CancelPart, typeof(Button))]
[TemplatePart(ConfirmPart, typeof(Button))]
[PseudoClasses(OwnerPseudoClass, YouPseudoClass, ConfirmingPseudoClass, NarrowPseudoClass, TwoTierPseudoClass)]
internal sealed class TeamMembersMemberRow : TeamMembersRowBase
{
    public const string LayoutPart = "PART_Layout";
    public const string AvatarPart = "PART_Avatar";
    public const string NamePart = "PART_Name";
    public const string YouPart = "PART_You";
    public const string EmailPart = "PART_Email";
    public const string ActiveInlinePart = "PART_ActiveInline";
    public const string ActivePart = "PART_Active";
    public const string RolePart = "PART_Role";
    public const string OwnerTextPart = "PART_OwnerText";
    public const string RemovePart = "PART_Remove";
    public const string ConfirmTitlePart = "PART_ConfirmTitle";
    public const string ConfirmDetailPart = "PART_ConfirmDetail";
    public const string CancelPart = "PART_Cancel";
    public const string ConfirmPart = "PART_ConfirmRemove";
    public const string OwnerPseudoClass = ":owner";
    public const string YouPseudoClass = ":you";
    public const string ConfirmingPseudoClass = ":confirming";
    public const string NarrowPseudoClass = ":narrow";
    public const string TwoTierPseudoClass = ":two-tier";

    private TeamMembersMemberRowPanel? _layout;
    private Avatar? _avatar;
    private TextBlock? _name;
    private TextBlock? _you;
    private TextBlock? _email;
    private TextBlock? _activeInline;
    private TextBlock? _active;
    private TeamMembersRolePicker? _role;
    private TextBlock? _ownerText;
    private Button? _remove;
    private TextBlock? _confirmTitle;
    private TextBlock? _confirmDetail;
    private Button? _cancel;
    private Button? _confirm;
    private TeamMembersRowMode _mode;
    private bool _watching;
    private bool _isConfirming;
    private bool _isBusy;

    public TeamMembersMemberRow(TeamMembersPerson person, TeamMembers owner)
        : base(owner)
    {
        Person = person;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public TeamMembersPerson Person { get; }

    public TeamMembersRolePicker? RolePicker => _role;

    public Button? RemoveButton => _remove;

    public Button? CancelButton => _cancel;

    public Button? ConfirmButton => _confirm;

    public Avatar? AvatarControl => _avatar;

    public TextBlock? ActiveText => _active;

    public TextBlock? ActiveInlineText => _activeInline;

    public TeamMembersMemberRowPanel? LayoutPanel => _layout;

    /// <summary>The column layout (set by the block from its width).</summary>
    public TeamMembersRowMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            PseudoClasses.Set(NarrowPseudoClass, value == TeamMembersRowMode.Narrow);
            PseudoClasses.Set(TwoTierPseudoClass, value == TeamMembersRowMode.TwoTier);
            if (_layout is not null)
            {
                _layout.Mode = value;
            }
        }
    }

    /// <summary>True while the row asks to confirm the removal.</summary>
    public bool IsConfirming
    {
        get => _isConfirming;
        set
        {
            _isConfirming = value;
            PseudoClasses.Set(ConfirmingPseudoClass, value);
            UpdateConfirmAccess();
        }
    }

    /// <summary>True while the removal or the role change is being saved: the confirm button spins.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            if (_confirm is not null)
            {
                _confirm.IsLoading = value && _isConfirming;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersRowPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_role is not null)
        {
            _role.Chosen -= OnRoleChosen;
        }
        if (_remove is not null)
        {
            _remove.Click -= OnRemoveClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_confirm is not null)
        {
            _confirm.Click -= OnConfirmClick;
        }
        _layout = e.NameScope.Find<TeamMembersMemberRowPanel>(LayoutPart);
        _avatar = e.NameScope.Find<Avatar>(AvatarPart);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _you = e.NameScope.Find<TextBlock>(YouPart);
        _email = e.NameScope.Find<TextBlock>(EmailPart);
        _activeInline = e.NameScope.Find<TextBlock>(ActiveInlinePart);
        _active = e.NameScope.Find<TextBlock>(ActivePart);
        _role = e.NameScope.Find<TeamMembersRolePicker>(RolePart);
        _ownerText = e.NameScope.Find<TextBlock>(OwnerTextPart);
        _remove = e.NameScope.Find<Button>(RemovePart);
        _confirmTitle = e.NameScope.Find<TextBlock>(ConfirmTitlePart);
        _confirmDetail = e.NameScope.Find<TextBlock>(ConfirmDetailPart);
        _cancel = e.NameScope.Find<Button>(CancelPart);
        _confirm = e.NameScope.Find<Button>(ConfirmPart);
        if (_role is not null)
        {
            _role.Chosen += OnRoleChosen;
        }
        if (_remove is not null)
        {
            _remove.Click += OnRemoveClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_confirm is not null)
        {
            _confirm.Click += OnConfirmClick;
        }
        if (_layout is not null)
        {
            _layout.Mode = _mode;
        }
        Refresh();
        UpdateConfirmAccess();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(true);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(false);
    }

    private void Watch(bool watch)
    {
        if (watch == _watching)
        {
            return;
        }
        _watching = watch;
        if (watch)
        {
            Person.PropertyChanged += OnPersonChanged;
        }
        else
        {
            Person.PropertyChanged -= OnPersonChanged;
        }
    }

    private void OnPersonChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();

    /// <summary>Copies the person, the labels and the role options into the row.</summary>
    public void Refresh()
    {
        var labels = Owner.Labels ?? new TeamMembersLabels();
        var name = Person.DisplayName;
        PseudoClasses.Set(OwnerPseudoClass, Person.IsOwner);
        PseudoClasses.Set(YouPseudoClass, Person.IsCurrentUser);
        if (_avatar is not null)
        {
            _avatar.DisplayName = name;
            _avatar.Source = Person.Avatar;
            _avatar.Status = Person.IsOnline ? AvatarStatus.Online : AvatarStatus.None;
            AutomationProperties.SetName(_avatar, Person.IsOnline ? $"{name}, {labels.Online}" : name);
        }
        if (_name is not null)
        {
            _name.Text = name;
        }
        if (_you is not null)
        {
            _you.Text = labels.You;
            _you.IsVisible = Person.IsCurrentUser;
        }
        if (_email is not null)
        {
            _email.Text = Person.Email;
            _email.IsVisible = !string.IsNullOrEmpty(Person.Email);
        }
        if (_active is not null)
        {
            _active.Text = Person.LastActive;
        }
        if (_activeInline is not null)
        {
            _activeInline.Text = Person.LastActive;
        }
        // Read only: every role is plain text like the owner's.
        var readOnly = Owner.IsReadOnly;
        if (_role is not null)
        {
            _role.Options = Owner.EffectiveRoles;
            _role.SelectedKey = Person.Role;
            _role.IsVisible = !Person.IsOwner && !readOnly;
            AutomationProperties.SetName(_role, labels.RoleFor(name));
        }
        if (_ownerText is not null)
        {
            _ownerText.Text = Person.IsOwner ? labels.Owner : Owner.RoleLabel(Person.Role);
            _ownerText.IsVisible = Person.IsOwner || readOnly;
        }
        if (_remove is not null)
        {
            _remove.IsVisible = !Person.IsOwner && Owner.IsRemoveAllowed;
            AutomationProperties.SetName(_remove, labels.RemoveName(name));
            Tooltip.SetTip(_remove, labels.RemoveName(name));
        }
        if (_confirmTitle is not null)
        {
            _confirmTitle.Text = labels.RemoveTitle(name);
        }
        if (_confirmDetail is not null)
        {
            _confirmDetail.Text = labels.RemoveDetail(name, Owner.WorkspaceName);
        }
        if (_cancel is not null)
        {
            _cancel.Content = labels.Cancel;
        }
        if (_confirm is not null)
        {
            _confirm.Content = labels.Remove;
            AutomationProperties.SetName(_confirm, labels.RemoveName(name));
        }
    }

    /// <summary>The normal face's controls are hidden from focus while confirming, and the confirm face's otherwise.</summary>
    private void UpdateConfirmAccess()
    {
        var confirming = _isConfirming;
        if (_role is not null)
        {
            _role.IsEnabled = !confirming;
        }
        if (_remove is not null)
        {
            _remove.IsEnabled = !confirming;
        }
        if (_cancel is not null)
        {
            _cancel.IsEnabled = confirming;
        }
        if (_confirm is not null)
        {
            _confirm.IsEnabled = confirming;
            _confirm.IsLoading = confirming && _isBusy;
        }
    }

    private void OnRoleChosen(TeamMembersRolePicker picker, string? key) => Owner.OnRoleChosen(this, key);

    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        // A keyboard-focused button (its ring shows) hands the focus on with a ring; a click does not.
        Owner.BeginRemove(this, _remove?.Classes.Contains(":focus-visible") == true ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Owner.CancelRemove(this, NavigationMethod.Tab);
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = Owner.ConfirmRemoveAsync(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && _isConfirming)
        {
            e.Handled = true;
            Owner.CancelRemove(this, NavigationMethod.Tab);
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>Reports a list row as a list item named by its person or address.</summary>
internal sealed class TeamMembersRowPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string? GetNameCore() => Owner switch
    {
        TeamMembersMemberRow member => member.Person.DisplayName,
        TeamMembersInviteRow invite => invite.Invite.Email,
        _ => base.GetNameCore()
    };
}

/// <summary>What a pending row's resend button shows.</summary>
internal enum TeamMembersResendState
{
    Idle,
    Sending,
    Sent,
}

/// <summary>
/// One pending invite: the dashed avatar, the address (on a pill that is the landing spot of the flying chip), "role ·
/// detail" (a blur swap when it changes) and the Resend and Revoke buttons. Resend spins while it runs, then reads
/// "✓ Sent" for 1.8 s.
/// </summary>
[TemplatePart(LayoutPart, typeof(TeamMembersInviteRowPanel))]
[TemplatePart(AvatarPart, typeof(TeamMembersDashedAvatar))]
[TemplatePart(EmailBadgePart, typeof(Border))]
[TemplatePart(EmailPart, typeof(TextBlock))]
[TemplatePart(DetailPart, typeof(ArcContentSwap))]
[TemplatePart(ResendPart, typeof(Button))]
[TemplatePart(RevokePart, typeof(Button))]
[PseudoClasses(ActionsBelowPseudoClass)]
internal sealed class TeamMembersInviteRow : TeamMembersRowBase
{
    public const string LayoutPart = "PART_Layout";
    public const string AvatarPart = "PART_Avatar";
    public const string EmailBadgePart = "PART_EmailBadge";
    public const string EmailPart = "PART_Email";
    public const string DetailPart = "PART_Detail";
    public const string ResendPart = "PART_Resend";
    public const string RevokePart = "PART_Revoke";
    public const string ActionsBelowPseudoClass = ":actions-below";

    /// <summary>How long the resend button reads "Sent" (1.8 s, measured).</summary>
    internal static TimeSpan SentDuration { get; set; } = TimeSpan.FromMilliseconds(1800);

    private TeamMembersInviteRowPanel? _layout;
    private TeamMembersDashedAvatar? _avatar;
    private Border? _badge;
    private TextBlock? _email;
    private ArcContentSwap? _detail;
    private Button? _resend;
    private Button? _revoke;
    private bool _actionsBelow;
    private bool _watching;
    private string? _detailText;
    private TeamMembersResendState _resendState;
    private CancellationTokenSource? _sentTimer;
    private bool _isRevoking;

    public TeamMembersInviteRow(TeamMembersInvite invite, TeamMembers owner)
        : base(owner)
    {
        Invite = invite;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    public TeamMembersInvite Invite { get; }

    public TeamMembersDashedAvatar? AvatarControl => _avatar;

    public Border? EmailBadge => _badge;

    public Button? ResendButton => _resend;

    public Button? RevokeButton => _revoke;

    public TeamMembersInviteRowPanel? LayoutPanel => _layout;

    /// <summary>The detail text shown now.</summary>
    public string? DetailText => _detailText;

    public TeamMembersResendState ResendState => _resendState;

    /// <summary>The buttons go under the address (the block is 480 wide or less).</summary>
    public bool ActionsBelow
    {
        get => _actionsBelow;
        set
        {
            _actionsBelow = value;
            PseudoClasses.Set(ActionsBelowPseudoClass, value);
            if (_layout is not null)
            {
                _layout.ActionsBelow = value;
            }
        }
    }

    /// <summary>True while the revoke is being saved: the revoke button spins.</summary>
    public bool IsRevoking
    {
        get => _isRevoking;
        set
        {
            _isRevoking = value;
            if (_revoke is not null)
            {
                _revoke.IsLoading = value;
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersRowPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_resend is not null)
        {
            _resend.Click -= OnResendClick;
        }
        if (_revoke is not null)
        {
            _revoke.Click -= OnRevokeClick;
        }
        _layout = e.NameScope.Find<TeamMembersInviteRowPanel>(LayoutPart);
        _avatar = e.NameScope.Find<TeamMembersDashedAvatar>(AvatarPart);
        _badge = e.NameScope.Find<Border>(EmailBadgePart);
        if (_badge is not null)
        {
            _badge.Opacity = _badgeHidden ? 0 : 1;
        }
        _email = e.NameScope.Find<TextBlock>(EmailPart);
        _detail = e.NameScope.Find<ArcContentSwap>(DetailPart);
        _resend = e.NameScope.Find<Button>(ResendPart);
        _revoke = e.NameScope.Find<Button>(RevokePart);
        if (_resend is not null)
        {
            _resend.Click += OnResendClick;
        }
        if (_revoke is not null)
        {
            _revoke.Click += OnRevokeClick;
            _revoke.IsLoading = _isRevoking;
        }
        if (_layout is not null)
        {
            _layout.ActionsBelow = _actionsBelow;
        }
        if (_detail is not null)
        {
            _detail.Content = null;
        }
        Refresh(animateDetail: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(true);
        Refresh(animateDetail: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(false);
        _sentTimer?.Cancel();
        _sentTimer = null;
        if (_resendState == TeamMembersResendState.Sent)
        {
            _resendState = TeamMembersResendState.Idle;
        }
    }

    private void Watch(bool watch)
    {
        if (watch == _watching)
        {
            return;
        }
        _watching = watch;
        if (watch)
        {
            Invite.PropertyChanged += OnInviteChanged;
        }
        else
        {
            Invite.PropertyChanged -= OnInviteChanged;
        }
    }

    private void OnInviteChanged(object? sender, AvaloniaPropertyChangedEventArgs e) =>
        Refresh(animateDetail: e.Property == TeamMembersInvite.DetailProperty || e.Property == TeamMembersInvite.RoleProperty);

    /// <summary>Copies the invite and the labels into the row; a changed detail swaps with a blur when <paramref name="animateDetail"/>.</summary>
    public void Refresh(bool animateDetail)
    {
        var labels = Owner.Labels ?? new TeamMembersLabels();
        var email = Invite.Email ?? string.Empty;
        if (_avatar is not null)
        {
            _avatar.Initials = Invite.DisplayInitials;
        }
        if (_email is not null)
        {
            _email.Text = email;
        }
        SetDetail(labels.InviteDetail(Owner.RoleLabel(Invite.Role), Invite.Detail), animateDetail);
        UpdateResend(labels);
        if (_resend is not null)
        {
            _resend.IsVisible = !Owner.IsReadOnly;
        }
        if (_revoke is not null)
        {
            _revoke.IsVisible = !Owner.IsReadOnly;
            _revoke.Content = labels.Revoke;
            AutomationProperties.SetName(_revoke, labels.RevokeName(email));
        }
    }

    private void SetDetail(string text, bool animate)
    {
        if (_detail is null)
        {
            _detailText = text;
            return;
        }
        if (_detailText == text && _detail.Content is not null)
        {
            return;
        }
        _detailText = text;
        var block = new TextBlock
        {
            Text = text,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            Classes = { "detail" }
        };
        if (!animate)
        {
            var mode = _detail.Mode;
            _detail.Mode = ContentSwapMode.None;
            _detail.Content = block;
            _detail.Mode = mode;
        }
        else
        {
            _detail.Content = block;
        }
    }

    private void UpdateResend(TeamMembersLabels labels)
    {
        if (_resend is null)
        {
            return;
        }
        var email = Invite.Email ?? string.Empty;
        _resend.IsLoading = _resendState == TeamMembersResendState.Sending;
        if (_resendState == TeamMembersResendState.Sent)
        {
            if (_resend.Content is not StackPanel)
            {
                _resend.Content = SentContent(labels.Sent);
            }
            AutomationProperties.SetName(_resend, labels.SentName(email));
        }
        else
        {
            _resend.Content = labels.Resend;
            AutomationProperties.SetName(_resend, labels.ResendName(email));
        }
    }

    private static StackPanel SentContent(string text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 6,
        Children =
        {
            new ArcGlyph { Data = ArcIcons.Check, Size = 15, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
        }
    };

    /// <summary>Moves the resend button to a state; Sent goes back to Idle after <see cref="SentDuration"/>.</summary>
    public void SetResendState(TeamMembersResendState state)
    {
        _sentTimer?.Cancel();
        _sentTimer = null;
        _resendState = state;
        UpdateResend(Owner.Labels ?? new TeamMembersLabels());
        if (state == TeamMembersResendState.Sent)
        {
            var timer = new CancellationTokenSource();
            _sentTimer = timer;
            _ = ResetAfterAsync(timer);
        }
    }

    private async Task ResetAfterAsync(CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(SentDuration, timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (ReferenceEquals(_sentTimer, timer))
        {
            _sentTimer = null;
            SetResendState(TeamMembersResendState.Idle);
        }
    }

    /// <summary>The flying copy of the address lands here: the badge is hidden until it does.</summary>
    public void SetBadgeHidden(bool hidden)
    {
        _badgeHidden = hidden;
        if (_badge is not null)
        {
            _badge.Opacity = hidden ? 0 : 1;
        }
    }

    private bool _badgeHidden;

    private void OnResendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_resendState == TeamMembersResendState.Sending)
        {
            return;
        }
        _ = Owner.ResendAsync(this);
    }

    private void OnRevokeClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (_isRevoking)
        {
            return;
        }
        _ = Owner.RevokeAsync(this);
    }
}

/// <summary>
/// The row a section shows when nothing matches: in the members section the text and a "Clear search" button (52 tall), in
/// the pending section a single muted line.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
[TemplatePart(ClearPart, typeof(Button))]
[PseudoClasses(PendingPseudoClass, CompactPseudoClass)]
internal sealed class TeamMembersEmptyRow : TemplatedControl
{
    public const string TextPart = "PART_Text";
    public const string ClearPart = "PART_Clear";
    public const string PendingPseudoClass = ":pending";
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TeamMembersEmptyRow, string?>(nameof(Text));

    public static readonly StyledProperty<string?> ClearTextProperty =
        AvaloniaProperty.Register<TeamMembersEmptyRow, string?>(nameof(ClearText));

    private Button? _clear;

    public TeamMembersEmptyRow(bool pending)
    {
        PseudoClasses.Set(PendingPseudoClass, pending);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public string? ClearText { get => GetValue(ClearTextProperty); set => SetValue(ClearTextProperty, value); }

    public Button? ClearButton => _clear;

    public bool IsCompact
    {
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    public event Action? ClearRequested;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_clear is not null)
        {
            _clear.Click -= OnClear;
        }
        _clear = e.NameScope.Find<Button>(ClearPart);
        if (_clear is not null)
        {
            _clear.Click += OnClear;
        }
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ClearRequested?.Invoke();
    }
}
