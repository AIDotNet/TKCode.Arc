using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// A card with its own apps and a connect handler that stands in for an OAuth popup and a first sync (the delays are
/// invented; a real handler would await your popup and sync calls with the same token).
/// </summary>
public partial class ConnectorSetupYourFlowDemo : UserControl
{
    private bool _issuesFailed;

    public ConnectorSetupYourFlowDemo()
    {
        InitializeComponent();
        Setup.Connectors =
        [
            new ConnectorApp("mail", "Mail", "Inbox, threads and attachments", ConnectorMarkKind.Mail)
            {
                Host = "mail.example.com",
                SyncCount = 3200,
                SyncNoun = "emails",
                Scopes = ["Read and search your email", "Draft replies for you to send"],
            },
            new ConnectorApp("calendar", "Calendar", "Events, invites and availability", ConnectorMarkKind.Calendar)
            {
                SyncCount = 240,
                SyncNoun = "events",
                Scopes = ["See your events and free time"],
            },
            new ConnectorApp("issues", "Issues", "Issues, projects and cycles", ConnectorMarkKind.Issues)
            {
                SyncCount = 610,
                SyncNoun = "issues",
                Scopes = ["Read issues and projects", "Comment as Atlas"],
            },
        ];
        Setup.ConnectedIds = ["calendar"];
        Setup.ConnectHandler = ConnectAsync;
        Setup.DisconnectHandler = (_, token) => Task.Delay(500, token);
        Setup.ConnectedIdsChanged += (_, e) => Show(e.Ids);
        Show(Setup.ConnectedIds);
    }

    private async Task<ConnectorConnectResult?> ConnectAsync(ConnectorApp app, ConnectorConnectContext context)
    {
        var token = context.CancellationToken;
        await Task.Delay(800, token);                       // the OAuth popup opens
        context.ReportAuthorizing();
        await Task.Delay(1000, token);                      // the person approves
        if (app.Id == "issues" && !_issuesFailed)
        {
            _issuesFailed = true;
            throw new InvalidOperationException("Needs admin approval");
        }
        var total = app.SyncCount ?? 100;
        for (var synced = 0L; synced < total; synced += Math.Max(1, total / 30))
        {
            context.ReportSyncing(synced, total);
            await Task.Delay(50, token);
        }
        return new ConnectorConnectResult { Account = "maya@tailspin.example", Synced = total };
    }

    private void Show(IReadOnlyList<string>? ids) =>
        Log.Text = "Connected: " + (ids is { Count: > 0 } ? string.Join(", ", ids) : "none");
}
