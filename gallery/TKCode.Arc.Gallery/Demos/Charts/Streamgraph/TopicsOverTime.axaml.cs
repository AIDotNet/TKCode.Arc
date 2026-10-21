using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class StreamgraphTopicsOverTimeDemo : UserControl
{
    public StreamgraphTopicsOverTimeDemo()
    {
        InitializeComponent();
        ShowWeeks(26);
    }

    private void On12WeeksClick(object? sender, RoutedEventArgs e) => ShowWeeks(12);

    private void On26WeeksClick(object? sender, RoutedEventArgs e) => ShowWeeks(26);

    private void On52WeeksClick(object? sender, RoutedEventArgs e) => ShowWeeks(52);

    private void OnWiggleClick(object? sender, RoutedEventArgs e) => Chart.Offset = StreamOffset.Wiggle;

    private void OnSilhouetteClick(object? sender, RoutedEventArgs e) => Chart.Offset = StreamOffset.Silhouette;

    private void OnZeroClick(object? sender, RoutedEventArgs e) => Chart.Offset = StreamOffset.Zero;

    // Replace the week starts and every layer's values together; each layer blends from its shape on screen.
    private void ShowWeeks(int weeks)
    {
        var start = ChartsADemoData.Today.AddDays(-7 * (weeks - 1));
        Chart.ItemsSource = Enumerable.Range(0, weeks).Select(w => start.AddDays(7 * w)).ToArray();
        for (var i = 0; i < Chart.Series.Count; i++)
        {
            Chart.Series[i].Values = ChartsADemoData.Daily(weeks, 60 - i * 9, 40, weeks * 7 + i, i % 2 == 0 ? 0.8 : -0.3);
        }
    }
}
