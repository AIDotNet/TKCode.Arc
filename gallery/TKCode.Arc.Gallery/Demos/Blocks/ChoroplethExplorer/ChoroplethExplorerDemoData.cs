using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Platform;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Sample data for the choropleth explorer demos. The outlines are public-domain map files shipped next to the demos
/// (Maps/map-*.txt, held by the content assembly and served as files by the WebAssembly host: US Census cartographic boundary files for the United States, Natural Earth admin-1 for the others),
/// read and split on first use; the block parses each outline once, when it is first drawn. Every value is invented for a
/// made-up B2B tools company ("Northwind Ledger") from a fixed seed (mulberry32 20261026): revenue follows a rough size of
/// each state with log-normal noise, growth is a normal spread around +7% with about a quarter negative, coverage a skewed
/// share between 2% and 30%; counties split their state's revenue unevenly and some have no accounts yet.
/// </summary>
public static class ChoroplethExplorerDemoData
{
    public const string Description = "Fiscal year ending March 31, 2027";

    /// <summary>Reads a map file by name (map-us.txt …); the gallery reads the content assembly, the WebAssembly host fetches the file from the site, a test or tool may point elsewhere.</summary>
    public static Func<string, string?> ReadMap { get; set; } = ReadResource;

    /// <summary>One switchable area: key, map file, name, nouns, currency, revenue total and whether subregions exist.</summary>
    private sealed record AreaSpec(string Key, string File, string Name, string Noun, string Nouns, string SubNoun, string SubNouns,
        string Symbol, double Revenue, string? EmptyText);

    private static readonly AreaSpec[] s_specs =
    [
        new("us", "map-us.txt", "United States", "state", "states", "county", "counties", "$", 48_000_000,
            "County detail covers the ten states with a regional sales team."),
        new("ca", "map-ca.txt", "Canada", "province", "provinces", "district", "districts", "C$", 9_400_000,
            "Canada is reported by province only."),
        new("br", "map-br.txt", "Brazil", "state", "states", "municipality", "municipalities", "R$", 21_000_000,
            "Brazil is reported by state only."),
        new("gb", "map-gb.txt", "United Kingdom", "region", "regions", "county", "counties", "£", 7_200_000, null),
        new("fr", "map-fr.txt", "France", "region", "regions", "department", "departments", "€", 8_100_000, null),
        new("de", "map-de.txt", "Germany", "state", "states", "district", "districts", "€", 9_800_000,
            "Germany is reported by state only."),
        new("it", "map-it.txt", "Italy", "region", "regions", "province", "provinces", "€", 6_300_000, null),
        new("au", "map-au.txt", "Australia", "state", "states", "region", "regions", "A$", 5_600_000,
            "Australia is reported by state and territory only."),
    ];

    /// <summary>
    /// Rough relative size of each US state (population in millions, a public figure, rounded): it shapes the revenue so the
    /// darkest steps gather in the large states, as on a real customer map.
    /// </summary>
    private static readonly Dictionary<string, double> s_usWeight = new()
    {
        ["AL"] = 5.1, ["AK"] = 0.7, ["AZ"] = 7.4, ["AR"] = 3.1, ["CA"] = 39, ["CO"] = 5.9, ["CT"] = 3.6, ["DE"] = 1.0, ["DC"] = 0.7,
        ["FL"] = 22.6, ["GA"] = 11.0, ["HI"] = 1.4, ["ID"] = 2.0, ["IL"] = 12.5, ["IN"] = 6.9, ["IA"] = 3.2, ["KS"] = 2.9, ["KY"] = 4.5,
        ["LA"] = 4.6, ["ME"] = 1.4, ["MD"] = 6.2, ["MA"] = 7.0, ["MI"] = 10.0, ["MN"] = 5.7, ["MS"] = 2.9, ["MO"] = 6.2, ["MT"] = 1.1,
        ["NE"] = 2.0, ["NV"] = 3.2, ["NH"] = 1.4, ["NJ"] = 9.3, ["NM"] = 2.1, ["NY"] = 19.6, ["NC"] = 10.8, ["ND"] = 0.8, ["OH"] = 11.8,
        ["OK"] = 4.1, ["OR"] = 4.2, ["PA"] = 13.0, ["RI"] = 1.1, ["SC"] = 5.4, ["SD"] = 0.9, ["TN"] = 7.1, ["TX"] = 30.5, ["UT"] = 3.4,
        ["VT"] = 0.6, ["VA"] = 8.7, ["WA"] = 7.8, ["WV"] = 1.8, ["WI"] = 5.9, ["WY"] = 0.6,
    };

    private static readonly Dictionary<string, ChoroplethArea> s_cache = new();

    /// <summary>The keys of the eight demo areas, in the switch's order.</summary>
    public static IReadOnlyList<string> AreaKeys => s_specs.Select(s => s.Key).ToArray();

    /// <summary>The demo's metrics for one currency: revenue (sequential), growth (diverging) and coverage (sequential).</summary>
    public static ChoroplethMetric[] Metrics(string symbol = "$") =>
    [
        new ChoroplethMetric("revenue", "Revenue")
        {
            HeadlineLabel = "Annual recurring revenue",
            LegendCaption = "Shaded by annual recurring revenue",
            CurrencySymbol = symbol,
        },
        new ChoroplethMetric("growth", "Growth", ChoroplethValueFormat.Change, ChoroplethScale.Diverging)
        {
            HeadlineLabel = "Year-over-year change",
            LegendCaption = "Shaded by change on last year",
            Aggregate = ChoroplethAggregate.WeightedAverage,
        },
        new ChoroplethMetric("coverage", "Coverage", ChoroplethValueFormat.Percent)
        {
            HeadlineLabel = "Target accounts signed",
            LegendCaption = "Shaded by share of target accounts signed",
            Aggregate = ChoroplethAggregate.WeightedAverage,
        },
    ];

    /// <summary>All eight areas; the United States is read at once, the others when <paramref name="eager"/> or on first use.</summary>
    public static List<ChoroplethArea> Areas(bool eager = true) => s_specs.Select(s => eager || s.Key == "us" ? Area(s.Key) : Shell(s)).ToList();

    /// <summary>One area with its regions and values (the regions are cached and shared by every demo).</summary>
    public static ChoroplethArea Area(string key)
    {
        if (s_cache.TryGetValue(key, out var cached))
        {
            return Fresh(cached);
        }
        var spec = s_specs.First(s => s.Key == key);
        var area = Shell(spec);
        Fill(area, spec);
        s_cache[key] = area;
        return Fresh(area);
    }

    /// <summary>Fills an area created by <see cref="Areas"/> with lazy loading (its regions are still empty).</summary>
    public static void Load(ChoroplethArea area)
    {
        if (area.Regions.Count > 0)
        {
            return;
        }
        area.Regions.AddRange(Area(area.Key).Regions);
    }

    private static ChoroplethArea Shell(AreaSpec spec)
    {
        var area = new ChoroplethArea(spec.Key, spec.Name, spec.Nouns, spec.Noun)
        {
            SubregionNoun = spec.SubNouns,
            SubregionNounSingular = spec.SubNoun,
            EmptyDetailText = spec.EmptyText,
        };
        area.Metrics.AddRange(Metrics(spec.Symbol));
        return area;
    }

    /// <summary>A new area object over the cached regions (blocks only read regions, so the demos share the parsed outlines).</summary>
    private static ChoroplethArea Fresh(ChoroplethArea source)
    {
        var area = Shell(s_specs.First(s => s.Key == source.Key));
        area.Regions.AddRange(source.Regions);
        return area;
    }

    // Map files -------------------------------------------------------------------------------------------------------

    private static string? ReadResource(string file)
    {
        var uri = new Uri($"avares://TKCode.Arc.Gallery.Content/Demos/Blocks/ChoroplethExplorer/Maps/{file}");
        if (!AssetLoader.Exists(uri))
        {
            return null;
        }
        using var reader = new StreamReader(AssetLoader.Open(uri), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads <c>S|id|code|name|x0,y0,x1,y1|path</c> (a region) and <c>C|parent|id|name|x0,y0,x1,y1|path</c> (a subregion)
    /// lines; # starts a comment. Sub-paths that start outside the 975 × 610 box (the far Aleutian islands) are left out and
    /// the box of such a region is measured from what is left.
    /// </summary>
    private static void Fill(ChoroplethArea area, AreaSpec spec)
    {
        var text = ReadMap(spec.File) ?? string.Empty;
        var byKey = new Dictionary<string, ChoroplethRegion>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 2 || line[0] == '#')
            {
                continue;
            }
            var parts = line.Split('|', 6);
            if (parts.Length < 6)
            {
                continue;
            }
            var (path, trimmed) = KeepInside(parts[5], area.ViewBox);
            var bounds = trimmed ? default : ParseBox(parts[4]);
            if (parts[0] == "S")
            {
                var region = new ChoroplethRegion(parts[1], parts[3], path, bounds) { Code = parts[2] };
                byKey[parts[1]] = region;
                area.Regions.Add(region);
            }
            else if (parts[0] == "C" && byKey.TryGetValue(parts[1], out var parent))
            {
                parent.Subregions.Add(new ChoroplethRegion(parts[2], parts[3], path, bounds));
            }
        }
        Invent(area, spec);
    }

    private static Rect ParseBox(string text)
    {
        var p = text.Split(',');
        if (p.Length != 4)
        {
            return default;
        }
        var c = CultureInfo.InvariantCulture;
        var x0 = double.Parse(p[0], c);
        var y0 = double.Parse(p[1], c);
        return new Rect(x0, y0, double.Parse(p[2], c) - x0, double.Parse(p[3], c) - y0);
    }

    private static (string Path, bool Trimmed) KeepInside(string path, Size box)
    {
        if (path.IndexOf('M', 1) < 0)
        {
            return (path, false);
        }
        var kept = new StringBuilder(path.Length);
        var trimmed = false;
        var start = 0;
        while (start < path.Length)
        {
            var next = path.IndexOf('M', start + 1);
            var end = next < 0 ? path.Length : next;
            var piece = path.AsSpan(start, end - start);
            var comma = piece.IndexOf(',');
            var x = comma > 1 && double.TryParse(piece[1..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out var px) ? px : 0;
            if (x < 0 || x > box.Width)
            {
                trimmed = true;
            }
            else
            {
                kept.Append(piece);
            }
            start = end;
        }
        return (kept.ToString(), trimmed);
    }

    // Invented values ------------------------------------------------------------------------------------------------------

    private static void Invent(ChoroplethArea area, AreaSpec spec)
    {
        var random = new Mulberry32(20261026u + (uint)Array.IndexOf(s_specs, spec) * 7919u);
        var regions = area.Regions;
        var raw = new double[regions.Count];
        for (var i = 0; i < regions.Count; i++)
        {
            var weight = spec.Key == "us" && s_usWeight.TryGetValue(regions[i].Key, out var w) ? Math.Pow(w, 1.1) : Math.Sqrt(Math.Max(1, regions[i].Bounds.Width * regions[i].Bounds.Height)) * Math.Exp(0.6 * random.NextGaussian());
            raw[i] = weight * Math.Exp(0.55 * random.NextGaussian());
        }
        var scale = spec.Revenue / raw.Sum();
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            var revenue = Round(raw[i] * scale);
            var growth = Math.Round(Math.Clamp(7 + 10 * random.NextGaussian(), -15, 45), 1);
            var coverage = Math.Round(Math.Clamp(60 * random.NextBeta(2, 9), 1.5, 34), 1);
            region.SetMetricValue("revenue", revenue);
            region.SetMetricValue("growth", growth);
            region.SetMetricValue("coverage", coverage);
            if (region.Subregions.Count > 0)
            {
                InventSubregions(region, revenue, growth, coverage, random);
            }
        }
    }

    private static void InventSubregions(ChoroplethRegion parent, double revenue, double growth, double coverage, Mulberry32 random)
    {
        var subs = parent.Subregions;
        var shares = new double[subs.Count];
        for (var i = 0; i < subs.Count; i++)
        {
            shares[i] = random.NextGamma(0.6);
        }
        var total = shares.Sum();
        for (var i = 0; i < subs.Count; i++)
        {
            var sub = subs[i];
            if (random.NextDouble() < 0.15)
            {
                // No accounts yet: no value for any metric.
                sub.SetMetricValue("revenue", null);
                sub.SetMetricValue("growth", null);
                sub.SetMetricValue("coverage", null);
                continue;
            }
            sub.SetMetricValue("revenue", Round(revenue * shares[i] / total));
            sub.SetMetricValue("growth", Math.Round(Math.Clamp(growth + 9 * random.NextGaussian(), -30, 60), 1));
            sub.SetMetricValue("coverage", Math.Round(Math.Clamp(coverage * Math.Exp(0.5 * random.NextGaussian()), 0.5, 60), 1));
        }
    }

    /// <summary>Two significant-ish digits for large amounts, like a report would show.</summary>
    private static double Round(double value) => value >= 100_000 ? Math.Round(value / 1000) * 1000 : Math.Round(value / 100) * 100;

    /// <summary>mulberry32: a small seeded generator, with Box–Muller normals (drawn in pairs) and gamma / beta draws.</summary>
    private sealed class Mulberry32(uint seed)
    {
        private uint _state = seed;
        private double? _spare;

        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5;
                var t = _state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public double NextGaussian()
        {
            if (_spare is { } spare)
            {
                _spare = null;
                return spare;
            }
            var u1 = 1 - NextDouble();
            var u2 = NextDouble();
            var r = Math.Sqrt(-2 * Math.Log(u1));
            _spare = r * Math.Sin(2 * Math.PI * u2);
            return r * Math.Cos(2 * Math.PI * u2);
        }

        /// <summary>Gamma(shape, 1) by Marsaglia and Tsang (shape below 1 boosted by U^(1/shape)).</summary>
        public double NextGamma(double shape)
        {
            if (shape < 1)
            {
                return NextGamma(shape + 1) * Math.Pow(1 - NextDouble(), 1 / shape);
            }
            var d = shape - 1.0 / 3;
            var c = 1 / Math.Sqrt(9 * d);
            while (true)
            {
                double x, v;
                do
                {
                    x = NextGaussian();
                    v = 1 + c * x;
                }
                while (v <= 0);
                v = v * v * v;
                var u = 1 - NextDouble();
                if (Math.Log(u) < 0.5 * x * x + d - d * v + d * Math.Log(v))
                {
                    return d * v;
                }
            }
        }

        public double NextBeta(double a, double b)
        {
            var x = NextGamma(a);
            var y = NextGamma(b);
            return x / (x + y);
        }
    }
}
