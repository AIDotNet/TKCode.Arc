using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The 3D math of <see cref="CubeMascot"/>: the eight corners of a unit cube are rotated (yaw, roll, pitch, then the fixed
/// 32 degree camera) and projected orthographically; the silhouette is their convex hull; each face gets its normal in
/// view space, its shading against a light from the upper left, and (for the four sides) an affine frame that maps
/// face coordinates (-0.5 to 0.5, y down) onto the screen, through which the painted face is drawn. All buffers are
/// allocated once; <see cref="Update"/> allocates nothing.
/// </summary>
internal sealed class CubeMascotScene
{
    /// <summary>Face indices: the four sides clockwise seen from above starting at the face side, then top and bottom.</summary>
    public const int Front = 0;

    public const int Right = 1;

    public const int Back = 2;

    public const int Left = 3;

    public const int Top = 4;

    public const int Bottom = 5;

    public const int FaceCount = 6;

    // Corner i has x = ±0.5 by bit 0, y by bit 1, z by bit 2.
    private static readonly int[][] s_faceCorners =
    [
        [4, 5, 7, 6], // +z (front)
        [1, 3, 7, 5], // +x (right)
        [0, 2, 3, 1], // -z (back)
        [0, 4, 6, 2], // -x (left)
        [2, 6, 7, 3], // +y (top)
        [0, 1, 5, 4]  // -y (bottom)
    ];

    private static readonly double[][] s_normals =
    [
        [0, 0, 1], [1, 0, 0], [0, 0, -1], [-1, 0, 0], [0, 1, 0], [0, -1, 0]
    ];

    // Each side's "right" direction seen from outside the cube (its down direction is always -y).
    private static readonly double[][] s_sideRight =
    [
        [1, 0, 0], [0, 0, -1], [-1, 0, 0], [0, 0, 1]
    ];

    /// <summary>Each side's angle from the face side (degrees): the front is 0, the next side to arrive (left) is -90.</summary>
    private static readonly double[] s_sideAngle = [0, 90, 180, -90];

    // The light, in view space: from the upper left and toward the viewer.
    private static readonly double s_lx;
    private static readonly double s_ly;
    private static readonly double s_lz;

    /// <summary>The light term of the top, front and shaded side at rest: the three shading anchors.</summary>
    public static readonly double TopAnchor;

    public static readonly double FrontAnchor;

    public static readonly double SideAnchor;

    private readonly double[] _m = new double[9];
    private readonly double[] _cx = new double[8];
    private readonly double[] _cy = new double[8];
    private readonly int[] _order = new int[8];
    private readonly int[] _chain = new int[17];

    static CubeMascotScene()
    {
        var len = Math.Sqrt(0.42 * 0.42 + 0.78 * 0.78 + 0.46 * 0.46);
        s_lx = -0.42 / len;
        s_ly = 0.78 / len;
        s_lz = 0.46 / len;
        var rest = new CubeMascotScene();
        rest.Update(CubeMascotPose.HomeYaw, 0, 0, 0, 0, 1, default);
        TopAnchor = rest.Light[Top];
        FrontAnchor = rest.Light[Front];
        SideAnchor = rest.Light[Right];
    }

    /// <summary>The projected corners (px).</summary>
    public Point[] Corners { get; } = new Point[8];

    /// <summary>The corners of the silhouette (convex hull), counterclockwise on screen; <see cref="HullCount"/> are used.</summary>
    public Point[] Hull { get; } = new Point[8];

    /// <summary>How many points of <see cref="Hull"/> are used (4 or 6 for a cube).</summary>
    public int HullCount { get; private set; }

    /// <summary>The view-space z of each face's normal: positive faces the viewer (visible).</summary>
    public double[] Facing { get; } = new double[FaceCount];

    /// <summary>Each face's light term (its normal against the light).</summary>
    public double[] Light { get; } = new double[FaceCount];

    /// <summary>Each side's affine frame: face coordinates (-0.5 to 0.5, y down) to screen px.</summary>
    public Matrix[] SideFrames { get; } = new Matrix[4];

    /// <summary>Each side's angle from the front position (degrees, wrapped): 0 in front, negative while arriving.</summary>
    public double[] SideAngles { get; } = new double[4];

    /// <summary>The projected center of the cube (px).</summary>
    public Point Center { get; private set; }

    /// <summary>The corner indices of face <paramref name="face"/>.</summary>
    public static ReadOnlySpan<int> FaceCorners(int face) => s_faceCorners[face];

    /// <summary>
    /// Rotates and projects the cube: <paramref name="yaw"/>, <paramref name="roll"/> and <paramref name="pitch"/> in
    /// degrees, <paramref name="lift"/> and <paramref name="shift"/> in cube units, <paramref name="unit"/> px per cube
    /// unit and <paramref name="origin"/> the screen point of the cube's center at rest.
    /// </summary>
    public void Update(double yaw, double roll, double pitch, double lift, double shift, double unit, Point origin)
    {
        // Columns of M = Camera · Pitch · Roll · Yaw, built by rotating the basis vectors.
        for (var j = 0; j < 3; j++)
        {
            var x = j == 0 ? 1.0 : 0.0;
            var y = j == 1 ? 1.0 : 0.0;
            var z = j == 2 ? 1.0 : 0.0;
            Rotate(yaw, roll, pitch, ref x, ref y, ref z);
            _m[j] = x;
            _m[3 + j] = y;
            _m[6 + j] = z;
        }
        var cam = CubeMascotPose.CameraPitch * Math.PI / 180;
        var center = new Point(origin.X + shift * unit, origin.Y - lift * Math.Cos(cam) * unit);
        Center = center;
        for (var i = 0; i < 8; i++)
        {
            var bx = (i & 1) != 0 ? 0.5 : -0.5;
            var by = (i & 2) != 0 ? 0.5 : -0.5;
            var bz = (i & 4) != 0 ? 0.5 : -0.5;
            var (sx, sy, _) = Transform(bx, by, bz);
            _cx[i] = center.X + sx * unit;
            _cy[i] = center.Y - sy * unit;
            Corners[i] = new Point(_cx[i], _cy[i]);
        }
        for (var f = 0; f < FaceCount; f++)
        {
            var n = s_normals[f];
            var (nx, ny, nz) = Transform(n[0], n[1], n[2]);
            Facing[f] = nz;
            Light[f] = nx * s_lx + ny * s_ly + nz * s_lz;
        }
        for (var s = 0; s < 4; s++)
        {
            var n = s_normals[s];
            var r = s_sideRight[s];
            var (ux, uy, _) = Transform(r[0], r[1], r[2]);
            var (vx, vy, _) = Transform(0, -1, 0);
            var (cx, cy, _) = Transform(n[0] * 0.5, n[1] * 0.5, n[2] * 0.5);
            SideFrames[s] = new Matrix(ux * unit, -uy * unit, vx * unit, -vy * unit, center.X + cx * unit, center.Y - cy * unit);
            SideAngles[s] = CubeMascotPose.WrapSigned(yaw - CubeMascotPose.HomeYaw + s_sideAngle[s]);
        }
        BuildHull();
    }

    /// <summary>Projects a point on the ground (y = -0.5) by the camera alone (the floor does not lean with the cube).</summary>
    public static Point Ground(double x, double z, double unit, Point origin, double shift)
    {
        var cam = CubeMascotPose.CameraPitch * Math.PI / 180;
        const double y = -0.5;
        var vy = y * Math.Cos(cam) - z * Math.Sin(cam);
        return new Point(origin.X + (x + shift) * unit, origin.Y - vy * unit);
    }

    private (double X, double Y, double Z) Transform(double x, double y, double z) =>
        (_m[0] * x + _m[1] * y + _m[2] * z, _m[3] * x + _m[4] * y + _m[5] * z, _m[6] * x + _m[7] * y + _m[8] * z);

    private static void Rotate(double yaw, double roll, double pitch, ref double x, ref double y, ref double z)
    {
        // Yaw about the vertical axis.
        var a = yaw * Math.PI / 180;
        var (c, s) = (Math.Cos(a), Math.Sin(a));
        var x1 = x * c + z * s;
        var z1 = -x * s + z * c;
        x = x1;
        z = z1;
        // Roll about the view axis (positive leans left).
        a = roll * Math.PI / 180;
        (c, s) = (Math.Cos(a), Math.Sin(a));
        x1 = x * c - y * s;
        var y1 = x * s + y * c;
        x = x1;
        y = y1;
        // Pitch, then the camera, about the horizontal axis (positive tips the front down and shows the top).
        foreach (var degrees in (ReadOnlySpan<double>)[pitch, CubeMascotPose.CameraPitch])
        {
            a = degrees * Math.PI / 180;
            (c, s) = (Math.Cos(a), Math.Sin(a));
            y1 = y * c - z * s;
            z1 = y * s + z * c;
            y = y1;
            z = z1;
        }
    }

    // Andrew's monotone chain over the eight projected corners, into Hull (counterclockwise on screen).
    private void BuildHull()
    {
        for (var i = 0; i < 8; i++)
        {
            _order[i] = i;
        }
        for (var i = 1; i < 8; i++)
        {
            var k = _order[i];
            var j = i - 1;
            while (j >= 0 && Less(k, _order[j]))
            {
                _order[j + 1] = _order[j];
                j--;
            }
            _order[j + 1] = k;
        }
        var count = 0;
        for (var i = 0; i < 8; i++)
        {
            var p = _order[i];
            while (count >= 2 && Cross(_chain[count - 2], _chain[count - 1], p) <= 1e-9)
            {
                count--;
            }
            _chain[count++] = p;
        }
        var lower = count + 1;
        for (var i = 6; i >= 0; i--)
        {
            var p = _order[i];
            while (count >= lower && Cross(_chain[count - 2], _chain[count - 1], p) <= 1e-9)
            {
                count--;
            }
            _chain[count++] = p;
        }
        count--;
        count = Math.Min(count, Hull.Length);
        for (var i = 0; i < count; i++)
        {
            Hull[i] = Corners[_chain[i]];
        }
        HullCount = count;
    }

    private bool Less(int a, int b) => _cx[a] < _cx[b] - 1e-9 || (Math.Abs(_cx[a] - _cx[b]) <= 1e-9 && _cy[a] < _cy[b]);

    private double Cross(int o, int a, int b) =>
        (_cx[a] - _cx[o]) * (_cy[b] - _cy[o]) - (_cy[a] - _cy[o]) * (_cx[b] - _cx[o]);
}
