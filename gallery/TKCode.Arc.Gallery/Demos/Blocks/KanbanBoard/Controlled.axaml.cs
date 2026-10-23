using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class KanbanBoardControlledDemo : UserControl
{
    private readonly ObservableCollection<KanbanCard> _cards = new(KanbanBoardDemoData.CreateCards());
    private readonly List<string> _log = new();

    public KanbanBoardControlledDemo()
    {
        InitializeComponent();
        Board.Columns = KanbanBoardDemoData.CreateColumns();
        Board.People = KanbanBoardDemoData.CreatePeople();
        Board.Labels = KanbanBoardDemoData.CreateLabels();
        // The page owns the list: the board reorders it in place, and CardsChanged hands over the order to persist.
        Board.CardsSource = _cards;
        Board.CardsChanged += (_, e) =>
        {
            _log.Insert(0, $"{e.Reason}: {e.Card?.Key} {e.Card?.Title}");
            if (_log.Count > 6)
            {
                _log.RemoveAt(_log.Count - 1);
            }
            Log.Text = string.Join(Environment.NewLine, _log);
            ShowOrder(e.Cards);
        };
        Board.CardMoved += (_, e) =>
        {
            _log.Insert(0, $"Moved by {e.ChangeSource}: {e.FromColumn?.Name} → {e.ToColumn?.Name}, list position {e.FromIndex} → {e.ToIndex}");
        };
        ShowOrder(_cards);
    }

    private void ShowOrder(IReadOnlyList<KanbanCard> cards)
    {
        var lines = Board.Columns.Select(column =>
            $"{column.Name,-9} {string.Join(" ", cards.Where(c => c.ColumnKey == column.Identity).Select(c => c.Key))}");
        Order.Text = string.Join(Environment.NewLine, lines);
    }
}
