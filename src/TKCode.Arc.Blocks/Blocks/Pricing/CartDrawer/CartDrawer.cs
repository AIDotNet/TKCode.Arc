using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// A small store on one block: a product grid whose pictures fly into the cart button when added, a count that rolls,
/// and a cart drawer that slides in inside the block with quantity steppers, swipe-to-remove rows, a free-shipping
/// progress bar, a promo code, rolling totals and a checkout that turns into a confirmation and a receipt.
/// </summary>
/// <remarks>
/// The cart is the block's own state: add with the grid's buttons or <see cref="Add"/>, read it from <see cref="Lines"/>
/// and the totals, follow it with the events. Promo codes and orders go through <see cref="ValidatePromoAsync"/> and
/// <see cref="PlaceOrderAsync"/>; by default they use <see cref="CartDrawerSamples"/>' simulated store.
/// </remarks>
/// <example><code>
/// &lt;arc:CartDrawer Title="Fresh from the workshop" FreeShippingThreshold="240" ShippingFee="9"
///                 ProductsSource="{Binding Products}" /&gt;
/// </code></example>
[TemplatePart(RootPart, typeof(Panel))]
[TemplatePart(ShopScrollerPart, typeof(ScrollViewer))]
[TemplatePart(GridPart, typeof(CartDrawerGridPanel))]
[TemplatePart(CartButtonPart, typeof(CartDrawerCartButton))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, OpenPseudoClass)]
public partial class CartDrawer : TemplatedControl
{
    public const string RootPart = "PART_Root";
    public const string ShopScrollerPart = "PART_ShopScroller";
    public const string ShopPart = "PART_Shop";
    public const string TitlePart = "PART_Title";
    public const string CartWrapPart = "PART_CartWrap";
    public const string RipplePart = "PART_Ripple";
    public const string CartButtonPart = "PART_CartButton";
    public const string GridPart = "PART_Grid";
    public const string ScrimPart = "PART_Scrim";
    public const string DrawerHostPart = "PART_DrawerHost";
    public const string FlightLayerPart = "PART_FlightLayer";
    public const string LivePart = "PART_Live";

    /// <summary>Set at 640px and below: two grid columns and tighter padding.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set at 380px and below: the cart button drops its text, prices move under the names, the drawer fills the block.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the drawer is open.</summary>
    public const string OpenPseudoClass = ":open";

    #region Properties

    /// <summary>The grid's heading ("Fresh from the workshop").</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CartDrawer, string?>(nameof(Title), CartDrawerSamples.Title);

    /// <summary>The products on the grid, in order. Products written inline in XAML land here.</summary>
    public static readonly DirectProperty<CartDrawer, AvaloniaList<CartDrawerProduct>> ProductsProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, AvaloniaList<CartDrawerProduct>>(nameof(Products), o => o.Products);

    /// <summary>A product source that replaces <see cref="Products"/> when set (observed when it raises collection changes).</summary>
    public static readonly StyledProperty<IEnumerable<CartDrawerProduct>?> ProductsSourceProperty =
        AvaloniaProperty.Register<CartDrawer, IEnumerable<CartDrawerProduct>?>(nameof(ProductsSource));

    /// <summary>ISO 4217 code of every amount ("USD" by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<CartDrawer, string>(nameof(Currency), "USD");

    /// <summary>
    /// Decimal places amounts are shown with and the discount is rounded to (default 0: whole currency units, halves
    /// away from zero).
    /// </summary>
    public static readonly StyledProperty<int> AmountDecimalsProperty =
        AvaloniaProperty.Register<CartDrawer, int>(nameof(AmountDecimals), 0, coerce: (_, v) => Math.Clamp(v, 0, 4));

    /// <summary>
    /// Subtotal (before the discount) from which shipping is free (default 240). 0 or less hides the free-shipping card and
    /// ships everything free.
    /// </summary>
    public static readonly StyledProperty<decimal> FreeShippingThresholdProperty =
        AvaloniaProperty.Register<CartDrawer, decimal>(nameof(FreeShippingThreshold), CartDrawerSamples.FreeShippingThreshold);

    /// <summary>Shipping charged below the threshold (default 9).</summary>
    public static readonly StyledProperty<decimal> ShippingFeeProperty =
        AvaloniaProperty.Register<CartDrawer, decimal>(nameof(ShippingFee), CartDrawerSamples.ShippingFee);

    /// <summary>Whether the cart drawer is open. Two-way; it cannot close while an order is being placed.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<CartDrawer, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay, coerce: CoerceIsOpen);

    /// <summary>Whether added products fly from their picture to the cart button (default true; Full motion only).</summary>
    public static readonly StyledProperty<bool> IsFlightEnabledProperty =
        AvaloniaProperty.Register<CartDrawer, bool>(nameof(IsFlightEnabled), true);

    /// <summary>Whether cart rows can be swiped left to remove them (default true; Full motion only, × always works).</summary>
    public static readonly StyledProperty<bool> IsSwipeToRemoveEnabledProperty =
        AvaloniaProperty.Register<CartDrawer, bool>(nameof(IsSwipeToRemoveEnabled), true);

    /// <summary>Whether the drawer offers a promo code field (default true).</summary>
    public static readonly StyledProperty<bool> IsPromoVisibleProperty =
        AvaloniaProperty.Register<CartDrawer, bool>(nameof(IsPromoVisible), true);

    /// <summary>
    /// Checks a promo code: return the promo to apply, or null to refuse it. Defaults to the sample check
    /// (<see cref="CartDrawerSamples.ValidatePromoAsync"/>, WORKSHOP10 is 10% off).
    /// </summary>
    public static readonly StyledProperty<Func<string, CancellationToken, Task<CartDrawerPromo?>>?> ValidatePromoAsyncProperty =
        AvaloniaProperty.Register<CartDrawer, Func<string, CancellationToken, Task<CartDrawerPromo?>>?>(
            nameof(ValidatePromoAsync), CartDrawerSamples.ValidatePromoAsync);

    /// <summary>
    /// Places the order and returns the receipt to show; throwing returns the drawer to the cart. Defaults to the sample
    /// order desk (<see cref="CartDrawerSamples.PlaceOrderAsync"/>, 1.3 s).
    /// </summary>
    public static readonly StyledProperty<Func<CartDrawerOrder, CancellationToken, Task<CartDrawerReceipt>>?> PlaceOrderAsyncProperty =
        AvaloniaProperty.Register<CartDrawer, Func<CartDrawerOrder, CancellationToken, Task<CartDrawerReceipt>>?>(
            nameof(PlaceOrderAsync), CartDrawerSamples.PlaceOrderAsync);

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<CartDrawerLabels> LabelsProperty =
        AvaloniaProperty.Register<CartDrawer, CartDrawerLabels>(nameof(Labels), new CartDrawerLabels());

    /// <summary>The cart's rows, in the order they were added.</summary>
    public static readonly DirectProperty<CartDrawer, IReadOnlyList<CartDrawerLine>> LinesProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, IReadOnlyList<CartDrawerLine>>(nameof(Lines), o => o.Lines);

    /// <summary>How many items the cart holds (the sum of the quantities).</summary>
    public static readonly DirectProperty<CartDrawer, int> ItemCountProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, int>(nameof(ItemCount), o => o.ItemCount);

    /// <summary>Sum of the line totals.</summary>
    public static readonly DirectProperty<CartDrawer, decimal> SubtotalProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, decimal>(nameof(Subtotal), o => o.Subtotal);

    /// <summary>What the applied promo takes off the subtotal.</summary>
    public static readonly DirectProperty<CartDrawer, decimal> DiscountProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, decimal>(nameof(Discount), o => o.Discount);

    /// <summary>The shipping charge: 0 from the free-shipping threshold (measured on the subtotal before the discount).</summary>
    public static readonly DirectProperty<CartDrawer, decimal> ShippingProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, decimal>(nameof(Shipping), o => o.Shipping);

    /// <summary>Subtotal − discount + shipping.</summary>
    public static readonly DirectProperty<CartDrawer, decimal> TotalProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, decimal>(nameof(Total), o => o.Total);

    /// <summary>Where the checkout is: shopping, placing the order, or showing its receipt.</summary>
    public static readonly DirectProperty<CartDrawer, CartDrawerState> StateProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, CartDrawerState>(nameof(State), o => o.State);

    /// <summary>The promo code in effect (null for none).</summary>
    public static readonly DirectProperty<CartDrawer, CartDrawerPromo?> AppliedPromoProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, CartDrawerPromo?>(nameof(AppliedPromo), o => o.AppliedPromo);

    /// <summary>The receipt of the order just placed (null until then and again after the drawer closes).</summary>
    public static readonly DirectProperty<CartDrawer, CartDrawerReceipt?> ReceiptProperty =
        AvaloniaProperty.RegisterDirect<CartDrawer, CartDrawerReceipt?>(nameof(Receipt), o => o.Receipt);

    /// <summary>Raised when a product is added from the grid or with <see cref="Add"/> (also when it raises a row's quantity).</summary>
    public static readonly RoutedEvent<CartDrawerItemEventArgs> ItemAddedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerItemEventArgs>(nameof(ItemAdded), RoutingStrategies.Bubble);

    /// <summary>Raised when a row's quantity changes with its stepper or <see cref="SetQuantity"/>.</summary>
    public static readonly RoutedEvent<CartDrawerItemEventArgs> QuantityChangedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerItemEventArgs>(nameof(QuantityChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a row is removed (×, the stepper's bin, a swipe or <see cref="Remove"/>).</summary>
    public static readonly RoutedEvent<CartDrawerItemEventArgs> ItemRemovedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerItemEventArgs>(nameof(ItemRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when a promo code is accepted.</summary>
    public static readonly RoutedEvent<CartDrawerPromoEventArgs> PromoAppliedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerPromoEventArgs>(nameof(PromoApplied), RoutingStrategies.Bubble);

    /// <summary>Raised when a promo code is refused.</summary>
    public static readonly RoutedEvent<CartDrawerPromoEventArgs> PromoRejectedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerPromoEventArgs>(nameof(PromoRejected), RoutingStrategies.Bubble);

    /// <summary>Raised when the applied promo code is removed.</summary>
    public static readonly RoutedEvent<CartDrawerPromoEventArgs> PromoRemovedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerPromoEventArgs>(nameof(PromoRemoved), RoutingStrategies.Bubble);

    /// <summary>Raised when an order goes through, with the order and its receipt.</summary>
    public static readonly RoutedEvent<CartDrawerOrderPlacedEventArgs> OrderPlacedEvent =
        RoutedEvent.Register<CartDrawer, CartDrawerOrderPlacedEventArgs>(nameof(OrderPlaced), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="ProductsProperty"/>
    [Content]
    public AvaloniaList<CartDrawerProduct> Products => _products;

    /// <inheritdoc cref="ProductsSourceProperty"/>
    public IEnumerable<CartDrawerProduct>? ProductsSource
    {
        get => GetValue(ProductsSourceProperty);
        set => SetValue(ProductsSourceProperty, value);
    }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    /// <inheritdoc cref="AmountDecimalsProperty"/>
    public int AmountDecimals
    {
        get => GetValue(AmountDecimalsProperty);
        set => SetValue(AmountDecimalsProperty, value);
    }

    /// <inheritdoc cref="FreeShippingThresholdProperty"/>
    public decimal FreeShippingThreshold
    {
        get => GetValue(FreeShippingThresholdProperty);
        set => SetValue(FreeShippingThresholdProperty, value);
    }

    /// <inheritdoc cref="ShippingFeeProperty"/>
    public decimal ShippingFee
    {
        get => GetValue(ShippingFeeProperty);
        set => SetValue(ShippingFeeProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="IsFlightEnabledProperty"/>
    public bool IsFlightEnabled
    {
        get => GetValue(IsFlightEnabledProperty);
        set => SetValue(IsFlightEnabledProperty, value);
    }

    /// <inheritdoc cref="IsSwipeToRemoveEnabledProperty"/>
    public bool IsSwipeToRemoveEnabled
    {
        get => GetValue(IsSwipeToRemoveEnabledProperty);
        set => SetValue(IsSwipeToRemoveEnabledProperty, value);
    }

    /// <inheritdoc cref="IsPromoVisibleProperty"/>
    public bool IsPromoVisible
    {
        get => GetValue(IsPromoVisibleProperty);
        set => SetValue(IsPromoVisibleProperty, value);
    }

    /// <inheritdoc cref="ValidatePromoAsyncProperty"/>
    public Func<string, CancellationToken, Task<CartDrawerPromo?>>? ValidatePromoAsync
    {
        get => GetValue(ValidatePromoAsyncProperty);
        set => SetValue(ValidatePromoAsyncProperty, value);
    }

    /// <inheritdoc cref="PlaceOrderAsyncProperty"/>
    public Func<CartDrawerOrder, CancellationToken, Task<CartDrawerReceipt>>? PlaceOrderAsync
    {
        get => GetValue(PlaceOrderAsyncProperty);
        set => SetValue(PlaceOrderAsyncProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public CartDrawerLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="LinesProperty"/>
    public IReadOnlyList<CartDrawerLine> Lines
    {
        get => _linesSnapshot;
        private set => SetAndRaise(LinesProperty, ref _linesSnapshot, value);
    }

    /// <inheritdoc cref="ItemCountProperty"/>
    public int ItemCount
    {
        get => _itemCount;
        private set => SetAndRaise(ItemCountProperty, ref _itemCount, value);
    }

    /// <inheritdoc cref="SubtotalProperty"/>
    public decimal Subtotal
    {
        get => _subtotal;
        private set => SetAndRaise(SubtotalProperty, ref _subtotal, value);
    }

    /// <inheritdoc cref="DiscountProperty"/>
    public decimal Discount
    {
        get => _discount;
        private set => SetAndRaise(DiscountProperty, ref _discount, value);
    }

    /// <inheritdoc cref="ShippingProperty"/>
    public decimal Shipping
    {
        get => _shipping;
        private set => SetAndRaise(ShippingProperty, ref _shipping, value);
    }

    /// <inheritdoc cref="TotalProperty"/>
    public decimal Total
    {
        get => _total;
        private set => SetAndRaise(TotalProperty, ref _total, value);
    }

    /// <inheritdoc cref="StateProperty"/>
    public CartDrawerState State
    {
        get => _state;
        private set => SetAndRaise(StateProperty, ref _state, value);
    }

    /// <inheritdoc cref="AppliedPromoProperty"/>
    public CartDrawerPromo? AppliedPromo
    {
        get => _promo;
        private set => SetAndRaise(AppliedPromoProperty, ref _promo, value);
    }

    /// <inheritdoc cref="ReceiptProperty"/>
    public CartDrawerReceipt? Receipt
    {
        get => _receipt;
        private set => SetAndRaise(ReceiptProperty, ref _receipt, value);
    }

    /// <inheritdoc cref="ItemAddedEvent"/>
    public event EventHandler<CartDrawerItemEventArgs>? ItemAdded
    {
        add => AddHandler(ItemAddedEvent, value);
        remove => RemoveHandler(ItemAddedEvent, value);
    }

    /// <inheritdoc cref="QuantityChangedEvent"/>
    public event EventHandler<CartDrawerItemEventArgs>? QuantityChanged
    {
        add => AddHandler(QuantityChangedEvent, value);
        remove => RemoveHandler(QuantityChangedEvent, value);
    }

    /// <inheritdoc cref="ItemRemovedEvent"/>
    public event EventHandler<CartDrawerItemEventArgs>? ItemRemoved
    {
        add => AddHandler(ItemRemovedEvent, value);
        remove => RemoveHandler(ItemRemovedEvent, value);
    }

    /// <inheritdoc cref="PromoAppliedEvent"/>
    public event EventHandler<CartDrawerPromoEventArgs>? PromoApplied
    {
        add => AddHandler(PromoAppliedEvent, value);
        remove => RemoveHandler(PromoAppliedEvent, value);
    }

    /// <inheritdoc cref="PromoRejectedEvent"/>
    public event EventHandler<CartDrawerPromoEventArgs>? PromoRejected
    {
        add => AddHandler(PromoRejectedEvent, value);
        remove => RemoveHandler(PromoRejectedEvent, value);
    }

    /// <inheritdoc cref="PromoRemovedEvent"/>
    public event EventHandler<CartDrawerPromoEventArgs>? PromoRemoved
    {
        add => AddHandler(PromoRemovedEvent, value);
        remove => RemoveHandler(PromoRemovedEvent, value);
    }

    /// <inheritdoc cref="OrderPlacedEvent"/>
    public event EventHandler<CartDrawerOrderPlacedEventArgs>? OrderPlaced
    {
        add => AddHandler(OrderPlacedEvent, value);
        remove => RemoveHandler(OrderPlacedEvent, value);
    }

    #endregion

    private sealed class Entry
    {
        public required CartDrawerProduct Product { get; init; }
        public int Quantity { get; set; }
        public CartDrawerRow? Row { get; set; }
    }

    private readonly AvaloniaList<CartDrawerProduct> _products = new();
    private readonly List<Entry> _entries = new();
    private readonly HashSet<CartDrawerProduct> _seeded = new();
    private IReadOnlyList<CartDrawerLine> _linesSnapshot = Array.Empty<CartDrawerLine>();
    private int _itemCount;
    private decimal _subtotal;
    private decimal _discount;
    private decimal _shipping;
    private decimal _total;
    private CartDrawerState _state;
    private CartDrawerPromo? _promo;
    private CartDrawerReceipt? _receipt;
    private INotifyCollectionChanged? _watchedSource;
    private CancellationTokenSource? _work;
    private bool _keyboardInput;
    private string? _lastAnnouncement;

    static CartDrawer()
    {
        FocusableProperty.OverrideDefaultValue<CartDrawer>(false);
    }

    public CartDrawer()
    {
        _products.CollectionChanged += (_, _) => OnProductsChanged();
        AddHandler(KeyDownEvent, (_, _) => _keyboardInput = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardInput = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>The last text sent to the polite live region (tests and diagnostics).</summary>
    internal string? LastAnnouncement => _lastAnnouncement;

    /// <summary>The products on the grid: <see cref="ProductsSource"/> when set, otherwise <see cref="Products"/>.</summary>
    internal IReadOnlyList<CartDrawerProduct> EffectiveProducts => ProductsSource is { } source ? source.ToList() : _products;

    private static bool CoerceIsOpen(AvaloniaObject o, bool value)
        => !value && o is CartDrawer { _state: CartDrawerState.Placing } || value;

    #region Public methods

    /// <summary>
    /// Adds one of <paramref name="product"/> (a new row, or one more on its row). Returns false when the row is at the
    /// product's maximum or an order is in progress: nothing flies and nothing is announced then.
    /// </summary>
    public bool Add(CartDrawerProduct product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (State != CartDrawerState.Shopping)
        {
            return false;
        }
        var entry = Find(product);
        var old = entry?.Quantity ?? 0;
        if (old >= product.MaxQuantity)
        {
            return false;
        }
        if (entry is null)
        {
            entry = new Entry { Product = product };
            _entries.Add(entry);
        }
        entry.Quantity = old + 1;
        StartFlight(product);
        Announce(Labels.AnnounceAdded(product.Name));
        Recompute(animate: true);
        ShowAdded(product);
        RaiseEvent(new CartDrawerItemEventArgs(ItemAddedEvent, product, old, entry.Quantity));
        return true;
    }

    /// <summary>Sets the quantity of <paramref name="product"/>'s row (clamped to its maximum; 0 removes the row).</summary>
    public void SetQuantity(CartDrawerProduct product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (State == CartDrawerState.Placing)
        {
            return;
        }
        var entry = Find(product);
        var target = CartDrawerMath.ClampQuantity(quantity, product.MaxQuantity);
        if (target <= 0)
        {
            if (entry is not null)
            {
                Remove(product);
            }
            return;
        }
        if (entry is null)
        {
            entry = new Entry { Product = product };
            _entries.Add(entry);
        }
        var old = entry.Quantity;
        if (old == target)
        {
            return;
        }
        entry.Quantity = target;
        Recompute(animate: true);
        RaiseEvent(new CartDrawerItemEventArgs(QuantityChangedEvent, product, old, target));
    }

    /// <summary>Removes <paramref name="product"/>'s row (it fades and collapses). Returns false when it was not in the cart.</summary>
    public bool Remove(CartDrawerProduct product) => RemoveCore(product, swiped: false);

    /// <summary>Empties the cart at once, without events or animation (also removes the promo code).</summary>
    public void Clear()
    {
        foreach (var entry in _entries)
        {
            entry.Row = null;
        }
        _entries.Clear();
        AppliedPromo = null;
        _pendingFlights = 0;
        RebuildRows();
        SyncPromoView(animate: false);
        Recompute(animate: false);
    }

    /// <summary>Opens the cart drawer.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Closes the cart drawer (ignored while an order is being placed). Closing on a receipt starts a new cart.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>
    /// Checks <paramref name="code"/> with <see cref="ValidatePromoAsync"/> and applies it when accepted. Empty codes are
    /// ignored. Returns whether a code was applied.
    /// </summary>
    public async Task<bool> ApplyPromoAsync(string code)
    {
        var trimmed = code?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || State != CartDrawerState.Shopping)
        {
            return false;
        }
        var check = ValidatePromoAsync;
        CartDrawerPromo? promo = null;
        if (check is not null)
        {
            try
            {
                promo = await check(trimmed, Work()).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
        if (promo is null)
        {
            OnPromoRejected(trimmed);
            RaiseEvent(new CartDrawerPromoEventArgs(PromoRejectedEvent, trimmed, null));
            return false;
        }
        AppliedPromo = promo;
        Recompute(animate: true);
        SyncPromoView(animate: true);
        Announce(Labels.AnnouncePromoApplied(promo.Code));
        FocusPromoRemove();
        RaiseEvent(new CartDrawerPromoEventArgs(PromoAppliedEvent, trimmed, promo));
        return true;
    }

    /// <summary>Removes the applied promo code.</summary>
    public void RemovePromo()
    {
        if (AppliedPromo is not { } promo || State != CartDrawerState.Shopping)
        {
            return;
        }
        AppliedPromo = null;
        Recompute(animate: true);
        SyncPromoView(animate: true);
        Announce(Labels.AnnouncePromoRemoved(promo.Code));
        FocusPromoInput();
        RaiseEvent(new CartDrawerPromoEventArgs(PromoRemovedEvent, promo.Code, promo));
    }

    /// <summary>
    /// Places the order with <see cref="PlaceOrderAsync"/>: the checkout button shows its busy state, then turns into the
    /// confirmation disc and the drawer shows the receipt. Does nothing with an empty cart or while an order is in progress.
    /// </summary>
    public async Task CheckoutAsync()
    {
        if (State != CartDrawerState.Shopping || _entries.Count == 0)
        {
            return;
        }
        var order = CreateOrder();
        State = CartDrawerState.Placing;
        EnterPlacing();
        CartDrawerReceipt receipt;
        try
        {
            var place = PlaceOrderAsync;
            receipt = place is null
                ? new CartDrawerReceipt(Labels.OrderPlacedTitle, string.Empty, order.ItemCount, order.Total, string.Empty)
                : await place(order, Work()).ConfigureAwait(true);
        }
        catch (Exception)
        {
            State = CartDrawerState.Shopping;
            LeavePlacing();
            return;
        }
        Receipt = receipt;
        State = CartDrawerState.Placed;
        ShowReceipt(receipt);
        Announce(Labels.AnnounceOrderPlaced(receipt.ItemCount, Money(receipt.Paid)));
        RaiseEvent(new CartDrawerOrderPlacedEventArgs(OrderPlacedEvent, order, receipt));
    }

    #endregion

    #region Cart state

    private Entry? Find(CartDrawerProduct product) => _entries.Find(e => ReferenceEquals(e.Product, product));

    private bool RemoveCore(CartDrawerProduct product, bool swiped)
    {
        var entry = Find(product);
        if (entry is null || State == CartDrawerState.Placing)
        {
            return false;
        }
        var index = _entries.IndexOf(entry);
        var focusedRow = entry.Row is { } row && row.IsKeyboardFocusWithin ? row : null;
        var focusOnRemove = focusedRow?.RemoveButton.IsFocused == true;
        _entries.Remove(entry);
        var old = entry.Quantity;
        if (entry.Row is { } leaving)
        {
            _ = LeaveRow(leaving, swiped);
        }
        Announce(Labels.AnnounceRemoved(product.Name));
        Recompute(animate: true);
        if (focusedRow is not null)
        {
            FocusAfterRemoval(index, focusOnRemove);
        }
        RaiseEvent(new CartDrawerItemEventArgs(ItemRemovedEvent, product, old, 0));
        return true;
    }

    private CartDrawerOrder CreateOrder()
        => new(Lines, AppliedPromo, Subtotal, Discount, Shipping, Total, Currency);

    /// <summary>Recomputes the totals and pushes them to the screen.</summary>
    private void Recompute(bool animate)
    {
        var lines = _entries.Select(e => new CartDrawerLine(e.Product, e.Quantity)).ToArray();
        Lines = lines;
        ItemCount = lines.Sum(l => l.Quantity);
        var subtotal = CartDrawerMath.Subtotal(lines);
        Subtotal = subtotal;
        var discount = AppliedPromo is { } promo ? CartDrawerMath.Discount(subtotal, promo.PercentOff, AmountDecimals) : 0m;
        Discount = discount;
        var shipping = CartDrawerMath.Shipping(subtotal, FreeShippingThreshold, ShippingFee);
        Shipping = shipping;
        Total = CartDrawerMath.Total(subtotal, discount, shipping);
        UpdateBadge(animate);
        UpdateGridButtons();
        UpdateDrawer(animate);
    }

    private void SeedInitialQuantities()
    {
        var changed = false;
        foreach (var product in EffectiveProducts)
        {
            if (!_seeded.Add(product))
            {
                continue;
            }
            if (product.InitialQuantity > 0 && Find(product) is null)
            {
                _entries.Add(new Entry { Product = product, Quantity = CartDrawerMath.ClampQuantity(product.InitialQuantity, product.MaxQuantity) });
                changed = true;
            }
        }
        if (changed)
        {
            RebuildRows();
            Recompute(animate: false);
        }
    }

    private void OnProductsChanged()
    {
        RebuildGrid();
        SeedInitialQuantities();
    }

    private void WatchSource()
    {
        if (_watchedSource is not null)
        {
            _watchedSource.CollectionChanged -= OnSourceChanged;
            _watchedSource = null;
        }
        if (ProductsSource is INotifyCollectionChanged observable)
        {
            _watchedSource = observable;
            observable.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnProductsChanged();

    private CancellationToken Work()
    {
        _work ??= new CancellationTokenSource();
        return _work.Token;
    }

    private void Announce(string text)
    {
        _lastAnnouncement = text;
        if (_live is not null)
        {
            // Clearing first makes a repeated sentence count as a change for screen readers.
            _live.Text = string.Empty;
            _live.Text = text;
        }
    }

    private string Money(decimal amount) => CartDrawerFormat.Money(amount, Currency, Labels.Culture, AmountDecimals);

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProductsSourceProperty)
        {
            WatchSource();
            OnProductsChanged();
        }
        else if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(OpenPseudoClass, IsOpen);
            OnIsOpenChanged(IsOpen);
        }
        else if (change.Property == TitleProperty)
        {
            if (_title is not null)
            {
                _title.Text = Title;
            }
        }
        else if (change.Property == LabelsProperty || change.Property == CurrencyProperty || change.Property == AmountDecimalsProperty)
        {
            RebuildGrid();
            RebuildRows();
            RebuildDrawerText();
            Recompute(animate: false);
        }
        else if (change.Property == FreeShippingThresholdProperty || change.Property == ShippingFeeProperty)
        {
            Recompute(animate: true);
        }
        else if (change.Property == IsPromoVisibleProperty)
        {
            UpdateDrawer(animate: false);
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateBreakpoints();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            if (State != CartDrawerState.Placing)
            {
                Close();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _work?.Cancel();
        _work?.Dispose();
        _work = null;
        StopAddedTimers();
    }
}
