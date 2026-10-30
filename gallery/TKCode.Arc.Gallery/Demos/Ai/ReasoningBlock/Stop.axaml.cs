using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>Streams the reasoning; Stop ends it as Stopped, Replay starts over.</summary>
public partial class ReasoningBlockStopDemo : UserControl
{
    public ReasoningBlockStopDemo()
    {
        InitializeComponent();
    }

    private void OnReplay(object? sender, RoutedEventArgs e)
    {
        Block.IsExpanded = false;
        Block.Simulation?.Restart();
    }
}
