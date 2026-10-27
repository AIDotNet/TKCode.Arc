using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhooksCompactDemo : UserControl
{
    public WebhooksCompactDemo()
    {
        InitializeComponent();
        Hooks.Description = WebhooksDemoData.Description;
        Hooks.CurrentUser = WebhooksDemoData.CurrentUser;
        Hooks.EventTypes.AddRange(WebhooksDemoData.EventTypes());
        Hooks.Endpoints.AddRange(WebhooksDemoData.Endpoints(DateTimeOffset.Now));
    }
}
