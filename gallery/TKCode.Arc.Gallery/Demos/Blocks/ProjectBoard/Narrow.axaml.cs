using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProjectBoardNarrowDemo : UserControl
{
    public ProjectBoardNarrowDemo()
    {
        InitializeComponent();
        ProjectBoardDemoData.Fill(Board);
    }
}
