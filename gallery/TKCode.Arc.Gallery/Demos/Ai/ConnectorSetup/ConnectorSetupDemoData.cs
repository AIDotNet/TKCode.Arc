namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The apps the connector setup demos list: eight generic kinds of app (no real products) with an invented company's
/// accounts and counts, two of them connected, and the scripted run (Chat connects, then a CRM attempt needs admin
/// approval and is retried). The assistant is an invented one. Demo content only.
/// </summary>
internal static class ConnectorSetupDemoData
{
    /// <summary>The assistant named in the texts (an invented one).</summary>
    public const string AssistantName = "Atlas";

    /// <summary>The ids connected when the card first shows.</summary>
    public static IReadOnlyList<string> Connected { get; } = ["code", "drive"];

    /// <summary>The scripted run: connect Chat; then a CRM attempt that needs admin approval; then its retry.</summary>
    public static IList<ConnectorSetupSimulationStep> Steps { get; } =
    [
        new("chat", TimeSpan.FromMilliseconds(900)),
        new("crm", TimeSpan.FromMilliseconds(1200), "Needs admin approval"),
        new("crm", TimeSpan.FromMilliseconds(1800)),
    ];

    /// <summary>The eight apps.</summary>
    public static IReadOnlyList<ConnectorApp> Apps { get; } =
    [
        new("chat", "Chat", "Channels, threads and direct messages", ConnectorMarkKind.Chat)
        {
            Host = "chat.example.com",
            Account = "tailspin.chat",
            SyncCount = 1240,
            SyncNoun = "messages",
            Scopes = ["Read messages in channels you belong to", "Search message history", "Post replies as Atlas"],
            Keywords = ["messages", "team", "channels"],
        },
        new("code", "Code", "Repositories, pull requests and reviews", ConnectorMarkKind.Code)
        {
            Host = "code.example.com",
            Account = "tailspin-toys",
            SyncCount = 86,
            SyncNoun = "repositories",
            Scopes = ["Read code and pull requests", "Comment on pull requests", "Open draft pull requests"],
            Keywords = ["git", "repos", "pull requests"],
        },
        new("docs", "Docs", "Pages, wikis and meeting notes", ConnectorMarkKind.Docs)
        {
            Host = "docs.example.com",
            Account = "Tailspin wiki",
            SyncCount = 412,
            SyncNoun = "pages",
            Scopes = ["Read pages shared with you", "Create pages in your private space"],
            Keywords = ["wiki", "notes", "pages"],
        },
        new("drive", "Drive", "Files and folders shared with you", ConnectorMarkKind.Drive)
        {
            Host = "drive.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 2480,
            SyncNoun = "files",
            Scopes = ["Read files you can open", "Search file contents"],
            Keywords = ["files", "storage", "folders"],
        },
        new("issues", "Issues", "Issues, projects and cycles", ConnectorMarkKind.Issues)
        {
            Host = "issues.example.com",
            Account = "Tailspin engineering",
            SyncCount = 930,
            SyncNoun = "issues",
            Scopes = ["Read issues and projects", "Create and update issues", "Comment as Atlas"],
            Keywords = ["tickets", "bugs", "tracker"],
        },
        new("calendar", "Calendar", "Events, invites and availability", ConnectorMarkKind.Calendar)
        {
            Host = "calendar.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 318,
            SyncNoun = "events",
            Scopes = ["See your events and free time", "Draft invites for you to send"],
            Keywords = ["meetings", "schedule", "events"],
        },
        new("mail", "Mail", "Inbox, threads and attachments", ConnectorMarkKind.Mail)
        {
            Host = "mail.example.com",
            Account = "maya@tailspin.example",
            SyncCount = 5600,
            SyncNoun = "emails",
            Scopes = ["Read and search your email", "Draft replies for you to send"],
            Keywords = ["email", "inbox"],
        },
        new("crm", "CRM", "Accounts, contacts and deals", ConnectorMarkKind.Crm)
        {
            Host = "crm.example.com",
            Account = "Tailspin sales",
            SyncCount = 1875,
            SyncNoun = "records",
            Scopes = ["Read accounts, contacts and deals", "Log activity on records"],
            Keywords = ["sales", "customers", "deals"],
        },
    ];

    /// <summary>A new simulation of the apps (tests and code use it; the XAML demos spell it out).</summary>
    public static ConnectorSetupSimulation Simulation() => new()
    {
        AssistantName = AssistantName,
        Apps = Apps,
        Connected = Connected,
        Steps = Steps,
    };
}
