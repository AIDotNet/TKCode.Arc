using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(HeaderPart, typeof(TeamMembersHeaderPanel))]
[TemplatePart(SeatMeterPart, typeof(Control))]
[TemplatePart(SeatsUsedPart, typeof(RollingDigits))]
[TemplatePart(SeatsOfPart, typeof(TextBlock))]
[TemplatePart(SeatBarPart, typeof(ProjectsProgressLine))]
[TemplatePart(FormPart, typeof(Border))]
[TemplatePart(InviteLabelPart, typeof(TextBlock))]
[TemplatePart(InviteFormPart, typeof(TeamMembersInviteFormPanel))]
[TemplatePart(FieldPart, typeof(TeamMembersEmailField))]
[TemplatePart(InviteRolePart, typeof(TeamMembersRolePicker))]
[TemplatePart(SendPart, typeof(Button))]
[TemplatePart(MessagePart, typeof(ArcContentSwap))]
[TemplatePart(MembersHeadPart, typeof(TeamMembersSectionHeadPanel))]
[TemplatePart(MembersHeadingPart, typeof(TextBlock))]
[TemplatePart(MembersCountPart, typeof(RollingDigits))]
[TemplatePart(SearchPart, typeof(SearchField))]
[TemplatePart(MemberRowsPart, typeof(StackPanel))]
[TemplatePart(PendingHeadPart, typeof(TeamMembersSectionHeadPanel))]
[TemplatePart(PendingHeadingPart, typeof(TextBlock))]
[TemplatePart(PendingCountPart, typeof(RollingDigits))]
[TemplatePart(InviteRowsPart, typeof(StackPanel))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[TemplatePart(FlightsPart, typeof(TeamMembersFlightLayer))]
[TemplatePart(AnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(MessageAnnouncerPart, typeof(ProjectsAnnouncer))]
[TemplatePart(AlertPart, typeof(ProjectsAnnouncer))]
[PseudoClasses(FramedPseudoClass, NarrowPseudoClass, StackedPseudoClass, ActionsBelowPseudoClass, TwoTierPseudoClass,
    FullPseudoClass, SendingPseudoClass, ReadOnlyPseudoClass, StatusErrorPseudoClass)]
public partial class TeamMembers
{
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string HeaderPart = "PART_Header";
    public const string SeatMeterPart = "PART_SeatMeter";
    public const string SeatsUsedPart = "PART_SeatsUsed";
    public const string SeatsOfPart = "PART_SeatsOf";
    public const string SeatBarPart = "PART_SeatBar";
    public const string FormPart = "PART_Form";
    public const string InviteLabelPart = "PART_InviteLabel";
    public const string InviteFormPart = "PART_InviteForm";
    public const string FieldPart = "PART_Field";
    public const string InviteRolePart = "PART_InviteRole";
    public const string SendPart = "PART_Send";
    public const string MessagePart = "PART_Message";
    public const string MembersHeadPart = "PART_MembersHead";
    public const string MembersHeadingPart = "PART_MembersHeading";
    public const string MembersCountPart = "PART_MembersCount";
    public const string SearchPart = "PART_Search";
    public const string MemberRowsPart = "PART_MemberRows";
    public const string PendingHeadPart = "PART_PendingHead";
    public const string PendingHeadingPart = "PART_PendingHeading";
    public const string PendingCountPart = "PART_PendingCount";
    public const string InviteRowsPart = "PART_InviteRows";
    public const string StatusPart = "PART_Status";
    public const string FlightsPart = "PART_Flights";
    public const string AnnouncerPart = "PART_Announcer";
    public const string MessageAnnouncerPart = "PART_MessageAnnouncer";
    public const string AlertPart = "PART_Alert";

    /// <summary>Set while <see cref="IsFramed"/> is true: the bordered card.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set at 640 wide or less: the last-active column moves under the email.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set at 600 wide or less: the header and the invite row stack, paddings tighten, the search takes its own line.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set at 480 wide or less: a pending invite's buttons move under the address.</summary>
    public const string ActionsBelowPseudoClass = ":actions-below";

    /// <summary>Set at 420 wide or less: member rows split into two tiers.</summary>
    public const string TwoTierPseudoClass = ":two-tier";

    /// <summary>Set while every seat is taken (the seat bar turns to the warning color).</summary>
    public const string FullPseudoClass = ":full";

    /// <summary>Set while the send handler runs.</summary>
    public const string SendingPseudoClass = ":sending";

    /// <summary>Set while <see cref="IsReadOnly"/> is true.</summary>
    public const string ReadOnlyPseudoClass = ":readonly";

    /// <summary>Set while the footer shows an error.</summary>
    public const string StatusErrorPseudoClass = ":status-error";

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(NarrowWidth), 640);

    internal static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(StackWidth), 600);

    internal static readonly StyledProperty<double> ActionsBelowWidthProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(ActionsBelowWidth), 480);

    internal static readonly StyledProperty<double> TwoTierWidthProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(TwoTierWidth), 420);

    /// <summary>The seat bar's value and maximum: template-bound so the bar starts with them (no slide on load).</summary>
    internal static readonly StyledProperty<double> SeatBarValueProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(SeatBarValue));

    internal static readonly StyledProperty<double> SeatBarMaximumProperty =
        AvaloniaProperty.Register<TeamMembers, double>(nameof(SeatBarMaximum), 10);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double StackWidth { get => GetValue(StackWidthProperty); set => SetValue(StackWidthProperty, value); }

    internal double ActionsBelowWidth { get => GetValue(ActionsBelowWidthProperty); set => SetValue(ActionsBelowWidthProperty, value); }

    internal double TwoTierWidth { get => GetValue(TwoTierWidthProperty); set => SetValue(TwoTierWidthProperty, value); }

    internal double SeatBarValue { get => GetValue(SeatBarValueProperty); set => SetValue(SeatBarValueProperty, value); }

    internal double SeatBarMaximum { get => GetValue(SeatBarMaximumProperty); set => SetValue(SeatBarMaximumProperty, value); }

    #endregion

    private static readonly IReadOnlyList<TeamMembersRoleOption> s_defaultRoles =
    [
        new("admin", "Admin", "Manages people, billing and settings"),
        new("member", "Member", "Creates and edits work"),
        new("viewer", "Viewer", "Can look and comment, not edit")
    ];

    private readonly List<TeamMembersPerson> _members = new();
    private readonly List<TeamMembersInvite> _invites = new();
    private readonly Dictionary<TeamMembersPerson, TeamMembersRowSlot> _memberSlots = new();
    private readonly Dictionary<TeamMembersInvite, TeamMembersRowSlot> _inviteSlots = new();
    private INotifyCollectionChanged? _observedMembers;
    private INotifyCollectionChanged? _observedInvites;

    private TextBlock? _title;
    private TextBlock? _description;
    private TeamMembersHeaderPanel? _header;
    private Control? _seatMeter;
    private RollingDigits? _seatsUsedDigits;
    private TextBlock? _seatsOf;
    private ProjectsProgressLine? _seatBar;
    private Border? _form;
    private TextBlock? _inviteLabel;
    private TeamMembersInviteFormPanel? _inviteForm;
    private TeamMembersEmailField? _field;
    private TeamMembersRolePicker? _inviteRole;
    private Button? _send;
    private ArcContentSwap? _message;
    private TeamMembersSectionHeadPanel? _membersHead;
    private TextBlock? _membersHeading;
    private RollingDigits? _membersCount;
    private SearchField? _search;
    private StackPanel? _memberRows;
    private TeamMembersSectionHeadPanel? _pendingHead;
    private TextBlock? _pendingHeading;
    private RollingDigits? _pendingCount;
    private StackPanel? _inviteRows;
    private ArcContentSwap? _status;
    private TeamMembersFlightLayer? _flights;
    private ProjectsAnnouncer? _announcer;
    private ProjectsAnnouncer? _messageAnnouncer;
    private ProjectsAnnouncer? _alert;
    private TeamMembersRowSlot? _membersEmptySlot;
    private TeamMembersEmptyRow? _membersEmpty;
    private TeamMembersRowSlot? _invitesEmptySlot;
    private TeamMembersEmptyRow? _invitesEmpty;

    private TeamMembersRowMode _rowMode = TeamMembersRowMode.Wide;
    private bool _stacked;
    private bool _actionsBelow;
    private bool _syncingSearch;
    private string? _messageKey;
    private bool _statusIsError;

    static TeamMembers()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<TeamMembers>(false);
    }

    public TeamMembers()
    {
        Members.CollectionChanged += (_, _) =>
        {
            if (MembersSource is null)
            {
                SyncMembers();
            }
        };
        Invites.CollectionChanged += (_, _) =>
        {
            if (InvitesSource is null)
            {
                SyncInvites();
            }
        };
        Roles.CollectionChanged += (_, _) => OnRolesChanged();
        PseudoClasses.Set(FramedPseudoClass, true);
        _statusText = new TeamMembersLabels().StatusIdle;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
        RebuildData();
    }

    /// <summary>The members shown now (from <see cref="MembersSource"/> or <see cref="Members"/>), in order.</summary>
    public IReadOnlyList<TeamMembersPerson> EffectiveMembers => _members;

    /// <summary>The pending invites shown now (from <see cref="InvitesSource"/> or <see cref="Invites"/>), in order.</summary>
    public IReadOnlyList<TeamMembersInvite> EffectiveInvites => _invites;

    /// <summary>The roles in the menus: <see cref="Roles"/>, or Admin, Member and Viewer when it is empty.</summary>
    public IReadOnlyList<TeamMembersRoleOption> EffectiveRoles => Roles.Count > 0 ? Roles : s_defaultRoles;

    /// <summary>The addresses waiting in the invite field, in order.</summary>
    public IReadOnlyList<string> PendingEmails => _field?.Chips ?? _chipsBeforeTemplate;

    private readonly List<string> _chipsBeforeTemplate = new();

    internal bool IsRemoveAllowed => !IsReadOnly;

    internal TeamMembersEmailField? Field => _field;

    internal Button? SendButton => _send;

    internal TeamMembersRolePicker? InviteRolePicker => _inviteRole;

    internal SearchField? Search => _search;

    internal TeamMembersFlightLayer? Flights => _flights;

    internal ProjectsAnnouncer? Announcer => _announcer;

    internal ProjectsAnnouncer? MessageAnnouncer => _messageAnnouncer;

    internal ProjectsAnnouncer? Alert => _alert;

    internal ProjectsProgressLine? SeatBar => _seatBar;

    internal RollingDigits? SeatsUsedDigits => _seatsUsedDigits;

    internal RollingDigits? MembersCountDigits => _membersCount;

    internal RollingDigits? PendingCountDigits => _pendingCount;

    internal TeamMembersEmptyRow? MembersEmptyRow => _membersEmpty;

    internal TeamMembersEmptyRow? InvitesEmptyRow => _invitesEmpty;

    internal TeamMembersRowSlot? MembersEmptySlot => _membersEmptySlot;

    internal TeamMembersRowSlot? InvitesEmptySlot => _invitesEmptySlot;

    internal TeamMembersHeaderPanel? HeaderPanel => _header;

    internal TeamMembersInviteFormPanel? InviteFormPanel => _inviteForm;

    internal TeamMembersSectionHeadPanel? MembersHeadPanel => _membersHead;

    internal TeamMembersSectionHeadPanel? PendingHeadPanel => _pendingHead;

    /// <summary>The message line under the invite field (its text) and whether it is an error / warning.</summary>
    internal string? MessageText { get; private set; }

    internal TeamMembersMessageKind MessageKind { get; private set; }

    internal TeamMembersRowMode RowMode => _rowMode;

    internal IEnumerable<TeamMembersMemberRow> MemberRows =>
        _memberRows?.Children.OfType<TeamMembersRowSlot>().Select(s => s.Child).OfType<TeamMembersMemberRow>() ?? [];

    internal IEnumerable<TeamMembersInviteRow> InviteRows =>
        _inviteRows?.Children.OfType<TeamMembersRowSlot>().Select(s => s.Child).OfType<TeamMembersInviteRow>() ?? [];

    internal TeamMembersMemberRow? RowOf(TeamMembersPerson person) =>
        _memberSlots.TryGetValue(person, out var slot) ? slot.Child as TeamMembersMemberRow : null;

    internal TeamMembersInviteRow? RowOf(TeamMembersInvite invite) =>
        _inviteSlots.TryGetValue(invite, out var slot) ? slot.Child as TeamMembersInviteRow : null;

    internal TeamMembersRowSlot? SlotOf(TeamMembersPerson person) => _memberSlots.GetValueOrDefault(person);

    internal TeamMembersRowSlot? SlotOf(TeamMembersInvite invite) => _inviteSlots.GetValueOrDefault(invite);

    /// <summary>The name of a role key (the key itself when it is not among the roles).</summary>
    internal string RoleLabel(string? key) =>
        EffectiveRoles.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.Ordinal))?.DisplayLabel ?? key ?? string.Empty;

    /// <summary>The block is a group named by its title.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new TeamMembersGroupPeer(this);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DetachParts();
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _header = e.NameScope.Find<TeamMembersHeaderPanel>(HeaderPart);
        _seatMeter = e.NameScope.Find<Control>(SeatMeterPart);
        _seatsUsedDigits = e.NameScope.Find<RollingDigits>(SeatsUsedPart);
        _seatsOf = e.NameScope.Find<TextBlock>(SeatsOfPart);
        _seatBar = e.NameScope.Find<ProjectsProgressLine>(SeatBarPart);
        _form = e.NameScope.Find<Border>(FormPart);
        _inviteLabel = e.NameScope.Find<TextBlock>(InviteLabelPart);
        _inviteForm = e.NameScope.Find<TeamMembersInviteFormPanel>(InviteFormPart);
        _field = e.NameScope.Find<TeamMembersEmailField>(FieldPart);
        _inviteRole = e.NameScope.Find<TeamMembersRolePicker>(InviteRolePart);
        _send = e.NameScope.Find<Button>(SendPart);
        _message = e.NameScope.Find<ArcContentSwap>(MessagePart);
        _membersHead = e.NameScope.Find<TeamMembersSectionHeadPanel>(MembersHeadPart);
        _membersHeading = e.NameScope.Find<TextBlock>(MembersHeadingPart);
        _membersCount = e.NameScope.Find<RollingDigits>(MembersCountPart);
        _search = e.NameScope.Find<SearchField>(SearchPart);
        _memberRows = e.NameScope.Find<StackPanel>(MemberRowsPart);
        _pendingHead = e.NameScope.Find<TeamMembersSectionHeadPanel>(PendingHeadPart);
        _pendingHeading = e.NameScope.Find<TextBlock>(PendingHeadingPart);
        _pendingCount = e.NameScope.Find<RollingDigits>(PendingCountPart);
        _inviteRows = e.NameScope.Find<StackPanel>(InviteRowsPart);
        _status = e.NameScope.Find<ArcContentSwap>(StatusPart);
        _flights = e.NameScope.Find<TeamMembersFlightLayer>(FlightsPart);
        _announcer = e.NameScope.Find<ProjectsAnnouncer>(AnnouncerPart);
        _messageAnnouncer = e.NameScope.Find<ProjectsAnnouncer>(MessageAnnouncerPart);
        _alert = e.NameScope.Find<ProjectsAnnouncer>(AlertPart);
        AttachParts();

        _memberSlots.Clear();
        _inviteSlots.Clear();
        _memberRows?.Children.Clear();
        _inviteRows?.Children.Clear();
        BuildEmptyRows();
        SyncMembers(animate: false);
        SyncInvites(animate: false);
        RefreshTexts();
        UpdateLayoutMode(Bounds.Width);
        UpdateCounts(animate: false);
        UpdateInviteState(animateMessage: false);
        ShowStatus(StatusText, _statusIsError, animate: false);
    }

    private void AttachParts()
    {
        if (_field is not null)
        {
            _field.Committed += OnFieldCommitted;
            _field.Pasted += OnFieldCommitted;
            _field.EnterOnEmpty += OnFieldEnterOnEmpty;
            _field.BackspaceOnEmpty += OnFieldBackspace;
            _field.ChipRemoveRequested += OnFieldChipRemove;
            _field.DraftChanged += OnFieldDraftChanged;
            foreach (var chip in _chipsBeforeTemplate)
            {
                _field.AddChip(chip, animate: false);
            }
            _chipsBeforeTemplate.Clear();
        }
        if (_inviteRole is not null)
        {
            _inviteRole.Chosen += OnInviteRoleChosen;
        }
        if (_send is not null)
        {
            _send.Click += OnSendClick;
        }
        if (_search is not null)
        {
            _search.TextChanged += OnSearchTextChanged;
            _search.Text = SearchText;
        }
    }

    private void DetachParts()
    {
        if (_field is not null)
        {
            _chipsBeforeTemplate.Clear();
            _chipsBeforeTemplate.AddRange(_field.Chips);
            _field.Committed -= OnFieldCommitted;
            _field.Pasted -= OnFieldCommitted;
            _field.EnterOnEmpty -= OnFieldEnterOnEmpty;
            _field.BackspaceOnEmpty -= OnFieldBackspace;
            _field.ChipRemoveRequested -= OnFieldChipRemove;
            _field.DraftChanged -= OnFieldDraftChanged;
        }
        if (_inviteRole is not null)
        {
            _inviteRole.Chosen -= OnInviteRoleChosen;
        }
        if (_send is not null)
        {
            _send.Click -= OnSendClick;
        }
        if (_search is not null)
        {
            _search.TextChanged -= OnSearchTextChanged;
        }
        if (_membersEmpty is not null)
        {
            _membersEmpty.ClearRequested -= OnClearSearchRequested;
        }
    }

    private void BuildEmptyRows()
    {
        _membersEmpty = new TeamMembersEmptyRow(pending: false);
        _membersEmpty.ClearRequested += OnClearSearchRequested;
        _membersEmptySlot = new TeamMembersRowSlot { Child = _membersEmpty };
        _membersEmptySlot.SetCollapsed(true, animate: false);
        _invitesEmpty = new TeamMembersEmptyRow(pending: true);
        _invitesEmptySlot = new TeamMembersRowSlot { Child = _invitesEmpty };
        _invitesEmptySlot.SetCollapsed(true, animate: false);
        _memberRows?.Children.Add(_membersEmptySlot);
        _inviteRows?.Children.Add(_invitesEmptySlot);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MembersSourceProperty)
        {
            RebuildData();
            SyncMembers(animate: false);
        }
        else if (change.Property == InvitesSourceProperty)
        {
            RebuildData();
            SyncInvites(animate: false);
        }
        else if (change.Property == SeatLimitProperty)
        {
            UpdateCounts(animate: true);
            UpdateInviteState(animateMessage: true);
        }
        else if (change.Property == InviteRoleProperty)
        {
            if (_inviteRole is not null)
            {
                _inviteRole.SelectedKey = InviteRole;
            }
        }
        else if (change.Property == SearchTextProperty)
        {
            if (!_syncingSearch && _search is not null && _search.Text != SearchText)
            {
                _syncingSearch = true;
                try
                {
                    _search.Text = SearchText;
                }
                finally
                {
                    _syncingSearch = false;
                }
            }
            ApplySearch(animate: true);
        }
        else if (change.Property == LabelsProperty || change.Property == TitleProperty || change.Property == DescriptionProperty ||
                 change.Property == WorkspaceNameProperty)
        {
            RefreshTexts();
            UpdateInviteState(animateMessage: false);
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == IsInviteVisibleProperty ||
                 change.Property == IsSearchVisibleProperty || change.Property == IsSeatMeterVisibleProperty ||
                 change.Property == IsFooterVisibleProperty)
        {
            PseudoClasses.Set(ReadOnlyPseudoClass, IsReadOnly);
            RefreshTexts();
        }
        else if (change.Property == EmailValidatorProperty)
        {
            UpdateInviteState(animateMessage: false);
        }
        else if (change.Property == NarrowWidthProperty || change.Property == StackWidthProperty ||
                 change.Property == ActionsBelowWidthProperty || change.Property == TwoTierWidthProperty)
        {
            UpdateLayoutMode(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelAll();
        ObserveSources(false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RebuildData();
        SyncMembers(animate: false);
        SyncInvites(animate: false);
    }

    /// <summary>Applies the width breakpoints (the block's own width, never the window's).</summary>
    private void UpdateLayoutMode(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var mode = width <= TwoTierWidth ? TeamMembersRowMode.TwoTier
            : width <= NarrowWidth ? TeamMembersRowMode.Narrow
            : TeamMembersRowMode.Wide;
        _stacked = width <= StackWidth;
        _actionsBelow = width <= ActionsBelowWidth;
        _rowMode = mode;
        PseudoClasses.Set(NarrowPseudoClass, mode != TeamMembersRowMode.Wide);
        PseudoClasses.Set(TwoTierPseudoClass, mode == TeamMembersRowMode.TwoTier);
        PseudoClasses.Set(StackedPseudoClass, _stacked);
        PseudoClasses.Set(ActionsBelowPseudoClass, _actionsBelow);
        if (_header is not null)
        {
            _header.IsStacked = _stacked;
        }
        if (_inviteForm is not null)
        {
            _inviteForm.IsStacked = _stacked;
        }
        if (_membersHead is not null)
        {
            _membersHead.IsStacked = _stacked;
        }
        foreach (var row in MemberRows)
        {
            ApplyLayout(row);
        }
        foreach (var row in InviteRows)
        {
            ApplyLayout(row);
        }
        if (_membersEmpty is not null)
        {
            _membersEmpty.IsCompact = _stacked;
        }
        if (_invitesEmpty is not null)
        {
            _invitesEmpty.IsCompact = _stacked;
        }
    }

    private void ApplyLayout(TeamMembersMemberRow row)
    {
        row.Mode = _rowMode;
        row.IsCompact = _stacked;
    }

    private void ApplyLayout(TeamMembersInviteRow row)
    {
        row.ActionsBelow = _actionsBelow;
        row.IsCompact = _stacked;
    }

    private void RefreshTexts()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        if (_seatMeter is not null)
        {
            _seatMeter.IsVisible = IsSeatMeterVisible;
        }
        if (_form is not null)
        {
            _form.IsVisible = IsInviteVisible && !IsReadOnly;
        }
        if (_inviteLabel is not null)
        {
            _inviteLabel.Text = labels.InviteHeading;
        }
        if (_field is not null)
        {
            _field.Placeholder = labels.Placeholder;
            _field.RemoveName = labels.RemoveChipName;
            AutomationProperties.SetName(_field, labels.EmailFieldName);
            if (_field.Input is { } input)
            {
                AutomationProperties.SetName(input, labels.EmailFieldName);
            }
        }
        if (_inviteRole is not null)
        {
            _inviteRole.Options = EffectiveRoles;
            _inviteRole.SelectedKey = InviteRole;
            AutomationProperties.SetName(_inviteRole, labels.InviteAsName);
        }
        if (_membersHeading is not null)
        {
            _membersHeading.Text = labels.MembersHeading;
        }
        if (_pendingHeading is not null)
        {
            _pendingHeading.Text = labels.PendingHeading;
        }
        if (_search is not null)
        {
            _search.Placeholder = labels.SearchPlaceholder;
            _search.IsVisible = IsSearchVisible;
            AutomationProperties.SetName(_search, labels.SearchName);
        }
        if (_membersEmpty is not null)
        {
            _membersEmpty.ClearText = labels.ClearSearch;
        }
        if (_memberRows is not null)
        {
            AutomationProperties.SetName(_memberRows, labels.MembersHeading);
        }
        if (_inviteRows is not null)
        {
            AutomationProperties.SetName(_inviteRows, labels.PendingHeading);
        }
        if (_seatBar is not null)
        {
            AutomationProperties.SetName(_seatBar, labels.SeatsName);
        }
        if (_statusIsError == false && (string.IsNullOrEmpty(_statusText) || _statusText == _idleText))
        {
            _idleText = labels.StatusIdle;
            StatusText = labels.StatusIdle;
            ShowStatus(StatusText, false, animate: false);
        }
        foreach (var row in MemberRows)
        {
            row.Refresh();
        }
        foreach (var row in InviteRows)
        {
            row.Refresh(animateDetail: false);
        }
        UpdateEmptyRows(animate: false);
    }

    private string _idleText = new TeamMembersLabels().StatusIdle;

    #endregion

    #region Data

    private void RebuildData()
    {
        ObserveSources(true);
        _members.Clear();
        foreach (var item in (IEnumerable?)MembersSource ?? Members)
        {
            if (item is TeamMembersPerson person && !_members.Contains(person))
            {
                _members.Add(person);
            }
        }
        _invites.Clear();
        foreach (var item in (IEnumerable?)InvitesSource ?? Invites)
        {
            if (item is TeamMembersInvite invite && !_invites.Contains(invite))
            {
                _invites.Add(invite);
            }
        }
    }

    private void ObserveSources(bool observe)
    {
        var members = observe ? MembersSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(members, _observedMembers))
        {
            if (_observedMembers is not null)
            {
                _observedMembers.CollectionChanged -= OnMembersSourceChanged;
            }
            _observedMembers = members;
            if (members is not null)
            {
                members.CollectionChanged += OnMembersSourceChanged;
            }
        }
        var invites = observe ? InvitesSource as INotifyCollectionChanged : null;
        if (!ReferenceEquals(invites, _observedInvites))
        {
            if (_observedInvites is not null)
            {
                _observedInvites.CollectionChanged -= OnInvitesSourceChanged;
            }
            _observedInvites = invites;
            if (invites is not null)
            {
                invites.CollectionChanged += OnInvitesSourceChanged;
            }
        }
    }

    private void OnMembersSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncMembers();

    private void OnInvitesSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncInvites();

    private void OnRolesChanged()
    {
        RefreshTexts();
    }

    /// <summary>The list the block writes to for members: the source when it is a writable list, else <see cref="Members"/>; null when the source is read only.</summary>
    private IList? WritableMembers => MembersSource is null ? Members : MembersSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;

    private IList? WritableInvites => InvitesSource is null ? Invites : InvitesSource is IList { IsReadOnly: false, IsFixedSize: false } list ? list : null;

    private bool CanAnimate => IsLoaded && ArcMotion.IsFull(this);

    private void SyncMembers() => SyncMembers(animate: true);

    private void SyncMembers(bool animate)
    {
        RebuildData();
        if (_memberRows is null)
        {
            UpdateCounts(animate);
            return;
        }
        animate &= CanAnimate;
        foreach (var (person, slot) in _memberSlots.ToArray())
        {
            if (!_members.Contains(person))
            {
                _memberSlots.Remove(person);
                RemoveSlot(_memberRows, slot, animate);
            }
        }
        foreach (var person in _members)
        {
            if (_memberSlots.ContainsKey(person))
            {
                continue;
            }
            var row = new TeamMembersMemberRow(person, this);
            ApplyLayout(row);
            var slot = new TeamMembersRowSlot { Child = row, Key = person.Key };
            _memberSlots[person] = slot;
            _memberRows.Children.Insert(Math.Max(0, _memberRows.Children.Count - 1), slot);
            slot.Enter(animate, TimeSpan.FromMilliseconds(200));
        }
        Order(_memberRows, _members.Select(p => _memberSlots[p]).ToList(), _membersEmptySlot);
        ApplySearch(animate);
        UpdateCounts(animate);
        UpdateInviteState(animateMessage: animate);
    }

    private void SyncInvites() => SyncInvites(animate: true);

    private void SyncInvites(bool animate)
    {
        RebuildData();
        if (_inviteRows is null)
        {
            UpdateCounts(animate);
            return;
        }
        animate &= CanAnimate;
        foreach (var (invite, slot) in _inviteSlots.ToArray())
        {
            if (!_invites.Contains(invite))
            {
                _inviteSlots.Remove(invite);
                RemoveSlot(_inviteRows, slot, animate);
            }
        }
        var index = 0;
        foreach (var invite in _invites)
        {
            if (_inviteSlots.ContainsKey(invite))
            {
                continue;
            }
            var row = new TeamMembersInviteRow(invite, this);
            ApplyLayout(row);
            var slot = new TeamMembersRowSlot { Child = row, Key = invite.Key };
            _inviteSlots[invite] = slot;
            _inviteRows.Children.Insert(Math.Max(0, _inviteRows.Children.Count - 1), slot);
            var flight = _pendingFlights.Remove(invite, out var source) ? source : (Point?)null;
            // A sent invite's row grows without a fade: its address arrives by air and its avatar pops in.
            slot.Enter(animate, flight is null ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero);
            if (flight is { } from && animate)
            {
                StartFlight(invite, row, from, index++);
            }
        }
        Order(_inviteRows, _invites.Select(i => _inviteSlots[i]).ToList(), _invitesEmptySlot);
        ApplySearch(animate);
        UpdateCounts(animate);
        UpdateInviteState(animateMessage: animate);
    }

    private void RemoveSlot(StackPanel panel, TeamMembersRowSlot slot, bool animate)
    {
        if (!animate)
        {
            panel.Children.Remove(slot);
            UpdateDividers();
            return;
        }
        _ = ExitAsync(panel, slot);
    }

    private async Task ExitAsync(StackPanel panel, TeamMembersRowSlot slot)
    {
        var exit = slot.ExitAsync(animate: true);
        UpdateDividers();
        await exit;
        panel.Children.Remove(slot);
        UpdateDividers();
    }

    /// <summary>Puts the slots in data order, leaving slots where they were, the empty row last.</summary>
    private static void Order(StackPanel panel, List<TeamMembersRowSlot> ordered, TeamMembersRowSlot? empty)
    {
        var children = panel.Children;
        var target = new List<Control>(ordered);
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is TeamMembersRowSlot { IsLeaving: true } leaving)
            {
                target.Insert(Math.Min(i, target.Count), leaving);
            }
        }
        if (empty is not null)
        {
            target.Add(empty);
        }
        for (var i = 0; i < target.Count; i++)
        {
            var current = children.IndexOf(target[i]);
            if (current >= 0 && current != i)
            {
                children.Move(current, i);
            }
        }
    }

    #endregion

    #region Counts, search, dividers

    private void UpdateCounts(bool animate)
    {
        var used = _members.Count + _invites.Count;
        var limit = Math.Max(0, SeatLimit);
        SeatsUsed = used;
        SeatsRemaining = Math.Max(0, limit - used);
        var full = limit > 0 && used >= limit;
        PseudoClasses.Set(FullPseudoClass, full);
        animate &= CanAnimate;
        if (_seatsUsedDigits is not null)
        {
            _seatsUsedDigits.IsInstant = !animate;
            _seatsUsedDigits.Value = used;
        }
        if (_seatsOf is not null)
        {
            _seatsOf.Text = " " + Labels.SeatsOf(limit);
        }
        SeatBarMaximum = Math.Max(1, limit);
        SeatBarValue = Math.Min(used, Math.Max(1, limit));
        if (_seatBar is not null)
        {
            AutomationProperties.SetHelpText(_seatBar, Labels.SeatsValue(used, limit));
        }
        UpdateSectionCounts(animate);
    }

    private void UpdateSectionCounts(bool animate)
    {
        var query = SearchText;
        var members = _members.Count(m => TeamMembersEmailParser.Matches(query, m.Name, m.Email));
        var invites = _invites.Count(i => TeamMembersEmailParser.Matches(query, i.Email));
        if (_membersCount is not null)
        {
            _membersCount.IsInstant = !animate;
            _membersCount.Value = members;
        }
        if (_pendingCount is not null)
        {
            _pendingCount.IsInstant = !animate;
            _pendingCount.Value = invites;
        }
        if (_pendingHead is not null)
        {
            var showPending = _invites.Count > 0 || !string.IsNullOrWhiteSpace(query) || Labels.NoInvites is not null;
            _pendingHead.IsVisible = showPending;
            if (_inviteRows is not null)
            {
                _inviteRows.IsVisible = showPending || _inviteRows.Children.OfType<TeamMembersRowSlot>().Any(s => s.IsLeaving);
            }
        }
    }

    private void ApplySearch(bool animate)
    {
        animate &= CanAnimate;
        var query = SearchText;
        foreach (var (person, slot) in _memberSlots)
        {
            slot.SetCollapsed(!TeamMembersEmailParser.Matches(query, person.Name, person.Email), animate);
        }
        foreach (var (invite, slot) in _inviteSlots)
        {
            slot.SetCollapsed(!TeamMembersEmailParser.Matches(query, invite.Email), animate);
        }
        UpdateSectionCounts(animate);
        UpdateEmptyRows(animate);
        UpdateDividers();
    }

    private void UpdateEmptyRows(bool animate)
    {
        var labels = Labels;
        var query = SearchText?.Trim() ?? string.Empty;
        var searching = query.Length > 0;
        if (_membersEmpty is not null && _membersEmptySlot is not null)
        {
            var none = _memberSlots.Values.All(s => s.IsCollapsed);
            var show = searching && none;
            if (show)
            {
                _membersEmpty.Text = labels.NoMembersMatch(query);
            }
            _membersEmptySlot.SetCollapsed(!show, animate);
        }
        if (_invitesEmpty is not null && _invitesEmptySlot is not null)
        {
            var none = _inviteSlots.Values.All(s => s.IsCollapsed);
            string? text = searching ? (none ? labels.NoInvitesMatch : null) : (_invites.Count == 0 ? labels.NoInvites : null);
            if (text is not null)
            {
                _invitesEmpty.Text = text;
            }
            _invitesEmptySlot.SetCollapsed(text is null, animate);
        }
    }

    /// <summary>The first visible row of each list has no divider; a hovered row hides its own and the next one's.</summary>
    internal void UpdateDividers()
    {
        UpdateDividers(_memberRows);
        UpdateDividers(_inviteRows);
    }

    private static void UpdateDividers(StackPanel? panel)
    {
        if (panel is null)
        {
            return;
        }
        var first = true;
        var previousHovered = false;
        foreach (var slot in panel.Children.OfType<TeamMembersRowSlot>())
        {
            if (slot.Child is not TeamMembersRowBase row)
            {
                continue;
            }
            if (slot.IsCollapsed || slot.IsLeaving)
            {
                continue;
            }
            row.IsFirst = first;
            row.IsDividerHidden = row.IsHovered || previousHovered;
            first = false;
            previousHovered = row.IsHovered;
        }
    }

    internal void OnRowHoverChanged() => UpdateDividers();

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingSearch || _search is null)
        {
            return;
        }
        _syncingSearch = true;
        try
        {
            SetCurrentValue(SearchTextProperty, _search.Text ?? string.Empty);
        }
        finally
        {
            _syncingSearch = false;
        }
    }

    private void OnClearSearchRequested()
    {
        ClearSearch();
        _search?.Focus(NavigationMethod.Tab);
    }

    /// <summary>Clears the search (every row comes back).</summary>
    public void ClearSearch() => SetCurrentValue(SearchTextProperty, string.Empty);

    #endregion

    #region Status and messages

    private void SetStatus(string text, bool isError)
    {
        _statusIsError = isError;
        StatusText = text;
        ShowStatus(text, isError, animate: true);
        if (isError)
        {
            _alert?.Announce(text);
        }
        else
        {
            _announcer?.Announce(text);
        }
    }

    private void ShowStatus(string text, bool isError, bool animate)
    {
        PseudoClasses.Set(StatusErrorPseudoClass, isError);
        if (_status is null)
        {
            return;
        }
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };
        if (isError)
        {
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Danger);
        }
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        if (!animate || _status.Content is null)
        {
            var mode = _status.Mode;
            _status.Mode = ContentSwapMode.None;
            _status.Content = block;
            _status.Mode = mode;
        }
        else
        {
            _status.Content = block;
        }
    }

    #endregion
}

/// <summary>What the line under the invite field is saying.</summary>
internal enum TeamMembersMessageKind
{
    Hint,
    Warning,
    Error,
}

/// <summary>Reports the block as a group named by its title.</summary>
internal sealed class TeamMembersGroupPeer(TeamMembers owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : ((TeamMembers)Owner).Title;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>One line under the invite field: an alert icon (warning or error) and the text.</summary>
internal sealed class TeamMembersMessageLine : StackPanel
{
    public TeamMembersMessageLine(TeamMembersMessageKind kind, string text)
    {
        Kind = kind;
        Orientation = Orientation.Horizontal;
        // Geometry of the line (Arc): a 14px alert icon (stroke 1.75) 6 before the 12 / 18 text, the icon centred on the
        // first line. Hints are muted and have no icon; warnings keep secondary text with a warning icon; errors are danger.
        Spacing = 6;
        if (kind != TeamMembersMessageKind.Hint)
        {
            var icon = new ArcGlyph
            {
                Data = ArcIcons.CircleAlert,
                Size = 14,
                StrokeThickness = 1.75,
                Margin = new Thickness(0, 2, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty,
                kind == TeamMembersMessageKind.Error ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.Warning);
            Children.Add(icon);
        }
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, kind switch
        {
            TeamMembersMessageKind.Error => ArcDesignTokenKind.Danger,
            TeamMembersMessageKind.Warning => ArcDesignTokenKind.TextSecondary,
            _ => ArcDesignTokenKind.TextMuted
        });
        Children.Add(block);
        Text = text;
    }

    public TeamMembersMessageKind Kind { get; }

    public string Text { get; }

    protected override Type StyleKeyOverride => typeof(TeamMembersMessageLine);

    protected override Size MeasureOverride(Size availableSize)
    {
        // The text wraps in what is left after the icon.
        if (Children.Count == 2 && double.IsFinite(availableSize.Width))
        {
            Children[0].Measure(availableSize);
            var icon = Children[0].DesiredSize.Width + Spacing;
            Children[1].Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - icon)));
            return new Size(Math.Min(availableSize.Width, icon + Children[1].DesiredSize.Width),
                Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }
        if (Children.Count == 1)
        {
            Children[0].Measure(availableSize);
            return Children[0].DesiredSize;
        }
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 1)
        {
            Children[0].Arrange(new Rect(finalSize));
            return finalSize;
        }
        if (Children.Count == 2)
        {
            var iconSize = Children[0].DesiredSize;
            Children[0].Arrange(new Rect(0, 0, iconSize.Width, iconSize.Height));
            var x = iconSize.Width + Spacing;
            Children[1].Arrange(new Rect(x, 0, Math.Max(0, finalSize.Width - x), Children[1].DesiredSize.Height));
            return finalSize;
        }
        return base.ArrangeOverride(finalSize);
    }
}
