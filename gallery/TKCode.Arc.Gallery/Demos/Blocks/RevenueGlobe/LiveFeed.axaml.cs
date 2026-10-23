using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class RevenueGlobeLiveFeedDemo : UserControl
{
    private static readonly DateTimeOffset Day = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly Random _random = new(1022);
    private DateTimeOffset _now = Day.AddHours(16);

    public RevenueGlobeLiveFeedDemo()
    {
        InitializeComponent();
        Globe.Headquarters = Globe.Cities[0];
        Globe.DayStart = Day;
        Globe.Now = _now;
        // The history so far, as a server would send it once: about one order every 3 seconds since midnight.
        var history = new List<RevenuePayment>();
        for (var t = Day.AddSeconds(_random.NextDouble() * 6); t < _now; t = t.AddSeconds(_random.NextDouble() * 6))
        {
            history.Add(Order(t));
        }
        Globe.Payments = history;
        _timer.Tick += OnTick;
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

    private void OnTick(object? sender, EventArgs e)
    {
        // A server pushes what happened in the last 400 ms: zero to three orders, then the new present.
        var next = _now.AddMilliseconds(400);
        var offsets = Enumerable.Range(0, _random.Next(0, 4)).Select(_ => _random.Next(0, 400)).Order().ToArray();
        foreach (var offset in offsets)
        {
            // In time order: appending at the end is cheap.
            Globe.AddPayment(Order(_now.AddMilliseconds(offset)));
        }
        _now = next;
        Globe.Now = next;
    }

    private RevenuePayment Order(DateTimeOffset time)
    {
        // Weighted pick of a city, and a book or two (€4–€30).
        var cities = Globe.Cities;
        var total = cities.Sum(c => c.Weight);
        var pick = _random.NextDouble() * total;
        var city = cities[^1];
        foreach (var c in cities)
        {
            pick -= c.Weight;
            if (pick < 0)
            {
                city = c;
                break;
            }
        }
        return new RevenuePayment(time, city.Key, Math.Round(4 + _random.NextDouble() * _random.NextDouble() * 26));
    }
}
