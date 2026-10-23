using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardReadOnlyDemo : UserControl
{
    public KanbanBoardReadOnlyDemo()
    {
        InitializeComponent();
        KanbanBoardDemoData.Fill(Board);
    }
}
