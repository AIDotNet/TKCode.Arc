using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RangeHistogramSliderPriceFilterDemo : UserControl
{
    public RangeHistogramSliderPriceFilterDemo()
    {
        InitializeComponent();
        // Two groups of listings: city rooms around $140 and larger homes around $380.
        var random = new Random(7);
        var prices = new double[320];
        for (var i = 0; i < prices.Length; i++)
        {
            var center = i % 3 == 0 ? 380.0 : 140.0;
            var spread = i % 3 == 0 ? 70.0 : 45.0;
            var offset = (random.NextDouble() + random.NextDouble() + random.NextDouble() - 1.5) * spread;
            prices[i] = Math.Clamp(center + offset, 20, 600);
        }
        Price.Minimum = 0;
        Price.Maximum = 600;
        Price.Data = prices;
        Price.ValueFormatter = value => $"${value:N0}";
    }
}
