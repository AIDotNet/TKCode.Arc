namespace TKCode.Arc;

/// <summary>One ridge of a <see cref="Ridgeline"/>: a group of raw observations drawn as a smoothed density curve.</summary>
public sealed class RidgelineSeries
{
    /// <summary>Creates an empty series (for XAML).</summary>
    public RidgelineSeries()
    {
    }

    /// <summary>Creates a series.</summary>
    public RidgelineSeries(string id, string label, IReadOnlyList<double> values)
    {
        Id = id;
        Label = label;
        Values = values;
    }

    /// <summary>Stable identity: a ridge that survives a data change morphs into its new shape.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Row label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Raw observations (at least about 20 for a meaningful shape).</summary>
    public IReadOnlyList<double> Values { get; set; } = [];
}

/// <summary>Arguments of <see cref="Ridgeline.ActiveIdChanged"/>.</summary>
public sealed class RidgelineActiveIdChangedEventArgs : EventArgs
{
    internal RidgelineActiveIdChangedEventArgs(string? id, RidgelineSeries? series)
    {
        Id = id;
        Series = series;
    }

    /// <summary>The lifted ridge, or null when it was put down.</summary>
    public string? Id { get; }

    /// <summary>The lifted series, or null.</summary>
    public RidgelineSeries? Series { get; }
}

/// <summary>Distribution statistics of one ridge.</summary>
internal sealed class RidgelineStats
{
    public double[] Sorted = [];
    public int N;
    public double Min;
    public double Max;
    public double Q1;
    public double Median;
    public double Q3;

    public static RidgelineStats Of(IReadOnlyList<double> values)
    {
        var sorted = values.Where(double.IsFinite).OrderBy(v => v).ToArray();
        return new RidgelineStats
        {
            Sorted = sorted, N = sorted.Length,
            Min = sorted.Length > 0 ? sorted[0] : 0, Max = sorted.Length > 0 ? sorted[^1] : 0,
            Q1 = Quantile(sorted, 0.25), Median = Quantile(sorted, 0.5), Q3 = Quantile(sorted, 0.75)
        };
    }

    public static double Quantile(double[] sorted, double q)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }
        var at = (sorted.Length - 1) * q;
        var low = (int)Math.Floor(at);
        var high = (int)Math.Ceiling(at);
        return sorted[low] + (sorted[high] - sorted[low]) * (at - low);
    }

    /// <summary>Silverman's rule of thumb: 0.9 · min(sd, IQR / 1.34) · n^−0.2.</summary>
    public double Silverman()
    {
        if (N < 2)
        {
            return 1;
        }
        var mean = Sorted.Average();
        var sd = Math.Sqrt(Sorted.Sum(v => (v - mean) * (v - mean)) / (N - 1));
        var iqr = (Q3 - Q1) / 1.34;
        var spread = Math.Min(sd, iqr > 0 ? iqr : sd);
        return Math.Max(1e-6, 0.9 * (spread > 0 ? spread : 1) * Math.Pow(N, -0.2));
    }

    /// <summary>Share of observations at or below <paramref name="value"/>.</summary>
    public double FractionAtOrBelow(double value)
    {
        if (N == 0)
        {
            return 0;
        }
        var count = 0;
        foreach (var v in Sorted)
        {
            if (v <= value)
            {
                count++;
            }
            else
            {
                break;
            }
        }
        return (double)count / N;
    }

    /// <summary>Samples a Gaussian kernel density across [lo, hi] into <paramref name="ys"/>.</summary>
    public void Density(double lo, double hi, double bandwidth, double[] xs, double[] ys)
    {
        var samples = xs.Length;
        var norm = 1 / (Math.Max(1, N) * bandwidth * Math.Sqrt(2 * Math.PI));
        for (var i = 0; i < samples; i++)
        {
            var x = lo + (hi - lo) * i / (samples - 1);
            var sum = 0.0;
            foreach (var value in Sorted)
            {
                var u = (x - value) / bandwidth;
                if (u > -5 && u < 5)
                {
                    sum += Math.Exp(-0.5 * u * u);
                }
            }
            xs[i] = x;
            ys[i] = N > 0 ? sum * norm : 0;
        }
    }
}
