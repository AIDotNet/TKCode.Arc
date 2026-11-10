using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CoverFlowInvokedDemo : UserControl
{
    public CoverFlowInvokedDemo()
    {
        InitializeComponent();
    }

    private void OnInvoked(object? sender, CoverFlowItemInvokedEventArgs e) =>
        Readout.Text = $"Playing {e.Item.Title} by {e.Item.Subtitle}.";
}
