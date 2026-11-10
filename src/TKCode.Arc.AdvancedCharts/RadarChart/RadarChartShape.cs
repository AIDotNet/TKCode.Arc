using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One profile on screen: vertices in unit coordinates (the outer ring has radius 1) and a closed geometry. A morph
/// only moves the vertices; the path is built from them when the profile is next drawn (once, and only if the web or
/// a vertex moved), so a frame raises no change notifications and a chart at rest builds nothing.
/// </summary>
internal sealed class RadarChartShape
{
    private readonly Point[] _points;
    private StreamGeometry? _geometry;

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
        _points = new Point[axisCount];
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

    public Geometry Geometry => _geometry ??= Build();

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
        for (var i = 0; i < _points.Length; i++)
        {
            var point = new Point(center.X + NowX[i] * radius, center.Y + NowY[i] * radius);
            if (_points[i] != point)
            {
                _points[i] = point;
                _geometry = null;
            }
        }
    }

    private StreamGeometry Build()
    {
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.SetFillRule(FillRule.EvenOdd);
            if (_points.Length > 0)
            {
                path.BeginFigure(_points[0], true);
                for (var i = 1; i < _points.Length; i++)
                {
                    path.LineTo(_points[i]);
                }
                path.EndFigure(true);
            }
        }
        return geometry;
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
