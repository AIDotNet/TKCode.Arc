using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WebhookTesterPreviewDemo : UserControl
{
    public WebhookTesterPreviewDemo()
    {
        InitializeComponent();
        Block.Deliveries.AddRange(WebhookTesterDemoData.Deliveries());
    }
}
