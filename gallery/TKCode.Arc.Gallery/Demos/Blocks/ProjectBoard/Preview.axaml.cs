using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProjectBoardPreviewDemo : UserControl
{
    public ProjectBoardPreviewDemo()
    {
        InitializeComponent();
        ProjectBoardDemoData.Fill(Board);
    }
}
