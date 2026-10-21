using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class GaugeThresholdsDemo : UserControl
{
    private readonly Random _random = new(3);

    public GaugeThresholdsDemo()
    {
        InitializeComponent();
    }

    // Set Value and Detail together so the number and its detail stay in step.
    private void OnNewReadingClick(object? sender, RoutedEventArgs e)
    {
        var used = _random.Next(40, 500);
        Disk.Value = Math.Round(used / 5.0);
        Disk.Detail = $"{used} of 500 GB";
    }

    private void OnAlmostFullClick(object? sender, RoutedEventArgs e)
    {
        Disk.Value = 93;
        Disk.Detail = "465 of 500 GB";
    }
}
