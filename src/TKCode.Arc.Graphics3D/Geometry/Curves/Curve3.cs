using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Tangent, normal and binormal at evenly spaced points of a curve, without sudden twists
/// (parallel transport frames).</summary>
public sealed class FrenetFrames
{
    internal FrenetFrames(Vector3[] tangents, Vector3[] normals, Vector3[] binormals)
    {
        Tangents = tangents;
        Normals = normals;
        Binormals = binormals;
    }

    /// <summary>Unit tangents.</summary>
    public Vector3[] Tangents { get; }

    /// <summary>Unit normals.</summary>
    public Vector3[] Normals { get; }

    /// <summary>Unit binormals (tangent × normal).</summary>
    public Vector3[] Binormals { get; }
}

/// <summary>Arc length bookkeeping shared by 2D and 3D curves: cumulative lengths and the map from a fraction of the
/// length to the curve parameter.</summary>
internal static class ArcLength
{
    public static float ToParameter(float[] lengths, float u, float? distance)
    {
        var last = lengths.Length - 1;
        var total = lengths[last];
        var target = distance ?? (u * total);
        if (total <= 0f || last == 0)
        {
            return Math.Clamp(u, 0f, 1f);
        }

        int low = 0, high = last;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var comparison = lengths[mid] - target;
            if (comparison < 0f)
            {
                low = mid + 1;
            }
            else if (comparison > 0f)
            {
                high = mid - 1;
            }
            else
            {
                high = mid;
                break;
            }
        }

        var i = Math.Clamp(high, 0, last);
        if (lengths[i] == target || i == last)
        {
            return (float)i / last;
        }

        var before = lengths[i];
        var segment = lengths[i + 1] - before;
        var fraction = segment > 0f ? (target - before) / segment : 0f;
        return (i + fraction) / last;
    }
}

/// <summary>A curve in space, parameterized by t in 0..1. The <c>...At</c> methods take u, a fraction of the
/// curve's length, instead.</summary>
public abstract class Curve3
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
    public abstract Vector3 GetPoint(float t);

    /// <summary>The point at a fraction u (0..1) of the curve's length.</summary>
    public virtual Vector3 GetPointAt(float u) => GetPoint(GetUtoTMapping(u));

    /// <summary><paramref name="divisions"/> + 1 points at equal parameter steps.</summary>
    public virtual Vector3[] GetPoints(int divisions = 5)
    {
        divisions = Math.Max(1, divisions);
        var points = new Vector3[divisions + 1];
        for (var i = 0; i <= divisions; i++)
        {
            points[i] = GetPoint((float)i / divisions);
        }

        return points;
    }

    /// <summary><paramref name="divisions"/> + 1 points at equal distances along the curve.</summary>
    public virtual Vector3[] GetSpacedPoints(int divisions = 5)
    {
        divisions = Math.Max(1, divisions);
        var points = new Vector3[divisions + 1];
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
            sum += Vector3.Distance(current, previous);
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
    public virtual Vector3 GetTangent(float t)
    {
        const float Delta = 0.0001f;
        var d = GetPoint(MathF.Min(t + Delta, 1f)) - GetPoint(MathF.Max(t - Delta, 0f));
        return d.LengthSquared() > 0f ? Vector3.Normalize(d) : Vector3.UnitX;
    }

    /// <summary>The unit tangent at a fraction u of the length.</summary>
    public Vector3 GetTangentAt(float u) => GetTangent(GetUtoTMapping(u));

    /// <summary>Frames at <paramref name="segments"/> + 1 points spaced evenly along the length. With
    /// <paramref name="closed"/> the last frame is twisted to meet the first.</summary>
    public FrenetFrames ComputeFrenetFrames(int segments, bool closed = false)
    {
        segments = Math.Max(1, segments);
        var tangents = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var binormals = new Vector3[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            tangents[i] = GetTangentAt((float)i / segments);
        }

        // Start with the axis the first tangent is least aligned with.
        var t0 = tangents[0];
        float tx = MathF.Abs(t0.X), ty = MathF.Abs(t0.Y), tz = MathF.Abs(t0.Z);
        var min = float.MaxValue;
        var normal = Vector3.UnitX;
        if (tx <= min)
        {
            min = tx;
            normal = Vector3.UnitX;
        }

        if (ty <= min)
        {
            min = ty;
            normal = Vector3.UnitY;
        }

        if (tz <= min)
        {
            normal = Vector3.UnitZ;
        }

        var vec = Vector3.Normalize(Vector3.Cross(t0, normal));
        normals[0] = Vector3.Cross(t0, vec);
        binormals[0] = Vector3.Cross(t0, normals[0]);
        for (var i = 1; i <= segments; i++)
        {
            normals[i] = normals[i - 1];
            vec = Vector3.Cross(tangents[i - 1], tangents[i]);
            if (vec.Length() > 1e-7f)
            {
                vec = Vector3.Normalize(vec);
                var theta = MathF.Acos(Math.Clamp(Vector3.Dot(tangents[i - 1], tangents[i]), -1f, 1f));
                normals[i] = Vector3.Transform(normals[i], Quaternion.CreateFromAxisAngle(vec, theta));
            }

            binormals[i] = Vector3.Cross(tangents[i], normals[i]);
        }

        if (closed)
        {
            var theta = MathF.Acos(Math.Clamp(Vector3.Dot(normals[0], normals[segments]), -1f, 1f)) / segments;
            if (Vector3.Dot(tangents[0], Vector3.Cross(normals[0], normals[segments])) > 0f)
            {
                theta = -theta;
            }

            for (var i = 1; i <= segments; i++)
            {
                normals[i] = Vector3.Transform(normals[i], Quaternion.CreateFromAxisAngle(tangents[i], theta * i));
                binormals[i] = Vector3.Cross(tangents[i], normals[i]);
            }
        }

        for (var i = 0; i <= segments; i++)
        {
            normals[i] = Vector3.Normalize(normals[i]);
            binormals[i] = Vector3.Normalize(binormals[i]);
        }

        return new FrenetFrames(tangents, normals, binormals);
    }
}

/// <summary>A straight segment.</summary>
public sealed class LineCurve3 : Curve3
{
    /// <summary>Creates a segment from <paramref name="v1"/> to <paramref name="v2"/>.</summary>
    public LineCurve3(Vector3 v1, Vector3 v2)
    {
        V1 = v1;
        V2 = v2;
    }

    /// <summary>The start.</summary>
    public Vector3 V1 { get; set; }

    /// <summary>The end.</summary>
    public Vector3 V2 { get; set; }

    /// <inheritdoc/>
    public override Vector3 GetPoint(float t) => t >= 1f ? V2 : Vector3.Lerp(V1, V2, t);

    /// <inheritdoc/>
    public override Vector3 GetPointAt(float u) => GetPoint(u);

    /// <inheritdoc/>
    public override Vector3 GetTangent(float t)
    {
        var d = V2 - V1;
        return d.LengthSquared() > 0f ? Vector3.Normalize(d) : Vector3.UnitX;
    }
}

/// <summary>A quadratic Bézier curve: start, one control point, end.</summary>
public sealed class QuadraticBezierCurve3 : Curve3
{
    /// <summary>Creates the curve.</summary>
    public QuadraticBezierCurve3(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
    }

    /// <summary>The start.</summary>
    public Vector3 V0 { get; set; }

    /// <summary>The control point.</summary>
    public Vector3 V1 { get; set; }

    /// <summary>The end.</summary>
    public Vector3 V2 { get; set; }

    /// <inheritdoc/>
    public override Vector3 GetPoint(float t)
    {
        var k = 1f - t;
        return (V0 * (k * k)) + (V1 * (2f * k * t)) + (V2 * (t * t));
    }
}

/// <summary>A cubic Bézier curve: start, two control points, end.</summary>
public sealed class CubicBezierCurve3 : Curve3
{
    /// <summary>Creates the curve.</summary>
    public CubicBezierCurve3(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        V0 = v0;
        V1 = v1;
        V2 = v2;
        V3 = v3;
    }

    /// <summary>The start.</summary>
    public Vector3 V0 { get; set; }

    /// <summary>The first control point.</summary>
    public Vector3 V1 { get; set; }

    /// <summary>The second control point.</summary>
    public Vector3 V2 { get; set; }

    /// <summary>The end.</summary>
    public Vector3 V3 { get; set; }

    /// <inheritdoc/>
    public override Vector3 GetPoint(float t)
    {
        var k = 1f - t;
        return (V0 * (k * k * k)) + (V1 * (3f * k * k * t)) + (V2 * (3f * k * t * t)) + (V3 * (t * t * t));
    }
}

/// <summary>How a <see cref="CatmullRomCurve3"/> spaces its knots.</summary>
public enum CatmullRomType
{
    /// <summary>Knots by the square root of the chord length: no cusps or self-intersections. The default.</summary>
    Centripetal,

    /// <summary>Knots by chord length.</summary>
    Chordal,

    /// <summary>Uniform knots, with <see cref="CatmullRomCurve3.Tension"/>.</summary>
    CatmullRom,
}

/// <summary>A smooth curve through a list of points.</summary>
public sealed class CatmullRomCurve3 : Curve3
{
    /// <summary>Creates the curve.</summary>
    public CatmullRomCurve3(IReadOnlyList<Vector3> points, bool closed = false, CatmullRomType curveType = CatmullRomType.Centripetal, float tension = 0.5f)
    {
        Points = points ?? throw new ArgumentNullException(nameof(points));
        Closed = closed;
        CurveType = curveType;
        Tension = tension;
    }

    /// <summary>The points the curve passes through (at least two).</summary>
    public IReadOnlyList<Vector3> Points { get; set; }

    /// <summary>True joins the last point back to the first.</summary>
    public bool Closed { get; set; }

    /// <summary>The knot spacing.</summary>
    public CatmullRomType CurveType { get; set; }

    /// <summary>Tangent strength of <see cref="CatmullRomType.CatmullRom"/>.</summary>
    public float Tension { get; set; }

    /// <inheritdoc/>
    public override Vector3 GetPoint(float t)
    {
        var points = Points;
        var l = points.Count;
        if (l == 0)
        {
            return Vector3.Zero;
        }

        if (l == 1)
        {
            return points[0];
        }

        var p = (l - (Closed ? 0 : 1)) * t;
        var intPoint = (int)MathF.Floor(p);
        var weight = p - intPoint;
        if (Closed)
        {
            intPoint += intPoint > 0 ? 0 : ((Math.Abs(intPoint) / l) + 1) * l;
        }
        else if (weight == 0f && intPoint == l - 1)
        {
            intPoint = l - 2;
            weight = 1f;
        }

        var p0 = Closed || intPoint > 0 ? points[(intPoint - 1 + l) % l] : points[0] - points[1] + points[0];
        var p1 = points[intPoint % l];
        var p2 = points[(intPoint + 1) % l];
        var p3 = Closed || intPoint + 2 < l ? points[(intPoint + 2) % l] : points[l - 1] - points[l - 2] + points[l - 1];
        Vector3 t1, t2;
        if (CurveType == CatmullRomType.CatmullRom)
        {
            t1 = (p2 - p0) * Tension;
            t2 = (p3 - p1) * Tension;
        }
        else
        {
            var pow = CurveType == CatmullRomType.Chordal ? 0.5f : 0.25f;
            var dt0 = MathF.Pow(Vector3.DistanceSquared(p0, p1), pow);
            var dt1 = MathF.Pow(Vector3.DistanceSquared(p1, p2), pow);
            var dt2 = MathF.Pow(Vector3.DistanceSquared(p2, p3), pow);
            if (dt1 < 1e-4f)
            {
                dt1 = 1f;
            }

            if (dt0 < 1e-4f)
            {
                dt0 = dt1;
            }

            if (dt2 < 1e-4f)
            {
                dt2 = dt1;
            }

            t1 = (((p1 - p0) / dt0) - ((p2 - p0) / (dt0 + dt1)) + ((p2 - p1) / dt1)) * dt1;
            t2 = (((p2 - p1) / dt1) - ((p3 - p1) / (dt1 + dt2)) + ((p3 - p2) / dt2)) * dt1;
        }

        // Cubic Hermite from p1 to p2 with tangents t1 and t2.
        var c2 = (p1 * -3f) + (p2 * 3f) - (t1 * 2f) - t2;
        var c3 = (p1 * 2f) - (p2 * 2f) + t1 + t2;
        var w2 = weight * weight;
        return p1 + (t1 * weight) + (c2 * w2) + (c3 * (w2 * weight));
    }
}

/// <summary>Several curves joined end to end, treated as one.</summary>
public sealed class CurvePath3 : Curve3
{
    private float[]? _curveLengths;

    /// <summary>The pieces, in order.</summary>
    public List<Curve3> Curves { get; } = [];

    /// <summary>Adds a piece.</summary>
    public CurvePath3 Add(Curve3 curve)
    {
        Curves.Add(curve ?? throw new ArgumentNullException(nameof(curve)));
        UpdateArcLengths();
        return this;
    }

    /// <summary>Adds a straight piece from the end back to the start when they differ.</summary>
    public CurvePath3 ClosePath()
    {
        if (Curves.Count > 0)
        {
            var start = Curves[0].GetPoint(0f);
            var end = Curves[^1].GetPoint(1f);
            if (start != end)
            {
                Add(new LineCurve3(end, start));
            }
        }

        return this;
    }

    /// <inheritdoc/>
    public override void UpdateArcLengths()
    {
        base.UpdateArcLengths();
        _curveLengths = null;
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
    public override Vector3 GetPoint(float t)
    {
        var lengths = CurveLengths();
        if (lengths.Length == 0)
        {
            return Vector3.Zero;
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
}
