using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A team's seats and what they cost, in one card: avatars and a seat meter show members, pending invites and open seats
/// against the seats paid for; a stepper changes the seat count while today's prorated charge and the new recurring total
/// roll live; pending invites can be resent, or revoked with an inline undo; and Update seats goes through a confirm step
/// before anything is charged. When every seat is taken the card says so and offers to add one. The change is applied
/// by <see cref="UpdateSeatsAsync"/>; without handlers the block simulates its backend and, by default, plays a short
/// demonstration loop until someone touches it.
/// </summary>
/// <example><code>
/// &lt;arc:SeatManager Workspace="Tidewater" Purchased="12" PeriodStart="2026-10-18" PeriodEnd="2026-11-18"&gt;
///     &lt;arc:SeatManager.Plan&gt;&lt;arc:SeatManagerPlan Name="Team" PricePerSeat="18" MaxSeats="25" /&gt;&lt;/arc:SeatManager.Plan&gt;
/// &lt;/arc:SeatManager&gt;
/// </code></example>
[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(MeterPart, typeof(SeatManagerMeter))]
[TemplatePart(NoticePart, typeof(ArcContentSwap))]
[TemplatePart(MinusPart, typeof(SeatManagerStepButton))]
[TemplatePart(SpinPart, typeof(SeatManagerSpinValue))]
[TemplatePart(PlusPart, typeof(SeatManagerStepButton))]
[TemplatePart(InviteListPart, typeof(StackPanel))]
[TemplatePart(FooterTextPart, typeof(ArcContentSwap))]
[TemplatePart(FooterActionsPart, typeof(ArcContentSwap))]
[PseudoClasses(NarrowPseudoClass, AddingPseudoClass, RemovingPseudoClass, FullPseudoClass, ConfirmPseudoClass, SavingPseudoClass,
    DonePseudoClass, ErrorPseudoClass)]
public partial class SeatManager : TemplatedControl
{
    public const string CardPart = "PART_Card";
    public const string TitlePart = "PART_Title";
    public const string PlanNamePart = "PART_PlanName";
    public const string BadgePart = "PART_Badge";
    public const string SubtitlePart = "PART_Subtitle";
    public const string AvatarsPart = "PART_Avatars";
    public const string UsedPart = "PART_Used";
    public const string MeterPart = "PART_Meter";
    public const string LegendMembersPart = "PART_LegendMembers";
    public const string LegendInvitedPart = "PART_LegendInvited";
    public const string LegendOpenPart = "PART_LegendOpen";
    public const string LegendDeltaPart = "PART_LegendDelta";
    public const string DotMembersPart = "PART_DotMembers";
    public const string DotInvitedPart = "PART_DotInvited";
    public const string DotOpenPart = "PART_DotOpen";
    public const string NoticePart = "PART_Notice";
    public const string StepperTitlePart = "PART_StepperTitle";
    public const string PurchasedPart = "PART_Purchased";
    public const string MinusPart = "PART_Minus";
    public const string SpinPart = "PART_Spin";
    public const string PlusPart = "PART_Plus";
    public const string DueLabelPart = "PART_DueLabel";
    public const string DueNotePart = "PART_DueNote";
    public const string DuePart = "PART_Due";
    public const string FromLabelPart = "PART_FromLabel";
    public const string FromNotePart = "PART_FromNote";
    public const string NextPart = "PART_Next";
    public const string InvitesTitlePart = "PART_InvitesTitle";
    public const string InviteCountPart = "PART_InviteCount";
    public const string InviteListPart = "PART_InviteList";
    public const string InvitesEmptyPart = "PART_InvitesEmpty";
    public const string InvitesEmptyTextPart = "PART_InvitesEmptyText";
    public const string FooterTextPart = "PART_FooterText";
    public const string FooterActionsPart = "PART_FooterActions";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the card is narrower than its narrow width (460).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the stepper adds seats.</summary>
    public const string AddingPseudoClass = ":adding";

    /// <summary>Set while the stepper removes seats.</summary>
    public const string RemovingPseudoClass = ":removing";

    /// <summary>Set while every paid seat is taken and nothing changes.</summary>
    public const string FullPseudoClass = ":full";

    /// <summary>Set in the confirm step.</summary>
    public const string ConfirmPseudoClass = ":confirm";

    /// <summary>Set while the change is applied.</summary>
    public const string SavingPseudoClass = ":saving";

    /// <summary>Set while the done message shows.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>Set after a failed update.</summary>
    public const string ErrorPseudoClass = ":error";

    #region Properties

    /// <summary>The workspace named in the line under the title.</summary>
    public static readonly StyledProperty<string?> WorkspaceProperty =
        AvaloniaProperty.Register<SeatManager, string?>(nameof(Workspace));

    /// <summary>The plan: its badge, the price of a seat, the billing interval, the currency and the seat cap.</summary>
    public static readonly StyledProperty<SeatManagerPlan?> PlanProperty =
        AvaloniaProperty.Register<SeatManager, SeatManagerPlan?>(nameof(Plan));

    /// <summary>The seats paid for now. Two-way: becomes the new count after a successful update.</summary>
    public static readonly StyledProperty<int> PurchasedProperty =
        AvaloniaProperty.Register<SeatManager, int>(nameof(Purchased), defaultBindingMode: BindingMode.TwoWay, coerce: CoercePurchased);

    /// <summary>
    /// The seat count on the stepper. Two-way; kept between <see cref="Minimum"/> and <see cref="Maximum"/>, and follows
    /// <see cref="Purchased"/> while nothing is being changed.
    /// </summary>
    public static readonly StyledProperty<int> TargetProperty =
        AvaloniaProperty.Register<SeatManager, int>(nameof(Target), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceTarget);

    /// <summary>The fewest seats the stepper allows: members plus the invites that still hold a seat.</summary>
    public static readonly DirectProperty<SeatManager, int> MinimumProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, int>(nameof(Minimum), o => o.Minimum);

    /// <summary>The most seats the stepper allows: the plan's <see cref="SeatManagerPlan.MaxSeats"/> (999 without a cap).</summary>
    public static readonly DirectProperty<SeatManager, int> MaximumProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, int>(nameof(Maximum), o => o.Maximum);

    /// <summary>The members holding a seat. The first four are drawn as avatars, the rest as a "+N" count.</summary>
    public static readonly DirectProperty<SeatManager, IList<SeatManagerMember>> MembersProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, IList<SeatManagerMember>>(nameof(Members), o => o.Members, (o, v) => o.Members = v);

    /// <summary>The total number of members when <see cref="Members"/> is only a sample for the avatars; null counts the list.</summary>
    public static readonly StyledProperty<int?> MemberCountProperty =
        AvaloniaProperty.Register<SeatManager, int?>(nameof(MemberCount));

    /// <summary>The pending invites; each holds a seat until it is accepted or revoked. A revoked invite leaves the list when its undo window closes.</summary>
    public static readonly DirectProperty<SeatManager, IList<SeatManagerInvite>> InvitesProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, IList<SeatManagerInvite>>(nameof(Invites), o => o.Invites, (o, v) => o.Invites = v);

    /// <summary>The first day of the current billing period.</summary>
    public static readonly StyledProperty<DateTime> PeriodStartProperty =
        AvaloniaProperty.Register<SeatManager, DateTime>(nameof(PeriodStart));

    /// <summary>The day the current billing period ends; the new total starts here and added seats are prorated up to it.</summary>
    public static readonly StyledProperty<DateTime> PeriodEndProperty =
        AvaloniaProperty.Register<SeatManager, DateTime>(nameof(PeriodEnd));

    /// <summary>"Today" for the proration; null uses the local date.</summary>
    public static readonly StyledProperty<DateTime?> NowProperty =
        AvaloniaProperty.Register<SeatManager, DateTime?>(nameof(Now));

    /// <summary>The card named in the footer and in the confirm step; null hides it.</summary>
    public static readonly StyledProperty<SeatManagerPaymentMethod?> PaymentMethodProperty =
        AvaloniaProperty.Register<SeatManager, SeatManagerPaymentMethod?>(nameof(PaymentMethod));

    /// <summary>The address named in the done message.</summary>
    public static readonly StyledProperty<string?> ReceiptEmailProperty =
        AvaloniaProperty.Register<SeatManager, string?>(nameof(ReceiptEmail));

    /// <summary>
    /// Applies a seat change. The block shows the spinner while it runs; if it throws, the exception's message appears in the
    /// footer with Try again. Null simulates a 900 ms charge (<see cref="SeatManagerSamples.ChargeDelay"/>).
    /// </summary>
    public static readonly StyledProperty<Func<SeatManagerQuote, CancellationToken, Task>?> UpdateSeatsAsyncProperty =
        AvaloniaProperty.Register<SeatManager, Func<SeatManagerQuote, CancellationToken, Task>?>(nameof(UpdateSeatsAsync));

    /// <summary>Sends an invite again. The button shows a spinner, then "Sent"; if it throws the button returns. Null simulates 660 ms.</summary>
    public static readonly StyledProperty<Func<SeatManagerInvite, CancellationToken, Task>?> ResendInviteAsyncProperty =
        AvaloniaProperty.Register<SeatManager, Func<SeatManagerInvite, CancellationToken, Task>?>(nameof(ResendInviteAsync));

    /// <summary>Revokes an invite. Called when the undo window closes; if it throws, the invite goes back where it was.</summary>
    public static readonly StyledProperty<Func<SeatManagerInvite, CancellationToken, Task>?> RevokeInviteAsyncProperty =
        AvaloniaProperty.Register<SeatManager, Func<SeatManagerInvite, CancellationToken, Task>?>(nameof(RevokeInviteAsync));

    /// <summary>How long Undo stays on a revoked invite (3 seconds).</summary>
    public static readonly StyledProperty<TimeSpan> UndoDurationProperty =
        AvaloniaProperty.Register<SeatManager, TimeSpan>(nameof(UndoDuration), TimeSpan.FromSeconds(3));

    /// <summary>
    /// Whether the demonstration loop plays (add three seats, pay, resend, revoke, reset; 12.7 s, paused off screen). Null
    /// (default) plays it when none of the three handlers is set. Any pointer press or key press inside the card stops it for
    /// good and leaves the card as it is.
    /// </summary>
    public static readonly StyledProperty<bool?> IsAutoPlayEnabledProperty =
        AvaloniaProperty.Register<SeatManager, bool?>(nameof(IsAutoPlayEnabled));

    /// <summary>The quote for the stepper's seat count against the seats paid for.</summary>
    public static readonly DirectProperty<SeatManager, SeatManagerQuote> QuoteProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, SeatManagerQuote>(nameof(Quote), o => o.Quote);

    /// <summary>Where the footer is in applying a change.</summary>
    public static readonly DirectProperty<SeatManager, SeatManagerPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, SeatManagerPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The message of the last failed update while <see cref="Phase"/> is Error, otherwise null.</summary>
    public static readonly DirectProperty<SeatManager, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    /// <summary>True while the demonstration loop is playing.</summary>
    public static readonly DirectProperty<SeatManager, bool> IsAutoPlayingProperty =
        AvaloniaProperty.RegisterDirect<SeatManager, bool>(nameof(IsAutoPlaying), o => o.IsAutoPlaying);

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<SeatManagerLabels> LabelsProperty =
        AvaloniaProperty.Register<SeatManager, SeatManagerLabels>(nameof(Labels), new SeatManagerLabels());

    /// <summary>Card width below which the layout tightens (theme token, 460).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<SeatManager, double>(nameof(NarrowWidth), 460);

    /// <summary>Glyph color of success states (theme token).</summary>
    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<SeatManager, IBrush?>(nameof(SuccessBrush));

    /// <summary>Glyph color of the full-team notice (theme token).</summary>
    internal static readonly StyledProperty<IBrush?> WarningBrushProperty =
        AvaloniaProperty.Register<SeatManager, IBrush?>(nameof(WarningBrush));

    /// <summary>Height of an invite row (theme token).</summary>
    internal static readonly StyledProperty<double> InviteRowHeightProperty =
        AvaloniaProperty.Register<SeatManager, double>(nameof(InviteRowHeight), 56);

    /// <summary>Margin of the invite actions (theme token).</summary>
    internal static readonly StyledProperty<Thickness> InviteActionsMarginProperty =
        AvaloniaProperty.Register<SeatManager, Thickness>(nameof(InviteActionsMargin), new Thickness(0, 0, -12, 0));

    /// <summary>Raised whenever the stepper changes the quote.</summary>
    public static readonly RoutedEvent<SeatManagerQuoteChangedEventArgs> QuoteChangedEvent =
        RoutedEvent.Register<SeatManager, SeatManagerQuoteChangedEventArgs>(nameof(QuoteChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a seat change was applied.</summary>
    public static readonly RoutedEvent<SeatManagerSeatsChangedEventArgs> SeatsChangedEvent =
        RoutedEvent.Register<SeatManager, SeatManagerSeatsChangedEventArgs>(nameof(SeatsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised on every change of <see cref="Phase"/>.</summary>
    public static readonly RoutedEvent<SeatManagerPhaseChangedEventArgs> PhaseChangedEvent =
        RoutedEvent.Register<SeatManager, SeatManagerPhaseChangedEventArgs>(nameof(PhaseChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an invite went out again.</summary>
    public static readonly RoutedEvent<SeatManagerInviteEventArgs> InviteResentEvent =
        RoutedEvent.Register<SeatManager, SeatManagerInviteEventArgs>(nameof(InviteResent), RoutingStrategies.Bubble);

    /// <summary>Raised when a revoked invite's undo window closed and the invite left the list.</summary>
    public static readonly RoutedEvent<SeatManagerInviteEventArgs> InviteRevokedEvent =
        RoutedEvent.Register<SeatManager, SeatManagerInviteEventArgs>(nameof(InviteRevoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a revoked invite came back: Undo, or a revoke handler that failed.</summary>
    public static readonly RoutedEvent<SeatManagerInviteEventArgs> InviteRestoredEvent =
        RoutedEvent.Register<SeatManager, SeatManagerInviteEventArgs>(nameof(InviteRestored), RoutingStrategies.Bubble);

    /// <inheritdoc cref="WorkspaceProperty"/>
    public string? Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }

    /// <inheritdoc cref="PlanProperty"/>
    public SeatManagerPlan? Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }

    /// <inheritdoc cref="PurchasedProperty"/>
    public int Purchased { get => GetValue(PurchasedProperty); set => SetValue(PurchasedProperty, value); }

    /// <inheritdoc cref="TargetProperty"/>
    public int Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public int Minimum { get => _minimum; private set => SetAndRaise(MinimumProperty, ref _minimum, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public int Maximum { get => _maximum; private set => SetAndRaise(MaximumProperty, ref _maximum, value); }

    /// <inheritdoc cref="MembersProperty"/>
    public IList<SeatManagerMember> Members
    {
        get => _members;
        set
        {
            var old = _members;
            if (SetAndRaise(MembersProperty, ref _members, value ?? new AvaloniaList<SeatManagerMember>()))
            {
                Watch(old, _members, OnMembersCollectionChanged);
                OnMembersChanged();
            }
        }
    }

    /// <inheritdoc cref="MemberCountProperty"/>
    public int? MemberCount { get => GetValue(MemberCountProperty); set => SetValue(MemberCountProperty, value); }

    /// <inheritdoc cref="InvitesProperty"/>
    public IList<SeatManagerInvite> Invites
    {
        get => _invites;
        set
        {
            var old = _invites;
            if (SetAndRaise(InvitesProperty, ref _invites, value ?? new AvaloniaList<SeatManagerInvite>()))
            {
                Watch(old, _invites, OnInvitesCollectionChanged);
                OnInvitesChanged();
            }
        }
    }

    /// <inheritdoc cref="PeriodStartProperty"/>
    public DateTime PeriodStart { get => GetValue(PeriodStartProperty); set => SetValue(PeriodStartProperty, value); }

    /// <inheritdoc cref="PeriodEndProperty"/>
    public DateTime PeriodEnd { get => GetValue(PeriodEndProperty); set => SetValue(PeriodEndProperty, value); }

    /// <inheritdoc cref="NowProperty"/>
    public DateTime? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="PaymentMethodProperty"/>
    public SeatManagerPaymentMethod? PaymentMethod { get => GetValue(PaymentMethodProperty); set => SetValue(PaymentMethodProperty, value); }

    /// <inheritdoc cref="ReceiptEmailProperty"/>
    public string? ReceiptEmail { get => GetValue(ReceiptEmailProperty); set => SetValue(ReceiptEmailProperty, value); }

    /// <inheritdoc cref="UpdateSeatsAsyncProperty"/>
    public Func<SeatManagerQuote, CancellationToken, Task>? UpdateSeatsAsync
    {
        get => GetValue(UpdateSeatsAsyncProperty);
        set => SetValue(UpdateSeatsAsyncProperty, value);
    }

    /// <inheritdoc cref="ResendInviteAsyncProperty"/>
    public Func<SeatManagerInvite, CancellationToken, Task>? ResendInviteAsync
    {
        get => GetValue(ResendInviteAsyncProperty);
        set => SetValue(ResendInviteAsyncProperty, value);
    }

    /// <inheritdoc cref="RevokeInviteAsyncProperty"/>
    public Func<SeatManagerInvite, CancellationToken, Task>? RevokeInviteAsync
    {
        get => GetValue(RevokeInviteAsyncProperty);
        set => SetValue(RevokeInviteAsyncProperty, value);
    }

    /// <inheritdoc cref="UndoDurationProperty"/>
    public TimeSpan UndoDuration { get => GetValue(UndoDurationProperty); set => SetValue(UndoDurationProperty, value); }

    /// <inheritdoc cref="IsAutoPlayEnabledProperty"/>
    public bool? IsAutoPlayEnabled { get => GetValue(IsAutoPlayEnabledProperty); set => SetValue(IsAutoPlayEnabledProperty, value); }

    /// <inheritdoc cref="QuoteProperty"/>
    public SeatManagerQuote Quote { get => _quote; private set => SetAndRaise(QuoteProperty, ref _quote, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public SeatManagerPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _errorMessage; private set => SetAndRaise(ErrorMessageProperty, ref _errorMessage, value); }

    /// <inheritdoc cref="IsAutoPlayingProperty"/>
    public bool IsAutoPlaying { get => _isAutoPlaying; private set => SetAndRaise(IsAutoPlayingProperty, ref _isAutoPlaying, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public SeatManagerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    internal IBrush? WarningBrush { get => GetValue(WarningBrushProperty); set => SetValue(WarningBrushProperty, value); }
    internal double InviteRowHeight { get => GetValue(InviteRowHeightProperty); set => SetValue(InviteRowHeightProperty, value); }
    internal Thickness InviteActionsMargin { get => GetValue(InviteActionsMarginProperty); set => SetValue(InviteActionsMarginProperty, value); }

    /// <inheritdoc cref="QuoteChangedEvent"/>
    public event EventHandler<SeatManagerQuoteChangedEventArgs>? QuoteChanged
    {
        add => AddHandler(QuoteChangedEvent, value);
        remove => RemoveHandler(QuoteChangedEvent, value);
    }

    /// <inheritdoc cref="SeatsChangedEvent"/>
    public event EventHandler<SeatManagerSeatsChangedEventArgs>? SeatsChanged
    {
        add => AddHandler(SeatsChangedEvent, value);
        remove => RemoveHandler(SeatsChangedEvent, value);
    }

    /// <inheritdoc cref="PhaseChangedEvent"/>
    public event EventHandler<SeatManagerPhaseChangedEventArgs>? PhaseChanged
    {
        add => AddHandler(PhaseChangedEvent, value);
        remove => RemoveHandler(PhaseChangedEvent, value);
    }

    /// <inheritdoc cref="InviteResentEvent"/>
    public event EventHandler<SeatManagerInviteEventArgs>? InviteResent
    {
        add => AddHandler(InviteResentEvent, value);
        remove => RemoveHandler(InviteResentEvent, value);
    }

    /// <inheritdoc cref="InviteRevokedEvent"/>
    public event EventHandler<SeatManagerInviteEventArgs>? InviteRevoked
    {
        add => AddHandler(InviteRevokedEvent, value);
        remove => RemoveHandler(InviteRevokedEvent, value);
    }

    /// <inheritdoc cref="InviteRestoredEvent"/>
    public event EventHandler<SeatManagerInviteEventArgs>? InviteRestored
    {
        add => AddHandler(InviteRestoredEvent, value);
        remove => RemoveHandler(InviteRestoredEvent, value);
    }

    #endregion

    private IList<SeatManagerMember> _members = new AvaloniaList<SeatManagerMember>();
    private IList<SeatManagerInvite> _invites = new AvaloniaList<SeatManagerInvite>();
    private int _minimum;
    private int _maximum = NoCap;
    private SeatManagerQuote _quote;
    private SeatManagerPhase _phase;
    private string? _errorMessage;
    private bool _isAutoPlaying;

    /// <summary>The stepper's maximum when the plan has no cap.</summary>
    internal const int NoCap = 999;

    static SeatManager()
    {
        FocusableProperty.OverrideDefaultValue<SeatManager>(false);
    }

    public SeatManager()
    {
        Watch(null, _members, OnMembersCollectionChanged);
        Watch(null, _invites, OnInvitesCollectionChanged);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        RecomputeLimits();
    }

    private static void Watch(object? old, object? now, System.Collections.Specialized.NotifyCollectionChangedEventHandler handler)
    {
        if (old is System.Collections.Specialized.INotifyCollectionChanged o)
        {
            o.CollectionChanged -= handler;
        }
        if (now is System.Collections.Specialized.INotifyCollectionChanged n)
        {
            n.CollectionChanged += handler;
        }
    }

    private void OnMembersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => OnMembersChanged();

    private void OnInvitesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => OnInvitesChanged();

    private static int CoercePurchased(AvaloniaObject owner, int value) => Math.Max(0, value);

    private static int CoerceTarget(AvaloniaObject owner, int value)
    {
        var block = (SeatManager)owner;
        var min = block._minimum;
        var max = Math.Max(min, block._maximum);
        return Math.Clamp(value, min, max);
    }
}
