namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented accounts of the Customer galaxy demos: 2,400 customers of a made-up SaaS company with four plans, four
/// sales regions, six industries and four health states, generated from a fixed seed so every run draws the same galaxy.
/// Plans: Solo, Team, Business, Corporate; MRR is log-normal within each plan (about $6M in total, Corporate about 45%).
/// </summary>
public static class CustomerGalaxyDemoData
{
    /// <summary>The seed of the generator.</summary>
    public const uint Seed = 1729;

    private static readonly string[] s_prefixes =
    [
        "Alder", "Birchline", "Cobalt", "Driftwood", "Everly", "Fernhill", "Glasshouse", "Halcyon", "Ironvale", "Juniper",
        "Kestrel", "Lanternfield", "Mossgate", "Northwind", "Opaline", "Pinecrest", "Quillstone", "Ridgeway", "Saltmarsh",
        "Tamarack", "Umberly", "Vantage", "Willowmere", "Yarrow", "Zephyr", "Ashgrove", "Brightwater", "Copperleaf",
        "Duneview", "Emberly"
    ];

    private static readonly string[] s_firstNames =
    [
        "Ada", "Bruno", "Celia", "Dario", "Elin", "Farah", "Gideon", "Hana", "Ivo", "Jonah", "Kaia", "Lior", "Mira",
        "Nils", "Odette", "Pavel", "Rhea", "Soren", "Talia", "Viktor"
    ];

    private static readonly string[] s_lastNames =
    [
        "Abernathy", "Bellweather", "Castellan", "Dunmore", "Eastbrook", "Fairholm", "Greaves", "Hollins", "Ingram",
        "Jessop", "Kirkwood", "Lindqvist", "Marchetti", "Northcott", "Okafor", "Penhallow", "Quarles", "Rosales",
        "Silvermane", "Thornbury"
    ];

    /// <summary>Plan key, label, accounts, median MRR, σ, lowest and highest MRR.</summary>
    private static readonly (string Key, string Label, int Count, double Median, double Sigma, double Low, double High)[] s_plans =
    [
        ("solo", "Solo", 1350, 140, 0.55, 25, 480),
        ("team", "Team", 690, 1250, 0.6, 400, 3600),
        ("business", "Business", 290, 6200, 0.5, 2200, 16000),
        ("corporate", "Corporate", 70, 36500, 0.55, 9000, 95000)
    ];

    /// <summary>Region key and label with its share of the accounts.</summary>
    private static readonly (string Key, string Label, double Weight)[] s_regions =
    [
        ("aurora", "Aurora", 1000), ("meridian", "Meridian", 780), ("harbor", "Harbor", 420), ("highlands", "Highlands", 200)
    ];

    /// <summary>Industry key, label, account suffix, share, and share among Corporate accounts.</summary>
    private static readonly (string Key, string Label, string Suffix, double Weight, double CorporateWeight)[] s_industries =
    [
        ("analytics", "Analytics", "Labs", 30, 36),
        ("banking", "Banking", "Bank", 16, 30),
        ("biotech", "Biotech", "Clinic", 17, 12),
        ("commerce", "Commerce", "Market", 13, 9),
        ("gaming", "Gaming", "Games", 11, 6),
        ("shipping", "Shipping", "Freight", 13, 7)
    ];

    private static readonly string[] s_alternateSuffixes = ["Systems", "Works", "Studio", "Group"];

    private static readonly Dictionary<string, string[]> s_notes = new()
    {
        ["growing"] = ["Seats up this quarter", "Added a new workspace", "Upgrade call booked", "Second team onboarded"],
        ["steady"] = ["Usage steady", "Renewal on track", "Quarterly review done", "No open tickets"],
        ["cooling"] = ["Logins down this month", "Champion changed roles", "Fewer active seats"],
        ["at-risk"] = ["Renewal in 12 days, no reply", "Open escalation", "Evaluating alternatives", "Payment failed twice"]
    };

    private static IReadOnlyList<GalaxyAccount>? s_accounts;

    /// <summary>The 2,400 accounts (largest MRR first), built once.</summary>
    public static IReadOnlyList<GalaxyAccount> Accounts => s_accounts ??= Create();

    /// <summary>The plan grouping with its clusters in plan order.</summary>
    public static GalaxyGrouping PlanGrouping() => Grouping("plan", "Plan", s_plans.Select(p => (p.Key, p.Label)));

    /// <summary>The region grouping.</summary>
    public static GalaxyGrouping RegionGrouping() => Grouping("region", "Region", s_regions.Select(r => (r.Key, r.Label)));

    /// <summary>The industry grouping.</summary>
    public static GalaxyGrouping IndustryGrouping() => Grouping("industry", "Industry", s_industries.Select(i => (i.Key, i.Label)));

    /// <summary>The health grouping (its clusters come from the control's health states).</summary>
    public static GalaxyGrouping HealthGrouping() => new() { Key = GalaxyGrouping.HealthKey, Header = "Health" };

    /// <summary>Plan, region, health and industry, the preview's tabs.</summary>
    public static IEnumerable<GalaxyGrouping> Groupings() => [PlanGrouping(), RegionGrouping(), HealthGrouping(), IndustryGrouping()];

    private static GalaxyGrouping Grouping(string key, string header, IEnumerable<(string Key, string Label)> groups)
    {
        var grouping = new GalaxyGrouping { Key = key, Header = header };
        foreach (var (k, label) in groups)
        {
            grouping.Groups.Add(new GalaxyGroupDefinition { Key = k, Label = label });
        }
        return grouping;
    }

    /// <summary>Generates the accounts (spec recipe: log-normal MRR per plan, weighted region, industry and health).</summary>
    public static IReadOnlyList<GalaxyAccount> Create(uint seed = Seed)
    {
        var random = new Mulberry32(seed);
        var accounts = new List<GalaxyAccount>(2400);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var number = 0;
        foreach (var plan in s_plans)
        {
            for (var i = 0; i < plan.Count; i++)
            {
                var raw = Math.Exp(Math.Log(plan.Median) + plan.Sigma * random.NextGaussian());
                var mrr = Math.Clamp(raw, plan.Low, plan.High);
                mrr = mrr >= 1000 ? Math.Round(mrr / 50) * 50 : mrr >= 100 ? Math.Round(mrr / 10) * 10 : Math.Round(mrr);
                var region = s_regions[random.PickWeighted(s_regions.Select(r => r.Weight).ToArray())];
                var corporate = plan.Key == "corporate";
                var industry = s_industries[random.PickWeighted(s_industries.Select(x => corporate ? x.CorporateWeight : x.Weight).ToArray())];
                // Larger plans grow more often; Corporate is a little less often at risk, Solo a little more.
                var growth = plan.Key switch { "corporate" => 0.16, "business" => 0.13, "team" => 0.09, _ => 0.07 };
                var risk = plan.Key switch { "corporate" => 0.07, "solo" => 0.11, _ => 0.10 };
                var health = random.PickWeighted([growth, 0.70, 0.11, risk]) switch
                {
                    0 => "growing",
                    1 => "steady",
                    2 => "cooling",
                    _ => "at-risk"
                };
                var prefix = s_prefixes[random.Next(s_prefixes.Length)];
                var suffix = random.NextDouble() < 0.7 ? industry.Suffix : s_alternateSuffixes[random.Next(s_alternateSuffixes.Length)];
                var name = prefix + " " + suffix;
                for (var n = 2; !names.Add(name); n++)
                {
                    name = prefix + " " + suffix + " " + n;
                }
                var selfServe = plan.Key == "solo" || plan.Key == "team" && random.NextDouble() < 0.4;
                var owner = selfServe
                    ? "Self-serve"
                    : s_firstNames[random.Next(s_firstNames.Length)] + " " + s_lastNames[random.Next(s_lastNames.Length)];
                var notes = s_notes[health];
                var account = new GalaxyAccount
                {
                    Id = "acct-" + (++number).ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                    Name = name,
                    Mrr = mrr,
                    Health = health,
                    Owner = owner,
                    Note = notes[random.Next(notes.Length)]
                };
                account.Groups["plan"] = plan.Key;
                account.Groups["region"] = region.Key;
                account.Groups["industry"] = industry.Key;
                accounts.Add(account);
            }
        }
        accounts.Sort((a, b) => b.Mrr.CompareTo(a.Mrr));
        return accounts;
    }

    /// <summary>Mulberry32, the generator of the Showpiece data recipes (matches its JavaScript original bit for bit).</summary>
    private sealed class Mulberry32
    {
        private uint _state;
        private double? _spare;

        public Mulberry32(uint seed) => _state = seed;

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Next(int count) => Math.Min(count - 1, (int)(NextDouble() * count));

        public double NextGaussian()
        {
            if (_spare is { } spare)
            {
                _spare = null;
                return spare;
            }
            var radius = Math.Sqrt(-2 * Math.Log(1 - NextDouble()));
            var angle = 2 * Math.PI * NextDouble();
            _spare = radius * Math.Sin(angle);
            return radius * Math.Cos(angle);
        }

        public int PickWeighted(double[] weights)
        {
            var total = weights.Sum();
            var target = NextDouble() * total;
            for (var i = 0; i < weights.Length; i++)
            {
                target -= weights[i];
                if (target < 0)
                {
                    return i;
                }
            }
            return weights.Length - 1;
        }
    }
}
