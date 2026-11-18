using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Shared code of the helper objects.</summary>
internal static class HelperUtil
{
    public static readonly ColorF Yellow = ColorF.FromHex(0xffff00);

    public static LineMaterial Lines(ColorF color, bool vertexColors = false) =>
        new() { Color = color, VertexColors = vertexColors, ToneMapped = false, Fog = false };

    public static void Put(float[] data, int index, Vector3 v)
    {
        data[index * 3] = v.X;
        data[(index * 3) + 1] = v.Y;
        data[(index * 3) + 2] = v.Z;
    }

    public static void Put(float[] data, int index, ColorF c)
    {
        data[index * 3] = c.R;
        data[(index * 3) + 1] = c.G;
        data[(index * 3) + 2] = c.B;
    }

    /// <summary>The rotation that turns local +Y into a direction.</summary>
    public static Quaternion FromUnitY(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-12f)
        {
            return Quaternion.Identity;
        }

        direction = Vector3.Normalize(direction);
        if (direction.Y > 0.99999f)
        {
            return Quaternion.Identity;
        }

        if (direction.Y < -0.99999f)
        {
            return new Quaternion(1f, 0f, 0f, 0f);
        }

        var axis = Vector3.Normalize(new Vector3(direction.Z, 0f, -direction.X));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(direction.Y));
    }

    /// <summary>The world-space box around every renderable object below (and including) a node.</summary>
    public static BoundingBox WorldBounds(Object3D root)
    {
        var box = BoundingBox.Empty;
        root.Traverse(o =>
        {
            if (o is Renderable r)
            {
                box.Expand(r.LocalBoundingBox.Transform(r.WorldMatrix));
            }
        });
        return box;
    }
}

/// <summary>A square grid of lines in the XZ plane, centered on the origin.</summary>
public class GridHelper : LineSegments
{
    /// <summary>Creates a grid.</summary>
    /// <param name="size">Length of each side.</param>
    /// <param name="divisions">Number of cells along each side.</param>
    /// <param name="centerLineColor">Color of the two lines through the center (default gray 0x444444).</param>
    /// <param name="gridColor">Color of the other lines (default gray 0x888888).</param>
    public GridHelper(float size = 10f, int divisions = 10, ColorF? centerLineColor = null, ColorF? gridColor = null)
        : base(Build(size, Math.Max(1, divisions), centerLineColor ?? ColorF.FromHex(0x444444), gridColor ?? ColorF.FromHex(0x888888)), HelperUtil.Lines(ColorF.White, true))
    {
        Name = "GridHelper";
    }

    private static BufferGeometry Build(float size, int divisions, ColorF center, ColorF grid)
    {
        var half = size / 2f;
        var step = size / divisions;
        var middle = divisions / 2;
        var positions = new float[(divisions + 1) * 4 * 3];
        var colors = new float[positions.Length];
        var v = 0;
        for (var i = 0; i <= divisions; i++)
        {
            var k = -half + (i * step);
            var color = i == middle && divisions % 2 == 0 ? center : grid;
            HelperUtil.Put(positions, v, new Vector3(-half, 0f, k));
            HelperUtil.Put(positions, v + 1, new Vector3(half, 0f, k));
            HelperUtil.Put(positions, v + 2, new Vector3(k, 0f, -half));
            HelperUtil.Put(positions, v + 3, new Vector3(k, 0f, half));
            for (var n = 0; n < 4; n++)
            {
                HelperUtil.Put(colors, v + n, color);
            }

            v += 4;
        }

        var g = new BufferGeometry { Name = "grid", Topology = Rhi.PrimitiveTopology.Lines };
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(positions, 3));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float(colors, 3));
        return g;
    }
}

/// <summary>A grid of circles and spokes in the XZ plane, centered on the origin.</summary>
public class PolarGridHelper : LineSegments
{
    /// <summary>Creates a polar grid.</summary>
    /// <param name="radius">Radius of the outermost circle.</param>
    /// <param name="sectors">Number of spokes.</param>
    /// <param name="rings">Number of circles.</param>
    /// <param name="divisions">Line segments per circle.</param>
    /// <param name="color1">First of the two alternating colors (default gray 0x444444).</param>
    /// <param name="color2">Second of the two alternating colors (default gray 0x888888).</param>
    public PolarGridHelper(float radius = 10f, int sectors = 16, int rings = 8, int divisions = 64, ColorF? color1 = null, ColorF? color2 = null)
        : base(Build(radius, Math.Max(0, sectors), Math.Max(0, rings), Math.Max(3, divisions), color1 ?? ColorF.FromHex(0x444444), color2 ?? ColorF.FromHex(0x888888)), HelperUtil.Lines(ColorF.White, true))
    {
        Name = "PolarGridHelper";
    }

    private static BufferGeometry Build(float radius, int sectors, int rings, int divisions, ColorF color1, ColorF color2)
    {
        var positions = new List<float>();
        var colors = new List<float>();
        void Vertex(Vector3 p, ColorF c)
        {
            positions.Add(p.X);
            positions.Add(p.Y);
            positions.Add(p.Z);
            colors.Add(c.R);
            colors.Add(c.G);
            colors.Add(c.B);
        }

        if (sectors > 1)
        {
            for (var i = 0; i < sectors; i++)
            {
                var (sin, cos) = MathF.SinCos((float)i / sectors * MathF.PI * 2f);
                var color = (i & 1) != 0 ? color1 : color2;
                Vertex(Vector3.Zero, color);
                Vertex(new Vector3(sin * radius, 0f, cos * radius), color);
            }
        }

        for (var i = 0; i < rings; i++)
        {
            var color = (i & 1) != 0 ? color1 : color2;
            var r = radius - (radius / rings * i);
            for (var j = 0; j < divisions; j++)
            {
                var (s1, c1) = MathF.SinCos((float)j / divisions * MathF.PI * 2f);
                var (s2, c2) = MathF.SinCos((float)(j + 1) / divisions * MathF.PI * 2f);
                Vertex(new Vector3(s1 * r, 0f, c1 * r), color);
                Vertex(new Vector3(s2 * r, 0f, c2 * r), color);
            }
        }

        var g = new BufferGeometry { Name = "polarGrid", Topology = Rhi.PrimitiveTopology.Lines };
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([.. positions], 3));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float([.. colors], 3));
        return g;
    }
}

/// <summary>The three axes as colored lines from the origin: X red, Y green, Z blue.</summary>
public class AxesHelper : LineSegments
{
    /// <summary>Creates the axes with a length.</summary>
    public AxesHelper(float size = 1f)
        : base(Build(size), HelperUtil.Lines(ColorF.White, true))
    {
        Name = "AxesHelper";
    }

    private static BufferGeometry Build(float size)
    {
        var g = new BufferGeometry { Name = "axes", Topology = Rhi.PrimitiveTopology.Lines };
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, size, 0f, 0f, 0f, 0f, 0f, 0f, size, 0f, 0f, 0f, 0f, 0f, 0f, size], 3));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float([1f, 0f, 0f, 1f, 0.6f, 0f, 0f, 1f, 0f, 0.6f, 1f, 0f, 0f, 0f, 1f, 0f, 0.6f, 1f], 3));
        return g;
    }

    /// <summary>Replaces the axis colors.</summary>
    public void SetColors(ColorF x, ColorF y, ColorF z)
    {
        var colors = Geometry.GetAttribute(BufferGeometry.Color)!;
        var data = colors.FloatData;
        HelperUtil.Put(data, 0, x);
        HelperUtil.Put(data, 1, x);
        HelperUtil.Put(data, 2, y);
        HelperUtil.Put(data, 3, y);
        HelperUtil.Put(data, 4, z);
        HelperUtil.Put(data, 5, z);
        colors.NeedsUpdate();
    }
}

/// <summary>An arrow: a line with a cone head.</summary>
public class ArrowHelper : Object3D
{
    private static BufferGeometry? s_lineGeometry;
    private static BufferGeometry? s_coneGeometry;
    private readonly Line _line;
    private readonly Mesh _cone;
    private readonly LineMaterial _lineMaterial;
    private readonly BasicMaterial _coneMaterial;

    /// <summary>Creates an arrow.</summary>
    /// <param name="direction">Where the arrow points (default +Z).</param>
    /// <param name="origin">Where it starts.</param>
    /// <param name="length">Total length.</param>
    /// <param name="color">Color (default yellow).</param>
    /// <param name="headLength">Length of the head (default 20 % of the length).</param>
    /// <param name="headWidth">Width of the head (default 20 % of the head length).</param>
    public ArrowHelper(Vector3? direction = null, Vector3 origin = default, float length = 1f, ColorF? color = null, float? headLength = null, float? headWidth = null)
    {
        Name = "ArrowHelper";
        if (s_lineGeometry is null)
        {
            var line = new BufferGeometry { Name = "arrowLine" };
            line.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 0f, 1f, 0f], 3));
            s_lineGeometry = line;
            var cone = new ConeGeometry(0.5f, 1f, 5, 1);
            cone.Transform(Matrix4x4.CreateTranslation(0f, -0.5f, 0f));
            s_coneGeometry = cone;
        }

        var c = color ?? HelperUtil.Yellow;
        _lineMaterial = HelperUtil.Lines(c);
        _coneMaterial = new BasicMaterial { Color = c, ToneMapped = false, Fog = false };
        _line = new Line(s_lineGeometry, _lineMaterial);
        _cone = new Mesh(s_coneGeometry!, _coneMaterial);
        Add(_line, _cone);
        Position = origin;
        SetDirection(direction ?? Vector3.UnitZ);
        SetLength(length, headLength, headWidth);
    }

    /// <summary>The shaft.</summary>
    public Line Line => _line;

    /// <summary>The head.</summary>
    public Mesh Cone => _cone;

    /// <summary>Points the arrow in a direction.</summary>
    public void SetDirection(Vector3 direction) => Rotation = HelperUtil.FromUnitY(direction);

    /// <summary>Sets the length, and optionally the size of the head.</summary>
    public void SetLength(float length, float? headLength = null, float? headWidth = null)
    {
        var head = headLength ?? (length * 0.2f);
        var width = headWidth ?? (head * 0.2f);
        _line.Scale = new Vector3(1f, MathF.Max(0.0001f, length - head), 1f);
        _cone.Scale = new Vector3(width, head, width);
        _cone.Position = new Vector3(0f, length, 0f);
    }

    /// <summary>Sets the color.</summary>
    public void SetColor(ColorF color)
    {
        _lineMaterial.Color = color;
        _coneMaterial.Color = color;
    }
}

/// <summary>The edges of an axis-aligned box, drawn as lines.</summary>
internal static class BoxLines
{
    private static readonly ushort[] s_indices = [0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7];

    public static BufferGeometry Create(BoundingBox box)
    {
        var g = new BufferGeometry { Name = "boxLines", Topology = Rhi.PrimitiveTopology.Lines };
        var positions = new float[24];
        Write(positions, box);
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(positions, 3));
        g.SetIndex((ushort[])s_indices.Clone());
        return g;
    }

    public static void Write(float[] positions, BoundingBox box)
    {
        if (box.IsEmpty)
        {
            Array.Clear(positions);
            return;
        }

        Vector3 min = box.Min, max = box.Max;
        HelperUtil.Put(positions, 0, new Vector3(max.X, max.Y, max.Z));
        HelperUtil.Put(positions, 1, new Vector3(min.X, max.Y, max.Z));
        HelperUtil.Put(positions, 2, new Vector3(min.X, min.Y, max.Z));
        HelperUtil.Put(positions, 3, new Vector3(max.X, min.Y, max.Z));
        HelperUtil.Put(positions, 4, new Vector3(max.X, max.Y, min.Z));
        HelperUtil.Put(positions, 5, new Vector3(min.X, max.Y, min.Z));
        HelperUtil.Put(positions, 6, new Vector3(min.X, min.Y, min.Z));
        HelperUtil.Put(positions, 7, new Vector3(max.X, min.Y, min.Z));
    }
}

/// <summary>The world-space bounding box of an object and everything below it, as lines. Add it to the scene root
/// and call <see cref="Update"/> after the object moved or changed.</summary>
public class BoxHelper : LineSegments
{
    /// <summary>Creates a box around an object.</summary>
    public BoxHelper(Object3D target, ColorF? color = null)
        : base(BoxLines.Create(BoundingBox.Empty), HelperUtil.Lines(color ?? HelperUtil.Yellow))
    {
        Name = "BoxHelper";
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Update();
    }

    /// <summary>The object the box follows.</summary>
    public Object3D Target { get; set; }

    /// <summary>The box found by the last <see cref="Update"/>, in world space.</summary>
    public BoundingBox Box { get; private set; }

    /// <summary>Measures the object again.</summary>
    public void Update()
    {
        Box = HelperUtil.WorldBounds(Target);
        var positions = Geometry.GetAttribute(BufferGeometry.Position)!;
        BoxLines.Write(positions.FloatData, Box);
        positions.NeedsUpdate();
        Geometry.InvalidateBounds();
    }
}

/// <summary>A given axis-aligned box as lines.</summary>
public class Box3Helper : LineSegments
{
    private BoundingBox _box;

    /// <summary>Creates lines around a box.</summary>
    public Box3Helper(BoundingBox box, ColorF? color = null)
        : base(BoxLines.Create(box), HelperUtil.Lines(color ?? HelperUtil.Yellow))
    {
        Name = "Box3Helper";
        _box = box;
    }

    /// <summary>The box shown.</summary>
    public BoundingBox Box
    {
        get => _box;
        set
        {
            _box = value;
            var positions = Geometry.GetAttribute(BufferGeometry.Position)!;
            BoxLines.Write(positions.FloatData, value);
            positions.NeedsUpdate();
            Geometry.InvalidateBounds();
        }
    }
}

/// <summary>A plane shown as a square outline with its diagonals and a translucent fill.</summary>
public class PlaneHelper : Object3D
{
    private readonly LineMaterial _lineMaterial;
    private readonly BasicMaterial _fillMaterial;
    private Plane _plane;
    private float _size;

    /// <summary>Creates the helper of a plane (<c>dot(Normal, p) + D = 0</c>).</summary>
    public PlaneHelper(Plane plane, float size = 1f, ColorF? color = null)
    {
        Name = "PlaneHelper";
        var c = color ?? HelperUtil.Yellow;
        _lineMaterial = HelperUtil.Lines(c);
        _fillMaterial = new BasicMaterial
        {
            Color = c, Opacity = 0.2f, Transparent = true, DepthWrite = false, Side = Side.Double, ToneMapped = false, Fog = false,
        };
        var outline = new BufferGeometry { Name = "planeOutline", Topology = Rhi.PrimitiveTopology.Lines };
        outline.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(
            [-1f, -1f, 0f, 1f, -1f, 0f, 1f, -1f, 0f, 1f, 1f, 0f, 1f, 1f, 0f, -1f, 1f, 0f, -1f, 1f, 0f, -1f, -1f, 0f, -1f, -1f, 0f, 1f, 1f, 0f, -1f, 1f, 0f, 1f, -1f, 0f], 3));
        Add(new LineSegments(outline, _lineMaterial), new Mesh(new PlaneGeometry(2f, 2f), _fillMaterial));
        _plane = plane;
        _size = size;
        Update();
    }

    /// <summary>The plane shown.</summary>
    public Plane Plane
    {
        get => _plane;
        set
        {
            _plane = value;
            Update();
        }
    }

    /// <summary>Side length of the square.</summary>
    public float Size
    {
        get => _size;
        set
        {
            _size = value;
            Update();
        }
    }

    private void Update()
    {
        var normal = _plane.Normal.LengthSquared() > 0f ? Vector3.Normalize(_plane.Normal) : Vector3.UnitZ;
        Position = normal * -_plane.D;
        Scale = new Vector3(_size * 0.5f, _size * 0.5f, 1f);
        LookAt(Position + normal);
    }
}
