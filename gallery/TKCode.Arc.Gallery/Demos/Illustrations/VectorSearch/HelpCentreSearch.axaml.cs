using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class VectorSearchHelpCentreSearchDemo : UserControl
{
    public VectorSearchHelpCentreSearchDemo()
    {
        InitializeComponent();
    }

    private void OnHalfSpeedClick(object? sender, RoutedEventArgs e)
    {
        Search.Speed = 0.5;
    }

    private void OnNormalSpeedClick(object? sender, RoutedEventArgs e)
    {
        Search.Speed = 1;
    }

    private void OnDoubleSpeedClick(object? sender, RoutedEventArgs e)
    {
        Search.Speed = 2;
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Search.Restart();
    }
}
