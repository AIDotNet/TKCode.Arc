using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class RidgelineDistributionsByMonthDemo : UserControl
{
    private static readonly string[] s_months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static readonly RidgelineSeries[] s_zurich = Highs([3, 5, 10, 14, 19, 22, 25, 24, 20, 14, 8, 4], 3.2, 4);

    private static readonly RidgelineSeries[] s_lisbon = Highs([15, 16, 19, 20, 22, 26, 28, 29, 27, 23, 18, 15], 2.4, 9);

    public RidgelineDistributionsByMonthDemo()
    {
        InitializeComponent();
        Chart.ItemsSource = s_zurich;
    }

    // Replace the label and the groups together; every curve morphs from its current shape to the new one.
    private void OnZurichClick(object? sender, RoutedEventArgs e) => Show("Daily highs in Zurich", s_zurich);

    private void OnLisbonClick(object? sender, RoutedEventArgs e) => Show("Daily highs in Lisbon", s_lisbon);

    private void Show(string label, RidgelineSeries[] months)
    {
        Chart.Label = label;
        Chart.ItemsSource = months;
    }

    /// <summary>Daily highs per month around a seasonal mean (deterministic, normal noise).</summary>
    private static RidgelineSeries[] Highs(double[] means, double spread, int seed)
    {
        var random = new Random(seed);
        double Normal() => Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
        return means.Select((mean, month) => new RidgelineSeries(s_months[month].ToLowerInvariant(), s_months[month],
            Enumerable.Range(0, 30).Select(_ => Math.Round(mean + Normal() * spread * (month is 0 or 11 ? 1.3 : 1), 1)).ToArray())).ToArray();
    }
}
