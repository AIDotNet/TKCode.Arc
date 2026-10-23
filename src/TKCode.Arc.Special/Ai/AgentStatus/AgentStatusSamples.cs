using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Two invented runs for <see cref="AgentRunSimulation"/> (the agents, file names and texts are made up). They keep the
/// reference's rhythm: the coding run takes six steps of 1.9, 1.5, 2.5, 1.9 and 3.3 seconds (the fifth counting 12 specs
/// in place) and then waits for an approval for as long as it takes; the research run takes five steps, and its third
/// fails once after five of nine pages so Retry can be seen.
/// </summary>
public static class AgentStatusSamples
{
    /// <summary>The coding agent: "Juniper Code 3" with a hexagon mark in deep orange.</summary>
    public static AgentModel CodingAgent { get; } =
        new("Juniper Code 3", AgentStatusIcons.Hexagon, new ImmutableSolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));

    /// <summary>The research agent: "Atlas Scout 2" with an orbit mark in blue.</summary>
    public static AgentModel ResearchAgent { get; } =
        new("Atlas Scout 2", AgentStatusIcons.Orbit, new ImmutableSolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));

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
                Icon = AgentStatusIcons.FileSearch, Duration = TimeSpan.FromMilliseconds(1917)
            },
            new()
            {
                Id = "lookup", ActiveLabel = "Looking up parseDueDate", DoneLabel = "Located 3 usages",
                Icon = ArcIcons.Search, Duration = TimeSpan.FromMilliseconds(1499)
            },
            new()
            {
                Id = "patch", ActiveLabel = "Patching lib/dates/parse-due.ts", DoneLabel = "Patched lib/dates/parse-due.ts",
                Icon = AgentStatusIcons.FilePen, Duration = TimeSpan.FromMilliseconds(2533)
            },
            new()
            {
                Id = "tests", ActiveLabel = "Adjusting 4 test suites", DoneLabel = "Adjusted 4 test suites",
                Icon = AgentStatusIcons.Files, Duration = TimeSpan.FromMilliseconds(1917)
            },
            new()
            {
                Id = "specs", ActiveLabel = "Checking specs", DoneLabel = "12 of 12 specs green",
                Icon = AgentStatusIcons.FlaskConical, Duration = TimeSpan.FromMilliseconds(3316),
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
                Icon = AgentStatusIcons.Scale, Duration = TimeSpan.FromMilliseconds(2217)
            },
            new()
            {
                Id = "write", ActiveLabel = "Drafting the summary", DoneLabel = "Wrote a 520 word summary",
                Icon = AgentStatusIcons.NotebookPen, Duration = TimeSpan.FromMilliseconds(2350)
            }
        ]
    };

    /// <summary>An approval request ("Needs your approval to deploy"), for pills that wait on a person.</summary>
    public static AgentStatusActivity ApprovalRequest { get; } = new("Needs your approval to deploy", ArcIcons.Search);

    /// <summary>A reading activity ("Reading 12 files"), for pills that are working.</summary>
    public static AgentStatusActivity Reading { get; } = new("Reading 12 files", ArcIcons.Search);

    /// <summary>Both runs: coding, then research.</summary>
    public static IReadOnlyList<AgentRunScenario> All { get; } = [Coding, Research];
}
