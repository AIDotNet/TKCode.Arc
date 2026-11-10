using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class MegaMenuMorphPreviewDemo : UserControl
{
    public MegaMenuMorphPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnLinkInvoked(object? sender, MegaMenuMorphLinkInvokedEventArgs e) =>
        Status.Text = e.Link is null ? $"Opened {e.Item.Header}" : $"Opened {e.Item.Header} › {e.Title}";
}
