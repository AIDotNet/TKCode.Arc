using Avalonia;

namespace TKCode.Arc;

/// <summary>A point or direction in plate space: x right, y down, z towards the viewer, all in plate widths.</summary>
internal readonly record struct ArmingVec(double X, double Y, double Z)
{
    public static ArmingVec operator +(ArmingVec a, ArmingVec b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static ArmingVec operator -(ArmingVec a, ArmingVec b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static ArmingVec operator *(ArmingVec a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>Turns about the x axis by <paramref name="degrees"/> as CSS rotateX does (y down, z out of the screen).</summary>
    public ArmingVec RotateX(double degrees)
    {
        var r = degrees * Math.PI / 180;
        var c = Math.Cos(r);
        var s = Math.Sin(r);
        return new ArmingVec(X, (Y * c) - (Z * s), (Y * s) + (Z * c));
    }
}

/// <summary>
/// The camera of the plate: the panel's lean (rotateX then rotateY about the plate's centre, as Arc's panel tilts) and a
/// perspective three plate widths deep, seen from a point above the cover's hinge. It maps plate space to stage pixels,
/// and a flat face to a projective <see cref="Matrix"/>, so every part is drawn flat in its own units and pushed through
/// that matrix (Skia draws the perspective).
/// </summary>
internal sealed class ArmingSwitchView
{
    // Arc Arming switch (design reference): perspective 3 W, origin (0.6, 0.1095) W, panel centre (0.5, 0.31) W.
    public const double PlateHeight = 0.62;
    private const double Depth = 3;
    private const double OriginX = 0.6;
    private const double OriginY = 0.1095;
    private const double CenterX = 0.5;
    private const double CenterY = 0.31;

    private double _r00 = 1, _r01, _r02, _r10, _r11 = 1, _r12, _r20, _r21, _r22 = 1;

    /// <summary>Pixels per plate width.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>The plate's top-left corner in stage pixels (before the lean).</summary>
    public Point Offset { get; private set; }

    /// <summary>Sets the plate's size and place and the panel's lean in degrees (positive x brings the bottom closer).</summary>
    public void Update(double scale, Point offset, double rotateX, double rotateY)
    {
        Scale = scale;
        Offset = offset;
        var a = rotateX * Math.PI / 180;
        var b = rotateY * Math.PI / 180;
        double ca = Math.Cos(a), sa = Math.Sin(a), cb = Math.Cos(b), sb = Math.Sin(b);
        // R = RX(a) · RY(b) (column vectors), as CSS "rotateX(a) rotateY(b)".
        _r00 = cb;
        _r01 = 0;
        _r02 = sb;
        _r10 = sa * sb;
        _r11 = ca;
        _r12 = -sa * cb;
        _r20 = -ca * sb;
        _r21 = sa;
        _r22 = ca * cb;
    }

    /// <summary>The leaned position of a plate point (still in plate widths).</summary>
    public ArmingVec Lean(ArmingVec p)
    {
        var x = p.X - CenterX;
        var y = p.Y - CenterY;
        return new ArmingVec(
            CenterX + (_r00 * x) + (_r01 * y) + (_r02 * p.Z),
            CenterY + (_r10 * x) + (_r11 * y) + (_r12 * p.Z),
            (_r20 * x) + (_r21 * y) + (_r22 * p.Z));
    }

    private ArmingVec LeanDirection(ArmingVec d) => new(
        (_r00 * d.X) + (_r01 * d.Y) + (_r02 * d.Z),
        (_r10 * d.X) + (_r11 * d.Y) + (_r12 * d.Z),
        (_r20 * d.X) + (_r21 * d.Y) + (_r22 * d.Z));

    /// <summary>How much nearer than the plate a point is after the lean (larger draws later).</summary>
    public double DepthOf(ArmingVec p) => Lean(p).Z;

    /// <summary>The perspective scale of a point (1 on the plate's plane at rest).</summary>
    public double PerspectiveAt(ArmingVec p) => Depth / (Depth - Lean(p).Z);

    /// <summary>Projects a plate point to stage pixels.</summary>
    public Point Project(ArmingVec p)
    {
        var q = Lean(p);
        var s = Depth / (Depth - q.Z);
        var x = OriginX + ((q.X - OriginX) * s);
        var y = OriginY + ((q.Y - OriginY) * s);
        return new Point(Offset.X + (x * Scale), Offset.Y + (y * Scale));
    }

    /// <summary>
    /// The matrix that draws a flat face: a point (u, v) of the face lands on origin + u·uAxis + v·vAxis in plate space,
    /// then is leaned, projected and scaled to stage pixels. <paramref name="unit"/> scales (u, v) first (pass 1 / Scale to
    /// draw the face in pixels, for text).
    /// </summary>
    public Matrix Face(ArmingVec origin, ArmingVec uAxis, ArmingVec vAxis, double unit = 1)
    {
        var a = Lean(origin);
        var u = LeanDirection(uAxis * unit);
        var v = LeanDirection(vAxis * unit);
        const double k = 1 / Depth;
        // Homogeneous: X·w = x - ox·z/d, Y·w = y - oy·z/d, w = 1 - z/d; rows act on [u v 1].
        var m11 = u.X - (OriginX * u.Z * k);
        var m12 = u.Y - (OriginY * u.Z * k);
        var m13 = -u.Z * k;
        var m21 = v.X - (OriginX * v.Z * k);
        var m22 = v.Y - (OriginY * v.Z * k);
        var m23 = -v.Z * k;
        var m31 = a.X - (OriginX * a.Z * k);
        var m32 = a.Y - (OriginY * a.Z * k);
        var m33 = 1 - (a.Z * k);
        var s = Scale;
        var ox = Offset.X;
        var oy = Offset.Y;
        // Then screen = plate·Scale + Offset, applied to the homogeneous row: (X·w)·s + ox·w.
        return new Matrix(
            (m11 * s) + (ox * m13), (m12 * s) + (oy * m13), m13,
            (m21 * s) + (ox * m23), (m22 * s) + (oy * m23), m23,
            (m31 * s) + (ox * m33), (m32 * s) + (oy * m33), m33);
    }

    /// <summary>The matrix of the plane parallel to the plate at height <paramref name="z"/>, in plate units.</summary>
    public Matrix Plane(double z = 0, double unit = 1) =>
        Face(new ArmingVec(0, 0, z), new ArmingVec(1, 0, 0), new ArmingVec(0, 1, 0), unit);
}
