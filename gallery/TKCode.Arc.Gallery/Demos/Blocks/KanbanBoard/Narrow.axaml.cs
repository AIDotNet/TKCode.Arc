using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardNarrowDemo : UserControl
{
    public KanbanBoardNarrowDemo()
    {
        InitializeComponent();
        KanbanBoardDemoData.Fill(Board);
    }
}
