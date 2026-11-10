using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RangeHistogramSliderFreeThumbsDemo : UserControl
{
    public RangeHistogramSliderFreeThumbsDemo()
    {
        InitializeComponent();
        // A long tail: most requests are quick, a few take far longer.
        var random = new Random(11);
        var times = new double[500];
        for (var i = 0; i < times.Length; i++)
        {
            times[i] = 40 - 90 * Math.Log(1 - random.NextDouble());
        }
        Latency.Data = times;
        Latency.ValueFormatter = value => $"{value:N0} ms";
    }
}
