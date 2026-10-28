namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev/demo/integrations: one row, the Integrations block with uiarc.dev's nine tools (Slack and Google Drive
/// connected), its three categories and four sync frequencies. Connecting takes 1.4 s and always succeeds; disconnecting
/// settles after 0.65 s. The block's labels are uiarc.dev's wording ("Configure", "Connected by … on …").
/// </summary>
public sealed class DemoIntegrationsPage : DemoPageView
{
    public DemoIntegrationsPage(IDemoHost host, DemoPage page)
        : base(host, page)
    {
        var directory = new Integrations
        {
            Title = "Integrations",
            Subtitle = "Connect the tools your team already works in.",
            Labels = DemoIntegrationsData.Labels,
            SimulatedConnectDuration = TimeSpan.FromMilliseconds(1400),
            DisconnectSettleDelay = TimeSpan.FromMilliseconds(650)
        };
        directory.Categories.AddRange(DemoIntegrationsData.Categories());
        directory.SyncOptions.AddRange(DemoIntegrationsData.SyncOptions());
        directory.Items.AddRange(DemoIntegrationsData.Items());
        AddRow(DemoRowLayout.Stack, "directory", directory);
    }
}

/// <summary>The Integrations page's data: uiarc.dev's tools, their access and the sync frequencies.</summary>
internal static class DemoIntegrationsData
{
    public static IntegrationLabels Labels { get; } = new()
    {
        Title = "Integrations",
        Subtitle = "Connect the tools your team already works in.",
        SearchPlaceholder = "Search by name or category",
        Configure = "Configure",
        ConfigureName = app => $"Configure {app}",
        NotConnected = "Not connected yet",
        Waiting = app => $"Waiting for {app} to approve access",
        ConnectedBy = (person, date) => string.IsNullOrEmpty(date) ? $"Connected by {person}" : $"Connected by {person} on {date}",
        ConnectedByYou = "Connected by you just now",
        ConnectedUnknown = "Connected",
        ConnectFailed = app => $"{app} did not finish connecting. Try again.",
        AccessTitle = "Access",
        HoldToDisconnect = "Hold to disconnect",
        Disconnected = "Disconnected",
        DisconnectNote = app => $"Stops syncing and revokes {app}’s access.",
        EmptySearchTitle = query => $"No integrations match “{query}”",
        EmptySearchHint = "Try a product name or a category such as storage.",
        EmptyConnectedTitle = "Nothing connected yet",
        EmptyConnectedHint = "Connect a tool and it will show up here.",
        BrowseAll = "Browse all",
        EmptyCategoryTitle = "No integrations here yet",
        AnnounceConnecting = app => $"Connecting {app}",
        AnnounceConnected = app => $"{app} connected",
        AnnounceFailed = app => $"{app} could not be connected",
        AnnounceDisconnected = app => $"{app} disconnected",
        AnnounceDisconnectFailed = app => $"{app} is still connected",
        AnnounceShown = count => count == 1 ? "Showing 1 integration" : $"Showing {count} integrations",
        AnnounceShownIn = (count, category) => count == 1
            ? $"Showing 1 {category.ToLowerInvariant()} integration"
            : $"Showing {count} {category.ToLowerInvariant()} integrations",
        AnnounceMatches = (count, query) => count == 1 ? $"1 result for “{query}”" : $"{count} results for “{query}”"
    };

    public static IntegrationCategory[] Categories() =>
    [
        new("communication", "Communication"),
        new("storage", "Storage"),
        new("analytics", "Analytics")
    ];

    public static IntegrationSyncOption[] SyncOptions() =>
    [
        new("realtime", "Real time", "Changes arrive within a few seconds.", "in real time"),
        new("15m", "Every 15 minutes", "Changes arrive in batches every 15 minutes.", "every 15 minutes"),
        new("hourly", "Every hour", "Runs at the start of every hour.", "every hour"),
        new("daily", "Once a day", "Runs once a day at 02:00 UTC.", "once a day")
    ];

    public static IntegrationItem[] Items() =>
    [
        Item("slack", "Slack", "communication", "realtime", DemoIntegrationLogos.Slack,
                "Post release notes and approval requests to the channels you pick.",
                "Read channel names and member lists", "Post and update messages in channels you choose",
                "Unfurl project links shared in conversations")
            .ConnectedBy("Emma Collins", new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero)),
        Item("google-drive", "Google Drive", "storage", "15m", DemoIntegrationLogos.GoogleDrive,
                "Attach docs and keep shared folders in step with their projects.",
                "View files in folders shared with the workspace", "Create a folder for each new project",
                "Mirror folder changes into project files")
            .ConnectedBy("Marcus Johnson", new DateTimeOffset(2026, 8, 28, 10, 0, 0, TimeSpan.Zero)),
        Item("mixpanel", "Mixpanel", "analytics", "realtime", DemoIntegrationLogos.Mixpanel,
            "Send product events and funnels to Mixpanel as they happen.",
            "Read the tracking plan and event schema", "Send workspace events to one Segment source",
            "Replay the last 7 days of events after reconnecting"),
        Item("gmail", "Gmail", "communication", "15m", DemoIntegrationLogos.Gmail,
            "Turn customer emails into tracked requests with the thread attached.",
            "Read messages sent to shared inboxes you pick", "Apply a label when a request is resolved",
            "Import attachments up to 25 MB"),
        Item("dropbox", "Dropbox", "storage", "hourly", DemoIntegrationLogos.Dropbox,
            "Link team folders to projects without uploading files twice.",
            "View the files and folders you select", "Add a comment when a file is approved",
            "Keep file versions and previews up to date"),
        Item("google-analytics", "Google Analytics", "analytics", "hourly", DemoIntegrationLogos.GoogleAnalytics,
            "Track sign-ups and traffic on your public project pages.",
            "Read saved cohorts to target announcements", "Create an annotation when a release ships",
            "Send account and user properties"),
        Item("zoom", "Zoom", "communication", "hourly", DemoIntegrationLogos.Zoom,
            "Attach recordings and transcripts to the meetings on each project.",
            "Read scheduled meetings and participants", "Create meeting links from project events",
            "Import cloud recordings and transcripts"),
        Item("amazon-s3", "Amazon S3", "storage", "daily", DemoIntegrationLogos.AmazonS3,
            "Archive nightly exports and audit logs to a bucket you own.",
            "List objects under the northwind/ prefix", "Write nightly exports to the bucket you choose",
            "Keep 13 months of daily exports"),
        Item("snowflake", "Snowflake", "analytics", "daily", DemoIntegrationLogos.Snowflake,
            "Sync projects, tasks, and time entries to your warehouse every night.",
            "Read table sizes to estimate sync time", "Create tables in the schema you choose",
            "Write incremental updates to those tables")
    ];

    private static IntegrationItem Item(string key, string name, string category, string frequency, object logo, string description,
                                        string read, string write, string sync)
    {
        var item = new IntegrationItem
        {
            Key = key,
            Name = name,
            Category = category,
            Description = description,
            Logo = logo,
            SyncFrequency = frequency
        };
        item.Scopes.Add(new IntegrationScope(IntegrationScopeKind.Read, read));
        item.Scopes.Add(new IntegrationScope(IntegrationScopeKind.Write, write));
        item.Scopes.Add(new IntegrationScope(IntegrationScopeKind.Sync, sync));
        return item;
    }

    private static IntegrationItem ConnectedBy(this IntegrationItem item, string person, DateTimeOffset at)
    {
        item.Status = IntegrationStatus.Connected;
        item.ConnectedBy = person;
        item.ConnectedAt = at;
        return item;
    }
}
