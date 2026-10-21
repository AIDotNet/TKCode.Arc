using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class WaffleChartSelectorsDemo : UserControl
{
    private sealed record Answer(string Name, double Weight);

    public WaffleChartSelectorsDemo()
    {
        InitializeComponent();

        // Any objects work when the chart is told how to read them.
        Chart.ItemsSource = new[] { new Answer("Yes", 64), new Answer("No", 27), new Answer("Not sure", 9) };
        Chart.KeySelector = item => ((Answer)item).Name;
        Chart.ValueSelector = item => ((Answer)item).Weight;
    }
}
