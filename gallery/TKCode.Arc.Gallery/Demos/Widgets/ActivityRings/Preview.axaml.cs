using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ActivityRingsPreviewDemo : UserControl
{
    public ActivityRingsPreviewDemo()
    {
        InitializeComponent();
    }

    private void OnWalk(object? sender, RoutedEventArgs e)
    {
        Rings.Rings[0].Value += 90;
        Rings.Rings[1].Value += 6;
    }

    private void OnStretch(object? sender, RoutedEventArgs e)
    {
        Rings.Rings[1].Value += 4;
        Rings.Rings[2].Value += 1;
    }

    private void OnReplay(object? sender, RoutedEventArgs e) => Rings.Replay();

    private void OnGoalReached(object? sender, ActivityRingGoalReachedEventArgs e) => Readout.Text = $"{e.Ring.Label} closed.";
}
