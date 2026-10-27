using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The Integrations demos' data (all invented: apps, people, permissions and marks; no real product or logo). Nine apps in
/// three categories, the first two connected. Marks are generated: a letter tile or a simple filled shape, colored by the
/// block from the chart series.
/// </summary>
public static class IntegrationsDemoData
{
    public const string Title = "Connected apps";

    public const string Subtitle = "Link the services your workspace relies on.";

    /// <summary>The three categories.</summary>
    public static IntegrationCategory[] Categories() =>
    [
        new("messaging", "Messaging"),
        new("files", "Files"),
        new("insights", "Insights"),
    ];

    /// <summary>The four sync frequencies of the drawer's menu.</summary>
    public static IntegrationSyncOption[] SyncOptions() =>
    [
        new("realtime", "Real-time", "Updates land within seconds.", "in real time"),
        new("30m", "Every 30 minutes", "Updates arrive in half-hour batches.", "every 30 minutes"),
        new("6h", "Every 6 hours", "Runs four times a day.", "every 6 hours"),
        new("nightly", "Nightly", "Runs every night at 03:00 local time.", "nightly"),
    ];

    // Simple filled marks on a 20-unit grid (drawn by the block in a series color).
    private static Geometry Shape(string data) => Geometry.Parse(data);

    private const string StackedTriangles = "M10 1 L19 9 L1 9 Z M10 8 L19 17 L1 17 Z";

    private const string DotGrid =
        "M2 2h4v4h-4z M8 2h4v4h-4z M14 2h4v4h-4z M2 8h4v4h-4z M8 8h4v4h-4z M14 8h4v4h-4z M2 14h4v4h-4z M8 14h4v4h-4z M14 14h4v4h-4z";

    private const string Hexagon = "M10 1 L18 5.5 L18 14.5 L10 19 L2 14.5 L2 5.5 Z";

    private const string TallyBars = "M3 4h3v13h-3z M8.5 2h3v15h-3z M14 6h3v11h-3z";

    private const string LensAndPlay = "M1 10a6 6 0 1 0 12 0a6 6 0 1 0 -12 0Z M14 7.5 L19 4.5 L19 15.5 L14 12.5 Z";

    /// <summary>The nine apps; Chirp and Vaultline are connected.</summary>
    public static IntegrationItem[] Apps()
    {
        return
        [
            App("chirp", "Chirp", "messaging", "Ping a room whenever a milestone changes owner.", null,
                ("View rooms and their members", IntegrationScopeKind.Read),
                ("Send milestone pings as the workspace bot", IntegrationScopeKind.Write),
                ("Refresh the room list every hour", IntegrationScopeKind.Sync))
                .Connected("Lena Ortiz", new DateTimeOffset(2026, 3, 12, 10, 0, 0, TimeSpan.Zero), "realtime"),
            App("vaultline", "Vaultline", "files", "Mirror design files into the matching workspace board.", Shape(StackedTriangles),
                ("Browse folders you share with the workspace", IntegrationScopeKind.Read),
                ("Add file links to boards and tasks", IntegrationScopeKind.Write),
                ("Pick up renamed files every 30 minutes", IntegrationScopeKind.Sync))
                .Connected("Theo Banerjee", new DateTimeOffset(2026, 2, 27, 9, 30, 0, TimeSpan.Zero), "30m"),
            App("pulsegrid", "Pulsegrid", "insights", "Chart feature adoption from in-app clicks.", Shape(DotGrid),
                ("Look up feature names and owners", IntegrationScopeKind.Read),
                ("Create one dashboard per workspace", IntegrationScopeKind.Write),
                ("Re-import a fortnight of clicks after relinking", IntegrationScopeKind.Sync)),
            App("inkwell", "Inkwell Mail", "messaging", "File support emails as tickets with the sender attached.", null,
                ("Read the support inbox you choose", IntegrationScopeKind.Read),
                ("Reply from tickets as the support address", IntegrationScopeKind.Write),
                ("Check for new mail every few minutes", IntegrationScopeKind.Sync)),
            App("boxyard", "Boxyard", "files", "Pull contract PDFs into the client record automatically.", Shape(Hexagon),
                ("Open contracts in the folders you allow", IntegrationScopeKind.Read),
                ("Attach signed copies to client records", IntegrationScopeKind.Write),
                ("Watch the contract folder for new files", IntegrationScopeKind.Sync)),
            App("tallymark", "Tallymark", "insights", "Measure how many visitors finish the onboarding tour.", Shape(TallyBars),
                ("Count page views on the tour pages", IntegrationScopeKind.Read),
                ("Tag visitors who finish the tour", IntegrationScopeKind.Write),
                ("Send totals back once an hour", IntegrationScopeKind.Sync)),
            App("huddle", "Huddle Video", "messaging", "Drop meeting summaries into the related task thread.", Shape(LensAndPlay),
                ("See meetings that mention a task", IntegrationScopeKind.Read),
                ("Post summaries in task threads", IntegrationScopeKind.Write),
                ("Fetch new summaries after each call", IntegrationScopeKind.Sync)),
            App("coldstore", "Coldstore", "files", "Keep a cold copy of every invoice for seven years.", null,
                ("List invoices and their totals", IntegrationScopeKind.Read),
                ("Write archive copies to your bucket", IntegrationScopeKind.Write),
                ("Archive new invoices every night", IntegrationScopeKind.Sync)),
            App("ledgerline", "Ledgerline", "insights", "Export billable hours to finance at the end of each week.", null,
                ("Read logged hours and rates", IntegrationScopeKind.Read),
                ("Create draft invoices for finance", IntegrationScopeKind.Write),
                ("Send the week's hours every Friday", IntegrationScopeKind.Sync),
                ("Manage which projects are billable", IntegrationScopeKind.Admin)),
        ];
    }

    private static IntegrationItem App(string key, string name, string category, string description, Geometry? logo,
        params (string Text, IntegrationScopeKind Kind)[] scopes)
    {
        var item = new IntegrationItem { Key = key, Name = name, Category = category, Description = description, Logo = logo };
        foreach (var (text, kind) in scopes)
        {
            item.Scopes.Add(new IntegrationScope(kind, text));
        }
        return item;
    }

    private static IntegrationItem Connected(this IntegrationItem item, string by, DateTimeOffset at, string frequency)
    {
        item.Status = IntegrationStatus.Connected;
        item.ConnectedBy = by;
        item.ConnectedAt = at;
        item.SyncFrequency = frequency;
        return item;
    }

    /// <summary>Fills <paramref name="block"/> with the categories, the sync options and the apps.</summary>
    public static void Fill(Integrations block, bool withSyncOptions = true)
    {
        block.Categories.AddRange(Categories());
        if (withSyncOptions)
        {
            block.SyncOptions.AddRange(SyncOptions());
        }
        block.Items.AddRange(Apps());
    }
}
