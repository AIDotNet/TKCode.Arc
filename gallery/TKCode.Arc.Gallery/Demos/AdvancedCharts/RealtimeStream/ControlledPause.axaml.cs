using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class RealtimeStreamControlledPauseDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Random _random = new(735);
    private double _moisture = 41.5;

    public RealtimeStreamControlledPauseDemo()
    {
        InitializeComponent();
        var start = DateTimeOffset.Now.AddSeconds(-20);
        for (var i = 0; i < 20; i++)
        {
            Next(start.AddSeconds(i));
        }
        _timer.Tick += (_, _) => Next(DateTimeOffset.Now);
        Stream.PausedChanged += (_, e) => Log.Text = e.IsPaused ? $"Held ({e.Reason})." : "Following the feed.";
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
        // The soil dries slowly; the irrigation line opens when it gets too low.
        _moisture += (_random.NextDouble() - 0.62) * 0.8;
        if (_moisture < 34)
        {
            _moisture += 9;
        }
        Stream.Append(time, Math.Round(_moisture, 1));
    }
}
