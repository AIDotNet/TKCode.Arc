using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A form for inviting people to a workspace. Type a name or an address and pick from the directory, or paste a whole
/// list: every person becomes a chip with a role, and entries that cannot be invited become red chips that say why
/// (select one to fix it). A seat meter counts members, pending invites and the new chips against the plan, and offers
/// the next plan when they run over. Send moves the chips into the pending list below, where each invite can be sent
/// again or revoked; when an invite is accepted its row leaves and the person joins the team avatars in the header.
/// Server work goes through <see cref="SendAction"/>, <see cref="ResendAction"/>, <see cref="RevokeAction"/> and
/// <see cref="UpgradeAction"/>; <see cref="MarkDelivered"/> and <see cref="MarkJoined"/> report what happened later.
/// <code>
/// &lt;arc:InvitePeople Title="Invite people" Description="Add people to Lumen Labs by name or email" WorkspaceName="Lumen Labs"&gt;
///     &lt;arc:InvitePeoplePerson Name="Clara Weiss" Email="clara.weiss@lumen.example" Title="Brand designer" /&gt;
/// &lt;/arc:InvitePeople&gt;
/// </code>
/// </summary>
[TemplatePart(CardPart, typeof(Border))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, NarrowPseudoClass, OverPseudoClass, FieldFocusedPseudoClass)]
public partial class InvitePeople : TemplatedControl
{
    internal const string CardPart = "PART_Card";
    internal const string FramedPseudoClass = ":framed";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string OverPseudoClass = ":over";
    internal const string FieldFocusedPseudoClass = ":field-focused";

    #region Properties

    /// <summary>The heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InvitePeople, string?>(nameof(Title), "Invite people");

    /// <summary>The line under the heading; null leaves it out.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InvitePeople, string?>(nameof(Description));

    /// <summary>The workspace people join (used in the announcement when someone accepts).</summary>
    public static readonly StyledProperty<string?> WorkspaceNameProperty =
        AvaloniaProperty.Register<InvitePeople, string?>(nameof(WorkspaceName));

    /// <summary>The people who can be found by name or email, when you give them as a list to follow; it wins over <see cref="Directory"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> DirectorySourceProperty =
        AvaloniaProperty.Register<InvitePeople, IEnumerable?>(nameof(DirectorySource));

    /// <summary>The roles a chip can get, in menu order; empty leaves the role buttons out.</summary>
    public static readonly StyledProperty<AvaloniaList<InvitePeopleRole>> RolesProperty =
        AvaloniaProperty.Register<InvitePeople, AvaloniaList<InvitePeopleRole>>(nameof(Roles));

    /// <summary>The plans, smallest first: the next one after <see cref="CurrentPlan"/> is offered when the seats run over.</summary>
    public static readonly StyledProperty<AvaloniaList<InvitePeoplePlan>> PlansProperty =
        AvaloniaProperty.Register<InvitePeople, AvaloniaList<InvitePeoplePlan>>(nameof(Plans));

    /// <summary>The plan in use; its seat limit is the meter's maximum. Null uses the first of <see cref="Plans"/>.</summary>
    public static readonly StyledProperty<InvitePeoplePlan?> CurrentPlanProperty =
        AvaloniaProperty.Register<InvitePeople, InvitePeoplePlan?>(nameof(CurrentPlan), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>How many people a query lists at most (5).</summary>
    public static readonly StyledProperty<int> MaxSuggestionsProperty =
        AvaloniaProperty.Register<InvitePeople, int>(nameof(MaxSuggestions), 5);

    /// <summary>How many people the "Suggested" list of an empty field shows (4).</summary>
    public static readonly StyledProperty<int> SuggestedCountProperty =
        AvaloniaProperty.Register<InvitePeople, int>(nameof(SuggestedCount), 4);

    /// <summary>A list the "Paste a sample list" button pastes; null hides the button.</summary>
    public static readonly StyledProperty<string?> SampleTextProperty =
        AvaloniaProperty.Register<InvitePeople, string?>(nameof(SampleText));

    /// <summary>The example domain of the "incomplete domain" message; null takes the domain of the first directory address.</summary>
    public static readonly StyledProperty<string?> ExampleDomainProperty =
        AvaloniaProperty.Register<InvitePeople, string?>(nameof(ExampleDomain));

    /// <summary>
    /// Sends the invites (called after the rows were added as "Sending"). Report deliveries with <see cref="MarkDelivered"/>;
    /// rows still sending when the task completes are marked sent. An exception takes the rows back and returns the chips
    /// to the field. Null sends nothing and marks the rows sent at once.
    /// </summary>
    public static readonly StyledProperty<Func<InvitePeopleSendRequest, CancellationToken, Task>?> SendActionProperty =
        AvaloniaProperty.Register<InvitePeople, Func<InvitePeopleSendRequest, CancellationToken, Task>?>(nameof(SendAction));

    /// <summary>Sends one invite again; an exception keeps the button as it was and announces the failure.</summary>
    public static readonly StyledProperty<Func<InvitePeoplePendingInvite, CancellationToken, Task>?> ResendActionProperty =
        AvaloniaProperty.Register<InvitePeople, Func<InvitePeoplePendingInvite, CancellationToken, Task>?>(nameof(ResendAction));

    /// <summary>Revokes one invite (the row leaves first); an exception puts the row back.</summary>
    public static readonly StyledProperty<Func<InvitePeoplePendingInvite, CancellationToken, Task>?> RevokeActionProperty =
        AvaloniaProperty.Register<InvitePeople, Func<InvitePeoplePendingInvite, CancellationToken, Task>?>(nameof(RevokeAction));

    /// <summary>Moves the workspace to the given plan; the block switches when the task completes. An exception keeps the plan.</summary>
    public static readonly StyledProperty<Func<InvitePeoplePlan, CancellationToken, Task>?> UpgradeActionProperty =
        AvaloniaProperty.Register<InvitePeople, Func<InvitePeoplePlan, CancellationToken, Task>?>(nameof(UpgradeAction));

    /// <summary>Runs with the <see cref="InvitePeopleSendRequest"/> as its parameter when invites are sent, before <see cref="SendAction"/>.</summary>
    public static readonly StyledProperty<ICommand?> SendCommandProperty =
        AvaloniaProperty.Register<InvitePeople, ICommand?>(nameof(SendCommand));

    /// <summary>True (default) draws a card at most 600 wide with a border; false lays the block flat at the container's width.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<InvitePeople, bool>(nameof(IsFramed), true);

    /// <summary>Shows the reset button beside Send (it calls <see cref="Reset"/>).</summary>
    public static readonly StyledProperty<bool> IsResetVisibleProperty =
        AvaloniaProperty.Register<InvitePeople, bool>(nameof(IsResetVisible));

    /// <summary>Every word the block shows or announces.</summary>
    public static readonly StyledProperty<InvitePeopleLabels> LabelsProperty =
        AvaloniaProperty.Register<InvitePeople, InvitePeopleLabels>(nameof(Labels), new InvitePeopleLabels());

    /// <summary>The chips in the field, in order.</summary>
    public static readonly DirectProperty<InvitePeople, IReadOnlyList<InvitePeopleEntry>> EntriesProperty =
        AvaloniaProperty.RegisterDirect<InvitePeople, IReadOnlyList<InvitePeopleEntry>>(nameof(Entries), o => o.Entries);

    /// <summary>Seats in use: members, pending invites and the chips that can be invited.</summary>
    public static readonly DirectProperty<InvitePeople, int> SeatsUsedProperty =
        AvaloniaProperty.RegisterDirect<InvitePeople, int>(nameof(SeatsUsed), o => o.SeatsUsed);

    /// <summary>True when <see cref="SeatsUsed"/> is more than the plan's limit (Send is held back).</summary>
    public static readonly DirectProperty<InvitePeople, bool> IsOverLimitProperty =
        AvaloniaProperty.RegisterDirect<InvitePeople, bool>(nameof(IsOverLimit), o => o.IsOverLimit);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<InvitePeople, double>(nameof(CompactWidth), 480);

    internal static readonly StyledProperty<double> NarrowContentWidthProperty =
        AvaloniaProperty.Register<InvitePeople, double>(nameof(NarrowContentWidth), 440);

    internal static readonly StyledProperty<IBrush?> HoverBrushProperty =
        AvaloniaProperty.Register<InvitePeople, IBrush?>(nameof(HoverBrush));

    internal static readonly StyledProperty<IBrush?> ArmedBrushProperty =
        AvaloniaProperty.Register<InvitePeople, IBrush?>(nameof(ArmedBrush));

    internal static readonly StyledProperty<IBrush?> ErrorFillBrushProperty =
        AvaloniaProperty.Register<InvitePeople, IBrush?>(nameof(ErrorFillBrush));

    internal static readonly StyledProperty<IBrush?> ErrorStrokeBrushProperty =
        AvaloniaProperty.Register<InvitePeople, IBrush?>(nameof(ErrorStrokeBrush));

    internal static readonly StyledProperty<IBrush?> ErrorGlyphBrushProperty =
        AvaloniaProperty.Register<InvitePeople, IBrush?>(nameof(ErrorGlyphBrush));

    #endregion

    #region Events

    /// <summary>The chips changed (added, removed, a role changed).</summary>
    public static readonly RoutedEvent<InvitePeopleEntriesChangedEventArgs> EntriesChangedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleEntriesChangedEventArgs>(nameof(EntriesChanged), RoutingStrategies.Bubble);

    /// <summary>A chip's role was picked from its menu.</summary>
    public static readonly RoutedEvent<InvitePeopleRoleChangedEventArgs> RoleChangedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleRoleChangedEventArgs>(nameof(RoleChanged), RoutingStrategies.Bubble);

    /// <summary>Invites went out (<see cref="SendAction"/> completed).</summary>
    public static readonly RoutedEvent<InvitePeopleInvitesSentEventArgs> InvitesSentEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleInvitesSentEventArgs>(nameof(InvitesSent), RoutingStrategies.Bubble);

    /// <summary><see cref="SendAction"/> threw; the rows were taken back and the chips are in the field again.</summary>
    public static readonly RoutedEvent<InvitePeopleSendFailedEventArgs> SendFailedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleSendFailedEventArgs>(nameof(SendFailed), RoutingStrategies.Bubble);

    /// <summary>An accepted invite left the list and its person joined the team (<see cref="Members"/>).</summary>
    public static readonly RoutedEvent<InvitePeopleInviteEventArgs> InviteJoinedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleInviteEventArgs>(nameof(InviteJoined), RoutingStrategies.Bubble);

    /// <summary>An invite was sent again.</summary>
    public static readonly RoutedEvent<InvitePeopleInviteEventArgs> InviteResentEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleInviteEventArgs>(nameof(InviteResent), RoutingStrategies.Bubble);

    /// <summary>An invite was revoked.</summary>
    public static readonly RoutedEvent<InvitePeopleInviteEventArgs> InviteRevokedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeopleInviteEventArgs>(nameof(InviteRevoked), RoutingStrategies.Bubble);

    /// <summary>The plan changed after an upgrade.</summary>
    public static readonly RoutedEvent<InvitePeoplePlanUpgradedEventArgs> PlanUpgradedEvent =
        RoutedEvent.Register<InvitePeople, InvitePeoplePlanUpgradedEventArgs>(nameof(PlanUpgraded), RoutingStrategies.Bubble);

    /// <summary><see cref="Reset"/> ran (the reset button or code); simulations that drive the block should stop.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ResetPerformedEvent =
        RoutedEvent.Register<InvitePeople, RoutedEventArgs>(nameof(ResetPerformed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="EntriesChangedEvent"/>
    public event EventHandler<InvitePeopleEntriesChangedEventArgs>? EntriesChanged
    {
        add => AddHandler(EntriesChangedEvent, value);
        remove => RemoveHandler(EntriesChangedEvent, value);
    }

    /// <inheritdoc cref="RoleChangedEvent"/>
    public event EventHandler<InvitePeopleRoleChangedEventArgs>? RoleChanged
    {
        add => AddHandler(RoleChangedEvent, value);
        remove => RemoveHandler(RoleChangedEvent, value);
    }

    /// <inheritdoc cref="InvitesSentEvent"/>
    public event EventHandler<InvitePeopleInvitesSentEventArgs>? InvitesSent
    {
        add => AddHandler(InvitesSentEvent, value);
        remove => RemoveHandler(InvitesSentEvent, value);
    }

    /// <inheritdoc cref="SendFailedEvent"/>
    public event EventHandler<InvitePeopleSendFailedEventArgs>? SendFailed
    {
        add => AddHandler(SendFailedEvent, value);
        remove => RemoveHandler(SendFailedEvent, value);
    }

    /// <inheritdoc cref="InviteJoinedEvent"/>
    public event EventHandler<InvitePeopleInviteEventArgs>? InviteJoined
    {
        add => AddHandler(InviteJoinedEvent, value);
        remove => RemoveHandler(InviteJoinedEvent, value);
    }

    /// <inheritdoc cref="InviteResentEvent"/>
    public event EventHandler<InvitePeopleInviteEventArgs>? InviteResent
    {
        add => AddHandler(InviteResentEvent, value);
        remove => RemoveHandler(InviteResentEvent, value);
    }

    /// <inheritdoc cref="InviteRevokedEvent"/>
    public event EventHandler<InvitePeopleInviteEventArgs>? InviteRevoked
    {
        add => AddHandler(InviteRevokedEvent, value);
        remove => RemoveHandler(InviteRevokedEvent, value);
    }

    /// <inheritdoc cref="PlanUpgradedEvent"/>
    public event EventHandler<InvitePeoplePlanUpgradedEventArgs>? PlanUpgraded
    {
        add => AddHandler(PlanUpgradedEvent, value);
        remove => RemoveHandler(PlanUpgradedEvent, value);
    }

    /// <inheritdoc cref="ResetPerformedEvent"/>
    public event EventHandler<RoutedEventArgs>? ResetPerformed
    {
        add => AddHandler(ResetPerformedEvent, value);
        remove => RemoveHandler(ResetPerformedEvent, value);
    }

    #endregion

    private readonly AvaloniaList<InvitePeopleEntry> _entries = new();
    private INotifyCollectionChanged? _observedDirectory;
    private int _seatsUsed;
    private bool _isOverLimit;

    static InvitePeople()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<InvitePeople>(false);
    }

    /// <summary>Creates the block with empty lists.</summary>
    public InvitePeople()
    {
        SetCurrentValue(RolesProperty, new AvaloniaList<InvitePeopleRole>());
        SetCurrentValue(PlansProperty, new AvaloniaList<InvitePeoplePlan>());
        Directory.CollectionChanged += (_, _) => OnDataChanged(DataPart.Directory);
        Members.CollectionChanged += OnMembersChanged;
        PendingInvites.CollectionChanged += OnPendingChanged;
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    #region CLR accessors

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="WorkspaceNameProperty"/>
    public string? WorkspaceName { get => GetValue(WorkspaceNameProperty); set => SetValue(WorkspaceNameProperty, value); }

    /// <summary>The people who can be found by name or email (the directory), in suggestion order. Members and invited people may be in it too: they show as unavailable.</summary>
    [Content]
    public AvaloniaList<InvitePeoplePerson> Directory { get; } = new();

    /// <inheritdoc cref="DirectorySourceProperty"/>
    public IEnumerable? DirectorySource { get => GetValue(DirectorySourceProperty); set => SetValue(DirectorySourceProperty, value); }

    /// <summary>The team, front first: shown as avatars in the header and counted as seats. A person who accepts is added at the front.</summary>
    public AvaloniaList<InvitePeoplePerson> Members { get; } = new();

    /// <summary>The invites waiting to be accepted, newest first. Sent invites are added at the front; revoked and accepted ones leave.</summary>
    public AvaloniaList<InvitePeoplePendingInvite> PendingInvites { get; } = new();

    /// <inheritdoc cref="RolesProperty"/>
    public AvaloniaList<InvitePeopleRole> Roles { get => GetValue(RolesProperty); set => SetValue(RolesProperty, value); }

    /// <inheritdoc cref="PlansProperty"/>
    public AvaloniaList<InvitePeoplePlan> Plans { get => GetValue(PlansProperty); set => SetValue(PlansProperty, value); }

    /// <inheritdoc cref="CurrentPlanProperty"/>
    public InvitePeoplePlan? CurrentPlan { get => GetValue(CurrentPlanProperty); set => SetValue(CurrentPlanProperty, value); }

    /// <inheritdoc cref="MaxSuggestionsProperty"/>
    public int MaxSuggestions { get => GetValue(MaxSuggestionsProperty); set => SetValue(MaxSuggestionsProperty, value); }

    /// <inheritdoc cref="SuggestedCountProperty"/>
    public int SuggestedCount { get => GetValue(SuggestedCountProperty); set => SetValue(SuggestedCountProperty, value); }

    /// <inheritdoc cref="SampleTextProperty"/>
    public string? SampleText { get => GetValue(SampleTextProperty); set => SetValue(SampleTextProperty, value); }

    /// <inheritdoc cref="ExampleDomainProperty"/>
    public string? ExampleDomain { get => GetValue(ExampleDomainProperty); set => SetValue(ExampleDomainProperty, value); }

    /// <inheritdoc cref="SendActionProperty"/>
    public Func<InvitePeopleSendRequest, CancellationToken, Task>? SendAction { get => GetValue(SendActionProperty); set => SetValue(SendActionProperty, value); }

    /// <inheritdoc cref="ResendActionProperty"/>
    public Func<InvitePeoplePendingInvite, CancellationToken, Task>? ResendAction { get => GetValue(ResendActionProperty); set => SetValue(ResendActionProperty, value); }

    /// <inheritdoc cref="RevokeActionProperty"/>
    public Func<InvitePeoplePendingInvite, CancellationToken, Task>? RevokeAction { get => GetValue(RevokeActionProperty); set => SetValue(RevokeActionProperty, value); }

    /// <inheritdoc cref="UpgradeActionProperty"/>
    public Func<InvitePeoplePlan, CancellationToken, Task>? UpgradeAction { get => GetValue(UpgradeActionProperty); set => SetValue(UpgradeActionProperty, value); }

    /// <inheritdoc cref="SendCommandProperty"/>
    public ICommand? SendCommand { get => GetValue(SendCommandProperty); set => SetValue(SendCommandProperty, value); }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed { get => GetValue(IsFramedProperty); set => SetValue(IsFramedProperty, value); }

    /// <inheritdoc cref="IsResetVisibleProperty"/>
    public bool IsResetVisible { get => GetValue(IsResetVisibleProperty); set => SetValue(IsResetVisibleProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public InvitePeopleLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="EntriesProperty"/>
    public IReadOnlyList<InvitePeopleEntry> Entries => _entries;

    /// <inheritdoc cref="SeatsUsedProperty"/>
    public int SeatsUsed { get => _seatsUsed; private set => SetAndRaise(SeatsUsedProperty, ref _seatsUsed, value); }

    /// <inheritdoc cref="IsOverLimitProperty"/>
    public bool IsOverLimit { get => _isOverLimit; private set => SetAndRaise(IsOverLimitProperty, ref _isOverLimit, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double NarrowContentWidth { get => GetValue(NarrowContentWidthProperty); set => SetValue(NarrowContentWidthProperty, value); }

    internal IBrush? HoverBrush { get => GetValue(HoverBrushProperty); set => SetValue(HoverBrushProperty, value); }

    internal IBrush? ArmedBrush { get => GetValue(ArmedBrushProperty); set => SetValue(ArmedBrushProperty, value); }

    internal IBrush? ErrorFillBrush { get => GetValue(ErrorFillBrushProperty); set => SetValue(ErrorFillBrushProperty, value); }

    internal IBrush? ErrorStrokeBrush { get => GetValue(ErrorStrokeBrushProperty); set => SetValue(ErrorStrokeBrushProperty, value); }

    internal IBrush? ErrorGlyphBrush { get => GetValue(ErrorGlyphBrushProperty); set => SetValue(ErrorGlyphBrushProperty, value); }

    #endregion

    /// <summary>The directory in effect: <see cref="DirectorySource"/> when set, otherwise <see cref="Directory"/>.</summary>
    internal IReadOnlyList<InvitePeoplePerson> EffectiveDirectory =>
        DirectorySource is { } source ? source.OfType<InvitePeoplePerson>().ToList() : Directory;

    /// <summary>The plan in effect: <see cref="CurrentPlan"/>, or the first plan.</summary>
    internal InvitePeoplePlan? EffectivePlan => CurrentPlan ?? (Plans.Count > 0 ? Plans[0] : null);

    /// <summary>The plan after <see cref="EffectivePlan"/>, or null on the largest one.</summary>
    internal InvitePeoplePlan? NextPlan
    {
        get
        {
            var plan = EffectivePlan;
            var index = plan is null ? -1 : Plans.IndexOf(plan);
            return index >= 0 && index + 1 < Plans.Count ? Plans[index + 1] : null;
        }
    }

    /// <summary>The role new chips get.</summary>
    internal InvitePeopleRole? DefaultRole => Roles.FirstOrDefault(r => r.IsDefault) ?? (Roles.Count > 0 ? Roles[0] : null);

    internal enum DataPart
    {
        Directory,
        Roles,
        Plans
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
            UpdateBreakpoints();
        }
        else if (change.Property == DirectorySourceProperty)
        {
            if (_observedDirectory is not null)
            {
                _observedDirectory.CollectionChanged -= OnDirectorySourceChanged;
            }
            _observedDirectory = DirectorySource as INotifyCollectionChanged;
            if (_observedDirectory is not null)
            {
                _observedDirectory.CollectionChanged += OnDirectorySourceChanged;
            }
            OnDataChanged(DataPart.Directory);
        }
        else if (change.Property == RolesProperty)
        {
            if (change.OldValue is AvaloniaList<InvitePeopleRole> oldRoles)
            {
                oldRoles.CollectionChanged -= OnRolesChanged;
            }
            if (change.NewValue is AvaloniaList<InvitePeopleRole> newRoles)
            {
                newRoles.CollectionChanged += OnRolesChanged;
            }
            OnDataChanged(DataPart.Roles);
        }
        else if (change.Property == PlansProperty)
        {
            if (change.OldValue is AvaloniaList<InvitePeoplePlan> oldPlans)
            {
                oldPlans.CollectionChanged -= OnPlansChanged;
            }
            if (change.NewValue is AvaloniaList<InvitePeoplePlan> newPlans)
            {
                newPlans.CollectionChanged += OnPlansChanged;
            }
            OnDataChanged(DataPart.Plans);
        }
        else if (change.Property == CurrentPlanProperty)
        {
            UpdateSeats();
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == LabelsProperty ||
                 change.Property == SampleTextProperty || change.Property == IsResetVisibleProperty)
        {
            UpdateTexts();
        }
        else if (change.Property == CompactWidthProperty || change.Property == NarrowContentWidthProperty)
        {
            UpdateBreakpoints();
        }
    }

    private void OnDirectorySourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged(DataPart.Directory);

    private void OnRolesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged(DataPart.Roles);

    private void OnPlansChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged(DataPart.Plans);

    private void OnDataChanged(DataPart part)
    {
        _roster = null;
        if (part == DataPart.Plans)
        {
            UpdateSeats();
        }
        if (_popup?.IsOpen == true)
        {
            RefreshSuggestions();
        }
    }

    private void RaiseEntriesChanged()
    {
        RaiseEvent(new InvitePeopleEntriesChangedEventArgs(EntriesChangedEvent, _entries.ToList()));
    }
}
