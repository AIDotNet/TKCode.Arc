using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhooksNewEndpointDemo : UserControl
{
    public WebhooksNewEndpointDemo()
    {
        InitializeComponent();
        Hooks.CurrentUser = WebhooksDemoData.CurrentUser;
        Hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        Hooks.Endpoints.Add(WebhooksDemoData.NewEndpoint(DateTimeOffset.Now));
    }
}
