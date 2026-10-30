using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A trial status banner (Arc Trial banner, design reference): a ring counts down the days left, the copy names what the
/// workspace loses, and the upgrade path is one click away. It comes as a slim <see cref="TrialBannerVariant.Banner"/>
/// row, a sidebar <see cref="TrialBannerVariant.Card"/> with the feature list, or a header
/// <see cref="TrialBannerVariant.Pill"/> that opens the card in a popover. All three share one state model: active, the
/// warning tone in the last <see cref="WarnAt"/> days (the copy then names the end day), expired (a lock) and upgraded
/// (a check). Dismiss and snooze fold the banner into the trial pill, which opens it again.
/// <para>
/// Give it <see cref="DaysLeft"/> from your billing provider, or <see cref="EndsAt"/> and let it count the local
/// calendar days with <see cref="TimeProvider"/> (it recounts at local midnight). <see cref="AddPaymentHandler"/> opens your
/// checkout: return once a payment method is on file to show the upgraded state, throw to show Try again. Without it the
/// call is simulated (900ms). With neither <see cref="DaysLeft"/> nor <see cref="EndsAt"/> there is no trial to show: the
/// banner reads "No trial in progress" over an empty ring, without actions. The plan, the workspace, the loss and the
/// features are your app's data and start empty.
/// </para>
/// </summary>
[TemplatePart(StagePart, typeof(TrialBannerStage))]
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(BannerFacePart, typeof(Border))]
[TemplatePart(BannerRowPart, typeof(TrialBannerRow))]
[TemplatePart(CardFacePart, typeof(Border))]
[TemplatePart(PillPart, typeof(TrialBannerPill))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(BannerPseudoClass, CardPseudoClass, PillPseudoClass, FoldedPseudoClass, WarningPseudoClass,
    ExpiredPseudoClass, UpgradedPseudoClass, BusyPseudoClass, FailedPseudoClass, CompactPseudoClass, PopoverPseudoClass)]
public partial class TrialBanner : TemplatedControl
{
    internal const string StagePart = "PART_Stage";
    internal const string SurfacePart = "PART_Surface";
    internal const string BannerFacePart = "PART_BannerFace";
    internal const string BannerRowPart = "PART_BannerRow";
    internal const string CardFacePart = "PART_CardFace";
    internal const string PillPart = "PART_Pill";
    internal const string LivePart = "PART_Live";

    internal const string BannerPseudoClass = ":banner";
    internal const string CardPseudoClass = ":card";
    internal const string PillPseudoClass = ":pill";
    internal const string FoldedPseudoClass = ":folded";
    internal const string WarningPseudoClass = ":warning";
    internal const string ExpiredPseudoClass = ":expired";
    internal const string UpgradedPseudoClass = ":upgraded";
    internal const string BusyPseudoClass = ":busy";
    internal const string FailedPseudoClass = ":failed";
    internal const string CompactPseudoClass = ":compact";
    internal const string PopoverPseudoClass = ":popover";

    /// <summary>Banner (a slim row), Card (a sidebar card with the features) or Pill (a header pill that opens the card).</summary>
    public static readonly StyledProperty<TrialBannerVariant> VariantProperty =
        AvaloniaProperty.Register<TrialBanner, TrialBannerVariant>(nameof(Variant));

    /// <summary>The plan being trialed ("Pro"), used in every line of copy. Empty by default.</summary>
    public static readonly StyledProperty<string> PlanProperty =
        AvaloniaProperty.Register<TrialBanner, string>(nameof(Plan), string.Empty);

    /// <summary>
    /// Whole days left, from your billing provider; 0 or less shows the expired state. Null counts them from
    /// <see cref="EndsAt"/> and today; with neither, the sample shows 9.
    /// </summary>
    public static readonly StyledProperty<int?> DaysLeftProperty =
        AvaloniaProperty.Register<TrialBanner, int?>(nameof(DaysLeft));

    /// <summary>The trial's length in days. The ring shows the days left of it.</summary>
    public static readonly StyledProperty<int> TrialDaysProperty =
        AvaloniaProperty.Register<TrialBanner, int>(nameof(TrialDays), 14);

    /// <summary>
    /// When the trial ends, for the date copy (and the days left when <see cref="DaysLeft"/> is null). Null uses a sample
    /// date: October 27, 2026 plus the days left. Pass your real end date.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset?> EndsAtProperty =
        AvaloniaProperty.Register<TrialBanner, DateTimeOffset?>(nameof(EndsAt));

    /// <summary>The last N days switch to the warning tone, with copy that names the end day.</summary>
    public static readonly StyledProperty<int> WarnAtProperty =
        AvaloniaProperty.Register<TrialBanner, int>(nameof(WarnAt), 3);

    /// <summary>The workspace's name, used in the expired and upgraded copy. Empty (the default) reads "Your workspace".</summary>
    public static readonly StyledProperty<string> WorkspaceProperty =
        AvaloniaProperty.Register<TrialBanner, string>(nameof(Workspace), string.Empty);

    /// <summary>One short phrase for the banner copy ("SAML SSO and 12 seats"). Keep it short so the banner stays on one line. Empty by default.</summary>
    public static readonly StyledProperty<string> LossSummaryProperty =
        AvaloniaProperty.Register<TrialBanner, string>(nameof(LossSummary), string.Empty);

    /// <summary>Whether a payment method is on file (two-way). Set by a successful <see cref="AddPaymentHandler"/>.</summary>
    public static readonly StyledProperty<bool> IsUpgradedProperty =
        AvaloniaProperty.Register<TrialBanner, bool>(nameof(IsUpgraded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the banner is folded after a dismiss or snooze (two-way). Set it to false to show the banner again.</summary>
    public static readonly StyledProperty<bool> IsCollapsedProperty =
        AvaloniaProperty.Register<TrialBanner, bool>(nameof(IsCollapsed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>After a dismiss or snooze: fold into the trial pill, which opens the banner again, or render nothing.</summary>
    public static readonly StyledProperty<TrialBannerCollapse> CollapseToProperty =
        AvaloniaProperty.Register<TrialBanner, TrialBannerCollapse>(nameof(CollapseTo));

    /// <summary>Shows the dismiss button.</summary>
    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<TrialBanner, bool>(nameof(IsDismissible), true);

    /// <summary>Shows the snooze menu.</summary>
    public static readonly StyledProperty<bool> IsSnoozableProperty =
        AvaloniaProperty.Register<TrialBanner, bool>(nameof(IsSnoozable), true);

    /// <summary>
    /// Opens checkout or the billing portal. Return once a payment method is on file to show the upgraded state; throw to
    /// show the error state with Try again. The token is cancelled when the banner leaves the window. Null simulates it (900ms).
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task>?> AddPaymentHandlerProperty =
        AvaloniaProperty.Register<TrialBanner, Func<CancellationToken, Task>?>(nameof(AddPaymentHandler));

    /// <summary>Where "today" comes from when the days are counted from <see cref="EndsAt"/>, and the snooze dates. The system clock by default.</summary>
    public static readonly StyledProperty<TimeProvider> TimeProviderProperty =
        AvaloniaProperty.Register<TrialBanner, TimeProvider>(nameof(TimeProvider), TimeProvider.System);

    /// <summary>Every word the banner shows or says.</summary>
    public static readonly StyledProperty<TrialBannerLabels> LabelsProperty =
        AvaloniaProperty.Register<TrialBanner, TrialBannerLabels>(nameof(Labels), TrialBannerLabels.Default);

    /// <summary>Where the trial is: active, warning, expired or upgraded.</summary>
    public static readonly DirectProperty<TrialBanner, TrialStatus> StatusProperty =
        AvaloniaProperty.RegisterDirect<TrialBanner, TrialStatus>(nameof(Status), o => o.Status);

    /// <summary>Raised when the person presses Compare plans. Bubbles. Open your plans page and set Handled; unhandled, the banner shows a short simulated note.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ComparePlansRequestedEvent =
        RoutedEvent.Register<TrialBanner, RoutedEventArgs>(nameof(ComparePlansRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person presses Manage billing in the upgraded state. Bubbles. Set Handled; unhandled, the banner shows a short simulated note.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ManageBillingRequestedEvent =
        RoutedEvent.Register<TrialBanner, RoutedEventArgs>(nameof(ManageBillingRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the person dismisses the banner. Bubbles. Persist it.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> DismissedEvent =
        RoutedEvent.Register<TrialBanner, RoutedEventArgs>(nameof(Dismissed), RoutingStrategies.Bubble);

    /// <summary>Raised with the chosen snooze and when it ends. Bubbles. Persist <see cref="TrialSnoozedEventArgs.Until"/> and stop rendering the banner until then.</summary>
    public static readonly RoutedEvent<TrialSnoozedEventArgs> SnoozedEvent =
        RoutedEvent.Register<TrialBanner, TrialSnoozedEventArgs>(nameof(Snoozed), RoutingStrategies.Bubble);

    private readonly AvaloniaList<TrialFeature> _features = [];
    private TrialStatus _status;

    /// <summary>Creates the banner.</summary>
    public TrialBanner()
    {
        SpecialFocusModality.Watch();
        _features.CollectionChanged += OnFeaturesChanged;
    }

    /// <inheritdoc cref="VariantProperty"/>
    public TrialBannerVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <inheritdoc cref="PlanProperty"/>
    public string Plan { get => GetValue(PlanProperty); set => SetValue(PlanProperty, value); }

    /// <inheritdoc cref="DaysLeftProperty"/>
    public int? DaysLeft { get => GetValue(DaysLeftProperty); set => SetValue(DaysLeftProperty, value); }

    /// <inheritdoc cref="TrialDaysProperty"/>
    public int TrialDays { get => GetValue(TrialDaysProperty); set => SetValue(TrialDaysProperty, value); }

    /// <inheritdoc cref="EndsAtProperty"/>
    public DateTimeOffset? EndsAt { get => GetValue(EndsAtProperty); set => SetValue(EndsAtProperty, value); }

    /// <inheritdoc cref="WarnAtProperty"/>
    public int WarnAt { get => GetValue(WarnAtProperty); set => SetValue(WarnAtProperty, value); }

    /// <inheritdoc cref="WorkspaceProperty"/>
    public string Workspace { get => GetValue(WorkspaceProperty); set => SetValue(WorkspaceProperty, value); }

    /// <inheritdoc cref="LossSummaryProperty"/>
    public string LossSummary { get => GetValue(LossSummaryProperty); set => SetValue(LossSummaryProperty, value); }

    /// <inheritdoc cref="IsUpgradedProperty"/>
    public bool IsUpgraded { get => GetValue(IsUpgradedProperty); set => SetValue(IsUpgradedProperty, value); }

    /// <inheritdoc cref="IsCollapsedProperty"/>
    public bool IsCollapsed { get => GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }

    /// <inheritdoc cref="CollapseToProperty"/>
    public TrialBannerCollapse CollapseTo { get => GetValue(CollapseToProperty); set => SetValue(CollapseToProperty, value); }

    /// <inheritdoc cref="IsDismissibleProperty"/>
    public bool IsDismissible { get => GetValue(IsDismissibleProperty); set => SetValue(IsDismissibleProperty, value); }

    /// <inheritdoc cref="IsSnoozableProperty"/>
    public bool IsSnoozable { get => GetValue(IsSnoozableProperty); set => SetValue(IsSnoozableProperty, value); }

    /// <inheritdoc cref="AddPaymentHandlerProperty"/>
    public Func<CancellationToken, Task>? AddPaymentHandler
    {
        get => GetValue(AddPaymentHandlerProperty);
        set => SetValue(AddPaymentHandlerProperty, value);
    }

    /// <inheritdoc cref="TimeProviderProperty"/>
    public TimeProvider TimeProvider { get => GetValue(TimeProviderProperty); set => SetValue(TimeProviderProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public TrialBannerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="StatusProperty"/>
    public TrialStatus Status
    {
        get => _status;
        private set => SetAndRaise(StatusProperty, ref _status, value);
    }

    /// <summary>What the workspace loses without paying, listed in the card (three or four rows). Empty shows the sample.</summary>
    public AvaloniaList<TrialFeature> Features => _features;

    /// <inheritdoc cref="ComparePlansRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ComparePlansRequested
    {
        add => AddHandler(ComparePlansRequestedEvent, value);
        remove => RemoveHandler(ComparePlansRequestedEvent, value);
    }

    /// <inheritdoc cref="ManageBillingRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ManageBillingRequested
    {
        add => AddHandler(ManageBillingRequestedEvent, value);
        remove => RemoveHandler(ManageBillingRequestedEvent, value);
    }

    /// <inheritdoc cref="DismissedEvent"/>
    public event EventHandler<RoutedEventArgs>? Dismissed
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    /// <inheritdoc cref="SnoozedEvent"/>
    public event EventHandler<TrialSnoozedEventArgs>? Snoozed
    {
        add => AddHandler(SnoozedEvent, value);
        remove => RemoveHandler(SnoozedEvent, value);
    }

    /// <summary>The features on screen: the ones given.</summary>
    internal IReadOnlyList<TrialFeature> DisplayedFeatures => Source._features;

    /// <summary>Whether there is a trial to show: days left, an end date, or an upgrade.</summary>
    internal bool HasTrial => Source is var source && (source.DaysLeft is not null || source.EndsAt is not null || source.IsUpgraded);
}
