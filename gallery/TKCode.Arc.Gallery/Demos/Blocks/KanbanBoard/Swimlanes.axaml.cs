using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardSwimlanesDemo : UserControl
{
    public KanbanBoardSwimlanesDemo()
    {
        InitializeComponent();
        KanbanBoardDemoData.Fill(Board);
        Board.CurrentUser = Board.People[2];
    }
}
