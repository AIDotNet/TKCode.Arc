namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The JourneyFlow demos' data (all invented): an online course's October trials, from the first page to a paid plan, in
/// five stages and four segments (all learners, then three channels that add up to it); and a help desk's tickets for the
/// single-segment demo.
/// </summary>
public static class JourneyFlowDemoData
{
    public const string Title = "Trial to paid journeys";

    public const string SubtitleFormat = "{0} visits in October, from first page to plan";

    /// <summary>The five stages: column header and the short header narrow charts use.</summary>
    public static JourneyStage[] Stages() =>
    [
        new("landing", "Landing", "Land"),
        new("trial", "Trial"),
        new("lesson", "First lesson", "Lesson"),
        new("week", "Week two", "Wk 2"),
        new("plan", "Plan"),
    ];

    /// <summary>Nine nodes: the main path on top of each stage, the people who left under it, the paid plan as the goal.</summary>
    public static JourneyNode[] Nodes() =>
    [
        new("arrived", "landing", "Arrived", JourneyNodeKind.Step, "Opened a course page in October"),
        new("started", "trial", "Started trial", JourneyNodeKind.Step, "Began the free trial"),
        new("left", "trial", "Left", JourneyNodeKind.Exit, "Closed the page without a trial") { DropOffLabel = "Left before the trial" },
        new("learned", "lesson", "Took a lesson", JourneyNodeKind.Step, "Finished one full lesson"),
        new("idle", "lesson", "Went quiet", JourneyNodeKind.Exit, "Never opened a lesson") { DropOffLabel = "Quiet after sign-up" },
        new("hooked", "week", "Hooked", JourneyNodeKind.Step, "Learned on three or more days"),
        new("drifting", "week", "Drifting", JourneyNodeKind.Exit, "Fewer than three study days"),
        new("subscribed", "plan", "Subscribed", JourneyNodeKind.Goal, "Picked a paid plan"),
        new("lapsed", "plan", "Lapsed", JourneyNodeKind.Exit, "Trial ended without a plan"),
    ];

    private static readonly (string Source, string Target)[] Pairs =
    [
        ("arrived", "started"), ("arrived", "left"),
        ("started", "learned"), ("started", "idle"),
        ("learned", "hooked"), ("learned", "drifting"),
        ("hooked", "subscribed"), ("hooked", "lapsed"),
        ("drifting", "subscribed"), ("drifting", "lapsed"),
    ];

    // Per channel, in the order of Pairs. "All learners" is the sum of the three channels.
    private static readonly double[] Search = [3140, 4910, 2400, 740, 1520, 880, 470, 1050, 40, 840];
    private static readonly double[] Social = [1120, 5230, 690, 430, 330, 360, 140, 190, 25, 335];
    private static readonly double[] Partners = [1060, 1330, 910, 150, 690, 220, 330, 360, 20, 200];

    /// <summary>The four segments: all learners first, then search, social and partners.</summary>
    public static JourneySegment[] Segments()
    {
        var all = new double[Pairs.Length];
        for (var i = 0; i < all.Length; i++)
        {
            all[i] = Search[i] + Social[i] + Partners[i];
        }
        return
        [
            Segment("all", "All learners", all),
            Segment("search", "Search", Search),
            Segment("social", "Social", Social),
            Segment("partners", "Partners", Partners),
        ];
    }

    /// <summary>One week of new counts for the live demo: every flow moves by up to ±18%, seeded so it repeats.</summary>
    public static double[] Jitter(double[] values, uint seed)
    {
        var state = seed;
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = Math.Round(values[i] * (0.82 + 0.36 * Next(ref state)));
        }
        return result;
    }

    /// <summary>The all-learners counts in the order of the segment's links.</summary>
    public static double[] AllLearners() => Segments()[0].Links.Select(l => l.Value).ToArray();

    /// <summary>Applies the jitter to the segment's links in place.</summary>
    public static void Apply(JourneySegment segment, double[] values)
    {
        for (var i = 0; i < segment.Links.Count && i < values.Length; i++)
        {
            segment.Links[i].Value = values[i];
        }
    }

    /// <summary>A help desk's tickets for one month (single segment): opened, then triaged or closed by a bot, and so on.</summary>
    public static JourneyStage[] TicketStages() =>
    [
        new("opened", "Opened"),
        new("first", "First reply"),
        new("outcome", "Outcome"),
    ];

    /// <summary>The ticket nodes.</summary>
    public static JourneyNode[] TicketNodes() =>
    [
        new("new", "opened", "New tickets", JourneyNodeKind.Step, "Every ticket opened in May"),
        new("agent", "first", "Agent replied", JourneyNodeKind.Step, "A person answered first"),
        new("bot", "first", "Bot closed", JourneyNodeKind.Exit, "Closed by an automatic answer") { DropOffLabel = "Closed by the bot" },
        new("solved", "outcome", "Solved", JourneyNodeKind.Goal, "Marked solved by the customer"),
        new("reopened", "outcome", "Reopened", JourneyNodeKind.Exit, "Came back after the reply"),
    ];

    /// <summary>The ticket flows.</summary>
    public static JourneySegment TicketSegment() => new("may", "May",
    [
        new JourneyLink("new", "agent", 38_400),
        new JourneyLink("new", "bot", 21_750),
        new JourneyLink("agent", "solved", 33_120),
        new JourneyLink("agent", "reopened", 5_280),
    ]);

    private static JourneySegment Segment(string key, string header, double[] values)
    {
        var segment = new JourneySegment(key, header);
        for (var i = 0; i < Pairs.Length; i++)
        {
            segment.Links.Add(new JourneyLink(Pairs[i].Source, Pairs[i].Target, values[i]));
        }
        return segment;
    }

    // mulberry32 (the Showpiece demos' generator): deterministic, so the live demo repeats.
    private static double Next(ref uint state)
    {
        state += 0x6D2B79F5;
        var t = state;
        t = (t ^ (t >> 15)) * (t | 1);
        t ^= t + (t ^ (t >> 7)) * (t | 61);
        return (t ^ (t >> 14)) / 4294967296.0;
    }
}
