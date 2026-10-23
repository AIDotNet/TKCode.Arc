namespace TKCode.Arc;

/// <summary>
/// The payments of a <see cref="RevenueGlobe"/> indexed for any playhead: times in seconds since the start of the day,
/// prefix sums over all payments and per city, and payment counts per 15 minutes. Every total at a time is a binary
/// search plus a subtraction (O(log n)), so a replay at 1500× never walks the payments. Payments must arrive in time
/// order for <see cref="Append"/>; anything else is sorted once by <see cref="Build"/>.
/// </summary>
internal sealed class RevenueGlobeLedger
{
    /// <summary>Histogram bins: one per 15 minutes of a 24 hour day.</summary>
    public const int BinCount = 96;

    public const double DaySeconds = 86400;

    public const double BinSeconds = DaySeconds / BinCount;

    private double[] _times = new double[16];
    private int[] _city = new int[16];
    private double[] _amount = new double[16];
    private double[] _prefix = new double[17];
    private int _count;
    private CitySeries[] _cities = [];
    private readonly int[] _bins = new int[BinCount];

    public RevenueGlobeLedger(int cityCount)
    {
        Reset(cityCount);
    }

    /// <summary>Number of payments.</summary>
    public int Count => _count;

    /// <summary>Number of cities the ledger keeps totals for.</summary>
    public int CityCount => _cities.Length;

    /// <summary>Payments per 15 minutes over the whole day (all payments, not only those before the playhead).</summary>
    public ReadOnlySpan<int> Bins => _bins;

    /// <summary>The largest bin (0 when empty).</summary>
    public int MaxBin { get; private set; }

    /// <summary>Changes on every edit (caches compare it).</summary>
    public int Version { get; private set; }

    public double TimeOf(int index) => _times[index];

    public int CityOf(int index) => _city[index];

    public double AmountOf(int index) => _amount[index];

    /// <summary>Time of the last payment (negative infinity when empty).</summary>
    public double LastTime => _count > 0 ? _times[_count - 1] : double.NegativeInfinity;

    /// <summary>Drops every payment and sizes the per-city totals.</summary>
    public void Reset(int cityCount)
    {
        _count = 0;
        _prefix[0] = 0;
        _cities = new CitySeries[Math.Max(0, cityCount)];
        for (var i = 0; i < _cities.Length; i++)
        {
            _cities[i] = new CitySeries();
        }
        Array.Clear(_bins);
        MaxBin = 0;
        Version++;
    }

    /// <summary>
    /// Builds the ledger from unordered payments: (seconds since the day started, city index or −1, amount).
    /// Sorted stably by time.
    /// </summary>
    public void Build(ReadOnlySpan<(double Time, int City, double Amount)> payments, int cityCount)
    {
        Reset(cityCount);
        var sorted = payments.ToArray();
        var ordered = true;
        for (var i = 1; i < sorted.Length; i++)
        {
            if (sorted[i].Time < sorted[i - 1].Time)
            {
                ordered = false;
                break;
            }
        }
        if (!ordered)
        {
            var keys = new double[sorted.Length];
            var index = new int[sorted.Length];
            for (var i = 0; i < sorted.Length; i++)
            {
                keys[i] = sorted[i].Time;
                index[i] = i;
            }
            // Stable: equal times keep their order (the index breaks ties).
            Array.Sort(index, (a, b) =>
            {
                var c = keys[a].CompareTo(keys[b]);
                return c != 0 ? c : a.CompareTo(b);
            });
            var copy = new (double, int, double)[sorted.Length];
            for (var i = 0; i < index.Length; i++)
            {
                copy[i] = sorted[index[i]];
            }
            sorted = copy;
        }
        EnsureCapacity(sorted.Length);
        foreach (var (time, city, amount) in sorted)
        {
            AppendCore(time, city, amount);
        }
        Version++;
    }

    /// <summary>
    /// Adds a payment at the end; false when it is earlier than the last one (the caller rebuilds with
    /// <see cref="Build"/>).
    /// </summary>
    public bool Append(double time, int city, double amount)
    {
        if (_count > 0 && time < _times[_count - 1])
        {
            return false;
        }
        EnsureCapacity(_count + 1);
        AppendCore(time, city, amount);
        Version++;
        return true;
    }

    private void AppendCore(double time, int city, double amount)
    {
        _times[_count] = time;
        _city[_count] = city;
        _amount[_count] = amount;
        _prefix[_count + 1] = _prefix[_count] + amount;
        _count++;
        if ((uint)city < (uint)_cities.Length)
        {
            _cities[city].Add(time, amount);
        }
        if (time >= 0 && time < DaySeconds)
        {
            var bin = Math.Min(BinCount - 1, (int)(time / BinSeconds));
            var value = ++_bins[bin];
            if (value > MaxBin)
            {
                MaxBin = value;
            }
        }
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _times.Length)
        {
            return;
        }
        var size = Math.Max(needed, _times.Length * 2);
        Array.Resize(ref _times, size);
        Array.Resize(ref _city, size);
        Array.Resize(ref _amount, size);
        Array.Resize(ref _prefix, size + 1);
    }

    /// <summary>Number of payments at or before <paramref name="time"/>.</summary>
    public int CountAt(double time) => UpperBound(_times, _count, time);

    /// <summary>Total amount of the payments at or before <paramref name="time"/>.</summary>
    public double RevenueAt(double time) => _prefix[CountAt(time)];

    /// <summary>Total amount of the payments in (<paramref name="from"/>, <paramref name="to"/>].</summary>
    public double RevenueBetween(double from, double to) => to <= from ? 0 : RevenueAt(to) - RevenueAt(from);

    /// <summary>Revenue of one city at or before <paramref name="time"/>.</summary>
    public double CityRevenueAt(int city, double time) => (uint)city < (uint)_cities.Length ? _cities[city].RevenueAt(time) : 0;

    /// <summary>Payments of one city at or before <paramref name="time"/>.</summary>
    public int CityCountAt(int city, double time) => (uint)city < (uint)_cities.Length ? _cities[city].CountAt(time) : 0;

    /// <summary>Height of a histogram bar in the 40-unit view box: 4 + 32 × count / largest count (0 when there are no payments).</summary>
    public static double BarHeight(int count, int maxCount) => maxCount <= 0 ? 0 : 4 + 32.0 * count / maxCount;

    internal static int UpperBound(double[] times, int count, double time)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            if (times[mid] <= time)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    private sealed class CitySeries
    {
        public double[] Times = new double[8];
        private double[] _prefix = new double[9];
        public int Count;

        public void Add(double time, double amount)
        {
            if (Count == Times.Length)
            {
                Array.Resize(ref Times, Count * 2);
                Array.Resize(ref _prefix, Count * 2 + 1);
            }
            Times[Count] = time;
            _prefix[Count + 1] = _prefix[Count] + amount;
            Count++;
        }

        public int CountAt(double time) => UpperBound(Times, Count, time);

        public double RevenueAt(double time) => _prefix[CountAt(time)];
    }
}
