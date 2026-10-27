using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProfilePageNarrowDemo : UserControl
{
    public ProfilePageNarrowDemo()
    {
        InitializeComponent();
        ProfilePageDemoData.Fill(Page);
    }
}
