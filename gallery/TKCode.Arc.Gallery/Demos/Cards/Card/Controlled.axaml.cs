using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardControlledDemo : UserControl
{
    public CardControlledDemo()
    {
        InitializeComponent();
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Plan.IsDetailsOpen = true;

    private void OnDetailsOpenChanged(object? sender, CardDetailsOpenChangedEventArgs e)
        => Log.Text = e.IsOpen ? "Open" : $"Closed ({e.Reason})";
}
