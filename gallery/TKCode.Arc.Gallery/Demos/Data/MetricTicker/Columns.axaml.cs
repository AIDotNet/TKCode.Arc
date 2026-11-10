using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricTickerColumnsDemo : UserControl
{
    private readonly Random _random = new(5);

    public MetricTickerColumnsDemo()
    {
        InitializeComponent();
    }

    private void OnNewOrder(object? sender, RoutedEventArgs e)
    {
        var revenue = Ticker.Items[0];
        var orders = Ticker.Items[1];
        var conversion = Ticker.Items[2];
        revenue.Value += 20 + _random.Next(180);
        orders.Value += 1;
        conversion.Value = Math.Round(conversion.Value + 0.1, 1);
    }
}
