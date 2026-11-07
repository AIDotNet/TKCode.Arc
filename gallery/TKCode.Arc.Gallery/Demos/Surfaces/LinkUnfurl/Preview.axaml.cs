using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class LinkUnfurlPreviewDemo : UserControl
{
    public LinkUnfurlPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnReplay(object? sender, RoutedEventArgs e) => Composer.Replay();
}
