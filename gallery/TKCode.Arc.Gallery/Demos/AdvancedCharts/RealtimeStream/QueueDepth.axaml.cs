using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class RealtimeStreamQueueDepthDemo : UserControl
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Random _random = new(2093);
    private readonly ObservableCollection<RealtimeStreamPoint> _points = new();
    private double _depth = 180;
    private bool _backlog;

    public RealtimeStreamQueueDepthDemo()
    {
        InitializeComponent();
        // A collection works too: the chart watches it and picks up what is added.
        var start = DateTimeOffset.Now.AddSeconds(-60);
        for (var i = 0; i < 120; i++)
        {
            _points.Add(Next(start.AddMilliseconds(i * 500)));
        }
        Stream.Points = _points;
        _timer.Tick += (_, _) =>
        {
            _points.Add(Next(DateTimeOffset.Now));
            // This feed trims its own history; the chart keeps what it needs.
            if (_points.Count > 240)
            {
                _points.RemoveAt(0);
            }
        };
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

    private RealtimeStreamPoint Next(DateTimeOffset time)
    {
        // Workers drain the queue while uploads arrive in bursts. Crossing 420 jobs raises one flag until it recovers.
        var arrivals = _random.Next(5) == 0 ? _random.Next(30, 110) : _random.Next(0, 22);
        _depth = Math.Clamp(_depth + arrivals - 16, 0, 500);
        var crossed = _depth >= 420 && !_backlog;
        _backlog = _depth >= 420 || (_backlog && _depth > 300);
        return new RealtimeStreamPoint(time, Math.Round(_depth), crossed ? "Backlog" : null);
    }
}
