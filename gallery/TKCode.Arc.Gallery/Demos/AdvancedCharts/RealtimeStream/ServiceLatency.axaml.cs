using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class RealtimeStreamServiceLatencyDemo : UserControl
{
    private static readonly string[] Causes = ["Cache flush", "Retry burst", "Slow replica", "Cold start"];

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new(4817);
    private double _phase;

    public RealtimeStreamServiceLatencyDemo()
    {
        InitializeComponent();
        // The last half minute, as a metrics endpoint would return it once.
        var start = DateTimeOffset.Now.AddSeconds(-30);
        for (var i = 0; i < 30; i++)
        {
            Next(start.AddSeconds(i));
        }
        _timer.Tick += (_, _) => Next(DateTimeOffset.Now);
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

    private void Next(DateTimeOffset time)
    {
        // A slow daily-traffic wave plus jitter; about one reading in sixteen is a spike your alerting would flag.
        _phase += 0.18;
        var latency = 142 + 26 * Math.Sin(_phase) + _random.NextDouble() * 18;
        string? anomaly = null;
        if (_random.Next(16) == 0)
        {
            latency += 90 + _random.NextDouble() * 70;
            anomaly = Causes[_random.Next(Causes.Length)];
        }
        Stream.Append(time, Math.Round(latency), anomaly);
    }
}
