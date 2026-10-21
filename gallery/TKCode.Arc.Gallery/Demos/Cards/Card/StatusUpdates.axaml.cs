using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class CardStatusUpdatesDemo : UserControl
{
    private static readonly string[] Steps =
    [
        "Updated just now",
        "Edited 1 minute ago",
        "Published to 4 teams",
        "Updated 2 hours ago"
    ];

    private int _step;

    public CardStatusUpdatesDemo()
    {
        InitializeComponent();
    }

    private void OnChangeStatusClick(object? sender, RoutedEventArgs e)
        => Digest.Status = Steps[_step++ % Steps.Length];
}
