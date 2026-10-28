namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The Analytics page's data: what uiarc.dev's four analytics blocks show when they are rendered without props (their
/// seeded defaults, reproduced number for number), shaped for our data-driven ports.
/// </summary>
internal static class DemoAnalyticsData
{
    // ---- Recurring revenue -------------------------------------------------------------------------------------------

    /// <summary>Recurring revenue before Sep 2024, the first plotted month.</summary>
    public const double OpeningMrr = 1_269_900;

    // Sep 2024 – Aug 2026: new, expansion, churn and the closing MRR of each month (mulberry32, seed 0x13527d6).
    private static readonly double[,] s_revenue =
    {
        { 47_400, 14_900, 24_000, 1_308_200 }, { 54_800, 17_100, 21_000, 1_359_100 }, { 46_600, 15_800, 22_600, 1_398_900 },
        { 41_200, 17_900, 24_600, 1_433_400 }, { 53_100, 16_800, 23_300, 1_480_000 }, { 54_100, 18_000, 39_100, 1_513_000 },
        { 64_700, 17_200, 34_500, 1_560_400 }, { 54_600, 20_000, 24_900, 1_610_100 }, { 58_000, 19_500, 25_300, 1_662_300 },
        { 58_200, 19_000, 25_100, 1_714_400 }, { 47_700, 21_000, 27_000, 1_756_100 }, { 52_900, 19_500, 31_100, 1_797_400 },
        { 67_600, 20_100, 27_700, 1_857_400 }, { 63_300, 29_700, 29_800, 1_920_600 }, { 61_000, 31_600, 29_500, 1_983_700 },
        { 47_000, 31_300, 29_600, 2_032_400 }, { 67_200, 32_800, 33_600, 2_098_800 }, { 68_300, 28_200, 31_100, 2_164_200 },
        { 72_500, 31_500, 34_100, 2_234_100 }, { 71_900, 31_700, 35_700, 2_302_000 }, { 70_800, 30_200, 36_400, 2_366_600 },
        { 72_000, 31_300, 32_200, 2_437_700 }, { 66_000, 30_100, 37_900, 2_495_900 }, { 63_400, 33_600, 35_400, 2_557_500 }
    };

    /// <summary>The 24 plotted months (churn negative, as our port wants it).</summary>
    public static IEnumerable<RevenueMonth> RevenueMonths()
    {
        var first = new DateOnly(2024, 9, 1);
        for (var i = 0; i < s_revenue.GetLength(0); i++)
        {
            yield return new RevenueMonth(first.AddMonths(i), s_revenue[i, 0], s_revenue[i, 1], -s_revenue[i, 2], s_revenue[i, 3]);
        }
    }

    // ---- MRR movement ------------------------------------------------------------------------------------------------

    private static readonly string[] s_plans = ["Enterprise", "Scale", "Growth", "Starter"];

    // Plan snapshots (amount, accounts per plan) on Jul 1, Aug 1, Sep 1 and Sep 30.
    private static readonly (double Amount, int Accounts)[][] s_snapshots =
    [
        [(151_200, 22), (137_760, 164), (96_880, 486), (26_540, 569)],
        [(156_900, 23), (144_120, 171), (101_450, 503), (28_330, 579)],
        [(158_300, 23), (149_660, 178), (105_230, 511), (28_890, 575)],
        [(167_400, 25), (157_980, 186), (110_820, 531), (29_450, 595)]
    ];

    private static readonly MrrMovementKind[] s_kinds =
    [
        MrrMovementKind.New, MrrMovementKind.Expansion, MrrMovementKind.Reactivation, MrrMovementKind.Contraction, MrrMovementKind.Churn
    ];

    private static readonly string[] s_moveNames = ["New business", "Expansion", "Reactivation", "Contraction", "Churn"];

    private static readonly string[] s_moveAxis = ["New", "Expansion", "Reactivation", "Contraction", "Churn"];

    private static readonly string[] s_moveShort = ["New", "Exp.", "React.", "Contr.", "Churn"];

    private static readonly string[] s_moveNouns =
        ["new accounts", "accounts expanded", "accounts came back", "accounts downgraded", "accounts cancelled"];

    private sealed record Move(int Count, double Total, (string Name, string Detail, double Amount)[] Accounts);

    private sealed record Period(string Key, string Label, string Long, string Range, string From, string To, int StartSnapshot,
        int EndSnapshot, Move[] Moves);

    // Arc derives Q3 from the months (summed totals, merged accounts); our port takes it ready-made, so it is listed here.
    private static readonly Period[] s_periods =
    [
        new("jul", "Jul", "July 2025", "Jul 1 – 31, 2025", "Jul 1", "Jul 31", 0, 1,
        [
            new(64, 18_640, [("Halcyon Health", "Enterprise, annual", 4_800), ("Kestrel Freight", "Scale, 60 seats", 1_740),
                ("Juniper Legal", "Scale, 45 seats", 1_310), ("Oakridge Schools", "Growth, 30 seats", 990)]),
            new(41, 9_870, [("Brightwater Labs", "Growth to Scale", 1_520), ("Tessellate", "Added 40 seats", 1_160),
                ("Quillon Bank", "Added audit logs", 900), ("Pinecrest Realty", "Added 25 seats", 725)]),
            new(9, 2_140, [("Fathom Studio", "Back after 4 months, Growth", 597), ("Vireo Travel", "Back after 7 months, Growth", 398),
                ("Bramble Foods", "Back after 2 months, Starter", 245)]),
            new(19, 4_310, [("Castor Mobility", "Scale to Growth", 1_080), ("Sable Insurance", "Removed 30 seats", 870),
                ("Lattice Robotics", "Removed SSO add-on", 400)]),
            new(38, 7_920, [("Copperline Energy", "Moved to an in-house tool", 1_990), ("Monarch Dental Group", "Budget cut", 1_195),
                ("Wren & Wolfe", "Acquired by Rhodes Group", 796), ("Arbor & Finch", "Went back to spreadsheets", 398)])
        ]),
        new("aug", "Aug", "August 2025", "Aug 1 – 31, 2025", "Aug 1", "Aug 31", 1, 2,
        [
            new(52, 15_210, [("Northpeak Logistics", "Enterprise, annual", 5_200), ("Solace Clinics", "Scale, 50 seats", 1_450),
                ("Harbor Cooperative", "Growth, 35 seats", 1_040), ("Ember Coffee", "Growth, 20 seats", 597)]),
            new(48, 12_460, [("Halcyon Health", "Added 120 seats", 2_400), ("Tessellate", "Growth to Scale", 1_380),
                ("Kestrel Freight", "Added 30 seats", 870), ("Oakridge Schools", "Added data retention", 600)]),
            new(6, 1_380, [("Cobalt Studio", "Back after 6 months, Growth", 398), ("Ferris Outdoor", "Back after 3 months, Starter", 196)]),
            new(26, 6_040, [("Quillon Bank", "Scale to Growth", 1_660), ("Juniper Legal", "Removed 20 seats", 580),
                ("Brightwater Labs", "Removed audit logs", 450)]),
            new(47, 11_730, [("Atlas Freightways", "Consolidated vendors", 3_980), ("Sable Insurance", "Budget cut", 1_470),
                ("Morrow Architects", "Moved to a competitor", 796), ("Tidewater Fitness", "Closed the business", 398)])
        ]),
        new("sep", "Sep", "September 2025", "Sep 1 – 30, 2025", "Sep 1", "Sep 30", 2, 3,
        [
            new(71, 21_950, [("Granite Mutual", "Enterprise, annual", 6_400), ("Orchard Health", "Scale, 80 seats", 2_320),
                ("Pioneer Robotics", "Scale, 55 seats", 1_595), ("Lark & Co", "Growth, 25 seats", 750)]),
            new(36, 8_730, [("Northpeak Logistics", "Added 60 seats", 1_200), ("Solace Clinics", "Growth to Scale", 1_090),
                ("Halcyon Health", "Added SSO", 800)]),
            new(12, 3_260, [("Copperline Energy", "Back after 2 months, Scale", 1_990), ("Arbor & Finch", "Back after 2 months, Growth", 398),
                ("Marlow Books", "Back after 9 months, Starter", 147)]),
            new(15, 3_480, [("Kestrel Freight", "Removed 15 seats", 435), ("Harbor Cooperative", "Growth to Starter", 350),
                ("Pinecrest Realty", "Removed SSO add-on", 300)]),
            new(33, 6_890, [("Castor Mobility", "Moved to a competitor", 1_020), ("Ember Coffee", "Budget cut", 597),
                ("Vireo Travel", "Seasonal pause", 398)])
        ]),
        new("q3", "Q3", "Q3 2025", "Jul 1 – Sep 30, 2025", "Jul 1", "Sep 30", 0, 3,
        [
            new(187, 55_800, [("Granite Mutual", "Sep · Enterprise, annual", 6_400), ("Northpeak Logistics", "Aug · Enterprise, annual", 5_200),
                ("Halcyon Health", "Jul · Enterprise, annual", 4_800), ("Orchard Health", "Sep · Scale, 80 seats", 2_320),
                ("Kestrel Freight", "Jul · Scale, 60 seats", 1_740)]),
            new(123, 31_060, [("Halcyon Health", "Aug and Sep · 2 changes", 3_200), ("Tessellate", "Jul and Aug · 2 changes", 2_540),
                ("Brightwater Labs", "Jul · Growth to Scale", 1_520), ("Northpeak Logistics", "Sep · Added 60 seats", 1_200),
                ("Solace Clinics", "Sep · Growth to Scale", 1_090)]),
            new(27, 6_780, [("Copperline Energy", "Sep · Back after 2 months, Scale", 1_990),
                ("Fathom Studio", "Jul · Back after 4 months, Growth", 597), ("Vireo Travel", "Jul · Back after 7 months, Growth", 398),
                ("Cobalt Studio", "Aug · Back after 6 months, Growth", 398), ("Arbor & Finch", "Sep · Back after 2 months, Growth", 398)]),
            new(60, 13_830, [("Quillon Bank", "Aug · Scale to Growth", 1_660), ("Castor Mobility", "Jul · Scale to Growth", 1_080),
                ("Sable Insurance", "Jul · Removed 30 seats", 870), ("Juniper Legal", "Aug · Removed 20 seats", 580),
                ("Brightwater Labs", "Aug · Removed audit logs", 450)]),
            new(118, 26_540, [("Atlas Freightways", "Aug · Consolidated vendors", 3_980),
                ("Copperline Energy", "Jul · Moved to an in-house tool", 1_990), ("Sable Insurance", "Aug · Budget cut", 1_470),
                ("Monarch Dental Group", "Jul · Budget cut", 1_195), ("Castor Mobility", "Sep · Moved to a competitor", 1_020)])
        ])
    ];

    /// <summary>Jul, Aug, Sep and Q3 2025 (Q3 is the one that shows).</summary>
    public static MrrPeriod[] MrrPeriods() => s_periods.Select(ToPeriod).ToArray();

    private static MrrPeriod ToPeriod(Period source)
    {
        var start = s_snapshots[source.StartSnapshot];
        var end = s_snapshots[source.EndSnapshot];
        var period = new MrrPeriod
        {
            Key = source.Key,
            Label = source.Label,
            LongLabel = source.Long,
            Caption = source.Range,
            StartDateText = source.From,
            EndDateText = source.To,
            StartingMrr = start.Sum(plan => plan.Amount),
            StartingAccounts = start.Sum(plan => plan.Accounts),
            EndingAccounts = end.Sum(plan => plan.Accounts),
            StartingCountNoun = $"paying accounts on {source.From}",
            EndingCountNoun = $"paying accounts on {source.To}"
        };
        for (var i = 0; i < s_kinds.Length; i++)
        {
            var move = source.Moves[i];
            var movement = new MrrMovement
            {
                Kind = s_kinds[i],
                Label = s_moveNames[i],
                ShortLabel = s_moveAxis[i],
                CompactLabel = s_moveShort[i],
                Amount = move.Total,
                AccountCount = move.Count,
                CountNoun = s_moveNouns[i]
            };
            foreach (var (name, detail, amount) in move.Accounts)
            {
                movement.Accounts.Add(new MrrAccount(name, amount, detail) { Key = name });
            }
            period.Movements.Add(movement);
        }
        for (var p = 0; p < s_plans.Length; p++)
        {
            period.StartingBreakdown.Add(new MrrAccount(s_plans[p], start[p].Amount, $"{start[p].Accounts} accounts", start[p].Accounts)
            {
                Key = "plan-" + s_plans[p]
            });
            period.EndingBreakdown.Add(new MrrAccount(s_plans[p], end[p].Amount, $"{end[p].Accounts} accounts", end[p].Accounts)
            {
                Key = "plan-" + s_plans[p]
            });
        }
        return period;
    }

    // ---- Cohort retention --------------------------------------------------------------------------------------------

    private static readonly int[][] s_selfServe =
    [
        [301, 207, 173, 149, 133, 120, 109, 106, 102, 98, 96, 96, 94],
        [319, 223, 183, 157, 140, 128, 119, 114, 107, 107, 104, 102],
        [339, 240, 200, 170, 153, 137, 126, 124, 121, 113, 110],
        [344, 243, 204, 176, 159, 140, 135, 128, 121, 115],
        [319, 230, 191, 165, 145, 134, 127, 116, 115],
        [327, 218, 186, 165, 146, 135, 126, 119],
        [377, 274, 233, 198, 178, 162, 155],
        [353, 257, 219, 190, 166, 154],
        [376, 282, 237, 205, 181],
        [421, 316, 265, 229],
        [406, 303, 262],
        [402, 306]
    ];

    private static readonly int[][] s_salesLed =
    [
        [59, 54, 52, 50, 48, 47, 46, 45, 44, 44, 44, 43, 43],
        [67, 63, 59, 57, 55, 54, 53, 52, 51, 51, 50, 49],
        [63, 58, 56, 54, 52, 50, 50, 49, 48, 48, 48],
        [72, 67, 65, 62, 60, 58, 57, 57, 55, 54],
        [76, 71, 68, 65, 64, 61, 60, 60, 59],
        [71, 67, 64, 61, 59, 58, 57, 55],
        [84, 79, 76, 73, 71, 70, 67],
        [81, 76, 74, 70, 68, 66],
        [83, 79, 76, 73, 70],
        [81, 76, 73, 71],
        [89, 85, 81],
        [89, 86]
    ];

    /// <summary>All (the sum of the two), Self-serve and Sales-led: twelve signup cohorts, Aug 2025 to Jul 2026.</summary>
    public static RetentionSegment[] RetentionSegments()
    {
        var all = s_selfServe.Select((row, r) => row.Select((value, m) => value + s_salesLed[r][m]).ToArray()).ToArray();
        return
        [
            Segment("all", "All", "all workspaces", all),
            Segment("self", "Self-serve", "self-serve workspaces", s_selfServe),
            Segment("sales", "Sales-led", "sales-led workspaces", s_salesLed)
        ];
    }

    private static RetentionSegment Segment(string key, string label, string noun, int[][] rows)
    {
        var first = new DateOnly(2025, 8, 1);
        var cohorts = rows.Select((row, r) =>
        {
            var month = first.AddMonths(r);
            var name = month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            var shortName = month.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture) + " ’" + (month.Year % 100).ToString("00");
            return new RetentionCohort(name, row[0], row[1..], shortName) { Key = month.ToString("yyyy-MM") };
        });
        return new RetentionSegment(label, cohorts, noun) { Key = key };
    }

    // ---- Journeys ----------------------------------------------------------------------------------------------------

    /// <summary>The five stages, left to right.</summary>
    public static JourneyStage[] JourneyStages() =>
    [
        new("visit", "Visit"),
        new("account", "Account"),
        new("setup", "Setup"),
        new("week", "First week", "Week 1"),
        new("plan", "Plan") { ShareBaseLabel = "onboarded" }
    ];

    /// <summary>The nine nodes: the main path, an exit at each stage and the paid plan as the goal.</summary>
    public static JourneyNode[] JourneyNodes() =>
    [
        new("visited", "visit", "Visited", JourneyNodeKind.Step, "Opened the product site in August"),
        new("signed-up", "account", "Signed up", JourneyNodeKind.Step, "Created an account"),
        new("bounced", "account", "Bounced", JourneyNodeKind.Exit, "Left without creating an account") { DropOffLabel = "Bounced after visiting" },
        new("onboarded", "setup", "Onboarded", JourneyNodeKind.Step, "Finished the setup checklist"),
        new("dropped", "setup", "Dropped", JourneyNodeKind.Exit, "Left before setup was finished") { DropOffLabel = "Dropped during setup" },
        new("activated", "week", "Activated", JourneyNodeKind.Step, "Shared a first project in week one"),
        new("stalled", "week", "Stalled", JourneyNodeKind.Exit, "No second session in week one"),
        new("paid", "plan", "Paid", JourneyNodeKind.Goal, "Started a paid plan"),
        new("free", "plan", "Free", JourneyNodeKind.Exit, "Stayed on the free plan")
    ];

    private static readonly (string Source, string Target)[] s_links =
    [
        ("visited", "signed-up"), ("visited", "bounced"), ("signed-up", "onboarded"), ("signed-up", "dropped"),
        ("onboarded", "activated"), ("onboarded", "stalled"), ("activated", "paid"), ("activated", "free"),
        ("stalled", "paid"), ("stalled", "free")
    ];

    private static readonly double[] s_organic = [2_863, 5_563, 2_172, 691, 1_394, 778, 463, 931, 41, 737];

    private static readonly double[] s_paidAds = [1_294, 4_858, 823, 471, 409, 414, 148, 261, 9, 405];

    private static readonly double[] s_referral = [1_118, 1_269, 957, 161, 722, 235, 331, 391, 28, 207];

    /// <summary>All users (the sum), Organic, Paid ads and Referral: August's visits by acquisition channel.</summary>
    public static JourneySegment[] JourneySegments()
    {
        var all = s_organic.Select((value, i) => value + s_paidAds[i] + s_referral[i]).ToArray();
        return
        [
            Journey("all", "All users", all),
            Journey("organic", "Organic", s_organic),
            Journey("paid-ads", "Paid ads", s_paidAds),
            Journey("referral", "Referral", s_referral)
        ];
    }

    private static JourneySegment Journey(string key, string header, double[] values) =>
        new(key, header, s_links.Select((link, i) => new JourneyLink(link.Source, link.Target, values[i])));
}
