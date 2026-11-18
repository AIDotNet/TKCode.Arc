using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>An axis-aligned box.</summary>
public struct BoundingBox : IEquatable<BoundingBox>
{
    /// <summary>The minimum corner.</summary>
    public Vector3 Min;

    /// <summary>The maximum corner.</summary>
    public Vector3 Max;

    /// <summary>Creates a box from its corners.</summary>
    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>A box that contains nothing; expanding it by a point yields that point.</summary>
    public static BoundingBox Empty => new(new Vector3(float.PositiveInfinity), new Vector3(float.NegativeInfinity));

    /// <summary>True when the box contains no point.</summary>
    public readonly bool IsEmpty => Max.X < Min.X || Max.Y < Min.Y || Max.Z < Min.Z;

    /// <summary>The center, or zero when empty.</summary>
    public readonly Vector3 Center => IsEmpty ? Vector3.Zero : (Min + Max) * 0.5f;

    /// <summary>The extent along each axis, or zero when empty.</summary>
    public readonly Vector3 Size => IsEmpty ? Vector3.Zero : Max - Min;

    /// <summary>Grows the box to contain a point.</summary>
    public void Expand(Vector3 point)
    {
        Min = Vector3.Min(Min, point);
        Max = Vector3.Max(Max, point);
    }

    /// <summary>Grows the box to contain another box.</summary>
    public void Expand(in BoundingBox other)
    {
        if (other.IsEmpty)
        {
            return;
        }

        Min = Vector3.Min(Min, other.Min);
        Max = Vector3.Max(Max, other.Max);
    }

    /// <summary>The smallest box that contains every point.</summary>
    public static BoundingBox FromPoints(ReadOnlySpan<Vector3> points)
    {
        var box = Empty;
        foreach (var p in points)
        {
            box.Expand(p);
        }

        return box;
    }

    /// <summary>The box around this box after an affine transform.</summary>
    public readonly BoundingBox Transform(in Matrix4x4 m)
    {
        if (IsEmpty)
        {
            return this;
        }

        // Arvo's method: transform center and extents separately.
        var center = (Min + Max) * 0.5f;
        var extent = (Max - Min) * 0.5f;
        var c = Vector3.Transform(center, m);
        var e = new Vector3(
            (MathF.Abs(m.M11) * extent.X) + (MathF.Abs(m.M21) * extent.Y) + (MathF.Abs(m.M31) * extent.Z),
            (MathF.Abs(m.M12) * extent.X) + (MathF.Abs(m.M22) * extent.Y) + (MathF.Abs(m.M32) * extent.Z),
            (MathF.Abs(m.M13) * extent.X) + (MathF.Abs(m.M23) * extent.Y) + (MathF.Abs(m.M33) * extent.Z));
        return new BoundingBox(c - e, c + e);
    }

    /// <summary>True when the point lies inside or on the box.</summary>
    public readonly bool Contains(Vector3 p) =>
        p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;

    /// <summary>True when the two boxes overlap.</summary>
    public readonly bool Intersects(in BoundingBox o) =>
        Min.X <= o.Max.X && Max.X >= o.Min.X && Min.Y <= o.Max.Y && Max.Y >= o.Min.Y && Min.Z <= o.Max.Z && Max.Z >= o.Min.Z;

    /// <summary>The surface area (used by the BVH builder).</summary>
    public readonly float SurfaceArea
    {
        get
        {
            if (IsEmpty)
            {
                return 0f;
            }

            var s = Max - Min;
            return 2f * ((s.X * s.Y) + (s.Y * s.Z) + (s.Z * s.X));
        }
    }

    /// <inheritdoc/>
    public readonly bool Equals(BoundingBox other) => Min == other.Min && Max == other.Max;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is BoundingBox other && Equals(other);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);
}

/// <summary>A sphere.</summary>
public struct BoundingSphere
{
    /// <summary>The center.</summary>
    public Vector3 Center;

    /// <summary>The radius; negative means empty.</summary>
    public float Radius;

    /// <summary>Creates a sphere.</summary>
    public BoundingSphere(Vector3 center, float radius)
    {
        Center = center;
        Radius = radius;
    }

    /// <summary>A sphere that contains nothing.</summary>
    public static BoundingSphere Empty => new(Vector3.Zero, -1f);

    /// <summary>True when the sphere contains no point.</summary>
    public readonly bool IsEmpty => Radius < 0f;

    /// <summary>The sphere around a box.</summary>
    public static BoundingSphere FromBox(in BoundingBox box) =>
        box.IsEmpty ? Empty : new BoundingSphere(box.Center, (box.Max - box.Min).Length() * 0.5f);

    /// <summary>A sphere around points, centered on the center of their box.</summary>
    public static BoundingSphere FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty)
        {
            return Empty;
        }

        var center = BoundingBox.FromPoints(points).Center;
        var max = 0f;
        foreach (var p in points)
        {
            max = MathF.Max(max, Vector3.DistanceSquared(center, p));
        }

        return new BoundingSphere(center, MathF.Sqrt(max));
    }

    /// <summary>The sphere after an affine transform (the radius scales by the largest axis scale).</summary>
    public readonly BoundingSphere Transform(in Matrix4x4 m) =>
        IsEmpty ? this : new BoundingSphere(Vector3.Transform(Center, m), Radius * MathUtil.MaxScale(m));
}

/// <summary>A ray with a normalized direction.</summary>
public readonly struct Ray
{
    /// <summary>Where the ray starts.</summary>
    public readonly Vector3 Origin;

    /// <summary>The normalized direction.</summary>
    public readonly Vector3 Direction;

    /// <summary>Creates a ray; the direction is normalized.</summary>
    public Ray(Vector3 origin, Vector3 direction)
    {
        Origin = origin;
        Direction = Vector3.Normalize(direction);
    }

    private Ray(Vector3 origin, Vector3 direction, bool raw)
    {
        _ = raw;
        Origin = origin;
        Direction = direction;
    }

    /// <summary>The point at a distance along the ray.</summary>
    public Vector3 At(float t) => Origin + (Direction * t);

    /// <summary>The ray in another space. The direction is not normalized, so distances along the result equal
    /// distances along this ray (used to cast into an object's local space).</summary>
    public Ray TransformUnnormalized(in Matrix4x4 m) =>
        new(Vector3.Transform(Origin, m), Vector3.TransformNormal(Direction, m), raw: true);

    /// <summary>Distance to a sphere, or null when the ray misses it.</summary>
    public float? Intersect(in BoundingSphere sphere)
    {
        var oc = sphere.Center - Origin;
        var a = Vector3.Dot(Direction, Direction);
        var tca = Vector3.Dot(oc, Direction) / a;
        var d2 = Vector3.Dot(oc, oc) - (tca * tca * a);
        var r2 = sphere.Radius * sphere.Radius;
        if (d2 > r2)
        {
            return null;
        }

        var thc = MathF.Sqrt((r2 - d2) / a);
        var t0 = tca - thc;
        var t1 = tca + thc;
        if (t1 < 0f)
        {
            return null;
        }

        return t0 < 0f ? t1 : t0;
    }

    /// <summary>Entry distance into a box (0 when the origin is inside), or null when the ray misses it.</summary>
    public float? Intersect(in BoundingBox box)
    {
        var inv = Vector3.One / Direction;
        var t1 = (box.Min - Origin) * inv;
        var t2 = (box.Max - Origin) * inv;
        var tmin = Vector3.Min(t1, t2);
        var tmax = Vector3.Max(t1, t2);
        var near = MathF.Max(tmin.X, MathF.Max(tmin.Y, tmin.Z));
        var far = MathF.Min(tmax.X, MathF.Min(tmax.Y, tmax.Z));
        if (float.IsNaN(near) || float.IsNaN(far) || far < 0f || near > far)
        {
            return null;
        }

        return MathF.Max(near, 0f);
    }

    /// <summary>Möller–Trumbore ray/triangle test. Returns the distance, and the barycentric weights of b and c.</summary>
    public bool IntersectTriangle(Vector3 a, Vector3 b, Vector3 c, bool cullBackFaces, out float t, out float u, out float v)
    {
        t = 0f;
        u = 0f;
        v = 0f;
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(Direction, e2);
        var det = Vector3.Dot(e1, p);
        if (cullBackFaces)
        {
            if (det < 1e-12f)
            {
                return false;
            }
        }
        else if (MathF.Abs(det) < 1e-12f)
        {
            return false;
        }

        var invDet = 1f / det;
        var s = Origin - a;
        u = Vector3.Dot(s, p) * invDet;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        var q = Vector3.Cross(s, e1);
        v = Vector3.Dot(Direction, q) * invDet;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        t = Vector3.Dot(e2, q) * invDet;
        return t >= 0f;
    }

    /// <summary>Distance to a plane, or null when parallel or behind the origin.</summary>
    public float? Intersect(in Plane plane)
    {
        var denom = Vector3.Dot(plane.Normal, Direction);
        if (MathF.Abs(denom) < 1e-9f)
        {
            return null;
        }

        var t = -(Vector3.Dot(plane.Normal, Origin) + plane.D) / denom;
        return t >= 0f ? t : null;
    }
}

/// <summary>The six planes of a view volume, normals pointing inward.</summary>
public struct Frustum
{
    /// <summary>Left, right, bottom, top, near, far.</summary>
    public Plane P0, P1, P2, P3, P4, P5;

    /// <summary>Extracts the planes from a view-projection matrix whose clip depth is 0..1 (the engine's canonical
    /// projection of <see cref="Camera"/>).</summary>
    public static Frustum FromViewProjection(in Matrix4x4 m)
    {
        var c1 = new Vector4(m.M11, m.M21, m.M31, m.M41);
        var c2 = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var c3 = new Vector4(m.M13, m.M23, m.M33, m.M43);
        var c4 = new Vector4(m.M14, m.M24, m.M34, m.M44);
        Frustum f;
        f.P0 = Normalize(c4 + c1);
        f.P1 = Normalize(c4 - c1);
        f.P2 = Normalize(c4 + c2);
        f.P3 = Normalize(c4 - c2);
        f.P4 = Normalize(c3);
        f.P5 = Normalize(c4 - c3);
        return f;
    }

    private static Plane Normalize(Vector4 p)
    {
        var len = new Vector3(p.X, p.Y, p.Z).Length();
        return len > 0f ? new Plane(p / len) : new Plane(p);
    }

    /// <summary>True when the sphere is at least partly inside.</summary>
    public readonly bool Intersects(in BoundingSphere s)
    {
        var c = s.Center;
        var r = -s.Radius;
        return Plane.DotCoordinate(P0, c) >= r && Plane.DotCoordinate(P1, c) >= r && Plane.DotCoordinate(P2, c) >= r
            && Plane.DotCoordinate(P3, c) >= r && Plane.DotCoordinate(P4, c) >= r && Plane.DotCoordinate(P5, c) >= r;
    }

    /// <summary>True when the box is at least partly inside (conservative).</summary>
    public readonly bool Intersects(in BoundingBox b) =>
        Outside(P0, b) && Outside(P1, b) && Outside(P2, b) && Outside(P3, b) && Outside(P4, b) && Outside(P5, b);

    private static bool Outside(in Plane p, in BoundingBox b)
    {
        // The corner farthest along the plane normal; the box is out when even that corner is behind the plane.
        var v = new Vector3(p.Normal.X >= 0f ? b.Max.X : b.Min.X, p.Normal.Y >= 0f ? b.Max.Y : b.Min.Y, p.Normal.Z >= 0f ? b.Max.Z : b.Min.Z);
        return Plane.DotCoordinate(p, v) >= 0f;
    }
}
