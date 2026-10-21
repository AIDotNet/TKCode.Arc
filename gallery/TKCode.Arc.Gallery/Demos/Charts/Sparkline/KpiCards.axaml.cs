using System.Globalization;
using Avalonia.Controls;
using TKCode.Arc.Gallery.Pages;

namespace TKCode.Arc.Gallery.Demos;

public partial class SparklineKpiCardsDemo : UserControl
{
    public SparklineKpiCardsDemo()
    {
        InitializeComponent();

        RevenueCard.ItemsSource = ChartsADemoData.Daily(14, 40, 14, 1, 0.9);
        ChurnCard.ItemsSource = ChartsADemoData.Daily(14, 20, 8, 2, 0.4);
        LatencyCard.ItemsSource = ChartsADemoData.Daily(14, 190, 30, 3, -0.8);
        QueueCard.ItemsSource = ChartsADemoData.Daily(14, 900, 300, 4, 25);

        // One label per point: scrubbing reads the date instead of the position.
        var labels = ChartsADemoData.Days(14).Select(day => day.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
        foreach (var card in new[] { RevenueCard, ChurnCard, LatencyCard, QueueCard })
        {
            card.PointLabels = labels;
        }
    }
}
