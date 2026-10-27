namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Invented market for the Market terminal demos: eight made-up companies (none of these tickers or names refers to a real
/// listing) with believable prices and daily moves, a small portfolio, and two earlier orders from this morning.
/// </summary>
public static class MarketTerminalDemoData
{
    public static MarketSymbol[] Symbols() =>
    [
        new("ORBT", "Orbital Freight", 184.53, 182.97, 0.024, 142_000_000),
        new("KLVN", "Kelvin Grid", 64.12, 64.80, 0.018, 22_000_000, "NYSE"),
        new("HALO", "Halo Biosystems", 238.40, 239.05, 0.020, 48_000_000),
        new("MRDN", "Meridian Cloud", 512.70, 508.10, 0.017, 21_000_000),
        new("TIDE", "Tidewater Robotics", 91.36, 88.85, 0.032, 60_000_000),
        new("PYRE", "Pyre Energy", 47.18, 47.60, 0.022, 18_000_000, "NYSE"),
        new("CNVS", "Canvas Commerce", 226.15, 228.30, 0.019, 37_000_000),
        new("LUMA", "Luma Pay", 33.84, 33.42, 0.028, 26_000_000, "NYSE"),
    ];

    public static MarketHolding[] Holdings() =>
    [
        new("ORBT", 120),
        new("MRDN", 15),
        new("TIDE", 40),
    ];

    public static MarketOrder[] Orders(DateTime today) =>
    [
        new(MarketOrderSide.Sell, MarketOrderType.Limit, "MRDN", 5, 520.00, MarketOrderStatus.Working, today.Date.AddHours(11).AddMinutes(18)),
        new(MarketOrderSide.Buy, MarketOrderType.Market, "ORBT", 25, 181.92, MarketOrderStatus.Filled, today.Date.AddHours(10).AddMinutes(42)),
    ];

    public const double BuyingPower = 18_420.50;

    /// <summary>Fills a terminal with the demo market.</summary>
    public static void Fill(MarketTerminal terminal)
    {
        terminal.Symbols.AddRange(Symbols());
        terminal.Holdings.AddRange(Holdings());
        terminal.Orders.AddRange(Orders(DateTime.Today));
        terminal.BuyingPower = BuyingPower;
    }
}
