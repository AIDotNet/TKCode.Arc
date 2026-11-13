using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AgentGallery"/> (its defaults): six categories and fourteen agents from made-up
/// makers, and replaceable simulated install handlers for demos. No name or mark belongs to a real product.
/// </summary>
public static class AgentGallerySamples
{
    /// <summary>How long the built-in simulated install takes: 1.2 seconds.</summary>
    public static readonly TimeSpan SimulatedInstallTime = TimeSpan.FromMilliseconds(1200);

    /// <summary>The six sample categories: Writing, Research, Coding, Data, Operations and Support.</summary>
    public static IReadOnlyList<AgentGalleryCategory> Categories() =>
    [
        new("writing", "Writing"),
        new("research", "Research"),
        new("coding", "Coding"),
        new("data", "Data"),
        new("operations", "Operations"),
        new("support", "Support")
    ];

    /// <summary>The fourteen sample agents, in no particular order (the gallery sorts them).</summary>
    public static IReadOnlyList<AgentGalleryItem> Agents() =>
    [
        Agent("quill-draft", "Quill Draft", "Fernhill Studio", true, "writing", ArcIcons.PenLine, AgentGalleryTint.Series1,
            "Turns rough notes into a first draft in your house style.",
            "Paste bullet points, a transcript or a messy outline and Quill Draft writes a clean first draft. It learns your tone from three examples you pick, keeps your headings, and marks every sentence it was unsure about so you know where to look.",
            4.8, 2_310, 48_200, new DateOnly(2026, 3, 14), featured: true,
            ["Drafting", "Tone matching", "Outlines"],
            ["Write a draft from notes, outlines or transcripts", "Match the tone of examples you choose", "Flag the sentences it is least sure about"],
            [new("Read documents you share with it", "Only the files you attach to a chat.")],
            ["Turn these notes into a one-page update", "Rewrite this paragraph to sound warmer", "Make an outline for a launch post"]),
        Agent("inbox-sorter", "Inbox Sorter", "Tidewater Labs", true, "operations", ArcIcons.Inbox, AgentGalleryTint.Series2,
            "Sorts new mail into folders and drafts the easy replies.",
            "Inbox Sorter reads new mail as it arrives, files it by rules you can read and edit, and drafts replies to routine questions. Drafts wait for you; nothing is sent until you press Send.",
            4.6, 1_874, 61_500, new DateOnly(2025, 11, 2), featured: false,
            ["Email", "Triage", "Drafts"],
            ["File new mail into folders by readable rules", "Draft replies to routine questions", "Summarize long threads in three lines"],
            [new("Read your mail", "To sort it and write drafts."), new("Move mail between folders", "Only into folders you name."), new("Create drafts", "Drafts are never sent on their own.", isSensitive: true)],
            ["What needs my answer today?", "Summarize the thread about the venue", "Draft a polite no to this invite"]),
        Agent("meeting-scribe", "Meeting Scribe", "Copperleaf", false, "operations", ArcIcons.Mic, AgentGalleryTint.Series3,
            "Takes notes in calls and sends the action items after.",
            "Meeting Scribe joins the calls you invite it to, writes notes as people talk and lists who promised what. After the call it posts a short summary to the channel you choose.",
            4.4, 963, 22_800, new DateOnly(2026, 6, 20), featured: false,
            ["Meetings", "Notes", "Action items"],
            ["Write notes during calls", "List action items with owners and dates", "Post a summary after the call"],
            [new("Join calls you invite it to"), new("Hear and record audio", "Recordings are deleted after 30 days.", isSensitive: true), new("Post to one channel", "The channel you pick when you install it.")],
            ["What did we decide about pricing?", "List my action items from this week", "Write a recap for people who missed it"]),
        Agent("lit-scout", "Lit Scout", "Bramble & Co", true, "research", ArcIcons.BookOpen, AgentGalleryTint.Series4,
            "Finds papers on a question and sums up what they agree on.",
            "Ask Lit Scout a research question and it searches open paper archives, reads the abstracts and full texts it can reach, and writes a short review: where the papers agree, where they differ and which ones to read first. Every claim links to its source.",
            4.9, 1_402, 18_700, new DateOnly(2026, 8, 5), featured: true,
            ["Papers", "Citations", "Reviews"],
            ["Search open archives for papers on a question", "Compare findings across papers", "Cite every claim it makes"],
            [new("Browse the web", "Only open paper archives.")],
            ["What is known about sleep and memory in teens?", "Find three papers that disagree with this one", "Summarize the methods of these studies"]),
        Agent("market-lens", "Market Lens", "Saltmarsh Tools", false, "research", ArcIcons.TrendingUp, AgentGalleryTint.Series1,
            "Tracks competitors and tells you what changed each week.",
            "Market Lens watches the public pages of companies you name: pricing, release notes and job posts. Each Monday it sends what changed and why it may matter.",
            4.2, 611, 9_400, new DateOnly(2026, 9, 28), featured: false,
            ["Competitors", "Weekly digest"],
            ["Watch public pages for changes", "Send a weekly digest of what changed", "Compare prices across companies"],
            [new("Browse the web", "Only the pages you list.")],
            ["What changed in the market this week?", "Compare the plans of these three tools", "Which companies are hiring for sales?"]),
        Agent("patch-pilot", "Patch Pilot", "Northpine", true, "coding", ArcIcons.Bug, AgentGalleryTint.Series2,
            "Reads a bug report, finds the cause and opens a fix for review.",
            "Patch Pilot reads a bug report, reproduces it in a sandbox, finds the change that caused it and opens a pull request with a fix and a test. You review and merge; it never pushes to the main branch.",
            4.7, 2_048, 35_900, new DateOnly(2026, 1, 9), featured: true,
            ["Bugs", "Pull requests", "Tests"],
            ["Reproduce a bug in a sandbox", "Find the change that caused it", "Open a pull request with a fix and a test"],
            [new("Read your repositories"), new("Open pull requests", "It cannot merge or push to protected branches.", isSensitive: true), new("Run code in a sandbox", "Nothing runs on your machines.")],
            ["Why does checkout fail for guests?", "Find what broke the nightly build", "Add a test for this bug"]),
        Agent("test-weaver", "Test Weaver", "Northpine", true, "coding", ArcIcons.ListChecks, AgentGalleryTint.Series3,
            "Writes the missing tests for the code you change.",
            "Test Weaver looks at each change you make, finds the paths no test covers and writes tests for them in the style your project already uses.",
            4.5, 788, 14_100, new DateOnly(2026, 10, 18), featured: false,
            ["Tests", "Coverage"],
            ["Find code paths no test covers", "Write tests in your project's style", "Explain why a test fails"],
            [new("Read your repositories"), new("Comment on pull requests")],
            ["Which parts of this change have no tests?", "Write tests for the date parser", "Why is this test flaky?"]),
        Agent("query-smith", "Query Smith", "Copperleaf", false, "data", ArcIcons.Database, AgentGalleryTint.Series4,
            "Answers questions about your data by writing the query for you.",
            "Ask a question in plain words and Query Smith writes the query, runs it on a read-only copy and shows the result with the query beside it, so you can check its work.",
            4.3, 1_156, 27_300, new DateOnly(2025, 12, 1), featured: false,
            ["SQL", "Questions", "Read-only"],
            ["Write queries from plain questions", "Run them on a read-only copy", "Explain a query line by line"],
            [new("Read your database", "Read-only; it cannot change rows.")],
            ["How many orders came from new customers last month?", "Which plans churn the most?", "Explain this query"]),
        Agent("chart-sketch", "Chart Sketch", "Fernhill Studio", true, "data", ArcIcons.ChartBar, AgentGalleryTint.Series1,
            "Makes a clear chart from a table and says what it shows.",
            "Give Chart Sketch a table or a file and it picks a chart that fits the data, labels it plainly and writes two sentences on what stands out.",
            4.6, 534, 8_900, new DateOnly(2026, 10, 30), featured: false,
            ["Charts", "Tables"],
            ["Pick a chart that fits the data", "Label axes and units plainly", "Say what stands out in two sentences"],
            [new("Read files you share with it")],
            ["Chart signups by week", "What stands out in this table?", "Make this chart easier to read"]),
        Agent("ledger-check", "Ledger Check", "Saltmarsh Tools", true, "data", ArcIcons.DollarSign, AgentGalleryTint.Series2,
            "Matches invoices to payments and lists what does not add up.",
            "Ledger Check compares invoices with bank lines each night, matches what it can and lists the rest with a likely reason, so month end takes hours instead of days.",
            4.4, 402, 6_100, new DateOnly(2026, 7, 11), featured: false,
            ["Finance", "Reconciling"],
            ["Match invoices to payments", "List the lines that do not match", "Suggest a likely reason for each"],
            [new("Read invoices and bank lines", "Read-only."), new("Read your vendor list")],
            ["What is still open for October?", "Why does this payment not match?", "List duplicate invoices"]),
        Agent("ticket-triage", "Ticket Triage", "Tidewater Labs", true, "support", ArcIcons.Flag, AgentGalleryTint.Series3,
            "Tags new tickets, sets their priority and routes them to a team.",
            "Ticket Triage reads each new ticket, tags its topic, sets a priority from rules you can see, and sends it to the right team. It learns from the changes your team makes.",
            4.7, 1_689, 41_000, new DateOnly(2026, 2, 22), featured: true,
            ["Tickets", "Routing", "Priority"],
            ["Tag tickets by topic", "Set a priority from readable rules", "Route each ticket to a team"],
            [new("Read and tag tickets"), new("Change ticket priority and team", isSensitive: true)],
            ["Which tickets are urgent right now?", "What are people asking about this week?", "Route this ticket"]),
        Agent("reply-coach", "Reply Coach", "Bramble & Co", false, "support", ArcIcons.MessageCircle, AgentGalleryTint.Series4,
            "Suggests answers to customers from your help center.",
            "Reply Coach suggests an answer for each conversation using your help center and past replies, with the articles it used. Your team edits and sends.",
            0, 0, 1_200, new DateOnly(2026, 11, 4), featured: false,
            ["Replies", "Help center"],
            ["Suggest replies from your help center", "Link the articles it used", "Keep your team's tone"],
            [new("Read conversations"), new("Read your help center")],
            ["How do I answer a refund request?", "Make this reply shorter", "Which article covers exports?"]),
        Agent("release-notes", "Release Notes", "Northpine", true, "writing", ArcIcons.FileText, AgentGalleryTint.Series2,
            "Writes release notes people want to read from merged changes.",
            "Release Notes reads the changes merged since your last release and writes notes for customers: what is new, what got better and what was fixed, in plain words.",
            4.5, 317, 5_600, new DateOnly(2026, 9, 3), featured: false,
            ["Changelog", "Writing"],
            ["Collect changes since the last release", "Group them into new, better and fixed", "Write them in plain words"],
            [new("Read your repositories")],
            ["Write notes for this release", "Make these notes shorter", "What changed for admins?"]),
        Agent("standup-digest", "Standup Digest", "Copperleaf", false, "operations", ArcIcons.Clock, AgentGalleryTint.Neutral,
            "Asks your team three questions each morning and shares a digest.",
            "Standup Digest asks each person what they did, what is next and what is in the way, then posts one short digest so nobody has to sit through a meeting.",
            4.1, 244, 7_800, new DateOnly(2026, 5, 16), featured: false,
            ["Standups", "Team"],
            ["Ask the team three questions each morning", "Post one short digest", "Point out who is blocked"],
            [new("Send direct messages to your team"), new("Post to one channel")],
            ["Who is blocked today?", "What did the team ship this week?", "Skip standup on Fridays"])
    ];

    /// <summary>An install handler that waits <paramref name="delay"/> and succeeds (a stand-in for your backend).</summary>
    /// <param name="delay">How long the install takes.</param>
    public static Func<AgentGalleryItem, CancellationToken, Task> SimulatedInstall(TimeSpan delay)
        => (_, cancellation) => Task.Delay(delay, cancellation);

    /// <summary>
    /// An install handler that fails the first <paramref name="failures"/> installs with <paramref name="message"/> and then
    /// succeeds, each after <paramref name="delay"/>.
    /// </summary>
    /// <param name="delay">How long each attempt takes.</param>
    /// <param name="failures">How many attempts fail first.</param>
    /// <param name="message">The message of the failure.</param>
    public static Func<AgentGalleryItem, CancellationToken, Task> FailingInstall(TimeSpan delay, int failures = 1,
        string message = "The workspace admin has not approved this agent yet.")
    {
        var left = failures;
        return async (_, cancellation) =>
        {
            await Task.Delay(delay, cancellation);
            if (left > 0)
            {
                left--;
                throw new InvalidOperationException(message);
            }
        };
    }

    private static AgentGalleryItem Agent(string id, string name, string maker, bool verified, string category, Geometry glyph,
        AgentGalleryTint tint, string pitch, string description, double rating, int ratingCount, long installs, DateOnly added,
        bool featured, string[] tags, string[] abilities, AgentGalleryPermission[] permissions, string[] prompts) => new()
    {
        Id = id,
        Name = name,
        Maker = maker,
        IsVerified = verified,
        CategoryKey = category,
        Glyph = glyph,
        Tint = tint,
        Pitch = pitch,
        Description = description,
        Rating = rating,
        RatingCount = ratingCount,
        Installs = installs,
        AddedOn = added,
        IsFeatured = featured,
        Tags = tags,
        Abilities = abilities,
        Permissions = permissions,
        SamplePrompts = prompts
    };
}
