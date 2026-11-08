using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A subscription checkout on one page: express wallets, a plan radio group with a billing switch, a seat stepper,
/// add-ons, billing details and a card form on the left, and an order summary on the right that recalculates every line,
/// the coupon, the tax by country and the total with each change, with every amount rolling digit by digit. A successful
/// payment fades the form aside while the summary glides to the centre and unfolds into a receipt. Below 760 px of its own
/// width the summary sits on top as a total you can expand; below 520 px plans and fields stack.
/// </summary>
/// <remarks>
/// Payments and coupon checks go through <see cref="PayAsync"/> and <see cref="ValidateCouponAsync"/>; without them the
/// block simulates both (<see cref="LiveCheckoutSamples"/>). Treat the client numbers as a preview and keep the real
/// tax and coupon math on your server; in production use your payment provider's hosted card fields.
/// </remarks>
/// <example><code>
/// &lt;arc:LiveCheckout SelectedPlanId="crew" Seats="12" Billing="Yearly" Currency="EUR" /&gt;
/// </code></example>
[TemplatePart(ShellPart, typeof(LiveCheckoutShell))]
[PseudoClasses(NarrowPseudoClass, StackedPseudoClass, ProcessingPseudoClass, PaidPseudoClass, SummaryOpenPseudoClass)]
public partial class LiveCheckout : TemplatedControl
{
    public const string ShellPart = "PART_Shell";

    /// <summary>Set below the two-column width (760 px of the block's own width).</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set below the stacking width (520 px): plans, wallets and billing fields in one column.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while a payment runs.</summary>
    public const string ProcessingPseudoClass = ":processing";

    /// <summary>Set while the receipt is shown.</summary>
    public const string PaidPseudoClass = ":paid";

    /// <summary>Set while the narrow summary is expanded.</summary>
    public const string SummaryOpenPseudoClass = ":summary-open";

    #region Properties

    /// <summary>The plans to choose from, in display order.</summary>
    public static readonly DirectProperty<LiveCheckout, IList<LiveCheckoutPlan>> PlansProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, IList<LiveCheckoutPlan>>(nameof(Plans), o => o.Plans, (o, v) => o.Plans = v);

    /// <summary>The optional add-ons, in display order (summary rows follow this order).</summary>
    public static readonly DirectProperty<LiveCheckout, IList<LiveCheckoutAddon>> AddonsProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, IList<LiveCheckoutAddon>>(nameof(Addons), o => o.Addons, (o, v) => o.Addons = v);

    /// <summary>The billing countries with their tax rates and postal code rules.</summary>
    public static readonly DirectProperty<LiveCheckout, IList<LiveCheckoutCountry>> CountriesProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, IList<LiveCheckoutCountry>>(nameof(Countries), o => o.Countries, (o, v) => o.Countries = v);

    /// <summary>The express wallet buttons above the card form; an empty list hides the row and its divider.</summary>
    public static readonly DirectProperty<LiveCheckout, IList<LiveCheckoutWallet>> WalletsProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, IList<LiveCheckoutWallet>>(nameof(Wallets), o => o.Wallets, (o, v) => o.Wallets = v);

    /// <summary>Ids of the ticked add-ons. Two-way: the check boxes edit it.</summary>
    public static readonly DirectProperty<LiveCheckout, IList<string>> SelectedAddonIdsProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, IList<string>>(nameof(SelectedAddonIds), o => o.SelectedAddonIds,
            (o, v) => o.SelectedAddonIds = v);

    /// <summary>Id of the chosen plan ("crew" by default; an unknown id falls back to the first plan). Two-way.</summary>
    public static readonly StyledProperty<string?> SelectedPlanIdProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(SelectedPlanId), LiveCheckoutSamples.DefaultPlanId,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Seats to buy, kept between <see cref="MinSeats"/> and <see cref="MaxSeats"/> (8 by default). Two-way.</summary>
    public static readonly StyledProperty<int> SeatsProperty =
        AvaloniaProperty.Register<LiveCheckout, int>(nameof(Seats), 8, defaultBindingMode: BindingMode.TwoWay, coerce: CoerceSeats);

    /// <summary>The fewest seats (1 by default).</summary>
    public static readonly StyledProperty<int> MinSeatsProperty =
        AvaloniaProperty.Register<LiveCheckout, int>(nameof(MinSeats), 1);

    /// <summary>The most seats (500 by default).</summary>
    public static readonly StyledProperty<int> MaxSeatsProperty =
        AvaloniaProperty.Register<LiveCheckout, int>(nameof(MaxSeats), 500);

    /// <summary>The billing period (yearly by default). Two-way: the billing switch edits it.</summary>
    public static readonly StyledProperty<LiveCheckoutBilling> BillingProperty =
        AvaloniaProperty.Register<LiveCheckout, LiveCheckoutBilling>(nameof(Billing), LiveCheckoutBilling.Yearly,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Discount for yearly billing, from 0 to 1 (0.2 by default), shown on the billing switch.</summary>
    public static readonly StyledProperty<decimal> YearlyDiscountProperty =
        AvaloniaProperty.Register<LiveCheckout, decimal>(nameof(YearlyDiscount), 0.2m, coerce: (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>ISO 4217 code of every amount ("USD" by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<LiveCheckout, string>(nameof(Currency), "USD");

    /// <summary>Code of the billing country (the first country when unknown). Two-way: the country list edits it.</summary>
    public static readonly StyledProperty<string?> CountryCodeProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(CountryCode), LiveCheckoutSamples.Billing.CountryCode,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The receipt email. Two-way.</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(Email), LiveCheckoutSamples.Billing.Email, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The name on the card. Two-way.</summary>
    public static readonly StyledProperty<string?> CardholderNameProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(CardholderName), LiveCheckoutSamples.Billing.Name,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The street address. Two-way.</summary>
    public static readonly StyledProperty<string?> AddressProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(Address), LiveCheckoutSamples.Billing.Address,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The city. Two-way.</summary>
    public static readonly StyledProperty<string?> CityProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(City), LiveCheckoutSamples.Billing.City, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The postal code, checked against the country's pattern. Two-way.</summary>
    public static readonly StyledProperty<string?> PostalCodeProperty =
        AvaloniaProperty.Register<LiveCheckout, string?>(nameof(PostalCode), LiveCheckoutSamples.Billing.PostalCode,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The card "Use test card" fills in; null hides the button.</summary>
    public static readonly StyledProperty<LiveCheckoutTestCard?> TestCardProperty =
        AvaloniaProperty.Register<LiveCheckout, LiveCheckoutTestCard?>(nameof(TestCard), LiveCheckoutSamples.TestCard());

    /// <summary>Whether the narrow summary is expanded. Two-way: its bar toggles it.</summary>
    public static readonly StyledProperty<bool> IsSummaryOpenProperty =
        AvaloniaProperty.Register<LiveCheckout, bool>(nameof(IsSummaryOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The built-in text and the culture amounts and dates use; replace the object to localize the block.</summary>
    public static readonly StyledProperty<LiveCheckoutLabels> LabelsProperty =
        AvaloniaProperty.Register<LiveCheckout, LiveCheckoutLabels>(nameof(Labels), new LiveCheckoutLabels());

    /// <summary>Where "today" comes from (receipt date, card expiry); the system clock by default.</summary>
    public static readonly StyledProperty<TimeProvider> ClockProperty =
        AvaloniaProperty.Register<LiveCheckout, TimeProvider>(nameof(Clock), TimeProvider.System);

    /// <summary>True while a payment runs.</summary>
    public static readonly DirectProperty<LiveCheckout, bool> IsProcessingProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, bool>(nameof(IsProcessing), o => o.IsProcessing);

    /// <summary>True while the receipt is shown.</summary>
    public static readonly DirectProperty<LiveCheckout, bool> IsPaidProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, bool>(nameof(IsPaid), o => o.IsPaid);

    /// <summary>The current subtotal, discount, tax and total.</summary>
    public static readonly DirectProperty<LiveCheckout, LiveCheckoutTotals> TotalsProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, LiveCheckoutTotals>(nameof(Totals), o => o.Totals);

    /// <summary>The applied coupon code, or null.</summary>
    public static readonly DirectProperty<LiveCheckout, string?> CouponCodeProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, string?>(nameof(CouponCode), o => o.CouponCode);

    /// <summary>The receipt of the last successful payment, or null.</summary>
    public static readonly DirectProperty<LiveCheckout, LiveCheckoutReceipt?> ReceiptProperty =
        AvaloniaProperty.RegisterDirect<LiveCheckout, LiveCheckoutReceipt?>(nameof(Receipt), o => o.Receipt);

    /// <summary>Raised whenever the totals change (plan, seats, billing, add-ons, coupon or country).</summary>
    public static readonly RoutedEvent<LiveCheckoutTotalsChangedEventArgs> TotalsChangedEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutTotalsChangedEventArgs>(nameof(TotalsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a coupon is accepted.</summary>
    public static readonly RoutedEvent<LiveCheckoutCouponEventArgs> CouponAppliedEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutCouponEventArgs>(nameof(CouponApplied), RoutingStrategies.Bubble);

    /// <summary>Raised when the applied coupon is removed.</summary>
    public static readonly RoutedEvent<LiveCheckoutCouponEventArgs> CouponRemovedEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutCouponEventArgs>(nameof(CouponRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised after a successful payment, as the receipt appears.</summary>
    public static readonly RoutedEvent<LiveCheckoutPaidEventArgs> PaidEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutPaidEventArgs>(nameof(Paid), RoutingStrategies.Bubble);

    /// <summary>Raised when a payment fails; its message is shown above the pay button.</summary>
    public static readonly RoutedEvent<LiveCheckoutPaymentFailedEventArgs> PaymentFailedEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutPaymentFailedEventArgs>(nameof(PaymentFailed), RoutingStrategies.Bubble);

    /// <summary>Raised when "Receipt PDF" is pressed: produce or download the receipt here.</summary>
    public static readonly RoutedEvent<LiveCheckoutPaidEventArgs> ReceiptRequestedEvent =
        RoutedEvent.Register<LiveCheckout, LiveCheckoutPaidEventArgs>(nameof(ReceiptRequested), RoutingStrategies.Bubble);

    /// <summary>Raised after "Start over" (or <see cref="Reset"/>) put everything back.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RestartedEvent =
        RoutedEvent.Register<LiveCheckout, RoutedEventArgs>(nameof(Restarted), RoutingStrategies.Bubble);

    /// <summary>Two-column width (theme token).</summary>
    internal static readonly StyledProperty<double> TwoColumnWidthProperty =
        AvaloniaProperty.Register<LiveCheckout, double>(nameof(TwoColumnWidth), 760);

    /// <summary>Stacking width (theme token).</summary>
    internal static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<LiveCheckout, double>(nameof(StackWidth), 520);

    private IList<LiveCheckoutPlan> _plans = new AvaloniaList<LiveCheckoutPlan>(LiveCheckoutSamples.Plans());
    private IList<LiveCheckoutAddon> _addons = new AvaloniaList<LiveCheckoutAddon>(LiveCheckoutSamples.Addons());
    private IList<LiveCheckoutCountry> _countries = new AvaloniaList<LiveCheckoutCountry>(LiveCheckoutSamples.Countries());
    private IList<LiveCheckoutWallet> _wallets = new AvaloniaList<LiveCheckoutWallet>(LiveCheckoutSamples.Wallets());
    private IList<string> _selectedAddonIds = new AvaloniaList<string> { LiveCheckoutSamples.DefaultAddonId };
    private bool _isProcessing;
    private bool _isPaid;
    private LiveCheckoutTotals _totals;
    private string? _couponCode;
    private LiveCheckoutReceipt? _receipt;

    /// <inheritdoc cref="PlansProperty"/>
    public IList<LiveCheckoutPlan> Plans
    {
        get => _plans;
        set => SetList(PlansProperty, ref _plans, value, RebuildPlans);
    }

    /// <inheritdoc cref="AddonsProperty"/>
    public IList<LiveCheckoutAddon> Addons
    {
        get => _addons;
        set => SetList(AddonsProperty, ref _addons, value, RebuildAddons);
    }

    /// <inheritdoc cref="CountriesProperty"/>
    public IList<LiveCheckoutCountry> Countries
    {
        get => _countries;
        set => SetList(CountriesProperty, ref _countries, value, RebuildCountries);
    }

    /// <inheritdoc cref="WalletsProperty"/>
    public IList<LiveCheckoutWallet> Wallets
    {
        get => _wallets;
        set => SetList(WalletsProperty, ref _wallets, value, RebuildWallets);
    }

    /// <inheritdoc cref="SelectedAddonIdsProperty"/>
    public IList<string> SelectedAddonIds
    {
        get => _selectedAddonIds;
        set => SetList(SelectedAddonIdsProperty, ref _selectedAddonIds, value, SyncAddonChecks);
    }

    /// <inheritdoc cref="SelectedPlanIdProperty"/>
    public string? SelectedPlanId { get => GetValue(SelectedPlanIdProperty); set => SetValue(SelectedPlanIdProperty, value); }

    /// <inheritdoc cref="SeatsProperty"/>
    public int Seats { get => GetValue(SeatsProperty); set => SetValue(SeatsProperty, value); }

    /// <inheritdoc cref="MinSeatsProperty"/>
    public int MinSeats { get => GetValue(MinSeatsProperty); set => SetValue(MinSeatsProperty, value); }

    /// <inheritdoc cref="MaxSeatsProperty"/>
    public int MaxSeats { get => GetValue(MaxSeatsProperty); set => SetValue(MaxSeatsProperty, value); }

    /// <inheritdoc cref="BillingProperty"/>
    public LiveCheckoutBilling Billing { get => GetValue(BillingProperty); set => SetValue(BillingProperty, value); }

    /// <inheritdoc cref="YearlyDiscountProperty"/>
    public decimal YearlyDiscount { get => GetValue(YearlyDiscountProperty); set => SetValue(YearlyDiscountProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="CountryCodeProperty"/>
    public string? CountryCode { get => GetValue(CountryCodeProperty); set => SetValue(CountryCodeProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="CardholderNameProperty"/>
    public string? CardholderName { get => GetValue(CardholderNameProperty); set => SetValue(CardholderNameProperty, value); }

    /// <inheritdoc cref="AddressProperty"/>
    public string? Address { get => GetValue(AddressProperty); set => SetValue(AddressProperty, value); }

    /// <inheritdoc cref="CityProperty"/>
    public string? City { get => GetValue(CityProperty); set => SetValue(CityProperty, value); }

    /// <inheritdoc cref="PostalCodeProperty"/>
    public string? PostalCode { get => GetValue(PostalCodeProperty); set => SetValue(PostalCodeProperty, value); }

    /// <inheritdoc cref="TestCardProperty"/>
    public LiveCheckoutTestCard? TestCard { get => GetValue(TestCardProperty); set => SetValue(TestCardProperty, value); }

    /// <inheritdoc cref="IsSummaryOpenProperty"/>
    public bool IsSummaryOpen { get => GetValue(IsSummaryOpenProperty); set => SetValue(IsSummaryOpenProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public LiveCheckoutLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ClockProperty"/>
    public TimeProvider Clock { get => GetValue(ClockProperty); set => SetValue(ClockProperty, value); }

    /// <inheritdoc cref="IsProcessingProperty"/>
    public bool IsProcessing
    {
        get => _isProcessing;
        private set
        {
            if (SetAndRaise(IsProcessingProperty, ref _isProcessing, value))
            {
                PseudoClasses.Set(ProcessingPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="IsPaidProperty"/>
    public bool IsPaid
    {
        get => _isPaid;
        private set
        {
            if (SetAndRaise(IsPaidProperty, ref _isPaid, value))
            {
                PseudoClasses.Set(PaidPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="TotalsProperty"/>
    public LiveCheckoutTotals Totals
    {
        get => _totals;
        private set => SetAndRaise(TotalsProperty, ref _totals, value);
    }

    /// <inheritdoc cref="CouponCodeProperty"/>
    public string? CouponCode
    {
        get => _couponCode;
        private set => SetAndRaise(CouponCodeProperty, ref _couponCode, value);
    }

    /// <inheritdoc cref="ReceiptProperty"/>
    public LiveCheckoutReceipt? Receipt
    {
        get => _receipt;
        private set => SetAndRaise(ReceiptProperty, ref _receipt, value);
    }

    /// <summary>
    /// Checks a coupon code (as typed, in upper case). Return <see cref="LiveCheckoutCouponResult.Accept"/> or
    /// <see cref="LiveCheckoutCouponResult.Reject"/>; throwing counts as a refusal with the exception's message. Null uses
    /// the sample rules (SPRING15 and CREW10 after 480 ms).
    /// </summary>
    public Func<string, CancellationToken, Task<LiveCheckoutCouponResult>>? ValidateCouponAsync { get; set; }

    /// <summary>
    /// Charges an order. Throw to fail: the exception's message is shown above the pay button. Return a receipt (or null)
    /// to succeed; missing receipt values are filled in. Null uses the simulator (1500 ms for cards, 1100 ms for wallets;
    /// <see cref="LiveCheckoutSamples.DeclinedCardNumber"/> is declined).
    /// </summary>
    public Func<LiveCheckoutOrder, CancellationToken, Task<LiveCheckoutReceipt?>>? PayAsync { get; set; }

    /// <summary>The summary rows as shown now.</summary>
    public IReadOnlyList<LiveCheckoutLine> Lines => _lines;

    /// <inheritdoc cref="TotalsChangedEvent"/>
    public event EventHandler<LiveCheckoutTotalsChangedEventArgs>? TotalsChanged
    {
        add => AddHandler(TotalsChangedEvent, value);
        remove => RemoveHandler(TotalsChangedEvent, value);
    }

    /// <inheritdoc cref="CouponAppliedEvent"/>
    public event EventHandler<LiveCheckoutCouponEventArgs>? CouponApplied
    {
        add => AddHandler(CouponAppliedEvent, value);
        remove => RemoveHandler(CouponAppliedEvent, value);
    }

    /// <inheritdoc cref="CouponRemovedEvent"/>
    public event EventHandler<LiveCheckoutCouponEventArgs>? CouponRemoved
    {
        add => AddHandler(CouponRemovedEvent, value);
        remove => RemoveHandler(CouponRemovedEvent, value);
    }

    /// <inheritdoc cref="PaidEvent"/>
    public event EventHandler<LiveCheckoutPaidEventArgs>? Paid
    {
        add => AddHandler(PaidEvent, value);
        remove => RemoveHandler(PaidEvent, value);
    }

    /// <inheritdoc cref="PaymentFailedEvent"/>
    public event EventHandler<LiveCheckoutPaymentFailedEventArgs>? PaymentFailed
    {
        add => AddHandler(PaymentFailedEvent, value);
        remove => RemoveHandler(PaymentFailedEvent, value);
    }

    /// <inheritdoc cref="ReceiptRequestedEvent"/>
    public event EventHandler<LiveCheckoutPaidEventArgs>? ReceiptRequested
    {
        add => AddHandler(ReceiptRequestedEvent, value);
        remove => RemoveHandler(ReceiptRequestedEvent, value);
    }

    /// <inheritdoc cref="RestartedEvent"/>
    public event EventHandler<RoutedEventArgs>? Restarted
    {
        add => AddHandler(RestartedEvent, value);
        remove => RemoveHandler(RestartedEvent, value);
    }

    internal double TwoColumnWidth { get => GetValue(TwoColumnWidthProperty); set => SetValue(TwoColumnWidthProperty, value); }
    internal double StackWidth { get => GetValue(StackWidthProperty); set => SetValue(StackWidthProperty, value); }

    #endregion

    private static int CoerceSeats(AvaloniaObject owner, int value)
    {
        var block = (LiveCheckout)owner;
        return Math.Clamp(value, Math.Max(1, block.MinSeats), Math.Max(block.MinSeats, block.MaxSeats));
    }

    private void SetList<T>(DirectPropertyBase<IList<T>> property, ref IList<T> field, IList<T>? value, Action changed)
    {
        if (field is INotifyCollectionChanged oldList)
        {
            oldList.CollectionChanged -= OnListChanged;
        }
        var next = value ?? new AvaloniaList<T>();
        if (SetAndRaise(property, ref field, next))
        {
            changed();
        }
        if (field is INotifyCollectionChanged newList)
        {
            newList.CollectionChanged -= OnListChanged;
            newList.CollectionChanged += OnListChanged;
        }
    }

    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _plans))
        {
            RebuildPlans();
        }
        else if (ReferenceEquals(sender, _addons))
        {
            RebuildAddons();
        }
        else if (ReferenceEquals(sender, _countries))
        {
            RebuildCountries();
        }
        else if (ReferenceEquals(sender, _wallets))
        {
            RebuildWallets();
        }
        else if (ReferenceEquals(sender, _selectedAddonIds))
        {
            SyncAddonChecks();
        }
    }
}
