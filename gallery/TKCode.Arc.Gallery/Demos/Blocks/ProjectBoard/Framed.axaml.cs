using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProjectBoardFramedDemo : UserControl
{
    public ProjectBoardFramedDemo()
    {
        InitializeComponent();
        ProjectBoardDemoData.Fill(Board);
    }
}
