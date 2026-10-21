using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class BreadcrumbDefaultDemo : UserControl
{
    public BreadcrumbDefaultDemo()
    {
        InitializeComponent();
    }

    private void OnItemClick(object? sender, BreadcrumbItemClickEventArgs e)
        => Status.Text = $"Navigate to {((BreadcrumbItem)e.Item!).Content} (index {e.Index})";
}
