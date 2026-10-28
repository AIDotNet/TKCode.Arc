using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderBasicDemo : UserControl
{
    public CascaderBasicDemo()
    {
        InitializeComponent();
        CascaderDemoData.Configure(Region);
        Region.ItemsSource = CascaderDemoData.Regions();
        Region.SelectionChanged += (_, _) => Result.Text = $"SelectedValue: {Region.SelectedValue ?? "none"}";
        Result.Text = "SelectedValue: none";
    }
}
