using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Three apps for a first-run checklist; with no handlers the card simulates the flow.</summary>
public partial class ConnectorSetupOnboardingDemo : UserControl
{
    public ConnectorSetupOnboardingDemo()
    {
        InitializeComponent();
        Setup.Connectors =
        [
            new ConnectorApp("chat", "Chat", "Channels and direct messages", ConnectorMarkKind.Chat)
            {
                SyncCount = 860,
                SyncNoun = "messages",
                Scopes = ["Read messages in channels you belong to", "Post replies as Atlas"],
            },
            new ConnectorApp("docs", "Docs", "Pages, wikis and meeting notes", ConnectorMarkKind.Docs)
            {
                SyncCount = 145,
                SyncNoun = "pages",
                Scopes = ["Read pages shared with you"],
            },
            new ConnectorApp("drive", "Drive", "Files and folders shared with you", ConnectorMarkKind.Drive)
            {
                SyncCount = 1320,
                SyncNoun = "files",
                Scopes = ["Read files you can open", "Search file contents"],
            },
        ];
    }
}
