using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One profile on screen: vertices in unit coordinates (the outer ring has radius 1) and a closed geometry that is
/// updated in place, so a morph moves points and builds no geometry objects.
/// </summary>
internal sealed class RadarChartShape
{
    private readonly PathFigure _figure = new() { IsClosed = true, IsFilled = true };
    private readonly LineSegment[] _segments;

    public RadarChartShape(RadarSeries series, int index, int axisCount)
    {
        Series = series;
        Index = index;
        FromX = new double[axisCount];
        FromY = new double[axisCount];
        ToX = new double[axisCount];
        ToY = new double[axisCount];
        NowX = new double[axisCount];
        NowY = new double[axisCount];
        Values = new double[axisCount];
        _segments = new LineSegment[Math.Max(0, axisCount - 1)];
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i] = new LineSegment();
            _figure.Segments!.Add(_segments[i]);
        }
        Geometry.Figures!.Add(_figure);
    }

    public RadarSeries Series { get; }

    /// <summary>Position in series order: picks the hue and the line pattern.</summary>
    public int Index { get; }

    public double[] FromX { get; }
    public double[] FromY { get; }
    public double[] ToX { get; }
    public double[] ToY { get; }
    public double[] NowX { get; }
    public double[] NowY { get; }

    /// <summary>The raw values per axis (NaN when missing), as read for the tooltip.</summary>
    public double[] Values { get; }

    public PathGeometry Geometry { get; } = new();

    public IBrush? Fill { get; set; }
    public IPen? Pen { get; set; }
    public IPen? MarkerPen { get; set; }

    /// <summary>Moves the geometry to the current vertices for a web at <paramref name="center"/>.</summary>
    public void Sync(Point center, double radius)
    {
        if (NowX.Length == 0)
        {
            return;
        }
        _figure.StartPoint = new Point(center.X + NowX[0] * radius, center.Y + NowY[0] * radius);
        for (var i = 0; i < _segments.Length; i++)
        {
            _segments[i].Point = new Point(center.X + NowX[i + 1] * radius, center.Y + NowY[i + 1] * radius);
        }
    }

    /// <summary>Sets the vertices <paramref name="t"/> of the way from their start to their target (a spring may pass 1).</summary>
    public void Advance(double t)
    {
        for (var i = 0; i < NowX.Length; i++)
        {
            NowX[i] = ChartsBMath.Mix(FromX[i], ToX[i], t);
            NowY[i] = ChartsBMath.Mix(FromY[i], ToY[i], t);
        }
    }
}
