namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The table the enrich column demos share: seven made-up companies on <c>.example</c> domains, the headcount and funding
/// column, and what each row settles into after "searching" and "reading" for a short while. Quarry Labs has no source
/// on its first attempt, so the failed state and Retry show; the retry finds it. The model is real
/// (<see cref="AiModelCatalog"/>); every company, number and address is invented. Demo content only.
/// </summary>
internal static class EnrichColumnDemoData
{
    /// <summary>The table's name in the top bar.</summary>
    public const string Title = "Target accounts";

    /// <summary>The row whose first attempt fails.</summary>
    public const string FailingId = "quarry";

    /// <summary>The AI column.</summary>
    public static EnrichColumnDefinition Column { get; } =
        new("Headcount and funding", "Find the headcount and latest funding round");

    /// <summary>The footer's model: a real model that can call a web search tool.</summary>
    public static EnrichColumnModel Model { get; } = new(AiModelCatalog.Gpt61Terra.Name, "web search");

    private static readonly (EnrichColumnRow Row, string Value, string Source, int Millis)[] s_data =
    [
        (new EnrichColumnRow("lumen", "Lumen Freight", "lumenfreight.example"), "240 people · Series B, $38M", "lumenfreight.example/about", 1500),
        (new EnrichColumnRow("harbor", "Harbor Analytics", "harboranalytics.example"), "85 people · Seed, $6M", "harboranalytics.example/news", 1200),
        (new EnrichColumnRow("kestrel", "Kestrel Robotics", "kestrel.example"), "410 people · Series C, $95M", "kestrel.example/press", 1800),
        (new EnrichColumnRow(FailingId, "Quarry Labs", "quarrylabs.example"), "32 people · Pre-seed, $1.5M", "quarrylabs.example/team", 1300),
        (new EnrichColumnRow("pinecrest", "Pinecrest Health", "pinecrest.example"), "1,200 people · Series D, $140M", "pinecrest.example/company", 1600),
        (new EnrichColumnRow("bluefin", "Bluefin Payments", "bluefin.example"), "560 people · Series C, $72M", "bluefin.example/blog", 1400),
        (new EnrichColumnRow("tidewater", "Tidewater Energy", "tidewater.example"), "150 people · Series A, $18M", "tidewater.example/investors", 1700),
    ];

    /// <summary>The seven rows.</summary>
    public static IList<EnrichColumnRow> Rows { get; } = s_data.Select(d => d.Row).ToArray();

    /// <summary>
    /// What each row settles into: "Searching for …" for 45% of its time, "Reading …" for the rest, then the value and
    /// its source. Quarry Labs fails its first attempt with "No source found".
    /// </summary>
    public static IList<EnrichColumnSimulationAnswer> Answers { get; } = s_data.Select(d => new EnrichColumnSimulationAnswer(
        d.Row.Id, d.Value, new EnrichColumnSource(d.Source, "https://" + d.Source))
    {
        Steps =
        [
            new EnrichColumnSimulationStep("Searching for " + d.Row.Name, TimeSpan.FromMilliseconds(d.Millis * 0.45)),
            new EnrichColumnSimulationStep("Reading " + d.Row.Domain, TimeSpan.FromMilliseconds(d.Millis * 0.55)),
        ],
        Error = d.Row.Id == FailingId ? "No source found" : null,
    }).ToArray();

    /// <summary>A new simulation of the table (tests and code use it; the XAML demos spell it out).</summary>
    public static EnrichColumnSimulation Simulation() => new()
    {
        Title = Title,
        Column = Column,
        Model = Model,
        Rows = Rows,
        Answers = Answers,
    };
}
