using System.Windows.Input;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A customer galaxy: every account a dot, its area by monthly recurring revenue
/// and its color by health, slowly turning as a tilted two-armed spiral. Group the accounts by any dimension and the dots
/// spring into one packed cluster per value, labelled with its MRR and size; pan, zoom about the pointer, double-click a
/// cluster to fly into it, hover a dot for its card and click it to select it. A totals panel beside the stage (under it
/// in narrow containers) shows the MRR, accounts, growing and at-risk MRR of everything or of the focused cluster, and a
/// list of the clusters by MRR or by accounts whose rows fly to their cluster.
/// </summary>
/// <remarks>
/// <para>
/// Data: <see cref="ItemsSource"/> (or the inline <see cref="Items"/>) holds <see cref="GalaxyAccount"/>s; every
/// <see cref="GalaxyGrouping"/> in <see cref="Groupings"/> adds a tab and reads <see cref="GalaxyAccount.Groups"/> under
/// its key (the key <see cref="GalaxyGrouping.HealthKey"/> groups by health). <see cref="HealthStates"/> names and
/// colors the health values (four defaults).
/// </para>
/// <para>
/// Keys on the stage: ← → fly to the previous / next cluster by MRR, ↓ ↑ select the next / previous account of the
/// focused cluster (of all accounts in the overview), + = − zoom by 1.6 about the center, 0 or Home return to every
/// cluster, Escape clears the selection and then the focus.
/// </para>
/// </remarks>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(TabsPart, typeof(GalaxyTabStrip))]
[TemplatePart(LegendPart, typeof(GalaxyLegendPanel))]
[TemplatePart(BodyPart, typeof(GalaxyBodyPanel))]
[TemplatePart(StagePart, typeof(GalaxyStage))]
[TemplatePart(LabelsPart, typeof(GalaxyLabelLayer))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(ShowAllHostPart, typeof(PresenceHost))]
[TemplatePart(ShowAllPart, typeof(GalaxyChipButton))]
[TemplatePart(ZoomPart, typeof(ShowpieceZoomControls))]
[TemplatePart(CardPart, typeof(ShowpieceHoverCard))]
[TemplatePart(LivePart, typeof(ShowpieceLiveRegion))]
[TemplatePart(TablePart, typeof(GalaxyHiddenTable))]
[TemplatePart(TotalsPart, typeof(GalaxyTotalsPanel))]
[TemplatePart(ScopePart, typeof(ArcContentSwap))]
[TemplatePart(MrrPart, typeof(ShowpieceNumber))]
[TemplatePart(AccountsPart, typeof(ShowpieceNumber))]
[TemplatePart(GrowthPart, typeof(ShowpieceNumber))]
[TemplatePart(RiskPart, typeof(ShowpieceNumber))]
[TemplatePart(GrowthDotPart, typeof(Border))]
[TemplatePart(RiskDotPart, typeof(Border))]
[TemplatePart(GrowthLabelPart, typeof(TextBlock))]
[TemplatePart(RiskLabelPart, typeof(TextBlock))]
[TemplatePart(ListTitlePart, typeof(TextBlock))]
[TemplatePart(SortMrrPart, typeof(GalaxySortButton))]
[TemplatePart(SortAccountsPart, typeof(GalaxySortButton))]
[TemplatePart(RowsPart, typeof(GalaxyRowsPanel))]
[PseudoClasses(GroupedPseudoClass, ClusterFocusedPseudoClass, RowHoverPseudoClass, SidePseudoClass, EmptyPseudoClass)]
public partial class CustomerGalaxy : TemplatedControl
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string TabsPart = "PART_Tabs";
    public const string LegendPart = "PART_Legend";
    public const string BodyPart = "PART_Body";
    public const string StagePart = "PART_Stage";
    public const string LabelsPart = "PART_Labels";
    public const string HintPart = "PART_Hint";
    public const string ShowAllHostPart = "PART_ShowAllHost";
    public const string ShowAllPart = "PART_ShowAll";
    public const string ZoomPart = "PART_Zoom";
    public const string CardPart = "PART_Card";
    public const string LivePart = "PART_Live";
    public const string TablePart = "PART_Table";
    public const string TotalsPart = "PART_Totals";
    public const string ScopePart = "PART_Scope";
    public const string MrrPart = "PART_Mrr";
    public const string AccountsPart = "PART_Accounts";
    public const string GrowthPart = "PART_Growth";
    public const string RiskPart = "PART_Risk";
    public const string GrowthDotPart = "PART_GrowthDot";
    public const string RiskDotPart = "PART_RiskDot";
    public const string GrowthLabelPart = "PART_GrowthLabel";
    public const string RiskLabelPart = "PART_RiskLabel";
    public const string ListTitlePart = "PART_ListTitle";
    public const string SortMrrPart = "PART_SortMrr";
    public const string SortAccountsPart = "PART_SortAccounts";
    public const string RowsPart = "PART_Rows";

    /// <summary>Set while a grouping is shown (clusters instead of the spiral).</summary>
    public const string GroupedPseudoClass = ":grouped";

    /// <summary>Set while a cluster is focused (the others dimmed, "Show all" shown).</summary>
    public const string ClusterFocusedPseudoClass = ":cluster-focused";

    /// <summary>Set while the pointer is on a row of the totals list (the other clusters dimmed).</summary>
    public const string RowHoverPseudoClass = ":row-hover";

    /// <summary>Set while the totals panel sits beside the stage.</summary>
    public const string SidePseudoClass = ":side";

    /// <summary>Set while there are no accounts.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The accounts to draw; when null the inline <see cref="Items"/> are used. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<GalaxyAccount>?> ItemsSourceProperty =
        AvaloniaProperty.Register<CustomerGalaxy, IEnumerable<GalaxyAccount>?>(nameof(ItemsSource));

    /// <summary>The key of the shown grouping, or null for the overview of all accounts (two-way). Changing it regroups the dots.</summary>
    public static readonly StyledProperty<string?> SelectedGroupingKeyProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string?>(nameof(SelectedGroupingKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The key of the focused cluster, or null (two-way): the camera flies into it and the other clusters dim.</summary>
    public static readonly StyledProperty<string?> FocusedGroupKeyProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string?>(nameof(FocusedGroupKey), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The selected account, or null (two-way): it keeps a ring and its card stays open. Setting it does not move the camera.</summary>
    public static readonly StyledProperty<GalaxyAccount?> SelectedAccountProperty =
        AvaloniaProperty.Register<CustomerGalaxy, GalaxyAccount?>(nameof(SelectedAccount), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How the totals list is ordered and measured (two-way). Default <see cref="GalaxyGroupSort.Mrr"/>.</summary>
    public static readonly StyledProperty<GalaxyGroupSort> GroupSortProperty =
        AvaloniaProperty.Register<CustomerGalaxy, GalaxyGroupSort>(nameof(GroupSort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The title above the grouping tabs; hidden when null.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string?>(nameof(Title));

    /// <summary>The line under the title; when null it is built from <see cref="SubtitleFormat"/>; an empty string hides it.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string?>(nameof(Subtitle));

    /// <summary>The automatic subtitle; <c>{0}</c> is the number of accounts.</summary>
    public static readonly StyledProperty<string> SubtitleFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(SubtitleFormat), "{0} accounts. Dot size shows MRR, color shows health.");

    /// <summary>Whether the overview and the clusters turn slowly (1.6°/s). Off at reduced motion.</summary>
    public static readonly StyledProperty<bool> IsAutoRotateProperty =
        AvaloniaProperty.Register<CustomerGalaxy, bool>(nameof(IsAutoRotate), true);

    /// <summary>The turning speed in degrees per second (default 1.6, one turn in about 224 s).</summary>
    public static readonly StyledProperty<double> RotationSpeedProperty =
        AvaloniaProperty.Register<CustomerGalaxy, double>(nameof(RotationSpeed), 1.6);

    /// <summary>The zoom factor of the zoom buttons and the + / − keys (default 1.6).</summary>
    public static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<CustomerGalaxy, double>(nameof(ZoomStep), 1.6);

    /// <summary>Wheel zoom per web pixel of scroll: one notch zooms by <c>exp(100 × value)</c> (default 0.0022, ×1.246).</summary>
    public static readonly StyledProperty<double> WheelZoomSensitivityProperty =
        AvaloniaProperty.Register<CustomerGalaxy, double>(nameof(WheelZoomSensitivity), 0.0022);

    /// <summary>The largest zoom relative to the fitted view (default 9); the smallest is always the fitted view.</summary>
    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<CustomerGalaxy, double>(nameof(MaxZoom), 9);

    /// <summary>Where the totals panel goes (default <see cref="GalaxyTotalsPlacement.Auto"/>: beside the stage from 880 px).</summary>
    public static readonly StyledProperty<GalaxyTotalsPlacement> TotalsPlacementProperty =
        AvaloniaProperty.Register<CustomerGalaxy, GalaxyTotalsPlacement>(nameof(TotalsPlacement));

    /// <summary>Shows the legend of health colors and dot size. Default true.</summary>
    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<CustomerGalaxy, bool>(nameof(ShowLegend), true);

    /// <summary>Shows the totals panel. Default true.</summary>
    public static readonly StyledProperty<bool> ShowTotalsProperty =
        AvaloniaProperty.Register<CustomerGalaxy, bool>(nameof(ShowTotals), true);

    /// <summary>Shows the gesture hint in the stage's corner. Default true.</summary>
    public static readonly StyledProperty<bool> ShowHintProperty =
        AvaloniaProperty.Register<CustomerGalaxy, bool>(nameof(ShowHint), true);

    /// <summary>Shows the zoom buttons. Default true.</summary>
    public static readonly StyledProperty<bool> ShowZoomControlsProperty =
        AvaloniaProperty.Register<CustomerGalaxy, bool>(nameof(ShowZoomControls), true);

    /// <summary>Currency symbol of the amounts (default "$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(CurrencySymbol), "$");

    /// <summary>Culture of the numbers (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(CultureName), "en-US");

    /// <summary>Formats every amount in place of the compact currency ($6.05M, $870K, $990).</summary>
    public static readonly StyledProperty<Func<double, string>?> MrrFormatterProperty =
        AvaloniaProperty.Register<CustomerGalaxy, Func<double, string>?>(nameof(MrrFormatter));

    /// <summary>Replaces the built-in account card; its data is the <see cref="GalaxyAccount"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> AccountTooltipTemplateProperty =
        AvaloniaProperty.Register<CustomerGalaxy, IDataTemplate?>(nameof(AccountTooltipTemplate));

    /// <summary>The overview tab (default "All").</summary>
    public static readonly StyledProperty<string> AllTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AllText), "All");

    /// <summary>The totals' scope name when no cluster is focused (default "All accounts").</summary>
    public static readonly StyledProperty<string> AllAccountsTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AllAccountsText), "All accounts");

    /// <summary>The caption over the large MRR number (default "Monthly recurring revenue").</summary>
    public static readonly StyledProperty<string> MrrCaptionTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(MrrCaptionText), "Monthly recurring revenue");

    /// <summary>"MRR": the sort button and the card's first row.</summary>
    public static readonly StyledProperty<string> MrrTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(MrrText), "MRR");

    /// <summary>"Accounts": the subtotal and the sort button.</summary>
    public static readonly StyledProperty<string> AccountsTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AccountsText), "Accounts");

    /// <summary>The card's owner row (default "Owner").</summary>
    public static readonly StyledProperty<string> OwnerTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(OwnerText), "Owner");

    /// <summary>The card's health row (default "Health").</summary>
    public static readonly StyledProperty<string> HealthTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(HealthText), "Health");

    /// <summary>The list title of a grouping without its own <see cref="GalaxyGrouping.ListTitle"/>; <c>{0}</c> is the header in lower case ("By {0}").</summary>
    public static readonly StyledProperty<string> ListTitleFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(ListTitleFormat), "By {0}");

    /// <summary>The name of health as a grouping when no health grouping is listed: the overview's list title (default "Health").</summary>
    public static readonly StyledProperty<string> HealthGroupingTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(HealthGroupingText), "Health");

    /// <summary>A number of accounts ("{0} accounts").</summary>
    public static readonly StyledProperty<string> AccountCountFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AccountCountFormat), "{0} accounts");

    /// <summary>One account ("{0} account").</summary>
    public static readonly StyledProperty<string> OneAccountFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(OneAccountFormat), "{0} account");

    /// <summary>The risk share after a row's count; <c>{0}</c> is the percentage ("{0} at risk").</summary>
    public static readonly StyledProperty<string> AtRiskShareFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AtRiskShareFormat), "{0} at risk");

    /// <summary>The gesture hint in the stage's corner.</summary>
    public static readonly StyledProperty<string> HintTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(HintText), "Drag to move · scroll to zoom · double-click a cluster to open it");

    /// <summary>The pill that returns from a focused cluster (default "Show all").</summary>
    public static readonly StyledProperty<string> ShowAllTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(ShowAllText), "Show all");

    /// <summary>Screen-reader name of the + button (default "Zoom in").</summary>
    public static readonly StyledProperty<string> ZoomInTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(ZoomInText), "Zoom in");

    /// <summary>Screen-reader name of the − button (default "Zoom out").</summary>
    public static readonly StyledProperty<string> ZoomOutTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(ZoomOutText), "Zoom out");

    /// <summary>Screen-reader name of the fit button (default "Show every account").</summary>
    public static readonly StyledProperty<string> FitTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(FitText), "Show every account");

    /// <summary>The legend's size entry (default "Size shows MRR").</summary>
    public static readonly StyledProperty<string> SizeLegendTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(SizeLegendText), "Size shows MRR");

    /// <summary>Screen-reader name of the grouping tabs (default "Group accounts by").</summary>
    public static readonly StyledProperty<string> GroupByTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(GroupByText), "Group accounts by");

    /// <summary>Screen-reader name of the MRR / Accounts switch (default "Order the list by").</summary>
    public static readonly StyledProperty<string> SortByTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(SortByText), "Order the list by");

    /// <summary>The cluster of accounts without a value for the grouping (default "Other").</summary>
    public static readonly StyledProperty<string> OtherTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(OtherText), "Other");

    /// <summary>Shown in the stage when there are no accounts (default "No accounts to show").</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(EmptyText), "No accounts to show");

    /// <summary>The stage's keyboard help for screen readers.</summary>
    public static readonly StyledProperty<string> KeyboardHelpTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(KeyboardHelpText),
            "Left and right arrows fly between clusters, up and down arrows step through the accounts of a cluster, plus and minus zoom, Escape goes back.");

    /// <summary>
    /// The stage's screen-reader summary: <c>{0}</c> accounts, <c>{1}</c> total MRR, <c>{2}</c> the layout
    /// (<see cref="OverviewLayoutText"/> or <see cref="GroupedLayoutFormat"/>).
    /// </summary>
    public static readonly StyledProperty<string> StageSummaryFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(StageSummaryFormat),
            "Customer galaxy of {0} accounts and {1} in monthly recurring revenue, {2}. Dot size shows MRR, color shows health.");

    /// <summary>The overview's layout in the summary (default "in one spiral").</summary>
    public static readonly StyledProperty<string> OverviewLayoutTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(OverviewLayoutText), "in one spiral");

    /// <summary>A grouping's layout in the summary; <c>{0}</c> is the grouping in lower case ("grouped by {0}").</summary>
    public static readonly StyledProperty<string> GroupedLayoutFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(GroupedLayoutFormat), "grouped by {0}");

    /// <summary>
    /// Announced when a cluster is focused: <c>{0}</c> name, <c>{1}</c> accounts, <c>{2}</c> MRR, <c>{3}</c> MRR at risk.
    /// </summary>
    public static readonly StyledProperty<string> GroupAnnouncementFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(GroupAnnouncementFormat), "{0}, {1}, {2} MRR, {3} at risk.");

    /// <summary>
    /// Announced when an account is selected with the keyboard: <c>{0}</c> name, <c>{1}</c> MRR, <c>{2}</c> health,
    /// <c>{3}</c> owner, <c>{4}</c> note.
    /// </summary>
    public static readonly StyledProperty<string> AccountAnnouncementFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(AccountAnnouncementFormat), "{0}, {1} MRR, {2}, owned by {3}. {4}");

    /// <summary>
    /// Announced when a grouping is shown or the view resets: <c>{0}</c> clusters, <c>{1}</c> grouping in lower case,
    /// <c>{2}</c> largest cluster, <c>{3}</c> its MRR.
    /// </summary>
    public static readonly StyledProperty<string> GroupingAnnouncementFormatProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(GroupingAnnouncementFormat), "{0} clusters by {1}. {2} is the largest with {3} MRR.");

    /// <summary>Announced when the overview is shown (default "All accounts in one spiral.").</summary>
    public static readonly StyledProperty<string> OverviewAnnouncementTextProperty =
        AvaloniaProperty.Register<CustomerGalaxy, string>(nameof(OverviewAnnouncementText), "All accounts in one spiral.");

    /// <summary>The account under the pointer, or null.</summary>
    public static readonly DirectProperty<CustomerGalaxy, GalaxyAccount?> HoveredAccountProperty =
        AvaloniaProperty.RegisterDirect<CustomerGalaxy, GalaxyAccount?>(nameof(HoveredAccount), o => o.HoveredAccount);

    /// <summary>The totals of all accounts, of every health state and of every cluster of the shown grouping.</summary>
    public static readonly DirectProperty<CustomerGalaxy, GalaxySummary> SummaryProperty =
        AvaloniaProperty.RegisterDirect<CustomerGalaxy, GalaxySummary>(nameof(Summary), o => o.Summary);

    /// <summary>The camera's zoom relative to the fitted view (1 = everything visible).</summary>
    public static readonly DirectProperty<CustomerGalaxy, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<CustomerGalaxy, double>(nameof(Zoom), o => o.Zoom);

    /// <summary>An account was selected or the selection cleared.</summary>
    public static readonly RoutedEvent<GalaxyAccountEventArgs> AccountSelectedEvent =
        RoutedEvent.Register<CustomerGalaxy, GalaxyAccountEventArgs>(nameof(AccountSelected), RoutingStrategies.Bubble);

    /// <summary>A cluster was focused, or the focus returned to every cluster.</summary>
    public static readonly RoutedEvent<GalaxyGroupEventArgs> GroupFocusedEvent =
        RoutedEvent.Register<CustomerGalaxy, GalaxyGroupEventArgs>(nameof(GroupFocused), RoutingStrategies.Bubble);

    /// <summary>The shown grouping changed.</summary>
    public static readonly RoutedEvent<GalaxyGroupingChangedEventArgs> GroupingChangedEvent =
        RoutedEvent.Register<CustomerGalaxy, GalaxyGroupingChangedEventArgs>(nameof(GroupingChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<GalaxyAccount>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="SelectedGroupingKeyProperty"/>
    public string? SelectedGroupingKey { get => GetValue(SelectedGroupingKeyProperty); set => SetValue(SelectedGroupingKeyProperty, value); }

    /// <inheritdoc cref="FocusedGroupKeyProperty"/>
    public string? FocusedGroupKey { get => GetValue(FocusedGroupKeyProperty); set => SetValue(FocusedGroupKeyProperty, value); }

    /// <inheritdoc cref="SelectedAccountProperty"/>
    public GalaxyAccount? SelectedAccount { get => GetValue(SelectedAccountProperty); set => SetValue(SelectedAccountProperty, value); }

    /// <inheritdoc cref="GroupSortProperty"/>
    public GalaxyGroupSort GroupSort { get => GetValue(GroupSortProperty); set => SetValue(GroupSortProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="SubtitleFormatProperty"/>
    public string SubtitleFormat { get => GetValue(SubtitleFormatProperty); set => SetValue(SubtitleFormatProperty, value); }

    /// <inheritdoc cref="IsAutoRotateProperty"/>
    public bool IsAutoRotate { get => GetValue(IsAutoRotateProperty); set => SetValue(IsAutoRotateProperty, value); }

    /// <inheritdoc cref="RotationSpeedProperty"/>
    public double RotationSpeed { get => GetValue(RotationSpeedProperty); set => SetValue(RotationSpeedProperty, value); }

    /// <inheritdoc cref="ZoomStepProperty"/>
    public double ZoomStep { get => GetValue(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }

    /// <inheritdoc cref="WheelZoomSensitivityProperty"/>
    public double WheelZoomSensitivity { get => GetValue(WheelZoomSensitivityProperty); set => SetValue(WheelZoomSensitivityProperty, value); }

    /// <inheritdoc cref="MaxZoomProperty"/>
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    /// <inheritdoc cref="TotalsPlacementProperty"/>
    public GalaxyTotalsPlacement TotalsPlacement { get => GetValue(TotalsPlacementProperty); set => SetValue(TotalsPlacementProperty, value); }

    /// <inheritdoc cref="ShowLegendProperty"/>
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    /// <inheritdoc cref="ShowTotalsProperty"/>
    public bool ShowTotals { get => GetValue(ShowTotalsProperty); set => SetValue(ShowTotalsProperty, value); }

    /// <inheritdoc cref="ShowHintProperty"/>
    public bool ShowHint { get => GetValue(ShowHintProperty); set => SetValue(ShowHintProperty, value); }

    /// <inheritdoc cref="ShowZoomControlsProperty"/>
    public bool ShowZoomControls { get => GetValue(ShowZoomControlsProperty); set => SetValue(ShowZoomControlsProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="MrrFormatterProperty"/>
    public Func<double, string>? MrrFormatter { get => GetValue(MrrFormatterProperty); set => SetValue(MrrFormatterProperty, value); }

    /// <inheritdoc cref="AccountTooltipTemplateProperty"/>
    public IDataTemplate? AccountTooltipTemplate { get => GetValue(AccountTooltipTemplateProperty); set => SetValue(AccountTooltipTemplateProperty, value); }

    /// <inheritdoc cref="AllTextProperty"/>
    public string AllText { get => GetValue(AllTextProperty); set => SetValue(AllTextProperty, value); }

    /// <inheritdoc cref="AllAccountsTextProperty"/>
    public string AllAccountsText { get => GetValue(AllAccountsTextProperty); set => SetValue(AllAccountsTextProperty, value); }

    /// <inheritdoc cref="MrrCaptionTextProperty"/>
    public string MrrCaptionText { get => GetValue(MrrCaptionTextProperty); set => SetValue(MrrCaptionTextProperty, value); }

    /// <inheritdoc cref="MrrTextProperty"/>
    public string MrrText { get => GetValue(MrrTextProperty); set => SetValue(MrrTextProperty, value); }

    /// <inheritdoc cref="AccountsTextProperty"/>
    public string AccountsText { get => GetValue(AccountsTextProperty); set => SetValue(AccountsTextProperty, value); }

    /// <inheritdoc cref="OwnerTextProperty"/>
    public string OwnerText { get => GetValue(OwnerTextProperty); set => SetValue(OwnerTextProperty, value); }

    /// <inheritdoc cref="HealthTextProperty"/>
    public string HealthText { get => GetValue(HealthTextProperty); set => SetValue(HealthTextProperty, value); }

    /// <inheritdoc cref="ListTitleFormatProperty"/>
    public string ListTitleFormat { get => GetValue(ListTitleFormatProperty); set => SetValue(ListTitleFormatProperty, value); }

    /// <inheritdoc cref="HealthGroupingTextProperty"/>
    public string HealthGroupingText { get => GetValue(HealthGroupingTextProperty); set => SetValue(HealthGroupingTextProperty, value); }

    /// <inheritdoc cref="AccountCountFormatProperty"/>
    public string AccountCountFormat { get => GetValue(AccountCountFormatProperty); set => SetValue(AccountCountFormatProperty, value); }

    /// <inheritdoc cref="OneAccountFormatProperty"/>
    public string OneAccountFormat { get => GetValue(OneAccountFormatProperty); set => SetValue(OneAccountFormatProperty, value); }

    /// <inheritdoc cref="AtRiskShareFormatProperty"/>
    public string AtRiskShareFormat { get => GetValue(AtRiskShareFormatProperty); set => SetValue(AtRiskShareFormatProperty, value); }

    /// <inheritdoc cref="HintTextProperty"/>
    public string HintText { get => GetValue(HintTextProperty); set => SetValue(HintTextProperty, value); }

    /// <inheritdoc cref="ShowAllTextProperty"/>
    public string ShowAllText { get => GetValue(ShowAllTextProperty); set => SetValue(ShowAllTextProperty, value); }

    /// <inheritdoc cref="ZoomInTextProperty"/>
    public string ZoomInText { get => GetValue(ZoomInTextProperty); set => SetValue(ZoomInTextProperty, value); }

    /// <inheritdoc cref="ZoomOutTextProperty"/>
    public string ZoomOutText { get => GetValue(ZoomOutTextProperty); set => SetValue(ZoomOutTextProperty, value); }

    /// <inheritdoc cref="FitTextProperty"/>
    public string FitText { get => GetValue(FitTextProperty); set => SetValue(FitTextProperty, value); }

    /// <inheritdoc cref="SizeLegendTextProperty"/>
    public string SizeLegendText { get => GetValue(SizeLegendTextProperty); set => SetValue(SizeLegendTextProperty, value); }

    /// <inheritdoc cref="GroupByTextProperty"/>
    public string GroupByText { get => GetValue(GroupByTextProperty); set => SetValue(GroupByTextProperty, value); }

    /// <inheritdoc cref="SortByTextProperty"/>
    public string SortByText { get => GetValue(SortByTextProperty); set => SetValue(SortByTextProperty, value); }

    /// <inheritdoc cref="OtherTextProperty"/>
    public string OtherText { get => GetValue(OtherTextProperty); set => SetValue(OtherTextProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    /// <inheritdoc cref="KeyboardHelpTextProperty"/>
    public string KeyboardHelpText { get => GetValue(KeyboardHelpTextProperty); set => SetValue(KeyboardHelpTextProperty, value); }

    /// <inheritdoc cref="StageSummaryFormatProperty"/>
    public string StageSummaryFormat { get => GetValue(StageSummaryFormatProperty); set => SetValue(StageSummaryFormatProperty, value); }

    /// <inheritdoc cref="OverviewLayoutTextProperty"/>
    public string OverviewLayoutText { get => GetValue(OverviewLayoutTextProperty); set => SetValue(OverviewLayoutTextProperty, value); }

    /// <inheritdoc cref="GroupedLayoutFormatProperty"/>
    public string GroupedLayoutFormat { get => GetValue(GroupedLayoutFormatProperty); set => SetValue(GroupedLayoutFormatProperty, value); }

    /// <inheritdoc cref="GroupAnnouncementFormatProperty"/>
    public string GroupAnnouncementFormat { get => GetValue(GroupAnnouncementFormatProperty); set => SetValue(GroupAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="AccountAnnouncementFormatProperty"/>
    public string AccountAnnouncementFormat { get => GetValue(AccountAnnouncementFormatProperty); set => SetValue(AccountAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="GroupingAnnouncementFormatProperty"/>
    public string GroupingAnnouncementFormat { get => GetValue(GroupingAnnouncementFormatProperty); set => SetValue(GroupingAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="OverviewAnnouncementTextProperty"/>
    public string OverviewAnnouncementText { get => GetValue(OverviewAnnouncementTextProperty); set => SetValue(OverviewAnnouncementTextProperty, value); }

    /// <inheritdoc cref="HoveredAccountProperty"/>
    public GalaxyAccount? HoveredAccount
    {
        get => _hoveredAccount;
        private set => SetAndRaise(HoveredAccountProperty, ref _hoveredAccount, value);
    }

    /// <inheritdoc cref="SummaryProperty"/>
    public GalaxySummary Summary
    {
        get => _summary;
        private set => SetAndRaise(SummaryProperty, ref _summary, value);
    }

    /// <inheritdoc cref="ZoomProperty"/>
    public double Zoom
    {
        get => _zoom;
        private set => SetAndRaise(ZoomProperty, ref _zoom, value);
    }

    /// <summary>The inline accounts, used when <see cref="ItemsSource"/> is null.</summary>
    public AvaloniaList<GalaxyAccount> Items { get; } = new();

    /// <summary>The groupings, one tab each after "All", in tab order.</summary>
    [Content]
    public AvaloniaList<GalaxyGrouping> Groupings { get; } = new();

    /// <summary>The health states: legend order, colors, and which ones count as growth and risk (four defaults).</summary>
    public AvaloniaList<GalaxyHealthState> HealthStates { get; } = new(GalaxyHealthState.CreateDefaults());

    /// <summary>An account was selected (or the selection cleared) by a click, a key or <see cref="SelectedAccount"/>.</summary>
    public event EventHandler<GalaxyAccountEventArgs>? AccountSelected
    {
        add => AddHandler(AccountSelectedEvent, value);
        remove => RemoveHandler(AccountSelectedEvent, value);
    }

    /// <summary>A cluster was focused (double click, arrow key, list row, <see cref="FocusedGroupKey"/>) or the view returned to every cluster.</summary>
    public event EventHandler<GalaxyGroupEventArgs>? GroupFocused
    {
        add => AddHandler(GroupFocusedEvent, value);
        remove => RemoveHandler(GroupFocusedEvent, value);
    }

    /// <summary>The shown grouping changed (tab, list row in the overview, <see cref="SelectedGroupingKey"/>).</summary>
    public event EventHandler<GalaxyGroupingChangedEventArgs>? GroupingChanged
    {
        add => AddHandler(GroupingChangedEvent, value);
        remove => RemoveHandler(GroupingChangedEvent, value);
    }

    /// <summary>Flies to the cluster whose key is the parameter (a string).</summary>
    public ICommand FlyToGroupCommand { get; }

    /// <summary>Returns to every cluster: clears the focus and fits the view.</summary>
    public ICommand ResetViewCommand { get; }

    #endregion

    private GalaxyAccount? _hoveredAccount;
    private GalaxySummary _summary = GalaxySummary.Empty;
    private double _zoom = 1;
}
