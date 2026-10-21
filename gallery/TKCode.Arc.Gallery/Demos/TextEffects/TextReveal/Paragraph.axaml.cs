using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextRevealParagraphDemo : UserControl
{
    public TextRevealParagraphDemo()
    {
        InitializeComponent();
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        Body.Replay();
    }
}
