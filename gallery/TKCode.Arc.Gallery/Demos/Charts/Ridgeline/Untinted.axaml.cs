using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RidgelineUntintedDemo : UserControl
{
    public RidgelineUntintedDemo()
    {
        InitializeComponent();
        Chart.ValueFormatter = v => $"{v:0} ms";

        // Three regions whose slow responses form a second hump (deterministic).
        var random = new Random(2);
        foreach (var (id, label, center, tail) in new[] { ("eu", "Europe", 120.0, 30.0), ("us", "Americas", 160.0, 45.0), ("ap", "Asia Pacific", 210.0, 70.0) })
        {
            Chart.Items.Add(new RidgelineSeries(id, label, Enumerable.Range(0, 80)
                .Select(i => center + (random.NextDouble() - 0.5) * tail + (i % 9 == 0 ? tail * 1.6 : 0)).ToArray()));
        }
    }
}
