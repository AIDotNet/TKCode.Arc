namespace TKCode.Arc;

/// <summary>A city a <see cref="LiveVisitorsSimulation"/> sends people from.</summary>
public sealed class LiveVisitorsSimulationCity
{
    /// <summary>Creates an empty city; set its name, country and position.</summary>
    public LiveVisitorsSimulationCity()
    {
    }

    /// <summary>Creates a city with its name, ISO 3166 alpha-2 country code, position and weight.</summary>
    public LiveVisitorsSimulationCity(string name, string country, double latitude, double longitude, int weight = 1)
    {
        Name = name;
        Country = country;
        Latitude = latitude;
        Longitude = longitude;
        Weight = weight;
    }

    /// <summary>The city's name, shown in the ticker.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The ISO 3166 alpha-2 country code ("DE").</summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>Latitude in degrees, north positive.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude in degrees, east positive.</summary>
    public double Longitude { get; set; }

    /// <summary>How much of the traffic comes from here, relative to the other cities.</summary>
    public int Weight { get; set; } = 1;
}

/// <summary>A page of the simulated site, with how much of the traffic it gets.</summary>
public sealed class LiveVisitorsSimulationPage
{
    /// <summary>Creates an empty page; set its path and weight.</summary>
    public LiveVisitorsSimulationPage()
    {
    }

    /// <summary>Creates a page with its path and weight.</summary>
    public LiveVisitorsSimulationPage(string path, int weight = 1)
    {
        Path = path;
        Weight = weight;
    }

    /// <summary>The page's path ("/pricing").</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>How many of the people on the site are here, relative to the other pages.</summary>
    public int Weight { get; set; } = 1;
}

/// <summary>A kind of conversion the simulated site records, with how often it happens.</summary>
public sealed class LiveVisitorsSimulationConversion
{
    /// <summary>Creates an empty conversion; set its label and weight.</summary>
    public LiveVisitorsSimulationConversion()
    {
    }

    /// <summary>Creates a conversion with its label and weight.</summary>
    public LiveVisitorsSimulationConversion(string label, int weight = 1)
    {
        Label = label;
        Weight = weight;
    }

    /// <summary>What happened, short and in the past tense ("Signed up").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>How often it happens, relative to the other conversions.</summary>
    public int Weight { get; set; } = 1;
}

/// <summary>
/// A simulated visitors stream for a <see cref="LiveVisitors"/> card that has no <see cref="LiveVisitors.Count"/>, for
/// demos, prototypes and tests. It plays the site you describe: about <see cref="TypicalCount"/> people spread over
/// <see cref="SitePages"/>, a new arrival from one of <see cref="Cities"/> every 600ms, page counts that drift, and one of
/// <see cref="Conversions"/> every 2.4s; it starts with a few conversions from the last minutes. Nothing is fetched or
/// tracked. It has no timer of its own: call <see cref="Advance"/> with the time that passed (the card does this from its
/// clock while it is on screen and not paused). Setting any data property starts the stream over. Create one per card.
/// </summary>
public sealed class LiveVisitorsSimulation
{
    /// <summary>Seconds between arrivals.</summary>
    public const double ArrivalInterval = 0.6;

    /// <summary>Seconds between conversions.</summary>
    public const double EventInterval = 2.4;

    /// <summary>Seconds an arrival stays on the map.</summary>
    public const double PingLifetime = 6;

    /// <summary>Seconds between drifts of the count and the page counts.</summary>
    public const double DriftInterval = 1.2;

    private const int EventLimit = 12;

    /// <summary>How far the count may wander below and above <see cref="TypicalCount"/>, as shares of it.</summary>
    private const double WanderDown = 20.0 / 128;

    private const double WanderUp = 24.0 / 128;

    private readonly Func<DateTimeOffset> _clock;
    private readonly List<(LiveVisitorsPing Ping, double Born)> _pings = [];
    private readonly List<LiveVisitorsEvent> _events = [];
    private Random _random = new(7);
    private IReadOnlyList<LiveVisitorsSimulationCity> _cities = [];
    private IReadOnlyList<LiveVisitorsSimulationPage> _sitePages = [];
    private IReadOnlyList<LiveVisitorsSimulationConversion> _conversions = [];
    private int _typicalCount;
    private int _seed;
    private int[] _pageCounts = [];
    private int _totalWeight;
    private double _time;
    private double _nextArrival;
    private double _nextEvent;
    private double _nextDrift;
    private int _serial;
    private int _target;

    /// <summary>Creates an empty stream with seed 7 on the system clock; set its data.</summary>
    public LiveVisitorsSimulation()
        : this(7)
    {
    }

    /// <summary>Creates the stream. The same <paramref name="seed"/> and data play the same traffic; <paramref name="clock"/> stamps the events (the system clock by default).</summary>
    public LiveVisitorsSimulation(int seed, Func<DateTimeOffset>? clock = null)
    {
        _seed = seed;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        Restart();
    }

    /// <summary>Raised after <see cref="Advance"/> changed the data.</summary>
    public event EventHandler? Changed;

    /// <summary>The cities people come from.</summary>
    public IReadOnlyList<LiveVisitorsSimulationCity> Cities
    {
        get => _cities;
        set
        {
            _cities = value ?? [];
            Restart();
        }
    }

    /// <summary>The pages of the site, with their share of the people on it.</summary>
    public IReadOnlyList<LiveVisitorsSimulationPage> SitePages
    {
        get => _sitePages;
        set
        {
            _sitePages = value ?? [];
            Restart();
        }
    }

    /// <summary>The conversions the ticker shows (signups, upgrades), with how often each happens.</summary>
    public IReadOnlyList<LiveVisitorsSimulationConversion> Conversions
    {
        get => _conversions;
        set
        {
            _conversions = value ?? [];
            Restart();
        }
    }

    /// <summary>About how many people are on the site: the count starts there and wanders within about 15% of it.</summary>
    public int TypicalCount
    {
        get => _typicalCount;
        set
        {
            _typicalCount = Math.Max(0, value);
            Restart();
        }
    }

    /// <summary>Seeds the traffic: the same seed and data play the same stream.</summary>
    public int Seed
    {
        get => _seed;
        set
        {
            _seed = value;
            Restart();
        }
    }

    /// <summary>The seconds the stream has played.</summary>
    public double Time => _time;

    /// <summary>People on the site right now.</summary>
    public int Count { get; private set; }

    /// <summary>The pages with people on them, busiest first.</summary>
    public IReadOnlyList<LiveVisitorsPage> Pages { get; private set; } = [];

    /// <summary>The arrivals of the last six seconds, newest first.</summary>
    public IReadOnlyList<LiveVisitorsPing> Visitors { get; private set; } = [];

    /// <summary>The recent conversions, newest first.</summary>
    public IReadOnlyList<LiveVisitorsEvent> Events { get; private set; } = [];

    /// <summary>Starts the stream over from its data: the count at <see cref="TypicalCount"/>, a few recent conversions and arrivals.</summary>
    public void Restart()
    {
        _random = new Random(_seed);
        _pings.Clear();
        _events.Clear();
        _time = 0;
        _serial = 0;
        _totalWeight = 0;
        foreach (var city in _cities)
        {
            _totalWeight += Math.Max(0, city.Weight);
        }
        Count = _typicalCount;
        _target = _typicalCount;
        _pageCounts = new int[_sitePages.Count];
        SpreadPages();
        // A few conversions from the last minutes so the ticker is full from the start.
        var now = _clock();
        int[] ages = [9, 34, 71, 138];
        for (var i = ages.Length - 1; i >= 0; i--)
        {
            if (PickCity() is { } city && PickConversion() is { } conversion)
            {
                _events.Insert(0, new LiveVisitorsEvent("e" + (++_serial), conversion.Label, city.Name, city.Country, now.AddSeconds(-ages[i]))
                {
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                });
            }
        }
        // Arrivals of the last seconds, already settled on the map.
        for (var i = 0; i < 7; i++)
        {
            AddPing(LiveVisitorsPingKind.Visit, -i * ArrivalInterval * 1.3);
        }
        _nextArrival = ArrivalInterval;
        _nextEvent = EventInterval;
        _nextDrift = DriftInterval;
        Publish();
    }

    /// <summary>Plays <paramref name="elapsed"/> of traffic. Returns whether anything changed (and then raises <see cref="Changed"/>).</summary>
    public bool Advance(TimeSpan elapsed)
    {
        var dt = Math.Max(0, elapsed.TotalSeconds);
        // A long gap (the card was off screen) plays at most a few seconds, so nothing floods in at once.
        dt = Math.Min(dt, PingLifetime);
        if (dt <= 0)
        {
            return false;
        }
        var end = _time + dt;
        var changed = false;
        while (true)
        {
            var next = Math.Min(_nextArrival, Math.Min(_nextEvent, _nextDrift));
            if (next > end)
            {
                break;
            }
            _time = next;
            if (next == _nextArrival)
            {
                changed |= AddPing(LiveVisitorsPingKind.Visit, _time);
                _nextArrival += ArrivalInterval;
            }
            else if (next == _nextEvent)
            {
                changed |= AddEvent();
                _nextEvent += EventInterval;
            }
            else
            {
                changed |= Drift();
                _nextDrift += DriftInterval;
            }
        }
        _time = end;
        var before = _pings.Count;
        _pings.RemoveAll(p => _time - p.Born > PingLifetime);
        changed |= _pings.Count != before;
        if (changed)
        {
            Publish();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return changed;
    }

    private LiveVisitorsSimulationCity? PickCity()
    {
        if (_totalWeight <= 0)
        {
            return null;
        }
        var pick = _random.Next(_totalWeight);
        foreach (var city in _cities)
        {
            var weight = Math.Max(0, city.Weight);
            if (pick < weight)
            {
                return city;
            }
            pick -= weight;
        }
        return null;
    }

    private LiveVisitorsSimulationConversion? PickConversion()
    {
        var total = 0;
        foreach (var conversion in _conversions)
        {
            total += Math.Max(0, conversion.Weight);
        }
        if (total <= 0)
        {
            return null;
        }
        var pick = _random.NextDouble() * total;
        foreach (var conversion in _conversions)
        {
            var weight = Math.Max(0, conversion.Weight);
            if (pick < weight)
            {
                return conversion;
            }
            pick -= weight;
        }
        return null;
    }

    private bool AddPing(LiveVisitorsPingKind kind, double born)
    {
        if (PickCity() is not { } city)
        {
            return false;
        }
        // Arrivals scatter around their city so the map does not ping on one spot.
        var lat = city.Latitude + (_random.NextDouble() - 0.5) * 5;
        var lon = city.Longitude + (_random.NextDouble() - 0.5) * 7;
        _pings.Add((new LiveVisitorsPing("v" + (++_serial), lat, lon, kind), born));
        return true;
    }

    private bool AddEvent()
    {
        if (PickCity() is not { } city || PickConversion() is not { } conversion)
        {
            return false;
        }
        var id = "e" + (++_serial);
        _events.Insert(0, new LiveVisitorsEvent(id, conversion.Label, city.Name, city.Country, _clock())
        {
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        });
        if (_events.Count > EventLimit)
        {
            _events.RemoveAt(_events.Count - 1);
        }
        _pings.Add((new LiveVisitorsPing(id, city.Latitude, city.Longitude, LiveVisitorsPingKind.Event), _time));
        return true;
    }

    /// <summary>The count walks toward a target that moves now and then; people move between pages.</summary>
    private bool Drift()
    {
        if (_typicalCount <= 0)
        {
            return false;
        }
        if (_random.NextDouble() < 0.25)
        {
            var low = Math.Max(1, _typicalCount - (int)Math.Round(_typicalCount * WanderDown));
            var high = Math.Max(low, _typicalCount + (int)Math.Round(_typicalCount * WanderUp));
            _target = Math.Clamp(_target + _random.Next(-9, 10), low, high);
        }
        var step = Math.Sign(_target - Count) * _random.Next(1, 4);
        if (step == 0)
        {
            step = _random.Next(-1, 2);
        }
        Count = Math.Max(1, Count + step);
        if (_pageCounts.Length > 0)
        {
            // Two pages trade a few people, then the pages are scaled back to the count.
            var from = _random.Next(_pageCounts.Length);
            var to = _random.Next(_pageCounts.Length);
            var moved = Math.Min(_pageCounts[from], _random.Next(1, 6));
            _pageCounts[from] -= moved;
            _pageCounts[to] += moved;
            Rebalance();
        }
        return true;
    }

    private void SpreadPages()
    {
        var weights = 0;
        foreach (var page in _sitePages)
        {
            weights += Math.Max(0, page.Weight);
        }
        if (weights <= 0)
        {
            return;
        }
        for (var i = 0; i < _sitePages.Count; i++)
        {
            _pageCounts[i] = (int)Math.Round(Count * (double)Math.Max(0, _sitePages[i].Weight) / weights);
        }
        Rebalance();
    }

    private void Rebalance()
    {
        var sum = 0;
        foreach (var c in _pageCounts)
        {
            sum += c;
        }
        var i = 0;
        while (sum != Count && i < 1000)
        {
            var index = _random.Next(_pageCounts.Length);
            if (sum < Count)
            {
                _pageCounts[index]++;
                sum++;
            }
            else if (_pageCounts[index] > 0)
            {
                _pageCounts[index]--;
                sum--;
            }
            i++;
        }
    }

    private void Publish()
    {
        var pages = new List<LiveVisitorsPage>(_pageCounts.Length);
        for (var i = 0; i < _pageCounts.Length && i < _sitePages.Count; i++)
        {
            if (_pageCounts[i] > 0)
            {
                pages.Add(new LiveVisitorsPage(_sitePages[i].Path, _pageCounts[i]));
            }
        }
        pages.Sort((a, b) => b.Visitors != a.Visitors ? b.Visitors.CompareTo(a.Visitors) : string.CompareOrdinal(a.Path, b.Path));
        Pages = pages;
        var pings = new List<LiveVisitorsPing>(_pings.Count);
        for (var i = _pings.Count - 1; i >= 0; i--)
        {
            pings.Add(_pings[i].Ping);
        }
        Visitors = pings;
        Events = [.. _events];
    }
}
