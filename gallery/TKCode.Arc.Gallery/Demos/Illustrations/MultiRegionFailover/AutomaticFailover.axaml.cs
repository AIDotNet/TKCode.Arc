using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiRegionFailoverAutomaticFailoverDemo : UserControl
{
    public MultiRegionFailoverAutomaticFailoverDemo()
    {
        InitializeComponent();
    }

    private void OnHalfSpeedClick(object? sender, RoutedEventArgs e)
    {
        Failover.Speed = 0.5;
    }

    private void OnNormalSpeedClick(object? sender, RoutedEventArgs e)
    {
        Failover.Speed = 1;
    }

    private void OnDoubleSpeedClick(object? sender, RoutedEventArgs e)
    {
        Failover.Speed = 2;
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        Failover.Restart();
    }
}
