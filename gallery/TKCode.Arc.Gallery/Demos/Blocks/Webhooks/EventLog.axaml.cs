using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhooksEventLogDemo : UserControl
{
    private readonly List<string> _lines = new();
    private int _pushed;

    public WebhooksEventLogDemo()
    {
        InitializeComponent();
        Hooks.Description = WebhooksDemoData.Description;
        Hooks.CurrentUser = WebhooksDemoData.CurrentUser;
        Hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        Hooks.Endpoints.AddRange(WebhooksDemoData.Endpoints(DateTimeOffset.Now));

        // The inspector's tab is a two-way property: this switch and the block's own tabs follow each other.
        Tab.ItemsSource = new[]
        {
            new SegmentedOption(WebhookInspectorTab.Payload, "Payload"),
            new SegmentedOption(WebhookInspectorTab.Headers, "Headers"),
        };
        Tab.SelectedIndex = 0;
        Tab.SelectionChanged += (_, _) => Hooks.InspectorTab = Tab.SelectedIndex == 1 ? WebhookInspectorTab.Headers : WebhookInspectorTab.Payload;
        Hooks.PropertyChanged += (_, e) =>
        {
            if (e.Property == Webhooks.InspectorTabProperty)
            {
                Tab.SelectedIndex = Hooks.InspectorTab == WebhookInspectorTab.Headers ? 1 : 0;
            }
            else if (e.Property == Webhooks.SelectedEndpointProperty || e.Property == Webhooks.SelectedDeliveryProperty)
            {
                ShowSelection();
            }
        };

        // A live feed: the host pushes what its server reports; every fourth delivery fails.
        Push.Click += (_, _) =>
        {
            if (Hooks.SelectedEndpoint is { } endpoint)
            {
                var type = endpoint.Events.Count > 0 ? endpoint.Events[_pushed % endpoint.Events.Count] : "test.ping";
                Hooks.AppendDelivery(endpoint, WebhooksDemoData.LiveDelivery(DateTimeOffset.Now, _pushed++, type));
            }
        };

        Hooks.EndpointSelectionChanged += (_, e) => Add($"Endpoint: {e.NewEndpoint?.DisplayName} ({e.ChangeSource})");
        Hooks.DeliverySelectionChanged += (_, e) => Add($"Delivery: {e.NewDelivery?.EventType} ({e.ChangeSource})");
        Hooks.RetryStarted += (_, e) => Add($"Retrying {e.Delivery.EventType}");
        Hooks.RetryCompleted += (_, e) => Add($"Retried: {Rate(e.OldRate)} → {Rate(e.NewRate)}");
        Hooks.ActionFailed += (_, e) => Add($"{e.Action} failed");
        Hooks.EndpointCreated += (_, e) => Add($"Added {e.Endpoint.DisplayName}");
        Hooks.TestEventSent += (_, e) => Add($"Test sent to {e.Endpoint.Host}");
        Hooks.ContentCopied += (_, e) => Add($"Copied the {e.Content.ToString().ToLowerInvariant()} ({e.Text.Length} characters)");
        Hooks.AddPanelOpened += (_, _) => Add("Add panel opened");
        Hooks.AddPanelClosed += (_, e) => Add($"Add panel closed ({e.Reason})");
        ShowSelection();
    }

    private static string Rate(double? rate) => rate is { } value ? $"{value * 100:0.0}%" : "–";

    private void ShowSelection() =>
        Selection.Text = $"{Hooks.SelectedEndpoint?.DisplayName ?? "No endpoint"} · {Hooks.SelectedDelivery?.EventType ?? "no delivery"}";

    private void Add(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join("\n", _lines);
    }
}
