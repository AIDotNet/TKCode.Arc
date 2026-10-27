using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhooksPreviewDemo : UserControl
{
    public WebhooksPreviewDemo()
    {
        InitializeComponent();
        Hooks.Description = WebhooksDemoData.Description;
        Hooks.CurrentUser = WebhooksDemoData.CurrentUser;
        Hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        Hooks.Endpoints.AddRange(WebhooksDemoData.Endpoints(DateTimeOffset.Now));
        // No RetryAction, CreateEndpointAction or SendTestAction: the block simulates a server that always answers 200.
    }
}
