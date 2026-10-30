using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class StickerPeelSavedDemo : UserControl
{
    private IReadOnlyDictionary<string, StickerPlacement>? _saved;

    public StickerPeelSavedDemo()
    {
        InitializeComponent();
    }

    private void OnPositionsChanged(object? sender, StickerPeelPositionsChangedEventArgs e)
    {
        _saved = e.Positions;
        var placed = e.Positions[e.Id];
        Readout.Text = string.Create(CultureInfo.InvariantCulture,
            $"Saved {e.Id}: x {placed.X:0.00}, y {placed.Y:0.00}");
    }

    private void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (_saved is not null)
        {
            Board.Positions = _saved;
        }
    }

    private void OnReset(object? sender, RoutedEventArgs e) => Board.Reset();
}
