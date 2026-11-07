namespace TKCode.Arc;

/// <summary>
/// One invented charging setup for <see cref="ChargeSession"/>: the names and the numbers a card is configured with.
/// <see cref="ApplyTo"/> copies them onto a card.
/// </summary>
/// <param name="Title">The charger or device name.</param>
/// <param name="Location">The line under the name.</param>
/// <param name="Capacity">Usable capacity in kWh.</param>
/// <param name="MaxPower">Peak power in kW.</param>
/// <param name="StartCharge">Charge in percent where a session starts.</param>
/// <param name="DefaultLimit">The limit in percent to start with.</param>
/// <param name="TaperAt">Charge in percent where the power starts to taper.</param>
/// <param name="Rate">Price per kWh.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
/// <param name="Locale">Locale name for the cost and numbers.</param>
public sealed record ChargeSessionSample(string Title, string Location, double Capacity, double MaxPower, double StartCharge,
    double DefaultLimit, double TaperAt, decimal Rate, string Currency, string Locale)
{
    /// <summary>Copies the setup onto <paramref name="card"/> and starts a new session with it.</summary>
    public void ApplyTo(ChargeSession card)
    {
        ArgumentNullException.ThrowIfNull(card);
        card.Title = Title;
        card.Location = Location;
        card.Capacity = Capacity;
        card.MaxPower = MaxPower;
        card.StartCharge = StartCharge;
        card.TaperAt = TaperAt;
        card.Rate = Rate;
        card.Currency = Currency;
        card.Locale = Locale;
        card.Limit = DefaultLimit;
        card.StartNewSession();
    }
}

/// <summary>Invented setups for the <see cref="ChargeSession"/> demos and tests (no real places or operators).</summary>
public static class ChargeSessionSamples
{
    /// <summary>A rapid public charger: the card's defaults (82 kWh, 180 kW, from 28% to 80%, USD).</summary>
    public static ChargeSessionSample RapidPoint { get; } =
        new("Bay 6 rapid point", "Lantern Quay depot, 180 kW", 82, 180, 28, 80, 55, 0.39m, "USD", "en-US");

    /// <summary>A depot charger for a delivery van, priced in euros with German number formats.</summary>
    public static ChargeSessionSample FleetVan { get; } =
        new("Fleet van 14", "Westbrook yard, 50 kW", 64, 50, 15, 90, 55, 0.31m, "EUR", "de-DE");

    /// <summary>A small device on a desk dock: tenths of a kWh and a very long time to the limit.</summary>
    public static ChargeSessionSample LaptopDock { get; } =
        new("Studio laptop dock", "Desk 3, 0.1 kW", 0.1, 0.1, 20, 100, 80, 0.30m, "GBP", "en-GB");
}
