using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class UsageMeterStorageDemo : UserControl
{
    private readonly Random _random = new(11);

    public UsageMeterStorageDemo()
    {
        InitializeComponent();
    }

    // Segments appear in the order they are declared: 0 is Media and 1 is Documents.
    private void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        var media = Storage.Segments[0];
        media.Value = Math.Round(media.Value + _random.Next(3, 9) + _random.NextDouble(), 1);
    }

    private void OnCleanUpClick(object? sender, RoutedEventArgs e)
    {
        Storage.Segments[0].Value = 14.2;
        Storage.Segments[1].Value = 9.6;
    }
}
