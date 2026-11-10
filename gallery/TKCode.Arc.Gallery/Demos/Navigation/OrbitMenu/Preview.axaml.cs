using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class OrbitMenuPreviewDemo : UserControl
{
    public OrbitMenuPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnActionInvoked(object? sender, OrbitMenuActionEventArgs e) => Status.Text = $"Started: {e.Action.Label}";
}
