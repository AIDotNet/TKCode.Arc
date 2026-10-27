namespace TKCode.Arc;

/// <summary>
/// A metric's daily series with everything the parts need to write it: values, the day each ends on, value, axis and change
/// texts. Built by the block from an <see cref="ExplorerMetric"/> whenever it changes; the parts never read the metric itself.
/// </summary>
internal sealed class MetricExplorerSeries
{
    public MetricExplorerSeries(ExplorerMetric source)
    {
        Source = source;
        Values = source.Values is { Count: > 0 } v ? v.ToArray() : [];
        End = source.EffectiveEndDate;
        Format = source.ValueFormat;
        Symbol = source.CurrencySymbol;
        Formatter = source.ValueFormatter;
        AxisFormatter = source.AxisFormatter;
        ChangeMode = source.ChangeMode;
        IsHigherBetter = source.IsHigherBetter;
        MaxAxisDecimals = source.MaxAxisDecimals;
        Title = source.Title;
        TabTitle = source.TabTitle;
        Description = source.Description;
    }

    public ExplorerMetric Source { get; }

    public double[] Values { get; }

    public DateTime End { get; }

    public ExplorerValueFormat Format { get; }

    public string Symbol { get; }

    public Func<double, string>? Formatter { get; }

    public Func<double, double, string>? AxisFormatter { get; }

    public ExplorerChangeMode ChangeMode { get; }

    public bool IsHigherBetter { get; }

    public int MaxAxisDecimals { get; }

    public string Title { get; }

    public string TabTitle { get; }

    public string? Description { get; }

    public int Count => Values.Length;

    /// <summary>The last value (today's).</summary>
    public double Latest => Values.Length > 0 ? Values[^1] : double.NaN;

    /// <summary>The value <paramref name="daysBack"/> days before the last one (NaN outside the series).</summary>
    public double At(double daysBack) => MetricExplorerMath.At(Values, daysBack);

    /// <summary>The day <paramref name="daysBack"/> days before the last one.</summary>
    public DateTime DateAt(int daysBack) => End.AddDays(-daysBack);

    /// <summary>A value as the cards, the tab and the readout write it.</summary>
    public string ValueText(double value)
    {
        if (!double.IsFinite(value))
        {
            return AnalyticsFormat.Missing;
        }
        return Formatter?.Invoke(value) ?? MetricExplorerMath.ValueText(Format, Symbol, value);
    }

    /// <summary>The axis for [min, max] and the unit its labels divide by.</summary>
    public (AnalyticsTickSet Ticks, double Unit) Axis(double min, double max)
    {
        var ticks = MetricExplorerMath.MetricAxis(Format, MaxAxisDecimals, min, max);
        var unit = Format == ExplorerValueFormat.CurrencyCompact ? MetricExplorerMath.CompactUnit(Math.Max(Math.Abs(min), Math.Abs(max))) : 1;
        return (ticks, unit);
    }

    /// <summary>A y label.</summary>
    public string AxisText(double value, double step, double unit) =>
        AxisFormatter?.Invoke(value, step) ?? MetricExplorerMath.AxisLabel(Format, Symbol, MaxAxisDecimals, value, step, unit);

    /// <summary>The change of <paramref name="now"/> against <paramref name="before"/> in the metric's mode (percent or points); NaN when unknown.</summary>
    public double Change(double now, double before) => MetricExplorerMath.Change(ChangeMode, now, before);

    /// <summary>The change as text ("+1.6%", "−1.1 pts"); a dash when unknown.</summary>
    public string ChangeText(double change) =>
        double.IsFinite(change) ? MetricExplorerMath.ChangeFormat(ChangeMode).Format(change) : AnalyticsFormat.Missing;

    /// <summary>Direction and tone of a change.</summary>
    public (AnalyticsDirection Direction, AnalyticsTone Tone) Trend(double change) => MetricExplorerMath.Trend(change, IsHigherBetter);
}
