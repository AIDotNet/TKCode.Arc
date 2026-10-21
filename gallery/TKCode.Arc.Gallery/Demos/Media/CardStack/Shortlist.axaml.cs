using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardStackShortlistDemo : UserControl
{
    public CardStackShortlistDemo()
    {
        InitializeComponent();
        Shortlist.LabelSelector = item => ((CardStackDestination)item!).Name;
    }

    private void OnDecided(object? sender, CardStackDecisionEventArgs e)
    {
        var name = ((CardStackDestination)e.Item!).Name;
        Log.Text = $"{name}: {(e.Decision == CardDecision.Right ? "shortlisted" : "skipped")}";
    }

    private void OnDecisionUndone(object? sender, CardStackDecisionEventArgs e)
    {
        Log.Text = $"{((CardStackDestination)e.Item!).Name}: restored";
    }

    private void OnStackReset(object? sender, RoutedEventArgs e)
    {
        Log.Text = "All cards are back in the stack.";
    }
}
