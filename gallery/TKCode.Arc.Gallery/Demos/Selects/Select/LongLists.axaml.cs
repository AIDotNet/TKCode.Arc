using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SelectLongListsDemo : UserControl
{
    public SelectLongListsDemo()
    {
        InitializeComponent();
        Builds.ItemsSource = Enumerable.Range(1, 5000).Select(i => $"Build {i:0000}").ToArray();
        Builds.SelectedIndex = 2499;
    }
}
