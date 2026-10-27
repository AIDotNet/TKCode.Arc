using System.Collections.Specialized;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

// The order ticket: side and type, shares and limit price, the summary, validation in the status line, placing (with an
// optional async callback), fills (market at once, marketable limits at the better price, working limits when the price
// crosses them), cancel, and the order list.
public partial class MarketTerminal
{
    private TextBlock? _ticketTitle, _ticketHeld, _priceLabel, _priceValue, _costLabel, _costValue, _powerLabel, _powerValue;
    private TextBlock? _status, _disclaimer, _ordersTitle;
    private SegmentedControl? _side, _type;
    private NumberField? _shares, _limit;
    private SpringSizeDecorator? _limitPanel;
    private Control? _limitHost, _ordersSection;
    private Button? _submit;
    private StackPanel? _orderRows;
    private CancellationTokenSource? _placing;
    private string? _error;
    private bool _pending;
    private readonly List<(MarketOrder Order, EventHandler Handler)> _orderHooks = new();

    /// <summary>What the ticket would submit now, or null while it cannot (no symbol, a problem in the status line, an order pending).</summary>
    public MarketOrderRequest? PendingRequest => CurrentRequest() is { } request && Validate(request) is null && !_pending ? request : null;

    /// <summary>The text under the submit button: a validation problem or the last failure; null when there is none.</summary>
    public string? StatusText => _status?.Text is { Length: > 0 } text ? text : null;

    /// <summary>Shares of <paramref name="ticker"/> held.</summary>
    public int SharesHeld(string ticker) => Holdings.Where(h => h.Ticker == ticker).Sum(h => h.Shares);

    /// <summary>Submits the ticket as if its button were pressed; returns the order, or null when it was not placed.</summary>
    public async Task<MarketOrder?> SubmitOrderAsync()
    {
        var request = CurrentRequest();
        if (request is null || _pending)
        {
            return null;
        }
        var problem = Validate(request);
        if (problem is not null)
        {
            UpdateTicket();
            return null;
        }
        _error = null;
        if (PlaceOrderAction is { } action)
        {
            _pending = true;
            _placing = new CancellationTokenSource();
            UpdateTicket();
            try
            {
                await action(request, _placing.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _error = string.IsNullOrWhiteSpace(ex.Message) ? Labels.OrderFailed : ex.Message;
                return null;
            }
            finally
            {
                _pending = false;
                _placing?.Dispose();
                _placing = null;
                UpdateTicket();
            }
        }
        return Execute(request);
    }

    /// <summary>Cancels a working order (after <see cref="CancelOrderAction"/>, when set); false when it was not working or the callback failed.</summary>
    public async Task<bool> CancelOrderAsync(MarketOrder order)
    {
        if (order.Status != MarketOrderStatus.Working)
        {
            return false;
        }
        if (CancelOrderAction is { } action)
        {
            try
            {
                await action(order, CancellationToken.None);
            }
            catch (Exception)
            {
                return false;
            }
        }
        if (order.Status != MarketOrderStatus.Working)
        {
            return false;
        }
        order.Update(MarketOrderStatus.Canceled, order.Price);
        Announce(order);
        RaiseEvent(new MarketOrderEventArgs(OrderCanceledEvent, order));
        return true;
    }

    private void HookTicket(INameScope scope)
    {
        _ticketTitle = scope.Find<TextBlock>("PART_TicketTitle");
        _ticketHeld = scope.Find<TextBlock>("PART_TicketHeld");
        _side = scope.Find<SegmentedControl>("PART_Side");
        _type = scope.Find<SegmentedControl>("PART_Type");
        _shares = scope.Find<NumberField>("PART_Shares");
        _limitPanel = scope.Find<SpringSizeDecorator>("PART_LimitPanel");
        _limitHost = scope.Find<Control>("PART_LimitHost");
        _limit = scope.Find<NumberField>("PART_Limit");
        _priceLabel = scope.Find<TextBlock>("PART_PriceLabel");
        _priceValue = scope.Find<TextBlock>("PART_PriceValue");
        _costLabel = scope.Find<TextBlock>("PART_CostLabel");
        _costValue = scope.Find<TextBlock>("PART_CostValue");
        _powerLabel = scope.Find<TextBlock>("PART_PowerLabel");
        _powerValue = scope.Find<TextBlock>("PART_PowerValue");
        _status = scope.Find<TextBlock>("PART_Status");
        _submit = scope.Find<Button>("PART_Submit");
        _disclaimer = scope.Find<TextBlock>("PART_Disclaimer");
        _ordersSection = scope.Find<Control>("PART_OrdersSection");
        _ordersTitle = scope.Find<TextBlock>("PART_OrdersTitle");
        _orderRows = scope.Find<StackPanel>("PART_OrderRows");

        if (_side is not null)
        {
            _side.SelectionChanged += OnSideSelectionChanged;
        }
        if (_type is not null)
        {
            _type.SelectionChanged += OnTypeSelectionChanged;
        }
        if (_shares is not null)
        {
            _shares.ValueChanged += OnSharesChanged;
        }
        if (_limit is not null)
        {
            _limit.ValueChanged += OnLimitChanged;
            _limit.PrefixFormatter = _ => "$";
        }
        if (_submit is not null)
        {
            _submit.Click += OnSubmitClick;
        }
        if (_status is not null)
        {
            AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        }
        SyncTicketInputs();
    }

    private void UnhookTicket()
    {
        if (_side is not null)
        {
            _side.SelectionChanged -= OnSideSelectionChanged;
        }
        if (_type is not null)
        {
            _type.SelectionChanged -= OnTypeSelectionChanged;
        }
        if (_shares is not null)
        {
            _shares.ValueChanged -= OnSharesChanged;
        }
        if (_limit is not null)
        {
            _limit.ValueChanged -= OnLimitChanged;
        }
        if (_submit is not null)
        {
            _submit.Click -= OnSubmitClick;
        }
        ClearOrderRows();
    }

    private void ApplyTicketLabels()
    {
        var labels = Labels;
        _syncing = true;
        try
        {
            if (_side is not null)
            {
                _side.ItemsSource = new[] { new SegmentedOption(MarketOrderSide.Buy, labels.Buy), new SegmentedOption(MarketOrderSide.Sell, labels.Sell) };
                _side.SelectedValue = OrderSide;
                AutomationProperties.SetName(_side, labels.SideGroup);
            }
            if (_type is not null)
            {
                _type.ItemsSource = new[] { new SegmentedOption(MarketOrderType.Market, labels.Market), new SegmentedOption(MarketOrderType.Limit, labels.Limit) };
                _type.SelectedValue = OrderType;
                AutomationProperties.SetName(_type, labels.TypeGroup);
            }
        }
        finally
        {
            _syncing = false;
        }
        if (_shares is not null)
        {
            _shares.Label = labels.Shares;
        }
        if (_limit is not null)
        {
            _limit.Label = labels.LimitPrice;
        }
        SetText(_disclaimer, labels.Disclaimer);
        SetText(_ordersTitle, labels.Orders);
    }

    private void SyncTicketInputs()
    {
        _syncing = true;
        try
        {
            if (_shares is not null && _shares.Value != OrderShares)
            {
                _shares.Value = Math.Max(0, OrderShares);
            }
            if (_limit is not null && (double)_limit.Value != LimitPrice)
            {
                _limit.Value = (decimal)Math.Max(0, Math.Round(LimitPrice, 2));
            }
            if (_side is not null)
            {
                _side.SelectedValue = OrderSide;
            }
            if (_type is not null)
            {
                _type.SelectedValue = OrderType;
            }
            if (_limitPanel is not null)
            {
                _limitPanel.IsExpanded = OrderType == MarketOrderType.Limit;
            }
            _limitHost?.Classes.Set("open", OrderType == MarketOrderType.Limit);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSideSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _side?.SelectedValue is MarketOrderSide side)
        {
            SetCurrentValue(OrderSideProperty, side);
        }
    }

    private void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _type?.SelectedValue is MarketOrderType type)
        {
            SetCurrentValue(OrderTypeProperty, type);
        }
    }

    private void OnSharesChanged(object? sender, NumberFieldValueChangedEventArgs e)
    {
        if (!_syncing && _shares is not null)
        {
            SetCurrentValue(OrderSharesProperty, (int)Math.Clamp(Math.Round(_shares.Value), 0, int.MaxValue));
        }
    }

    private void OnLimitChanged(object? sender, NumberFieldValueChangedEventArgs e)
    {
        if (!_syncing && _limit is not null)
        {
            SetCurrentValue(LimitPriceProperty, (double)_limit.Value);
        }
    }

    private async void OnSubmitClick(object? sender, RoutedEventArgs e) => await SubmitOrderAsync();

    private void OnTicketModeChanged(bool typeChanged)
    {
        _error = null;
        if (typeChanged && OrderType == MarketOrderType.Limit && LimitPrice <= 0 && EffectiveSymbol is { } symbol)
        {
            SetCurrentValue(LimitPriceProperty, Math.Round(PriceOf(symbol.Ticker), 2));
        }
        SyncTicketInputs();
        UpdateTicket();
    }

    private void ResetTicketForTicker()
    {
        _error = null;
        if (OrderType == MarketOrderType.Limit && EffectiveSymbol is { } symbol)
        {
            SetCurrentValue(LimitPriceProperty, Math.Round(PriceOf(symbol.Ticker), 2));
        }
        UpdateTicket();
    }

    private MarketOrderRequest? CurrentRequest()
    {
        var symbol = EffectiveSymbol;
        if (symbol is null || _simulation is null || !_simulation.Contains(symbol.Ticker))
        {
            return null;
        }
        var price = OrderType == MarketOrderType.Limit ? Math.Round(LimitPrice, 2) : _simulation.Price(symbol.Ticker);
        return new MarketOrderRequest(OrderSide, OrderType, symbol.Ticker, OrderShares, price);
    }

    private string? Validate(MarketOrderRequest request)
    {
        var labels = Labels;
        if (request.Shares < 1)
        {
            return labels.TooFewShares;
        }
        if (request.Type == MarketOrderType.Limit && request.Price <= 0)
        {
            return labels.NoPrice;
        }
        if (request.Side == MarketOrderSide.Buy && request.Shares * request.Price > BuyingPower + 1e-6)
        {
            return labels.NotEnoughBuyingPower(FinanceFormat.Money(BuyingPower, "$"));
        }
        var held = SharesHeld(request.Ticker);
        if (request.Side == MarketOrderSide.Sell && request.Shares > held)
        {
            return labels.NotEnoughShares(held, request.Ticker);
        }
        return null;
    }

    private void UpdateTicket()
    {
        var labels = Labels;
        var request = CurrentRequest();
        var ticker = request?.Ticker ?? string.Empty;
        SetText(_ticketTitle, labels.Trade(ticker));
        SetText(_ticketHeld, labels.SharesHeld(SharesHeld(ticker)));
        if (request is null)
        {
            if (_submit is not null)
            {
                _submit.IsEnabled = false;
            }
            return;
        }
        var isBuy = request.Side == MarketOrderSide.Buy;
        SetText(_priceLabel, request.Type == MarketOrderType.Limit ? labels.LimitPrice : labels.MarketPrice);
        SetText(_priceValue, FinanceFormat.Money(request.Price, "$"));
        SetText(_costLabel, isBuy ? labels.EstimatedCost : labels.EstimatedProceeds);
        SetText(_costValue, FinanceFormat.Money(Math.Max(0, request.Shares) * request.Price, "$"));
        SetText(_powerLabel, isBuy ? labels.BuyingPower : labels.Position);
        SetText(_powerValue, isBuy ? FinanceFormat.Money(BuyingPower, "$") : FinanceFormat.Count(SharesHeld(ticker)));
        var problem = _error ?? Validate(request);
        if (_status is not null)
        {
            _status.Text = problem;
            _status.Classes.Set("shown", problem is not null);
        }
        if (_submit is not null)
        {
            _submit.Content = labels.Submit(request.Side, Math.Max(0, request.Shares), ticker);
            _submit.IsLoading = _pending;
            _submit.IsEnabled = Validate(request) is null && !_pending;
        }
    }

    private MarketOrder Execute(MarketOrderRequest request)
    {
        var live = PriceOf(request.Ticker);
        MarketOrder order;
        if (request.Type == MarketOrderType.Market)
        {
            order = new MarketOrder(request.Side, request.Type, request.Ticker, request.Shares, live, MarketOrderStatus.Filled, DateTime.Now);
        }
        else
        {
            // A marketable limit fills at once, at the better of the limit and the market.
            var marketable = request.Side == MarketOrderSide.Buy ? request.Price >= live : request.Price <= live;
            order = marketable
                ? new MarketOrder(request.Side, request.Type, request.Ticker, request.Shares, live, MarketOrderStatus.Filled, DateTime.Now)
                : new MarketOrder(request.Side, request.Type, request.Ticker, request.Shares, request.Price, MarketOrderStatus.Working, DateTime.Now);
        }
        if (order.Status == MarketOrderStatus.Filled)
        {
            ApplyFill(order);
        }
        Orders.Insert(0, order);
        Announce(order);
        RaiseEvent(new MarketOrderEventArgs(OrderPlacedEvent, order));
        if (order.Status == MarketOrderStatus.Filled)
        {
            RaiseEvent(new MarketOrderEventArgs(OrderFilledEvent, order));
        }
        UpdateTicket();
        return order;
    }

    private void ApplyFill(MarketOrder order)
    {
        var amount = order.Shares * order.Price;
        var sign = order.Side == MarketOrderSide.Buy ? 1 : -1;
        SetCurrentValue(BuyingPowerProperty, Math.Round(BuyingPower - sign * amount, 2));
        var holding = Holdings.FirstOrDefault(h => h.Ticker == order.Ticker);
        if (holding is null)
        {
            Holdings.Add(new MarketHolding(order.Ticker, sign * order.Shares));
        }
        else
        {
            holding.Shares += sign * order.Shares;
        }
    }

    /// <summary>Working limit orders fill on their own when the live price reaches them.</summary>
    private void FillWorkingOrders()
    {
        if (_simulation is null)
        {
            return;
        }
        foreach (var order in Orders.ToArray())
        {
            if (order.Status != MarketOrderStatus.Working || !_simulation.Contains(order.Ticker))
            {
                continue;
            }
            var price = _simulation.Price(order.Ticker);
            var reached = order.Side == MarketOrderSide.Buy ? price <= order.Price : price >= order.Price;
            if (!reached)
            {
                continue;
            }
            order.Update(MarketOrderStatus.Filled, order.Price);
            ApplyFill(order);
            Announce(order);
            RaiseEvent(new MarketOrderEventArgs(OrderFilledEvent, order));
        }
    }

    private void Announce(MarketOrder order)
    {
        var labels = Labels;
        _announcer?.Announce(labels.OrderAnnouncement(labels.OrderTitle(order.Side, order.Shares, order.Ticker), labels.Status(order.Status)));
    }

    private void OnOrderListChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_orderRows is null)
        {
            return;
        }
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewStartingIndex == 0 && e.NewItems is { Count: 1 } items
            && items[0] is MarketOrder order && _orderRows.Children.Count == Orders.Count - 1)
        {
            // A new order grows into the top of the list.
            var host = new PresenceHost { IsPresent = false, Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Height, Child = CreateOrderRow(order) };
            _orderRows.Children.Insert(0, host);
            Dispatcher.UIThread.Post(() => host.IsPresent = true, DispatcherPriority.Loaded);
            UpdateOrdersSection();
            return;
        }
        RebuildOrders();
    }

    private void RebuildOrders()
    {
        if (_orderRows is null)
        {
            return;
        }
        ClearOrderRows();
        foreach (var order in Orders)
        {
            _orderRows.Children.Add(CreateOrderRow(order));
        }
        UpdateOrdersSection();
    }

    private void UpdateOrdersSection()
    {
        if (_ordersSection is not null)
        {
            _ordersSection.IsVisible = Orders.Count > 0;
        }
    }

    private void ClearOrderRows()
    {
        foreach (var (order, handler) in _orderHooks)
        {
            order.Changed -= handler;
        }
        _orderHooks.Clear();
        _orderRows?.Children.Clear();
    }

    private MarketOrderRow CreateOrderRow(MarketOrder order)
    {
        var row = new MarketOrderRow { Order = order };
        ConfigureRow(row, order);
        EventHandler handler = (_, _) => ConfigureRow(row, order);
        order.Changed += handler;
        _orderHooks.Add((order, handler));
        row.CancelRequested += async (_, _) => await CancelOrderAsync(order);
        return row;
    }

    private void ConfigureRow(MarketOrderRow row, MarketOrder order)
    {
        var labels = Labels;
        var title = labels.OrderTitle(order.Side, order.Shares, order.Ticker);
        var price = FinanceFormat.Money(order.Price, "$");
        row.Title = title;
        row.Detail = labels.OrderDetail(order.Type, price, order.Time.ToString("HH:mm", FinanceFormat.Culture));
        row.Status = order.Status;
        row.StatusText = labels.Status(order.Status);
        row.CancelName = labels.CancelOrder(title, price);
        AutomationProperties.SetName(row, $"{row.Title}, {row.Detail}, {row.StatusText}");
    }

    internal IEnumerable<MarketOrderRow> OrderRows => _orderRows?.Children.Select(c => c as MarketOrderRow ?? (c as PresenceHost)?.Child as MarketOrderRow)
        .OfType<MarketOrderRow>() ?? Enumerable.Empty<MarketOrderRow>();

    internal AvaloniaButton? SubmitButton => _submit;
}
