using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProviderStatusOwnDataDemo : UserControl
{
    private int _checks;

    public ProviderStatusOwnDataDemo()
    {
        InitializeComponent();
        Card.Providers = Providers(0);
        Card.RefreshHandler = RefreshAsync;
        Card.IncidentOpened += (_, e) =>
        {
            // Open your status page here; the demo only logs it.
            e.Handled = true;
            Log.Text = $"Opened the status page of {e.Provider.Name}: {e.Incident.Title}";
        };
        Card.ExpandedChanged += (_, e) => Log.Text = e.ExpandedId is null ? "Closed the incident" : $"Opened {e.ExpandedId}";
    }

    private async Task<IReadOnlyList<ProviderStatusItem>?> RefreshAsync(CancellationToken token)
    {
        await Task.Delay(700, token);
        _checks++;
        if (_checks % 3 == 0)
        {
            throw new InvalidOperationException("Couldn’t reach the status feeds. Showing the last results.");
        }
        return Providers(_checks);
    }

    private static IReadOnlyList<ProviderStatusItem> Providers(int check)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        return
        [
            new ProviderStatusItem
            {
                Id = "primary",
                Name = AiModelCatalog.Anthropic.Name,
                Detail = ProviderStatusDemoData.Detail(AiModelCatalog.Anthropic, null, AiModelCatalog.ClaudeOpus55) + " · primary route",
                Health = ProviderStatusHealth.Degraded,
                LatencyMs = 840 + check % 4 * 35,
                Days = ProviderStatusDemoData.History(today, 29, 99.62, "Sample: slower responses since this morning."),
                Incident = new ProviderStatusIncident
                {
                    Id = "slow-region",
                    Title = "Sample incident: slower responses in one region",
                    Updates =
                    [
                        new ProviderStatusUpdate
                        {
                            Stage = ProviderStatusStage.Investigating,
                            At = now.AddMinutes(-26),
                            Message = "Responses from one region are slower than usual. Investigating."
                        },
                        new ProviderStatusUpdate
                        {
                            Stage = ProviderStatusStage.Identified,
                            At = now.AddMinutes(-11),
                            Message = "The cause is identified; traffic is moving to other regions."
                        }
                    ]
                }
            },
            new ProviderStatusItem
            {
                Id = "fallback",
                Name = AiModelCatalog.OpenAI.Name,
                Detail = ProviderStatusDemoData.Detail(AiModelCatalog.OpenAI, null, AiModelCatalog.Gpt61Sol) + " · fallback",
                Health = ProviderStatusHealth.Operational,
                LatencyMs = 390 + check % 3 * 20,
                Days = ProviderStatusDemoData.History(today, 12, 99.7, "Sample: rate limits were lower than usual.")
            },
            new ProviderStatusItem
            {
                Id = "vision",
                Name = AiModelCatalog.Google.Name,
                Detail = ProviderStatusDemoData.Detail(AiModelCatalog.Google, null, AiModelCatalog.Gemini4Argon) + " · image input",
                Health = ProviderStatusHealth.Operational,
                LatencyMs = 1240 + check % 5 * 60,
                Days = ProviderStatusDemoData.History(today, 20, 98.4, "Sample: image requests failed for 50 minutes.")
            }
        ];
    }
}
