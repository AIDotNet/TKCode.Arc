using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>What a step of the <see cref="MrrWaterfall"/> bridge does to the recurring revenue.</summary>
public enum MrrMovementKind
{
    /// <summary>Revenue from accounts that started paying in the period (adds).</summary>
    New,

    /// <summary>Extra revenue from accounts that were already paying (adds; counts toward net revenue retention).</summary>
    Expansion,

    /// <summary>Revenue from accounts that came back after leaving (adds).</summary>
    Reactivation,

    /// <summary>Revenue given up by accounts that stayed (subtracts; counts against net revenue retention).</summary>
    Contraction,

    /// <summary>Revenue lost with accounts that left (subtracts; counts against net revenue retention and logo churn).</summary>
    Churn
}

/// <summary>How the <see cref="MrrWaterfall"/> shows amounts.</summary>
public enum MrrValueMode
{
    /// <summary>Currency amounts ("$412.4k", "+$55,800").</summary>
    Currency,

    /// <summary>Percent of the period's starting revenue, which is 100%.</summary>
    PercentOfStart
}

/// <summary>
/// One account (or one plan tier) behind a step of the <see cref="MrrWaterfall"/>: a row of the side panel with its name, its
/// amount, a line of detail and a bar for its share of the step.
/// </summary>
public class MrrAccount : AvaloniaObject
{
    /// <summary>Identifies the account across periods so the panel can slide a kept row to its new place; default is the name.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<MrrAccount, string?>(nameof(Key));

    /// <summary>The account's (or tier's) name.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<MrrAccount, string>(nameof(Name), string.Empty);

    /// <summary>The line under the name ("May · Team, 24 seats").</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<MrrAccount, string?>(nameof(Detail));

    /// <summary>The account's contribution to the step as a positive amount (the sign comes from the step's kind).</summary>
    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<MrrAccount, double>(nameof(Amount));

    /// <summary>How many accounts the row stands for (tiers); 1 for one account.</summary>
    public static readonly StyledProperty<int> AccountCountProperty =
        AvaloniaProperty.Register<MrrAccount, int>(nameof(AccountCount), 1);

    /// <summary>Whether the row is a remainder row (muted name); the block creates the remainder row itself.</summary>
    public static readonly StyledProperty<bool> IsRestProperty =
        AvaloniaProperty.Register<MrrAccount, bool>(nameof(IsRest));

    /// <summary>Creates an empty account.</summary>
    public MrrAccount()
    {
        PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates an account.</summary>
    public MrrAccount(string name, double amount, string? detail = null, int accountCount = 1) : this()
    {
        Name = name;
        Amount = amount;
        Detail = detail;
        AccountCount = accountCount;
    }

    /// <summary>Raised when a property changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="AmountProperty"/>
    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    /// <inheritdoc cref="AccountCountProperty"/>
    public int AccountCount { get => GetValue(AccountCountProperty); set => SetValue(AccountCountProperty, value); }

    /// <inheritdoc cref="IsRestProperty"/>
    public bool IsRest { get => GetValue(IsRestProperty); set => SetValue(IsRestProperty, value); }

    /// <summary>The key panels use: <see cref="Key"/> or the name.</summary>
    internal string EffectiveKey => Key ?? Name;
}

/// <summary>One floating bar of the bridge: new business, expansion, reactivation, contraction or churn.</summary>
public class MrrMovement : AvaloniaObject
{
    /// <summary>What the step does; it decides the sign, the color and which headline ratios it counts toward.</summary>
    public static readonly StyledProperty<MrrMovementKind> KindProperty =
        AvaloniaProperty.Register<MrrMovement, MrrMovementKind>(nameof(Kind));

    /// <summary>The step's name in the panel and in announcements ("New business").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MrrMovement, string>(nameof(Label), string.Empty);

    /// <summary>The name under the bar ("New"); null uses <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string?> ShortLabelProperty =
        AvaloniaProperty.Register<MrrMovement, string?>(nameof(ShortLabel));

    /// <summary>The name under the bar when the bars are narrow ("Exp."); null uses <see cref="ShortLabel"/>.</summary>
    public static readonly StyledProperty<string?> CompactLabelProperty =
        AvaloniaProperty.Register<MrrMovement, string?>(nameof(CompactLabel));

    /// <summary>The size of the step as a positive amount (the sign comes from <see cref="Kind"/>).</summary>
    public static readonly StyledProperty<double> AmountProperty =
        AvaloniaProperty.Register<MrrMovement, double>(nameof(Amount));

    /// <summary>How many accounts the step covers.</summary>
    public static readonly StyledProperty<int> AccountCountProperty =
        AvaloniaProperty.Register<MrrMovement, int>(nameof(AccountCount));

    /// <summary>The words after the count in the panel ("new customers"); null says "accounts".</summary>
    public static readonly StyledProperty<string?> CountNounProperty =
        AvaloniaProperty.Register<MrrMovement, string?>(nameof(CountNoun));

    /// <summary>The name of the remainder row; null uses the block's label.</summary>
    public static readonly StyledProperty<string?> RestLabelProperty =
        AvaloniaProperty.Register<MrrMovement, string?>(nameof(RestLabel));

    /// <summary>Creates an empty movement.</summary>
    public MrrMovement()
    {
        PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Accounts.CollectionChanged += OnAccountsChanged;
    }

    /// <summary>Raised when a property or an account changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KindProperty"/>
    public MrrMovementKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ShortLabelProperty"/>
    public string? ShortLabel { get => GetValue(ShortLabelProperty); set => SetValue(ShortLabelProperty, value); }

    /// <inheritdoc cref="CompactLabelProperty"/>
    public string? CompactLabel { get => GetValue(CompactLabelProperty); set => SetValue(CompactLabelProperty, value); }

    /// <inheritdoc cref="AmountProperty"/>
    public double Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    /// <inheritdoc cref="AccountCountProperty"/>
    public int AccountCount { get => GetValue(AccountCountProperty); set => SetValue(AccountCountProperty, value); }

    /// <inheritdoc cref="CountNounProperty"/>
    public string? CountNoun { get => GetValue(CountNounProperty); set => SetValue(CountNounProperty, value); }

    /// <inheritdoc cref="RestLabelProperty"/>
    public string? RestLabel { get => GetValue(RestLabelProperty); set => SetValue(RestLabelProperty, value); }

    /// <summary>The named accounts behind the step, in any order; the block lists the largest ones and groups the rest.</summary>
    [Content]
    public AvaloniaList<MrrAccount> Accounts { get; } = new();

    /// <summary>Whether the step adds revenue.</summary>
    public bool IsGrowth => Kind is MrrMovementKind.New or MrrMovementKind.Expansion or MrrMovementKind.Reactivation;

    /// <summary>The step's amount with its sign.</summary>
    public double SignedAmount => IsGrowth ? Math.Abs(Amount) : -Math.Abs(Amount);

    private void OnAccountsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (MrrAccount account in e.OldItems)
            {
                account.Changed -= OnAccountChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (MrrAccount account in e.NewItems)
            {
                account.Changed += OnAccountChanged;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnAccountChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// One period of the bridge ("Q2"): the revenue it started with, the five movements in bar order, how many accounts paid at
/// the start and the end, and the split of the starting and ending revenue by plan tier.
/// </summary>
public class MrrPeriod : AvaloniaObject
{
    /// <summary>A stable id for the period (switch value, events); default is the label.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(Key));

    /// <summary>The text of the period switch's button ("Q2").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<MrrPeriod, string>(nameof(Label), string.Empty);

    /// <summary>The period's name in sentences and in the table's caption ("Q2 2026").</summary>
    public static readonly StyledProperty<string?> LongLabelProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(LongLabel));

    /// <summary>The date range in the subtitle ("Apr 1 – Jun 30, 2026").</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(Caption));

    /// <summary>The date the period started, for "since {0}" ("Apr 1").</summary>
    public static readonly StyledProperty<string?> StartDateTextProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(StartDateText));

    /// <summary>The date the period ended, for "Ending MRR on {0}" ("Jun 30").</summary>
    public static readonly StyledProperty<string?> EndDateTextProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(EndDateText));

    /// <summary>The recurring revenue at the start.</summary>
    public static readonly StyledProperty<double> StartingMrrProperty =
        AvaloniaProperty.Register<MrrPeriod, double>(nameof(StartingMrr));

    /// <summary>How many accounts paid at the start.</summary>
    public static readonly StyledProperty<int> StartingAccountsProperty =
        AvaloniaProperty.Register<MrrPeriod, int>(nameof(StartingAccounts));

    /// <summary>How many accounts paid at the end.</summary>
    public static readonly StyledProperty<int> EndingAccountsProperty =
        AvaloniaProperty.Register<MrrPeriod, int>(nameof(EndingAccounts));

    /// <summary>The words after the count when the start bar is selected ("paying customers on Apr 1"); null uses the block's text.</summary>
    public static readonly StyledProperty<string?> StartingCountNounProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(StartingCountNoun));

    /// <summary>The words after the count when the end bar is selected ("paying customers on Jun 30"); null uses the block's text.</summary>
    public static readonly StyledProperty<string?> EndingCountNounProperty =
        AvaloniaProperty.Register<MrrPeriod, string?>(nameof(EndingCountNoun));

    /// <summary>Creates an empty period.</summary>
    public MrrPeriod()
    {
        PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        Movements.CollectionChanged += OnChildrenChanged;
        StartingBreakdown.CollectionChanged += OnChildrenChanged;
        EndingBreakdown.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>Raised when anything about the period changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="LongLabelProperty"/>
    public string? LongLabel { get => GetValue(LongLabelProperty); set => SetValue(LongLabelProperty, value); }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }

    /// <inheritdoc cref="StartDateTextProperty"/>
    public string? StartDateText { get => GetValue(StartDateTextProperty); set => SetValue(StartDateTextProperty, value); }

    /// <inheritdoc cref="EndDateTextProperty"/>
    public string? EndDateText { get => GetValue(EndDateTextProperty); set => SetValue(EndDateTextProperty, value); }

    /// <inheritdoc cref="StartingMrrProperty"/>
    public double StartingMrr { get => GetValue(StartingMrrProperty); set => SetValue(StartingMrrProperty, value); }

    /// <inheritdoc cref="StartingAccountsProperty"/>
    public int StartingAccounts { get => GetValue(StartingAccountsProperty); set => SetValue(StartingAccountsProperty, value); }

    /// <inheritdoc cref="EndingAccountsProperty"/>
    public int EndingAccounts { get => GetValue(EndingAccountsProperty); set => SetValue(EndingAccountsProperty, value); }

    /// <inheritdoc cref="StartingCountNounProperty"/>
    public string? StartingCountNoun { get => GetValue(StartingCountNounProperty); set => SetValue(StartingCountNounProperty, value); }

    /// <inheritdoc cref="EndingCountNounProperty"/>
    public string? EndingCountNoun { get => GetValue(EndingCountNounProperty); set => SetValue(EndingCountNounProperty, value); }

    /// <summary>The movements in bar order (normally new, expansion, reactivation, contraction, churn).</summary>
    [Content]
    public AvaloniaList<MrrMovement> Movements { get; } = new();

    /// <summary>The starting revenue by plan tier, listed when the start bar is selected (no remainder row).</summary>
    public AvaloniaList<MrrAccount> StartingBreakdown { get; } = new();

    /// <summary>The ending revenue by plan tier, listed when the end bar is selected (no remainder row).</summary>
    public AvaloniaList<MrrAccount> EndingBreakdown { get; } = new();

    /// <summary>The key switches use: <see cref="Key"/> or the label.</summary>
    internal string EffectiveKey => Key ?? Label;

    /// <summary>The revenue at the end: the start plus every movement.</summary>
    public double EndingMrr
    {
        get
        {
            var total = StartingMrr;
            foreach (var movement in Movements)
            {
                total += movement.SignedAmount;
            }
            return total;
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is MrrMovement movement)
                {
                    movement.Changed -= OnChildChanged;
                }
                else if (item is MrrAccount account)
                {
                    account.Changed -= OnChildChanged;
                }
            }
        }
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is MrrMovement movement)
                {
                    movement.Changed += OnChildChanged;
                }
                else if (item is MrrAccount account)
                {
                    account.Changed += OnChildChanged;
                }
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChildChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>Event data for <see cref="MrrWaterfall.SelectionChanged"/>.</summary>
public class MrrSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MrrSelectionChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex, MrrMovement? movement, string name)
        : base(routedEvent)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
        Movement = movement;
        Name = name;
    }

    /// <summary>The bar selected before (0 is the start bar).</summary>
    public int OldIndex { get; }

    /// <summary>The bar selected now.</summary>
    public int NewIndex { get; }

    /// <summary>The selected step; null for the start and end bars.</summary>
    public MrrMovement? Movement { get; }

    /// <summary>The selected bar's name ("Start", "New", "End").</summary>
    public string Name { get; }
}

/// <summary>Event data for <see cref="MrrWaterfall.PeriodChanged"/>.</summary>
public class MrrPeriodChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MrrPeriodChangedEventArgs(RoutedEvent routedEvent, MrrPeriod? oldPeriod, MrrPeriod? newPeriod) : base(routedEvent)
    {
        OldPeriod = oldPeriod;
        NewPeriod = newPeriod;
    }

    /// <summary>The period shown before.</summary>
    public MrrPeriod? OldPeriod { get; }

    /// <summary>The period shown now.</summary>
    public MrrPeriod? NewPeriod { get; }
}

/// <summary>Event data for <see cref="MrrWaterfall.ValueModeChanged"/>.</summary>
public class MrrValueModeChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public MrrValueModeChangedEventArgs(RoutedEvent routedEvent, MrrValueMode oldMode, MrrValueMode newMode) : base(routedEvent)
    {
        OldMode = oldMode;
        NewMode = newMode;
    }

    /// <summary>The mode before.</summary>
    public MrrValueMode OldMode { get; }

    /// <summary>The mode now.</summary>
    public MrrValueMode NewMode { get; }
}

/// <summary>
/// The words of a <see cref="MrrWaterfall"/>: switch names, headline labels, panel texts and the sentences screen readers
/// hear. Every default is plain English; replace the ones you need.
/// </summary>
public class MrrWaterfallLabels : AvaloniaObject
{
    /// <summary>The period switch's screen reader name.</summary>
    public static readonly StyledProperty<string> PeriodSwitchProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(PeriodSwitch), "Period");

    /// <summary>The value switch's screen reader name.</summary>
    public static readonly StyledProperty<string> ValueSwitchProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(ValueSwitch), "Show values as");

    /// <summary>The value switch's currency button; {0} is the currency symbol.</summary>
    public static readonly StyledProperty<string> CurrencyOptionProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(CurrencyOption), "USD");

    /// <summary>The value switch's percent button.</summary>
    public static readonly StyledProperty<string> PercentOptionProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(PercentOption), "% of start");

    /// <summary>The headline's label; {0} is the end date.</summary>
    public static readonly StyledProperty<string> EndingFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(EndingFormat), "Ending MRR on {0}");

    /// <summary>The text after the headline change; {0} is the start date.</summary>
    public static readonly StyledProperty<string> SinceFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(SinceFormat), "since {0}");

    /// <summary>The first statistic's label.</summary>
    public static readonly StyledProperty<string> RetentionLabelProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(RetentionLabel), "Net revenue retention");

    /// <summary>The second statistic's label.</summary>
    public static readonly StyledProperty<string> QuickRatioLabelProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(QuickRatioLabel), "Quick ratio");

    /// <summary>The third statistic's label.</summary>
    public static readonly StyledProperty<string> LogoChurnLabelProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(LogoChurnLabel), "Logo churn");

    /// <summary>The first bar's name under the bar.</summary>
    public static readonly StyledProperty<string> StartBarProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(StartBar), "Start");

    /// <summary>The last bar's name under the bar.</summary>
    public static readonly StyledProperty<string> EndBarProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(EndBar), "End");

    /// <summary>The panel heading of the start bar.</summary>
    public static readonly StyledProperty<string> StartTitleProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(StartTitle), "Starting MRR");

    /// <summary>The panel heading of the end bar.</summary>
    public static readonly StyledProperty<string> EndTitleProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(EndTitle), "Ending MRR");

    /// <summary>The words after the count of a step without its own noun.</summary>
    public static readonly StyledProperty<string> AccountsNounProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(AccountsNoun), "accounts");

    /// <summary>The words after the count on the start bar without a period noun.</summary>
    public static readonly StyledProperty<string> StartingNounProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(StartingNoun), "paying accounts at the start");

    /// <summary>The words after the count on the end bar without a period noun.</summary>
    public static readonly StyledProperty<string> EndingNounProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(EndingNoun), "paying accounts at the end");

    /// <summary>The remainder row's name.</summary>
    public static readonly StyledProperty<string> RestLabelProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(RestLabel), "Remaining accounts");

    /// <summary>The remainder and tier rows' second line; {0} is the count.</summary>
    public static readonly StyledProperty<string> AccountCountFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(AccountCountFormat), "{0} accounts");

    /// <summary>The second line of a row that stands for one account.</summary>
    public static readonly StyledProperty<string> OneAccountTextProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(OneAccountText), "1 account");

    /// <summary>The hint under the panel.</summary>
    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(Hint), "Pick a bar, or use the arrow keys, to list its accounts");

    /// <summary>The hint while a different bar is previewed under the pointer.</summary>
    public static readonly StyledProperty<string> HoverHintProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(HoverHint), "Click to keep this list");

    /// <summary>The panel's screen reader name; {0} is the bar's name in lower case.</summary>
    public static readonly StyledProperty<string> PanelNameFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(PanelNameFormat), "Accounts behind {0}");

    /// <summary>The bar group's screen reader name.</summary>
    public static readonly StyledProperty<string> BarsNameProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(BarsName), "Movements");

    /// <summary>A bar's name for screen readers; {0} name, {1} amount, {2} accounts.</summary>
    public static readonly StyledProperty<string> BarNameFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(BarNameFormat), "{0}, {1}, {2}");

    /// <summary>Said when the period changes; {0} period, {1} start, {2} end, {3} up or down, {4} change.</summary>
    public static readonly StyledProperty<string> PeriodAnnouncementFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(PeriodAnnouncementFormat), "{0}: MRR moved from {1} to {2}, {3} {4}");

    /// <summary>"up" in sentences.</summary>
    public static readonly StyledProperty<string> UpWordProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(UpWord), "up");

    /// <summary>"down" in sentences.</summary>
    public static readonly StyledProperty<string> DownWordProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(DownWord), "down");

    /// <summary>The chart's summary; {0} period, {1} start, {2} steps, {3} end.</summary>
    public static readonly StyledProperty<string> SummaryFormatProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(SummaryFormat), "MRR bridge for {0}: starts at {1}; {2}; ends at {3}.");

    /// <summary>The chart's help text.</summary>
    public static readonly StyledProperty<string> ChartHintProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(ChartHint),
            "Arrow keys move between bars and wrap around. Home and End jump to the first and last bar.");

    /// <summary>The screen reader table's column headers, comma separated: step, amount, share, accounts.</summary>
    public static readonly StyledProperty<string> TableHeadersProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(TableHeaders), "Movement,Amount,Share of starting MRR,Accounts");

    /// <summary>Shown when there is no period.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<MrrWaterfallLabels, string>(nameof(EmptyText), "No periods to show yet.");

    /// <summary>Creates the labels with the English defaults.</summary>
    public MrrWaterfallLabels()
    {
        PropertyChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when a text changes.</summary>
    public event EventHandler? Changed;

    /// <inheritdoc cref="PeriodSwitchProperty"/>
    public string PeriodSwitch { get => GetValue(PeriodSwitchProperty); set => SetValue(PeriodSwitchProperty, value); }

    /// <inheritdoc cref="ValueSwitchProperty"/>
    public string ValueSwitch { get => GetValue(ValueSwitchProperty); set => SetValue(ValueSwitchProperty, value); }

    /// <inheritdoc cref="CurrencyOptionProperty"/>
    public string CurrencyOption { get => GetValue(CurrencyOptionProperty); set => SetValue(CurrencyOptionProperty, value); }

    /// <inheritdoc cref="PercentOptionProperty"/>
    public string PercentOption { get => GetValue(PercentOptionProperty); set => SetValue(PercentOptionProperty, value); }

    /// <inheritdoc cref="EndingFormatProperty"/>
    public string EndingFormat { get => GetValue(EndingFormatProperty); set => SetValue(EndingFormatProperty, value); }

    /// <inheritdoc cref="SinceFormatProperty"/>
    public string SinceFormat { get => GetValue(SinceFormatProperty); set => SetValue(SinceFormatProperty, value); }

    /// <inheritdoc cref="RetentionLabelProperty"/>
    public string RetentionLabel { get => GetValue(RetentionLabelProperty); set => SetValue(RetentionLabelProperty, value); }

    /// <inheritdoc cref="QuickRatioLabelProperty"/>
    public string QuickRatioLabel { get => GetValue(QuickRatioLabelProperty); set => SetValue(QuickRatioLabelProperty, value); }

    /// <inheritdoc cref="LogoChurnLabelProperty"/>
    public string LogoChurnLabel { get => GetValue(LogoChurnLabelProperty); set => SetValue(LogoChurnLabelProperty, value); }

    /// <inheritdoc cref="StartBarProperty"/>
    public string StartBar { get => GetValue(StartBarProperty); set => SetValue(StartBarProperty, value); }

    /// <inheritdoc cref="EndBarProperty"/>
    public string EndBar { get => GetValue(EndBarProperty); set => SetValue(EndBarProperty, value); }

    /// <inheritdoc cref="StartTitleProperty"/>
    public string StartTitle { get => GetValue(StartTitleProperty); set => SetValue(StartTitleProperty, value); }

    /// <inheritdoc cref="EndTitleProperty"/>
    public string EndTitle { get => GetValue(EndTitleProperty); set => SetValue(EndTitleProperty, value); }

    /// <inheritdoc cref="AccountsNounProperty"/>
    public string AccountsNoun { get => GetValue(AccountsNounProperty); set => SetValue(AccountsNounProperty, value); }

    /// <inheritdoc cref="StartingNounProperty"/>
    public string StartingNoun { get => GetValue(StartingNounProperty); set => SetValue(StartingNounProperty, value); }

    /// <inheritdoc cref="EndingNounProperty"/>
    public string EndingNoun { get => GetValue(EndingNounProperty); set => SetValue(EndingNounProperty, value); }

    /// <inheritdoc cref="RestLabelProperty"/>
    public string RestLabel { get => GetValue(RestLabelProperty); set => SetValue(RestLabelProperty, value); }

    /// <inheritdoc cref="AccountCountFormatProperty"/>
    public string AccountCountFormat { get => GetValue(AccountCountFormatProperty); set => SetValue(AccountCountFormatProperty, value); }

    /// <inheritdoc cref="OneAccountTextProperty"/>
    public string OneAccountText { get => GetValue(OneAccountTextProperty); set => SetValue(OneAccountTextProperty, value); }

    /// <inheritdoc cref="HintProperty"/>
    public string Hint { get => GetValue(HintProperty); set => SetValue(HintProperty, value); }

    /// <inheritdoc cref="HoverHintProperty"/>
    public string HoverHint { get => GetValue(HoverHintProperty); set => SetValue(HoverHintProperty, value); }

    /// <inheritdoc cref="PanelNameFormatProperty"/>
    public string PanelNameFormat { get => GetValue(PanelNameFormatProperty); set => SetValue(PanelNameFormatProperty, value); }

    /// <inheritdoc cref="BarsNameProperty"/>
    public string BarsName { get => GetValue(BarsNameProperty); set => SetValue(BarsNameProperty, value); }

    /// <inheritdoc cref="BarNameFormatProperty"/>
    public string BarNameFormat { get => GetValue(BarNameFormatProperty); set => SetValue(BarNameFormatProperty, value); }

    /// <inheritdoc cref="PeriodAnnouncementFormatProperty"/>
    public string PeriodAnnouncementFormat { get => GetValue(PeriodAnnouncementFormatProperty); set => SetValue(PeriodAnnouncementFormatProperty, value); }

    /// <inheritdoc cref="UpWordProperty"/>
    public string UpWord { get => GetValue(UpWordProperty); set => SetValue(UpWordProperty, value); }

    /// <inheritdoc cref="DownWordProperty"/>
    public string DownWord { get => GetValue(DownWordProperty); set => SetValue(DownWordProperty, value); }

    /// <inheritdoc cref="SummaryFormatProperty"/>
    public string SummaryFormat { get => GetValue(SummaryFormatProperty); set => SetValue(SummaryFormatProperty, value); }

    /// <inheritdoc cref="ChartHintProperty"/>
    public string ChartHint { get => GetValue(ChartHintProperty); set => SetValue(ChartHintProperty, value); }

    /// <inheritdoc cref="TableHeadersProperty"/>
    public string TableHeaders { get => GetValue(TableHeadersProperty); set => SetValue(TableHeadersProperty, value); }

    /// <inheritdoc cref="EmptyTextProperty"/>
    public string EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }
}
