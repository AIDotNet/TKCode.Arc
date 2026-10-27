namespace TKCode.Arc;

/// <summary>A key moment placed on the data: its value on the line and the effect measured after it.</summary>
internal sealed class LineReplayMomentInfo
{
    public LineReplayMomentInfo(LineReplayMoment moment, int index)
    {
        Moment = moment;
        Index = index;
    }

    public LineReplayMoment Moment { get; }

    /// <summary>Its position in time order.</summary>
    public int Index { get; }

    public double Time { get; set; }

    /// <summary>The line's value at the moment (interpolated).</summary>
    public double Value { get; set; }

    public bool IsIncident => Moment.Kind == LineReplayMomentKind.Incident;

    /// <summary>Minutes after the moment its effect is measured over (60, or 20 for an incident).</summary>
    public double Window { get; set; }

    /// <summary>For an incident the relative dip (−0.22); otherwise the rise in value units (+881).</summary>
    public double Effect { get; set; } = double.NaN;
}

/// <summary>
/// The data of a <see cref="LineReplay"/>, prepared once per data set: points in time order, the running peak, the
/// fixed value axis, and each moment's value and effect.
/// </summary>
internal sealed class LineReplayData
{
    /// <summary>Minutes over which a moment's rise is measured.</summary>
    public const double RiseWindow = 60;

    /// <summary>Minutes over which an incident's dip is measured.</summary>
    public const double DipWindow = 20;

    public static readonly LineReplayData Empty = new([], []);

    public LineReplayData(IReadOnlyList<LineReplayPoint> points, IReadOnlyList<LineReplayMoment> moments)
    {
        var sorted = points.Where(p => double.IsFinite(p.Time)).OrderBy(p => p.Time).ToArray();
        Count = sorted.Length;
        Times = new double[Count];
        Values = new double[Count];
        Baselines = new double[Count];
        PeakValues = new double[Count];
        PeakTimes = new double[Count];
        var peak = double.NegativeInfinity;
        var peakTime = double.NaN;
        var max = 0.0;
        for (var i = 0; i < Count; i++)
        {
            var p = sorted[i];
            Times[i] = p.Time;
            Values[i] = double.IsFinite(p.Value) ? p.Value : 0;
            Baselines[i] = p.Baseline is { } b && double.IsFinite(b) ? b : double.NaN;
            HasBaseline |= double.IsFinite(Baselines[i]);
            if (Values[i] > peak)
            {
                peak = Values[i];
                peakTime = p.Time;
            }
            PeakValues[i] = peak;
            PeakTimes[i] = peakTime;
            max = Math.Max(max, Values[i]);
            if (double.IsFinite(Baselines[i]))
            {
                max = Math.Max(max, Baselines[i]);
            }
        }
        Start = Count > 0 ? Times[0] : 0;
        End = Count > 0 ? Times[^1] : 1;
        if (End <= Start)
        {
            End = Start + 1;
        }
        (AxisStep, AxisTop) = LineChartsFormat.NiceZeroAxis(max, 4);
        MinStep = Count > 1 ? (End - Start) / (Count - 1) : 5;

        var placed = moments.Where(m => double.IsFinite(m.Time)).OrderBy(m => m.Time).ToList();
        Moments = new LineReplayMomentInfo[placed.Count];
        for (var i = 0; i < placed.Count; i++)
        {
            var info = new LineReplayMomentInfo(placed[i], i) { Time = Math.Clamp(placed[i].Time, Start, End) };
            info.Value = ValueAt(info.Time);
            info.Window = info.IsIncident ? DipWindow : RiseWindow;
            info.Effect = EffectOf(info);
            Moments[i] = info;
        }
    }

    public int Count { get; }

    public double[] Times { get; }

    public double[] Values { get; }

    /// <summary>The comparison per point (NaN where missing).</summary>
    public double[] Baselines { get; }

    public bool HasBaseline { get; }

    /// <summary>The highest value up to and including each point, and when it was reached.</summary>
    public double[] PeakValues { get; }

    public double[] PeakTimes { get; }

    public double Start { get; }

    public double End { get; }

    /// <summary>The value axis, fixed to the whole series so the line never rescales while it plays.</summary>
    public double AxisTop { get; }

    public double AxisStep { get; }

    /// <summary>The average step between points (one keyboard step on the scrubber).</summary>
    public double MinStep { get; }

    public LineReplayMomentInfo[] Moments { get; }

    /// <summary>The index of the last point at or before <paramref name="time"/> (−1 before the first).</summary>
    public int IndexAtOrBefore(double time)
    {
        var index = Array.BinarySearch(Times, time);
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>The value at <paramref name="time"/>, linear between points.</summary>
    public double ValueAt(double time) => Interpolate(Values, time);

    /// <summary>The comparison at <paramref name="time"/> (NaN where missing).</summary>
    public double BaselineAt(double time) => Interpolate(Baselines, time);

    private double Interpolate(double[] series, double time)
    {
        if (Count == 0)
        {
            return double.NaN;
        }
        var i = IndexAtOrBefore(time);
        if (i < 0)
        {
            return series[0];
        }
        if (i >= Count - 1)
        {
            return series[^1];
        }
        var span = Times[i + 1] - Times[i];
        var t = span > 0 ? (time - Times[i]) / span : 0;
        return series[i] + (series[i + 1] - series[i]) * t;
    }

    /// <summary>The highest value reached by <paramref name="time"/> and when (NaN before the first point).</summary>
    public (double Value, double Time) PeakAt(double time)
    {
        var i = IndexAtOrBefore(time);
        if (i < 0)
        {
            return (double.NaN, double.NaN);
        }
        var (value, at) = (PeakValues[i], PeakTimes[i]);
        // Between points the line's own value may already be above the last point's peak.
        var current = ValueAt(time);
        return current > value ? (current, time) : (value, at);
    }

    private double EffectOf(LineReplayMomentInfo info)
    {
        var start = info.Value;
        var end = info.Time + info.Window;
        var extreme = start;
        var from = IndexAtOrBefore(info.Time) + 1;
        for (var i = Math.Max(0, from); i < Count && Times[i] <= end + 1e-9; i++)
        {
            extreme = info.IsIncident ? Math.Min(extreme, Values[i]) : Math.Max(extreme, Values[i]);
        }
        if (info.IsIncident)
        {
            return start != 0 ? (extreme - start) / Math.Abs(start) : double.NaN;
        }
        return extreme - start;
    }
}
