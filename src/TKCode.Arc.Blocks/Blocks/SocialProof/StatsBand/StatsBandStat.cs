// Ported from Arc's free stats-band block (MIT, Copyright (c) 2026 Elia Kuratli): the data shapes of stats-band-data.ts and the
// dotted world of stats-band-world.ts. The sample numbers are this library's own.

using Avalonia;
using Avalonia.Collections;

namespace TKCode.Arc;

/// <summary>How a <see cref="StatsBand"/> sets its stats.</summary>
public enum StatsBandLayout
{
    /// <summary>The stats float on whitespace.</summary>
    Plain,

    /// <summary>A hairline grid, ruled above and below.</summary>
    Divided
}

/// <summary>How a stat writes its number.</summary>
public enum StatsBandNotation
{
    /// <summary>All digits: 2,334,000.</summary>
    Standard,

    /// <summary>Short: 2.3M. The unit is drawn like a suffix and the count stays inside it.</summary>
    Compact
}

/// <summary>A small picture that proves the number beside it.</summary>
public abstract class StatsBandVisual : AvaloniaObject
{
}

/// <summary>A sparkline of a series, oldest first.</summary>
public sealed class StatsBandTrend : StatsBandVisual
{
    /// <summary>Creates the object with its empty lists.</summary>
    public StatsBandTrend()
    {
        Values = new();
    }

    /// <summary>The series, oldest first.</summary>
    public static readonly StyledProperty<AvaloniaList<double>> ValuesProperty =
        AvaloniaProperty.Register<StatsBandTrend, AvaloniaList<double>>(nameof(Values));

    /// <inheritdoc cref="ValuesProperty"/>
    public AvaloniaList<double> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
}

/// <summary>One bar per day: a full day is full height, a day with downtime is visibly shorter.</summary>
public sealed class StatsBandUptime : StatsBandVisual
{
    /// <summary>Creates the object with its empty lists.</summary>
    public StatsBandUptime()
    {
        Days = new();
    }

    /// <summary>Each day's uptime in percent, oldest first.</summary>
    public static readonly StyledProperty<AvaloniaList<double>> DaysProperty =
        AvaloniaProperty.Register<StatsBandUptime, AvaloniaList<double>>(nameof(Days));

    /// <inheritdoc cref="DaysProperty"/>
    public AvaloniaList<double> Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }
}

/// <summary>A histogram of equal-width bins across 0 to <see cref="Maximum"/>; the bars below <see cref="Marker"/> are inked.</summary>
public sealed class StatsBandDistribution : StatsBandVisual
{
    /// <summary>Creates the object with its empty lists.</summary>
    public StatsBandDistribution()
    {
        Bins = new();
    }

    /// <summary>The count of each bin.</summary>
    public static readonly StyledProperty<AvaloniaList<double>> BinsProperty =
        AvaloniaProperty.Register<StatsBandDistribution, AvaloniaList<double>>(nameof(Bins));

    /// <inheritdoc cref="BinsProperty"/>
    public AvaloniaList<double> Bins { get => GetValue(BinsProperty); set => SetValue(BinsProperty, value); }

    /// <summary>The value at the end of the last bin.</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<StatsBandDistribution, double>(nameof(Maximum));

    /// <inheritdoc cref="MaximumProperty"/>
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>The value (such as the median) below which bars are inked.</summary>
    public static readonly StyledProperty<double> MarkerProperty =
        AvaloniaProperty.Register<StatsBandDistribution, double>(nameof(Marker));

    /// <inheritdoc cref="MarkerProperty"/>
    public double Marker { get => GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }
}

/// <summary>A dotted world with a dot lit for each point.</summary>
public sealed class StatsBandMap : StatsBandVisual
{
    /// <summary>Creates the object with its empty lists.</summary>
    public StatsBandMap()
    {
        Points = new();
    }

    /// <summary>The places to light, each as a point of longitude (X) and latitude (Y).</summary>
    public static readonly StyledProperty<AvaloniaList<Point>> PointsProperty =
        AvaloniaProperty.Register<StatsBandMap, AvaloniaList<Point>>(nameof(Points));

    /// <inheritdoc cref="PointsProperty"/>
    public AvaloniaList<Point> Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
}

/// <summary>One headline number of a <see cref="StatsBand"/>.</summary>
public sealed class StatsBandStat : AvaloniaObject
{
    /// <summary>The final number; the band counts up to it.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<StatsBandStat, double>(nameof(Value));

    /// <inheritdoc cref="ValueProperty"/>
    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Text before the number, such as "$".</summary>
    public static readonly StyledProperty<string?> PrefixProperty =
        AvaloniaProperty.Register<StatsBandStat, string?>(nameof(Prefix));

    /// <inheritdoc cref="PrefixProperty"/>
    public string? Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>Text after the number, such as "%", "ms" or "+", drawn smaller and muted.</summary>
    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<StatsBandStat, string?>(nameof(Suffix));

    /// <inheritdoc cref="SuffixProperty"/>
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    /// <summary>Digits after the decimal point.</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<StatsBandStat, int>(nameof(Decimals));

    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }

    /// <summary>Standard digits, or Compact for 2.3M.</summary>
    public static readonly StyledProperty<StatsBandNotation> NotationProperty =
        AvaloniaProperty.Register<StatsBandStat, StatsBandNotation>(nameof(Notation));

    /// <inheritdoc cref="NotationProperty"/>
    public StatsBandNotation Notation { get => GetValue(NotationProperty); set => SetValue(NotationProperty, value); }

    /// <summary>What the number measures.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatsBandStat, string>(nameof(Label), string.Empty);

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>One short supporting line under the label.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<StatsBandStat, string?>(nameof(Detail));

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <summary>One line of context that replaces the detail while the stat is hovered or focused.</summary>
    public static readonly StyledProperty<string?> ContextProperty =
        AvaloniaProperty.Register<StatsBandStat, string?>(nameof(Context));

    /// <inheritdoc cref="ContextProperty"/>
    public string? Context { get => GetValue(ContextProperty); set => SetValue(ContextProperty, value); }

    /// <summary>A tiny picture that draws in after the number lands.</summary>
    public static readonly StyledProperty<StatsBandVisual?> VisualProperty =
        AvaloniaProperty.Register<StatsBandStat, StatsBandVisual?>(nameof(Visual));

    /// <inheritdoc cref="VisualProperty"/>
    public StatsBandVisual? Visual { get => GetValue(VisualProperty); set => SetValue(VisualProperty, value); }
}

/// <summary>The coarse dotted world: 80 columns by 30 rows, from 168° W to 192° E and 76° N to 56° S.</summary>
internal static class StatsBandWorld
{
    public const int Columns = 80;
    public const int Rows = 30;
    private const double West = -168, East = 192, North = 76, South = -56;

    private static readonly string[] s_rows =
    [
        "001f007f800020fc0000", "7fc0cc7f00781ffffff8", "7ffffe38c0dffffffff8", "7fff0e1003bfffffff60", "40ffcf0009bffffff0c0",
        "007fff801ffffffff880", "003fff0007fffffff000", "003ffc001c737fffec00", "003ff800181f7fff4800", "001ff0000f07ffff3000",
        "000f80001fff7fff0000", "000f00003ffb9fff0000", "000340003fffc7b80000", "0001c0003ffd82380000", "000020003ffe021c0000",
        "00001f001fff00100000", "00000fc001ff00010000", "00000fe001fe00028000", "00001ff800fc00080c00", "00000ff800fc00000200",
        "000007f800fc00003000", "000003f800fd0000fc00", "000003f000790001fe00", "000007e000780001fe00", "000007c000700001de00",
        "00000780000000000e00", "00000600000000000008", "00000600000000000020", "00000400000000000000", "00000400000000000000",
    ];

    /// <summary>Land cells as a flat row-major array.</summary>
    public static readonly bool[] Land = BuildLand();

    private static bool[] BuildLand()
    {
        var land = new bool[Columns * Rows];
        for (var y = 0; y < Rows; y++)
        {
            for (var d = 0; d < s_rows[y].Length; d++)
            {
                var bits = Convert.ToInt32(s_rows[y][d].ToString(), 16);
                for (var b = 0; b < 4; b++)
                {
                    land[y * Columns + d * 4 + b] = (bits & (8 >> b)) != 0;
                }
            }
        }
        return land;
    }

    /// <summary>Snaps a point to the nearest land cell, so a coastal city never lights a dot in the sea.</summary>
    public static int NearestLand(double longitude, double latitude)
    {
        var wrapped = ((longitude - West) % 360 + 360) % 360;
        var x = Math.Clamp((int)Math.Floor(wrapped / (East - West) * Columns), 0, Columns - 1);
        var y = Math.Clamp((int)Math.Floor((North - latitude) / (North - South) * Rows), 0, Rows - 1);
        var best = y * Columns + x;
        var distance = int.MaxValue;
        for (var dy = -2; dy <= 2; dy++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var cx = x + dx;
                var cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= Columns || cy >= Rows || !Land[cy * Columns + cx])
                {
                    continue;
                }
                var d = dx * dx + dy * dy;
                if (d < distance)
                {
                    distance = d;
                    best = cy * Columns + cx;
                }
            }
        }
        return best;
    }
}

/// <summary>The sample band: a fictional invoicing service.</summary>
internal static class StatsBandSamples
{
    public static StatsBandStat[] Create()
    {
        var days = new AvaloniaList<double>();
        for (var day = 0; day < 90; day++)
        {
            days.Add(day == 61 ? 99.31 : day == 17 ? 99.9 : 100);
        }
        return
        [
            new StatsBandStat
            {
                Value = 1_486_000, Notation = StatsBandNotation.Compact, Decimals = 1, Label = "Invoices sent this year",
                Detail = "By 8,700 small businesses", Context = "Up from 910K the year before",
                Visual = new StatsBandTrend { Values = new AvaloniaList<double>(92, 97, 94, 108, 113, 121, 119, 134, 142, 151, 168, 177) }
            },
            new StatsBandStat
            {
                Value = 99.98, Decimals = 2, Suffix = "%", Label = "Uptime over 90 days",
                Detail = "Checked once a minute", Context = "16 minutes down, 14 of them on one night",
                Visual = new StatsBandUptime { Days = days }
            },
            new StatsBandStat
            {
                Value = 42, Suffix = "ms", Label = "Median page response",
                Detail = "Across every region", Context = "p95 is 118 ms, down from 170 ms",
                Visual = new StatsBandDistribution
                {
                    Bins = new AvaloniaList<double>(1, 6, 15, 24, 19, 12, 8, 6, 4, 3, 2, 1.5, 1, .8, .5, .3),
                    Maximum = 160,
                    Marker = 42
                }
            },
            new StatsBandStat
            {
                Value = 28, Label = "Data regions worldwide",
                Detail = "Your data stays where you choose", Context = "Six added this year, including Nairobi",
                Visual = new StatsBandMap
                {
                    Points = new AvaloniaList<Point>(
                        new Point(-122.3, 47.6), new Point(-104.9, 39.7), new Point(-84.4, 33.7), new Point(-73.6, 45.5), new Point(-71, 42.4),
                        new Point(-90.5, 14.6), new Point(-66.9, 10.5), new Point(-78.5, -.2), new Point(-43.2, -22.9), new Point(-56.2, -34.9),
                        new Point(-9.1, 38.7), new Point(-.1, 51.5), new Point(12.5, 41.9), new Point(10, 53.6), new Point(24.9, 60.2), new Point(26.1, 44.4),
                        new Point(-17.4, 14.7), new Point(31.2, 30), new Point(36.8, -1.3), new Point(18.4, -33.9), new Point(46.7, 24.7),
                        new Point(77.2, 28.6), new Point(100.5, 13.8), new Point(121, 14.6), new Point(121.5, 31.2), new Point(135.5, 34.7), new Point(144.9, -37.8), new Point(174.8, -41.3))
                }
            }
        ];
    }
}
