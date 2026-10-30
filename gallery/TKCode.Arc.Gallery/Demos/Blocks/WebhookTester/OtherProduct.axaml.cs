using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhookTesterOtherProductDemo : UserControl
{
    public WebhookTesterOtherProductDemo()
    {
        InitializeComponent();
        Block.Source = new WebhookTesterSource { Name = "Relay Mail" };
        Block.Events.AddRange(WebhookTesterDemoData.RelayEvents());
    }
}
