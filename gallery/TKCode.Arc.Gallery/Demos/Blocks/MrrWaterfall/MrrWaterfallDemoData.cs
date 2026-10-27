namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The MrrWaterfall demos' data (all invented and deterministic): one subscription product over the second quarter of 2026,
/// as three monthly periods and the quarter that adds them up. The accounts behind each movement are the named ones; the
/// block groups the rest of a step's revenue in its remainder row. A second data set shows a shrinking quarter.
/// </summary>
public static class MrrWaterfallDemoData
{
    public const string Title = "Recurring revenue bridge";

    public const string Scope = "All tiers";

    private static readonly string[] s_monthLabels = ["Apr", "May", "Jun"];

    private static readonly string[] s_monthStart = ["Apr 1", "May 1", "Jun 1"];

    private static readonly string[] s_monthEnd = ["Apr 30", "May 31", "Jun 30"];

    private static readonly string[] s_monthCaption = ["Apr 1 – 30, 2026", "May 1 – 31, 2026", "Jun 1 – 30, 2026"];

    private static readonly string[] s_monthLong = ["April 2026", "May 2026", "June 2026"];

    // Month starting MRR, then the five movement totals (new, expansion, reactivation, contraction, churn) per month.
    private static readonly double[] s_starts = [286_400, 300_210, 308_120];

    private static readonly double[][] s_totals =
    [
        [13_950, 6_820, 1_610, 2_940, 5_630],
        [10_480, 8_740, 930, 4_180, 8_060],
        [15_370, 6_110, 2_280, 2_350, 4_770]
    ];

    private static readonly int[][] s_counts =
    [
        [44, 29, 6, 13, 25],
        [32, 31, 5, 15, 30],
        [40, 28, 8, 13, 24]
    ];

    private static readonly int[] s_accounts = [892, 917, 924, 948];

    private static readonly string[] s_labels = ["New business", "Expansion", "Reactivation", "Contraction", "Churn"];

    private static readonly string[] s_short = ["New", "Expansion", "Reactivation", "Contraction", "Churn"];

    private static readonly string[] s_compact = ["New", "Exp.", "React.", "Contr.", "Churn"];

    private static readonly string[] s_nouns = ["new customers", "customers grew", "customers returned", "customers reduced seats", "customers left"];

    private static readonly MrrMovementKind[] s_kinds =
    [
        MrrMovementKind.New, MrrMovementKind.Expansion, MrrMovementKind.Reactivation, MrrMovementKind.Contraction, MrrMovementKind.Churn
    ];

    // The named accounts: month (0-2), movement (0-4), name, amount, what happened.
    private static readonly (int Month, int Move, string Name, double Amount, string Note)[] s_named =
    [
        (0, 0, "Larchmont Dental", 3_200, "Business, annual"),
        (0, 0, "Bluefin Analytics", 2_640, "Plus, 40 seats"),
        (0, 0, "Copperfield Studio", 1_810, "Team, 24 seats"),
        (0, 0, "Quayside Bakery Co", 1_250, "Team, 18 seats"),
        (0, 1, "Tidewater Logistics", 1_900, "Business, +30 seats"),
        (0, 1, "Windrow Ceramics", 1_420, "Plus, +12 seats"),
        (0, 1, "Ostrander Legal", 980, "Team, +8 seats"),
        (0, 2, "Pinecrest Clinics", 640, "Returned after 4 months, Plus"),
        (0, 2, "Saltmarsh Media", 410, "Returned after 2 months, Team"),
        (0, 3, "Halberd Security", 880, "Dropped 14 seats"),
        (0, 3, "Ivory Lane Hotels", 760, "Dropped 9 seats"),
        (0, 4, "Mosswood Schools", 1_340, "Switched to spreadsheets"),
        (0, 4, "Redfern Freight", 1_020, "Team disbanded"),
        (0, 4, "Glasshouse Labs", 790, "Price review"),

        (1, 0, "Amberline Travel", 2_880, "Business, annual"),
        (1, 0, "Kettle & Crumb", 1_540, "Team, 16 seats"),
        (1, 0, "Larchmont Dental", 1_260, "Plus, 20 seats"),
        (1, 1, "Bluefin Analytics", 2_310, "Plus, +24 seats"),
        (1, 1, "Tidewater Logistics", 1_740, "Business, +18 seats"),
        (1, 1, "Mosswood Schools", 960, "Team, +10 seats"),
        (1, 1, "Copperfield Studio", 720, "Team, +6 seats"),
        (1, 2, "Windrow Ceramics", 520, "Returned after 6 months, Plus"),
        (1, 3, "Redfern Freight", 1_180, "Dropped 21 seats"),
        (1, 3, "Pinecrest Clinics", 910, "Dropped 12 seats"),
        (1, 3, "Quayside Bakery Co", 640, "Dropped 8 seats"),
        (1, 4, "Ivory Lane Hotels", 1_890, "Merged with parent account"),
        (1, 4, "Halberd Security", 1_460, "Seasonal pause"),
        (1, 4, "Ostrander Legal", 1_120, "Switched to spreadsheets"),

        (2, 0, "Glasshouse Labs", 3_620, "Business, annual"),
        (2, 0, "Saltmarsh Media", 2_480, "Plus, 36 seats"),
        (2, 0, "Amberline Travel", 1_730, "Team, 22 seats"),
        (2, 0, "Windrow Ceramics", 1_150, "Team, 14 seats"),
        (2, 1, "Larchmont Dental", 1_880, "Business, +20 seats"),
        (2, 1, "Kettle & Crumb", 1_050, "Team, +9 seats"),
        (2, 1, "Redfern Freight", 780, "Plus, +7 seats"),
        (2, 2, "Halberd Security", 860, "Returned after 3 months, Plus"),
        (2, 2, "Ivory Lane Hotels", 640, "Returned after 5 months, Team"),
        (2, 3, "Tidewater Logistics", 740, "Dropped 10 seats"),
        (2, 3, "Bluefin Analytics", 590, "Dropped 7 seats"),
        (2, 4, "Quayside Bakery Co", 1_410, "Team disbanded"),
        (2, 4, "Pinecrest Clinics", 1_150, "Price review"),
        (2, 4, "Mosswood Schools", 880, "Seasonal pause"),
    ];

    private static readonly double[] s_startTier = [0.36, 0.32, 0.24, 0.08];

    private static readonly double[] s_endTier = [0.366, 0.319, 0.238, 0.077];

    private static readonly int[] s_startTierCounts = [21, 140, 380, 351];

    private static readonly string[] s_tiers = ["Business", "Team", "Plus", "Basic"];

    /// <summary>Apr, May, Jun and Q2 (the last one is the default view).</summary>
    public static MrrPeriod[] Periods()
    {
        var periods = new List<MrrPeriod>();
        for (var m = 0; m < 3; m++)
        {
            periods.Add(Month(m));
        }
        periods.Add(Quarter());
        return periods.ToArray();
    }

    private static MrrPeriod Month(int month)
    {
        var period = new MrrPeriod
        {
            Key = s_monthLabels[month].ToLowerInvariant(),
            Label = s_monthLabels[month],
            LongLabel = s_monthLong[month],
            Caption = s_monthCaption[month],
            StartDateText = s_monthStart[month],
            EndDateText = s_monthEnd[month],
            StartingMrr = s_starts[month],
            StartingAccounts = s_accounts[month],
            EndingAccounts = s_accounts[month + 1],
            StartingCountNoun = $"paying customers on {s_monthStart[month]}",
            EndingCountNoun = $"paying customers on {s_monthEnd[month]}"
        };
        for (var move = 0; move < 5; move++)
        {
            var movement = Movement(move, s_totals[month][move], s_counts[month][move]);
            foreach (var (_, _, name, amount, note) in s_named.Where(n => n.Month == month && n.Move == move))
            {
                movement.Accounts.Add(new MrrAccount(name, amount, note) { Key = name });
            }
            period.Movements.Add(movement);
        }
        Tiers(period, s_starts[month], period.EndingMrr, s_accounts[month], s_accounts[month + 1]);
        return period;
    }

    private static MrrPeriod Quarter()
    {
        var period = new MrrPeriod
        {
            Key = "q2",
            Label = "Q2",
            LongLabel = "Q2 2026",
            Caption = "Apr 1 – Jun 30, 2026",
            StartDateText = "Apr 1",
            EndDateText = "Jun 30",
            StartingMrr = s_starts[0],
            StartingAccounts = s_accounts[0],
            EndingAccounts = s_accounts[3],
            StartingCountNoun = "paying customers on Apr 1",
            EndingCountNoun = "paying customers on Jun 30"
        };
        for (var move = 0; move < 5; move++)
        {
            var total = s_totals[0][move] + s_totals[1][move] + s_totals[2][move];
            var count = s_counts[0][move] + s_counts[1][move] + s_counts[2][move];
            var movement = Movement(move, total, count);
            // The same account in two months is one row of the quarter.
            var merged = new Dictionary<string, (double Amount, string Note)>();
            foreach (var (month, _, name, amount, note) in s_named.Where(n => n.Move == move))
            {
                var text = $"{s_monthLabels[month]} · {note}";
                merged[name] = merged.TryGetValue(name, out var existing) ? (existing.Amount + amount, existing.Note) : (amount, text);
            }
            foreach (var (name, (amount, note)) in merged)
            {
                movement.Accounts.Add(new MrrAccount(name, amount, note) { Key = name });
            }
            period.Movements.Add(movement);
        }
        Tiers(period, s_starts[0], period.EndingMrr, s_accounts[0], s_accounts[3]);
        return period;
    }

    private static MrrMovement Movement(int move, double amount, int count) => new()
    {
        Kind = s_kinds[move],
        Label = s_labels[move],
        ShortLabel = s_short[move],
        CompactLabel = s_compact[move],
        Amount = amount,
        AccountCount = count,
        CountNoun = s_nouns[move],
        RestLabel = "Remaining accounts"
    };

    private static void Tiers(MrrPeriod period, double start, double end, int startAccounts, int endAccounts)
    {
        var startCounts = new int[4];
        var endCounts = new int[4];
        var sum = 0;
        for (var i = 0; i < 4; i++)
        {
            startCounts[i] = i < 3 ? (int)Math.Round(s_startTierCounts[i] * startAccounts / 892.0) : 0;
            sum += startCounts[i];
        }
        startCounts[3] = startAccounts - sum;
        sum = 0;
        for (var i = 0; i < 4; i++)
        {
            endCounts[i] = i < 3 ? (int)Math.Round(s_startTierCounts[i] * endAccounts / 892.0) : 0;
            sum += endCounts[i];
        }
        endCounts[3] = endAccounts - sum;
        var startSum = 0.0;
        var endSum = 0.0;
        for (var i = 0; i < 4; i++)
        {
            var a = i < 3 ? Math.Round(start * s_startTier[i]) : start - startSum;
            var b = i < 3 ? Math.Round(end * s_endTier[i]) : end - endSum;
            startSum += a;
            endSum += b;
            period.StartingBreakdown.Add(new MrrAccount(s_tiers[i], a, null, startCounts[i]) { Key = "tier-" + s_tiers[i] });
            period.EndingBreakdown.Add(new MrrAccount(s_tiers[i], b, null, endCounts[i]) { Key = "tier-" + s_tiers[i] });
        }
    }

    /// <summary>A shrinking quarter of a different product (more lost than won), for the downward headline.</summary>
    public static MrrPeriod[] ShrinkingPeriods()
    {
        var period = new MrrPeriod
        {
            Key = "q3",
            Label = "Q3",
            LongLabel = "Q3 2026",
            Caption = "Jul 1 – Sep 30, 2026",
            StartDateText = "Jul 1",
            EndDateText = "Sep 30",
            StartingMrr = 158_000,
            StartingAccounts = 410,
            EndingAccounts = 389,
            StartingCountNoun = "paying customers on Jul 1",
            EndingCountNoun = "paying customers on Sep 30"
        };
        period.Movements.Add(Custom(MrrMovementKind.New, "New business", "New", "New", 7_400, 21, "new customers",
            ("Birchwood Pediatrics", 1_900, "Jul · Team, 20 seats"), ("Northgate Print", 1_450, "Aug · Plus, annual")));
        period.Movements.Add(Custom(MrrMovementKind.Expansion, "Expansion", "Expansion", "Exp.", 3_100, 16, "customers grew",
            ("Saltmarsh Media", 880, "Sep · Plus, +10 seats")));
        period.Movements.Add(Custom(MrrMovementKind.Reactivation, "Reactivation", "Reactivation", "React.", 600, 3, "customers returned"));
        period.Movements.Add(Custom(MrrMovementKind.Contraction, "Contraction", "Contraction", "Contr.", 6_300, 27, "customers reduced seats",
            ("Halberd Security", 1_700, "Aug · Dropped 30 seats"), ("Redfern Freight", 1_240, "Jul · Dropped 22 seats")));
        period.Movements.Add(Custom(MrrMovementKind.Churn, "Churn", "Churn", "Churn", 14_900, 45, "customers left",
            ("Ivory Lane Hotels", 3_400, "Jul · Price review"), ("Mosswood Schools", 2_650, "Sep · Seasonal pause"),
            ("Glasshouse Labs", 2_100, "Aug · Switched to spreadsheets")));
        period.StartingBreakdown.Add(new MrrAccount("Business", 62_000, null, 70) { Key = "tier-Business" });
        period.StartingBreakdown.Add(new MrrAccount("Team", 55_000, null, 160) { Key = "tier-Team" });
        period.StartingBreakdown.Add(new MrrAccount("Plus", 41_000, null, 180) { Key = "tier-Plus" });
        period.EndingBreakdown.Add(new MrrAccount("Business", 57_500, null, 66) { Key = "tier-Business" });
        period.EndingBreakdown.Add(new MrrAccount("Team", 52_400, null, 150) { Key = "tier-Team" });
        period.EndingBreakdown.Add(new MrrAccount("Plus", 38_000, null, 173) { Key = "tier-Plus" });
        return [period];
    }

    private static MrrMovement Custom(MrrMovementKind kind, string label, string shortLabel, string compact, double amount, int count, string noun,
        params (string Name, double Amount, string Note)[] accounts)
    {
        var movement = new MrrMovement
        {
            Kind = kind, Label = label, ShortLabel = shortLabel, CompactLabel = compact, Amount = amount, AccountCount = count, CountNoun = noun
        };
        foreach (var (name, value, note) in accounts)
        {
            movement.Accounts.Add(new MrrAccount(name, value, note) { Key = name });
        }
        return movement;
    }
}
