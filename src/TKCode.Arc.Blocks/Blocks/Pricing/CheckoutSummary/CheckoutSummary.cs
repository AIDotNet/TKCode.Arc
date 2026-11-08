using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// The last step before paying for one product: a title, the item with its price, a promo code field that turns an
/// accepted code into a removable chip, a breakdown (subtotal, automatic lines, the code, tax) whose lines slide in and
/// out, a rolling total and a pay button that walks through processing to paid (or to an error with "Try again"). The
/// code check (<see cref="CouponValidator"/>) and the payment (<see cref="PayAsync"/>) are delegates so a host can call
/// its backend; <see cref="CheckoutSummarySamples"/> has simulated ones.
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(ItemPart, typeof(Control))]
[TemplatePart(CouponPart, typeof(CheckoutSummaryCouponField))]
[TemplatePart(LinesPart, typeof(CheckoutSummaryLines))]
[TemplatePart(PayPart, typeof(CheckoutSummaryPayButton))]
[TemplatePart(PayErrorHostPart, typeof(CheckoutSummaryCollapse))]
[TemplatePart(PayErrorPart, typeof(TextBlock))]
[PseudoClasses(ProcessingPseudoClass, PaidPseudoClass, PayErrorPseudoClass, CouponAppliedPseudoClass, HasMediaPseudoClass)]
public class CheckoutSummary : TemplatedControl
{
    /// <summary>Template part: the title.</summary>
    public const string TitlePart = "PART_Title";

    /// <summary>Template part: the item row.</summary>
    public const string ItemPart = "PART_Item";

    /// <summary>Template part: the promo code field.</summary>
    public const string CouponPart = "PART_Coupon";

    /// <summary>Template part: the breakdown and total.</summary>
    public const string LinesPart = "PART_Lines";

    /// <summary>Template part: the pay button.</summary>
    public const string PayPart = "PART_Pay";

    /// <summary>Template part: the collapsing host of the payment error line.</summary>
    public const string PayErrorHostPart = "PART_PayErrorHost";

    /// <summary>Template part: the payment error line.</summary>
    public const string PayErrorPart = "PART_PayError";

    /// <summary>Pseudo-class while the payment runs.</summary>
    public const string ProcessingPseudoClass = ":processing";

    /// <summary>Pseudo-class once paid.</summary>
    public const string PaidPseudoClass = ":paid";

    /// <summary>Pseudo-class after a failed payment.</summary>
    public const string PayErrorPseudoClass = ":pay-error";

    /// <summary>Pseudo-class while a promo code is applied.</summary>
    public const string CouponAppliedPseudoClass = ":coupon-applied";

    /// <summary>Pseudo-class when the item has media.</summary>
    public const string HasMediaPseudoClass = ":has-media";

    /// <summary>Key of the promo code line in the breakdown.</summary>
    public const string CouponLineId = "coupon";

    /// <summary>The product being paid for; null hides the item row.</summary>
    public static readonly StyledProperty<CheckoutSummaryItem?> ItemProperty =
        AvaloniaProperty.Register<CheckoutSummary, CheckoutSummaryItem?>(nameof(Item));

    /// <summary>The tax line; null shows no tax.</summary>
    public static readonly StyledProperty<CheckoutSummaryTax?> TaxProperty =
        AvaloniaProperty.Register<CheckoutSummary, CheckoutSummaryTax?>(nameof(Tax));

    /// <summary>ISO 4217 code of the amounts (default USD).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<CheckoutSummary, string>(nameof(Currency), "USD");

    /// <summary>Culture used to format amounts (default en-US).</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<CheckoutSummary, string>(nameof(CultureName), "en-US");

    /// <summary>Heading of the card, also its accessible name (default "Order summary").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CheckoutSummary, string?>(nameof(Title), "Order summary");

    /// <summary>Small line under the pay button next to a lock (default "Encrypted payment").</summary>
    public static readonly StyledProperty<string?> SecureNoteProperty =
        AvaloniaProperty.Register<CheckoutSummary, string?>(nameof(SecureNote), "Encrypted payment");

    /// <summary>False hides the secure note.</summary>
    public static readonly StyledProperty<bool> IsSecureNoteVisibleProperty =
        AvaloniaProperty.Register<CheckoutSummary, bool>(nameof(IsSecureNoteVisible), true);

    /// <summary>Optional small print at the bottom of the card (text or any control).</summary>
    public static readonly StyledProperty<object?> FootnoteProperty =
        AvaloniaProperty.Register<CheckoutSummary, object?>(nameof(Footnote));

    /// <summary>False hides the promo code field. It also stays hidden while <see cref="CouponValidator"/> is null.</summary>
    public static readonly StyledProperty<bool> IsCouponVisibleProperty =
        AvaloniaProperty.Register<CheckoutSummary, bool>(nameof(IsCouponVisible), true);

    /// <summary>True turns typed letters into capitals as they are typed (default true).</summary>
    public static readonly StyledProperty<bool> UppercaseCouponProperty =
        AvaloniaProperty.Register<CheckoutSummary, bool>(nameof(UppercaseCoupon), true);

    /// <summary>Where the pay button is: idle, processing, paid or error. Two-way; a host may drive it directly.</summary>
    public static readonly StyledProperty<CheckoutSummaryPayState> PayStateProperty =
        AvaloniaProperty.Register<CheckoutSummary, CheckoutSummaryPayState>(nameof(PayState), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Text of the error line after a failed payment; the labels' generic message when null.</summary>
    public static readonly StyledProperty<string?> PayErrorMessageProperty =
        AvaloniaProperty.Register<CheckoutSummary, string?>(nameof(PayErrorMessage));

    /// <summary>False shows the pay button but does not allow paying.</summary>
    public static readonly StyledProperty<bool> IsPayEnabledProperty =
        AvaloniaProperty.Register<CheckoutSummary, bool>(nameof(IsPayEnabled), true);

    /// <summary>The block's built-in text; replace the whole object to localize it.</summary>
    public static readonly StyledProperty<CheckoutSummaryLabels> LabelsProperty =
        AvaloniaProperty.Register<CheckoutSummary, CheckoutSummaryLabels>(nameof(Labels), new CheckoutSummaryLabels());

    /// <summary>The promo code in use, or null.</summary>
    public static readonly DirectProperty<CheckoutSummary, CheckoutSummaryCouponResult?> AppliedCouponProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, CheckoutSummaryCouponResult?>(nameof(AppliedCoupon), o => o.AppliedCoupon);

    /// <summary>The item price.</summary>
    public static readonly DirectProperty<CheckoutSummary, decimal> SubtotalProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, decimal>(nameof(Subtotal), o => o.Subtotal);

    /// <summary>What the promo code takes off (positive), zero without a code.</summary>
    public static readonly DirectProperty<CheckoutSummary, decimal> CouponDiscountProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, decimal>(nameof(CouponDiscount), o => o.CouponDiscount);

    /// <summary>The tax amount (zero without a rate).</summary>
    public static readonly DirectProperty<CheckoutSummary, decimal> TaxAmountProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, decimal>(nameof(TaxAmount), o => o.TaxAmount);

    /// <summary>The amount to pay.</summary>
    public static readonly DirectProperty<CheckoutSummary, decimal> TotalProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, decimal>(nameof(Total), o => o.Total);

    /// <summary>True while paying and after: the promo code can no longer change.</summary>
    public static readonly DirectProperty<CheckoutSummary, bool> IsCouponLockedProperty =
        AvaloniaProperty.RegisterDirect<CheckoutSummary, bool>(nameof(IsCouponLocked), o => o.IsCouponLocked);

    /// <summary>Raised when a promo code was accepted.</summary>
    public static readonly RoutedEvent<CheckoutSummaryCouponEventArgs> CouponAppliedEvent =
        RoutedEvent.Register<CheckoutSummary, CheckoutSummaryCouponEventArgs>(nameof(CouponApplied), RoutingStrategies.Bubble);

    /// <summary>Raised when a promo code was refused.</summary>
    public static readonly RoutedEvent<CheckoutSummaryCouponEventArgs> CouponRejectedEvent =
        RoutedEvent.Register<CheckoutSummary, CheckoutSummaryCouponEventArgs>(nameof(CouponRejected), RoutingStrategies.Bubble);

    /// <summary>Raised when the applied promo code was removed.</summary>
    public static readonly RoutedEvent<CheckoutSummaryCouponEventArgs> CouponRemovedEvent =
        RoutedEvent.Register<CheckoutSummary, CheckoutSummaryCouponEventArgs>(nameof(CouponRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when Pay is pressed, before the payment starts; set <see cref="CheckoutSummaryPayRequestedEventArgs.Cancel"/> to stop it.</summary>
    public static readonly RoutedEvent<CheckoutSummaryPayRequestedEventArgs> PayRequestedEvent =
        RoutedEvent.Register<CheckoutSummary, CheckoutSummaryPayRequestedEventArgs>(nameof(PayRequested), RoutingStrategies.Bubble);

    /// <summary>Raised whenever <see cref="PayState"/> changes.</summary>
    public static readonly RoutedEvent<CheckoutSummaryPayStateChangedEventArgs> PayStateChangedEvent =
        RoutedEvent.Register<CheckoutSummary, CheckoutSummaryPayStateChangedEventArgs>(nameof(PayStateChanged), RoutingStrategies.Bubble);

    private readonly AvaloniaList<CheckoutSummaryLine> _adjustments = new();
    private readonly List<CheckoutSummaryLine> _watchedLines = new();
    private CheckoutSummaryItem? _watchedItem;
    private CheckoutSummaryTax? _watchedTax;
    private Control? _item;
    private TextBlock? _itemName;
    private TextBlock? _itemDescription;
    private TextBlock? _itemPrice;
    private TextBlock? _itemPeriod;
    private ContentControl? _media;
    private CheckoutSummaryCouponField? _coupon;
    private CheckoutSummaryLines? _lines;
    private CheckoutSummaryPayButton? _pay;
    private CheckoutSummaryCollapse? _payErrorHost;
    private TextBlock? _payError;
    private Func<string, CancellationToken, Task<CheckoutSummaryCouponResult>>? _couponValidator;
    private CancellationTokenSource? _payment;
    private Exception? _payException;
    private CheckoutSummaryCouponResult? _appliedCoupon;
    private decimal _subtotal;
    private decimal _couponDiscount;
    private decimal _taxAmount;
    private decimal _total;
    private bool _isCouponLocked;
    private string _formattedTotal = string.Empty;

    /// <summary>Creates the block.</summary>
    public CheckoutSummary()
    {
        _adjustments.CollectionChanged += OnAdjustmentsChanged;
    }

    /// <inheritdoc cref="ItemProperty"/>
    public CheckoutSummaryItem? Item { get => GetValue(ItemProperty); set => SetValue(ItemProperty, value); }

    /// <summary>Automatic lines between the subtotal and the tax (negative amounts are discounts); lines that come or go slide in and out.</summary>
    [Content]
    public AvaloniaList<CheckoutSummaryLine> Adjustments => _adjustments;

    /// <inheritdoc cref="TaxProperty"/>
    public CheckoutSummaryTax? Tax { get => GetValue(TaxProperty); set => SetValue(TaxProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SecureNoteProperty"/>
    public string? SecureNote { get => GetValue(SecureNoteProperty); set => SetValue(SecureNoteProperty, value); }

    /// <inheritdoc cref="IsSecureNoteVisibleProperty"/>
    public bool IsSecureNoteVisible { get => GetValue(IsSecureNoteVisibleProperty); set => SetValue(IsSecureNoteVisibleProperty, value); }

    /// <inheritdoc cref="FootnoteProperty"/>
    public object? Footnote { get => GetValue(FootnoteProperty); set => SetValue(FootnoteProperty, value); }

    /// <inheritdoc cref="IsCouponVisibleProperty"/>
    public bool IsCouponVisible { get => GetValue(IsCouponVisibleProperty); set => SetValue(IsCouponVisibleProperty, value); }

    /// <inheritdoc cref="UppercaseCouponProperty"/>
    public bool UppercaseCoupon { get => GetValue(UppercaseCouponProperty); set => SetValue(UppercaseCouponProperty, value); }

    /// <summary>
    /// Checks a promo code (trimmed, as typed) and answers with <see cref="CheckoutSummaryCouponResult.Valid"/>,
    /// <see cref="CheckoutSummaryCouponResult.ValidPercent"/> or <see cref="CheckoutSummaryCouponResult.Invalid"/>; an
    /// exception counts as a refusal. Null hides the promo code field.
    /// </summary>
    public Func<string, CancellationToken, Task<CheckoutSummaryCouponResult>>? CouponValidator
    {
        get => _couponValidator;
        set
        {
            _couponValidator = value;
            if (_coupon is not null)
            {
                _coupon.Validator = value;
            }
            UpdateCouponVisibility();
        }
    }

    /// <summary>
    /// Runs the payment: completing moves to <see cref="CheckoutSummaryPayState.Paid"/>, throwing to
    /// <see cref="CheckoutSummaryPayState.Error"/>. When null, Pay only raises <see cref="PayRequested"/>, moves to
    /// Processing and leaves the outcome to the host through <see cref="PayState"/>.
    /// </summary>
    public Func<CancellationToken, Task>? PayAsync { get; set; }

    /// <inheritdoc cref="PayStateProperty"/>
    public CheckoutSummaryPayState PayState { get => GetValue(PayStateProperty); set => SetValue(PayStateProperty, value); }

    /// <inheritdoc cref="PayErrorMessageProperty"/>
    public string? PayErrorMessage { get => GetValue(PayErrorMessageProperty); set => SetValue(PayErrorMessageProperty, value); }

    /// <inheritdoc cref="IsPayEnabledProperty"/>
    public bool IsPayEnabled { get => GetValue(IsPayEnabledProperty); set => SetValue(IsPayEnabledProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CheckoutSummaryLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="AppliedCouponProperty"/>
    public CheckoutSummaryCouponResult? AppliedCoupon
    {
        get => _appliedCoupon;
        private set => SetAndRaise(AppliedCouponProperty, ref _appliedCoupon, value);
    }

    /// <inheritdoc cref="SubtotalProperty"/>
    public decimal Subtotal { get => _subtotal; private set => SetAndRaise(SubtotalProperty, ref _subtotal, value); }

    /// <inheritdoc cref="CouponDiscountProperty"/>
    public decimal CouponDiscount { get => _couponDiscount; private set => SetAndRaise(CouponDiscountProperty, ref _couponDiscount, value); }

    /// <inheritdoc cref="TaxAmountProperty"/>
    public decimal TaxAmount { get => _taxAmount; private set => SetAndRaise(TaxAmountProperty, ref _taxAmount, value); }

    /// <inheritdoc cref="TotalProperty"/>
    public decimal Total { get => _total; private set => SetAndRaise(TotalProperty, ref _total, value); }

    /// <inheritdoc cref="IsCouponLockedProperty"/>
    public bool IsCouponLocked { get => _isCouponLocked; private set => SetAndRaise(IsCouponLockedProperty, ref _isCouponLocked, value); }

    /// <summary>The total as shown, such as "$96.75".</summary>
    public string FormattedTotal => _formattedTotal;

    /// <inheritdoc cref="CouponAppliedEvent"/>
    public event EventHandler<CheckoutSummaryCouponEventArgs>? CouponApplied
    {
        add => AddHandler(CouponAppliedEvent, value);
        remove => RemoveHandler(CouponAppliedEvent, value);
    }

    /// <inheritdoc cref="CouponRejectedEvent"/>
    public event EventHandler<CheckoutSummaryCouponEventArgs>? CouponRejected
    {
        add => AddHandler(CouponRejectedEvent, value);
        remove => RemoveHandler(CouponRejectedEvent, value);
    }

    /// <inheritdoc cref="CouponRemovedEvent"/>
    public event EventHandler<CheckoutSummaryCouponEventArgs>? CouponRemoved
    {
        add => AddHandler(CouponRemovedEvent, value);
        remove => RemoveHandler(CouponRemovedEvent, value);
    }

    /// <inheritdoc cref="PayRequestedEvent"/>
    public event EventHandler<CheckoutSummaryPayRequestedEventArgs>? PayRequested
    {
        add => AddHandler(PayRequestedEvent, value);
        remove => RemoveHandler(PayRequestedEvent, value);
    }

    /// <inheritdoc cref="PayStateChangedEvent"/>
    public event EventHandler<CheckoutSummaryPayStateChangedEventArgs>? PayStateChanged
    {
        add => AddHandler(PayStateChangedEvent, value);
        remove => RemoveHandler(PayStateChangedEvent, value);
    }

    /// <summary>Checks and applies <paramref name="code"/> as if typed; returns true when it was accepted.</summary>
    public Task<bool> ApplyCouponAsync(string code) => _coupon?.ApplyAsync(code) ?? Task.FromResult(false);

    /// <summary>Removes the applied promo code (does nothing while paying or after).</summary>
    public void RemoveCoupon()
    {
        if (!IsCouponLocked)
        {
            _coupon?.Remove(focusInput: false);
        }
    }

    /// <summary>Starts a payment as if Pay was pressed; returns once it ended (or at once without <see cref="PayAsync"/>).</summary>
    public async Task StartPaymentAsync()
    {
        if (!IsPayEnabled || PayState is CheckoutSummaryPayState.Processing or CheckoutSummaryPayState.Paid)
        {
            return;
        }
        var request = new CheckoutSummaryPayRequestedEventArgs(PayRequestedEvent, Total, _formattedTotal);
        RaiseEvent(request);
        if (request.Cancel)
        {
            return;
        }
        _coupon?.CancelCheck();
        _payException = null;
        SetCurrentValue(PayStateProperty, CheckoutSummaryPayState.Processing);
        if (PayAsync is not { } pay)
        {
            return;
        }
        _payment?.Cancel();
        var cts = new CancellationTokenSource();
        _payment = cts;
        try
        {
            await pay(cts.Token);
            if (ReferenceEquals(_payment, cts))
            {
                SetCurrentValue(PayStateProperty, CheckoutSummaryPayState.Paid);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Reset or a new attempt took over.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_payment, cts))
            {
                _payException = ex;
                SetCurrentValue(PayStateProperty, CheckoutSummaryPayState.Error);
            }
        }
        finally
        {
            if (ReferenceEquals(_payment, cts))
            {
                _payment = null;
            }
            cts.Dispose();
        }
    }

    /// <summary>Cancels a running payment and returns the button to idle (for a new order, or after a host-side form returned).</summary>
    public void ResetPayState()
    {
        _payment?.Cancel();
        _payment = null;
        _payException = null;
        SetCurrentValue(PayStateProperty, CheckoutSummaryPayState.Idle);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_coupon is not null)
        {
            _coupon.CouponApplied -= OnCouponApplied;
            _coupon.CouponRejected -= OnCouponRejected;
            _coupon.CouponRemoved -= OnCouponRemoved;
        }
        if (_pay is not null)
        {
            _pay.Click -= OnPayClick;
        }
        _item = e.NameScope.Find<Control>(ItemPart);
        _itemName = e.NameScope.Find<TextBlock>("PART_ItemName");
        _itemDescription = e.NameScope.Find<TextBlock>("PART_ItemDescription");
        _itemPrice = e.NameScope.Find<TextBlock>("PART_ItemPrice");
        _itemPeriod = e.NameScope.Find<TextBlock>("PART_ItemPeriod");
        _media = e.NameScope.Find<ContentControl>("PART_Media");
        AutomationProperties.SetName(this, Title ?? string.Empty);
        _coupon = e.NameScope.Find<CheckoutSummaryCouponField>(CouponPart);
        _lines = e.NameScope.Find<CheckoutSummaryLines>(LinesPart);
        _pay = e.NameScope.Find<CheckoutSummaryPayButton>(PayPart);
        _payErrorHost = e.NameScope.Find<CheckoutSummaryCollapse>(PayErrorHostPart);
        _payError = e.NameScope.Find<TextBlock>(PayErrorPart);
        if (_coupon is not null)
        {
            _coupon.Validator = _couponValidator;
            _coupon.Labels = Labels;
            _coupon.Uppercase = UppercaseCoupon;
            _coupon.IsLocked = IsCouponLocked;
            _coupon.SetApplied(AppliedCoupon);
            _coupon.CouponApplied += OnCouponApplied;
            _coupon.CouponRejected += OnCouponRejected;
            _coupon.CouponRemoved += OnCouponRemoved;
        }
        if (_pay is not null)
        {
            _pay.Click += OnPayClick;
            _pay.IsPayEnabled = IsPayEnabled;
            _pay.State = PayState;
        }
        if (_payErrorHost is not null)
        {
            _payErrorHost.FadeWithSpring = true;
            _payErrorHost.SetOpen(PayState == CheckoutSummaryPayState.Error, animate: false);
        }
        UpdateCouponVisibility();
        UpdateLabels();
        Recompute(animate: false);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == ItemProperty)
        {
            Watch(ref _watchedItem, change.GetNewValue<CheckoutSummaryItem?>());
            PseudoClasses.Set(HasMediaPseudoClass, Item?.Media is not null);
            Recompute(animate: IsLoaded);
        }
        else if (property == TaxProperty)
        {
            Watch(ref _watchedTax, change.GetNewValue<CheckoutSummaryTax?>());
            Recompute(animate: IsLoaded);
        }
        else if (property == CurrencyProperty || property == CultureNameProperty)
        {
            Recompute(animate: false);
        }
        else if (property == LabelsProperty)
        {
            if (_coupon is not null)
            {
                _coupon.Labels = Labels;
            }
            UpdateLabels();
            Recompute(animate: false);
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title ?? string.Empty);
        }
        else if (property == IsCouponVisibleProperty)
        {
            UpdateCouponVisibility();
        }
        else if (property == UppercaseCouponProperty && _coupon is not null)
        {
            _coupon.Uppercase = change.GetNewValue<bool>();
        }
        else if (property == IsPayEnabledProperty && _pay is not null)
        {
            _pay.IsPayEnabled = change.GetNewValue<bool>();
        }
        else if (property == PayErrorMessageProperty)
        {
            UpdatePayError();
        }
        else if (property == PayStateProperty)
        {
            OnPayStateChanged(change.GetOldValue<CheckoutSummaryPayState>(), change.GetNewValue<CheckoutSummaryPayState>());
        }
    }

    private void OnPayStateChanged(CheckoutSummaryPayState oldState, CheckoutSummaryPayState newState)
    {
        PseudoClasses.Set(ProcessingPseudoClass, newState == CheckoutSummaryPayState.Processing);
        PseudoClasses.Set(PaidPseudoClass, newState == CheckoutSummaryPayState.Paid);
        PseudoClasses.Set(PayErrorPseudoClass, newState == CheckoutSummaryPayState.Error);
        // The reference keeps the code editable while paying and after; locking it keeps the charge and the total in step.
        IsCouponLocked = newState is CheckoutSummaryPayState.Processing or CheckoutSummaryPayState.Paid;
        if (_coupon is not null)
        {
            if (IsCouponLocked)
            {
                _coupon.CancelCheck();
            }
            _coupon.IsLocked = IsCouponLocked;
        }
        if (_pay is not null)
        {
            _pay.State = newState;
        }
        if (newState != CheckoutSummaryPayState.Error)
        {
            _payException = null;
        }
        UpdatePayError();
        _payErrorHost?.SetOpen(newState == CheckoutSummaryPayState.Error, IsLoaded);
        RaiseEvent(new CheckoutSummaryPayStateChangedEventArgs(PayStateChangedEvent, oldState, newState, _payException));
    }

    private void UpdatePayError()
    {
        if (_payError is not null)
        {
            _payError.Text = PayErrorMessage ?? Labels.PayFailed;
        }
    }

    private void OnPayClick(object? sender, RoutedEventArgs e) => _ = StartPaymentAsync();

    private void OnCouponApplied(string code, CheckoutSummaryCouponResult result)
    {
        AppliedCoupon = result;
        PseudoClasses.Set(CouponAppliedPseudoClass, true);
        Recompute(animate: true);
        RaiseEvent(new CheckoutSummaryCouponEventArgs(CouponAppliedEvent, result.Code ?? code, result));
    }

    private void OnCouponRejected(string code, CheckoutSummaryCouponResult result)
        => RaiseEvent(new CheckoutSummaryCouponEventArgs(CouponRejectedEvent, code, result));

    private void OnCouponRemoved(string code)
    {
        AppliedCoupon = null;
        PseudoClasses.Set(CouponAppliedPseudoClass, false);
        Recompute(animate: true);
        RaiseEvent(new CheckoutSummaryCouponEventArgs(CouponRemovedEvent, code, null));
    }

    private void OnAdjustmentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var line in _watchedLines)
        {
            line.PropertyChanged -= OnDataChanged;
        }
        _watchedLines.Clear();
        foreach (var line in _adjustments)
        {
            line.PropertyChanged += OnDataChanged;
            _watchedLines.Add(line);
        }
        Recompute(animate: IsLoaded);
    }

    private void Watch<T>(ref T? field, T? value) where T : AvaloniaObject
    {
        if (field is not null)
        {
            field.PropertyChanged -= OnDataChanged;
        }
        field = value;
        if (field is not null)
        {
            field.PropertyChanged += OnDataChanged;
        }
    }

    private void OnDataChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is CheckoutSummaryItem && e.Property == CheckoutSummaryItem.MediaProperty)
        {
            PseudoClasses.Set(HasMediaPseudoClass, Item?.Media is not null);
        }
        Recompute(animate: IsLoaded);
    }

    private void UpdateCouponVisibility()
    {
        if (_coupon is not null)
        {
            _coupon.IsVisible = IsCouponVisible && _couponValidator is not null;
        }
    }

    private void UpdateLabels()
    {
        var labels = Labels;
        if (_lines is not null)
        {
            _lines.TotalLabel = labels.Total;
        }
        if (_pay is not null)
        {
            _pay.ProcessingLabel = labels.Processing;
            _pay.PaidLabel = labels.Paid;
            _pay.RetryLabel = labels.TryAgain;
        }
        UpdatePayError();
    }

    private CultureInfo Culture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private void Recompute(bool animate)
    {
        var item = Item;
        var tax = Tax;
        var currency = Currency;
        var culture = Culture();
        var labels = Labels;
        var adjustments = new decimal[_adjustments.Count];
        for (var i = 0; i < adjustments.Length; i++)
        {
            adjustments[i] = _adjustments[i].Amount;
        }
        var coupon = AppliedCoupon;
        var minor = CheckoutSummaryMoney.MinorDigits(currency);
        var totals = CheckoutSummaryMath.Compute(item?.Price ?? 0m, adjustments, coupon, tax?.Rate, minor);
        Subtotal = totals.Subtotal;
        CouponDiscount = totals.Coupon;
        TaxAmount = totals.Tax;
        Total = totals.Total;

        var all = new List<decimal>(adjustments.Length + 4) { totals.Subtotal, totals.Coupon, totals.Tax, totals.Total };
        all.AddRange(adjustments);
        var decimals = CheckoutSummaryMoney.DecimalsFor(currency, all.ToArray());
        string Money(decimal amount) => CheckoutSummaryMoney.Format(amount, currency, culture, decimals);

        var views = new List<CheckoutSummaryLineView>(adjustments.Length + 3)
        {
            new("subtotal", labels.Subtotal, null, Money(totals.Subtotal))
        };
        for (var i = 0; i < _adjustments.Count; i++)
        {
            var line = _adjustments[i];
            var key = string.IsNullOrEmpty(line.Id) || line.Id == CouponLineId ? "line-" + i.ToString(CultureInfo.InvariantCulture) : "line-" + line.Id;
            views.Add(new CheckoutSummaryLineView(key, line.Label ?? string.Empty, line.Note, Money(line.Amount), IsDiscount: line.Amount < 0));
        }
        if (coupon is { IsValid: true })
        {
            var label = string.IsNullOrWhiteSpace(coupon.Label) ? coupon.Code ?? string.Empty : coupon.Label;
            views.Add(new CheckoutSummaryLineView(CouponLineId, label, null, Money(-totals.Coupon), IsDiscount: totals.Coupon > 0));
        }
        if (tax is not null)
        {
            var name = tax.Label ?? string.Empty;
            if (tax.Rate is { } rate)
            {
                views.Add(new CheckoutSummaryLineView("tax", labels.TaxWithRate(name, CheckoutSummaryMoney.Percent(rate, culture)), null, Money(totals.Tax)));
            }
            else
            {
                views.Add(new CheckoutSummaryLineView("tax", name, null, string.IsNullOrWhiteSpace(tax.Note) ? labels.TaxPending : tax.Note, IsPending: true));
            }
        }

        _formattedTotal = Money(totals.Total);
        var shape = CheckoutSummaryMoney.ShapeOf(currency, culture, decimals);
        if (_lines is not null)
        {
            _lines.SetLines(views, animate);
            _lines.SetTotal(totals.Total, shape.Prefix, shape.Suffix, decimals, culture.Name.Length == 0 ? "en-US" : culture.Name,
                labels.TotalAnnouncement(_formattedTotal));
        }
        if (_pay is not null)
        {
            _pay.IdleLabel = labels.Pay(_formattedTotal);
        }
        UpdateItemTexts(item, Money);
    }

    private void UpdateItemTexts(CheckoutSummaryItem? item, Func<decimal, string> money)
    {
        if (_item is not null)
        {
            _item.IsVisible = item is not null;
        }
        if (item is null)
        {
            return;
        }
        SetText(_itemName, item.Name);
        SetText(_itemDescription, item.Description);
        SetText(_itemPrice, money(item.Price));
        SetText(_itemPeriod, item.Period);
        if (_media is not null)
        {
            _media.Content = item.Media;
            _media.IsVisible = item.Media is not null;
        }
    }

    private static void SetText(TextBlock? block, string? text)
    {
        if (block is not null)
        {
            block.Text = text;
            block.IsVisible = !string.IsNullOrEmpty(text);
        }
    }
}
