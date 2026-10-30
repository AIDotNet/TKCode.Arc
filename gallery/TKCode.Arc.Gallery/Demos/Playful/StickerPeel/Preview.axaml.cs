using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class StickerPeelPreviewDemo : UserControl
{
    public StickerPeelPreviewDemo()
    {
        InitializeComponent();
    }

    // Clearing the positions sends every sticker gliding back to where its item starts.
    private void OnReset(object? sender, RoutedEventArgs e) => Lid.Reset();
}
