using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The perspective of a <see cref="CoverFlow"/> cover as one projective <see cref="Matrix"/>: the cover (local box
/// 0..width × 0..height) is scaled about its centre, turned about its vertical centre line by a yaw, seen through a
/// perspective with the eye in front of the cover's resting centre, and moved sideways. A turned plane under perspective
/// is exactly a 3×3 projective map, so the picture stays sharp. Pure functions without allocation.
/// </summary>
/// <remarks>
/// Axes: x right, y down, z toward the viewer. A positive yaw sends the cover's right edge away from the viewer, so covers
/// on the right of the centre use a positive yaw and face the middle. Avalonia matrices act on row vectors with the
/// perspective terms in <c>M13</c>/<c>M23</c>/<c>M33</c>.
/// </remarks>
internal static class CoverFlowProjection
{
    /// <summary>
    /// The matrix that draws a cover of <paramref name="width"/> × <paramref name="height"/>, laid out on the stage's
    /// centre line, moved <paramref name="offsetX"/> px sideways, scaled by <paramref name="scale"/> and turned by
    /// <paramref name="yawDegrees"/>, seen from <paramref name="perspective"/> px away.
    /// </summary>
    public static Matrix CoverMatrix(double width, double height, double offsetX, double scale, double yawDegrees, double perspective)
    {
        var phi = yawDegrees * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var cx = width / 2;
        var cy = height / 2;
        var d = perspective > 1 && double.IsFinite(perspective) ? perspective : 1e6;
        // Relative to the centre (x = u − cx, y = v − cy): X' = s·cos·x, Y' = s·y, Z' = −s·sin·x. Through the perspective
        // the image is (cx + tx + d·X'/W, cy + d·Y'/W) with W = d − Z'; in homogeneous form, linear in (u, v, 1).
        var wu = scale * sin;
        var w1 = d - scale * sin * cx;
        var xu = (cx + offsetX) * wu + d * scale * cos;
        var x1 = (cx + offsetX) * w1 - d * scale * cos * cx;
        var yu = cy * wu;
        var yv = d * scale;
        var y1 = cy * w1 - d * scale * cy;
        var k = 1 / w1;
        return new Matrix(xu * k, yu * k, wu * k, 0, yv * k, 0, x1 * k, y1 * k, 1);
    }

    /// <summary>Applies a projective matrix to a point (divides by w).</summary>
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

    /// <summary>The point of the cover's own box drawn at <paramref name="screen"/>; false when the map cannot be inverted.</summary>
    public static bool TryUnproject(in Matrix m, Point screen, out Point local)
    {
        // The inverse of the 3×3 map by its adjugate (row-vector convention).
        double a = m.M11, b = m.M12, c = m.M13, d = m.M21, e = m.M22, f = m.M23, g = m.M31, h = m.M32, i = m.M33;
        var det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        if (Math.Abs(det) < 1e-12)
        {
            local = default;
            return false;
        }
        var i11 = (e * i - f * h) / det;
        var i12 = (c * h - b * i) / det;
        var i13 = (b * f - c * e) / det;
        var i21 = (f * g - d * i) / det;
        var i22 = (a * i - c * g) / det;
        var i23 = (c * d - a * f) / det;
        var i31 = (d * h - e * g) / det;
        var i32 = (b * g - a * h) / det;
        var i33 = (a * e - b * d) / det;
        var x = screen.X * i11 + screen.Y * i21 + i31;
        var y = screen.X * i12 + screen.Y * i22 + i32;
        var w = screen.X * i13 + screen.Y * i23 + i33;
        if (Math.Abs(w) < 1e-12)
        {
            local = default;
            return false;
        }
        local = new Point(x / w, y / w);
        return true;
    }

    /// <summary>
    /// Where a cover sits at <paramref name="d"/> covers from the centre (negative is left): its sideways offset in px,
    /// its yaw in degrees and its scale. Between the centre and the first neighbour everything blends, so a drag turns
    /// the covers smoothly.
    /// </summary>
    public static (double OffsetX, double Yaw, double Scale) Pose(double d, double coverWidth, double centerGap, double sideSpacing,
                                                                 double tilt, double sideScale)
    {
        var a = Math.Abs(d);
        var near = Math.Min(1, a);
        var x = a <= 1 ? a * centerGap * coverWidth : centerGap * coverWidth + (a - 1) * sideSpacing;
        return (Math.Sign(d) * x, Math.Sign(d) * near * tilt, 1 - (1 - sideScale) * near);
    }
}
