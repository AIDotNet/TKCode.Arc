using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A polyline (a check, a cross arm) that draws itself: the points, the cumulative arc length of every vertex (computed
/// once) and <see cref="Draw"/>, which paints the path cut at a progress from 0 to 1 as one line per segment (the pen's
/// round caps join them). Drawing allocates nothing.
/// </summary>
internal sealed class AiStroke
{
    private readonly Point[] _points;
    private readonly double[] _ends;

    /// <summary>A stroke through <paramref name="points"/> (at least two).</summary>
    public AiStroke(params Point[] points)
    {
        if (points.Length < 2)
        {
            throw new ArgumentException("A stroke needs at least two points.", nameof(points));
        }
        _points = points;
        _ends = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            _ends[i] = _ends[i - 1] + Distance(points[i - 1], points[i]);
        }
    }

    /// <summary>The number of vertices.</summary>
    public int Count => _points.Length;

    /// <summary>The total length.</summary>
    public double Length => _ends[^1];

    /// <summary>The end of the stroke's i-th vertex measured along the path (0 for the first vertex).</summary>
    public double LengthTo(int vertex) => _ends[vertex];

    /// <summary>The point at <paramref name="progress"/> (0 to 1, clamped) of the way along the path.</summary>
    public Point PointAt(double progress)
    {
        var drawn = Length * Math.Clamp(progress, 0, 1);
        for (var i = 1; i < _points.Length; i++)
        {
            if (drawn <= _ends[i] || i == _points.Length - 1)
            {
                var length = _ends[i] - _ends[i - 1];
                var t = length > 0 ? Math.Min(1, (drawn - _ends[i - 1]) / length) : 1;
                return _points[i - 1] + (_points[i] - _points[i - 1]) * t;
            }
        }
        return _points[0];
    }

    /// <summary>Draws the path from its start up to <paramref name="progress"/> (nothing at 0 or below, all of it from 1).</summary>
    public void Draw(DrawingContext context, IPen pen, double progress)
    {
        if (progress <= 0)
        {
            return;
        }
        var drawn = Length * Math.Min(1, progress);
        for (var i = 1; i < _points.Length && drawn > _ends[i - 1]; i++)
        {
            var length = _ends[i] - _ends[i - 1];
            var t = length > 0 ? Math.Min(1, (drawn - _ends[i - 1]) / length) : 1;
            context.DrawLine(pen, _points[i - 1], t >= 1 ? _points[i] : _points[i - 1] + (_points[i] - _points[i - 1]) * t);
        }
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
