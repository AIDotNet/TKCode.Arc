namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The ConnectedScatter demos' data (all invented, seeded and deterministic): the acquisition cost and the twelve-month
/// customer value of three sales channels over eighteen months (July 2024 to December 2025), and a weekly spend and revenue
/// series for the formats demo. Each series is a set of anchor values joined by a smooth interpolation with a little noise
/// (seed 2417, ±1.5%), rounded to 5 for costs and 10 for values.
/// </summary>
public static class ConnectedScatterDemoData
{
    public const string Title = "Unit economics by channel";

    private static readonly string[] s_months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    /// <summary>The horizontal axis of the channel demos: acquisition cost, $300 to $1,900.</summary>
    public static ScatterAxis CostAxis() => new("CAC", "Customer acquisition cost", 300, 1900, 200);

    /// <summary>The vertical axis of the channel demos: customer value, $1k to $6k.</summary>
    public static ScatterAxis ValueAxis() => new("LTV", "12 month customer value", 1000, 6000, 1000);

    /// <summary>Field sales, Community and Resellers: eighteen months each, with notes on the first and the last.</summary>
    public static ScatterSeries[] Channels()
    {
        var random = new Mulberry32(2417);
        var labels = MonthLabels(2024, 7, 18);
        return
        [
            Series("Field sales", "field-sales", labels, random,
                [(0, 1150), (4, 1420), (8, 1720), (12, 1520), (17, 1380)],
                [(0, 3600), (4, 3850), (8, 4100), (11, 4550), (17, 5100)],
                new ScatterNote(5, "Territory split", ScatterLabelSide.Top), new ScatterNote(11, "Usage pricing", ScatterLabelSide.Left)),
            Series("Community", "community", labels, random,
                [(0, 480), (5, 445), (9, 410), (13, 390), (17, 360)],
                [(0, 2300), (6, 2750), (11, 3250), (17, 3900)],
                new ScatterNote(8, "Forum launch", ScatterLabelSide.Right)),
            Series("Resellers", "resellers", labels, random,
                [(0, 1700), (6, 1560), (12, 1380), (17, 1250)],
                [(0, 4300), (5, 4550), (11, 4950), (17, 5300)],
                new ScatterNote(4, "Margin tiers", ScatterLabelSide.Right)),
        ];
    }

    /// <summary>The first two channels without notes (the minimal demo).</summary>
    public static ScatterSeries[] PlainChannels()
    {
        var channels = Channels();
        var result = new ScatterSeries[2];
        for (var i = 0; i < 2; i++)
        {
            result[i] = new ScatterSeries(channels[i].Name, channels[i].Points.Select(p => new ScatterPoint(p.Label, p.X, p.Y)), channels[i].Key);
        }
        return result;
    }

    /// <summary>Twelve weeks of ad spend (x) against the revenue it brought in (y), one series, in euros.</summary>
    public static ScatterSeries[] Weekly()
    {
        var random = new Mulberry32(718);
        var points = new List<ScatterPoint>();
        for (var week = 0; week < 12; week++)
        {
            var spend = 8000 + 2200 * Math.Sin(week / 11.0 * Math.PI * 1.4) + week * 380 + random.Gaussian(0, 160);
            var revenue = 17000 + 3600 * Math.Sin(week / 11.0 * Math.PI * 1.1 + 0.6) + week * 1150 + random.Gaussian(0, 380);
            points.Add(new ScatterPoint($"Week {week + 1}", Math.Round(spend / 50) * 50, Math.Round(revenue / 100) * 100));
        }
        return [new ScatterSeries("Paid social", points, "paid-social")];
    }

    private static string[] MonthLabels(int year, int month, int count)
    {
        var labels = new string[count];
        for (var i = 0; i < count; i++)
        {
            var m = month - 1 + i;
            labels[i] = $"{s_months[m % 12]} {year + m / 12}";
        }
        return labels;
    }

    private static ScatterSeries Series(string name, string key, string[] labels, Mulberry32 random, (int, double)[] x, (int, double)[] y,
        params ScatterNote[] notes)
    {
        var series = new ScatterSeries { Name = name, Key = key };
        for (var i = 0; i < labels.Length; i++)
        {
            var cost = Smooth(x, i) * (1 + random.Gaussian(0, 0.008));
            var value = Smooth(y, i) * (1 + random.Gaussian(0, 0.008));
            series.Points.Add(new ScatterPoint(labels[i], Math.Round(cost / 5) * 5, Math.Round(value / 10) * 10));
        }
        series.Notes.AddRange(notes);
        return series;
    }

    // Cosine interpolation between anchors (index, value).
    private static double Smooth((int Index, double Value)[] anchors, int i)
    {
        for (var a = 0; a < anchors.Length - 1; a++)
        {
            if (i <= anchors[a + 1].Index)
            {
                var t = (double)(i - anchors[a].Index) / (anchors[a + 1].Index - anchors[a].Index);
                var eased = (1 - Math.Cos(Math.PI * t)) / 2;
                return anchors[a].Value + (anchors[a + 1].Value - anchors[a].Value) * eased;
            }
        }
        return anchors[^1].Value;
    }

    /// <summary>mulberry32 (the Showpiece demos' generator) with Box–Muller normals.</summary>
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
