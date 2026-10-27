using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProfilePagePreviewDemo : UserControl
{
    public ProfilePagePreviewDemo()
    {
        InitializeComponent();
        ProfilePageDemoData.Fill(Page);
    }
}
