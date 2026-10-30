using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The runs the agent status demos play: a coding agent and a research agent, named after real models from
/// <see cref="AiModelCatalog"/>. The tasks, file names, counts and texts are invented. They keep the reference's rhythm:
/// the coding run takes six steps of 1.9, 1.5, 2.5, 1.9 and 3.3 seconds (the fifth counting 12 specs in place) and then
/// waits for an approval for as long as it takes; the research run takes five steps, and its third fails once after five
/// of nine pages so Retry can be seen. Demo content only; the marks are Lucide glyphs, not the vendors' logos.
/// </summary>
internal static class AgentStatusDemoData
{
    // Lucide glyphs (ISC) the runs use that Core ArcIcons does not ship.
    private static readonly Geometry s_fileSearch = ArcIcons.Parse(
        "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z " +
        "M14 2v5a1 1 0 0 0 1 1h5 M9 14.5a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0 -5 0z M13.3 16.3 15 18");

    private static readonly Geometry s_filePen = ArcIcons.Parse(
        "M12.659 22H18a2 2 0 0 0 2-2V8a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 14 2H6a2 2 0 0 0-2 2v9.34 " +
        "M14 2v5a1 1 0 0 0 1 1h5 M10.378 12.622a1 1 0 0 1 3 3.003L8.36 20.637a2 2 0 0 1-.854.506l-2.867.837a.5.5 0 0 1-.62-.62l.836-2.869a2 2 0 0 1 .506-.853z");

    private static readonly Geometry s_files = ArcIcons.Parse(
        "M15 2h-4a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V8 M16.706 2.706A2.4 2.4 0 0 0 15 2v5a1 1 0 0 0 1 1h5a2.4 2.4 0 0 0-.706-1.706z " +
        "M5 7a2 2 0 0 0-2 2v11a2 2 0 0 0 2 2h8a2 2 0 0 0 1.732-1");

    private static readonly Geometry s_flaskConical = ArcIcons.Parse(
        "M14 2v6a2 2 0 0 0 .245.96l5.51 10.08A2 2 0 0 1 18 22H6a2 2 0 0 1-1.755-2.96l5.51-10.08A2 2 0 0 0 10 8V2 M6.453 15h11.094 M8.5 2h7");

    private static readonly Geometry s_scale = ArcIcons.Parse(
        "M12 3v18 M19 8l3 8a5 5 0 0 1-6 0zV7 M3 7h1a17 17 0 0 0 8-2 17 17 0 0 0 8 2h1 M5 8l3 8a5 5 0 0 1-6 0zV7 M7 21h10");

    private static readonly Geometry s_notebookPen = ArcIcons.Parse(
        "M13.4 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-7.4 M2 6h4 M2 10h4 M2 14h4 M2 18h4 " +
        "M21.378 5.626a1 1 0 1 0-3.004-3.004l-5.01 5.012a2 2 0 0 0-.506.854l-.837 2.87a.5.5 0 0 0 .62.62l2.87-.837a2 2 0 0 0 .854-.506z");

    private static readonly Geometry s_hexagon = ArcIcons.Parse(
        "M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z");

    private static readonly Geometry s_orbit = ArcIcons.Parse(
        "M20.341 6.484A10 10 0 0 1 10.266 21.85 M3.659 17.516A10 10 0 0 1 13.74 2.152 M9 12a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z " +
        "M17 5a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z M3 19a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");

    /// <summary>The coding agent: Claude Sonnet 5.5 (catalog name) with a hexagon mark in deep orange.</summary>
    public static AgentModel CodingAgent { get; } =
        new(AiModelCatalog.ClaudeSonnet55.Name, s_hexagon, new ImmutableSolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));

    /// <summary>The research agent: Gemini 4 Argon (catalog name) with an orbit mark in blue.</summary>
    public static AgentModel ResearchAgent { get; } =
        new(AiModelCatalog.Gemini4Argon.Name, s_orbit, new ImmutableSolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));

    /// <summary>
    /// A coding agent fixes a date parsing bug: it scans, looks up a function, patches a file, adjusts tests, checks 12
    /// specs, then asks to deploy to staging and waits. Approve deploys (1.4 seconds) and finishes; Deny finishes without it.
    /// </summary>
    public static AgentRunScenario Coding { get; } = new()
    {
        Id = "coding",
        Agent = CodingAgent,
        Outcome = "5 files touched",
        DeniedOutcome = "5 files touched, not deployed",
        ApprovedNotice = "Approved. The deploy is only simulated here.",
        DeniedNotice = "Denied. Nothing was deployed.",
        Steps =
        [
            new()
            {
                Id = "scan", ActiveLabel = "Scanning 9 modules", DoneLabel = "Scanned 9 modules",
                Icon = s_fileSearch, Duration = TimeSpan.FromMilliseconds(1917)
            },
            new()
            {
                Id = "lookup", ActiveLabel = "Looking up parseDueDate", DoneLabel = "Located 3 usages",
                Icon = ArcIcons.Search, Duration = TimeSpan.FromMilliseconds(1499)
            },
            new()
            {
                Id = "patch", ActiveLabel = "Patching lib/dates/parse-due.ts", DoneLabel = "Patched lib/dates/parse-due.ts",
                Icon = s_filePen, Duration = TimeSpan.FromMilliseconds(2533)
            },
            new()
            {
                Id = "tests", ActiveLabel = "Adjusting 4 test suites", DoneLabel = "Adjusted 4 test suites",
                Icon = s_files, Duration = TimeSpan.FromMilliseconds(1917)
            },
            new()
            {
                Id = "specs", ActiveLabel = "Checking specs", DoneLabel = "12 of 12 specs green",
                Icon = s_flaskConical, Duration = TimeSpan.FromMilliseconds(3316),
                ProgressTotal = 12, ProgressInterval = TimeSpan.FromMilliseconds(279)
            },
            new()
            {
                Id = "deploy", ActiveLabel = "Deploying build 2.4.1", DoneLabel = "Deployed build 2.4.1",
                Icon = ArcIcons.CloudUpload, Duration = TimeSpan.FromMilliseconds(1440),
                Request = "Waiting on you to deploy to staging", WaitingLabel = "Approval needed to deploy to staging",
                SkippedLabel = "Deploy skipped"
            }
        ]
    };

    /// <summary>
    /// A research agent compares laptop warranty terms: it drafts questions, queries search engines, opens nine pages
    /// (failing once after five, so Retry can be seen), weighs the terms and writes a summary.
    /// </summary>
    public static AgentRunScenario Research { get; } = new()
    {
        Id = "research",
        Agent = ResearchAgent,
        Outcome = "8 pages cited",
        RetryNotice = "Trying the pages that did not load again.",
        Steps =
        [
            new()
            {
                Id = "plan", ActiveLabel = "Drafting 3 sub-questions", DoneLabel = "Drafted 3 sub-questions",
                Icon = ArcIcons.ListChecks, Duration = TimeSpan.FromMilliseconds(1433)
            },
            new()
            {
                Id = "search", ActiveLabel = "Querying 2 search engines", DoneLabel = "Collected 21 links",
                Icon = ArcIcons.Globe, Duration = TimeSpan.FromMilliseconds(1917)
            },
            new()
            {
                Id = "read", ActiveLabel = "Opening pages", DoneLabel = "Opened 9 pages",
                Icon = ArcIcons.BookOpen, Duration = TimeSpan.FromMilliseconds(3273),
                ProgressTotal = 9, ProgressInterval = TimeSpan.FromMilliseconds(363),
                FailAtProgress = 5, Failure = "Fetch failed · 2 pages did not load", FailedLabel = "2 pages did not load",
                RetryDuration = TimeSpan.FromMilliseconds(1456)
            },
            new()
            {
                Id = "compare", ActiveLabel = "Weighing warranty terms across 4 shops", DoneLabel = "Weighed 4 shops",
                Icon = s_scale, Duration = TimeSpan.FromMilliseconds(2217)
            },
            new()
            {
                Id = "write", ActiveLabel = "Drafting the summary", DoneLabel = "Wrote a 520 word summary",
                Icon = s_notebookPen, Duration = TimeSpan.FromMilliseconds(2350)
            }
        ]
    };

    /// <summary>An approval request ("Needs your approval to deploy"), for pills that wait on a person.</summary>
    public static AgentStatusActivity ApprovalRequest { get; } = new("Needs your approval to deploy", ArcIcons.Search);

    /// <summary>A reading activity ("Reading 12 files"), for pills that are working.</summary>
    public static AgentStatusActivity Reading { get; } = new("Reading 12 files", ArcIcons.Search);
}
