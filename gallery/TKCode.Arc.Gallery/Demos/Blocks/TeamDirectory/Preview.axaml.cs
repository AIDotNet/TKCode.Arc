using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamDirectoryPreviewDemo : UserControl
{
    public TeamDirectoryPreviewDemo()
    {
        InitializeComponent();
        Directory.People.AddRange(TeamDirectoryDemoData.People());
    }
}
