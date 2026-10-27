using Avalonia;

namespace TKCode.Arc;

/// <summary>Which width class of the block's own container the plot lays out for (measured: 900 and 560).</summary>
internal enum HorizonTier
{
    /// <summary>Wider than 900.</summary>
    Wide,

    /// <summary>561–900: narrower name and value columns.</summary>
    Medium,

    /// <summary>560 and below: smaller rows, 16px sides, 12px text.</summary>
    Compact
}

/// <summary>Row geometry of one width class.</summary>
internal readonly record struct HorizonMetrics(double RowHeight, double BandInset, double SidePadding, double NameWidth, double ValueWidth,
    double ColumnGap, double FontSize);

/// <summary>The plot's resolved layout for a width: every x the rows, the crosshair and the axis share.</summary>
internal readonly struct HorizonLayout
{
    public HorizonLayout(double width, HorizonTier tier, HorizonMetrics metrics, double topPadding, double axisHeight, double bottomPadding)
    {
        Width = width;
        Tier = tier;
        Metrics = metrics;
        TopPadding = topPadding;
        AxisHeight = axisHeight;
        BottomPadding = bottomPadding;
    }

    public double Width { get; }

    public HorizonTier Tier { get; }

    public HorizonMetrics Metrics { get; }

    public double TopPadding { get; }

    public double AxisHeight { get; }

    public double BottomPadding { get; }

    /// <summary>Left edge of the band column (side padding + name column + gap).</summary>
    public double PlotLeft => Metrics.SidePadding + Metrics.NameWidth + Metrics.ColumnGap;

    /// <summary>Right edge of the band column.</summary>
    public double PlotRight => Math.Max(PlotLeft, Width - Metrics.SidePadding - Metrics.ValueWidth - Metrics.ColumnGap);

    public double PlotWidth => PlotRight - PlotLeft;

    /// <summary>Right edge of the value column (right-aligned numbers).</summary>
    public double ValueRight => Width - Metrics.SidePadding;

    public double ValueLeft => ValueRight - Metrics.ValueWidth;

    /// <summary>Top of row <paramref name="slot"/>.</summary>
    public double RowTop(int slot) => TopPadding + slot * Metrics.RowHeight;

    /// <summary>Top of the axis strip under <paramref name="rows"/> rows.</summary>
    public double AxisTop(int rows) => TopPadding + rows * Metrics.RowHeight;

    /// <summary>Whole plot height for <paramref name="rows"/> rows.</summary>
    public double TotalHeight(int rows) => AxisTop(rows) + AxisHeight + BottomPadding;

    /// <summary>The x of day <paramref name="index"/> of <paramref name="count"/> (0 → PlotLeft, last → PlotRight).</summary>
    public double DayX(double index, int count) => count <= 1 ? PlotLeft : PlotLeft + index / (count - 1) * PlotWidth;
}

/// <summary>
/// The pure model of the horizon chart, measured from the design reference: the band fold (one unit-box polygon per row
/// side, drawn four times with <c>y' = B·y + k</c> and fill alpha <c>α = 1 − 0.08^(1/B)</c>, so k layers of one side
/// stack to <c>1 − (1 − α)^k</c> and B layers always reach 0.92), the automatic extent, the three sort orders, the
/// crosshair's day from an x, the width classes of the rows and the legend's cell widths.
/// </summary>
internal static class HorizonEngine
{
    /// <summary>The most bands one row folds into (layers drawn per side).</summary>
    public const int MaxBands = 4;

    /// <summary>The alpha every row reaches with all of its bands stacked.</summary>
    public const double FullAlpha = 0.92;

    /// <summary>The extent step of the automatic extent (36 for values up to 36).</summary>
    public const double ExtentStep = 12;

    /// <summary>Container widths at which the plot turns Medium (900) and Compact (560).</summary>
    public const double MediumWidth = 900;

    public const double CompactWidth = 560;

    public static readonly HorizonMetrics Wide = new(36, 4, 24, 132, 60, 20, 14);

    public static readonly HorizonMetrics Medium = new(36, 4, 24, 116, 52, 16, 14);

    public static readonly HorizonMetrics Compact = new(34, 4, 16, 84, 44, 10, 12);

    /// <summary>Alpha of one layer at <paramref name="bands"/> (a continuous value while the fold runs).</summary>
    public static double LayerAlpha(double bands) => AnalyticsColors.HorizonLayerAlpha(Math.Clamp(bands, 1e-6, MaxBands));

    /// <summary>Alpha after <paramref name="layers"/> layers of one side stack.</summary>
    public static double StackedAlpha(int layers, double bands) => AnalyticsColors.HorizonStackedAlpha(layers, bands);

    /// <summary>The alpha as 8-bit steps, so a fold in flight caches at most 256 brushes a colour.</summary>
    public static double QuantizeAlpha(double alpha) => Math.Round(Math.Clamp(alpha, 0, 1) * 255) / 255;

    /// <summary>Layer <paramref name="layer"/> (1–4) shows nothing while <c>layer − 1 ≥ bands</c> (it would need a value above the extent).</summary>
    public static bool IsLayerVisible(int layer, double bands) => layer - 1 < bands;

    /// <summary>
    /// The fraction of the band's height layer <paramref name="layer"/> fills at normalized value <paramref name="v"/> (0–1):
    /// <c>clamp(B·v − (k − 1), 0, 1)</c>, measured from the band's bottom edge.
    /// </summary>
    public static double LayerFill(double v, double bands, int layer) => Math.Clamp(bands * v - (layer - 1), 0, 1);

    /// <summary>The automatic extent: the largest absolute value rounded up to a multiple of 12 (36 for a peak of 35), at least 12.</summary>
    public static double AutoExtent(double maxAbs)
    {
        if (double.IsNaN(maxAbs) || maxAbs <= 0)
        {
            return ExtentStep;
        }
        return Math.Max(ExtentStep, Math.Ceiling(maxAbs / ExtentStep - 1e-9) * ExtentStep);
    }

    /// <summary>The largest absolute value over every row (NaN skipped).</summary>
    public static double MaxAbs(IReadOnlyList<double[]> rows)
    {
        var max = 0.0;
        foreach (var row in rows)
        {
            foreach (var v in row)
            {
                if (!double.IsNaN(v) && Math.Abs(v) > max)
                {
                    max = Math.Abs(v);
                }
            }
        }
        return max;
    }

    /// <summary>A value as a 0–1 fraction of the extent on its side: <c>max(v, 0)</c> when <paramref name="upper"/>, else <c>−min(v, 0)</c>.</summary>
    public static double Normalize(double value, double extent, bool upper)
    {
        if (double.IsNaN(value) || extent <= 0)
        {
            return 0;
        }
        var side = upper ? Math.Max(value, 0) : Math.Max(-value, 0);
        return Math.Clamp(side / extent, 0, 1);
    }

    /// <summary>
    /// The matrix of layer <paramref name="layer"/> for a unit-box polygon (x 0…n−1, y −v…0): x to the plot's width,
    /// <c>y' = B·y + k</c> to the plot's height, so the polygon's baseline sits at <c>plotY + k·h</c> and layer k only shows
    /// where <c>B·v</c> exceeds <c>k − 1</c>. The caller clips to the plot rectangle.
    /// </summary>
    public static Matrix LayerMatrix(double plotX, double plotY, double width, double height, int days, double bands, int layer)
    {
        var sx = days <= 1 ? width : width / (days - 1);
        return new Matrix(sx, 0, 0, height * bands, plotX, plotY + height * layer);
    }

    /// <summary>The unit-box points of one side of a row: (0,0), (i, −v_i), (n−1, 0). Fills <paramref name="destination"/> (n + 2 points).</summary>
    public static void UnitPolygon(IReadOnlyList<double> values, double extent, bool upper, Span<Point> destination)
    {
        var n = values.Count;
        destination[0] = new Point(0, 0);
        for (var i = 0; i < n; i++)
        {
            destination[i + 1] = new Point(i, -Normalize(values[i], extent, upper));
        }
        destination[n + 1] = new Point(Math.Max(0, n - 1), 0);
    }

    /// <summary>The width class for a container width.</summary>
    public static HorizonTier TierFor(double width, double mediumWidth = MediumWidth, double compactWidth = CompactWidth)
    {
        if (width <= compactWidth)
        {
            return HorizonTier.Compact;
        }
        return width <= mediumWidth ? HorizonTier.Medium : HorizonTier.Wide;
    }

    /// <summary>Resolves the layout of a plot <paramref name="width"/> wide.</summary>
    public static HorizonLayout Resolve(double width)
        => Resolve(width, Wide, Medium, Compact, MediumWidth, CompactWidth);

    /// <summary>Resolves the layout from the three metric sets (the token's values).</summary>
    public static HorizonLayout Resolve(double width, HorizonMetrics wide, HorizonMetrics medium, HorizonMetrics compact,
        double mediumWidth, double compactWidth)
    {
        var tier = TierFor(width, mediumWidth, compactWidth);
        var metrics = tier switch
        {
            HorizonTier.Compact => compact,
            HorizonTier.Medium => medium,
            _ => wide
        };
        return new HorizonLayout(width, tier, metrics, 8, 26, 16);
    }

    /// <summary>
    /// The day under x: <c>round((x − left) / width · (n − 1))</c> clamped to 0…n−1 (x 500 over a 1152-wide block's
    /// 177–1047 columns → day 36 of 119).
    /// </summary>
    public static int DayAt(double x, double left, double width, int days)
    {
        if (days <= 0)
        {
            return -1;
        }
        if (days == 1 || width <= 0)
        {
            return 0;
        }
        var index = (int)Math.Round((x - left) / width * (days - 1), MidpointRounding.AwayFromZero);
        return Math.Clamp(index, 0, days - 1);
    }

    /// <summary>The ordering of the rows (indexes into the input) for a sort; ties keep the input order.</summary>
    public static int[] Order(IReadOnlyList<string> names, IReadOnlyList<double[]> values, HorizonSort sort)
    {
        var order = new int[names.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        var keys = new double[order.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = sort switch
            {
                HorizonSort.Peak => PeakOf(values[i]),
                HorizonSort.Latest => LatestOf(values[i]),
                _ => 0
            };
        }
        Comparison<int> compare = sort switch
        {
            HorizonSort.Name => (a, b) =>
            {
                var c = string.Compare(names[a], names[b], StringComparison.CurrentCultureIgnoreCase);
                return c != 0 ? c : a.CompareTo(b);
            },
            _ => (a, b) =>
            {
                var c = keys[b].CompareTo(keys[a]);
                return c != 0 ? c : a.CompareTo(b);
            }
        };
        Array.Sort(order, compare);
        return order;
    }

    /// <summary>The largest absolute value of a row.</summary>
    public static double PeakOf(double[] row)
    {
        var max = 0.0;
        foreach (var v in row)
        {
            if (!double.IsNaN(v) && Math.Abs(v) > max)
            {
                max = Math.Abs(v);
            }
        }
        return max;
    }

    /// <summary>A row's last value (0 for an empty row; NaN counts as 0).</summary>
    public static double LatestOf(double[] row) => row.Length == 0 || double.IsNaN(row[^1]) ? 0 : row[^1];

    /// <summary>The highest and the lowest row on a day (indexes into the input; −1 when there are no rows).</summary>
    public static (int Highest, int Lowest) Extremes(IReadOnlyList<double[]> values, int day)
    {
        var high = -1;
        var low = -1;
        for (var i = 0; i < values.Count; i++)
        {
            if (day < 0 || day >= values[i].Length || double.IsNaN(values[i][day]))
            {
                continue;
            }
            if (high < 0 || values[i][day] > values[high][day])
            {
                high = i;
            }
            if (low < 0 || values[i][day] < values[low][day])
            {
                low = i;
            }
        }
        return (high, low);
    }

    /// <summary>The crosshair's next day for a key (±1, Shift ±7, Home / End), or null when the key does not move it. Clamps, never wraps.</summary>
    public static int? KeyDay(Avalonia.Input.Key key, bool shift, int current, int days)
    {
        if (days <= 0)
        {
            return null;
        }
        var step = shift ? 7 : 1;
        switch (key)
        {
            case Avalonia.Input.Key.Home:
                return 0;
            case Avalonia.Input.Key.End:
                return days - 1;
            case Avalonia.Input.Key.Left:
            case Avalonia.Input.Key.Right:
                var from = current < 0 ? days - 1 : current;
                return Math.Clamp(from + (key == Avalonia.Input.Key.Left ? -step : step), 0, days - 1);
            default:
                return null;
        }
    }

    /// <summary>The legend's grow factor of layer <paramref name="layer"/> at <paramref name="bands"/> (1 shown, 0 folded away).</summary>
    public static double LegendGrow(int layer, int bands) => layer <= bands ? 1 : 0;

    /// <summary>The legend cell's opacity target of layer <paramref name="layer"/> at <paramref name="bands"/>.</summary>
    public static double LegendOpacity(int layer, int bands) => layer <= bands ? StackedAlpha(layer, bands) : 0;

    /// <summary>
    /// Widths of the legend's cells (4 below, 4 above, in drawing order: below outer→inner, above inner→outer) for the
    /// current grow values: <c>(width − zeroGap) · grow / Σ grow</c> (48.8 each for 3 bands over 300).
    /// </summary>
    public static void LegendWidths(double width, ReadOnlySpan<double> grows, Span<double> destination, double zeroGap = 7)
    {
        var sum = 0.0;
        foreach (var g in grows)
        {
            sum += g;
        }
        var room = Math.Max(0, width - zeroGap);
        for (var i = 0; i < grows.Length && i < destination.Length; i++)
        {
            destination[i] = sum <= 0 ? 0 : room * grows[i] / sum;
        }
    }

    /// <summary>The legend's three label columns: the end labels take their text widths, "below" and "above" share the rest.</summary>
    public static (double Left, double Below, double Zero, double Above, double Right) LegendColumns(double width, double leftText, double zeroText,
        double rightText)
    {
        var rest = Math.Max(0, width - leftText - zeroText - rightText);
        return (leftText, rest / 2, zeroText, rest / 2, rightText);
    }

    /// <summary>The month ticks of a day list: the first day and every first of a month (label = abbreviated month).</summary>
    public static List<HorizonTick> MonthTicks(IReadOnlyList<HorizonDay> days)
    {
        var ticks = new List<HorizonTick>();
        for (var i = 0; i < days.Count; i++)
        {
            if (days[i].Date is not { } date)
            {
                continue;
            }
            if (ticks.Count == 0 && i == 0 || date.Day == 1)
            {
                ticks.Add(new HorizonTick(i, date.ToString("MMM", AnalyticsFormat.DefaultCulture)));
            }
        }
        return ticks;
    }
}
