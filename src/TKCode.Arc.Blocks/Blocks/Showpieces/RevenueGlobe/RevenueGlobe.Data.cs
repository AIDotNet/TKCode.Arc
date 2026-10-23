using System.Collections.Specialized;
using Avalonia.Collections;

namespace TKCode.Arc;

public partial class RevenueGlobe
{
    private readonly RevenueGlobeLedger _ledger;
    private RevenueGlobeScene _scene = RevenueGlobeScene.Empty;
    private RevenueRegion[] _regions = [];
    private readonly Dictionary<string, int> _cityByKey = new(StringComparer.Ordinal);
    private readonly List<RevenuePayment> _extraPayments = new();
    private string? _hqKey;

    // Totals at the playhead (after the payments still in flight).
    private double[] _cityRevenue = [];
    private int[] _cityCount = [];
    private double[] _regionRevenue = [];
    private double _revenue;
    private int _paymentCount;
    private double _lastHour;

    // Payments whose arcs are in flight: they count once their arc lands.
    private readonly List<int> _pending = new();
    private double _pendingRevenue;
    private int _pendingCount;
    private double[] _pendingCityRevenue = [];
    private int[] _pendingCityCount = [];

    /// <summary>Total revenue at the playhead (tests; what the large figure counts toward).</summary>
    internal double CurrentRevenue => _revenue;

    /// <summary>Payments at the playhead (tests).</summary>
    internal int CurrentPaymentCount => _paymentCount;

    /// <summary>Revenue of the 60 minutes before the playhead (tests).</summary>
    internal double CurrentLastHour => _lastHour;

    /// <summary>Revenue of a region at the playhead (tests).</summary>
    internal double RegionRevenue(int region) => (uint)region < (uint)_regionRevenue.Length ? _regionRevenue[region] : 0;

    /// <summary>Revenue of a city at the playhead (tests).</summary>
    internal double CityRevenueAt(int city) => (uint)city < (uint)_cityRevenue.Length ? _cityRevenue[city] : 0;

    /// <summary>Payments whose arcs are still flying (tests).</summary>
    internal int PendingCount => _pending.Count;

    internal RevenueGlobeLedger Ledger => _ledger;

    internal RevenueGlobeScene Scene => _scene;

    internal RevenueGlobeCanvas? Canvas => _canvas;

    internal RevenueGlobeTimeline? Timeline => _timeline;

    private void OnCitiesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildScene();

    private void OnRegionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildScene();

    private void RebuildScene()
    {
        var cities = Cities.ToArray();
        _regions = _regionsList.ToArray();
        _cityByKey.Clear();
        for (var i = 0; i < cities.Length; i++)
        {
            var key = cities[i].Key;
            if (!string.IsNullOrEmpty(key))
            {
                _cityByKey.TryAdd(key, i);
            }
        }
        _hqKey = Headquarters?.Key;
        var regionKeys = new string?[_regions.Length];
        for (var i = 0; i < _regions.Length; i++)
        {
            regionKeys[i] = _regions[i].Key;
        }
        _scene = new RevenueGlobeScene(cities, Headquarters, regionKeys);
        _cityRevenue = new double[cities.Length];
        _cityCount = new int[cities.Length];
        _pendingCityRevenue = new double[cities.Length];
        _pendingCityCount = new int[cities.Length];
        _regionRevenue = new double[_regions.Length];
        _hoverCity = -1;
        _listHoverCity = -1;
        if (_canvas is not null)
        {
            _canvas.Scene = _scene;
        }
        ConfigureCamera();
        RebuildLedger();
        BuildRows();
        ApplySelectionToParts(animate: false);
    }

    private int CityIndexOf(string? key)
    {
        if (key is null)
        {
            return -1;
        }
        return _cityByKey.TryGetValue(key, out var index) ? index : -1;
    }

    private double Seconds(DateTimeOffset time) => (time - EffectiveDayStart).TotalSeconds;

    private DateTimeOffset _ledgerDayStart;

    private void RebuildLedger()
    {
        _ledgerDayStart = EffectiveDayStart;
        var source = Payments;
        var list = new List<(double, int, double)>(source is ICollection<RevenuePayment> c ? c.Count + _extraPayments.Count : 1024);
        if (source is not null)
        {
            foreach (var payment in source)
            {
                list.Add((Seconds(payment.Time), CityIndexOf(payment.CityKey), payment.Amount));
            }
        }
        foreach (var payment in _extraPayments)
        {
            list.Add((Seconds(payment.Time), CityIndexOf(payment.CityKey), payment.Amount));
        }
        _ledger.Build(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list), _scene.Cities.Length);
        DropPending();
        UpdateTimeline(force: true);
        UpdateFigures();
        RefreshStill();
    }

    private void OnPaymentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null && sender is System.Collections.ICollection collection &&
            e.NewStartingIndex == collection.Count - e.NewItems.Count)
        {
            foreach (var item in e.NewItems)
            {
                if (item is RevenuePayment payment && !AppendPayment(payment))
                {
                    RebuildLedger();
                    return;
                }
            }
            AfterAppend();
            return;
        }
        RebuildLedger();
    }

    /// <summary>
    /// Adds a payment as it happens (live data). It joins the totals when the playhead reaches its time; one that is
    /// already due flies its arc right away.
    /// </summary>
    public void AddPayment(RevenuePayment payment)
    {
        _extraPayments.Add(payment);
        if (!AppendPayment(payment))
        {
            RebuildLedger();
            return;
        }
        AfterAppend();
    }

    private bool AppendPayment(RevenuePayment payment)
    {
        var seconds = Seconds(payment.Time);
        var city = CityIndexOf(payment.CityKey);
        if (!_ledger.Append(seconds, city, payment.Amount))
        {
            return false;
        }
        // A payment that arrives already due while live (the app appends after moving Now) still flies its arc.
        var time = _playback.Time;
        if (_playback.IsLive && seconds <= time && seconds > time - 5 && IsFullMotion && !IsPausedState)
        {
            TrySpawn(_ledger.Count - 1);
        }
        return true;
    }

    private void AfterAppend()
    {
        UpdateTimeline(force: false);
        UpdateFigures();
    }

    // Pending payments ---------------------------------------------------------------------------------------------------

    private bool TrySpawn(int payment)
    {
        var city = _ledger.CityOf(payment);
        if (city < 0 || city == _scene.HqIndex || !_scene.HasArc(city) || _canvas is null)
        {
            return false;
        }
        if (!_canvas.SpawnArc(city, _ledger.AmountOf(payment), payment))
        {
            return false;
        }
        _pending.Add(payment);
        var amount = _ledger.AmountOf(payment);
        _pendingRevenue += amount;
        _pendingCount++;
        _pendingCityRevenue[city] += amount;
        _pendingCityCount[city]++;
        return true;
    }

    private bool RemovePending(int payment)
    {
        var index = _pending.IndexOf(payment);
        if (index < 0)
        {
            return false;
        }
        _pending.RemoveAt(index);
        var amount = _ledger.AmountOf(payment);
        var city = _ledger.CityOf(payment);
        _pendingRevenue -= amount;
        _pendingCount--;
        if ((uint)city < (uint)_pendingCityRevenue.Length)
        {
            _pendingCityRevenue[city] -= amount;
            _pendingCityCount[city]--;
        }
        if (_pending.Count == 0)
        {
            // Clear rounding drift.
            _pendingRevenue = 0;
            _pendingCount = 0;
            Array.Clear(_pendingCityRevenue);
            Array.Clear(_pendingCityCount);
        }
        return true;
    }

    /// <summary>Arcs in flight stop counting: the totals come straight from the ledger (after a jump).</summary>
    private void DropPending()
    {
        _pending.Clear();
        _pendingRevenue = 0;
        _pendingCount = 0;
        Array.Clear(_pendingCityRevenue);
        Array.Clear(_pendingCityCount);
        _canvas?.DetachArcs();
    }

    private void OnArcLanded(int payment)
    {
        if (payment >= 0 && RemovePending(payment))
        {
            UpdateFigures();
        }
    }

    // Still arcs (reduced motion) ------------------------------------------------------------------------------------------

    /// <summary>Arcs drawn whole under reduced motion: the latest payments before the playhead, one per city.</summary>
    internal const int StillArcCount = 18;

    private void RefreshStill()
    {
        if (_canvas is null)
        {
            return;
        }
        if (IsFullMotion)
        {
            if (_canvas.StillArcCount > 0)
            {
                _canvas.SetStillArcs([]);
            }
            return;
        }
        var arcs = new List<(int, double)>(StillArcCount);
        var seen = new HashSet<int>();
        var end = _ledger.CountAt(_playback.Time);
        for (var i = end - 1; i >= 0 && i >= end - 20000 && arcs.Count < StillArcCount; i--)
        {
            var city = _ledger.CityOf(i);
            if (city < 0 || city == _scene.HqIndex || !_scene.HasArc(city) || !seen.Add(city))
            {
                continue;
            }
            arcs.Add((city, RevenueGlobeMath.ArcWeight(_ledger.AmountOf(i))));
        }
        _canvas.SetStillArcs(arcs.ToArray());
    }
}
