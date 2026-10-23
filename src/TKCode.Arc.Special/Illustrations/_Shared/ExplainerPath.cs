using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A path an explainer draws and moves things along: lines, cubic curves and circular arcs flattened into a polyline with
/// an arc-length table, so a head, a packet or a trail moves at an even speed and a partial stroke ("draw in", "retract")
/// can be shown with a dash on one cached geometry. Build it once when the layout changes; reading it allocates nothing.
/// </summary>
internal sealed class ExplainerPath
{
    private readonly List<Point> _points = new();
    private readonly List<double> _lengths = new();
    private StreamGeometry? _geometry;

    /// <summary>The total length in device-independent pixels.</summary>
    public double Length => _lengths.Count == 0 ? 0 : _lengths[^1];

    /// <summary>The flattened points, first to last.</summary>
    public IReadOnlyList<Point> Points => _points;

    /// <summary>The start point (origin when empty).</summary>
    public Point Start => _points.Count == 0 ? default : _points[0];

    /// <summary>The end point (origin when empty).</summary>
    public Point End => _points.Count == 0 ? default : _points[^1];

    /// <summary>Forgets every point; the path can be rebuilt with <see cref="MoveTo"/>.</summary>
    public ExplainerPath Clear()
    {
        _points.Clear();
        _lengths.Clear();
        _geometry = null;
        return this;
    }

    /// <summary>Starts the path at <paramref name="point"/> (only valid on an empty path).</summary>
    public ExplainerPath MoveTo(Point point)
    {
        if (_points.Count > 0)
        {
            throw new InvalidOperationException("MoveTo starts a path; call Clear first.");
        }
        Add(point);
        return this;
    }

    /// <summary>A straight segment to <paramref name="point"/>.</summary>
    public ExplainerPath LineTo(Point point)
    {
        Add(point);
        return this;
    }

    /// <summary>A cubic Bézier segment from the current point, flattened into <paramref name="samples"/> pieces.</summary>
    public ExplainerPath CubicTo(Point control1, Point control2, Point end, int samples = 32)
    {
        var start = End;
        for (var i = 1; i <= samples; i++)
        {
            var t = i / (double)samples;
            var u = 1 - t;
            var x = u * u * u * start.X + 3 * u * u * t * control1.X + 3 * u * t * t * control2.X + t * t * t * end.X;
            var y = u * u * u * start.Y + 3 * u * u * t * control1.Y + 3 * u * t * t * control2.Y + t * t * t * end.Y;
            Add(new Point(x, y));
        }
        return this;
    }

    /// <summary>
    /// A circular arc around <paramref name="center"/> from <paramref name="startDegrees"/> sweeping
    /// <paramref name="sweepDegrees"/> (0° = 12 o'clock, positive = clockwise, as on a clock face). On an empty path it
    /// starts at the arc's first point; otherwise it joins the current point to it with a straight segment.
    /// </summary>
    public ExplainerPath ArcTo(Point center, double radius, double startDegrees, double sweepDegrees, double degreesPerPiece = 2)
    {
        var pieces = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepDegrees) / Math.Max(0.25, degreesPerPiece)));
        for (var i = 0; i <= pieces; i++)
        {
            var point = OnCircle(center, radius, startDegrees + sweepDegrees * i / pieces);
            if (i == 0 && _points.Count > 0 && Near(point, End))
            {
                continue;
            }
            Add(point);
        }
        return this;
    }

    /// <summary>The point on a circle at a clock angle (0° = 12 o'clock, clockwise).</summary>
    public static Point OnCircle(Point center, double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Sin(radians), center.Y - radius * Math.Cos(radians));
    }

    /// <summary>The point <paramref name="distance"/> pixels along the path (clamped to its ends).</summary>
    public Point PointAt(double distance)
    {
        if (_points.Count == 0)
        {
            return default;
        }
        if (_points.Count == 1 || distance <= 0)
        {
            return _points[0];
        }
        if (distance >= Length)
        {
            return _points[^1];
        }
        var i = Segment(distance);
        var from = _lengths[i];
        var span = _lengths[i + 1] - from;
        var t = span <= 0 ? 0 : (distance - from) / span;
        var a = _points[i];
        var b = _points[i + 1];
        return new Point(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>The point at <paramref name="fraction"/> (0..1) of the length.</summary>
    public Point PointAtFraction(double fraction) => PointAt(fraction * Length);

    /// <summary>The unit direction of travel at <paramref name="distance"/> (zero on an empty or one-point path).</summary>
    public Vector TangentAt(double distance)
    {
        if (_points.Count < 2)
        {
            return default;
        }
        var i = Segment(Math.Clamp(distance, 0, Length));
        var d = _points[i + 1] - _points[i];
        var length = Math.Sqrt(d.X * d.X + d.Y * d.Y);
        return length <= 0 ? default : d / length;
    }

    /// <summary>The whole path as one cached geometry (built on first use).</summary>
    public Geometry Geometry
    {
        get
        {
            if (_geometry is { } cached)
            {
                return cached;
            }
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                if (_points.Count > 0)
                {
                    context.BeginFigure(_points[0], false);
                    for (var i = 1; i < _points.Count; i++)
                    {
                        context.LineTo(_points[i]);
                    }
                    context.EndFigure(false);
                }
            }
            _geometry = geometry;
            return geometry;
        }
    }

    /// <summary>
    /// Sets <paramref name="pen"/>'s dash so that drawing <see cref="Geometry"/> with it shows only the part between
    /// <paramref name="from"/> and <paramref name="to"/> pixels along the path. The pen must own a
    /// <see cref="DashStyle"/> made for this (reused every frame, so nothing is allocated). Returns false when the part
    /// is empty and nothing should be drawn.
    /// </summary>
    public bool ShowPart(Pen pen, double from, double to)
    {
        from = Math.Clamp(from, 0, Length);
        to = Math.Clamp(to, 0, Length);
        if (to - from <= 0.01)
        {
            return false;
        }
        var thickness = Math.Max(pen.Thickness, 0.001);
        var dash = pen.DashStyle as DashStyle ?? new DashStyle();
        var dashes = dash.Dashes ??= new AvaloniaList<double>();
        var visible = (to - from) / thickness;
        var gap = (Length + 2) / thickness;
        if (dashes.Count != 2)
        {
            dashes.Clear();
            dashes.Add(visible);
            dashes.Add(gap);
        }
        else
        {
            dashes[0] = visible;
            dashes[1] = gap;
        }
        // Dashes and offset are in multiples of the pen thickness; the pattern starts `from` pixels in.
        dash.Offset = visible + gap - from / thickness;
        if (!ReferenceEquals(pen.DashStyle, dash))
        {
            pen.DashStyle = dash;
        }
        return true;
    }

    private void Add(Point point)
    {
        if (_points.Count == 0)
        {
            _points.Add(point);
            _lengths.Add(0);
        }
        else
        {
            var last = _points[^1];
            var d = point - last;
            _points.Add(point);
            _lengths.Add(_lengths[^1] + Math.Sqrt(d.X * d.X + d.Y * d.Y));
        }
        _geometry = null;
    }

    private int Segment(double distance)
    {
        // The last index i with _lengths[i] <= distance, kept below the final point.
        var lo = 0;
        var hi = _lengths.Count - 2;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_lengths[mid] <= distance)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;
}
