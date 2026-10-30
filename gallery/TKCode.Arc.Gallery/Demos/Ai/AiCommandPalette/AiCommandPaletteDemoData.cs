using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The invented workspace the AI command palette demos search and ask about: pages and actions, three suggested questions
/// and the canned answers of the simulated model, with sources and actions that pretend to run. The people, customers and
/// numbers are made up; the model name is a real one from <see cref="AiModelCatalog"/>. The marks are neutral Lucide glyphs.
/// </summary>
internal static class AiCommandPaletteDemoData
{
    private const string Pages = "Pages";
    private const string Actions = "Actions";
    private const string Recent = "Recent";

    /// <summary>The model credited with the answers: xAI's Grok 4.7 (checked on <see cref="AiModelCatalog.CheckedOn"/>).</summary>
    public static readonly string ModelName = AiModelCatalog.Grok47.Name;

    /// <summary>What the walkthrough searches for.</summary>
    public const string SearchQuery = "inv";

    /// <summary>The pages and actions.</summary>
    public static readonly IReadOnlyList<AiCommandPaletteItem> Items =
    [
        Item("inbox", "Inbox", Pages, ArcIcons.Inbox, "12 unread", "G I", "messages notifications", suggested: true),
        Item("my-issues", "My issues", Pages, ArcIcons.CircleDot, "8 open", "G M", "assigned tasks", suggested: true),
        Item("projects", "Projects", Pages, ArcIcons.Layers, null, "G P", "roadmap"),
        Item("billing", "Billing", Pages, ArcIcons.CreditCard, "Settings", null, "invoice payment card plan subscription"),
        Item("invoices", "Invoices", Pages, ArcIcons.FileSpreadsheet, "Billing", null, "payment receipts"),
        Item("members", "Team members", Pages, ArcIcons.Users, "Settings", null, "people invite roles"),
        Item("settings", "Settings", Pages, ArcIcons.Settings, null, "⌘ ,", "preferences account"),
        Item("roadmap", "Q3 roadmap", Recent, ArcIcons.FileText, "Doc · Product", null, "plan quarter", suggested: true),
        Item("incident", "billing-api 502s", Recent, ArcIcons.TriangleAlert, "Incident · Resolved 2d ago", null, "outage error deploy"),
        Item("new-issue", "Create issue", Actions, ArcIcons.Plus, null, "C", "new bug task ticket", suggested: true),
        Item("invite", "Invite teammate", Actions, ArcIcons.Mail, null, null, "add member people"),
        Item("theme", "Switch theme", Actions, ArcIcons.SunMoon, "Light · Dark", "⌘ ⇧ L", "appearance dark mode", suggested: true),
        Item("copy-link", "Copy link to page", Actions, ArcIcons.Link, null, "⌘ L", "share url"),
        Item("log-out", "Log out", Actions, ArcIcons.LogOut, null, null, "sign out"),
    ];

    /// <summary>The questions offered while Ask AI is open and empty.</summary>
    public static readonly IReadOnlyList<string> Questions =
    [
        "Why did the March invoice for Acme fail?",
        "What shipped in the last release?",
        "Who owns the onboarding flow?",
    ];

    /// <summary>The canned answers; the last one, without keywords, answers anything else.</summary>
    public static readonly IReadOnlyList<AiCommandPaletteSimulationAnswer> Answers =
    [
        new()
        {
            Keywords = "invoice acme march fail payment billing",
            Sources =
            [
                new("card", "Acme · payment history", "Acme’s payment history: the card on file expired on March 1.", ArcIcons.CreditCard),
                new("pr", "billing-api #1893", "Pull request billing-api #1893: roll back the retry worker after 502s during the deploy.", ArcIcons.GitBranch),
                new("ticket", "Support ticket 4417", "Support ticket 4417: card expiry notice sent to Acme’s billing contact.", ArcIcons.MessageSquare),
            ],
            Text = "The March invoice for Acme failed because the card on file expired on March 1 [1]. The automatic retry on March 4 then hit "
                + "a 502 from billing-api during a deploy that was rolled back the same day [2].\n\nSupport already emailed Acme’s billing "
                + "contact about the expired card, and the account is in its 14 day grace period [3]. Once the card is updated, retrying "
                + "the charge should settle the invoice. The fix that keeps retries from running during a deploy is still open.",
            Actions =
            [
                new("issue", "Create issue in Linear", "ENG · Billing", ArcIcons.CircleDot, "Created ENG-2481"),
                new("reply", "Draft reply to Acme", "Support", ArcIcons.Mail, "Draft saved to Support"),
                new("runbook", "Open billing runbook in Notion", "Docs", ArcIcons.BookOpen, "Opened Billing runbook"),
            ],
        },
        new()
        {
            Keywords = "shipped ship release released changelog new",
            Sources =
            [
                new("notes", "Release notes 4.12", "Release notes for version 4.12, published on Tuesday.", ArcIcons.FileText),
                new("channel", "#eng-releases", "The #eng-releases channel: the release thread for 4.12.", ArcIcons.Hash),
            ],
            Text = "Version 4.12 went out on Tuesday [1]. It adds saved views to the issue list, bulk editing for labels and a faster search "
                + "index that cuts typical queries to under 80 ms [1].\n\nTwo fixes rode along: invoices no longer retry during a deploy, "
                + "and exports keep their column order [2]. Nothing was rolled back.",
            Actions =
            [
                new("post", "Post summary to Slack", "#eng-releases", ArcIcons.MessageSquare, "Posted to #eng-releases"),
                new("notes", "Open release notes", "Docs", ArcIcons.FileText, "Opened Release notes 4.12"),
            ],
        },
        new()
        {
            Keywords = "owns owner onboarding who flow",
            Sources =
            [
                new("directory", "Team directory", "The team directory: Growth team members and their areas.", ArcIcons.Users),
                new("roadmap", "Q3 roadmap", "The Q3 roadmap: onboarding checklist work and its owner.", ArcIcons.FileText),
            ],
            Text = "Priya Shah on the Growth team owns the onboarding flow [1]. She took it over in July and is leading the new checklist "
                + "work on the Q3 roadmap [2].\n\nFor design questions, Marco Diaz is the designer on the project [1].",
            Actions =
            [
                new("message", "Message Priya Shah", "Direct message", ArcIcons.MessageCircle, "Message sent to Priya"),
                new("roadmap", "Open Q3 roadmap", "Docs", ArcIcons.FileText, "Opened Q3 roadmap"),
            ],
        },
        new()
        {
            Sources =
            [
                new("search", "Workspace search", "Pages and docs in this workspace that mention the question.", ArcIcons.Search),
                new("docs", "Team handbook", "The team handbook, the closest page to the question.", ArcIcons.BookOpen),
            ],
            Text = "I looked through the workspace for “{question}”. The closest matches are a few pages in the workspace search [1] "
                + "and the team handbook [2], but none of them answers it directly.\n\nTry naming a project, a customer or a person, "
                + "or open the handbook to read on.",
            Actions =
            [
                new("handbook", "Open team handbook", "Docs", ArcIcons.BookOpen, "Opened Team handbook"),
            ],
        },
    ];

    private static AiCommandPaletteItem Item(string id, string label, string group, Geometry icon, string? meta, string? shortcut,
        string keywords, bool suggested = false) =>
        new(id, label, group, icon, meta) { Shortcut = shortcut, Keywords = keywords, IsSuggested = suggested };
}
