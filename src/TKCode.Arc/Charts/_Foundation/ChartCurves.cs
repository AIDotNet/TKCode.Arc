namespace TKCode.Arc;

/// <summary>
/// Curve sampling shared by the line charts: monotone cubic tangents (they never swing past the data), dense
/// sampling, value lookup and resampling onto shared grids so datasets of any length morph into each other.
/// All methods allocate only their result arrays and run once per data change, never per frame.
/// </summary>
internal static class ChartCurves
{
    /// <summary>Monotone cubic (Fritsch–Carlson / d3 monotoneX) tangents for evenly spaced values.</summary>
    public static void MonotoneTangents(ReadOnlySpan<double> values, double step, Span<double> tangents)
    {
        var last = values.Length - 1;
        if (last < 1)
        {
            if (last == 0)
            {
                tangents[0] = 0;
            }
            return;
        }
        Span<double> slopes = last <= 256 ? stackalloc double[last] : new double[last];
        for (var i = 0; i < last; i++)
        {
            slopes[i] = (values[i + 1] - values[i]) / step;
        }
        if (last == 1)
        {
            tangents[0] = slopes[0];
            tangents[1] = slopes[0];
            return;
        }
        tangents[0] = 0;
        tangents[last] = 0;
        for (var i = 1; i < last; i++)
        {
            var s0 = slopes[i - 1];
            var s1 = slopes[i];
            var t = (Math.Sign(s0) + Math.Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), Math.Abs(s0 + s1) / 4);
            tangents[i] = double.IsNaN(t) ? 0 : t;
        }
        tangents[0] = (3 * slopes[0] - tangents[1]) / 2;
        tangents[last] = (3 * slopes[last - 1] - tangents[last - 1]) / 2;
    }

    /// <summary>Cubic Hermite value between <paramref name="a"/> and <paramref name="b"/> at <paramref name="t"/> (0..1).</summary>
    public static double Hermite(double a, double b, double ta, double tb, double step, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * a + (t3 - 2 * t2 + t) * step * ta + (3 * t2 - 2 * t3) * b + (t3 - t2) * step * tb;
    }

    /// <summary>
    /// The line through every value on x from 0 to 1, in data units (LineChart). Smooth curves take up to 16
    /// samples per segment (<paramref name="budget"/> / segments), each clamped to its segment's range.
    /// </summary>
    public static (double[] Xs, double[] Ys) SampleLine(ReadOnlySpan<double> values, bool smooth, int budget = 192)
    {
        if (values.Length == 0)
        {
            return ([], []);
        }
        if (values.Length == 1)
        {
            return ([0, 1], [values[0], values[0]]);
        }
        var last = values.Length - 1;
        var step = 1.0 / last;
        if (!smooth)
        {
            var lx = new double[values.Length];
            var ly = new double[values.Length];
            for (var i = 0; i <= last; i++)
            {
                lx[i] = i * step;
                ly[i] = values[i];
            }
            return (lx, ly);
        }
        var tangents = new double[values.Length];
        MonotoneTangents(values, step, tangents);
        var samples = Math.Max(1, Math.Min(16, (int)Math.Round((double)budget / last)));
        var count = last * samples + 1;
        var xs = new double[count];
        var ys = new double[count];
        var k = 0;
        for (var i = 0; i < last; i++)
        {
            var low = Math.Min(values[i], values[i + 1]);
            var high = Math.Max(values[i], values[i + 1]);
            for (var s = 0; s < samples; s++)
            {
                var t = (double)s / samples;
                xs[k] = (i + t) * step;
                ys[k] = Math.Clamp(Hermite(values[i], values[i + 1], tangents[i], tangents[i + 1], step, t), low, high);
                k++;
            }
        }
        xs[k] = 1;
        ys[k] = values[last];
        return (xs, ys);
    }

    /// <summary>Value of a sampled line at <paramref name="x"/>, straight between samples.</summary>
    public static double ValueAt(ReadOnlySpan<double> xs, ReadOnlySpan<double> ys, double x)
    {
        if (xs.Length == 0)
        {
            return 0;
        }
        var low = 0;
        var high = xs.Length - 1;
        if (x <= xs[low])
        {
            return ys[low];
        }
        if (x >= xs[high])
        {
            return ys[high];
        }
        while (high - low > 1)
        {
            var middle = (low + high) >> 1;
            if (xs[middle] < x)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }
        var x0 = xs[low];
        var x1 = xs[high];
        return x1 == x0 ? ys[high] : ys[low] + (ys[high] - ys[low]) * (x - x0) / (x1 - x0);
    }

    /// <summary>Evenly spaced grid from 0 to 1 with <paramref name="count"/> points.</summary>
    public static double[] UniformGrid(int count)
    {
        var grid = new double[count];
        for (var i = 0; i < count; i++)
        {
            grid[i] = (double)i / (count - 1);
        }
        return grid;
    }

    /// <summary>Sorted union of two sorted x lists (duplicates within 1e-9 dropped), so a morph lands exactly on the target's vertices.</summary>
    public static double[] Union(ReadOnlySpan<double> a, ReadOnlySpan<double> b)
    {
        var result = new List<double>(a.Length + b.Length);
        int i = 0, j = 0;
        while (i < a.Length || j < b.Length)
        {
            double next;
            if (j >= b.Length || (i < a.Length && a[i] <= b[j]))
            {
                next = a[i++];
            }
            else
            {
                next = b[j++];
            }
            if (result.Count == 0 || next - result[^1] > 1e-9)
            {
                result.Add(next);
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Resamples raw values onto <paramref name="count"/> evenly spaced samples with monotone cubic tangents
    /// (Streamgraph's 161-point grid): never past the data, never below zero.
    /// </summary>
    public static double[] Resample(ReadOnlySpan<double> raw, int count)
    {
        var output = new double[count];
        if (raw.Length == 0)
        {
            return output;
        }
        var values = new double[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            values[i] = double.IsNaN(raw[i]) ? 0 : Math.Max(0, raw[i]);
        }
        if (values.Length == 1)
        {
            Array.Fill(output, values[0]);
            return output;
        }
        var last = values.Length - 1;
        var tangents = new double[values.Length];
        MonotoneTangents(values, 1, tangents);
        for (var k = 0; k < count; k++)
        {
            var at = (double)k / (count - 1) * last;
            var seg = Math.Min(last - 1, (int)Math.Floor(at));
            var t = at - seg;
            var a = values[seg];
            var b = values[seg + 1];
            output[k] = Math.Clamp(Hermite(a, b, tangents[seg], tangents[seg + 1], 1, t), Math.Min(a, b), Math.Max(a, b));
        }
        return output;
    }
}
