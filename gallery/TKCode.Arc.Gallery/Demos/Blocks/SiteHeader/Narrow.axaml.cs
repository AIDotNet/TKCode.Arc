using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteHeaderNarrowDemo : UserControl
{
    public SiteHeaderNarrowDemo()
    {
        InitializeComponent();
        Header.ItemsSource = SiteHeaderDemoData.ExampleItems();
        Header.Current = "resources";
        FrameHost.Child = null;
        FrameHost.Child = SiteHeaderDemoData.CreateFrame(Header, 520, out _);
    }
}
