using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardPreviewDemo : UserControl
{
    public KanbanBoardPreviewDemo()
    {
        InitializeComponent();
        KanbanBoardDemoData.Fill(Board);
        Board.Cards.Insert(9, new KanbanCard("FLD-215", "Compress photos before they leave the device", "testing")
        {
            AssigneeKey = "jonas",
            LabelKey = "sync",
            Points = 2
        });
    }
}
