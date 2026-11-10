namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="ResearchRun"/>: a question about keeping older homes warm with heat pumps, a plan,
/// seven made-up sources on <c>.example</c> sites, the notes and the cited report. With no <see cref="ResearchRun.Sources"/>
/// the run plays them live.
/// </summary>
public static class ResearchRunSamples
{
    /// <summary>The sample question.</summary>
    public const string Question = "Which changes make a heat pump work best in a drafty house built before 1950?";

    /// <summary>The sample plan.</summary>
    public static IReadOnlyList<string> Plan() =>
    [
        "Find field studies of heat pumps in older houses",
        "Compare sizing, insulation and radiator upgrades",
        "Check running costs against a gas boiler",
        "Write a ranked list of changes with sources"
    ];

    /// <summary>The sample sources (all invented), numbered 1 to 7, queued; the sixth is skipped when read.</summary>
    public static IReadOnlyList<ResearchRunSource> Sources() =>
    [
        Source(1, "Two winters of heat pumps in row houses", "homeheat-lab.example", "Field data from 42 row houses built 1890–1930."),
        Source(2, "Sizing guide for retrofit heat pumps", "retrofit-guild.example", "Room by room heat loss before choosing a unit."),
        Source(3, "Radiators that work at low flow temperatures", "warmrooms.example", "Bigger emitters let the pump run at 45 °C."),
        Source(4, "Air sealing before electrification", "tightbuild.example", "Blower door results before and after sealing."),
        Source(5, "Running costs: heat pump versus gas", "energy-notes.example", "Seasonal efficiency against two tariffs."),
        Source(6, "Forum thread: my pump is noisy", "home-forum.example", "Anecdotes only."),
        Source(7, "Cold climate performance report", "north-grid.example", "Efficiency down to −20 °C outdoor air.")
    ];

    /// <summary>The note taken from each sample source by its number (the skipped one has none).</summary>
    internal static ResearchRunNote? NoteFor(int index) => index switch
    {
        1 => new ResearchRunNote("Houses that were air sealed first used 23% less electricity for heat than those that were not.", [1]),
        2 => new ResearchRunNote("Units sized from a room by room heat loss ran in longer, steadier cycles; rule of thumb sizing led to oversizing in 3 of 5 cases.", [2]),
        3 => new ResearchRunNote("Swapping the two or three smallest radiators allowed a 45 °C flow temperature, raising seasonal efficiency from about 2.6 to 3.3.", [3, 1]),
        4 => new ResearchRunNote("Sealing the attic floor and the rim joist cut air leakage by a third for under a week of work.", [4]),
        5 => new ResearchRunNote("At a seasonal efficiency of 3 or more, heating cost less than with a gas boiler on both tariffs.", [5]),
        7 => new ResearchRunNote("Modern cold climate units kept an efficiency above 2 down to −15 °C.", [7]),
        _ => null
    };

    /// <summary>The why of the skipped sample source.</summary>
    internal const string SkippedReason = "Anecdotes only; left out.";

    /// <summary>The sample report: four paragraphs citing sources by their numbers in brackets.</summary>
    public const string Report =
        "Seal the house before anything else. In two winters of field data, houses that were air sealed first used about a " +
        "quarter less electricity for heat [1], and the biggest leaks, the attic floor and the rim joist, take less than a week " +
        "to close [4].\n\n" +
        "Size the heat pump from a room by room heat loss, not from the old boiler. Units sized this way ran in long, steady " +
        "cycles, while rule of thumb sizing overshot in most homes [2].\n\n" +
        "Replace the smallest radiators so the pump can run at 45 °C. That one change raised seasonal efficiency from about " +
        "2.6 to 3.3 [3][1], and at 3 or more the pump heats for less than a gas boiler [5].\n\n" +
        "In very cold places, choose a cold climate model: they keep an efficiency above 2 down to −15 °C [7].";

    private static ResearchRunSource Source(int index, string title, string domain, string snippet) => new()
    {
        Index = index,
        Title = title,
        Domain = domain,
        Snippet = snippet,
        Url = new Uri($"https://{domain}/")
    };
}
