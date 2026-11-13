namespace TKCode.Arc;

/// <summary>One candle: when it opened, its open, high, low and close, and the shares traded.</summary>
internal struct MarketCandle
{
    public DateTime Time;
    public double Open;
    public double High;
    public double Low;
    public double Close;
    public double Volume;

    public MarketCandle(DateTime time, double open, double high, double low, double close, double volume)
    {
        Time = time;
        Open = open;
        High = high;
        Low = low;
        Close = close;
        Volume = volume;
    }

    /// <summary>Takes in a newer candle of the same span (folding finer candles into a coarser one).</summary>
    public void Absorb(in MarketCandle next)
    {
        High = Math.Max(High, next.High);
        Low = Math.Min(Low, next.Low);
        Close = next.Close;
        Volume += next.Volume;
    }
}

/// <summary>
/// The candles of one symbol in one range, with the two moving averages. <see cref="Warmup"/> candles before the visible ones
/// exist only so the averages are defined from the first visible candle. Ticks update the last candle in place.
/// </summary>
internal sealed class MarketSeries
{
    public MarketSeries(MarketRange range, MarketCandle[] candles, int warmup)
    {
        Range = range;
        Candles = candles;
        Warmup = warmup;
        Ma20 = new double[candles.Length];
        Ma50 = new double[candles.Length];
        ComputeAverage(Ma20, 20, 0);
        ComputeAverage(Ma50, 50, 0);
    }

    public MarketRange Range { get; }

    /// <summary>Every candle, warmup first.</summary>
    public MarketCandle[] Candles { get; }

    public int Warmup { get; }

    /// <summary>The 20 and 50 candle simple moving averages of the closes (NaN until enough candles exist).</summary>
    public double[] Ma20 { get; }

    public double[] Ma50 { get; }

    /// <summary>How many candles are shown (the warmup is not).</summary>
    public int Count => Candles.Length - Warmup;

    public ref MarketCandle this[int visibleIndex] => ref Candles[Warmup + visibleIndex];

    public double Ma20At(int visibleIndex) => Ma20[Warmup + visibleIndex];

    public double Ma50At(int visibleIndex) => Ma50[Warmup + visibleIndex];

    public ref MarketCandle Last => ref Candles[^1];

    /// <summary>A new last price: the last candle's close, high, low and volume follow, and the averages' last values.</summary>
    public void Tick(double price, double volume)
    {
        ref var last = ref Candles[^1];
        last.Close = price;
        last.High = Math.Max(last.High, price);
        last.Low = Math.Min(last.Low, price);
        last.Volume += volume;
        ComputeAverage(Ma20, 20, Candles.Length - 1);
        ComputeAverage(Ma50, 50, Candles.Length - 1);
    }

    private void ComputeAverage(double[] target, int period, int from)
    {
        // One running sum over the closes; a tick only recomputes the last value.
        var start = Math.Max(0, from - period + 1);
        double sum = 0;
        for (var i = start; i < from; i++)
        {
            sum += Candles[i].Close;
        }
        for (var i = from; i < Candles.Length; i++)
        {
            sum += Candles[i].Close;
            if (i - start + 1 > period)
            {
                sum -= Candles[i - period].Close;
            }
            target[i] = i + 1 >= period ? sum / period : double.NaN;
        }
    }
}

/// <summary>
/// The simulated market of a <see cref="MarketTerminal"/>: for each symbol a year of daily candles, five sessions of 30 minute
/// candles and today's 5 minute candles, generated deterministically from the ticker (its hash seeds the random walk) and
/// pinned so yesterday closes at <see cref="MarketSymbol.PreviousClose"/> and today's last candle at
/// <see cref="MarketSymbol.Price"/>. Today's session is the same on every range: the 30 minute and daily candles of today are
/// folded from the 5 minute ones, and a tick moves the last candle of every range together.
/// </summary>
internal sealed class MarketSimulation
{
    /// <summary>5 minute candles in a full session (9:30 to 16:00).</summary>
    public const int SessionBars = 78;

    /// <summary>How far today's session has run when the simulation starts (5 minute candles).</summary>
    public const int ElapsedBars = 62;

    private const int DailyHistory = 252;
    private const int DailyWarmup = 60;
    private const int FiveDaySessions = 5;
    private const int HalfHourWarmupSessions = 4;
    private static readonly TimeSpan s_sessionOpen = new(9, 30, 0);
    private static readonly TimeSpan s_fiveMinutes = TimeSpan.FromMinutes(5);

    private readonly Dictionary<string, Market> _markets = new(StringComparer.Ordinal);
    private readonly Random _ticks;

    public MarketSimulation(IEnumerable<MarketSymbol> symbols, DateOnly session, int tickSeed = 7)
    {
        Session = session;
        _ticks = new Random(tickSeed);
        foreach (var symbol in symbols)
        {
            if (!string.IsNullOrEmpty(symbol.Ticker) && !_markets.ContainsKey(symbol.Ticker))
            {
                _markets[symbol.Ticker] = new Market(symbol, session);
            }
        }
    }

    public DateOnly Session { get; }

    public IEnumerable<string> Tickers => _markets.Keys;

    public bool Contains(string ticker) => _markets.ContainsKey(ticker);

    /// <summary>The live price of <paramref name="ticker"/>.</summary>
    public double Price(string ticker) => _markets.TryGetValue(ticker, out var m) ? m.Price : 0;

    public MarketSymbol? Symbol(string ticker) => _markets.TryGetValue(ticker, out var m) ? m.Symbol : null;

    /// <summary>Today's open, high, low and volume so far.</summary>
    public MarketCandle Today(string ticker) => _markets.TryGetValue(ticker, out var m) ? m.TodayCandle : default;

    /// <summary>Today's 5 minute closes (the watchlist's sparkline).</summary>
    public MarketSeries Intraday(string ticker) => _markets[ticker].Get(MarketRange.OneDay);

    /// <summary>The candles of <paramref name="ticker"/> in <paramref name="range"/> (built once, then updated by ticks).</summary>
    public MarketSeries Series(string ticker, MarketRange range) => _markets[ticker].Get(range);

    /// <summary>One random step for every symbol.</summary>
    public void Tick()
    {
        foreach (var market in _markets.Values)
        {
            market.Tick(_ticks);
        }
    }

    /// <summary>Moves <paramref name="ticker"/> to <paramref name="price"/> (tests and real price feeds).</summary>
    public void SetPrice(string ticker, double price)
    {
        if (_markets.TryGetValue(ticker, out var market))
        {
            market.Apply(price, 0);
        }
    }

    internal static uint Hash(string text)
    {
        // FNV-1a: stable across runs and platforms (string.GetHashCode is randomized per process).
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }
        return hash;
    }

    private sealed class Market
    {
        private readonly MarketSeries?[] _series = new MarketSeries?[5];
        private readonly MarketCandle[] _daily;
        private readonly MarketCandle[] _today;
        private readonly MarketCandle[] _yesterday;
        private readonly DateOnly _session;
        private readonly int _seed;

        public Market(MarketSymbol symbol, DateOnly session)
        {
            Symbol = symbol;
            _session = session;
            _seed = (int)(Hash(symbol.Ticker) & 0x7FFFFFFF);
            var random = new Random(_seed);
            _daily = GenerateDaily(random);
            _yesterday = GenerateIntraday(new Random(_seed ^ 0x5bd1e995), DateOnly.FromDateTime(_daily[^1].Time), s_fiveMinutes,
                _daily[^1].Open, _daily[^1].Close, SessionBars, Symbol.Volatility, Symbol.AverageVolume);
            var open = symbol.PreviousClose * Math.Exp(Normal(random) * symbol.Volatility * 0.25);
            _today = GenerateIntraday(new Random(_seed ^ 0x27d4eb2d), session, s_fiveMinutes, open, symbol.Price, ElapsedBars,
                symbol.Volatility * Math.Sqrt(ElapsedBars / (double)SessionBars), symbol.AverageVolume * ElapsedBars / SessionBars);
            Price = symbol.Price;
        }

        public MarketSymbol Symbol { get; }

        public double Price { get; private set; }

        public MarketCandle TodayCandle
        {
            get
            {
                var candle = _today[0];
                for (var i = 1; i < _today.Length; i++)
                {
                    candle.Absorb(_today[i]);
                }
                return candle;
            }
        }

        public MarketSeries Get(MarketRange range) => _series[(int)range] ??= Build(range);

        public void Tick(Random random)
        {
            // A step of about 3% of the daily move, with a little pull toward yesterday's close so prices stay believable.
            var step = Normal(random) * Symbol.Volatility * 0.03;
            var pull = Math.Log(Symbol.PreviousClose / Price) * 0.002;
            var price = Math.Round(Price * Math.Exp(step + pull), 2);
            var volume = Symbol.AverageVolume / SessionBars * (0.004 + random.NextDouble() * 0.008);
            Apply(price, volume);
        }

        public void Apply(double price, double volume)
        {
            Price = price;
            ref var last = ref _today[^1];
            last.Close = price;
            last.High = Math.Max(last.High, price);
            last.Low = Math.Min(last.Low, price);
            last.Volume += volume;
            foreach (var series in _series)
            {
                series?.Tick(price, volume);
            }
        }

        private MarketSeries Build(MarketRange range)
        {
            switch (range)
            {
                case MarketRange.OneDay:
                {
                    // Yesterday's 5 minute candles are the warmup of today's averages.
                    var candles = new MarketCandle[_yesterday.Length + _today.Length];
                    _yesterday.CopyTo(candles, 0);
                    _today.CopyTo(candles, _yesterday.Length);
                    return new MarketSeries(range, candles, _yesterday.Length);
                }
                case MarketRange.FiveDays:
                {
                    var list = new List<MarketCandle>();
                    var sessions = FiveDaySessions - 1 + HalfHourWarmupSessions;
                    var random = new Random(_seed ^ 0x165667b1);
                    for (var s = sessions; s >= 1; s--)
                    {
                        var day = _daily[^s];
                        var bars = GenerateIntraday(random, DateOnly.FromDateTime(day.Time), TimeSpan.FromMinutes(30), day.Open,
                            day.Close, SessionBars / 6, Symbol.Volatility, day.Volume);
                        list.AddRange(bars);
                    }
                    var warmup = HalfHourWarmupSessions * (SessionBars / 6);
                    list.AddRange(Fold(_today, 6));
                    return new MarketSeries(range, list.ToArray(), warmup);
                }
                default:
                {
                    var visible = range switch
                    {
                        MarketRange.OneMonth => 22,
                        MarketRange.SixMonths => 126,
                        _ => DailyHistory,
                    };
                    var take = Math.Min(_daily.Length, visible - 1 + DailyWarmup);
                    var candles = new MarketCandle[take + 1];
                    Array.Copy(_daily, _daily.Length - take, candles, 0, take);
                    candles[^1] = TodayCandle;
                    return new MarketSeries(range, candles, Math.Max(0, candles.Length - visible));
                }
            }
        }

        private MarketCandle[] GenerateDaily(Random random)
        {
            var count = DailyHistory - 1 + DailyWarmup;
            var vol = Symbol.Volatility;
            var logs = new double[count + 1];
            // A random walk with slow swings (a drifting mean), so six months show trends rather than noise.
            double drift = 0;
            for (var i = 1; i <= count; i++)
            {
                drift = drift * 0.94 + Normal(random) * vol * 0.025;
                logs[i] = logs[i - 1] + drift + Normal(random) * vol;
            }
            // Pin the walk so yesterday closes at the previous close.
            var shift = Math.Log(Symbol.PreviousClose) - logs[count];
            var candles = new MarketCandle[count];
            for (var i = 0; i < count; i++)
            {
                var open = Math.Exp(logs[i] + shift + Normal(random) * vol * 0.2);
                var close = Math.Exp(logs[i + 1] + shift);
                var high = Math.Max(open, close) * (1 + Math.Abs(Normal(random)) * vol * 0.45);
                var low = Math.Min(open, close) * (1 - Math.Abs(Normal(random)) * vol * 0.45);
                var move = Math.Abs(Math.Log(close / open)) / Math.Max(vol, 1e-6);
                var volume = Symbol.AverageVolume * Math.Exp(Normal(random) * 0.3) * (0.8 + 0.25 * move);
                candles[i] = new MarketCandle(SessionDay(i).ToDateTime(TimeOnly.FromTimeSpan(s_sessionOpen)), Round(open), Round(high),
                    Round(low), Round(close), Math.Round(volume));
            }
            return candles;
        }

        /// <summary>Intraday candles from <paramref name="open"/> to <paramref name="close"/> (a Brownian bridge), U-shaped volume.</summary>
        private static MarketCandle[] GenerateIntraday(Random random, DateOnly day, TimeSpan span, double open, double close, int bars,
            double dayVol, double dayVolume)
        {
            var step = dayVol / Math.Sqrt(Math.Max(bars, 1));
            var walk = new double[bars + 1];
            for (var i = 1; i <= bars; i++)
            {
                walk[i] = walk[i - 1] + Normal(random) * step;
            }
            var target = Math.Log(close / open);
            var candles = new MarketCandle[bars];
            var start = day.ToDateTime(TimeOnly.FromTimeSpan(s_sessionOpen));
            double weightSum = 0;
            var weights = new double[bars];
            for (var i = 0; i < bars; i++)
            {
                var x = (i + 0.5) / bars;
                weights[i] = 1 + 1.6 * Math.Pow(2 * x - 1, 2);
                weightSum += weights[i];
            }
            for (var i = 0; i < bars; i++)
            {
                var a = Math.Exp(walk[i] - walk[bars] * i / bars + target * i / bars) * open;
                var b = Math.Exp(walk[i + 1] - walk[bars] * (i + 1) / bars + target * (i + 1) / bars) * open;
                var wick = step * 0.6;
                var high = Math.Max(a, b) * (1 + Math.Abs(Normal(random)) * wick);
                var low = Math.Min(a, b) * (1 - Math.Abs(Normal(random)) * wick);
                var volume = dayVolume * weights[i] / weightSum * Math.Exp(Normal(random) * 0.35);
                candles[i] = new MarketCandle(start + span * i, Round(a), Round(high), Round(low), Round(b), Math.Round(volume));
            }
            if (bars > 0)
            {
                candles[0].Open = Round(open);
                candles[^1].Close = Round(close);
                candles[^1].High = Math.Max(candles[^1].High, candles[^1].Close);
                candles[^1].Low = Math.Min(candles[^1].Low, candles[^1].Close);
            }
            return candles;
        }

        private static IEnumerable<MarketCandle> Fold(MarketCandle[] candles, int size)
        {
            for (var i = 0; i < candles.Length; i += size)
            {
                var folded = candles[i];
                for (var j = i + 1; j < Math.Min(i + size, candles.Length); j++)
                {
                    folded.Absorb(candles[j]);
                }
                yield return folded;
            }
        }

        /// <summary>The weekday <paramref name="index"/> sessions into the daily history (the last index is yesterday).</summary>
        private DateOnly SessionDay(int index)
        {
            var back = DailyHistory - 1 + DailyWarmup - index;
            var day = _session;
            while (back > 0)
            {
                day = day.AddDays(-1);
                if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                {
                    back--;
                }
            }
            return day;
        }

        private static double Round(double price) => Math.Round(price, 2);
    }

    internal static double Normal(Random random)
    {
        // Box-Muller.
        var u1 = 1.0 - random.NextDouble();
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
