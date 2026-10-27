using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TeamDirectoryNarrowDemo : UserControl
{
    public TeamDirectoryNarrowDemo()
    {
        InitializeComponent();
        Directory.People.AddRange(TeamDirectoryDemoData.People());
    }
}
