using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeSelectLeafOnlyDemo : UserControl
{
    public TreeSelectLeafOnlyDemo()
    {
        InitializeComponent();
        TreeSelectDemoData.Configure(Entry);
        Entry.ItemsSource = TreeSelectDemoData.Project();
    }
}
