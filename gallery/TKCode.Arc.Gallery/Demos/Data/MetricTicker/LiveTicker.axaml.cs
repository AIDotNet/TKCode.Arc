using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class MetricTickerLiveTickerDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Random _random = new(11);

    public MetricTickerLiveTickerDemo()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Update();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void Update()
    {
        // One or two metrics change per tick, as live readings do.
        var item = Ticker.Items[_random.Next(Ticker.Items.Count)];
        var drift = 1 + (_random.NextDouble() - 0.5) * 0.12;
        item.Value = Math.Round(item.Value * drift, item.Decimals);
    }
}
