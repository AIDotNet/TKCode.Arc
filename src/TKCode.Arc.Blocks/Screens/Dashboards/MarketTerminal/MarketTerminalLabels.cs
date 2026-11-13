namespace TKCode.Arc;

/// <summary>
/// The built-in text of <see cref="MarketTerminal"/>. Replace the whole object to localize the block; parameterized entries
/// are functions that receive already formatted values. Times and numbers use en-US formats.
/// </summary>
public sealed class MarketTerminalLabels
{
    /// <summary>The range switch's labels, by <see cref="MarketRange"/>: "1D", "5D", "1M", "6M", "1Y".</summary>
    public Func<MarketRange, string> RangeLabel { get; init; } = range => range switch
    {
        MarketRange.OneDay => "1D",
        MarketRange.FiveDays => "5D",
        MarketRange.OneMonth => "1M",
        MarketRange.SixMonths => "6M",
        _ => "1Y",
    };

    /// <summary>The range in words, for the chart's screen reader name: "six months".</summary>
    public Func<MarketRange, string> RangeName { get; init; } = range => range switch
    {
        MarketRange.OneDay => "one day",
        MarketRange.FiveDays => "five days",
        MarketRange.OneMonth => "one month",
        MarketRange.SixMonths => "six months",
        _ => "one year",
    };

    /// <summary>The candle size in words: "5 minute candles", "30 minute candles", "daily candles".</summary>
    public Func<MarketRange, string> Interval { get; init; } = range => range switch
    {
        MarketRange.OneDay => "5 minute candles",
        MarketRange.FiveDays => "30 minute candles",
        _ => "daily candles",
    };

    /// <summary>The chart's screen reader name: (ticker, range, interval) ⇒ "ORBT price chart, six months of daily candles".</summary>
    public Func<string, string, string, string> ChartName { get; init; } = (ticker, range, interval) => $"{ticker} price chart, {range} of {interval}";

    /// <summary>The chart's screen reader help: which keys do what.</summary>
    public string ChartHelp { get; init; } =
        "Left and right arrows step through candles, with Shift ten at a time. Home and End go to the first and the latest candle. Plus and minus zoom, 0 resets the view, Escape clears the crosshair.";

    /// <summary>The time in the readout: "Sep 24, 2026" for daily candles, "Sep 24, 11:35" intraday.</summary>
    public Func<DateTime, MarketRange, string> ReadoutTime { get; init; } = (time, range) => range is MarketRange.OneDay or MarketRange.FiveDays
        ? time.ToString("MMM d, HH:mm", FinanceFormat.Culture)
        : time.ToString("MMM d, yyyy", FinanceFormat.Culture);

    /// <summary>The time axis labels: "11:00" for one day, "Sep 22" for five days and one month, "Sep" beyond.</summary>
    public Func<DateTime, MarketRange, string> AxisTime { get; init; } = (time, range) => range switch
    {
        MarketRange.OneDay => time.ToString("HH:00", FinanceFormat.Culture),
        MarketRange.FiveDays or MarketRange.OneMonth => time.ToString("MMM d", FinanceFormat.Culture),
        _ => time.ToString("MMM", FinanceFormat.Culture),
    };

    /// <summary>The crosshair's time tag: "11:35" for one day, "Sep 22, 11:00" for five days, "Sep 22" beyond.</summary>
    public Func<DateTime, MarketRange, string> CrosshairTime { get; init; } = (time, range) => range switch
    {
        MarketRange.OneDay => time.ToString("HH:mm", FinanceFormat.Culture),
        MarketRange.FiveDays => time.ToString("MMM d, HH:mm", FinanceFormat.Culture),
        _ => time.ToString("MMM d", FinanceFormat.Culture),
    };

    /// <summary>Read when the crosshair moves by keyboard.</summary>
    public Func<MarketCandleDescription, string> CandleAnnouncement { get; init; } = c =>
        $"{c.Time}. Open {FinanceFormat.Price(c.Open)}, high {FinanceFormat.Price(c.High)}, low {FinanceFormat.Price(c.Low)}, close {FinanceFormat.Price(c.Close)}, "
        + $"{(c.Change >= 0 ? "up" : "down")} {FinanceFormat.Percent(c.Change, 2)}, volume {FinanceFormat.Volume(c.Volume)}.";

    public string VolumeShort { get; init; } = "Vol";

    public string Ma20 { get; init; } = "MA 20";

    public string Ma50 { get; init; } = "MA 50";

    /// <summary>The live button's text.</summary>
    public string Live { get; init; } = "Live";

    /// <summary>Screen reader names of the live button while streaming and while paused.</summary>
    public string PauseLive { get; init; } = "Pause live prices";

    public string ResumeLive { get; init; } = "Resume live prices";

    /// <summary>After today's change: "today".</summary>
    public string Today { get; init; } = "today";

    public string Open { get; init; } = "Open";

    public string High { get; init; } = "High";

    public string Low { get; init; } = "Low";

    public string Volume { get; init; } = "Volume";

    /// <summary>Screen reader names of the toolbar groups and buttons.</summary>
    public string RangeGroup { get; init; } = "Time range";

    public string AverageGroup { get; init; } = "Moving averages";

    public string ZoomIn { get; init; } = "Zoom in";

    public string ZoomOut { get; init; } = "Zoom out";

    public string ResetView { get; init; } = "Reset view";

    public string Watchlist { get; init; } = "Watchlist";

    /// <summary>Next to the watchlist's title: "8 symbols".</summary>
    public Func<int, string> SymbolCount { get; init; } = count => count == 1 ? "1 symbol" : $"{count} symbols";

    /// <summary>A watchlist row's screen reader name: (ticker, name, price, change today) ⇒ "ORBT, Orbital Freight, 184.53, up 0.85 percent today".</summary>
    public Func<string, string, string, double, string> RowName { get; init; } = (ticker, name, price, change) =>
        $"{ticker}, {name}, {price}, {(change >= 0 ? "up" : "down")} {Math.Abs(change * 100).ToString("0.00", FinanceFormat.Culture)} percent today";

    /// <summary>The ticket's title: ticker ⇒ "Trade ORBT".</summary>
    public Func<string, string> Trade { get; init; } = ticker => $"Trade {ticker}";

    /// <summary>Next to the ticket's title: shares ⇒ "120 shares held", "No shares held".</summary>
    public Func<int, string> SharesHeld { get; init; } = shares => shares switch
    {
        0 => "No shares held",
        1 => "1 share held",
        _ => $"{FinanceFormat.Count(shares)} shares held",
    };

    public string Buy { get; init; } = "Buy";

    public string Sell { get; init; } = "Sell";

    public string Market { get; init; } = "Market";

    public string Limit { get; init; } = "Limit";

    public string SideGroup { get; init; } = "Side";

    public string TypeGroup { get; init; } = "Order type";

    public string Shares { get; init; } = "Shares";

    public string LimitPrice { get; init; } = "Limit price";

    public string MarketPrice { get; init; } = "Market price";

    public string EstimatedCost { get; init; } = "Estimated cost";

    public string EstimatedProceeds { get; init; } = "Estimated proceeds";

    public string BuyingPower { get; init; } = "Buying power";

    public string Position { get; init; } = "Shares held";

    /// <summary>The submit button: (side, shares, ticker) ⇒ "Buy 10 ORBT".</summary>
    public Func<MarketOrderSide, int, string, string> Submit { get; init; } = (side, shares, ticker) =>
        $"{(side == MarketOrderSide.Buy ? "Buy" : "Sell")} {FinanceFormat.Count(shares)} {ticker}";

    /// <summary>Under the submit button.</summary>
    public string Disclaimer { get; init; } = "Simulated trading: prices and orders are not real.";

    /// <summary>Problems written under the button instead of placing the order.</summary>
    public string TooFewShares { get; init; } = "Enter at least one share.";

    public string NoPrice { get; init; } = "Enter a limit price above zero.";

    /// <summary>A buy costs more than the buying power: the formatted buying power ⇒ "Not enough buying power. You have $2,400.00.".</summary>
    public Func<string, string> NotEnoughBuyingPower { get; init; } = power => $"Not enough buying power. You have {power}.";

    /// <summary>A sell asks for more than is held: (held, ticker) ⇒ "You hold 120 ORBT.".</summary>
    public Func<int, string, string> NotEnoughShares { get; init; } = (held, ticker) => held == 0 ? $"You hold no {ticker}." : $"You hold only {FinanceFormat.Count(held)} {ticker}.";

    /// <summary>The order callback failed: the exception's message, or this when it has none.</summary>
    public string OrderFailed { get; init; } = "The order did not go through. Try again.";

    public string Orders { get; init; } = "Orders";

    /// <summary>An order's title: (side, shares, ticker) ⇒ "Sell 4 ORBT".</summary>
    public Func<MarketOrderSide, int, string, string> OrderTitle { get; init; } = (side, shares, ticker) =>
        $"{(side == MarketOrderSide.Buy ? "Buy" : "Sell")} {FinanceFormat.Count(shares)} {ticker}";

    /// <summary>An order's detail: (type, price, time) ⇒ "Limit at $516.00, 11:18".</summary>
    public Func<MarketOrderType, string, string, string> OrderDetail { get; init; } = (type, price, time) =>
        $"{(type == MarketOrderType.Market ? "Market" : "Limit")} at {price}, {time}";

    public Func<MarketOrderStatus, string> Status { get; init; } = status => status switch
    {
        MarketOrderStatus.Working => "Working",
        MarketOrderStatus.Filled => "Filled",
        _ => "Canceled",
    };

    /// <summary>The cancel button's screen reader name: (title, price) ⇒ "Cancel sell 4 ORBT at $516.00".</summary>
    public Func<string, string, string> CancelOrder { get; init; } = (title, price) =>
        $"Cancel {(title.Length > 0 ? char.ToLowerInvariant(title[0]) + title[1..] : title)} at {price}";

    /// <summary>Announced when an order is placed, fills or is canceled: (title, status) ⇒ "Buy 10 ORBT: Filled.".</summary>
    public Func<string, string, string> OrderAnnouncement { get; init; } = (title, status) => $"{title}: {status}.";
}
