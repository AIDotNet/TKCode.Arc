using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderLeafNameDemo : UserControl
{
    public CascaderLeafNameDemo()
    {
        InitializeComponent();
        CascaderDemoData.Configure(Region);
        Region.ItemsSource = CascaderDemoData.Regions();
        Region.SelectedValue = "440305";
    }
}
