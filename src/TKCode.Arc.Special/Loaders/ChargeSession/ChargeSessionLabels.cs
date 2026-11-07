namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="ChargeSession"/> shows or announces, in English by default. Replace the instance (or set its
/// properties) to translate or reword; texts with numbers are functions so that the word order can change. Numbers arrive
/// already formatted for the card's <see cref="ChargeSession.Locale"/> ("80%", "150 kW", "22.8 kWh", "$9.57") and durations
/// in minutes of charging at full power.
/// </summary>
public sealed class ChargeSessionLabels
{
    /// <summary>The status line while charging.</summary>
    public string Charging { get; set; } = "Charging";

    /// <summary>The status line after Stop.</summary>
    public string Stopped { get; set; } = "Stopped";

    /// <summary>The status line once the limit is reached.</summary>
    public string Complete { get; set; } = "Limit reached";

    /// <summary>The action button while charging.</summary>
    public string Stop { get; set; } = "Stop charging";

    /// <summary>The action button after Stop.</summary>
    public string Resume { get; set; } = "Resume charging";

    /// <summary>The action button once the limit is reached.</summary>
    public string NewSession { get; set; } = "Start new session";

    /// <summary>The heading over the curve, also its accessible name.</summary>
    public string CurveTitle { get; set; } = "Charge curve";

    /// <summary>The reading beside the curve heading: power and charge, "150 kW at 40%".</summary>
    public Func<string, string, string> CurveReading { get; set; } = (power, charge) => $"{power} at {charge}";

    /// <summary>A percentage: the whole number, "80%".</summary>
    public Func<string, string> Percent { get; set; } = value => $"{value}%";

    /// <summary>A power: the number, "150 kW".</summary>
    public Func<string, string> Power { get; set; } = value => $"{value} kW";

    /// <summary>An energy: the number with one decimal, "22.8 kWh".</summary>
    public Func<string, string> Energy { get; set; } = value => $"{value} kWh";

    /// <summary>The power stat's name.</summary>
    public string PowerStat { get; set; } = "Power";

    /// <summary>The energy stat's name.</summary>
    public string EnergyStat { get; set; } = "Energy added";

    /// <summary>The time stat's name: the limit, "Time to 80%".</summary>
    public Func<string, string> TimeStat { get; set; } = limit => $"Time to {limit}";

    /// <summary>The cost stat's name.</summary>
    public string CostStat { get; set; } = "Cost";

    /// <summary>The time stat while stopped.</summary>
    public string TimePaused { get; set; } = "Paused";

    /// <summary>The time stat once the limit is reached.</summary>
    public string TimeDone { get; set; } = "Done";

    /// <summary>
    /// The time stat while charging: minutes at full power, "Under 1 min", "23 min", "1 h 05 min" (minutes rounded, two
    /// digits after the hours).
    /// </summary>
    public Func<double, string> Time { get; set; } = minutes =>
    {
        if (minutes < 1)
        {
            return "Under 1 min";
        }
        var whole = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        return whole < 60 ? $"{whole} min" : $"{whole / 60} h {whole % 60:00} min";
    };

    /// <summary>
    /// The time left in words for the limit's value text and announcement: "under a minute", "about 1 minute", "about 23
    /// minutes", "about 1 hour 5 minutes".
    /// </summary>
    public Func<double, string> Remaining { get; set; } = minutes =>
    {
        if (minutes < 1)
        {
            return "under a minute";
        }
        var whole = (int)Math.Round(minutes, MidpointRounding.AwayFromZero);
        if (whole < 60)
        {
            return whole == 1 ? "about 1 minute" : $"about {whole} minutes";
        }
        var hours = whole / 60;
        var rest = whole % 60;
        var h = hours == 1 ? "1 hour" : $"{hours} hours";
        return rest switch
        {
            0 => $"about {h}",
            1 => $"about {h} 1 minute",
            _ => $"about {h} {rest} minutes",
        };
    };

    /// <summary>The hint under the card, which also describes the limit marker.</summary>
    public string Hint { get; set; } = "Drag on the battery or the curve to choose where charging stops.";

    /// <summary>The limit marker's accessible name.</summary>
    public string Limit { get; set; } = "Charge limit";

    /// <summary>
    /// The limit marker's value text: the limit and, while it is above the charge, the time left in words (else null),
    /// "Charge limit 80%, about 23 minutes to go".
    /// </summary>
    public Func<string, string?, string> LimitValue { get; set; } =
        (limit, remaining) => remaining is null ? $"Charge limit {limit}" : $"Charge limit {limit}, {remaining} to go";

    /// <summary>Announced on Stop: charge, energy and cost, "Stopped at 63%. 22.8 kWh added, costing $9.57.".</summary>
    public Func<string, string, string, string> StoppedAnnouncement { get; set; } =
        (charge, energy, cost) => $"Stopped at {charge}. {energy} added, costing {cost}.";

    /// <summary>Announced on Resume or when a raised limit restarts charging: the limit, "Charging again, up to 90%.".</summary>
    public Func<string, string> ResumedAnnouncement { get; set; } = limit => $"Charging again, up to {limit}.";

    /// <summary>Announced when the limit is reached: charge, energy and cost, "Reached 80%. 41.0 kWh added, costing $15.99.".</summary>
    public Func<string, string, string, string> CompleteAnnouncement { get; set; } =
        (charge, energy, cost) => $"Reached {charge}. {energy} added, costing {cost}.";

    /// <summary>Announced on Start new session: start charge and limit, "New session from 28%, up to 80%.".</summary>
    public Func<string, string, string> NewSessionAnnouncement { get; set; } =
        (start, limit) => $"New session from {start}, up to {limit}.";

    /// <summary>
    /// Announced when a limit change settles: the limit and, while it is above the charge, the time left in words (else
    /// null), "Limit set to 90%. About 31 minutes to go at full power.".
    /// </summary>
    public Func<string, string?, string> LimitAnnouncement { get; set; } = (limit, remaining) =>
        remaining is null
            ? $"Limit set to {limit}."
            : $"Limit set to {limit}. {char.ToUpperInvariant(remaining[0])}{remaining[1..]} to go at full power.";
}
