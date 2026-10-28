using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderHoverExpandDemo : UserControl
{
    public CascaderHoverExpandDemo()
    {
        InitializeComponent();
        CascaderDemoData.Configure(Region);
        Region.ItemsSource = CascaderDemoData.Regions();
    }
}
