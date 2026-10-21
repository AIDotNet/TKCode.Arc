using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextRevealForcedLinesAndDelayDemo : UserControl
{
    public TextRevealForcedLinesAndDelayDemo()
    {
        InitializeComponent();
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        Lines.Replay();
    }
}
