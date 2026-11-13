using System.Globalization;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// What a <see cref="WindRoseChart"/> draws, computed from the amounts once per data change: displayed values (percent or
/// as given), the stacked top of every segment, the sector totals and the radial axis. Pure, so the arithmetic is tested
/// without a window.
/// </summary>
internal sealed class WindRoseModel
{
    /// <summary>The 32 compass points clockwise from north, as the English abbreviations data and the catalog use.</summary>
    internal static readonly string[] CompassNames =
    [
        "N", "NbE", "NNE", "NEbN", "NE", "NEbE", "ENE", "EbN", "E", "EbS", "ESE", "SEbE", "SE", "SEbS", "SSE", "SbE",
        "S", "SbW", "SSW", "SWbS", "SW", "SWbW", "WSW", "WbS", "W", "WbN", "WNW", "NWbW", "NW", "NWbN", "NNW", "NbW"
    ];

    private static readonly double[] s_mantissas = [1, 2, 2.5, 5];

    /// <summary>Most rings an automatic step gives.</summary>
    internal const int MaxAutoRings = 5;

    private WindRoseModel(int sectors, int bins)
    {
        SectorCount = sectors;
        BinCount = bins;
        Raw = new double[sectors * bins];
        Display = new double[sectors * bins];
        Top = new double[sectors * bins];
        SectorTotals = new double[sectors];
        SectorStacks = new double[sectors];
    }

    public int SectorCount { get; }
    public int BinCount { get; }

    /// <summary>Amounts as given, at <c>sector * BinCount + bin</c>.</summary>
    public double[] Raw { get; }

    /// <summary>Amounts on the radial axis (percent of <see cref="Total"/> or as given).</summary>
    public double[] Display { get; }

    /// <summary>Displayed stacked top of each segment: the sum of its bin and the bins inside it.</summary>
    public double[] Top { get; }

    public double[] SectorTotals { get; }
    public double[] SectorStacks { get; }

    public double Calm { get; private set; }
    public double CalmDisplay { get; private set; }

    /// <summary>All amounts plus calm: the 100 % of the percent axis.</summary>
    public double Total { get; private set; }

    /// <summary>Total of each bin over all sectors.</summary>
    public double[] BinTotals { get; private set; } = [];

    public double MaxStack { get; private set; }
    public double Maximum { get; private set; } = 1;
    public double Step { get; private set; } = 1;
    public bool IsPercent { get; private set; }

    /// <summary>Nothing to draw: no positive amount and no calm.</summary>
    public bool IsEmpty => Total <= 0;

    public static WindRoseModel Build(int sectors, int bins, double[] raw, double calm, bool percent, double fixedMaximum, double fixedStep)
    {
        var model = new WindRoseModel(sectors, bins);
        var sum = 0.0;
        model.BinTotals = new double[bins];
        for (var i = 0; i < raw.Length && i < model.Raw.Length; i++)
        {
            var value = Clean(raw[i]);
            model.Raw[i] = value;
            sum += value;
            model.BinTotals[i % bins] += value;
        }
        model.Calm = Clean(calm);
        model.Total = sum + model.Calm;
        model.IsPercent = percent;
        var scale = percent ? (model.Total > 0 ? 100 / model.Total : 0) : 1;
        model.CalmDisplay = model.Calm * scale;
        var maxStack = 0.0;
        for (var s = 0; s < sectors; s++)
        {
            var top = 0.0;
            var total = 0.0;
            for (var b = 0; b < bins; b++)
            {
                var at = s * bins + b;
                var shown = model.Raw[at] * scale;
                model.Display[at] = shown;
                total += model.Raw[at];
                top += shown;
                model.Top[at] = top;
            }
            model.SectorTotals[s] = total;
            model.SectorStacks[s] = top;
            maxStack = Math.Max(maxStack, top);
        }
        model.MaxStack = maxStack;
        (model.Maximum, model.Step) = Axis(maxStack, fixedMaximum, fixedStep, !percent);
        return model;
    }

    private static double Clean(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    /// <summary>
    /// The value at the outer ring and the distance between rings. A fixed maximum stays as given (its step defaults to
    /// at most <see cref="MaxAutoRings"/> rings); otherwise the maximum is the first multiple of the step at or above the
    /// longest petal. Counts never get a fractional step.
    /// </summary>
    public static (double Maximum, double Step) Axis(double maxStack, double fixedMaximum, double fixedStep, bool integers)
    {
        var hasStep = double.IsFinite(fixedStep) && fixedStep > 0;
        if (double.IsFinite(fixedMaximum) && fixedMaximum > 0)
        {
            var step = hasStep ? fixedStep : AutoStep(fixedMaximum, integers);
            if (fixedMaximum / step > 50)
            {
                step = AutoStep(fixedMaximum, integers);
            }
            return (fixedMaximum, step);
        }
        var span = maxStack > 0 && double.IsFinite(maxStack) ? maxStack : 1;
        var auto = hasStep ? fixedStep : AutoStep(span, integers);
        if (span / auto > 50)
        {
            auto = AutoStep(span, integers);
        }
        var maximum = Math.Ceiling(span / auto - 1e-9) * auto;
        return (Math.Max(auto, maximum), auto);
    }

    /// <summary>The smallest of 1, 2, 2.5 and 5 × 10ⁿ that covers <paramref name="span"/> in at most <see cref="MaxAutoRings"/> rings.</summary>
    public static double AutoStep(double span, bool integers)
    {
        if (!(span > 0) || !double.IsFinite(span))
        {
            return 1;
        }
        var exponent = (int)Math.Floor(Math.Log10(span / MaxAutoRings)) - 1;
        for (var e = exponent; e < exponent + 4; e++)
        {
            var power = Math.Pow(10, e);
            foreach (var mantissa in s_mantissas)
            {
                var step = mantissa * power;
                if (integers && (step < 1 || Math.Abs(step - Math.Round(step)) > 1e-9))
                {
                    continue;
                }
                if (Math.Ceiling(span / step - 1e-9) <= MaxAutoRings)
                {
                    return step;
                }
            }
        }
        return Math.Pow(10, exponent + 4);
    }

    // ---- directions ---------------------------------------------------------------------------------------------

    /// <summary>Whether the default labels of <paramref name="count"/> sectors are compass points (4, 8, 16 or 32 sectors).</summary>
    public static bool HasCompassLabels(int count) => count is 4 or 8 or 16 or 32;

    /// <summary>The default label of a sector: its compass point in the current language, or its degrees.</summary>
    public static string DefaultLabel(int index, int count)
    {
        if (HasCompassLabels(count))
        {
            return WindRoseChartLangResources.Get((WindRoseChartLangResourceKind)(index * (32 / count)));
        }
        return Degrees(index * 360.0 / count) + "°";
    }

    public static string Degrees(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>
    /// The sector a direction falls in, or -1: a number is degrees; a string is a number (an optional trailing °), one of
    /// <paramref name="labels"/>, a compass abbreviation such as "NNE" or "NbE", or a compass point in the current language.
    /// </summary>
    public static int SectorOf(object? direction, int count, IReadOnlyList<string> labels)
    {
        switch (direction)
        {
            case null:
                return -1;
            case double d:
                return WindRoseBinning.SectorOf(d, count);
            case float f:
                return WindRoseBinning.SectorOf(f, count);
            case int i:
                return WindRoseBinning.SectorOf(i, count);
            case long l:
                return WindRoseBinning.SectorOf(l, count);
            case decimal m:
                return WindRoseBinning.SectorOf((double)m, count);
            case string s:
                return SectorOfText(s, count, labels);
            case IConvertible convertible:
                try
                {
                    return WindRoseBinning.SectorOf(convertible.ToDouble(CultureInfo.InvariantCulture), count);
                }
                catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
                {
                    return SectorOfText(convertible.ToString(CultureInfo.InvariantCulture), count, labels);
                }
            default:
                return SectorOfText(direction.ToString() ?? string.Empty, count, labels);
        }
    }

    private static int SectorOfText(string text, int count, IReadOnlyList<string> labels)
    {
        var s = text.Trim();
        if (s.Length == 0)
        {
            return -1;
        }
        var number = s.EndsWith('°') ? s[..^1].Trim() : s;
        if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees))
        {
            return WindRoseBinning.SectorOf(degrees, count);
        }
        for (var i = 0; i < labels.Count && i < count; i++)
        {
            if (string.Equals(labels[i], s, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        for (var k = 0; k < CompassNames.Length; k++)
        {
            if (string.Equals(CompassNames[k], s, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(WindRoseChartLangResources.Get((WindRoseChartLangResourceKind)k), s, StringComparison.OrdinalIgnoreCase))
            {
                return WindRoseBinning.SectorOf(k * 11.25, count);
            }
        }
        return -1;
    }

    /// <summary>A bin index from a number or a numeric string, or -1.</summary>
    public static int BinOf(object? value) => value switch
    {
        null => -1,
        int i => i,
        long l => l is >= 0 and <= int.MaxValue ? (int)l : -1,
        string s => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1,
        _ => ChartsBMemberReader.ToDouble(value) is var d && double.IsFinite(d) && Math.Abs(d - Math.Round(d)) < 1e-9 && d >= 0 && d <= int.MaxValue
            ? (int)Math.Round(d)
            : -1
    };
}
