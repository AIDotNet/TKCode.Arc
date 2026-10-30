using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The card fed by the app: a timer plays the part of a realtime socket.</summary>
public partial class LiveVisitorsYourDataDemo : UserControl
{
    private static readonly (string City, string Country, double Lat, double Lon)[] s_places =
    [
        ("Portland", "US", 45.52, -122.68), ("Chicago", "US", 41.88, -87.63), ("Lisbon", "PT", 38.72, -9.14),
        ("Oslo", "NO", 59.91, 10.75), ("Warsaw", "PL", 52.23, 21.01), ("Cape Town", "ZA", -33.92, 18.42),
        ("Mumbai", "IN", 19.08, 72.88), ("Osaka", "JP", 34.69, 135.50), ("Auckland", "NZ", -36.85, 174.76),
    ];

    private readonly DispatcherTimer _socket = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly Random _random = new(11);
    private readonly Dictionary<string, int> _pages = new()
    {
        ["/app/inbox"] = 21, ["/app/reports"] = 14, ["/app/settings/billing"] = 6, ["/app/onboarding"] = 9,
    };
    private int _ticks;

    public LiveVisitorsYourDataDemo()
    {
        InitializeComponent();
        Card.Labels = new LiveVisitorsLabels { People = n => n == 1 ? "person in the app now" : "people in the app now" };
        Card.PausedChanged += (_, e) =>
        {
            // Pause your own subscription with the card.
            if (e.IsPaused)
            {
                _socket.Stop();
                Log.Text = "Feed paused.";
            }
            else
            {
                _socket.Start();
                Log.Text = "Feed resumed.";
            }
        };
        _socket.Tick += (_, _) => Receive();
        AttachedToVisualTree += (_, _) =>
        {
            _ticks = 0;
            Card.Connection = LiveVisitorsConnection.Connecting;
            _socket.Start();
        };
        DetachedFromVisualTree += (_, _) => _socket.Stop();
    }

    private void Receive()
    {
        _ticks++;
        if (_ticks == 2)
        {
            Card.Connection = LiveVisitorsConnection.Live;
            Log.Text = "Connected.";
        }
        if (_ticks < 2)
        {
            return;
        }
        // People move between pages.
        var keys = _pages.Keys.ToArray();
        var from = keys[_random.Next(keys.Length)];
        var to = keys[_random.Next(keys.Length)];
        if (_pages[from] > 1)
        {
            _pages[from]--;
            _pages[to]++;
        }
        if (_random.NextDouble() < 0.4)
        {
            _pages[to]++;
        }
        Card.Pages = _pages.Select(p => new LiveVisitorsPage(p.Key, p.Value)).ToArray();
        Card.Count = _pages.Values.Sum();

        var place = s_places[_random.Next(s_places.Length)];
        Card.PushVisitor(place.Lat + _random.NextDouble() * 2 - 1, place.Lon + _random.NextDouble() * 2 - 1);
        if (_ticks % 4 == 0)
        {
            Card.PushEvent(new LiveVisitorsEvent
            {
                Label = _ticks % 12 == 0 ? "Upgraded to Pro" : "Started a trial",
                City = place.City,
                Country = place.Country,
                At = DateTimeOffset.UtcNow,
                Latitude = place.Lat,
                Longitude = place.Lon,
            });
        }
    }
}
