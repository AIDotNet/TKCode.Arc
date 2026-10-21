using Avalonia.Controls;
using Avalonia.Interactivity;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class SparklineWithoutHeadlineDemo : UserControl
{
    private int _seed = 10;

    public SparklineWithoutHeadlineDemo()
    {
        InitializeComponent();
        Visits.ItemsSource = ChartsADemoData.Daily(30, 300, 120, 7, 3);
    }

    // New data morphs the line.
    private void OnNewDataClick(object? sender, RoutedEventArgs e)
        => Visits.ItemsSource = ChartsADemoData.Daily(30, 300, 120, _seed++, 3);
}
