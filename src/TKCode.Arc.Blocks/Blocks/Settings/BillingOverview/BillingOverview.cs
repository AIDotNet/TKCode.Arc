using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A billing page in one view: the current plan with its status, price and next charge; the card on file and the
/// billing email; and the invoice history with a download button per invoice. Every action raises a routed event for the
/// host to handle (change or cancel the plan, update the card, edit the email, download an invoice). Older invoices
/// beyond <see cref="VisibleInvoiceCount"/> fold behind "Show all invoices". Composed of <see cref="SettingsGroup"/>,
/// <see cref="SettingsRow"/>, <see cref="Badge"/> and <see cref="Button"/>.
/// </summary>
[TemplatePart(PlanNamePart, typeof(TextBlock))]
[TemplatePart(PlanStatusPart, typeof(Badge))]
[TemplatePart(PlanDetailPart, typeof(TextBlock))]
[TemplatePart(PricePart, typeof(TextBlock))]
[TemplatePart(NextInvoicePart, typeof(TextBlock))]
[TemplatePart(CancelPlanPart, typeof(Button))]
[TemplatePart(ChangePlanPart, typeof(Button))]
[TemplatePart(CardBrandPart, typeof(TextBlock))]
[TemplatePart(CardTitlePart, typeof(TextBlock))]
[TemplatePart(CardExpiryPart, typeof(TextBlock))]
[TemplatePart(UpdateCardPart, typeof(Button))]
[TemplatePart(EmailRowPart, typeof(SettingsRow))]
[TemplatePart(EditEmailPart, typeof(Button))]
[TemplatePart(InvoicesGroupPart, typeof(SettingsGroup))]
[TemplatePart(InvoiceRowsPart, typeof(Panel))]
[TemplatePart(MoreInvoicesPart, typeof(SpringSizeDecorator))]
[TemplatePart(MoreRowsPart, typeof(Panel))]
[TemplatePart(ShowAllPart, typeof(Button))]
[PseudoClasses(NarrowPseudoClass, CardExpiringPseudoClass, CardExpiredPseudoClass, InvoicesExpandedPseudoClass)]
public class BillingOverview : TemplatedControl
{
    public const string PlanGroupPart = "PART_PlanGroup";
    public const string PlanEyebrowPart = "PART_PlanEyebrow";
    public const string PlanNamePart = "PART_PlanName";
    public const string PlanStatusPart = "PART_PlanStatus";
    public const string PlanDetailPart = "PART_PlanDetail";
    public const string PricePart = "PART_Price";
    public const string NextInvoicePart = "PART_NextInvoice";
    public const string CancelPlanPart = "PART_CancelPlan";
    public const string ChangePlanPart = "PART_ChangePlan";
    public const string PaymentGroupPart = "PART_PaymentGroup";
    public const string CardBrandPart = "PART_CardBrand";
    public const string CardGlyphPart = "PART_CardGlyph";
    public const string CardTitlePart = "PART_CardTitle";
    public const string CardExpiryPart = "PART_CardExpiry";
    public const string UpdateCardPart = "PART_UpdateCard";
    public const string EmailRowPart = "PART_EmailRow";
    public const string EditEmailPart = "PART_EditEmail";
    public const string InvoicesGroupPart = "PART_InvoicesGroup";
    public const string InvoiceTablePart = "PART_InvoiceTable";
    public const string InvoiceHeaderPart = "PART_InvoiceHeader";
    public const string InvoiceRowsPart = "PART_InvoiceRows";
    public const string MoreInvoicesPart = "PART_MoreInvoices";
    public const string MoreRowsPart = "PART_MoreRows";
    public const string InvoicesEmptyPart = "PART_InvoicesEmpty";
    public const string EmptyTitlePart = "PART_EmptyTitle";
    public const string EmptyHintPart = "PART_EmptyHint";
    public const string InvoicesFooterPart = "PART_InvoicesFooter";
    public const string ShowAllPart = "PART_ShowAll";
    public const string ShowAllTextPart = "PART_ShowAllText";

    /// <summary>Class on the billing email button while its row is stacked, so the ghost label lines up with the text.</summary>
    internal const string StackedClass = "stacked";

    public const string NarrowPseudoClass = ":narrow";
    public const string CardExpiringPseudoClass = ":card-expiring";
    public const string CardExpiredPseudoClass = ":card-expired";
    public const string InvoicesExpandedPseudoClass = ":invoices-expanded";

    #region Properties

    /// <summary>The current subscription; null shows "No plan" with a "Choose a plan" button.</summary>
    public static readonly StyledProperty<BillingPlan?> PlanProperty =
        AvaloniaProperty.Register<BillingOverview, BillingPlan?>(nameof(Plan));

    /// <summary>The card on file; null shows "No payment method" with an "Add card" button.</summary>
    public static readonly StyledProperty<BillingPaymentMethod?> PaymentMethodProperty =
        AvaloniaProperty.Register<BillingOverview, BillingPaymentMethod?>(nameof(PaymentMethod));

    /// <summary>Where invoices are emailed, shown in the payment card.</summary>
    public static readonly StyledProperty<string?> BillingEmailProperty =
        AvaloniaProperty.Register<BillingOverview, string?>(nameof(BillingEmail));

    /// <summary>The amount of the next charge; null uses the plan's <see cref="BillingPlan.Price"/>.</summary>
    public static readonly StyledProperty<decimal?> NextInvoiceAmountProperty =
        AvaloniaProperty.Register<BillingOverview, decimal?>(nameof(NextInvoiceAmount));

    /// <summary>
    /// The invoice history, shown newest first. Defaults to an empty list you can add to; replace or bind it to show another
    /// collection. Changes to an observable collection and to the invoices themselves update the table.
    /// </summary>
    public static readonly DirectProperty<BillingOverview, IList<BillingInvoice>> InvoicesProperty =
        AvaloniaProperty.RegisterDirect<BillingOverview, IList<BillingInvoice>>(nameof(Invoices), o => o.Invoices,
            (o, v) => o.Invoices = v);

    /// <summary>How many invoices show before "Show all invoices"; the rest fold behind it.</summary>
    public static readonly StyledProperty<int> VisibleInvoiceCountProperty =
        AvaloniaProperty.Register<BillingOverview, int>(nameof(VisibleInvoiceCount), 4);

    /// <summary>Whether the invoices beyond <see cref="VisibleInvoiceCount"/> are shown ("Show all invoices" toggles it).</summary>
    public static readonly StyledProperty<bool> IsInvoiceListExpandedProperty =
        AvaloniaProperty.Register<BillingOverview, bool>(nameof(IsInvoiceListExpanded),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Below this width the price moves under the plan name, footers stack and the Invoice column hides.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<BillingOverview, double>(nameof(NarrowWidth), 560);

    /// <summary>The built-in text; replace the object to reword or localize the block.</summary>
    public static readonly StyledProperty<BillingOverviewLabels> LabelsProperty =
        AvaloniaProperty.Register<BillingOverview, BillingOverviewLabels>(nameof(Labels), new BillingOverviewLabels());

    /// <summary>Whether the block is narrower than <see cref="NarrowWidth"/>.</summary>
    public static readonly DirectProperty<BillingOverview, bool> IsNarrowProperty =
        AvaloniaProperty.RegisterDirect<BillingOverview, bool>(nameof(IsNarrow), o => o.IsNarrow);

    /// <summary>Whether the card's expiry month starts within 60 days of <see cref="Clock"/>'s today (and has not ended yet).</summary>
    public static readonly DirectProperty<BillingOverview, bool> IsPaymentMethodExpiringSoonProperty =
        AvaloniaProperty.RegisterDirect<BillingOverview, bool>(nameof(IsPaymentMethodExpiringSoon), o => o.IsPaymentMethodExpiringSoon);

    /// <summary>Whether the card's expiry month has passed.</summary>
    public static readonly DirectProperty<BillingOverview, bool> IsPaymentMethodExpiredProperty =
        AvaloniaProperty.RegisterDirect<BillingOverview, bool>(nameof(IsPaymentMethodExpired), o => o.IsPaymentMethodExpired);

    /// <inheritdoc cref="PlanProperty"/>
    public BillingPlan? Plan
    {
        get => GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    /// <inheritdoc cref="PaymentMethodProperty"/>
    public BillingPaymentMethod? PaymentMethod
    {
        get => GetValue(PaymentMethodProperty);
        set => SetValue(PaymentMethodProperty, value);
    }

    /// <inheritdoc cref="BillingEmailProperty"/>
    public string? BillingEmail
    {
        get => GetValue(BillingEmailProperty);
        set => SetValue(BillingEmailProperty, value);
    }

    /// <inheritdoc cref="NextInvoiceAmountProperty"/>
    public decimal? NextInvoiceAmount
    {
        get => GetValue(NextInvoiceAmountProperty);
        set => SetValue(NextInvoiceAmountProperty, value);
    }

    /// <inheritdoc cref="InvoicesProperty"/>
    public IList<BillingInvoice> Invoices
    {
        get => _invoices;
        set
        {
            value ??= new AvaloniaList<BillingInvoice>();
            var old = _invoices;
            if (SetAndRaise(InvoicesProperty, ref _invoices, value))
            {
                if (_isWatching && old is INotifyCollectionChanged oldObservable)
                {
                    oldObservable.CollectionChanged -= OnInvoicesCollectionChanged;
                }
                if (_isWatching && value is INotifyCollectionChanged observable)
                {
                    observable.CollectionChanged += OnInvoicesCollectionChanged;
                }
                RefreshInvoices();
            }
        }
    }

    /// <inheritdoc cref="VisibleInvoiceCountProperty"/>
    public int VisibleInvoiceCount
    {
        get => GetValue(VisibleInvoiceCountProperty);
        set => SetValue(VisibleInvoiceCountProperty, value);
    }

    /// <inheritdoc cref="IsInvoiceListExpandedProperty"/>
    public bool IsInvoiceListExpanded
    {
        get => GetValue(IsInvoiceListExpandedProperty);
        set => SetValue(IsInvoiceListExpandedProperty, value);
    }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public BillingOverviewLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="IsNarrowProperty"/>
    public bool IsNarrow
    {
        get => _isNarrow;
        private set
        {
            if (SetAndRaise(IsNarrowProperty, ref _isNarrow, value))
            {
                PseudoClasses.Set(NarrowPseudoClass, value);
                ApplyNarrowToRows();
            }
        }
    }

    /// <inheritdoc cref="IsPaymentMethodExpiringSoonProperty"/>
    public bool IsPaymentMethodExpiringSoon
    {
        get => _isExpiringSoon;
        private set
        {
            if (SetAndRaise(IsPaymentMethodExpiringSoonProperty, ref _isExpiringSoon, value))
            {
                PseudoClasses.Set(CardExpiringPseudoClass, value);
            }
        }
    }

    /// <inheritdoc cref="IsPaymentMethodExpiredProperty"/>
    public bool IsPaymentMethodExpired
    {
        get => _isExpired;
        private set
        {
            if (SetAndRaise(IsPaymentMethodExpiredProperty, ref _isExpired, value))
            {
                PseudoClasses.Set(CardExpiredPseudoClass, value);
            }
        }
    }

    /// <summary>
    /// Where "today" comes from when deciding whether the card expires soon (default <see cref="TimeProvider.System"/>).
    /// </summary>
    public TimeProvider Clock
    {
        get => _clock;
        set
        {
            _clock = value ?? TimeProvider.System;
            RefreshPayment();
        }
    }

    #endregion

    #region Events

    /// <summary>"Change plan" (or "Choose a plan" without a plan) was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> ChangePlanRequestedEvent =
        RoutedEvent.Register<BillingOverview, RoutedEventArgs>(nameof(ChangePlanRequested), RoutingStrategies.Bubble);

    /// <summary>"Cancel plan" was pressed. The block does not change the plan; the host confirms and updates <see cref="Plan"/>.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CancelPlanRequestedEvent =
        RoutedEvent.Register<BillingOverview, RoutedEventArgs>(nameof(CancelPlanRequested), RoutingStrategies.Bubble);

    /// <summary>"Update" (or "Add card" without a payment method) was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> UpdatePaymentMethodRequestedEvent =
        RoutedEvent.Register<BillingOverview, RoutedEventArgs>(nameof(UpdatePaymentMethodRequested), RoutingStrategies.Bubble);

    /// <summary>The billing email's "Edit" (or "Add") button was pressed.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> EditBillingEmailRequestedEvent =
        RoutedEvent.Register<BillingOverview, RoutedEventArgs>(nameof(EditBillingEmailRequested), RoutingStrategies.Bubble);

    /// <summary>An invoice's download button was pressed; <see cref="BillingInvoiceEventArgs.Invoice"/> is the invoice.</summary>
    public static readonly RoutedEvent<BillingInvoiceEventArgs> InvoiceDownloadRequestedEvent =
        RoutedEvent.Register<BillingOverview, BillingInvoiceEventArgs>(nameof(InvoiceDownloadRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ChangePlanRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? ChangePlanRequested
    {
        add => AddHandler(ChangePlanRequestedEvent, value);
        remove => RemoveHandler(ChangePlanRequestedEvent, value);
    }

    /// <inheritdoc cref="CancelPlanRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CancelPlanRequested
    {
        add => AddHandler(CancelPlanRequestedEvent, value);
        remove => RemoveHandler(CancelPlanRequestedEvent, value);
    }

    /// <inheritdoc cref="UpdatePaymentMethodRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? UpdatePaymentMethodRequested
    {
        add => AddHandler(UpdatePaymentMethodRequestedEvent, value);
        remove => RemoveHandler(UpdatePaymentMethodRequestedEvent, value);
    }

    /// <inheritdoc cref="EditBillingEmailRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? EditBillingEmailRequested
    {
        add => AddHandler(EditBillingEmailRequestedEvent, value);
        remove => RemoveHandler(EditBillingEmailRequestedEvent, value);
    }

    /// <inheritdoc cref="InvoiceDownloadRequestedEvent"/>
    public event EventHandler<BillingInvoiceEventArgs>? InvoiceDownloadRequested
    {
        add => AddHandler(InvoiceDownloadRequestedEvent, value);
        remove => RemoveHandler(InvoiceDownloadRequestedEvent, value);
    }

    #endregion

    private IList<BillingInvoice> _invoices;
    private TimeProvider _clock = TimeProvider.System;
    private bool _isNarrow;
    private bool _isExpiringSoon;
    private bool _isExpired;
    private bool _isWatching;
    private BillingPlan? _watchedPlan;
    private BillingPaymentMethod? _watchedCard;
    private readonly List<BillingInvoice> _watchedInvoices = new();

    // Rows are created once and reused: the pool only grows, and a refresh reassigns invoices to rows in order.
    private readonly List<BillingOverviewInvoiceRow> _rows = new();
    private readonly List<BillingInvoice> _ordered = new();
    private int _shownRows;

    private TextBlock? _planEyebrow;
    private TextBlock? _planName;
    private Badge? _planStatus;
    private TextBlock? _planDetail;
    private TextBlock? _price;
    private TextBlock? _nextInvoice;
    private Button? _cancelPlan;
    private Button? _changePlan;
    private SettingsGroup? _paymentGroup;
    private TextBlock? _cardBrand;
    private Control? _cardGlyph;
    private TextBlock? _cardTitle;
    private TextBlock? _cardExpiry;
    private Button? _updateCard;
    private SettingsRow? _emailRow;
    private Button? _editEmail;
    private SettingsGroup? _invoicesGroup;
    private Control? _invoiceTable;
    private BillingOverviewInvoiceRow? _invoiceHeader;
    private Panel? _invoiceRows;
    private SpringSizeDecorator? _moreInvoices;
    private Panel? _moreRows;
    private Control? _invoicesEmpty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyHint;
    private Control? _invoicesFooter;
    private Button? _showAll;
    private ArcContentSwap? _showAllText;
    private Run? _priceAmount;
    private Run? _pricePeriod;
    private Run? _nextBefore;
    private Run? _nextAmount;
    private Run? _nextAfter;
    private bool _watchingCollapse;

    public BillingOverview()
    {
        var invoices = new AvaloniaList<BillingInvoice>();
        _invoices = invoices;
    }

    static BillingOverview()
    {
        FocusableProperty.OverrideDefaultValue<BillingOverview>(false);
        AffectsMeasure<BillingOverview>(NarrowWidthProperty);
    }

    protected override Type StyleKeyOverride => typeof(BillingOverview);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _planEyebrow = scope.Find<TextBlock>(PlanEyebrowPart);
        _planName = scope.Find<TextBlock>(PlanNamePart);
        _planStatus = scope.Find<Badge>(PlanStatusPart);
        _planDetail = scope.Find<TextBlock>(PlanDetailPart);
        _price = scope.Find<TextBlock>(PricePart);
        _nextInvoice = scope.Find<TextBlock>(NextInvoicePart);
        _cancelPlan = scope.Find<Button>(CancelPlanPart);
        _changePlan = scope.Find<Button>(ChangePlanPart);
        _paymentGroup = scope.Find<SettingsGroup>(PaymentGroupPart);
        _cardBrand = scope.Find<TextBlock>(CardBrandPart);
        _cardGlyph = scope.Find<Control>(CardGlyphPart);
        _cardTitle = scope.Find<TextBlock>(CardTitlePart);
        _cardExpiry = scope.Find<TextBlock>(CardExpiryPart);
        _updateCard = scope.Find<Button>(UpdateCardPart);
        _emailRow = scope.Find<SettingsRow>(EmailRowPart);
        _editEmail = scope.Find<Button>(EditEmailPart);
        _invoicesGroup = scope.Find<SettingsGroup>(InvoicesGroupPart);
        _invoiceTable = scope.Find<Control>(InvoiceTablePart);
        _invoiceHeader = scope.Find<BillingOverviewInvoiceRow>(InvoiceHeaderPart);
        _invoiceRows = scope.Find<Panel>(InvoiceRowsPart);
        _moreInvoices = scope.Find<SpringSizeDecorator>(MoreInvoicesPart);
        _moreRows = scope.Find<Panel>(MoreRowsPart);
        _invoicesEmpty = scope.Find<Control>(InvoicesEmptyPart);
        _emptyTitle = scope.Find<TextBlock>(EmptyTitlePart);
        _emptyHint = scope.Find<TextBlock>(EmptyHintPart);
        _invoicesFooter = scope.Find<Control>(InvoicesFooterPart);
        _showAll = scope.Find<Button>(ShowAllPart);
        _showAllText = scope.Find<ArcContentSwap>(ShowAllTextPart);
        HookParts();
        BuildInlines();
        _shownRows = -1;
        if (_moreRows is not null && _moreInvoices is not null)
        {
            _moreRows.IsVisible = IsInvoiceListExpanded;
            _moreInvoices.IsExpanded = IsInvoiceListExpanded;
        }
        RefreshAll();
    }

    private void HookParts()
    {
        Hook(_changePlan, OnChangePlanClick);
        Hook(_cancelPlan, OnCancelPlanClick);
        Hook(_updateCard, OnUpdateCardClick);
        Hook(_editEmail, OnEditEmailClick);
        Hook(_showAll, OnShowAllClick);
        if (_emailRow is not null)
        {
            _emailRow.PropertyChanged += OnEmailRowPropertyChanged;
        }
    }

    private void UnhookParts()
    {
        Unhook(_changePlan, OnChangePlanClick);
        Unhook(_cancelPlan, OnCancelPlanClick);
        Unhook(_updateCard, OnUpdateCardClick);
        Unhook(_editEmail, OnEditEmailClick);
        Unhook(_showAll, OnShowAllClick);
        if (_emailRow is not null)
        {
            _emailRow.PropertyChanged -= OnEmailRowPropertyChanged;
        }
        StopWatchingCollapse();
        // Rows belong to the panels of the old template; take them out so the new template can host them.
        _invoiceRows?.Children.Clear();
        _moreRows?.Children.Clear();
        if (_price?.Inlines is { } priceInlines)
        {
            priceInlines.Clear();
        }
        if (_nextInvoice?.Inlines is { } nextInlines)
        {
            nextInlines.Clear();
        }
    }

    private void OnEmailRowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SettingsRow.IsStackedProperty)
        {
            _editEmail?.Classes.Set(StackedClass, _emailRow?.IsStacked == true);
        }
    }

    private static void Hook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(Button? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    /// <summary>The price ("$90 per month") and the next charge ("Next invoice $90.00 on …") are runs, so they share a baseline.</summary>
    private void BuildInlines()
    {
        if (_price is not null)
        {
            _priceAmount = new Run();
            _pricePeriod = new Run();
            ArcTokens.Bind(_priceAmount, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXL);
            ArcTokens.Bind(_priceAmount, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            ArcTokens.Bind(_priceAmount, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            ArcTokens.Bind(_pricePeriod, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            ArcTokens.Bind(_pricePeriod, TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            _price.Inlines ??= new InlineCollection();
            _price.Inlines.Clear();
            _price.Inlines.Add(_priceAmount);
            _price.Inlines.Add(_pricePeriod);
        }
        if (_nextInvoice is not null)
        {
            _nextBefore = new Run();
            _nextAmount = new Run();
            _nextAfter = new Run();
            ArcTokens.Bind(_nextAmount, TextElement.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            ArcTokens.Bind(_nextAmount, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            _nextInvoice.Inlines ??= new InlineCollection();
            _nextInvoice.Inlines.Clear();
            _nextInvoice.Inlines.Add(_nextBefore);
            _nextInvoice.Inlines.Add(_nextAmount);
            _nextInvoice.Inlines.Add(_nextAfter);
        }
    }

    #endregion

    #region Lifetime and model changes

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Model events are watched only while shown, so a long-lived model never keeps a detached block alive.
        _isWatching = true;
        if (_invoices is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += OnInvoicesCollectionChanged;
        }
        WatchPlan(Plan);
        WatchCard(PaymentMethod);
        RefreshAll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isWatching = false;
        if (_invoices is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged -= OnInvoicesCollectionChanged;
        }
        WatchPlan(null);
        WatchCard(null);
        WatchInvoices(Array.Empty<BillingInvoice>());
        StopWatchingCollapse();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlanProperty)
        {
            if (_isWatching)
            {
                WatchPlan(Plan);
            }
            RefreshPlan();
            RefreshInvoices();
        }
        else if (change.Property == PaymentMethodProperty)
        {
            if (_isWatching)
            {
                WatchCard(PaymentMethod);
            }
            RefreshPayment();
        }
        else if (change.Property == NextInvoiceAmountProperty)
        {
            RefreshPlan();
        }
        else if (change.Property == BillingEmailProperty)
        {
            RefreshEmail();
        }
        else if (change.Property == VisibleInvoiceCountProperty)
        {
            RefreshInvoices();
        }
        else if (change.Property == IsInvoiceListExpandedProperty)
        {
            PseudoClasses.Set(InvoicesExpandedPseudoClass, IsInvoiceListExpanded);
            ApplyExpansion();
        }
        else if (change.Property == LabelsProperty)
        {
            RefreshAll();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width comes from the parent, not from the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            IsNarrow = availableSize.Width < NarrowWidth;
        }
        return base.MeasureOverride(availableSize);
    }

    private void WatchPlan(BillingPlan? plan)
    {
        if (ReferenceEquals(_watchedPlan, plan))
        {
            return;
        }
        if (_watchedPlan is not null)
        {
            _watchedPlan.PropertyChanged -= OnPlanPropertyChanged;
        }
        _watchedPlan = plan;
        if (plan is not null)
        {
            plan.PropertyChanged += OnPlanPropertyChanged;
        }
    }

    private void WatchCard(BillingPaymentMethod? card)
    {
        if (ReferenceEquals(_watchedCard, card))
        {
            return;
        }
        if (_watchedCard is not null)
        {
            _watchedCard.PropertyChanged -= OnCardPropertyChanged;
        }
        _watchedCard = card;
        if (card is not null)
        {
            card.PropertyChanged += OnCardPropertyChanged;
        }
    }

    private void WatchInvoices(IEnumerable<BillingInvoice> invoices)
    {
        foreach (var invoice in _watchedInvoices)
        {
            invoice.PropertyChanged -= OnInvoicePropertyChanged;
        }
        _watchedInvoices.Clear();
        foreach (var invoice in invoices)
        {
            invoice.PropertyChanged += OnInvoicePropertyChanged;
            _watchedInvoices.Add(invoice);
        }
    }

    private void OnPlanPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshPlan();
        if (e.PropertyName == nameof(BillingPlan.Currency))
        {
            RefreshInvoices();
        }
    }

    private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshPayment();

    private void OnInvoicePropertyChanged(object? sender, PropertyChangedEventArgs e) => RefreshInvoices();

    private void OnInvoicesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshInvoices();

    #endregion

    #region Refresh

    private void RefreshAll()
    {
        RefreshPlan();
        RefreshPayment();
        RefreshEmail();
        RefreshInvoices();
    }

    private CultureInfo Culture => Labels.Culture ?? SettingsFormat.DefaultCulture;

    /// <summary>The badge tone of a plan status: Active success, Trialing accent, PastDue danger, Canceled neutral.</summary>
    internal static BadgeTone ToneOf(BillingPlanStatus status) => status switch
    {
        BillingPlanStatus.Active => BadgeTone.Success,
        BillingPlanStatus.Trialing => BadgeTone.Info,
        BillingPlanStatus.PastDue => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    /// <summary>The badge tone of an invoice status: Paid success, PastDue danger, the rest neutral.</summary>
    internal static BadgeTone ToneOf(BillingInvoiceStatus status) => status switch
    {
        BillingInvoiceStatus.Paid => BadgeTone.Success,
        BillingInvoiceStatus.PastDue => BadgeTone.Danger,
        _ => BadgeTone.Neutral
    };

    private void RefreshPlan()
    {
        var labels = Labels;
        var culture = Culture;
        var plan = Plan;
        if (_planEyebrow is not null)
        {
            _planEyebrow.Text = labels.CurrentPlan;
        }
        if (plan is null)
        {
            SetText(_planName, labels.NoPlan);
            if (_planStatus is not null)
            {
                _planStatus.IsVisible = false;
            }
            SetText(_planDetail, labels.NoPlanDescription);
            if (_price is not null)
            {
                _price.IsVisible = false;
            }
            if (_nextInvoice is not null)
            {
                _nextInvoice.IsVisible = false;
            }
            SetButton(_changePlan, labels.ChoosePlan, null);
            if (_cancelPlan is not null)
            {
                _cancelPlan.IsVisible = false;
            }
            return;
        }

        SetText(_planName, string.IsNullOrWhiteSpace(plan.Name) ? labels.NoPlan : plan.Name);
        if (_planStatus is not null)
        {
            _planStatus.IsVisible = true;
            _planStatus.Tone = ToneOf(plan.Status);
            _planStatus.Content = plan.Status switch
            {
                BillingPlanStatus.Trialing => labels.Trialing,
                BillingPlanStatus.PastDue => labels.PastDue,
                BillingPlanStatus.Canceled => labels.Canceled,
                _ => labels.Active
            };
        }

        var date = plan.RenewsOn is { } renews ? SettingsFormat.Date(renews, culture) : null;
        var parts = new List<string>(2);
        if (plan.Seats is { } seats)
        {
            parts.Add(labels.Seats(seats));
        }
        if (date is not null)
        {
            parts.Add(plan.Status switch
            {
                BillingPlanStatus.Trialing => labels.TrialEndsOn(date),
                BillingPlanStatus.PastDue => labels.OverdueSince(date),
                BillingPlanStatus.Canceled => labels.EndsOn(date),
                _ => labels.RenewsOn(date)
            });
        }
        SetText(_planDetail, parts.Count == 0 ? null : string.Join(labels.Separator, parts));

        if (_price is not null)
        {
            _price.IsVisible = true;
            if (_priceAmount is not null && _pricePeriod is not null)
            {
                _priceAmount.Text = SettingsFormat.MoneyShort(plan.Price, plan.Currency, culture);
                _pricePeriod.Text = " " + (plan.Interval == BillingInterval.Year ? labels.PerYear : labels.PerMonth);
            }
        }

        var amount = SettingsFormat.Money(NextInvoiceAmount ?? plan.Price, plan.Currency, culture);
        string footer;
        if (plan.Status == BillingPlanStatus.Canceled)
        {
            footer = labels.NoUpcomingInvoice;
        }
        else if (plan.Status == BillingPlanStatus.PastDue)
        {
            footer = labels.Overdue(amount);
        }
        else
        {
            footer = date is null ? labels.NextInvoiceAmount(amount) : labels.NextInvoice(amount, date);
        }
        if (_nextInvoice is not null)
        {
            _nextInvoice.IsVisible = true;
            SetEmphasized(footer, plan.Status == BillingPlanStatus.Canceled ? null : amount);
            AutomationProperties.SetName(_nextInvoice, footer);
        }
        SetButton(_changePlan, labels.ChangePlan, null);
        SetButton(_cancelPlan, labels.CancelPlan, null);
        if (_cancelPlan is not null)
        {
            _cancelPlan.IsVisible = plan.Status != BillingPlanStatus.Canceled;
        }
    }

    /// <summary>Writes <paramref name="text"/> into the footer runs with <paramref name="emphasis"/> (the amount) in the foreground color.</summary>
    private void SetEmphasized(string text, string? emphasis)
    {
        if (_nextBefore is null || _nextAmount is null || _nextAfter is null)
        {
            return;
        }
        var index = string.IsNullOrEmpty(emphasis) ? -1 : text.IndexOf(emphasis, StringComparison.Ordinal);
        if (index < 0)
        {
            _nextBefore.Text = text;
            _nextAmount.Text = string.Empty;
            _nextAfter.Text = string.Empty;
            return;
        }
        _nextBefore.Text = text.Substring(0, index);
        _nextAmount.Text = emphasis;
        _nextAfter.Text = text.Substring(index + emphasis!.Length);
    }

    private void RefreshPayment()
    {
        var labels = Labels;
        var card = PaymentMethod;
        if (_paymentGroup is not null)
        {
            _paymentGroup.Header = labels.Payment;
        }
        if (card is null)
        {
            IsPaymentMethodExpiringSoon = false;
            IsPaymentMethodExpired = false;
            if (_cardBrand is not null)
            {
                _cardBrand.IsVisible = false;
            }
            if (_cardGlyph is not null)
            {
                _cardGlyph.IsVisible = true;
            }
            SetText(_cardTitle, labels.NoPaymentMethod);
            SetText(_cardExpiry, labels.NoPaymentMethodDescription);
            SetButton(_updateCard, labels.AddCard, labels.AddCard);
            return;
        }

        var (soon, expired) = ExpiryState(card, _clock);
        IsPaymentMethodExpiringSoon = soon;
        IsPaymentMethodExpired = expired;
        var brand = string.IsNullOrWhiteSpace(card.Brand) ? "Card" : card.Brand.Trim();
        if (_cardBrand is not null)
        {
            _cardBrand.IsVisible = true;
            _cardBrand.Text = MarkOf(brand);
        }
        if (_cardGlyph is not null)
        {
            _cardGlyph.IsVisible = false;
        }
        SetText(_cardTitle, labels.CardEnding(brand, card.Last4));
        var month = Math.Clamp(card.ExpiryMonth, 1, 12).ToString("00", CultureInfo.InvariantCulture);
        var expiry = $"{month}/{card.ExpiryYear.ToString(CultureInfo.InvariantCulture)}";
        SetText(_cardExpiry, expired ? labels.Expired(expiry) : soon ? labels.ExpiresSoon(expiry) : labels.Expires(expiry));
        SetButton(_updateCard, labels.UpdateCard, labels.UpdateCardName);
    }

    /// <summary>
    /// Whether <paramref name="card"/>'s expiry month starts within 60 days of today (expiring soon), or has already ended
    /// (expired). A card is valid through the last day of its expiry month.
    /// </summary>
    internal static (bool Soon, bool Expired) ExpiryState(BillingPaymentMethod card, TimeProvider clock)
    {
        if (card.ExpiryMonth is < 1 or > 12 || card.ExpiryYear is < 1 or > 9998)
        {
            return (false, false);
        }
        var today = clock.GetLocalNow().Date;
        var month = new DateTime(card.ExpiryYear, card.ExpiryMonth, 1);
        if (today >= month.AddMonths(1))
        {
            return (false, true);
        }
        return ((month - today).TotalDays <= 60, false);
    }

    /// <summary>The short text mark for a card brand ("Visa" ⇒ VISA, "Mastercard" ⇒ MC); text only, no logos.</summary>
    internal static string MarkOf(string brand)
    {
        var key = brand.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return key switch
        {
            "MASTERCARD" => "MC",
            "AMERICANEXPRESS" or "AMEX" => "AMEX",
            "DINERSCLUB" or "DINERS" => "DC",
            "UNIONPAY" => "UPI",
            _ => key.Length <= 4 ? key : key.Substring(0, 4)
        };
    }

    private void RefreshEmail()
    {
        var labels = Labels;
        var email = BillingEmail;
        if (_emailRow is not null)
        {
            _emailRow.Header = labels.BillingEmail;
            _emailRow.Description = string.IsNullOrWhiteSpace(email) ? labels.NoBillingEmail : email;
        }
        SetButton(_editEmail, string.IsNullOrWhiteSpace(email) ? labels.AddEmail : labels.EditEmail, labels.EditEmailName);
    }

    private void RefreshInvoices()
    {
        var labels = Labels;
        var culture = Culture;
        _ordered.Clear();
        foreach (var invoice in _invoices)
        {
            if (invoice is not null)
            {
                _ordered.Add(invoice);
            }
        }
        if (_isWatching)
        {
            WatchInvoices(_ordered);
        }
        // Newest first; invoices on the same day keep their order in the list (a stable sort).
        var sorted = _ordered.OrderByDescending(i => i.Date).ToList();

        if (_invoicesGroup is not null)
        {
            _invoicesGroup.Header = labels.Invoices;
        }
        SetText(_emptyTitle, labels.NoInvoices);
        SetText(_emptyHint, labels.NoInvoicesHint);
        if (_invoiceHeader is not null)
        {
            _invoiceHeader.DateText = labels.DateColumn;
            _invoiceHeader.NumberText = labels.NumberColumn;
            _invoiceHeader.AmountText = labels.AmountColumn;
            _invoiceHeader.StatusText = labels.StatusColumn;
            _invoiceHeader.IsNarrow = IsNarrow;
        }
        var hasInvoices = sorted.Count > 0;
        if (_invoiceTable is not null)
        {
            _invoiceTable.IsVisible = hasInvoices;
        }
        if (_invoicesEmpty is not null)
        {
            _invoicesEmpty.IsVisible = !hasInvoices;
        }

        while (_rows.Count < sorted.Count)
        {
            _rows.Add(CreateRow());
        }
        var planCurrency = Plan?.Currency;
        for (var i = 0; i < sorted.Count; i++)
        {
            var invoice = sorted[i];
            var row = _rows[i];
            row.Invoice = invoice;
            row.IsNarrow = IsNarrow;
            row.DateText = SettingsFormat.Date(invoice.Date, culture);
            row.NumberText = invoice.Number;
            row.AmountText = SettingsFormat.Money(invoice.Amount, invoice.Currency ?? planCurrency, culture);
            row.StatusText = invoice.Status switch
            {
                BillingInvoiceStatus.Open => labels.Open,
                BillingInvoiceStatus.PastDue => labels.InvoicePastDue,
                BillingInvoiceStatus.Refunded => labels.Refunded,
                BillingInvoiceStatus.Void => labels.Void,
                _ => labels.Paid
            };
            row.StatusTone = ToneOf(invoice.Status);
            row.IsMuted = invoice.Status is BillingInvoiceStatus.Refunded or BillingInvoiceStatus.Void;
            row.DownloadName = labels.Download(invoice.Number);
        }
        for (var i = sorted.Count; i < _rows.Count; i++)
        {
            _rows[i].Invoice = null;
        }
        PlaceRows(sorted.Count);

        var visible = Math.Max(0, VisibleInvoiceCount);
        var hasMore = sorted.Count > visible;
        if (_invoicesGroup is not null)
        {
            _invoicesGroup.Footer = hasMore ? _invoicesFooter : null;
        }
        UpdateShowAllText(sorted.Count);
    }

    /// <summary>Puts the first <see cref="VisibleInvoiceCount"/> rows in the table and the rest in the folding part.</summary>
    private void PlaceRows(int count)
    {
        if (_invoiceRows is null || _moreRows is null)
        {
            return;
        }
        var visible = Math.Min(count, Math.Max(0, VisibleInvoiceCount));
        if (_shownRows == count && _invoiceRows.Children.Count == visible)
        {
            return;
        }
        _shownRows = count;
        _invoiceRows.Children.Clear();
        _moreRows.Children.Clear();
        for (var i = 0; i < count; i++)
        {
            (i < visible ? _invoiceRows : _moreRows).Children.Add(_rows[i]);
        }
    }

    private BillingOverviewInvoiceRow CreateRow()
    {
        var row = new BillingOverviewInvoiceRow();
        row.DownloadRequested += OnRowDownloadRequested;
        return row;
    }

    private void UpdateShowAllText(int count)
    {
        var labels = Labels;
        var text = IsInvoiceListExpanded ? labels.ShowFewer : labels.ShowAll(count);
        if (_showAllText is not null)
        {
            _showAllText.Content = text;
        }
        if (_showAll is not null)
        {
            AutomationProperties.SetName(_showAll, text);
        }
    }

    private void ApplyNarrowToRows()
    {
        if (_invoiceHeader is not null)
        {
            _invoiceHeader.IsNarrow = _isNarrow;
        }
        foreach (var row in _rows)
        {
            row.IsNarrow = _isNarrow;
        }
    }

    #endregion

    #region Folding invoices

    private void ApplyExpansion()
    {
        UpdateShowAllText(_ordered.Count);
        if (_moreInvoices is null || _moreRows is null)
        {
            return;
        }
        if (IsInvoiceListExpanded)
        {
            StopWatchingCollapse();
            _moreRows.IsVisible = true;
            _moreInvoices.IsExpanded = true;
        }
        else
        {
            _moreInvoices.IsExpanded = false;
            // The folded rows stay visible while the height springs shut, then leave the tab order.
            _watchingCollapse = true;
            _moreInvoices.LayoutUpdated += OnMoreInvoicesLayoutUpdated;
        }
    }

    private void OnMoreInvoicesLayoutUpdated(object? sender, EventArgs e)
    {
        if (_moreInvoices is null || _moreRows is null)
        {
            StopWatchingCollapse();
            return;
        }
        if (!IsInvoiceListExpanded && _moreInvoices.Bounds.Height < 0.5)
        {
            _moreRows.IsVisible = false;
            StopWatchingCollapse();
        }
    }

    private void StopWatchingCollapse()
    {
        if (!_watchingCollapse)
        {
            return;
        }
        _watchingCollapse = false;
        if (_moreInvoices is not null)
        {
            _moreInvoices.LayoutUpdated -= OnMoreInvoicesLayoutUpdated;
        }
    }

    #endregion

    #region Actions

    private void OnChangePlanClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ChangePlanRequestedEvent, this));
    }

    private void OnCancelPlanClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(CancelPlanRequestedEvent, this));
    }

    private void OnUpdateCardClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(UpdatePaymentMethodRequestedEvent, this));
    }

    private void OnEditEmailClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(EditBillingEmailRequestedEvent, this));
    }

    private void OnShowAllClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(IsInvoiceListExpandedProperty, !IsInvoiceListExpanded);
    }

    private void OnRowDownloadRequested(object? sender, EventArgs e)
    {
        if (sender is BillingOverviewInvoiceRow { Invoice: { } invoice })
        {
            RaiseEvent(new BillingInvoiceEventArgs(InvoiceDownloadRequestedEvent, invoice) { Source = this });
        }
    }

    #endregion

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
            block.IsVisible = !string.IsNullOrEmpty(text);
        }
    }

    private static void SetButton(Button? button, string text, string? name)
    {
        if (button is null)
        {
            return;
        }
        if (!Equals(button.Content, text))
        {
            button.Content = text;
        }
        if (name is null)
        {
            button.ClearValue(AutomationProperties.NameProperty);
        }
        else
        {
            AutomationProperties.SetName(button, name);
        }
    }
}
