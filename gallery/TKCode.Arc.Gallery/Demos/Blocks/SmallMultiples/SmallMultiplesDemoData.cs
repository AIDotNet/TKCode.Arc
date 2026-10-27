using System.Globalization;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The SmallMultiples demos' data (all invented): monthly net revenue of eight markets over 24 months, November 2024 to
/// October 2026, in thousands of US dollars (three of them flat, one falling); and weekly signups of four plans over 26
/// weeks. The curves are closed-form (a trend, a yearly season and a little deterministic wobble), so every run draws
/// the same chart.
/// </summary>
public static class SmallMultiplesDemoData
{
    public const string Title = "Revenue by market";

    public const string Description = "Monthly net revenue, November 2024 to October 2026";

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>24 months from November 2024, oldest first.</summary>
    public static SmallMultiplesPeriod[] Months()
    {
        var start = new DateTime(2024, 11, 1);
        return Enumerable.Range(0, 24)
            .Select(i => start.AddMonths(i))
            .Select(d => new SmallMultiplesPeriod(d.ToString("MMM yyyy", s_culture), d.ToString("MMMM yyyy", s_culture)))
            .ToArray();
    }

    /// <summary>The eight markets (key, name, start in $k, growth over the two years, season, wobble seed).</summary>
    public static SmallMultiplesSeries[] Markets() =>
    [
        Market("us", "United States", 1180, 0.40, 0.035, 1),
        Market("ca", "Canada", 455, 0.21, 0.05, 2),
        Market("es", "Spain", 520, 0.02, 0.09, 3),
        Market("mx", "Mexico", 168, 1.15, 0.04, 4),
        Market("in", "India", 228, 0.62, 0.03, 5),
        Market("sg", "Singapore", 306, -0.03, 0.08, 6),
        Market("za", "South Africa", 318, -0.27, 0.03, 7),
        Market("nz", "New Zealand", 188, 0.01, 0.07, 8),
    ];

    private static SmallMultiplesSeries Market(string key, string name, double start, double growth, double season, int seed)
    {
        var values = new double[24];
        for (var i = 0; i < 24; i++)
        {
            var trend = 1 + growth * i / 23;
            // A yearly season (it cancels out at a 12-month lag, so growth reads the trend) and a small wobble.
            var yearly = 1 + season * Math.Sin(2 * Math.PI * (i + seed) / 12);
            var wobble = 1 + 0.018 * Math.Sin(i * 1.7 + seed * 2.3) + 0.012 * Math.Sin(i * 3.1 + seed);
            values[i] = Math.Round(start * trend * yearly * wobble, 1);
        }
        return new SmallMultiplesSeries(key, name, values);
    }

    /// <summary>26 weeks ending the week of 19 October 2026, labeled by the Monday.</summary>
    public static SmallMultiplesPeriod[] Weeks()
    {
        var start = new DateTime(2026, 4, 27);
        return Enumerable.Range(0, 26)
            .Select(i => start.AddDays(i * 7))
            .Select(d => new SmallMultiplesPeriod(d.ToString("MMM d", s_culture), "Week of " + d.ToString("MMMM d, yyyy", s_culture)))
            .ToArray();
    }

    /// <summary>Weekly signups of four plans.</summary>
    public static SmallMultiplesSeries[] Plans() =>
    [
        Plan("free", "Free", 4200, 0.35, 1),
        Plan("starter", "Starter", 1320, 0.62, 2),
        Plan("team", "Team", 610, 0.03, 3),
        Plan("business", "Business", 145, -0.18, 4),
    ];

    private static SmallMultiplesSeries Plan(string key, string name, double start, double growth, int seed)
    {
        var values = new double[26];
        for (var i = 0; i < 26; i++)
        {
            var trend = 1 + growth * i / 25;
            var wobble = 1 + 0.05 * Math.Sin(i * 1.3 + seed) + 0.03 * Math.Sin(i * 2.9 + seed * 1.7);
            values[i] = Math.Round(start * trend * wobble);
        }
        return new SmallMultiplesSeries(key, name, values);
    }

    /// <summary>Whole signups with group separators ("4,812").</summary>
    public static string Signups(double value) => value.ToString("#,0", s_culture);

    /// <summary>Compact signups for the axis ("4.8k", "600").</summary>
    public static string SignupTicks(double value) =>
        value >= 1000 ? (value / 1000).ToString("0.#", s_culture) + "k" : value.ToString("0", s_culture);
}
