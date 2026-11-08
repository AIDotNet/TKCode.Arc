using Avalonia.Controls;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Pages;

public partial class BoardPage : UserControl
{
    public BoardPage()
    {
        InitializeComponent();
    }

    // A move, a quick add or an edit: keep the cards.
    private void OnCardsChanged(object? sender, KanbanCardsChangedEventArgs e) => (DataContext as BoardPageViewModel)?.Save();
}
