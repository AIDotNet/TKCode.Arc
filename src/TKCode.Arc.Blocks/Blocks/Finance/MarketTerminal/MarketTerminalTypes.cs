using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The time range of the <see cref="MarketTerminal"/> chart.</summary>
public enum MarketRange
{
    /// <summary>Today, in 5 minute candles.</summary>
    OneDay,

    /// <summary>The last five sessions, in 30 minute candles.</summary>
    FiveDays,

    /// <summary>About a month of daily candles.</summary>
    OneMonth,

    /// <summary>About six months of daily candles.</summary>
    SixMonths,

    /// <summary>About a year of daily candles.</summary>
    OneYear,
}

/// <summary>Which side of the market an order takes.</summary>
public enum MarketOrderSide
{
    Buy,
    Sell,
}

/// <summary>How an order is priced.</summary>
public enum MarketOrderType
{
    /// <summary>Fills at the live price.</summary>
    Market,

    /// <summary>Fills only at the limit price or better; waits (working) until the price reaches it.</summary>
    Limit,
}

/// <summary>Where an order stands.</summary>
public enum MarketOrderStatus
{
    /// <summary>A limit order waiting for the price to reach it.</summary>
    Working,

    /// <summary>Done at <see cref="MarketOrder.Price"/>.</summary>
    Filled,

    /// <summary>Canceled before it filled.</summary>
    Canceled,
}

/// <summary>
/// A symbol of the <see cref="MarketTerminal"/> watchlist. Its candles are generated from these numbers, the same way every
/// time for the same ticker and range, and then move with the simulated live price.
/// </summary>
public sealed class MarketSymbol : AvaloniaObject
{
    public MarketSymbol()
    {
    }

    public MarketSymbol(string ticker, string name, double price, double previousClose, double volatility = 0.02,
        double averageVolume = 20_000_000, string exchange = "Nasdaq", string currency = "USD")
    {
        Ticker = ticker;
        Name = name;
        Price = price;
        PreviousClose = previousClose;
        Volatility = volatility;
        AverageVolume = averageVolume;
        Exchange = exchange;
        Currency = currency;
    }

    /// <summary>The symbol ("ORBT").</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>The company name ("Orbital Freight").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The exchange shown under the name.</summary>
    public string Exchange { get; set; } = "Nasdaq";

    /// <summary>The currency shown after the exchange.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>The price right now (where today's candles end).</summary>
    public double Price { get; set; }

    /// <summary>Yesterday's close: the base of today's change.</summary>
    public double PreviousClose { get; set; }

    /// <summary>Typical daily move as a fraction (0.02 = 2%); drives the generated candles and the live ticks.</summary>
    public double Volatility { get; set; } = 0.02;

    /// <summary>Typical shares traded per day.</summary>
    public double AverageVolume { get; set; } = 20_000_000;
}

/// <summary>Shares held of one ticker; the ticket blocks selling more than this.</summary>
public sealed class MarketHolding : AvaloniaObject
{
    public MarketHolding()
    {
    }

    public MarketHolding(string ticker, int shares)
    {
        Ticker = ticker;
        Shares = shares;
    }

    /// <summary>The symbol held.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>How many shares.</summary>
    public int Shares { get; set; }
}

/// <summary>An order in the <see cref="MarketTerminal"/> order list.</summary>
public sealed class MarketOrder : AvaloniaObject
{
    /// <summary>Raised on the order when its status or price changes (a working order filled, an order canceled).</summary>
    public event EventHandler? Changed;

    public MarketOrder()
    {
    }

    public MarketOrder(MarketOrderSide side, MarketOrderType type, string ticker, int shares, double price, MarketOrderStatus status,
        DateTime time)
    {
        Side = side;
        Type = type;
        Ticker = ticker;
        Shares = shares;
        Price = price;
        Status = status;
        Time = time;
    }

    /// <summary>A key for the order (generated when not given).</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public MarketOrderSide Side { get; set; }

    public MarketOrderType Type { get; set; }

    public string Ticker { get; set; } = string.Empty;

    public int Shares { get; set; }

    /// <summary>The fill price of a filled order, the limit of a working or canceled limit order.</summary>
    public double Price { get; set; }

    public MarketOrderStatus Status { get; set; }

    /// <summary>When the order was placed (the list shows the time of day).</summary>
    public DateTime Time { get; set; }

    internal void Update(MarketOrderStatus status, double price)
    {
        Status = status;
        Price = price;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>What the ticket submits: passed to <see cref="MarketTerminal.PlaceOrderAction"/> and <see cref="MarketTerminal.OrderPlaced"/>.</summary>
/// <param name="Side">Buy or sell.</param>
/// <param name="Type">Market or limit.</param>
/// <param name="Ticker">The symbol.</param>
/// <param name="Shares">How many shares (at least one).</param>
/// <param name="Price">The live price for a market order, the limit for a limit order.</param>
public sealed record MarketOrderRequest(MarketOrderSide Side, MarketOrderType Type, string Ticker, int Shares, double Price);

/// <summary>Arguments of <see cref="MarketTerminal.TickerChanged"/>.</summary>
public sealed class MarketTickerChangedEventArgs : RoutedEventArgs
{
    public MarketTickerChangedEventArgs(RoutedEvent routedEvent, string? oldTicker, string? newTicker) : base(routedEvent)
    {
        OldTicker = oldTicker;
        NewTicker = newTicker;
    }

    /// <summary>The symbol charted before.</summary>
    public string? OldTicker { get; }

    /// <summary>The symbol charted now.</summary>
    public string? NewTicker { get; }
}

/// <summary>Arguments of <see cref="MarketTerminal.OrderPlaced"/>, <see cref="MarketTerminal.OrderFilled"/> and <see cref="MarketTerminal.OrderCanceled"/>.</summary>
public sealed class MarketOrderEventArgs : RoutedEventArgs
{
    public MarketOrderEventArgs(RoutedEvent routedEvent, MarketOrder order) : base(routedEvent)
    {
        Order = order;
    }

    /// <summary>The order (its <see cref="MarketOrder.Status"/> says where it stands).</summary>
    public MarketOrder Order { get; }
}
