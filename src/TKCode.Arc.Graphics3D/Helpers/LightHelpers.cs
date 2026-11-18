using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>Shows a directional light: a square at its position, perpendicular to the light, and a line to its
/// target. Add it to the scene root and call <see cref="Update"/> after the light changed.</summary>
public class DirectionalLightHelper : Object3D
{
    private readonly LineMaterial _material;
    private readonly Line _targetLine;

    /// <summary>Creates the helper. Without a color it takes the light's color.</summary>
    public DirectionalLightHelper(DirectionalLight light, float size = 1f, ColorF? color = null)
    {
        Name = "DirectionalLightHelper";
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Color = color;
        _material = HelperUtil.Lines(ColorF.White);
        var square = new BufferGeometry { Name = "lightPlane" };
        square.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([-size, size, 0f, size, size, 0f, size, -size, 0f, -size, -size, 0f, -size, size, 0f], 3));
        var line = new BufferGeometry { Name = "lightTarget" };
        line.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 0f, 0f, 1f], 3));
        _targetLine = new Line(line, _material) { FrustumCulled = false };
        Add(new Line(square, _material) { FrustumCulled = false }, _targetLine);
        Update();
    }

    /// <summary>The light shown.</summary>
    public DirectionalLight Light { get; }

    /// <summary>The line color; null follows the light's color.</summary>
    public ColorF? Color { get; set; }

    /// <summary>Follows the light's position, target and color.</summary>
    public void Update()
    {
        var position = Light.WorldPosition;
        Position = position;
        var target = Light.Target ?? position + Light.Direction;
        LookAt(target);
        _targetLine.Scale = new Vector3(1f, 1f, MathF.Max(Vector3.Distance(position, target), 1e-4f));
        _material.Color = Color ?? Light.Color;
    }
}

/// <summary>Shows a point light as a small wire sphere. Add it to the scene root and call <see cref="Update"/> after
/// the light changed.</summary>
public class PointLightHelper : LineSegments
{
    /// <summary>Creates the helper. Without a color it takes the light's color.</summary>
    public PointLightHelper(PointLight light, float sphereSize = 1f, ColorF? color = null)
        : base(new WireframeGeometry(new SphereGeometry(sphereSize, 4, 2)), HelperUtil.Lines(ColorF.White))
    {
        Name = "PointLightHelper";
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Color = color;
        Update();
    }

    /// <summary>The light shown.</summary>
    public PointLight Light { get; }

    /// <summary>The line color; null follows the light's color.</summary>
    public ColorF? Color { get; set; }

    /// <summary>Follows the light's position and color.</summary>
    public void Update()
    {
        Position = Light.WorldPosition;
        ((LineMaterial)Material).Color = Color ?? Light.Color;
    }
}

/// <summary>Shows a spot light as its cone. Add it to the scene root and call <see cref="Update"/> after the light
/// changed.</summary>
public class SpotLightHelper : Object3D
{
    private readonly LineSegments _cone;
    private readonly LineMaterial _material;

    /// <summary>Creates the helper. Without a color it takes the light's color.</summary>
    public SpotLightHelper(SpotLight light, ColorF? color = null)
    {
        Name = "SpotLightHelper";
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Color = color;
        var positions = new List<float>
        {
            0f, 0f, 0f, 0f, 0f, 1f,
            0f, 0f, 0f, 1f, 0f, 1f,
            0f, 0f, 0f, -1f, 0f, 1f,
            0f, 0f, 0f, 0f, 1f, 1f,
            0f, 0f, 0f, 0f, -1f, 1f,
        };
        const int Segments = 32;
        for (var i = 0; i < Segments; i++)
        {
            var (s1, c1) = MathF.SinCos((float)i / Segments * MathF.PI * 2f);
            var (s2, c2) = MathF.SinCos((float)(i + 1) / Segments * MathF.PI * 2f);
            positions.AddRange([c1, s1, 1f, c2, s2, 1f]);
        }

        var geometry = new BufferGeometry { Name = "spotCone", Topology = Rhi.PrimitiveTopology.Lines };
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([.. positions], 3));
        _material = HelperUtil.Lines(ColorF.White);
        _cone = new LineSegments(geometry, _material) { FrustumCulled = false };
        Add(_cone);
        Update();
    }

    /// <summary>The light shown.</summary>
    public SpotLight Light { get; }

    /// <summary>The line color; null follows the light's color.</summary>
    public ColorF? Color { get; set; }

    /// <summary>Follows the light's position, target, cone and color.</summary>
    public void Update()
    {
        Position = Light.WorldPosition;
        var length = Light.Distance > 0f ? Light.Distance : 1000f;
        var width = length * MathF.Tan(Math.Clamp(Light.Angle, 0f, (MathF.PI / 2f) - 0.001f));
        _cone.Scale = new Vector3(width, width, length);
        LookAt(Light.Target ?? Light.WorldPosition + Light.Direction);
        _material.Color = Color ?? Light.Color;
    }
}

/// <summary>Shows a hemisphere light as a wire octahedron: the half toward the sky in the sky color, the other half
/// in the ground color. Add it to the scene root and call <see cref="Update"/> after the light changed.</summary>
public class HemisphereLightHelper : LineSegments
{
    /// <summary>Creates the helper. With a color, both halves use it.</summary>
    public HemisphereLightHelper(HemisphereLight light, float size = 1f, ColorF? color = null)
        : base(Build(size), HelperUtil.Lines(ColorF.White, true))
    {
        Name = "HemisphereLightHelper";
        Light = light ?? throw new ArgumentNullException(nameof(light));
        Color = color;
        Update();
    }

    /// <summary>The light shown.</summary>
    public HemisphereLight Light { get; }

    /// <summary>The line color; null shows the light's sky and ground colors.</summary>
    public ColorF? Color { get; set; }

    private static BufferGeometry Build(float size)
    {
        var g = new WireframeGeometry(new OctahedronGeometry(size));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float(new float[g.VertexCount * 3], 3));
        return g;
    }

    /// <summary>Follows the light's position, direction and colors.</summary>
    public void Update()
    {
        var position = Light.WorldPosition;
        Position = position;
        Rotation = HelperUtil.FromUnitY(position);
        var positions = Geometry.GetAttribute(BufferGeometry.Position)!;
        var colors = Geometry.GetAttribute(BufferGeometry.Color)!;
        var sky = Color ?? Light.Color;
        var ground = Color ?? Light.GroundColor;
        for (var i = 0; i < positions.Count; i++)
        {
            var y = positions.GetVector3(i).Y;
            HelperUtil.Put(colors.FloatData, i, y > 1e-5f ? sky : y < -1e-5f ? ground : ColorF.Lerp(ground, sky, 0.5f));
        }

        colors.NeedsUpdate();
    }
}

/// <summary>Shows what a camera sees: its near and far rectangles, the edges between them, its up direction and its
/// line of sight. Add it to the scene root and call <see cref="Update"/> after the camera changed.</summary>
public class CameraHelper : LineSegments
{
    private const int VertexCount = 50;

    /// <summary>Creates the helper of a camera.</summary>
    public CameraHelper(Camera camera)
        : base(Build(), HelperUtil.Lines(ColorF.White, true))
    {
        Name = "CameraHelper";
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        FrustumCulled = false;
        SetColors(ColorF.FromHex(0xffaa00), ColorF.FromHex(0xff0000), ColorF.FromHex(0x00aaff), ColorF.White, ColorF.FromHex(0x333333));
        Update();
    }

    /// <summary>The camera shown.</summary>
    public Camera Camera { get; }

    private static BufferGeometry Build()
    {
        var g = new BufferGeometry { Name = "cameraFrustum", Topology = Rhi.PrimitiveTopology.Lines };
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(new float[VertexCount * 3], 3));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float(new float[VertexCount * 3], 3));
        return g;
    }

    /// <summary>Sets the colors of the frustum, the lines from the eye, the up triangle, the line of sight and the
    /// crosses.</summary>
    public void SetColors(ColorF frustum, ColorF cone, ColorF up, ColorF target, ColorF cross)
    {
        var colors = Geometry.GetAttribute(BufferGeometry.Color)!;
        var data = colors.FloatData;
        for (var i = 0; i < VertexCount; i++)
        {
            HelperUtil.Put(data, i, i < 24 ? frustum : i < 32 ? cone : i < 38 ? up : i < 42 ? target : cross);
        }

        colors.NeedsUpdate();
    }

    /// <summary>Follows the camera's position, orientation and projection.</summary>
    public void Update()
    {
        var camera = Camera;
        Vector3 P(float x, float y, float z) => camera.Unproject(new Vector3(x, y, z));
        Vector3 n1 = P(-1f, -1f, 0f), n2 = P(1f, -1f, 0f), n3 = P(-1f, 1f, 0f), n4 = P(1f, 1f, 0f);
        Vector3 f1 = P(-1f, -1f, 1f), f2 = P(1f, -1f, 1f), f3 = P(-1f, 1f, 1f), f4 = P(1f, 1f, 1f);
        var c = P(0f, 0f, 0f);
        var t = P(0f, 0f, 1f);
        var eye = camera.IsOrthographic ? c : camera.WorldPosition;
        Vector3 u1 = P(0.7f, 1.1f, 0f), u2 = P(-0.7f, 1.1f, 0f), u3 = P(0f, 2f, 0f);
        ReadOnlySpan<Vector3> points =
        [
            n1, n2, n2, n4, n4, n3, n3, n1,
            f1, f2, f2, f4, f4, f3, f3, f1,
            n1, f1, n2, f2, n3, f3, n4, f4,
            eye, n1, eye, n2, eye, n3, eye, n4,
            u1, u2, u2, u3, u3, u1,
            c, t, eye, c,
            P(-1f, 0f, 0f), P(1f, 0f, 0f), P(0f, -1f, 0f), P(0f, 1f, 0f),
            P(-1f, 0f, 1f), P(1f, 0f, 1f), P(0f, -1f, 1f), P(0f, 1f, 1f),
        ];
        var positions = Geometry.GetAttribute(BufferGeometry.Position)!;
        for (var i = 0; i < points.Length; i++)
        {
            HelperUtil.Put(positions.FloatData, i, points[i]);
        }

        positions.NeedsUpdate();
        Geometry.InvalidateBounds();
    }
}

/// <summary>Shows the bones of a skeleton as lines from each joint to its parent (blue at the parent, green at the
/// child), drawn on top of everything. Add it to the scene root and call <see cref="Update"/> every frame the
/// skeleton moves.</summary>
public class SkeletonHelper : LineSegments
{
    private readonly Object3D[] _joints;

    /// <summary>Creates the helper of every <see cref="Bone"/> below (and including) a node.</summary>
    public SkeletonHelper(Object3D root)
        : this(Collect(root ?? throw new ArgumentNullException(nameof(root))))
    {
    }

    /// <summary>Creates the helper of a skeleton's joints.</summary>
    public SkeletonHelper(Skeleton skeleton)
        : this((skeleton ?? throw new ArgumentNullException(nameof(skeleton))).Bones)
    {
    }

    private SkeletonHelper(Object3D[] bones)
        : base(Build(bones, out var joints), new LineMaterial { VertexColors = true, DepthTest = false, DepthWrite = false, Transparent = true, ToneMapped = false, Fog = false })
    {
        Name = "SkeletonHelper";
        Bones = bones;
        _joints = joints;
        FrustumCulled = false;
        Update();
    }

    /// <summary>The joints shown.</summary>
    public IReadOnlyList<Object3D> Bones { get; }

    private static Object3D[] Collect(Object3D root)
    {
        var bones = new List<Object3D>();
        root.Traverse(o =>
        {
            if (o is Bone)
            {
                bones.Add(o);
            }
        });
        return [.. bones];
    }

    private static BufferGeometry Build(Object3D[] bones, out Object3D[] joints)
    {
        var set = new HashSet<Object3D>(bones, ReferenceEqualityComparer.Instance);
        var list = new List<Object3D>();
        foreach (var bone in bones)
        {
            if (bone.Parent is not null && set.Contains(bone.Parent))
            {
                list.Add(bone);
            }
        }

        joints = [.. list];
        var colors = new float[joints.Length * 6];
        for (var i = 0; i < joints.Length; i++)
        {
            colors[(i * 6) + 2] = 1f;
            colors[(i * 6) + 4] = 1f;
        }

        var g = new BufferGeometry { Name = "skeleton", Topology = Rhi.PrimitiveTopology.Lines };
        g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(new float[joints.Length * 6], 3));
        g.SetAttribute(BufferGeometry.Color, VertexAttribute.Float(colors, 3));
        return g;
    }

    /// <summary>Follows the joints' current positions.</summary>
    public void Update()
    {
        var positions = Geometry.GetAttribute(BufferGeometry.Position)!;
        var data = positions.FloatData;
        for (var i = 0; i < _joints.Length; i++)
        {
            HelperUtil.Put(data, i * 2, _joints[i].Parent!.WorldPosition);
            HelperUtil.Put(data, (i * 2) + 1, _joints[i].WorldPosition);
        }

        positions.NeedsUpdate();
        Geometry.InvalidateBounds();
    }
}
