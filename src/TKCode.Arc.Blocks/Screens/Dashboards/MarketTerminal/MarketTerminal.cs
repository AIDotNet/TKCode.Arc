using System.Collections.Specialized;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A trading terminal screen. A candlestick chart with volume, two moving averages, a crosshair with an open, high, low and
/// close readout, and wheel, drag and pinch zoom sits beside a watchlist with intraday sparklines and flashing live prices,
/// and an order ticket for market and limit orders with an order list. Everything is simulated by default: candles are
/// generated from each symbol's price, previous close and volatility, ticks are a random walk, and orders fill against the
/// live price locally. Set <see cref="PlaceOrderAction"/> and <see cref="CancelOrderAction"/> (and feed prices with
/// <see cref="SetPrice"/>) to connect it to a real service.
/// <code>
/// &lt;arc:MarketTerminal Ticker="ORBT" Range="OneDay" BuyingPower="25000"&gt;
///     &lt;arc:MarketSymbol Ticker="ORBT" Name="Orbital Freight" Price="184.53" PreviousClose="182.97" Volatility="0.024" /&gt;
///     &lt;arc:MarketSymbol Ticker="KLVN" Name="Kelvin Grid" Price="64.12" PreviousClose="64.80" /&gt;
/// &lt;/arc:MarketTerminal&gt;
/// </code>
/// </summary>
[TemplatePart(ChartPart, typeof(MarketChart))]
[TemplatePart(LayoutPart, typeof(Grid))]
[PseudoClasses(WidePseudoClass, MediumPseudoClass, NarrowPseudoClass, CompactPseudoClass)]
public partial class MarketTerminal : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string ChartCardPart = "PART_ChartCard";
    public const string WatchCardPart = "PART_WatchCard";
    public const string TicketCardPart = "PART_TicketCard";
    public const string ChartPart = "PART_Chart";

    /// <summary>From 1080px wide: the watchlist and the ticket stack in a column beside the chart.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>720 to 1080px: the watchlist and the ticket sit side by side under the chart.</summary>
    public const string MediumPseudoClass = ":medium";

    /// <summary>Below 720px: everything stacks.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Below 640px: the stats take their own row, the range switch its own line, and toolbar buttons grow to 44px.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>The symbol charted and traded (two-way); null or unknown charts the first symbol.</summary>
    public static readonly StyledProperty<string?> TickerProperty =
        AvaloniaProperty.Register<MarketTerminal, string?>(nameof(Ticker), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The chart's time range (two-way). One and five days use 5 and 30 minute candles; the rest are daily.</summary>
    public static readonly StyledProperty<MarketRange> RangeProperty =
        AvaloniaProperty.Register<MarketTerminal, MarketRange>(nameof(Range), MarketRange.SixMonths, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the 20 candle moving average is drawn (two-way; the toolbar toggles it).</summary>
    public static readonly StyledProperty<bool> ShowMa20Property =
        AvaloniaProperty.Register<MarketTerminal, bool>(nameof(ShowMa20), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the 50 candle moving average is drawn (two-way; the toolbar toggles it).</summary>
    public static readonly StyledProperty<bool> ShowMa50Property =
        AvaloniaProperty.Register<MarketTerminal, bool>(nameof(ShowMa50), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether simulated ticks stream (two-way; the Live button pauses and resumes them).</summary>
    public static readonly StyledProperty<bool> IsLiveProperty =
        AvaloniaProperty.Register<MarketTerminal, bool>(nameof(IsLive), true, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Time between simulated ticks.</summary>
    public static readonly StyledProperty<TimeSpan> TickIntervalProperty =
        AvaloniaProperty.Register<MarketTerminal, TimeSpan>(nameof(TickInterval), TimeSpan.FromMilliseconds(1200));

    /// <summary>The trading day the simulation treats as today; null uses today's date.</summary>
    public static readonly StyledProperty<DateTime?> SessionDateProperty =
        AvaloniaProperty.Register<MarketTerminal, DateTime?>(nameof(SessionDate));

    /// <summary>Cash available for buys (two-way). The ticket blocks a buy that costs more, and fills update it.</summary>
    public static readonly StyledProperty<double> BuyingPowerProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(BuyingPower), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The ticket's side (two-way).</summary>
    public static readonly StyledProperty<MarketOrderSide> OrderSideProperty =
        AvaloniaProperty.Register<MarketTerminal, MarketOrderSide>(nameof(OrderSide), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The ticket's order type (two-way); a limit order shows the limit price field.</summary>
    public static readonly StyledProperty<MarketOrderType> OrderTypeProperty =
        AvaloniaProperty.Register<MarketTerminal, MarketOrderType>(nameof(OrderType), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The ticket's share count (two-way).</summary>
    public static readonly StyledProperty<int> OrderSharesProperty =
        AvaloniaProperty.Register<MarketTerminal, int>(nameof(OrderShares), 10, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The ticket's limit price (two-way); 0 fills in the live price when the limit field opens.</summary>
    public static readonly StyledProperty<double> LimitPriceProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(LimitPrice), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Places an order with a real service: awaited when the ticket is submitted, while the button shows a spinner. An
    /// exception leaves the order unplaced and writes its message under the button. Null places it locally at once.
    /// </summary>
    public static readonly StyledProperty<Func<MarketOrderRequest, CancellationToken, Task>?> PlaceOrderActionProperty =
        AvaloniaProperty.Register<MarketTerminal, Func<MarketOrderRequest, CancellationToken, Task>?>(nameof(PlaceOrderAction));

    /// <summary>Cancels a working order with a real service; awaited before the order shows as canceled. Null cancels at once.</summary>
    public static readonly StyledProperty<Func<MarketOrder, CancellationToken, Task>?> CancelOrderActionProperty =
        AvaloniaProperty.Register<MarketTerminal, Func<MarketOrder, CancellationToken, Task>?>(nameof(CancelOrderAction));

    /// <summary>The block's built-in text.</summary>
    public static readonly StyledProperty<MarketTerminalLabels> LabelsProperty =
        AvaloniaProperty.Register<MarketTerminal, MarketTerminalLabels>(nameof(Labels), new MarketTerminalLabels());

    internal static readonly StyledProperty<double> WideThresholdProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(WideThreshold), 1080);

    internal static readonly StyledProperty<double> MediumThresholdProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(MediumThreshold), 720);

    internal static readonly StyledProperty<double> CompactThresholdProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(CompactThreshold), 640);

    internal static readonly StyledProperty<double> SideColumnWidthProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(SideColumnWidth), 340);

    internal static readonly StyledProperty<double> ChartHeightProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(ChartHeight), 420);

    internal static readonly StyledProperty<double> CompactChartHeightProperty =
        AvaloniaProperty.Register<MarketTerminal, double>(nameof(CompactChartHeight), 340);

    #endregion

    #region Events

    /// <summary>Raised when the charted symbol changes.</summary>
    public static readonly RoutedEvent<MarketTickerChangedEventArgs> TickerChangedEvent =
        RoutedEvent.Register<MarketTerminal, MarketTickerChangedEventArgs>(nameof(TickerChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an order is placed (it may already be filled, or working).</summary>
    public static readonly RoutedEvent<MarketOrderEventArgs> OrderPlacedEvent =
        RoutedEvent.Register<MarketTerminal, MarketOrderEventArgs>(nameof(OrderPlaced), RoutingStrategies.Bubble);

    /// <summary>Raised when an order fills: a market order right away, a limit order when the price reaches it.</summary>
    public static readonly RoutedEvent<MarketOrderEventArgs> OrderFilledEvent =
        RoutedEvent.Register<MarketTerminal, MarketOrderEventArgs>(nameof(OrderFilled), RoutingStrategies.Bubble);

    /// <summary>Raised when a working order is canceled from the list.</summary>
    public static readonly RoutedEvent<MarketOrderEventArgs> OrderCanceledEvent =
        RoutedEvent.Register<MarketTerminal, MarketOrderEventArgs>(nameof(OrderCanceled), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TickerChangedEvent"/>
    public event EventHandler<MarketTickerChangedEventArgs>? TickerChanged
    {
        add => AddHandler(TickerChangedEvent, value);
        remove => RemoveHandler(TickerChangedEvent, value);
    }

    /// <inheritdoc cref="OrderPlacedEvent"/>
    public event EventHandler<MarketOrderEventArgs>? OrderPlaced
    {
        add => AddHandler(OrderPlacedEvent, value);
        remove => RemoveHandler(OrderPlacedEvent, value);
    }

    /// <inheritdoc cref="OrderFilledEvent"/>
    public event EventHandler<MarketOrderEventArgs>? OrderFilled
    {
        add => AddHandler(OrderFilledEvent, value);
        remove => RemoveHandler(OrderFilledEvent, value);
    }

    /// <inheritdoc cref="OrderCanceledEvent"/>
    public event EventHandler<MarketOrderEventArgs>? OrderCanceled
    {
        add => AddHandler(OrderCanceledEvent, value);
        remove => RemoveHandler(OrderCanceledEvent, value);
    }

    #endregion

    private Grid? _layout;
    private Control? _chartCard;
    private Control? _watchCard;
    private Control? _ticketCard;
    private MarketChart? _chart;
    private FinanceAnnouncer? _announcer;
    private LayoutMode _mode = LayoutMode.None;

    private enum LayoutMode
    {
        None,
        Wide,
        Medium,
        Narrow,
    }

    public MarketTerminal()
    {
        Symbols.CollectionChanged += OnSymbolsChanged;
        Orders.CollectionChanged += OnOrdersChanged;
        Holdings.CollectionChanged += (_, _) => UpdateTicket();
    }

    /// <inheritdoc cref="TickerProperty"/>
    public string? Ticker { get => GetValue(TickerProperty); set => SetValue(TickerProperty, value); }

    /// <inheritdoc cref="RangeProperty"/>
    public MarketRange Range { get => GetValue(RangeProperty); set => SetValue(RangeProperty, value); }

    /// <inheritdoc cref="ShowMa20Property"/>
    public bool ShowMa20 { get => GetValue(ShowMa20Property); set => SetValue(ShowMa20Property, value); }

    /// <inheritdoc cref="ShowMa50Property"/>
    public bool ShowMa50 { get => GetValue(ShowMa50Property); set => SetValue(ShowMa50Property, value); }

    /// <inheritdoc cref="IsLiveProperty"/>
    public bool IsLive { get => GetValue(IsLiveProperty); set => SetValue(IsLiveProperty, value); }

    /// <inheritdoc cref="TickIntervalProperty"/>
    public TimeSpan TickInterval { get => GetValue(TickIntervalProperty); set => SetValue(TickIntervalProperty, value); }

    /// <inheritdoc cref="SessionDateProperty"/>
    public DateTime? SessionDate { get => GetValue(SessionDateProperty); set => SetValue(SessionDateProperty, value); }

    /// <inheritdoc cref="BuyingPowerProperty"/>
    public double BuyingPower { get => GetValue(BuyingPowerProperty); set => SetValue(BuyingPowerProperty, value); }

    /// <inheritdoc cref="OrderSideProperty"/>
    public MarketOrderSide OrderSide { get => GetValue(OrderSideProperty); set => SetValue(OrderSideProperty, value); }

    /// <inheritdoc cref="OrderTypeProperty"/>
    public MarketOrderType OrderType { get => GetValue(OrderTypeProperty); set => SetValue(OrderTypeProperty, value); }

    /// <inheritdoc cref="OrderSharesProperty"/>
    public int OrderShares { get => GetValue(OrderSharesProperty); set => SetValue(OrderSharesProperty, value); }

    /// <inheritdoc cref="LimitPriceProperty"/>
    public double LimitPrice { get => GetValue(LimitPriceProperty); set => SetValue(LimitPriceProperty, value); }

    /// <inheritdoc cref="PlaceOrderActionProperty"/>
    public Func<MarketOrderRequest, CancellationToken, Task>? PlaceOrderAction
    {
        get => GetValue(PlaceOrderActionProperty);
        set => SetValue(PlaceOrderActionProperty, value);
    }

    /// <inheritdoc cref="CancelOrderActionProperty"/>
    public Func<MarketOrder, CancellationToken, Task>? CancelOrderAction
    {
        get => GetValue(CancelOrderActionProperty);
        set => SetValue(CancelOrderActionProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public MarketTerminalLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    internal double WideThreshold { get => GetValue(WideThresholdProperty); set => SetValue(WideThresholdProperty, value); }
    internal double MediumThreshold { get => GetValue(MediumThresholdProperty); set => SetValue(MediumThresholdProperty, value); }
    internal double CompactThreshold { get => GetValue(CompactThresholdProperty); set => SetValue(CompactThresholdProperty, value); }
    internal double SideColumnWidth { get => GetValue(SideColumnWidthProperty); set => SetValue(SideColumnWidthProperty, value); }
    internal double ChartHeight { get => GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }
    internal double CompactChartHeight { get => GetValue(CompactChartHeightProperty); set => SetValue(CompactChartHeightProperty, value); }

    /// <summary>The watchlist, in order (a collection change rebuilds the simulation).</summary>
    [Content]
    public AvaloniaList<MarketSymbol> Symbols { get; } = new();

    /// <summary>Shares held per ticker; fills update them.</summary>
    public AvaloniaList<MarketHolding> Holdings { get; } = new();

    /// <summary>The order list, newest first; placed orders are inserted at the top.</summary>
    public AvaloniaList<MarketOrder> Orders { get; } = new();

    /// <summary>The symbol charted: <see cref="Ticker"/> when it is in <see cref="Symbols"/>, else the first symbol.</summary>
    public MarketSymbol? EffectiveSymbol =>
        Symbols.FirstOrDefault(s => s.Ticker == Ticker) ?? (Symbols.Count > 0 ? Symbols[0] : null);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _layout = scope.Find<Grid>(LayoutPart);
        _chartCard = scope.Find<Control>(ChartCardPart);
        _watchCard = scope.Find<Control>(WatchCardPart);
        _ticketCard = scope.Find<Control>(TicketCardPart);
        _chart = scope.Find<MarketChart>(ChartPart);
        _announcer = scope.Find<FinanceAnnouncer>("PART_Announcer");
        HookParts(scope);
        _mode = LayoutMode.None;
        ApplyLayout();
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BoundsProperty || property == WideThresholdProperty || property == MediumThresholdProperty
            || property == CompactThresholdProperty || property == SideColumnWidthProperty || property == ChartHeightProperty
            || property == CompactChartHeightProperty)
        {
            ApplyLayout();
        }
        else if (property == TickerProperty)
        {
            OnTickerChanged(change.GetOldValue<string?>());
        }
        else if (property == RangeProperty)
        {
            ShowSeries(animate: true);
            SyncToolbar();
        }
        else if (property == ShowMa20Property || property == ShowMa50Property)
        {
            SyncToolbar();
        }
        else if (property == IsLiveProperty || property == TickIntervalProperty)
        {
            UpdateLiveState();
        }
        else if (property == SessionDateProperty)
        {
            Rebuild();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
            RebuildWatchlist();
            RebuildOrders();
            UpdateAll();
        }
        else if (property == BuyingPowerProperty || property == OrderSharesProperty || property == LimitPriceProperty)
        {
            if (property != BuyingPowerProperty)
            {
                // Editing the order clears the last failure.
                _error = null;
            }
            SyncTicketInputs();
            UpdateTicket();
        }
        else if (property == OrderSideProperty || property == OrderTypeProperty)
        {
            OnTicketModeChanged(property == OrderTypeProperty);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLiveState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Ticks pause while the block is not on screen.
        UpdateLiveState();
        _placing?.Cancel();
    }

    private void OnSymbolsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void ApplyLayout()
    {
        if (_layout is null || _chartCard is null || _watchCard is null || _ticketCard is null)
        {
            return;
        }
        var width = Bounds.Width;
        var mode = width <= 0 ? LayoutMode.Medium : width >= WideThreshold ? LayoutMode.Wide : width >= MediumThreshold ? LayoutMode.Medium : LayoutMode.Narrow;
        var compact = width > 0 && width < CompactThreshold;
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (_chart is not null)
        {
            _chart.Height = mode == LayoutMode.Wide ? double.NaN : compact ? CompactChartHeight : ChartHeight;
            _chart.MinHeight = mode == LayoutMode.Wide ? ChartHeight : 0;
        }
        if (mode == _mode)
        {
            return;
        }
        _mode = mode;
        PseudoClasses.Set(WidePseudoClass, mode == LayoutMode.Wide);
        PseudoClasses.Set(MediumPseudoClass, mode == LayoutMode.Medium);
        PseudoClasses.Set(NarrowPseudoClass, mode == LayoutMode.Narrow);
        _layout.ColumnDefinitions.Clear();
        _layout.RowDefinitions.Clear();
        switch (mode)
        {
            case LayoutMode.Wide:
                _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(SideColumnWidth)));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                Place(_chartCard, 0, 0, rowSpan: 2);
                Place(_watchCard, 0, 1);
                Place(_ticketCard, 1, 1);
                break;
            case LayoutMode.Medium:
                _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Place(_chartCard, 0, 0, columnSpan: 2);
                Place(_watchCard, 1, 0);
                Place(_ticketCard, 1, 1);
                break;
            default:
                _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Place(_chartCard, 0, 0);
                Place(_watchCard, 1, 0);
                Place(_ticketCard, 2, 0);
                break;
        }
    }

    private static void Place(Control control, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetRowSpan(control, rowSpan);
        Grid.SetColumnSpan(control, columnSpan);
    }
}
