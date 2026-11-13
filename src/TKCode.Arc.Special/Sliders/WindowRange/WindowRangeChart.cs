using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The series behind a <see cref="WindowRange"/>: a smooth line through evenly spread points and the area under it.</summary>
internal static class WindowRangeChart
{
    /// <summary>The largest value sits this share of the chart height below its top.</summary>
    public const double Headroom = 0.11;

    /// <summary>The value at the top of the chart for <paramref name="values"/> (the axis starts at 0).</summary>
    public static double TopOf(ReadOnlySpan<double> values)
    {
        var max = 0.0;
        foreach (var value in values)
        {
            if (double.IsFinite(value))
            {
                max = Math.Max(max, value);
            }
        }
        return max > 0 ? max / (1 - Headroom) : 1;
    }

    /// <summary>The y of <paramref name="value"/> in a chart from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
    public static double YOf(double value, double scaleTop, double top, double bottom)
    {
        var share = double.IsFinite(value) ? Math.Clamp(value / scaleTop, 0, 1) : 0;
        return bottom - share * (bottom - top);
    }

    /// <summary>
    /// Builds the line and the area for <paramref name="values"/> spread from <paramref name="left"/> to
    /// <paramref name="right"/>. The curve is monotone between points (no overshoot past a peak or below zero), one cubic per
    /// segment.
    /// </summary>
    public static (StreamGeometry Line, StreamGeometry Area) Build(ReadOnlySpan<double> values, double scaleTop, double left,
        double right, double top, double bottom)
    {
        var line = new StreamGeometry();
        var area = new StreamGeometry();
        var n = values.Length;
        if (n == 0)
        {
            return (line, area);
        }
        Span<double> ys = n <= 1024 ? stackalloc double[n] : new double[n];
        Span<double> tangents = n <= 1024 ? stackalloc double[n] : new double[n];
        for (var i = 0; i < n; i++)
        {
            ys[i] = YOf(values[i], scaleTop, top, bottom);
        }
        var h = n > 1 ? (right - left) / (n - 1) : 0;
        Tangents(ys, h, tangents);
        using (var context = line.Open())
        {
            Trace(context, ys, tangents, left, h, close: false, bottom);
        }
        using (var context = area.Open())
        {
            Trace(context, ys, tangents, left, h, close: true, bottom);
        }
        return (line, area);
    }

    private static void Trace(StreamGeometryContext context, ReadOnlySpan<double> ys, ReadOnlySpan<double> tangents, double left,
        double h, bool close, double bottom)
    {
        var n = ys.Length;
        context.BeginFigure(new Point(left, ys[0]), close);
        if (n == 1)
        {
            context.LineTo(new Point(left, ys[0]));
        }
        for (var i = 0; i < n - 1; i++)
        {
            var x0 = left + i * h;
            var x1 = x0 + h;
            var d = h / 3;
            context.CubicBezierTo(new Point(x0 + d, ys[i] + d * tangents[i]), new Point(x1 - d, ys[i + 1] - d * tangents[i + 1]),
                new Point(x1, ys[i + 1]));
        }
        if (close)
        {
            context.LineTo(new Point(left + (n - 1) * h, bottom));
            context.LineTo(new Point(left, bottom));
        }
        context.EndFigure(close);
    }

    /// <summary>Monotone tangents (Fritsch–Carlson, as in a monotone-x curve) for evenly spaced points.</summary>
    internal static void Tangents(ReadOnlySpan<double> ys, double h, Span<double> tangents)
    {
        var n = ys.Length;
        if (n < 2 || h <= 0)
        {
            tangents.Clear();
            return;
        }
        for (var i = 1; i < n - 1; i++)
        {
            var s0 = (ys[i] - ys[i - 1]) / h;
            var s1 = (ys[i + 1] - ys[i]) / h;
            var p = (s0 + s1) / 2;
            tangents[i] = (Math.Sign(s0) + Math.Sign(s1)) * Math.Min(Math.Min(Math.Abs(s0), Math.Abs(s1)), 0.5 * Math.Abs(p));
        }
        if (n == 2)
        {
            var s = (ys[1] - ys[0]) / h;
            tangents[0] = s;
            tangents[1] = s;
            return;
        }
        tangents[0] = (3 * (ys[1] - ys[0]) / h - tangents[1]) / 2;
        tangents[n - 1] = (3 * (ys[n - 1] - ys[n - 2]) / h - tangents[n - 2]) / 2;
    }
}
