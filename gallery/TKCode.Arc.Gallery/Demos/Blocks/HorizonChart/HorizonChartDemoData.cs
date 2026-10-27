namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The HorizonChart demos' data (all invented, seeded and deterministic): daily orders at fourteen fulfilment hubs against
/// each hub's winter forecast from February to May 2027 (119 days), and the latency of twenty services against their targets
/// over 90 days. Each row is noise plus a few flat-topped events (a spring stock-out, a promotion), so the rows differ in
/// where and how far they leave their baseline.
/// </summary>
public static class HorizonChartDemoData
{
    public const string OrdersTitle = "Orders against forecast";

    public const string OrdersDescription = "Daily orders at each fulfilment hub compared with its winter forecast, February to May 2027";

    /// <summary>The 119 days from 2 Feb to 31 May 2027.</summary>
    public static HorizonDay[] OrderDays()
    {
        var days = new HorizonDay[119];
        var start = new DateOnly(2027, 2, 2);
        for (var i = 0; i < days.Length; i++)
        {
            days[i] = new HorizonDay(start.AddDays(i));
        }
        return days;
    }

    // Name, then events as (start, rise, plateau, fall, amplitude) days and percent, then the noise level.
    private static readonly (string Name, (int Start, int Rise, int Plateau, int Fall, double Amplitude)[] Events, double Noise)[] s_hubs =
    [
        ("Northgate", [(52, 8, 28, 8, -33)], 2.5),
        ("Harbor Point", [(30, 7, 14, 8, 30)], 2.5),
        ("Cedar Junction", [(60, 8, 20, 8, -28), (4, 2, 3, 2, -30)], 2.5),
        ("Ironbridge", [(96, 6, 8, 8, 29)], 2.5),
        ("Maple Yard", [(58, 7, 24, 8, -26)], 2.5),
        ("Silverlake", [(68, 8, 18, 8, -24)], 2.5),
        ("Stonefield", [(36, 8, 12, 8, 33)], 2.5),
        ("Westmarch", [(40, 8, 30, 8, 15)], 2.2),
        ("Riverbend", [(20, 6, 40, 8, -14)], 2.2),
        ("Foxhollow", [(78, 8, 32, 8, 14)], 2.2),
        ("Brightwater", [], 1.8),
        ("Kingsferry", [(60, 20, 40, 0, 8)], 1.6),
        ("Ashdown", [(62, 20, 40, 0, -6)], 1.6),
        ("Larkspur", [], 1.6),
    ];

    /// <summary>Fourteen hubs, in data order (the block sorts them).</summary>
    public static HorizonSeries[] OrderSeries()
    {
        var random = new Mulberry32(20270202);
        var list = new HorizonSeries[s_hubs.Length];
        for (var i = 0; i < s_hubs.Length; i++)
        {
            var (name, events, noise) = s_hubs[i];
            list[i] = new HorizonSeries(name, Row(random, 119, noise, events, 35.4), name.ToLowerInvariant().Replace(' ', '-'));
        }
        return list;
    }

    public static readonly string[] ServiceNames =
    [
        "auth-gateway", "billing", "catalog", "checkout", "cdn-edge", "email-relay", "feed-ranker", "geocoder", "image-resize", "inventory",
        "ledger", "media-encoder", "notifier", "orders", "payments", "search", "sessions", "tax-engine", "uploads", "webhooks"
    ];

    /// <summary>The 90 days up to 31 Aug 2027.</summary>
    public static HorizonDay[] LatencyDays()
    {
        var days = new HorizonDay[90];
        var start = new DateOnly(2027, 6, 3);
        for (var i = 0; i < days.Length; i++)
        {
            days[i] = new HorizonDay(start.AddDays(i));
        }
        return days;
    }

    /// <summary>Twenty services: milliseconds above or below each service's target.</summary>
    public static HorizonSeries[] LatencySeries()
    {
        var random = new Mulberry32(20270831);
        var list = new HorizonSeries[ServiceNames.Length];
        for (var i = 0; i < list.Length; i++)
        {
            var events = new List<(int Start, int Rise, int Plateau, int Fall, double Amplitude)>();
            var kind = i % 5;
            if (kind == 0)
            {
                events.Add((random.Int(10, 55), 4, random.Int(6, 18), 6, 40 + random.Next() * 18));
            }
            else if (kind == 1)
            {
                events.Add((random.Int(30, 70), 3, random.Int(10, 30), 4, -(22 + random.Next() * 20)));
            }
            else if (kind == 2)
            {
                events.Add((random.Int(2, 20), 3, 4, 3, 55));
                events.Add((random.Int(50, 75), 5, random.Int(8, 14), 6, -28));
            }
            list[i] = new HorizonSeries(ServiceNames[i], Row(random, 90, 4.5 + (i % 3), events.ToArray(), 59.4), ServiceNames[i]);
        }
        return list;
    }

    private static double[] Row(Mulberry32 random, int days, double noise, (int Start, int Rise, int Plateau, int Fall, double Amplitude)[] events, double limit)
    {
        var values = new double[days];
        var previous = 0.0;
        for (var d = 0; d < days; d++)
        {
            var n = noise * random.Gaussian(0, 1) + 0.4 * previous;
            previous = n;
            var v = n;
            foreach (var e in events)
            {
                v += e.Amplitude * Envelope(d, e.Start, e.Rise, e.Plateau, e.Fall);
            }
            values[d] = Math.Round(Math.Clamp(v, -limit, limit), 1);
        }
        return values;
    }

    // A flat-topped envelope: up over rise days, flat for plateau days, down over fall days (a zero fall ends at the plateau).
    private static double Envelope(int day, int start, int rise, int plateau, int fall)
    {
        var t = day - start;
        if (t < 0)
        {
            return 0;
        }
        if (t < rise)
        {
            return (t + 1.0) / (rise + 1.0);
        }
        t -= rise;
        if (t < plateau)
        {
            return 1;
        }
        t -= plateau;
        return fall > 0 && t < fall ? 1 - (t + 1.0) / (fall + 1.0) : fall > 0 ? 0 : 1;
    }

    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;
        private double? _spare;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int Int(int min, int max) => min + (int)(Next() * (max - min + 1));

        public double Gaussian(double mean, double sd)
        {
            if (_spare is { } spare)
            {
                _spare = null;
                return mean + sd * spare;
            }
            double u, v, s;
            do
            {
                u = Next() * 2 - 1;
                v = Next() * 2 - 1;
                s = u * u + v * v;
            }
            while (s >= 1 || s == 0);
            var factor = Math.Sqrt(-2 * Math.Log(s) / s);
            _spare = v * factor;
            return mean + sd * u * factor;
        }
    }
}
