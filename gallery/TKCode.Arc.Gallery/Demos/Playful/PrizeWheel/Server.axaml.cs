using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class PrizeWheelServerDemo : UserControl
{
    private int _ticks;

    public PrizeWheelServerDemo()
    {
        InitializeComponent();
    }

    private async void OnDraw(object? sender, RoutedEventArgs e)
    {
        Draw.IsEnabled = false;
        Readout.Text = "Asking the server...";
        // Stands in for a request that returns the winner.
        await Task.Delay(400);
        _ticks = 0;
        Wheel.SpinTo(Random.Shared.Next(Wheel.Segments.Count));
    }

    private void OnTicked(object? sender, RoutedEventArgs e) => Readout.Text = $"{++_ticks} pegs";

    private void OnSpinCompleted(object? sender, PrizeWheelResultEventArgs e)
    {
        Readout.Text = $"Winner: {e.Segment.Label}";
        Draw.IsEnabled = true;
    }
}
