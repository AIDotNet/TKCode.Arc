using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ProgressCountedValueDemo : UserControl
{
    private readonly Random _random = new(3);

    public ProgressCountedValueDemo()
    {
        InitializeComponent();
    }

    private void OnAdvanceClick(object? sender, RoutedEventArgs e)
    {
        Upload.Value = Math.Min(100, Upload.Value + _random.Next(8, 30));
    }

    private void OnCompleteClick(object? sender, RoutedEventArgs e)
    {
        Upload.Value = 100;
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        Upload.Value = 0;
    }
}
