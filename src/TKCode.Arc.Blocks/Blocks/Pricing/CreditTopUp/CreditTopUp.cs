using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A card for buying usage credits: the balance with a two-week usage chart and how many days it lasts, preset packs
/// with bonus badges, a custom amount slider, auto top-up with a threshold, the saved card, and a checkout whose new
/// balance rolls in like an odometer. Every price, badge and total derives from <see cref="Pricing"/>. Without a
/// <see cref="PurchaseHandler"/> the card simulates a 1.2 second charge and adds the credits to its own
/// <see cref="Balance"/>; with one, it charges through your backend and shows the error with a retry when the handler
/// throws.
/// </summary>
/// <example><code>
/// &lt;arc:CreditTopUp Balance="{Binding Credits}" Usage="{Binding DailyUsage}" PaymentMethods="{Binding Cards}" /&gt;
/// </code></example>
[TemplatePart(BalancePart, typeof(CreditTopUpOdometer))]
[TemplatePart(PacksPart, typeof(CreditTopUpChoiceGroup))]
[TemplatePart(SliderPart, typeof(Slider))]
[TemplatePart(AutoSwitchPart, typeof(Switch))]
[TemplatePart(ThresholdsPart, typeof(CreditTopUpChoiceGroup))]
[TemplatePart(ChangePart, typeof(Button))]
[TemplatePart(BuyPart, typeof(Button))]
[TemplatePart(StatusPart, typeof(ArcContentSwap))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, AutoOffPseudoClass, ProcessingPseudoClass, DonePseudoClass, ErrorPseudoClass)]
public partial class CreditTopUp : TemplatedControl
{
    public const string HeadPart = "PART_Head";
    public const string BalanceColumnPart = "PART_BalanceColumn";
    public const string BalanceTitlePart = "PART_BalanceTitle";
    public const string HealthPart = "PART_Health";
    public const string BalancePart = "PART_Balance";
    public const string DaysLeftPart = "PART_DaysLeft";
    public const string ChartBoxPart = "PART_ChartBox";
    public const string ChartPart = "PART_Chart";
    public const string ChartLabelPart = "PART_ChartLabel";
    public const string AddTitlePart = "PART_AddTitle";
    public const string RatePart = "PART_Rate";
    public const string PacksPart = "PART_Packs";
    public const string SliderPart = "PART_Slider";
    public const string AutoTitlePart = "PART_AutoTitle";
    public const string AutoSubtitlePart = "PART_AutoSubtitle";
    public const string AutoSwitchPart = "PART_AutoSwitch";
    public const string ThresholdTitlePart = "PART_ThresholdTitle";
    public const string ThresholdsPart = "PART_Thresholds";
    public const string CardInfoPart = "PART_CardInfo";
    public const string ChangePart = "PART_Change";
    public const string TotalPart = "PART_Total";
    public const string BonusLinePart = "PART_BonusLine";
    public const string PricePart = "PART_Price";
    public const string BuyPart = "PART_Buy";
    public const string StatusPart = "PART_Status";

    /// <summary>Set while the card is at most <c>448</c> wide: the chart moves under the balance.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the card is at most <c>416</c> wide: the side paddings tighten to 16.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while auto top-up is off: the threshold row dims and takes no input.</summary>
    public const string AutoOffPseudoClass = ":auto-off";

    /// <summary>Set while a charge runs.</summary>
    public const string ProcessingPseudoClass = ":processing";

    /// <summary>Set while the card shows a finished purchase.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>Set after a failed charge.</summary>
    public const string ErrorPseudoClass = ":error";

    #region Properties

    /// <summary>
    /// The balance in credits. Two-way: a purchase adds its total (bonus included). Any change, from a purchase or from
    /// outside, rolls the figure.
    /// </summary>
    public static readonly StyledProperty<long> BalanceProperty =
        AvaloniaProperty.Register<CreditTopUp, long>(nameof(Balance), CreditTopUpSamples.Balance, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Daily usage, oldest first; the last day is today (a partial day). Drives the bars, their readout and the days-left
    /// line (the average of the full days). Null or empty hides the chart and the days-left line. Default: 14 sample days.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<CreditTopUpUsageDay>?> UsageProperty =
        AvaloniaProperty.Register<CreditTopUp, IReadOnlyList<CreditTopUpUsageDay>?>(nameof(Usage), CreditTopUpSamples.Usage());

    /// <summary>The preset pack sizes in credits (keep it to three for the layout). Default: 1,500, 6,000 and 24,000.</summary>
    public static readonly StyledProperty<IReadOnlyList<long>> PacksProperty =
        AvaloniaProperty.Register<CreditTopUp, IReadOnlyList<long>>(nameof(Packs), CreditTopUpSamples.Packs());

    /// <summary>The price per credit, currency and bonus tiers every price, badge and total derive from.</summary>
    public static readonly StyledProperty<CreditTopUpPricing> PricingProperty =
        AvaloniaProperty.Register<CreditTopUp, CreditTopUpPricing>(nameof(Pricing), CreditTopUpPricing.Default);

    /// <summary>The custom amount slider's range and step. An amount equal to a pack selects that pack.</summary>
    public static readonly StyledProperty<CreditTopUpRange> RangeProperty =
        AvaloniaProperty.Register<CreditTopUp, CreditTopUpRange>(nameof(Range), CreditTopUpRange.Default);

    /// <summary>The amount to buy, in credits (two-way; kept inside <see cref="Range"/> on its step). Default 6,000, the second sample pack.</summary>
    public static readonly StyledProperty<long> SelectedCreditsProperty =
        AvaloniaProperty.Register<CreditTopUp, long>(nameof(SelectedCredits), 6_000, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The saved cards; Change cycles through them. Empty shows "No saved card" and the purchase goes without a card id.</summary>
    public static readonly StyledProperty<IReadOnlyList<CreditTopUpPaymentMethod>> PaymentMethodsProperty =
        AvaloniaProperty.Register<CreditTopUp, IReadOnlyList<CreditTopUpPaymentMethod>>(nameof(PaymentMethods), CreditTopUpSamples.PaymentMethods());

    /// <summary>The <see cref="CreditTopUpPaymentMethod.Id"/> of the card to charge (two-way); null or unknown uses the first card.</summary>
    public static readonly StyledProperty<string?> SelectedPaymentMethodIdProperty =
        AvaloniaProperty.Register<CreditTopUp, string?>(nameof(SelectedPaymentMethodId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The auto top-up threshold choices. Default: 750, 1,500 and 3,000.</summary>
    public static readonly StyledProperty<IReadOnlyList<long>> ThresholdsProperty =
        AvaloniaProperty.Register<CreditTopUp, IReadOnlyList<long>>(nameof(Thresholds), CreditTopUpSamples.Thresholds());

    /// <summary>Whether auto top-up is on (two-way: the switch edits it).</summary>
    public static readonly StyledProperty<bool> IsAutoTopUpEnabledProperty =
        AvaloniaProperty.Register<CreditTopUp, bool>(nameof(IsAutoTopUpEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The auto top-up threshold in credits (two-way: the threshold choices edit it). The balance reads as running low
    /// below it, even while auto top-up is off. Default 1,500.
    /// </summary>
    public static readonly StyledProperty<long> AutoTopUpThresholdProperty =
        AvaloniaProperty.Register<CreditTopUp, long>(nameof(AutoTopUpThreshold), 1_500, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Culture of every count, amount and date (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<CreditTopUp, string>(nameof(CultureName), "en-US");

    /// <summary>The built-in text; replace the object to reword or localize the card.</summary>
    public static readonly StyledProperty<CreditTopUpLabels> LabelsProperty =
        AvaloniaProperty.Register<CreditTopUp, CreditTopUpLabels>(nameof(Labels), CreditTopUpLabels.Default);

    /// <summary>Where the purchase stands: idle, processing, done or error.</summary>
    public static readonly DirectProperty<CreditTopUp, CreditTopUpPhase> PhaseProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, CreditTopUpPhase>(nameof(Phase), o => o.Phase);

    /// <summary>The price of <see cref="SelectedCredits"/> under <see cref="Pricing"/>.</summary>
    public static readonly DirectProperty<CreditTopUp, CreditTopUpQuote> CurrentQuoteProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, CreditTopUpQuote>(nameof(CurrentQuote), o => o.CurrentQuote);

    /// <summary>Running low when <see cref="Balance"/> is below <see cref="AutoTopUpThreshold"/>, otherwise healthy.</summary>
    public static readonly DirectProperty<CreditTopUp, CreditTopUpHealth> HealthProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, CreditTopUpHealth>(nameof(Health), o => o.Health);

    /// <summary>The average daily usage over the full days of <see cref="Usage"/> (today left out); null without a full day.</summary>
    public static readonly DirectProperty<CreditTopUp, double?> AverageDailyUsageProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, double?>(nameof(AverageDailyUsage), o => o.AverageDailyUsage);

    /// <summary>Whole days the balance lasts at <see cref="AverageDailyUsage"/> (rounded down); null without a burn rate.</summary>
    public static readonly DirectProperty<CreditTopUp, int?> DaysLeftProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, int?>(nameof(DaysLeft), o => o.DaysLeft);

    /// <summary>The message of the last failed charge while <see cref="Phase"/> is <see cref="CreditTopUpPhase.Error"/>.</summary>
    public static readonly DirectProperty<CreditTopUp, string?> ErrorMessageProperty =
        AvaloniaProperty.RegisterDirect<CreditTopUp, string?>(nameof(ErrorMessage), o => o.ErrorMessage);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<CreditTopUp, double>(nameof(NarrowWidth), 448);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CreditTopUp, double>(nameof(CompactWidth), 416);

    internal static readonly StyledProperty<TimeSpan> NoticeDwellProperty =
        AvaloniaProperty.Register<CreditTopUp, TimeSpan>(nameof(NoticeDwell), TimeSpan.FromMilliseconds(2600));

    internal static readonly StyledProperty<TimeSpan> DoneDwellProperty =
        AvaloniaProperty.Register<CreditTopUp, TimeSpan>(nameof(DoneDwell), TimeSpan.FromMilliseconds(2400));

    /// <summary>Raised when auto top-up is switched or its threshold changes.</summary>
    public static readonly RoutedEvent<CreditTopUpAutoTopUpChangedEventArgs> AutoTopUpChangedEvent =
        RoutedEvent.Register<CreditTopUp, CreditTopUpAutoTopUpChangedEventArgs>(nameof(AutoTopUpChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when Change selects another saved card.</summary>
    public static readonly RoutedEvent<CreditTopUpPaymentMethodChangedEventArgs> PaymentMethodChangedEvent =
        RoutedEvent.Register<CreditTopUp, CreditTopUpPaymentMethodChangedEventArgs>(nameof(PaymentMethodChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a purchase went through, with the order and the new balance.</summary>
    public static readonly RoutedEvent<CreditTopUpPurchasedEventArgs> PurchasedEvent =
        RoutedEvent.Register<CreditTopUp, CreditTopUpPurchasedEventArgs>(nameof(Purchased), RoutingStrategies.Bubble);

    /// <summary>Raised when the purchase handler threw, with the order and the message shown.</summary>
    public static readonly RoutedEvent<CreditTopUpPurchaseFailedEventArgs> PurchaseFailedEvent =
        RoutedEvent.Register<CreditTopUp, CreditTopUpPurchaseFailedEventArgs>(nameof(PurchaseFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="BalanceProperty"/>
    public long Balance { get => GetValue(BalanceProperty); set => SetValue(BalanceProperty, value); }

    /// <inheritdoc cref="UsageProperty"/>
    public IReadOnlyList<CreditTopUpUsageDay>? Usage { get => GetValue(UsageProperty); set => SetValue(UsageProperty, value); }

    /// <inheritdoc cref="PacksProperty"/>
    public IReadOnlyList<long> Packs { get => GetValue(PacksProperty); set => SetValue(PacksProperty, value); }

    /// <inheritdoc cref="PricingProperty"/>
    public CreditTopUpPricing Pricing { get => GetValue(PricingProperty); set => SetValue(PricingProperty, value); }

    /// <inheritdoc cref="RangeProperty"/>
    public CreditTopUpRange Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    /// <inheritdoc cref="SelectedCreditsProperty"/>
    public long SelectedCredits { get => GetValue(SelectedCreditsProperty); set => SetValue(SelectedCreditsProperty, value); }

    /// <inheritdoc cref="PaymentMethodsProperty"/>
    public IReadOnlyList<CreditTopUpPaymentMethod> PaymentMethods { get => GetValue(PaymentMethodsProperty); set => SetValue(PaymentMethodsProperty, value); }

    /// <inheritdoc cref="SelectedPaymentMethodIdProperty"/>
    public string? SelectedPaymentMethodId { get => GetValue(SelectedPaymentMethodIdProperty); set => SetValue(SelectedPaymentMethodIdProperty, value); }

    /// <inheritdoc cref="ThresholdsProperty"/>
    public IReadOnlyList<long> Thresholds { get => GetValue(ThresholdsProperty); set => SetValue(ThresholdsProperty, value); }

    /// <inheritdoc cref="IsAutoTopUpEnabledProperty"/>
    public bool IsAutoTopUpEnabled { get => GetValue(IsAutoTopUpEnabledProperty); set => SetValue(IsAutoTopUpEnabledProperty, value); }

    /// <inheritdoc cref="AutoTopUpThresholdProperty"/>
    public long AutoTopUpThreshold { get => GetValue(AutoTopUpThresholdProperty); set => SetValue(AutoTopUpThresholdProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CreditTopUpLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PhaseProperty"/>
    public CreditTopUpPhase Phase { get => _phase; private set => SetAndRaise(PhaseProperty, ref _phase, value); }

    /// <inheritdoc cref="CurrentQuoteProperty"/>
    public CreditTopUpQuote CurrentQuote { get => _quote; private set => SetAndRaise(CurrentQuoteProperty, ref _quote, value); }

    /// <inheritdoc cref="HealthProperty"/>
    public CreditTopUpHealth Health { get => _health; private set => SetAndRaise(HealthProperty, ref _health, value); }

    /// <inheritdoc cref="AverageDailyUsageProperty"/>
    public double? AverageDailyUsage { get => _average; private set => SetAndRaise(AverageDailyUsageProperty, ref _average, value); }

    /// <inheritdoc cref="DaysLeftProperty"/>
    public int? DaysLeft { get => _daysLeft; private set => SetAndRaise(DaysLeftProperty, ref _daysLeft, value); }

    /// <inheritdoc cref="ErrorMessageProperty"/>
    public string? ErrorMessage { get => _error; private set => SetAndRaise(ErrorMessageProperty, ref _error, value); }

    /// <summary>
    /// Charges an order through your backend: resolve once the credits are added; throw to show the exception's message
    /// with a retry. The token is cancelled when the card leaves the visual tree. Null (the default) simulates a
    /// 1.2 second charge and the status line says nothing was charged.
    /// </summary>
    public Func<CreditTopUpOrder, CancellationToken, Task>? PurchaseHandler
    {
        get => _purchaseHandler;
        set
        {
            _purchaseHandler = value;
            UpdateStatus();
        }
    }

    /// <summary>Whether purchases are simulated (no <see cref="PurchaseHandler"/>).</summary>
    public bool IsSimulated => _purchaseHandler is null;

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal TimeSpan NoticeDwell { get => GetValue(NoticeDwellProperty); set => SetValue(NoticeDwellProperty, value); }
    internal TimeSpan DoneDwell { get => GetValue(DoneDwellProperty); set => SetValue(DoneDwellProperty, value); }

    /// <summary>The waits of the card (simulated charge, done and notice dwell); tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (time, token) => Task.Delay(time, token);

    /// <inheritdoc cref="AutoTopUpChangedEvent"/>
    public event EventHandler<CreditTopUpAutoTopUpChangedEventArgs>? AutoTopUpChanged
    {
        add => AddHandler(AutoTopUpChangedEvent, value);
        remove => RemoveHandler(AutoTopUpChangedEvent, value);
    }

    /// <inheritdoc cref="PaymentMethodChangedEvent"/>
    public event EventHandler<CreditTopUpPaymentMethodChangedEventArgs>? PaymentMethodChanged
    {
        add => AddHandler(PaymentMethodChangedEvent, value);
        remove => RemoveHandler(PaymentMethodChangedEvent, value);
    }

    /// <inheritdoc cref="PurchasedEvent"/>
    public event EventHandler<CreditTopUpPurchasedEventArgs>? Purchased
    {
        add => AddHandler(PurchasedEvent, value);
        remove => RemoveHandler(PurchasedEvent, value);
    }

    /// <inheritdoc cref="PurchaseFailedEvent"/>
    public event EventHandler<CreditTopUpPurchaseFailedEventArgs>? PurchaseFailed
    {
        add => AddHandler(PurchaseFailedEvent, value);
        remove => RemoveHandler(PurchaseFailedEvent, value);
    }

    #endregion

    /// <summary>
    /// Prices <paramref name="credits"/> under <paramref name="pricing"/>: price = credits × unit price, bonus from the
    /// highest tier at or below the amount, total = credits + bonus. Use it on a server to price the same order.
    /// </summary>
    public static CreditTopUpQuote Quote(long credits, CreditTopUpPricing pricing) => CreditTopUpMath.Quote(credits, pricing);

    private CreditTopUpPhase _phase;
    private CreditTopUpQuote _quote;
    private CreditTopUpHealth _health;
    private double? _average;
    private int? _daysLeft;
    private string? _error;
    private Func<CreditTopUpOrder, CancellationToken, Task>? _purchaseHandler;

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }

    public CreditTopUp()
    {
        _quote = CreditTopUpMath.Quote(SelectedCredits, Pricing);
        RecomputeHealth();
    }
}
