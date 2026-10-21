using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TextRevealHeroDemo : UserControl
{
    public TextRevealHeroDemo()
    {
        InitializeComponent();
    }

    private void OnReplayClick(object? sender, RoutedEventArgs e)
    {
        Hero.Replay();
    }
}
