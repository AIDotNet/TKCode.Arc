using System.Globalization;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>A teammate as the overview shows them: who is online, what they focus on and how full their week is.</summary>
internal sealed record OverviewPerson(string Id, string Name, string Role, bool Online, string Availability, string Focus, int Hours,
                                      string Place, int Reviews, string Bio);

/// <summary>A step of a project, done or upcoming.</summary>
internal sealed record OverviewMilestone(string Label, DateTime Date, bool Done = false);

/// <summary>A row of the overview's projects table (and its drawer). Status changes replace the record.</summary>
internal sealed record OverviewProject(string Id, string Name, string Client, string Lead, string[] Team, DemoProjectStatus Status,
                                       int Progress, DateTime Due, int Budget, int Spent, string Summary, OverviewMilestone[] Milestones);

/// <summary>One of the hero's ranges (Week, Month, Quarter) and the texts that follow it.</summary>
internal sealed record OverviewRange(string Id, string Label, string Span, string Short, string Compare, string Period, string[] PointLabels);

/// <summary>A key metric's reading over one range.</summary>
internal sealed record OverviewReading(double Value, string Delta, bool Rising, bool Good, double[] Series);

/// <summary>A tile of the key metrics strip.</summary>
internal sealed record OverviewMetric(string Label, string Prefix, string Suffix, int Decimals, Func<double, string> Format,
                                      IReadOnlyDictionary<string, OverviewReading> Readings);

/// <summary>A day or week of logged hours.</summary>
internal sealed record OverviewHours(DateTime Day, int All, int Billable);

/// <summary>
/// The overview's own data (uiarc.dev's "studio" module): its people and projects carry more than the shared
/// <see cref="DemoData"/> ones (online state, hours, budgets, milestones), plus the metrics, the hours and the activity year.
/// All of it is invented; the generated series reproduce uiarc.dev's seeded PRNG exactly.
/// </summary>
internal static class OverviewData
{
    public static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    public const int Capacity = 40;

    private const string Minus = "−";
    private const string Nbsp = " ";

    public static IReadOnlyList<OverviewPerson> People { get; } =
    [
        new("emma", "Emma Collins", "Design director", true, "Available", "atlas", 36, "Lisbon · 4:10 PM", 24,
            "Leads product work across the studio and runs the Tuesday critique."),
        new("marcus", "Marcus Johnson", "Senior product designer", true, "Available", "northwind", 43, "Brooklyn · 11:10 AM", 18,
            "Owns the Northwind onboarding and prototypes every flow before it ships."),
        new("ava", "Ava Mitchell", "Research lead", false, "In interviews until 5 PM", "tidewater", 30, "London · 4:10 PM", 11,
            "Runs discovery and usability studies, and turns interviews into decisions."),
        new("jasmine", "Jasmine Brooks", "Design engineer", true, "Available", "harbor", 38, "Toronto · 11:10 AM", 15,
            "Builds the Harbor component library and keeps tokens in step with code."),
        new("sofia", "Sofia Ramirez", "Brand designer", false, "Out until Thursday", "fieldnotes", 16, "Barcelona · 5:10 PM", 9,
            "Shapes identities from first sketch to the final logo suite."),
        new("ryan", "Ryan Sullivan", "Motion designer", true, "Available", "atlas", 34, "Berlin · 5:10 PM", 7,
            "Designs the moments between screens: transitions, launches, and loaders."),
        new("olivia", "Olivia Bennett", "Content designer", true, "Available", "northwind", 29, "Mexico City · 9:10 AM", 13,
            "Writes interface copy that sounds like a person, not a policy."),
        new("chloe", "Chloe Nguyen", "Product designer", false, "Offline until morning", "harbor", 39, "Tokyo · 12:10 AM", 16,
            "Designs dense data tools and audits components for accessibility."),
        new("hannah", "Hannah Walsh", "Producer", true, "Available", "northwind", 32, "Lagos · 4:10 PM", 6,
            "Keeps timelines honest and clients informed, one weekly note at a time."),
        new("daniel", "Daniel Kim", "Design technologist", false, "Out sick today", "harbor", 8, "Milan · 5:10 PM", 5,
            "Bridges Figma and code with prototypes that feel like the real thing.")
    ];

    public static OverviewPerson Person(string id) => People.First(person => person.Id == id);

    private static DateTime Sep(int day) => new(2026, 9, day);

    public static IReadOnlyList<OverviewProject> Projects { get; } =
    [
        new("northwind", "Northwind app", "Northwind", "marcus", ["marcus", "olivia", "ryan", "hannah"], DemoProjectStatus.AtRisk, 54,
            Sep(30), 62500, 41200, "Onboarding is six days behind plan after the scope change on September 8.",
            [
                new("Research synthesis", new DateTime(2026, 8, 14), true), new("Information architecture", new DateTime(2026, 8, 28), true),
                new("Onboarding flows", Sep(25)), new("Usability test", Sep(28)), new("Handoff", Sep(30))
            ]),
        new("atlas", "Atlas platform", "Orbit Labs", "emma", ["emma", "jasmine", "chloe", "ryan"], DemoProjectStatus.OnTrack, 72,
            new DateTime(2026, 10, 9), 48200, 30100, "Checkout flows were approved yesterday. Motion specs are next.",
            [
                new("Checkout flows", Sep(21), true), new("Motion specs", Sep(29)), new("Engineering review", new DateTime(2026, 10, 2)),
                new("Launch assets", new DateTime(2026, 10, 9))
            ]),
        new("fieldnotes", "Fieldnotes identity", "Fieldnotes Co.", "sofia", ["sofia", "ryan", "olivia"], DemoProjectStatus.Review, 89,
            Sep(28), 26400, 22800, "The client approved the logo suite. Brand guidelines are in review.",
            [new("Logo suite", Sep(22), true), new("Brand guidelines", Sep(25)), new("Final files", Sep(28))]),
        new("harbor", "Harbor design system", "Harbor Health", "jasmine", ["jasmine", "chloe", "daniel", "emma"], DemoProjectStatus.OnTrack, 41,
            new DateTime(2026, 11, 13), 71000, 24600, "Tokens 0.9 shipped this morning with dark mode surfaces.",
            [
                new("Token foundations", Sep(22), true), new("Core components", new DateTime(2026, 10, 16)),
                new("Accessibility audit", new DateTime(2026, 10, 30)), new("Documentation site", new DateTime(2026, 11, 13))
            ]),
        new("tidewater", "Tidewater research", "Tidewater Bank", "ava", ["ava", "hannah"], DemoProjectStatus.OnTrack, 63,
            new DateTime(2026, 10, 21), 18900, 10400, "Six of ten interviews are recorded. Synthesis starts next week.",
            [
                new("Recruiting", Sep(4), true), new("Interviews", Sep(30)), new("Synthesis", new DateTime(2026, 10, 14)),
                new("Readout", new DateTime(2026, 10, 21))
            ]),
        new("mono", "Mono commerce", "Mono Studio", "chloe", ["chloe", "marcus", "daniel"], DemoProjectStatus.Shipped, 100,
            Sep(12), 19700, 18900, "Launched on September 12. Conversion is up 14 percent in the first week.",
            [new("Product pages", new DateTime(2026, 8, 21), true), new("Checkout", Sep(4), true), new("Launch", Sep(12), true)])
    ];

    /// <summary>The project "New project" creates.</summary>
    public static OverviewProject Kestrel { get; } = new("kestrel", "Kestrel website", "Kestrel Coffee", "emma", ["emma", "olivia"],
        DemoProjectStatus.OnTrack, 0, new DateTime(2026, 12, 4), 24000, 0, "Kickoff is booked for Monday. The brief is in the shared folder.",
        [
            new("Kickoff", Sep(28)), new("Site map", new DateTime(2026, 10, 9)), new("Visual design", new DateTime(2026, 11, 6)),
            new("Launch", new DateTime(2026, 12, 4))
        ]);

    public static IReadOnlyList<OverviewRange> Ranges { get; } =
    [
        new("week", "Week", "Last 7 days", "vs last week", "compared with the previous week", "Sep 16 – 22, 2026",
            ["Wed 16", "Thu 17", "Fri 18", "Sat 19", "Sun 20", "Mon 21", "Tue 22"]),
        new("month", "Month", "Last 30 days", "vs prior 30 days", "compared with the previous 30 days", "Aug 24 – Sep 22, 2026",
            ["Aug 24", "Aug 27", "Aug 30", "Sep 2", "Sep 5", "Sep 8", "Sep 11", "Sep 14", "Sep 17", "Sep 20"]),
        new("quarter", "Quarter", "Last 13 weeks", "vs prior 13 weeks", "compared with the previous 13 weeks", "Jun 22 – Sep 20, 2026",
            Enumerable.Range(0, 13).Select(i => "Week of " + new DateTime(2026, 6, 22).AddDays(7 * i).ToString("MMM d", En)).ToArray())
    ];

    public static OverviewRange Range(string id) => Ranges.First(range => range.Id == id);

    private static Dictionary<string, OverviewReading> Readings(OverviewReading week, OverviewReading month, OverviewReading quarter) =>
        new() { ["week"] = week, ["month"] = month, ["quarter"] = quarter };

    public static IReadOnlyList<OverviewMetric> Metrics { get; } =
    [
        new("Revenue booked", "$", string.Empty, 0, value => value.ToString("C0", En), Readings(
            new(24600, "+8.4%", true, true, [3200, 4100, 2800, 5600, 1200, 900, 6800]),
            new(96400, "+12.4%", true, true, [7800, 9400, 8600, 11200, 9100, 10400, 8900, 10800, 9600, 10600]),
            new(281900, "+9.1%", true, true, [18200, 19600, 21400, 17800, 22900, 20100, 23400, 21700, 19900, 24800, 22300, 25200, 24600]))),
        new("Reviews closed", string.Empty, string.Empty, 0, value => value.ToString("N0", En), Readings(
            new(18, "+12.5%", true, true, [3, 4, 2, 5, 1, 0, 3]),
            new(74, "+8.8%", true, true, [6, 8, 7, 9, 5, 8, 7, 8, 9, 7]),
            new(221, Minus + "2.6%", false, false, [14, 17, 15, 19, 16, 18, 15, 17, 19, 16, 18, 19, 18]))),
        new("Median review time", string.Empty, Nbsp + "h", 1, value => value.ToString("0.0", En) + Nbsp + "h", Readings(
            new(5.4, Minus + "0.8" + Nbsp + "h", false, true, [6.2, 5.8, 5.1, 5.6, 4.9, 5.3, 5.4]),
            new(6.1, Minus + "0.4" + Nbsp + "h", false, true, [7, 6.8, 6.4, 6.9, 6.2, 6, 6.5, 5.8, 6.3, 6.1]),
            new(7.2, "+0.3" + Nbsp + "h", true, false, [6.8, 7.1, 7.4, 6.9, 7.6, 7.3, 7, 7.5, 7.2, 7.8, 7.1, 6.9, 7.2]))),
        new("On-time delivery", string.Empty, "%", 0, value => value.ToString("0", En) + "%", Readings(
            new(96, "+4 pts", true, true, [92, 94, 95, 93, 96, 97, 96]),
            new(92, "+2 pts", true, true, [88, 90, 89, 91, 90, 92, 91, 93, 92, 92]),
            new(89, Minus + "1 pt", false, false, [84, 86, 85, 87, 88, 86, 89, 90, 88, 91, 90, 89, 89])))
    ];

    /// <summary>Hours logged per day, Aug 24 to Sep 22 (today, so far); uiarc.dev's seed 2209, computed once.</summary>
    public static IReadOnlyList<OverviewHours> Daily { get; } = Hours(new DateTime(2026, 8, 24), 1,
    [
        64, 50, 63, 51, 57, 45, 63, 46, 60, 44, 4, 3, 7, 6, 65, 47, 58, 42, 69, 56, 61, 45, 58, 46, 6, 4, 5, 4, 65, 52,
        66, 50, 68, 48, 61, 49, 50, 39, 1, 1, 1, 1, 60, 47, 63, 51, 67, 53, 54, 39, 64, 45, 7, 5, 5, 4, 65, 49, 38, 28
    ]);

    /// <summary>Hours logged per week, the weeks of Jun 22 to Sep 14.</summary>
    public static IReadOnlyList<OverviewHours> Weekly { get; } = Hours(new DateTime(2026, 6, 22), 7,
    [
        321, 256, 306, 219, 323, 246, 289, 216, 301, 215, 277, 209, 262, 197, 262, 201, 292, 229, 311, 240, 292, 229, 310, 247,
        331, 254
    ]);

    private static OverviewHours[] Hours(DateTime start, int step, int[] pairs) =>
        Enumerable.Range(0, pairs.Length / 2).Select(i => new OverviewHours(start.AddDays(step * i), pairs[2 * i], pairs[2 * i + 1])).ToArray();

    public static string FirstName(string name) => name.Split(' ')[0];

    #region Activity year

    private static readonly string[] s_dayProjects = ["Atlas platform", "Northwind app", "Harbor design system", "Fieldnotes identity", "Tidewater research"];

    /// <summary>
    /// A year of daily reviews or handoffs (Sep 23 2025 to Sep 22 2026): quiet weekends and holidays, busy two-week crunches
    /// before four deadlines. A port of uiarc.dev's generator: the same seeds and the same order of draws.
    /// </summary>
    public static List<ActivityDay> Activity(bool reviews)
    {
        var random = new Mulberry32(reviews ? 4481u : 7717u);
        DateTime[] deadlines = [new(2025, 11, 20), new(2026, 3, 12), new(2026, 6, 18), new(2026, 9, 12)];
        var days = new List<ActivityDay>();
        for (var day = new DateTime(2025, 9, 23); day <= new DateTime(2026, 9, 22); day = day.AddDays(1))
        {
            var weekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var holiday = (day >= new DateTime(2025, 12, 22) && day < new DateTime(2026, 1, 5))
                          || (day >= new DateTime(2026, 8, 3) && day < new DateTime(2026, 8, 14));
            var crunch = deadlines.Any(deadline => day <= deadline && (deadline - day).TotalDays < 14);
            var chance = holiday ? 0.04 : crunch ? (weekend ? 0.35 : 0.97) : weekend ? 0.1 : 0.82;
            var mean = reviews ? (crunch ? 7.5 : 3.8) : crunch ? 3.2 : 1.6;
            var count = 0;
            if (random.Next() < chance)
            {
                count = Math.Max(1, (int)JsRound(mean * (0.4 + Math.Pow(random.Next(), 1.6) * 1.5)));
            }
            days.Add(new ActivityDay(DateOnly.FromDateTime(day), count));
        }
        return days;
    }

    /// <summary>The project a day's note names: uiarc.dev hashes the ISO date into the five active projects.</summary>
    public static string DayProject(DateOnly date)
    {
        var hash = 7;
        foreach (var c in date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        {
            hash = (31 * hash + c) % 9973;
        }
        return s_dayProjects[hash % s_dayProjects.Length];
    }

    #endregion

    #region Growth metrics (MetricExplorer)

    /// <summary>The MetricExplorer's four series: 730 daily points ending Sep 22 2026, as uiarc.dev builds them.</summary>
    public static ExplorerMetric[] Growth()
    {
        var mrrNoise = Noise(11, 0.94, 0.0021);
        var accountsNoise = Noise(23, 0.9, 4.2);
        var activationNoise = Noise(37, 0.88, 0.15);
        var nrrNoise = Noise(51, 0.95, 0.045);
        var mrr = Pin(i => 790000 * Math.Exp(0.00066 * i) * (1 + mrrNoise[i]) + 18400 * Smooth((i - 402) / 3.0)
                           + 23900 * Smooth((i - 588) / 3.0) - 15600 * Smooth((i - 668) / 2.0), 1284600, 100);
        var accounts = Pin(i => 1388 + 905 * Math.Pow(i / 729.0, 1.18) + 21 * Math.Sin(i / 58.0)
                                - 34 * Math.Exp(-Math.Pow((i - 322) / 16.0, 2)) + accountsNoise[i], 2318, 1);
        var activation = Pin(i => 33.4 + 2.2 * i / 729 + 4.4 * Smooth((i - 684) / 28.0) + 0.55 * Math.Sin(i / 41.0) + activationNoise[i], 41.8, 0.1);
        var nrr = Pin(i => 105.6 + 8.6 * Smooth((i - 120) / 560.0) - 1.9 * Smooth((i - 684) / 45.0) + nrrNoise[i], 112.4, 0.1);
        var end = new DateTime(2026, 9, 22);
        return
        [
            new("MRR", mrr, ExplorerValueFormat.CurrencyCompact)
            {
                Key = "mrr", ShortTitle = "MRR", EndDate = end,
                Description = "Monthly recurring revenue from active subscriptions, before refunds and credits."
            },
            new("Active accounts", accounts, ExplorerValueFormat.Count)
            {
                Key = "accounts", ShortTitle = "Accounts", EndDate = end,
                Description = "Paying workspaces with at least one member active in the last 7 days."
            },
            new("Activation rate", activation, ExplorerValueFormat.Percent)
            {
                Key = "activation", ShortTitle = "Activation", EndDate = end, ChangeMode = ExplorerChangeMode.Points,
                Description = "New workspaces that invite a teammate within 7 days, trailing 28 days."
            },
            new("Net revenue retention", nrr, ExplorerValueFormat.Percent)
            {
                Key = "nrr", ShortTitle = "NRR", EndDate = end, ChangeMode = ExplorerChangeMode.Points,
                Description = "Revenue kept and expanded from customers of a year ago, trailing 12 months."
            }
        ];
    }

    private static double Smooth(double x) => x <= 0 ? 0 : x >= 1 ? 1 : x * x * (3 - 2 * x);

    /// <summary>A damped random walk: each step keeps <paramref name="decay"/> of the last and adds a triangular kick.</summary>
    private static double[] Noise(uint seed, double decay, double amplitude)
    {
        var random = new Mulberry32(seed);
        var values = new double[730];
        double level = 0;
        for (var i = 0; i < values.Length; i++)
        {
            level = level * decay + (random.Next() + random.Next() + random.Next() - 1.5) * 2 * amplitude;
            values[i] = level;
        }
        return values;
    }

    /// <summary>Shifts a raw series so it ends on <paramref name="target"/> and rounds it to <paramref name="step"/>.</summary>
    private static double[] Pin(Func<int, double> raw, double target, double step)
    {
        var values = Enumerable.Range(0, 730).Select(raw).ToArray();
        var shift = target - values[729];
        return values.Select(value => Math.Round(JsRound((value + shift) / step) * step, 6)).ToArray();
    }

    #endregion

    /// <summary>JavaScript's Math.round (halves go up, also below zero).</summary>
    public static double JsRound(double value) => Math.Floor(value + 0.5);

    /// <summary>mulberry32, bit for bit as uiarc.dev's demo data uses it.</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var e = _state;
                e = (e ^ (e >> 15)) * (1u | e);
                e ^= e + (e ^ (e >> 7)) * (61u | e);
                return (e ^ (e >> 14)) / 4294967296.0;
            }
        }
    }
}
