using System.Globalization;
using Avalonia;

namespace TKCode.Arc.Primitives;

/// <summary>
/// One flattened sub-path of a <see cref="PathMorphShape"/>: points along the outline plus the boundaries of the
/// source commands (segments), so two outlines with the same command structure can be sampled vertex to vertex.
/// A closed contour repeats its first point at the end.
/// </summary>
public sealed class PathMorphContour
{
    private readonly Point[] _points;
    private readonly int[] _segmentEnds;
    private readonly double[] _cumulative;

    /// <summary>Creates a contour from flattened points.</summary>
    /// <param name="points">Flattened outline. A closed contour must end on its first point.</param>
    /// <param name="segmentEnds">Index into <paramref name="points"/> where each source segment ends (ascending, last = points.Length - 1).</param>
    /// <param name="isClosed">Whether the contour is closed.</param>
    public PathMorphContour(Point[] points, int[] segmentEnds, bool isClosed)
    {
        if (points.Length == 0)
        {
            throw new ArgumentException("A contour needs at least one point.", nameof(points));
        }
        _points = points;
        _segmentEnds = segmentEnds.Length == 0 ? [points.Length - 1] : segmentEnds;
        IsClosed = isClosed;
        _cumulative = new double[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            _cumulative[i] = _cumulative[i - 1] + Distance(points[i - 1], points[i]);
        }
        Length = _cumulative[^1];
    }

    /// <summary>The flattened outline.</summary>
    public IReadOnlyList<Point> Points => _points;

    /// <summary>Number of source commands (lines, curves, arcs, the closing line) in this contour.</summary>
    public int SegmentCount => _segmentEnds.Length;

    public bool IsClosed { get; }

    /// <summary>Total arc length.</summary>
    public double Length { get; }

    /// <summary>Arc length of source segment <paramref name="segment"/>.</summary>
    public double SegmentLength(int segment) => _cumulative[_segmentEnds[segment]] - SegmentStartDistance(segment);

    /// <summary>The average of the outline's points (a collapsed contour grows from here).</summary>
    public Point Centroid
    {
        get
        {
            var count = IsClosed && _points.Length > 1 ? _points.Length - 1 : _points.Length;
            double x = 0, y = 0;
            for (var i = 0; i < count; i++)
            {
                x += _points[i].X;
                y += _points[i].Y;
            }
            return new Point(x / count, y / count);
        }
    }

    /// <summary>The point at arc length <paramref name="distance"/> from the start (clamped to the outline).</summary>
    public Point PointAt(double distance)
    {
        if (_points.Length == 1 || distance <= 0)
        {
            return _points[0];
        }
        if (distance >= Length)
        {
            return _points[^1];
        }
        var index = Array.BinarySearch(_cumulative, distance);
        if (index >= 0)
        {
            return _points[index];
        }
        index = ~index;
        var a = _points[index - 1];
        var b = _points[index];
        var span = _cumulative[index] - _cumulative[index - 1];
        var f = span <= 0 ? 0 : (distance - _cumulative[index - 1]) / span;
        return new Point(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
    }

    /// <summary>The point at fraction <paramref name="fraction"/> (by arc length) of source segment <paramref name="segment"/>.</summary>
    public Point PointInSegment(int segment, double fraction)
    {
        var start = SegmentStartDistance(segment);
        var end = _cumulative[_segmentEnds[segment]];
        if (end - start <= 1e-12)
        {
            return _points[_segmentEnds[segment]];
        }
        return PointAt(start + (end - start) * Math.Clamp(fraction, 0, 1));
    }

    private double SegmentStartDistance(int segment) => segment == 0 ? 0 : _cumulative[_segmentEnds[segment - 1]];

    internal static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>
/// P16: an outline that <see cref="PathMorph"/> can interpolate. Parsed from SVG path data (the full command set:
/// M L H V C S Q T A Z, absolute and relative) and flattened to polylines, so it needs no rendering platform.
/// Built from data points with <see cref="FromPolylines"/> (charts).
/// </summary>
public sealed class PathMorphShape
{
    private readonly PathMorphContour[] _contours;

    public PathMorphShape(IEnumerable<PathMorphContour> contours)
    {
        _contours = contours.ToArray();
        Signature = string.Join("|", _contours.Select(c => (c.IsClosed ? "c" : "o") + c.SegmentCount.ToString(CultureInfo.InvariantCulture)));
    }

    public IReadOnlyList<PathMorphContour> Contours => _contours;

    /// <summary>Total arc length of all contours.</summary>
    public double Length => _contours.Sum(c => c.Length);

    /// <summary>Command structure (contour count, closedness, segments per contour). Equal signatures sample vertex to vertex.</summary>
    public string Signature { get; }

    /// <summary>True when both outlines have the same command structure, so their vertices can correspond.</summary>
    public bool IsCompatibleWith(PathMorphShape other) => string.Equals(Signature, other.Signature, StringComparison.Ordinal);

    /// <summary>Parses SVG path data. <paramref name="flatness"/> is the curve step in path units (24-unit icons: 0.5).</summary>
    public static PathMorphShape Parse(string pathData, double flatness = 0.5) => new(PathMorphParser.Parse(pathData, flatness));

    /// <summary>Builds a shape whose segments are the straight lines between the given vertices (chart series, polygons).</summary>
    public static PathMorphShape FromPolylines(IEnumerable<(IReadOnlyList<Point> Points, bool IsClosed)> polylines)
    {
        var contours = new List<PathMorphContour>();
        foreach (var (points, closed) in polylines)
        {
            if (points.Count == 0)
            {
                continue;
            }
            var list = new List<Point>(points);
            if (closed && list.Count > 1 && PathMorphContour.Distance(list[0], list[^1]) > 1e-9)
            {
                list.Add(list[0]);
            }
            var ends = new int[Math.Max(1, list.Count - 1)];
            for (var i = 0; i < ends.Length; i++)
            {
                ends[i] = Math.Min(i + 1, list.Count - 1);
            }
            contours.Add(new PathMorphContour(list.ToArray(), ends, closed));
        }
        return new PathMorphShape(contours);
    }

    internal static PathMorphShape FromFrame(PathMorphFrame frame)
    {
        var contours = new List<(IReadOnlyList<Point>, bool)>(frame.ContourCount);
        for (var i = 0; i < frame.ContourCount; i++)
        {
            contours.Add((frame.GetContour(i).ToArray(), frame.IsClosed(i)));
        }
        return FromPolylines(contours);
    }
}

/// <summary>SVG path data parser that flattens curves and arcs (no platform geometry needed).</summary>
internal static class PathMorphParser
{
    public static List<PathMorphContour> Parse(string data, double flatness)
    {
        var builder = new ContourBuilder(flatness);
        var reader = new Reader(data);
        var current = new Point();
        var subpathStart = new Point();
        var lastControl = new Point();
        var lastCommand = ' ';
        var command = ' ';
        while (reader.SkipSeparators())
        {
            if (reader.PeekCommand(out var next))
            {
                command = next;
                reader.Advance();
            }
            else if (command == ' ')
            {
                throw new FormatException($"Path data must start with a command: '{data}'.");
            }
            var relative = char.IsLower(command);
            var origin = relative ? current : new Point();
            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                {
                    var p = reader.ReadPoint() + origin;
                    builder.MoveTo(p);
                    current = subpathStart = p;
                    // Subsequent pairs are implicit line-tos.
                    command = relative ? 'l' : 'L';
                    lastCommand = 'M';
                    lastControl = current;
                    continue;
                }
                case 'L':
                {
                    var p = reader.ReadPoint() + origin;
                    builder.LineTo(current, p);
                    current = p;
                    break;
                }
                case 'H':
                {
                    var x = reader.ReadNumber() + origin.X;
                    var p = new Point(x, current.Y);
                    builder.LineTo(current, p);
                    current = p;
                    break;
                }
                case 'V':
                {
                    var y = reader.ReadNumber() + origin.Y;
                    var p = new Point(current.X, y);
                    builder.LineTo(current, p);
                    current = p;
                    break;
                }
                case 'C':
                {
                    var c1 = reader.ReadPoint() + origin;
                    var c2 = reader.ReadPoint() + origin;
                    var p = reader.ReadPoint() + origin;
                    builder.CubicTo(current, c1, c2, p);
                    lastControl = c2;
                    current = p;
                    lastCommand = 'C';
                    continue;
                }
                case 'S':
                {
                    var c1 = lastCommand is 'C' ? Reflect(lastControl, current) : current;
                    var c2 = reader.ReadPoint() + origin;
                    var p = reader.ReadPoint() + origin;
                    builder.CubicTo(current, c1, c2, p);
                    lastControl = c2;
                    current = p;
                    lastCommand = 'C';
                    continue;
                }
                case 'Q':
                {
                    var c = reader.ReadPoint() + origin;
                    var p = reader.ReadPoint() + origin;
                    builder.QuadTo(current, c, p);
                    lastControl = c;
                    current = p;
                    lastCommand = 'Q';
                    continue;
                }
                case 'T':
                {
                    var c = lastCommand is 'Q' ? Reflect(lastControl, current) : current;
                    var p = reader.ReadPoint() + origin;
                    builder.QuadTo(current, c, p);
                    lastControl = c;
                    current = p;
                    lastCommand = 'Q';
                    continue;
                }
                case 'A':
                {
                    var rx = reader.ReadNumber();
                    var ry = reader.ReadNumber();
                    var rotation = reader.ReadNumber();
                    var large = reader.ReadFlag();
                    var sweep = reader.ReadFlag();
                    var p = reader.ReadPoint() + origin;
                    builder.ArcTo(current, rx, ry, rotation, large, sweep, p);
                    current = p;
                    break;
                }
                case 'Z':
                {
                    builder.Close(current, subpathStart);
                    current = subpathStart;
                    lastCommand = 'Z';
                    lastControl = current;
                    // A command after Z without M continues from the subpath start.
                    command = ' ';
                    if (reader.SkipSeparators() && !reader.PeekCommand(out _))
                    {
                        throw new FormatException("Numbers cannot follow Z.");
                    }
                    continue;
                }
                default:
                    throw new FormatException($"Unsupported path command '{command}'.");
            }
            lastCommand = 'L';
            lastControl = current;
        }
        return builder.Finish();
    }

    private static Point Reflect(Point control, Point about) => new(2 * about.X - control.X, 2 * about.Y - control.Y);

    private sealed class ContourBuilder
    {
        private readonly double _flatness;
        private readonly List<PathMorphContour> _contours = new();
        private readonly List<Point> _points = new();
        private readonly List<int> _ends = new();
        private Point _pendingStart;
        private bool _hasPending;

        public ContourBuilder(double flatness) => _flatness = Math.Max(flatness, 0.01);

        public void MoveTo(Point p)
        {
            Flush(false);
            _pendingStart = p;
            _hasPending = true;
        }

        private void EnsureStarted(Point from)
        {
            if (_points.Count == 0)
            {
                _points.Add(_hasPending ? _pendingStart : from);
                _hasPending = false;
            }
        }

        public void LineTo(Point from, Point to)
        {
            EnsureStarted(from);
            _points.Add(to);
            _ends.Add(_points.Count - 1);
        }

        public void CubicTo(Point p0, Point c1, Point c2, Point p3)
        {
            EnsureStarted(p0);
            var hull = PathMorphContour.Distance(p0, c1) + PathMorphContour.Distance(c1, c2) + PathMorphContour.Distance(c2, p3);
            var steps = Math.Clamp((int)Math.Ceiling(hull / _flatness), 1, 96);
            for (var i = 1; i <= steps; i++)
            {
                var t = (double)i / steps;
                var mt = 1 - t;
                var a = mt * mt * mt;
                var b = 3 * mt * mt * t;
                var c = 3 * mt * t * t;
                var d = t * t * t;
                _points.Add(new Point(a * p0.X + b * c1.X + c * c2.X + d * p3.X, a * p0.Y + b * c1.Y + c * c2.Y + d * p3.Y));
            }
            _ends.Add(_points.Count - 1);
        }

        public void QuadTo(Point p0, Point c, Point p2)
        {
            // Degree elevation keeps one flattening path for both curve kinds.
            var c1 = new Point(p0.X + 2.0 / 3 * (c.X - p0.X), p0.Y + 2.0 / 3 * (c.Y - p0.Y));
            var c2 = new Point(p2.X + 2.0 / 3 * (c.X - p2.X), p2.Y + 2.0 / 3 * (c.Y - p2.Y));
            CubicTo(p0, c1, c2, p2);
        }

        /// <summary>SVG arc implementation notes F.6.5 / F.6.6: endpoint to center parameterization, then sampled by angle.</summary>
        public void ArcTo(Point from, double rx, double ry, double rotationDegrees, bool largeArc, bool sweep, Point to)
        {
            rx = Math.Abs(rx);
            ry = Math.Abs(ry);
            if (rx < 1e-12 || ry < 1e-12 || PathMorphContour.Distance(from, to) < 1e-12)
            {
                LineTo(from, to);
                return;
            }
            EnsureStarted(from);
            var phi = rotationDegrees * Math.PI / 180;
            var cos = Math.Cos(phi);
            var sin = Math.Sin(phi);
            var dx = (from.X - to.X) / 2;
            var dy = (from.Y - to.Y) / 2;
            var x1 = cos * dx + sin * dy;
            var y1 = -sin * dx + cos * dy;
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1)
            {
                var s = Math.Sqrt(lambda);
                rx *= s;
                ry *= s;
            }
            var num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var coef = den <= 0 ? 0 : Math.Sqrt(Math.Max(0, num / den));
            if (largeArc == sweep)
            {
                coef = -coef;
            }
            var cx1 = coef * rx * y1 / ry;
            var cy1 = -coef * ry * x1 / rx;
            var cx = cos * cx1 - sin * cy1 + (from.X + to.X) / 2;
            var cy = sin * cx1 + cos * cy1 + (from.Y + to.Y) / 2;
            var theta1 = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
            var delta = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);
            if (!sweep && delta > 0)
            {
                delta -= 2 * Math.PI;
            }
            else if (sweep && delta < 0)
            {
                delta += 2 * Math.PI;
            }
            var steps = Math.Clamp((int)Math.Ceiling(Math.Abs(delta) * Math.Max(rx, ry) / _flatness), 2, 128);
            for (var i = 1; i < steps; i++)
            {
                var theta = theta1 + delta * i / steps;
                var ex = rx * Math.Cos(theta);
                var ey = ry * Math.Sin(theta);
                _points.Add(new Point(cos * ex - sin * ey + cx, sin * ex + cos * ey + cy));
            }
            _points.Add(to);
            _ends.Add(_points.Count - 1);
        }

        private static double Angle(double ux, double uy, double vx, double vy)
        {
            var angle = Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
            return angle;
        }

        public void Close(Point current, Point start)
        {
            if (_points.Count == 0)
            {
                // "M x y Z": a single point.
                _points.Add(_hasPending ? _pendingStart : start);
                _hasPending = false;
            }
            if (PathMorphContour.Distance(current, start) > 1e-9)
            {
                _points.Add(start);
                _ends.Add(_points.Count - 1);
            }
            Flush(true);
            _pendingStart = start;
            _hasPending = true;
        }

        private void Flush(bool closed)
        {
            if (_points.Count > 0)
            {
                _contours.Add(new PathMorphContour(_points.ToArray(), _ends.ToArray(), closed));
            }
            _points.Clear();
            _ends.Clear();
            _hasPending = false;
        }

        public List<PathMorphContour> Finish()
        {
            Flush(false);
            return _contours;
        }
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<char> _text;
        private int _index;

        public Reader(string text)
        {
            _text = text.AsSpan();
            _index = 0;
        }

        /// <summary>Skips whitespace and commas; false at the end.</summary>
        public bool SkipSeparators()
        {
            while (_index < _text.Length && (char.IsWhiteSpace(_text[_index]) || _text[_index] == ','))
            {
                _index++;
            }
            return _index < _text.Length;
        }

        public bool PeekCommand(out char command)
        {
            command = _index < _text.Length ? _text[_index] : '\0';
            return command is 'M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v' or 'C' or 'c' or 'S' or 's' or 'Q' or 'q'
                or 'T' or 't' or 'A' or 'a' or 'Z' or 'z';
        }

        public void Advance() => _index++;

        public Point ReadPoint()
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return new Point(x, y);
        }

        public bool ReadFlag()
        {
            SkipSeparators();
            if (_index >= _text.Length || (_text[_index] != '0' && _text[_index] != '1'))
            {
                throw new FormatException("Expected an arc flag (0 or 1).");
            }
            return _text[_index++] == '1';
        }

        public double ReadNumber()
        {
            SkipSeparators();
            var start = _index;
            if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
            {
                _index++;
            }
            var digits = false;
            while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
            {
                _index++;
                digits = true;
            }
            if (_index < _text.Length && _text[_index] == '.')
            {
                _index++;
                while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
                {
                    _index++;
                    digits = true;
                }
            }
            if (!digits)
            {
                throw new FormatException($"Expected a number at position {start}.");
            }
            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                var save = _index;
                _index++;
                if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
                {
                    _index++;
                }
                var expDigits = false;
                while (_index < _text.Length && char.IsAsciiDigit(_text[_index]))
                {
                    _index++;
                    expDigits = true;
                }
                if (!expDigits)
                {
                    _index = save;
                }
            }
            return double.Parse(_text[start.._index], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
