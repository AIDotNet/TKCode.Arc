namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented call the meeting summary demos play: a pricing page relaunch sync with five people, its summary,
/// decisions and action items, each with the transcript quote it came from. People, product and numbers are made up;
/// the model credited is a real one from <see cref="AiModelCatalog"/>. Demo content only.
/// </summary>
internal static class MeetingSummaryDemoData
{
    private static TimeSpan At(int minutes, int seconds) => new(0, minutes, seconds);

    /// <summary>The notes of the call (the card never changes them).</summary>
    public static MeetingSummaryData Meeting { get; } = new()
    {
        Title = "Pricing page relaunch sync",
        Platform = "Video call",
        StartsAt = new DateTime(2026, 10, 28, 14, 0, 0),
        DurationMinutes = 34,
        TranscriptWords = 4812,
        Attendees =
        [
            new("maya", "Maya Lindqvist", "Design"),
            new("daniel", "Daniel Okafor", "Engineering"),
            new("priya", "Priya Raman", "Marketing"),
            new("tom", "Tom Becker", "Product"),
            new("lena", "Lena Moreau", "Support"),
        ],
        Summary =
        [
            new("s1", "The team reviewed the new pricing page draft and agreed the three-tier layout reads clearly on desktop.",
                new("maya", At(2, 14), "I think the three columns finally work. People see the middle plan first, which is what we want.")),
            new("s2", "Support sees most confusion around seat limits, so the comparison table needs a clearer seats row.",
                new("lena", At(9, 41), "Half of the tickets this month were about seats. Nobody can tell what happens when you add the sixth person.")),
            new("s3", "Engineering can ship the page behind a flag next week once the checkout copy is final.",
                new("daniel", At(17, 5), "The page itself is done. If copy lands by Monday I can put it behind the flag on Tuesday.")),
        ],
        Decisions =
        [
            new("d1", "Launch with annual billing selected by default, with the monthly price shown beside it.",
                new("tom", At(21, 30), "Let's default to annual and keep monthly right next to it, so nobody feels tricked.")),
            new("d2", "Drop the enterprise calculator from this release and link to sales instead.",
                new("priya", At(26, 52), "The calculator is a whole project. For now a Talk to sales link is enough.")),
        ],
        ActionItems =
        [
            new("a1", "Redesign the seats row in the comparison table", "maya", new DateOnly(2026, 10, 31),
                new("maya", At(11, 8), "I'll take the seats row. I'll have something to look at by Friday.")),
            new("a2", "Finalize the checkout copy and send it for review", "priya", new DateOnly(2026, 11, 2),
                new("priya", At(18, 20), "I'll write the checkout copy over the weekend and share it Monday morning.")),
            new("a3", "Put the new page behind the pricing_v2 flag", "daniel", new DateOnly(2026, 11, 4),
                new("daniel", At(19, 2), "Flag is pricing_v2. I'll wire it up as soon as the copy is in.")),
            new("a4", "Draft a help article on how seat limits work", "lena", new DateOnly(2026, 11, 6),
                new("lena", At(29, 47), "I can write the help article so we have something to link from the table.")),
        ],
    };

    /// <summary>The model credited in the footer (a neutral mark; no brand mark is drawn).</summary>
    public static AgentModel Model { get; } = new(AiModelCatalog.Qwen38Max.Name, ArcIcons.Sparkles);

    /// <summary>The references simulated sends hand out: "ISS-142" in Issues, "Notes 143" in Docs.</summary>
    public static IReadOnlyDictionary<string, string> ReferenceFormats { get; } = new Dictionary<string, string>
    {
        [MeetingSummarySendTarget.Issues.Id] = "ISS-{0}",
        [MeetingSummarySendTarget.Docs.Id] = "Notes {0}",
    };

    public const int FirstReferenceNumber = 142;
}
