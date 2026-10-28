using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CascaderDisabledDemo : UserControl
{
    public CascaderDisabledDemo()
    {
        InitializeComponent();
        foreach (var cascader in new[] { Partial, Off, ReadOnly })
        {
            CascaderDemoData.Configure(cascader);
            cascader.ItemsSource = CascaderDemoData.Regions();
        }
        Off.SelectedValue = "3302";
        ReadOnly.SelectedValue = "3101";
    }
}
