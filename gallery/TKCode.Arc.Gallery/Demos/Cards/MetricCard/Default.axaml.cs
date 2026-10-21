using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricCardDefaultDemo : UserControl
{
    private readonly Random _random = new(3);

    public MetricCardDefaultDemo()
    {
        InitializeComponent();
    }

    private void OnNewDataClick(object? sender, RoutedEventArgs e)
    {
        var next = Math.Max(200, Orders.Value + _random.Next(-260, 300));
        var delta = (next - Orders.Value) / Orders.Value * 100;
        Orders.Value = next;
        Orders.Change = delta >= 0 ? $"+{delta:0}%" : $"−{-delta:0}%";
        Orders.Context = delta >= 0 ? "Up on last week" : "Down on last week";
        var points = new List<double>(Orders.Sparkline!) { next };
        points.RemoveAt(0);
        Orders.Sparkline = points;

        Latency.Value = Math.Max(80, Latency.Value + _random.Next(-40, 40));
        Latency.Change = Latency.Value < 182
            ? $"−{(182 - Latency.Value) / 182 * 100:0}%"
            : $"+{(Latency.Value - 182) / 182 * 100:0}%";
    }

    private void OnToggleChipClick(object? sender, RoutedEventArgs e)
        => Uptime.Change = Uptime.Change is null ? "+0.02%" : null;
}
