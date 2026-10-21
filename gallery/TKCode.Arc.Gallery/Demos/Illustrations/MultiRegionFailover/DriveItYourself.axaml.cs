using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MultiRegionFailoverDriveItYourselfDemo : UserControl
{
    public MultiRegionFailoverDriveItYourselfDemo()
    {
        InitializeComponent();
    }

    private void OnPrimaryRegionChanged(object? sender, int index)
    {
        Primary.Text = $"Primary: {MultiRegionFailover.DefaultRegions[index]}";
    }

    private void OnFailEastClick(object? sender, RoutedEventArgs e)
    {
        Failover.FailRegion(0);
    }

    private void OnRecoverEastClick(object? sender, RoutedEventArgs e)
    {
        Failover.RecoverRegion(0);
    }

    private void OnFailWestClick(object? sender, RoutedEventArgs e)
    {
        Failover.FailRegion(1);
    }

    private void OnRecoverWestClick(object? sender, RoutedEventArgs e)
    {
        Failover.RecoverRegion(1);
    }
}
