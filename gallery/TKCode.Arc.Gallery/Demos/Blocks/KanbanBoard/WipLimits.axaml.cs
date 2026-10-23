using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardWipLimitsDemo : UserControl
{
    public KanbanBoardWipLimitsDemo()
    {
        InitializeComponent();
        KanbanBoardDemoData.Fill(Board);
        Board.CardMoving += (_, e) =>
        {
            // The board only warns; a handler makes the limits strict by sending the card back.
            if (Strict.IsChecked == true && e.IsTargetOverLimit && e.From.ColumnKey != e.To.ColumnKey)
            {
                e.Cancel = true;
                Status.Text = $"{e.Card.Key} went back: {Board.Columns.First(c => c.Identity == e.To.ColumnKey).Name} is full.";
            }
        };
        Board.ColumnLimitStateChanged += (_, e) =>
        {
            Status.Text = e.IsOverLimit
                ? $"{e.Column.Name} is over its limit: {e.Count} of {e.Limit}."
                : $"{e.Column.Name} is back within its limit: {e.Count} of {e.Limit}.";
        };
    }
}
