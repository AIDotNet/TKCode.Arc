using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A curve in the plane, parameterized by t in 0..1. The <c>...At</c> methods take u, a fraction of the
/// curve's length, instead.</summary>
public abstract class Curve2
{
    private float[]? _lengths;
    private int _arcLengthDivisions = 200;

    /// <summary>How many pieces the curve is cut into to measure its length.</summary>
    public int ArcLengthDivisions
    {
        get => _arcLengthDivisions;
        set
        {
            _arcLengthDivisions = Math.Max(1, value);
            _lengths = null;
        }
    }

    /// <summary>The point at parameter t (0..1).</summary>
    public abstract Vector2 GetPoint(float t);

    /// <summary>The point at a fraction u (0..1) of the curve's length.</summary>
    public virtual Vector2 GetPointAt(float u) => GetPoint(GetUtoTMapping(u));

    /// <summary><paramref name="divisions"/> + 1 points at equal parameter steps.</summary>
    public virtual Vector2[] GetPoints(int divisions = 5)
    {
        divisions = Math.Max(1, divisions);
        var points = new Vector2[divisions + 1];
        for (var i = 0; i <= divisions; i++)
        {
            points[i] = GetPoint((float)i / divisions);
        }

        return points;
    }

    /// <summary><paramref name="divisions"/> + 1 points at equal distances along the curve.</summary>
    public virtual Vector2[] GetSpacedPoints(int divisions = 5)
    {
        divisions = Math.Max(1, divisions);
        var points = new Vector2[divisions + 1];
        for (var i = 0; i <= divisions; i++)
        {
            points[i] = GetPointAt((float)i / divisions);
        }

        return points;
    }

    /// <summary>The length of the curve.</summary>
    public virtual float GetLength() => GetLengths()[^1];

    /// <summary>Cumulative lengths at <see cref="ArcLengthDivisions"/> + 1 equal parameter steps.</summary>
    public virtual float[] GetLengths()
    {
        if (_lengths is not null && _lengths.Length == _arcLengthDivisions + 1)
        {
            return _lengths;
        }

        var lengths = new float[_arcLengthDivisions + 1];
        var previous = GetPoint(0f);
        var sum = 0f;
        for (var i = 1; i <= _arcLengthDivisions; i++)
        {
            var current = GetPoint((float)i / _arcLengthDivisions);
            sum += Vector2.Distance(current, previous);
            lengths[i] = sum;
            previous = current;
        }

        _lengths = lengths;
        return lengths;
    }

    /// <summary>Forgets the measured lengths; call after changing the curve's control points.</summary>
    public virtual void UpdateArcLengths() => _lengths = null;

    /// <summary>The parameter t at a fraction u of the length (or at a distance from the start).</summary>
    public float GetUtoTMapping(float u, float? distance = null) => ArcLength.ToParameter(GetLengths(), u, distance);

    /// <summary>The unit tangent at parameter t.</summary>
    public virtual Vector2 GetTangent(float t)
    {
        const float Delta = 0.0001f;
        var d = GetPoint(MathF.Min(t + Delta, 1f)) - GetPoint(MathF.Max(t - Delta, 0f));
        return d.LengthSquared() > 0f ? Vector2.Normalize(d) : Vector2.UnitX;
    }

    /// <summary>The unit tangent at a fraction u of the length.</summary>
    public Vector2 GetTangentAt(float u) => GetTangent(GetUtoTMapping(u));

    /// <summary>How many straight pieces represent this curve when a path is turned into points with
    /// <paramref name="divisions"/> pieces per curve.</summary>
    internal virtual int Resolution(int divisions) => divisions;
}

/// <summary>A straight segment in the plane.</summary>
public sealed class LineCurve2 : Curve2
{
    /// <summary>Creates a segment from <paramref name="v1"/> to <paramref name="v2"/>.</summary>
    public LineCurve2(Vector2 v1, Vector2 v2)
    {
        V1 = v1;
        V2 = v2;
    }

    /// <summary>The start.</summary>
    public Vector2 V1 { get; set; }

    /// <summary>The end.</summary>
    public Vector2 V2 { get; set; }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t) => t >= 1f ? V2 : Vector2.Lerp(V1, V2, t);

    /// <inheritdoc/>
    public override Vector2 GetPointAt(float u) => GetPoint(u);

    /// <inheritdoc/>
    public override Vector2 GetTangent(float t)
    {
        var d = V2 - V1;
        return d.LengthSquared() > 0f ? Vector2.Normalize(d) : Vector2.UnitX;
    }

    internal override int Resolution(int divisions) => 1;
}

/// <summary>A quadratic Bézier curve in the plane.</summary>
public sealed class QuadraticBezierCurve2 : Curve2
{
    /// <summary>Creates the curve: start, control point, end.</summary>
    public QuadraticBezierCurve2(Vector2 v0, Vector2 v1, Vector2 v2)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
    }

    /// <summary>The start.</summary>
    public Vector2 V0 { get; set; }

    /// <summary>The control point.</summary>
    public Vector2 V1 { get; set; }

    /// <summary>The end.</summary>
    public Vector2 V2 { get; set; }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t)
    {
        var k = 1f - t;
        return (V0 * (k * k)) + (V1 * (2f * k * t)) + (V2 * (t * t));
    }
}

/// <summary>A cubic Bézier curve in the plane.</summary>
public sealed class CubicBezierCurve2 : Curve2
{
    /// <summary>Creates the curve: start, two control points, end.</summary>
    public CubicBezierCurve2(Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
        V3 = v3;
    }

    /// <summary>The start.</summary>
    public Vector2 V0 { get; set; }

    /// <summary>The first control point.</summary>
    public Vector2 V1 { get; set; }

    /// <summary>The second control point.</summary>
    public Vector2 V2 { get; set; }

    /// <summary>The end.</summary>
    public Vector2 V3 { get; set; }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t)
    {
        var k = 1f - t;
        return (V0 * (k * k * k)) + (V1 * (3f * k * k * t)) + (V2 * (3f * k * t * t)) + (V3 * (t * t * t));
    }
}

/// <summary>An elliptical arc.</summary>
public class EllipseCurve : Curve2
{
    /// <summary>Creates an arc of an ellipse centered on (<paramref name="x"/>, <paramref name="y"/>). Angles are in
    /// radians from the +X axis; the arc runs counter-clockwise unless <paramref name="clockwise"/> is true.</summary>
    public EllipseCurve(
        float x = 0f, float y = 0f, float xRadius = 1f, float yRadius = 1f, float startAngle = 0f, float endAngle = MathF.PI * 2f,
        bool clockwise = false, float rotation = 0f)
    {
        X = x;
        Y = y;
        XRadius = xRadius;
        YRadius = yRadius;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Clockwise = clockwise;
        Rotation = rotation;
    }

    /// <summary>Center x.</summary>
    public float X { get; set; }

    /// <summary>Center y.</summary>
    public float Y { get; set; }

    /// <summary>Radius along x.</summary>
    public float XRadius { get; set; }

    /// <summary>Radius along y.</summary>
    public float YRadius { get; set; }

    /// <summary>Where the arc starts, in radians.</summary>
    public float StartAngle { get; set; }

    /// <summary>Where the arc ends, in radians.</summary>
    public float EndAngle { get; set; }

    /// <summary>True runs the arc clockwise.</summary>
    public bool Clockwise { get; set; }

    /// <summary>Rotation of the whole ellipse about its center, in radians.</summary>
    public float Rotation { get; set; }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t)
    {
        const float TwoPi = MathF.PI * 2f;
        var delta = EndAngle - StartAngle;
        var samePoints = MathF.Abs(delta) < float.Epsilon;
        while (delta < 0f)
        {
            delta += TwoPi;
        }

        while (delta > TwoPi)
        {
            delta -= TwoPi;
        }

        if (delta < 1e-7f)
        {
            delta = samePoints ? 0f : TwoPi;
        }

        if (Clockwise && !samePoints)
        {
            delta = delta == TwoPi ? -TwoPi : delta - TwoPi;
        }

        var angle = StartAngle + (t * delta);
        var x = X + (XRadius * MathF.Cos(angle));
        var y = Y + (YRadius * MathF.Sin(angle));
        if (Rotation != 0f)
        {
            var (sin, cos) = MathF.SinCos(Rotation);
            var tx = x - X;
            var ty = y - Y;
            x = (tx * cos) - (ty * sin) + X;
            y = (tx * sin) + (ty * cos) + Y;
        }

        return new Vector2(x, y);
    }

    internal override int Resolution(int divisions) => divisions * 2;
}

/// <summary>A circular arc.</summary>
public sealed class ArcCurve : EllipseCurve
{
    /// <summary>Creates an arc of a circle.</summary>
    public ArcCurve(float x = 0f, float y = 0f, float radius = 1f, float startAngle = 0f, float endAngle = MathF.PI * 2f, bool clockwise = false)
        : base(x, y, radius, radius, startAngle, endAngle, clockwise)
    {
    }
}

/// <summary>A smooth curve through a list of points in the plane (uniform Catmull-Rom).</summary>
public sealed class SplineCurve2 : Curve2
{
    /// <summary>Creates the curve.</summary>
    public SplineCurve2(IReadOnlyList<Vector2> points)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
    }

    /// <summary>The points the curve passes through.</summary>
    public IReadOnlyList<Vector2> Points { get; set; }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t)
    {
        var points = Points;
        var n = points.Count;
        if (n == 0)
        {
            return Vector2.Zero;
        }

        var p = (n - 1) * t;
        var i = Math.Clamp((int)MathF.Floor(p), 0, n - 1);
        var w = p - i;
        var p0 = points[i == 0 ? i : i - 1];
        var p1 = points[i];
        var p2 = points[i > n - 2 ? n - 1 : i + 1];
        var p3 = points[i > n - 3 ? n - 1 : i + 2];
        var v0 = (p2 - p0) * 0.5f;
        var v1 = (p3 - p1) * 0.5f;
        var w2 = w * w;
        var w3 = w2 * w;
        return (((p1 * 2f) - (p2 * 2f) + v0 + v1) * w3) + (((p1 * -3f) + (p2 * 3f) - (v0 * 2f) - v1) * w2) + (v0 * w) + p1;
    }

    internal override int Resolution(int divisions) => divisions * Math.Max(1, Points.Count);
}

/// <summary>A 2D outline built with drawing commands (move, line, curve, arc), like a canvas path.</summary>
public class Path2 : Curve2
{
    private float[]? _curveLengths;

    /// <summary>Creates an empty path.</summary>
    public Path2()
    {
    }

    /// <summary>Creates a path of straight lines through points.</summary>
    public Path2(IReadOnlyList<Vector2> points)
    {
        SetFromPoints(points);
    }

    /// <summary>The pieces, in order.</summary>
    public List<Curve2> Curves { get; } = [];

    /// <summary>Where the next command starts.</summary>
    public Vector2 CurrentPoint { get; private set; }

    /// <summary>True makes <see cref="GetPoints"/> end on the first point.</summary>
    public bool AutoClose { get; set; }

    /// <summary>Replaces the path with straight lines through points.</summary>
    public Path2 SetFromPoints(IReadOnlyList<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Curves.Clear();
        if (points.Count > 0)
        {
            MoveTo(points[0].X, points[0].Y);
            for (var i = 1; i < points.Count; i++)
            {
                LineTo(points[i].X, points[i].Y);
            }
        }

        Changed();
        return this;
    }

    /// <summary>Starts a new stroke at a point.</summary>
    public Path2 MoveTo(float x, float y)
    {
        CurrentPoint = new Vector2(x, y);
        return this;
    }

    /// <summary>A straight line to a point.</summary>
    public Path2 LineTo(float x, float y)
    {
        var to = new Vector2(x, y);
        Curves.Add(new LineCurve2(CurrentPoint, to));
        CurrentPoint = to;
        Changed();
        return this;
    }

    /// <summary>A quadratic Bézier to a point.</summary>
    public Path2 QuadraticCurveTo(float controlX, float controlY, float x, float y)
    {
        var to = new Vector2(x, y);
        Curves.Add(new QuadraticBezierCurve2(CurrentPoint, new Vector2(controlX, controlY), to));
        CurrentPoint = to;
        Changed();
        return this;
    }

    /// <summary>A cubic Bézier to a point.</summary>
    public Path2 BezierCurveTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
        var to = new Vector2(x, y);
        Curves.Add(new CubicBezierCurve2(CurrentPoint, new Vector2(control1X, control1Y), new Vector2(control2X, control2Y), to));
        CurrentPoint = to;
        Changed();
        return this;
    }

    /// <summary>A smooth curve from the current point through the given points.</summary>
    public Path2 SplineThru(IReadOnlyList<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var all = new List<Vector2>(points.Count + 1) { CurrentPoint };
        all.AddRange(points);
        Curves.Add(new SplineCurve2(all));
        CurrentPoint = all[^1];
        Changed();
        return this;
    }

    /// <summary>A circular arc whose center is given relative to the current point.</summary>
    public Path2 Arc(float x, float y, float radius, float startAngle, float endAngle, bool clockwise = false) =>
        AbsArc(x + CurrentPoint.X, y + CurrentPoint.Y, radius, startAngle, endAngle, clockwise);

    /// <summary>A circular arc with an absolute center.</summary>
    public Path2 AbsArc(float x, float y, float radius, float startAngle, float endAngle, bool clockwise = false) =>
        AbsEllipse(x, y, radius, radius, startAngle, endAngle, clockwise);

    /// <summary>An elliptical arc whose center is given relative to the current point.</summary>
    public Path2 Ellipse(float x, float y, float xRadius, float yRadius, float startAngle, float endAngle, bool clockwise = false, float rotation = 0f) =>
        AbsEllipse(x + CurrentPoint.X, y + CurrentPoint.Y, xRadius, yRadius, startAngle, endAngle, clockwise, rotation);

    /// <summary>An elliptical arc with an absolute center. A straight line joins the current point to its start.</summary>
    public Path2 AbsEllipse(float x, float y, float xRadius, float yRadius, float startAngle, float endAngle, bool clockwise = false, float rotation = 0f)
    {
        var curve = new EllipseCurve(x, y, xRadius, yRadius, startAngle, endAngle, clockwise, rotation);
        if (Curves.Count > 0)
        {
            var first = curve.GetPoint(0f);
            if (!Same(first, CurrentPoint))
            {
                LineTo(first.X, first.Y);
            }
        }

        Curves.Add(curve);
        CurrentPoint = curve.GetPoint(1f);
        Changed();
        return this;
    }

    /// <summary>A straight line back to the start of the path when it is not already there.</summary>
    public Path2 ClosePath()
    {
        if (Curves.Count > 0)
        {
            var start = Curves[0].GetPoint(0f);
            var end = Curves[^1].GetPoint(1f);
            if (!Same(start, end))
            {
                Curves.Add(new LineCurve2(end, start));
                CurrentPoint = start;
                Changed();
            }
        }

        return this;
    }

    private void Changed()
    {
        _curveLengths = null;
        base.UpdateArcLengths();
    }

    /// <inheritdoc/>
    public override void UpdateArcLengths()
    {
        Changed();
        foreach (var curve in Curves)
        {
            curve.UpdateArcLengths();
        }
    }

    private float[] CurveLengths()
    {
        if (_curveLengths is null || _curveLengths.Length != Curves.Count)
        {
            var lengths = new float[Curves.Count];
            var sum = 0f;
            for (var i = 0; i < lengths.Length; i++)
            {
                sum += Curves[i].GetLength();
                lengths[i] = sum;
            }

            _curveLengths = lengths;
        }

        return _curveLengths;
    }

    /// <inheritdoc/>
    public override float GetLength()
    {
        var lengths = CurveLengths();
        return lengths.Length > 0 ? lengths[^1] : 0f;
    }

    /// <inheritdoc/>
    public override Vector2 GetPoint(float t)
    {
        var lengths = CurveLengths();
        if (lengths.Length == 0)
        {
            return CurrentPoint;
        }

        var d = t * lengths[^1];
        for (var i = 0; i < lengths.Length; i++)
        {
            if (lengths[i] >= d)
            {
                var curve = Curves[i];
                var segment = curve.GetLength();
                var u = segment == 0f ? 0f : 1f - ((lengths[i] - d) / segment);
                return curve.GetPointAt(Math.Clamp(u, 0f, 1f));
            }
        }

        return Curves[^1].GetPoint(1f);
    }

    /// <summary>The outline as points: every curve cut into <paramref name="divisions"/> pieces (straight lines into
    /// one, arcs into twice as many), without repeating the point where two pieces meet.</summary>
    public override Vector2[] GetPoints(int divisions = 12)
    {
        var points = new List<Vector2>();
        foreach (var curve in Curves)
        {
            var piece = curve.GetPoints(curve.Resolution(Math.Max(1, divisions)));
            foreach (var p in piece)
            {
                if (points.Count > 0 && Same(points[^1], p))
                {
                    continue;
                }

                points.Add(p);
            }
        }

        if (AutoClose && points.Count > 1 && !Same(points[^1], points[0]))
        {
            points.Add(points[0]);
        }

        return [.. points];
    }

    /// <summary>True when two points differ only by rounding (relative to their size).</summary>
    internal static bool Same(Vector2 a, Vector2 b) =>
        Vector2.DistanceSquared(a, b) <= 1e-10f * MathF.Max(1f, MathF.Max(a.LengthSquared(), b.LengthSquared()));
}

/// <summary>A closed 2D outline with optional holes: the input of <see cref="ShapeGeometry"/> and
/// <see cref="ExtrudeGeometry"/>.</summary>
public class Shape2 : Path2
{
    /// <summary>Creates an empty shape.</summary>
    public Shape2()
    {
    }

    /// <summary>Creates a polygon through points.</summary>
    public Shape2(IReadOnlyList<Vector2> points)
        : base(points)
    {
    }

    /// <summary>Outlines cut out of the shape.</summary>
    public List<Path2> Holes { get; } = [];

    // The drawing commands again, returning the shape so that calls can be chained from "new Shape2()".

    /// <inheritdoc cref="Path2.MoveTo"/>
    public new Shape2 MoveTo(float x, float y)
    {
        base.MoveTo(x, y);
        return this;
    }

    /// <inheritdoc cref="Path2.LineTo"/>
    public new Shape2 LineTo(float x, float y)
    {
        base.LineTo(x, y);
        return this;
    }

    /// <inheritdoc cref="Path2.QuadraticCurveTo"/>
    public new Shape2 QuadraticCurveTo(float controlX, float controlY, float x, float y)
    {
        base.QuadraticCurveTo(controlX, controlY, x, y);
        return this;
    }

    /// <inheritdoc cref="Path2.BezierCurveTo"/>
    public new Shape2 BezierCurveTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
        base.BezierCurveTo(control1X, control1Y, control2X, control2Y, x, y);
        return this;
    }

    /// <inheritdoc cref="Path2.SplineThru"/>
    public new Shape2 SplineThru(IReadOnlyList<Vector2> points)
    {
        base.SplineThru(points);
        return this;
    }

    /// <inheritdoc cref="Path2.Arc"/>
    public new Shape2 Arc(float x, float y, float radius, float startAngle, float endAngle, bool clockwise = false)
    {
        base.Arc(x, y, radius, startAngle, endAngle, clockwise);
        return this;
    }

    /// <inheritdoc cref="Path2.AbsArc"/>
    public new Shape2 AbsArc(float x, float y, float radius, float startAngle, float endAngle, bool clockwise = false)
    {
        base.AbsArc(x, y, radius, startAngle, endAngle, clockwise);
        return this;
    }

    /// <inheritdoc cref="Path2.Ellipse"/>
    public new Shape2 Ellipse(float x, float y, float xRadius, float yRadius, float startAngle, float endAngle, bool clockwise = false, float rotation = 0f)
    {
        base.Ellipse(x, y, xRadius, yRadius, startAngle, endAngle, clockwise, rotation);
        return this;
    }

    /// <inheritdoc cref="Path2.AbsEllipse"/>
    public new Shape2 AbsEllipse(float x, float y, float xRadius, float yRadius, float startAngle, float endAngle, bool clockwise = false, float rotation = 0f)
    {
        base.AbsEllipse(x, y, xRadius, yRadius, startAngle, endAngle, clockwise, rotation);
        return this;
    }

    /// <inheritdoc cref="Path2.ClosePath"/>
    public new Shape2 ClosePath()
    {
        base.ClosePath();
        return this;
    }

    /// <summary>The outline and the holes as point lists, each without a repeated closing point.</summary>
    public (Vector2[] Shape, Vector2[][] Holes) ExtractPoints(int divisions = 12)
    {
        var holes = new Vector2[Holes.Count][];
        for (var i = 0; i < holes.Length; i++)
        {
            holes[i] = Open(Holes[i].GetPoints(divisions));
        }

        return (Open(GetPoints(divisions)), holes);
    }

    private static Vector2[] Open(Vector2[] points) =>
        points.Length > 2 && Same(points[0], points[^1]) ? points[..^1] : points;
}
