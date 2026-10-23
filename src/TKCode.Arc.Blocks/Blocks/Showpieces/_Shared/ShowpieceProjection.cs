using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A 3D point or direction. The type has no fixed convention; each function that takes one says which it uses
/// (CSS 3D: x right, y down, z toward the viewer; the orbit view: y up; the sphere: z = north pole).
/// </summary>
internal readonly record struct ShowpieceVec3(double X, double Y, double Z)
{
    public static readonly ShowpieceVec3 Zero = new(0, 0, 0);

    public static ShowpieceVec3 operator +(ShowpieceVec3 a, ShowpieceVec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static ShowpieceVec3 operator -(ShowpieceVec3 a, ShowpieceVec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static ShowpieceVec3 operator -(ShowpieceVec3 a) => new(-a.X, -a.Y, -a.Z);

    public static ShowpieceVec3 operator *(ShowpieceVec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    public static ShowpieceVec3 operator *(double s, ShowpieceVec3 a) => new(a.X * s, a.Y * s, a.Z * s);

    public double Dot(ShowpieceVec3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public ShowpieceVec3 Cross(ShowpieceVec3 other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>The unit vector in the same direction (zero stays zero).</summary>
    public ShowpieceVec3 Normalized()
    {
        var length = Length;
        return length > 1e-12 ? this * (1 / length) : Zero;
    }
}

/// <summary>
/// A 3D affine transform (3×3 linear part and a translation) for column vectors: <c>p' = M·p + T</c>.
/// Rotations follow CSS 3D (x right, y down, z toward the viewer), so <see cref="RotationZ"/> turns clockwise on screen
/// like <c>rotateZ()</c>. Compose with <see cref="Then"/>: <c>a.Then(b)</c> applies <c>a</c> first. A CSS transform list
/// applies right to left, so <c>transform: translate3d(t) rotateY(θ)</c> is <c>RotationY(θ).Then(Translation(t))</c>.
/// </summary>
internal readonly struct ShowpieceTransform3D
{
    public readonly double M11, M12, M13, M21, M22, M23, M31, M32, M33;
    public readonly double Tx, Ty, Tz;

    public ShowpieceTransform3D(double m11, double m12, double m13, double m21, double m22, double m23,
                                double m31, double m32, double m33, double tx, double ty, double tz)
    {
        M11 = m11; M12 = m12; M13 = m13;
        M21 = m21; M22 = m22; M23 = m23;
        M31 = m31; M32 = m32; M33 = m33;
        Tx = tx; Ty = ty; Tz = tz;
    }

    public static readonly ShowpieceTransform3D Identity = new(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0);

    public static ShowpieceTransform3D Translation(double x, double y, double z) => new(1, 0, 0, 0, 1, 0, 0, 0, 1, x, y, z);

    public static ShowpieceTransform3D Translation(ShowpieceVec3 t) => Translation(t.X, t.Y, t.Z);

    public static ShowpieceTransform3D Scale(double s) => new(s, 0, 0, 0, s, 0, 0, 0, s, 0, 0, 0);

    public static ShowpieceTransform3D Scale(double x, double y, double z) => new(x, 0, 0, 0, y, 0, 0, 0, z, 0, 0, 0);

    /// <summary>CSS <c>rotateX(a)</c>: y' = y·cos a − z·sin a, z' = y·sin a + z·cos a.</summary>
    public static ShowpieceTransform3D RotationX(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceTransform3D(1, 0, 0, 0, c, -s, 0, s, c, 0, 0, 0);
    }

    /// <summary>CSS <c>rotateY(a)</c>: x' = x·cos a + z·sin a, z' = −x·sin a + z·cos a.</summary>
    public static ShowpieceTransform3D RotationY(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceTransform3D(c, 0, s, 0, 1, 0, -s, 0, c, 0, 0, 0);
    }

    /// <summary>CSS <c>rotateZ(a)</c>: x' = x·cos a − y·sin a, y' = x·sin a + y·cos a (clockwise on screen).</summary>
    public static ShowpieceTransform3D RotationZ(double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceTransform3D(c, -s, 0, s, c, 0, 0, 0, 1, 0, 0, 0);
    }

    /// <summary>The transform that applies this one first and then <paramref name="next"/>.</summary>
    public ShowpieceTransform3D Then(in ShowpieceTransform3D next) => new(
        next.M11 * M11 + next.M12 * M21 + next.M13 * M31,
        next.M11 * M12 + next.M12 * M22 + next.M13 * M32,
        next.M11 * M13 + next.M12 * M23 + next.M13 * M33,
        next.M21 * M11 + next.M22 * M21 + next.M23 * M31,
        next.M21 * M12 + next.M22 * M22 + next.M23 * M32,
        next.M21 * M13 + next.M22 * M23 + next.M23 * M33,
        next.M31 * M11 + next.M32 * M21 + next.M33 * M31,
        next.M31 * M12 + next.M32 * M22 + next.M33 * M32,
        next.M31 * M13 + next.M32 * M23 + next.M33 * M33,
        next.M11 * Tx + next.M12 * Ty + next.M13 * Tz + next.Tx,
        next.M21 * Tx + next.M22 * Ty + next.M23 * Tz + next.Ty,
        next.M31 * Tx + next.M32 * Ty + next.M33 * Tz + next.Tz);

    public ShowpieceVec3 Transform(ShowpieceVec3 p) => new(
        M11 * p.X + M12 * p.Y + M13 * p.Z + Tx,
        M21 * p.X + M22 * p.Y + M23 * p.Z + Ty,
        M31 * p.X + M32 * p.Y + M33 * p.Z + Tz);

    public ShowpieceVec3 Transform(double x, double y, double z) => Transform(new ShowpieceVec3(x, y, z));
}

/// <summary>Uniform scale-to-fit result: <c>screen = content · Scale + Offset</c>.</summary>
internal readonly record struct ShowpieceFit(double Scale, Vector Offset)
{
    public Point Apply(Point content) => new(content.X * Scale + Offset.X, content.Y * Scale + Offset.Y);
}

/// <summary>
/// Projection math shared by the Showpieces (pure functions, no allocation): CSS-style rotations and perspective,
/// planar homographies as Avalonia <see cref="Matrix"/> values (a plane under perspective is exactly a 3×3 projective
/// map, so a layer or card is drawn with one <c>RenderTransform</c>), their inverse (screen point back to the plane),
/// and bounds fitting. Orbit (pinhole) and sphere (orthographic) projections are <see cref="ShowpieceOrbitView"/> and
/// <see cref="ShowpieceSphereView"/>.
/// </summary>
/// <remarks>
/// Avalonia matrices act on row vectors: <c>[x y 1]·M = (X, Y, W)</c> and the point is <c>(X/W, Y/W)</c>, with the
/// perspective terms in <c>M13</c>/<c>M23</c> (<see cref="Matrix.Transform(Point)"/> divides by W).
/// </remarks>
internal static class ShowpieceProjection
{
    /// <summary>Degrees to radians.</summary>
    public static double Radians(double degrees) => degrees * Math.PI / 180;

    /// <summary>Radians to degrees.</summary>
    public static double Degrees(double radians) => radians * 180 / Math.PI;

    /// <summary>Wraps an angle to (−π, π].</summary>
    public static double WrapAngle(double radians)
    {
        var wrapped = Math.IEEERemainder(radians, 2 * Math.PI);
        return wrapped <= -Math.PI ? wrapped + 2 * Math.PI : wrapped;
    }

    /// <summary>CSS <c>rotateX</c> of a point (see <see cref="ShowpieceTransform3D.RotationX"/>).</summary>
    public static ShowpieceVec3 RotateX(ShowpieceVec3 p, double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceVec3(p.X, p.Y * c - p.Z * s, p.Y * s + p.Z * c);
    }

    /// <summary>CSS <c>rotateY</c> of a point.</summary>
    public static ShowpieceVec3 RotateY(ShowpieceVec3 p, double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceVec3(p.X * c + p.Z * s, p.Y, -p.X * s + p.Z * c);
    }

    /// <summary>CSS <c>rotateZ</c> of a point.</summary>
    public static ShowpieceVec3 RotateZ(ShowpieceVec3 p, double radians)
    {
        var c = Math.Cos(radians);
        var s = Math.Sin(radians);
        return new ShowpieceVec3(p.X * c - p.Y * s, p.X * s + p.Y * c, p.Z);
    }

    /// <summary>
    /// CSS <c>perspective(d)</c> with its origin at <paramref name="origin"/>: a point at depth z (toward the viewer
    /// positive) lands at <c>origin + (p − origin)·d/(d − z)</c>. An infinite distance is an orthographic projection.
    /// </summary>
    public static Point Perspective(ShowpieceVec3 p, double distance, Point origin = default)
    {
        var f = PerspectiveScale(p.Z, distance);
        return new Point(origin.X + (p.X - origin.X) * f, origin.Y + (p.Y - origin.Y) * f);
    }

    /// <summary>The CSS perspective scale <c>d/(d − z)</c> (1 for an infinite distance).</summary>
    public static double PerspectiveScale(double z, double distance)
        => double.IsPositiveInfinity(distance) ? 1 : distance / (distance - z);

    /// <summary>
    /// The homography that maps a plane's local 2D coordinates (its z = 0 plane before <paramref name="planeToScene"/>)
    /// to the projected 2D scene under CSS <paramref name="perspective"/> with origin <paramref name="origin"/>. Exact:
    /// <c>Apply(matrix, (u, v))</c> equals <c>Perspective(planeToScene.Transform(u, v, 0), perspective, origin)</c>.
    /// Pass <see cref="double.PositiveInfinity"/> for an orthographic projection.
    /// </summary>
    public static Matrix PlaneMatrix(in ShowpieceTransform3D planeToScene, double perspective, Point origin = default)
    {
        // P = u·a + v·b + t with a, b the first two columns. Homogeneous image of P: (d·Px − ox·Pz, d·Py − oy·Pz, d − Pz).
        double ax = planeToScene.M11, ay = planeToScene.M21, az = planeToScene.M31;
        double bx = planeToScene.M12, by = planeToScene.M22, bz = planeToScene.M32;
        double tx = planeToScene.Tx, ty = planeToScene.Ty, tz = planeToScene.Tz;
        if (double.IsPositiveInfinity(perspective))
        {
            return new Matrix(ax, ay, 0, bx, by, 0, tx, ty, 1);
        }
        var d = perspective;
        double ox = origin.X, oy = origin.Y;
        var m33 = d - tz;
        var scale = Math.Abs(m33) > 1e-12 ? 1 / m33 : 1;
        return new Matrix(
            (d * ax - ox * az) * scale, (d * ay - oy * az) * scale, -az * scale,
            (d * bx - ox * bz) * scale, (d * by - oy * bz) * scale, -bz * scale,
            (d * tx - ox * tz) * scale, (d * ty - oy * tz) * scale, m33 * scale);
    }

    /// <summary>A 2D similarity (uniform scale then translation) as a matrix, for appending after a projection.</summary>
    public static Matrix ScaleThenTranslate(double scale, Vector translation) =>
        new(scale, 0, 0, 0, scale, 0, translation.X, translation.Y, 1);

    /// <summary>Full 3×3 product: the matrix that applies <paramref name="first"/> and then <paramref name="then"/>.</summary>
    public static Matrix Multiply(in Matrix first, in Matrix then) => new(
        first.M11 * then.M11 + first.M12 * then.M21 + first.M13 * then.M31,
        first.M11 * then.M12 + first.M12 * then.M22 + first.M13 * then.M32,
        first.M11 * then.M13 + first.M12 * then.M23 + first.M13 * then.M33,
        first.M21 * then.M11 + first.M22 * then.M21 + first.M23 * then.M31,
        first.M21 * then.M12 + first.M22 * then.M22 + first.M23 * then.M32,
        first.M21 * then.M13 + first.M22 * then.M23 + first.M23 * then.M33,
        first.M31 * then.M11 + first.M32 * then.M21 + first.M33 * then.M31,
        first.M31 * then.M12 + first.M32 * then.M22 + first.M33 * then.M32,
        first.M31 * then.M13 + first.M32 * then.M23 + first.M33 * then.M33);

    /// <summary>Applies a projective matrix to a point, dividing by w (same as <see cref="Matrix.Transform(Point)"/>).</summary>
    public static Point Apply(in Matrix m, Point p)
    {
        var x = p.X * m.M11 + p.Y * m.M21 + m.M31;
        var y = p.X * m.M12 + p.Y * m.M22 + m.M32;
        var w = p.X * m.M13 + p.Y * m.M23 + m.M33;
        if (Math.Abs(w) < 1e-12)
        {
            w = 1e-12;
        }
        return new Point(x / w, y / w);
    }

    /// <summary>
    /// The homography that maps the four points <paramref name="from"/> onto <paramref name="to"/> (in order). Returns
    /// false when the points are degenerate (three on a line). Solves the 8×8 system on the stack.
    /// </summary>
    public static bool TryHomography(ReadOnlySpan<Point> from, ReadOnlySpan<Point> to, out Matrix matrix)
    {
        if (from.Length != 4 || to.Length != 4)
        {
            throw new ArgumentException("A homography needs exactly four point pairs.");
        }
        // Unknowns (row-vector convention): M11, M21, M31, M12, M22, M32, M13, M23 with M33 = 1.
        Span<double> a = stackalloc double[8 * 9];
        for (var i = 0; i < 4; i++)
        {
            double x = from[i].X, y = from[i].Y, u = to[i].X, v = to[i].Y;
            var r = 2 * i * 9;
            a[r + 0] = x; a[r + 1] = y; a[r + 2] = 1; a[r + 3] = 0; a[r + 4] = 0; a[r + 5] = 0;
            a[r + 6] = -u * x; a[r + 7] = -u * y; a[r + 8] = u;
            r += 9;
            a[r + 0] = 0; a[r + 1] = 0; a[r + 2] = 0; a[r + 3] = x; a[r + 4] = y; a[r + 5] = 1;
            a[r + 6] = -v * x; a[r + 7] = -v * y; a[r + 8] = v;
        }
        Span<double> h = stackalloc double[8];
        if (!Solve(a, 8, h))
        {
            matrix = Matrix.Identity;
            return false;
        }
        matrix = new Matrix(h[0], h[3], h[6], h[1], h[4], h[7], h[2], h[5], 1);
        return true;
    }

    /// <summary>Gauss–Jordan elimination with partial pivoting on an n×(n+1) augmented matrix.</summary>
    private static bool Solve(Span<double> a, int n, Span<double> result)
    {
        var stride = n + 1;
        for (var col = 0; col < n; col++)
        {
            var pivot = col;
            var best = Math.Abs(a[col * stride + col]);
            for (var row = col + 1; row < n; row++)
            {
                var value = Math.Abs(a[row * stride + col]);
                if (value > best)
                {
                    best = value;
                    pivot = row;
                }
            }
            if (best < 1e-12)
            {
                return false;
            }
            if (pivot != col)
            {
                for (var k = 0; k < stride; k++)
                {
                    (a[col * stride + k], a[pivot * stride + k]) = (a[pivot * stride + k], a[col * stride + k]);
                }
            }
            var inv = 1 / a[col * stride + col];
            for (var k = col; k < stride; k++)
            {
                a[col * stride + k] *= inv;
            }
            for (var row = 0; row < n; row++)
            {
                if (row == col)
                {
                    continue;
                }
                var factor = a[row * stride + col];
                if (factor == 0)
                {
                    continue;
                }
                for (var k = col; k < stride; k++)
                {
                    a[row * stride + k] -= factor * a[col * stride + k];
                }
            }
        }
        for (var i = 0; i < n; i++)
        {
            result[i] = a[i * stride + n];
        }
        return true;
    }

    /// <summary>The full 3×3 inverse of a projective matrix; false when it is singular.</summary>
    public static bool TryInvert(in Matrix m, out Matrix inverse)
    {
        var c11 = m.M22 * m.M33 - m.M23 * m.M32;
        var c12 = m.M13 * m.M32 - m.M12 * m.M33;
        var c13 = m.M12 * m.M23 - m.M13 * m.M22;
        var det = m.M11 * c11 + m.M21 * c12 + m.M31 * c13;
        if (Math.Abs(det) < 1e-15)
        {
            inverse = Matrix.Identity;
            return false;
        }
        var invDet = 1 / det;
        inverse = new Matrix(
            c11 * invDet,
            c12 * invDet,
            c13 * invDet,
            (m.M23 * m.M31 - m.M21 * m.M33) * invDet,
            (m.M11 * m.M33 - m.M13 * m.M31) * invDet,
            (m.M13 * m.M21 - m.M11 * m.M23) * invDet,
            (m.M21 * m.M32 - m.M22 * m.M31) * invDet,
            (m.M12 * m.M31 - m.M11 * m.M32) * invDet,
            (m.M11 * m.M22 - m.M12 * m.M21) * invDet);
        return true;
    }

    /// <summary>
    /// Maps a screen point back onto the plane of a homography (for hit tests on a projected layer or card). False when
    /// the matrix is singular or the point lies on the plane's horizon.
    /// </summary>
    public static bool TryUnproject(in Matrix homography, Point screen, out Point plane)
    {
        if (!TryInvert(homography, out var inverse))
        {
            plane = default;
            return false;
        }
        var w = screen.X * inverse.M13 + screen.Y * inverse.M23 + inverse.M33;
        if (Math.Abs(w) < 1e-12)
        {
            plane = default;
            return false;
        }
        plane = new Point((screen.X * inverse.M11 + screen.Y * inverse.M21 + inverse.M31) / w,
            (screen.X * inverse.M12 + screen.Y * inverse.M22 + inverse.M32) / w);
        return true;
    }

    /// <summary>The axis-aligned bounds of a set of points (empty rect for none).</summary>
    public static Rect Bounds(ReadOnlySpan<Point> points)
    {
        if (points.IsEmpty)
        {
            return default;
        }
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var p in points)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// The uniform scale that fits <paramref name="content"/> into <paramref name="frame"/> (times
    /// <paramref name="fill"/>) and the offset that centers it there.
    /// </summary>
    public static ShowpieceFit Fit(Rect content, Rect frame, double fill = 1)
    {
        if (content.Width <= 0 && content.Height <= 0)
        {
            return new ShowpieceFit(1, frame.Center - content.Center);
        }
        var sx = content.Width > 0 ? frame.Width / content.Width : double.PositiveInfinity;
        var sy = content.Height > 0 ? frame.Height / content.Height : double.PositiveInfinity;
        var scale = Math.Min(sx, sy) * fill;
        var center = content.Center;
        return new ShowpieceFit(scale, new Vector(frame.Center.X - center.X * scale, frame.Center.Y - center.Y * scale));
    }

    /// <summary>A unit vector on the sphere (x = cos φ cos λ, y = cos φ sin λ, z = sin φ; z is the north pole).</summary>
    public static ShowpieceVec3 LatLonToUnit(double latitudeDegrees, double longitudeDegrees)
    {
        var phi = Radians(latitudeDegrees);
        var lambda = Radians(longitudeDegrees);
        var c = Math.Cos(phi);
        return new ShowpieceVec3(c * Math.Cos(lambda), c * Math.Sin(lambda), Math.Sin(phi));
    }
}

/// <summary>
/// A pinhole orbit view (ActivityTerrain): the camera circles <see cref="Target"/> at <see cref="Distance"/>, turned by
/// yaw about the up axis and tilted down by pitch; world y is up. With p = P − T:
/// <c>xc = p.x·cos yaw + p.z·sin yaw</c>, <c>zc = −p.x·sin yaw + p.z·cos yaw</c>; with a = −pitch,
/// <c>y2 = p.y·cos a + zc·sin a</c>, <c>z2 = −p.y·sin a + zc·cos a</c>; <c>depth = distance − z2</c>;
/// <c>screen = center + f·(xc, −y2)/depth</c>. The target always lands on <see cref="Center"/>.
/// </summary>
internal readonly struct ShowpieceOrbitView
{
    private readonly double _cosYaw, _sinYaw, _cosA, _sinA;

    public ShowpieceOrbitView(double yaw, double pitch, double distance, ShowpieceVec3 target, double focal, Point center)
    {
        Yaw = yaw;
        Pitch = pitch;
        Distance = distance;
        Target = target;
        Focal = focal;
        Center = center;
        _cosYaw = Math.Cos(yaw);
        _sinYaw = Math.Sin(yaw);
        _cosA = Math.Cos(-pitch);
        _sinA = Math.Sin(-pitch);
    }

    public double Yaw { get; }
    public double Pitch { get; }
    public double Distance { get; }
    public ShowpieceVec3 Target { get; }

    /// <summary>Focal length in px (the zoom multiplies it; the camera never dollies).</summary>
    public double Focal { get; }

    /// <summary>Where the target projects (screen px).</summary>
    public Point Center { get; }

    /// <summary>Projects a world point. Returns false when it is at or behind the camera.</summary>
    public bool Project(ShowpieceVec3 world, out Point screen, out double depth)
    {
        double px = world.X - Target.X, py = world.Y - Target.Y, pz = world.Z - Target.Z;
        var xc = px * _cosYaw + pz * _sinYaw;
        var zc = -px * _sinYaw + pz * _cosYaw;
        var y2 = py * _cosA + zc * _sinA;
        var z2 = -py * _sinA + zc * _cosA;
        depth = Distance - z2;
        if (depth <= 1e-9)
        {
            screen = default;
            return false;
        }
        var f = Focal / depth;
        screen = new Point(Center.X + xc * f, Center.Y - y2 * f);
        return true;
    }

    /// <summary>Camera-space depth of a world point (larger is farther): the painter's sort key.</summary>
    public double DepthOf(ShowpieceVec3 world)
    {
        double px = world.X - Target.X, py = world.Y - Target.Y, pz = world.Z - Target.Z;
        var zc = -px * _sinYaw + pz * _cosYaw;
        return Distance - (-py * _sinA + zc * _cosA);
    }

    /// <summary>
    /// Casts the ray through <paramref name="screen"/> onto the horizontal plane <c>y = planeY</c> (the floor). False when
    /// the ray is parallel to the plane or meets it behind the camera.
    /// </summary>
    public bool TryUnprojectToPlane(Point screen, double planeY, out ShowpieceVec3 world)
    {
        // Camera-space point at a given depth: xc = dx·depth/f, y2 = −dy·depth/f, z2 = distance − depth.
        var dx = (screen.X - Center.X) / Focal;
        var dy = -(screen.Y - Center.Y) / Focal;
        // world(depth) = Eye + depth·Dir, both from the inverse rotations (undo pitch, then yaw).
        var eye = ToWorld(0, 0, Distance);
        var along = ToWorld(dx, dy, -1) - ToWorld(0, 0, 0);
        if (Math.Abs(along.Y) < 1e-12)
        {
            world = default;
            return false;
        }
        var depth = (planeY - eye.Y) / along.Y;
        if (depth <= 0)
        {
            world = default;
            return false;
        }
        world = eye + along * depth;
        return true;
    }

    private ShowpieceVec3 ToWorld(double xc, double y2, double z2)
    {
        var py = y2 * _cosA - z2 * _sinA;
        var zc = y2 * _sinA + z2 * _cosA;
        var px = xc * _cosYaw - zc * _sinYaw;
        var pz = xc * _sinYaw + zc * _cosYaw;
        return new ShowpieceVec3(px + Target.X, py + Target.Y, pz + Target.Z);
    }
}

/// <summary>
/// An orthographic view of a sphere (RevenueGlobe) looking at longitude λc and latitude φc, north up:
/// <c>x = cx + R·cos φ·sin(λ − λc)</c>, <c>y = cy − R·(sin φ·cos φc − cos φ·sin φc·cos(λ − λc))</c>,
/// <c>depth = sin φ·sin φc + cos φ·cos φc·cos(λ − λc)</c> (≤ 0 is the far side). Unit vectors come from
/// <see cref="ShowpieceProjection.LatLonToUnit"/>; projecting one costs a handful of multiplications.
/// </summary>
internal readonly struct ShowpieceSphereView
{
    private readonly double _cosLon, _sinLon, _cosLat, _sinLat;

    public ShowpieceSphereView(double centerLongitude, double centerLatitude, double radius, Point center)
    {
        CenterLongitude = centerLongitude;
        CenterLatitude = centerLatitude;
        Radius = radius;
        Center = center;
        _cosLon = Math.Cos(centerLongitude);
        _sinLon = Math.Sin(centerLongitude);
        _cosLat = Math.Cos(centerLatitude);
        _sinLat = Math.Sin(centerLatitude);
    }

    /// <summary>λc in radians.</summary>
    public double CenterLongitude { get; }

    /// <summary>φc in radians.</summary>
    public double CenterLatitude { get; }

    public double Radius { get; }

    public Point Center { get; }

    /// <summary>Projects a unit vector; returns its depth (≤ 0 on the far side; the point is still computed).</summary>
    public double Project(ShowpieceVec3 unit, out Point screen)
    {
        var a = unit.X * _cosLon + unit.Y * _sinLon;
        var b = -unit.X * _sinLon + unit.Y * _cosLon;
        var up = unit.Z * _cosLat - a * _sinLat;
        screen = new Point(Center.X + Radius * b, Center.Y - Radius * up);
        return unit.Z * _sinLat + a * _cosLat;
    }

    /// <summary>Projects a latitude / longitude in degrees.</summary>
    public double Project(double latitudeDegrees, double longitudeDegrees, out Point screen)
        => Project(ShowpieceProjection.LatLonToUnit(latitudeDegrees, longitudeDegrees), out screen);

    /// <summary>The unit vector on the near side under a screen point; false outside the disc.</summary>
    public bool TryUnproject(Point screen, out ShowpieceVec3 unit)
    {
        if (Radius <= 0)
        {
            unit = default;
            return false;
        }
        var b = (screen.X - Center.X) / Radius;
        var up = -(screen.Y - Center.Y) / Radius;
        var rest = 1 - b * b - up * up;
        if (rest < 0)
        {
            unit = default;
            return false;
        }
        var depth = Math.Sqrt(rest);
        // Invert the view rotation: (a, up, depth) → (unit.x, unit.y, unit.z).
        var z = up * _cosLat + depth * _sinLat;
        var a = -up * _sinLat + depth * _cosLat;
        unit = new ShowpieceVec3(a * _cosLon - b * _sinLon, a * _sinLon + b * _cosLon, z);
        return true;
    }
}
