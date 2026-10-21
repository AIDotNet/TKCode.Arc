using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class AnimatedCounterRollingDigitsDemo : UserControl
{
    private readonly Random _random = new(7);

    public AnimatedCounterRollingDigitsDemo()
    {
        InitializeComponent();
    }

    private void OnIncreaseClick(object? sender, RoutedEventArgs e)
    {
        Revenue.Value += _random.Next(100, 4000);
        Users.Value += _random.Next(1, 40);
        Rate.Value = Math.Round(Rate.Value + _random.NextDouble(), 2);
    }

    private void OnDecreaseClick(object? sender, RoutedEventArgs e)
    {
        Revenue.Value = Math.Max(0, Revenue.Value - _random.Next(100, 4000));
        Users.Value = Math.Max(0, Users.Value - _random.Next(1, 40));
        Rate.Value = Math.Max(0, Math.Round(Rate.Value - _random.NextDouble(), 2));
    }
}
