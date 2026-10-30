using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhookTesterNarrowDemo : UserControl
{
    public WebhookTesterNarrowDemo()
    {
        InitializeComponent();
        Block.Deliveries.AddRange(WebhookTesterDemoData.Deliveries());
    }
}
