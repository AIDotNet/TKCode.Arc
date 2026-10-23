using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class SiteHeaderSimpleDemo : UserControl
{
    public SiteHeaderSimpleDemo()
    {
        InitializeComponent();
        Header.ItemsSource = SiteHeaderDemoData.DocsItems();
        // The header goes into the same small scrolling page as the preview.
        FrameHost.Child = null;
        FrameHost.Child = SiteHeaderDemoData.CreateFrame(Header, 360, out _);
    }
}
