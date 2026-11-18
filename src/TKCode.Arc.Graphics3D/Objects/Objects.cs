using System.Numerics;
using TKCode.Arc.Graphics3D.Rhi;
using TKCode.Arc.Graphics3D.Shading;

namespace TKCode.Arc.Graphics3D;

/// <summary>Many copies of one geometry and material drawn in a single call, each with its own transform and
/// optional color. The fastest way to draw thousands of equal objects.</summary>
public class InstancedMesh : Mesh
{
    private BoundingSphere _sphere;
    private BoundingBox _box;
    private int _boundsVersion = -1;
    private int _count;

    /// <summary>Creates an instanced mesh with room for <paramref name="capacity"/> instances, all at the identity.</summary>
    public InstancedMesh(BufferGeometry geometry, Material material, int capacity)
        : base(geometry, material)
    {
        Matrices = new Matrix4x4[Math.Max(1, capacity)];
        Array.Fill(Matrices, Matrix4x4.Identity);
        _count = Matrices.Length;
    }

    /// <summary>The instance transforms, relative to this node. After writing to the array call <see cref="NeedsUpdate"/>.</summary>
    public Matrix4x4[] Matrices { get; }

    /// <summary>Per-instance colors (linear RGB), multiplied with the material color; null until first set.</summary>
    public Vector3[]? Colors { get; private set; }

    /// <summary>How many instances are drawn (at most the capacity).</summary>
    public int Count
    {
        get => _count;
        set
        {
            _count = Math.Clamp(value, 0, Matrices.Length);
            NeedsUpdate();
        }
    }

    /// <summary>Changes when instance transforms change.</summary>
    public int MatricesVersion { get; private set; }

    /// <summary>Changes when instance colors change.</summary>
    public int ColorsVersion { get; private set; }

    internal ResourceBinding? InstanceBinding;

    /// <summary>Sets one instance transform.</summary>
    public void SetMatrix(int index, in Matrix4x4 matrix)
    {
        Matrices[index] = matrix;
        NeedsUpdate();
    }

    /// <summary>Sets one instance color.</summary>
    public void SetColor(int index, ColorF color)
    {
        if (Colors is null)
        {
            Colors = new Vector3[Matrices.Length];
            Array.Fill(Colors, Vector3.One);
        }

        Colors[index] = color.ToVector3();
        ColorsVersion++;
    }

    /// <summary>Tells the renderer that <see cref="Matrices"/> changed.</summary>
    public void NeedsUpdate()
    {
        MatricesVersion++;
        NotifyBoundsChanged();
    }

    /// <summary>Tells the renderer that <see cref="Colors"/> changed.</summary>
    public void ColorsNeedUpdate() => ColorsVersion++;

    internal override BoundingSphere LocalBoundingSphere
    {
        get
        {
            EnsureBounds();
            return _sphere;
        }
    }

    internal override BoundingBox LocalBoundingBox
    {
        get
        {
            EnsureBounds();
            return _box;
        }
    }

    private void EnsureBounds()
    {
        if (_boundsVersion == MatricesVersion)
        {
            return;
        }

        var geometryBox = Geometry.BoundingBox;
        var box = BoundingBox.Empty;
        for (var i = 0; i < _count; i++)
        {
            box.Expand(geometryBox.Transform(Matrices[i]));
        }

        _box = box;
        _sphere = BoundingSphere.FromBox(box);
        _boundsVersion = MatricesVersion;
    }
}

/// <summary>A joint of a skeleton.</summary>
public class Bone : Object3D
{
}

/// <summary>The joints that deform a <see cref="SkinnedMesh"/>, with the inverse of each joint's bind pose.</summary>
public sealed class Skeleton
{
    /// <summary>Creates a skeleton. Without inverse bind matrices the current pose becomes the bind pose.</summary>
    public Skeleton(Object3D[] bones, Matrix4x4[]? inverseBindMatrices = null)
    {
        Bones = bones ?? throw new ArgumentNullException(nameof(bones));
        if (inverseBindMatrices is null)
        {
            inverseBindMatrices = new Matrix4x4[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                Matrix4x4.Invert(bones[i].WorldMatrix, out inverseBindMatrices[i]);
            }
        }

        if (inverseBindMatrices.Length != bones.Length)
        {
            throw new ArgumentException("One inverse bind matrix per bone is required.", nameof(inverseBindMatrices));
        }

        InverseBindMatrices = inverseBindMatrices;
    }

    /// <summary>The joints, indexed by the <c>skinIndex</c> attribute.</summary>
    public Object3D[] Bones { get; }

    /// <summary>World to joint space in the bind pose, per joint.</summary>
    public Matrix4x4[] InverseBindMatrices { get; }
}

/// <summary>A mesh deformed by a <see cref="Skeleton"/> through the geometry's <c>skinIndex</c> and <c>skinWeight</c>
/// attributes. As in glTF, the mesh's own transform does not move the skinned vertices: the joints do.</summary>
public class SkinnedMesh : Mesh
{
    /// <summary>Creates a skinned mesh.</summary>
    public SkinnedMesh(BufferGeometry geometry, Material material)
        : base(geometry, material)
    {
        // The static bounds do not follow the pose.
        FrustumCulled = false;
    }

    /// <summary>The skeleton, or null to draw the mesh undeformed.</summary>
    public Skeleton? Skeleton { get; set; }

    /// <summary>Applied to the vertices before skinning.</summary>
    public Matrix4x4 BindMatrix { get; set; } = Matrix4x4.Identity;
}

/// <summary>A connected line through the geometry's vertices.</summary>
public class Line : Renderable
{
    /// <summary>Creates a line.</summary>
    public Line(BufferGeometry geometry, Material material)
        : base(geometry, material)
    {
    }

    internal override PrimitiveTopology Topology => PrimitiveTopology.LineStrip;
}

/// <summary>Separate line segments: every two vertices (or indices) form one segment.</summary>
public class LineSegments : Line
{
    /// <summary>Creates line segments.</summary>
    public LineSegments(BufferGeometry geometry, Material material)
        : base(geometry, material)
    {
    }

    internal override PrimitiveTopology Topology => PrimitiveTopology.Lines;
}

/// <summary>One screen-facing square per vertex.</summary>
public class Points : Renderable
{
    /// <summary>Creates points.</summary>
    public Points(BufferGeometry geometry, Material material)
        : base(geometry, material)
    {
    }

    internal override PrimitiveTopology Topology => PrimitiveTopology.Points;
}

/// <summary>A quad that always faces the camera, positioned and scaled by its node.</summary>
public class Sprite : Renderable
{
    private static BufferGeometry? s_quad;

    /// <summary>Creates a sprite.</summary>
    public Sprite(SpriteMaterial material)
        : base(Quad, material)
    {
    }

    private static BufferGeometry Quad
    {
        get
        {
            if (s_quad is null)
            {
                var g = new BufferGeometry { Name = "sprite" };
                g.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([-0.5f, -0.5f, 0f, 0.5f, -0.5f, 0f, 0.5f, 0.5f, 0f, -0.5f, 0.5f, 0f], 3));
                g.SetAttribute(BufferGeometry.Uv, VertexAttribute.Float([0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f], 2));
                g.SetIndex(new ushort[] { 0, 1, 2, 0, 2, 3 });
                // The quad is placed in view space; cull by the node's scale instead.
                g.BoundingBox = new BoundingBox(new Vector3(-0.71f), new Vector3(0.71f));
                s_quad = g;
            }

            return s_quad;
        }
    }
}

/// <summary>Material of <see cref="Line"/> and <see cref="LineSegments"/>: one unlit color (lines are one pixel wide).</summary>
public class LineMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.White);

    /// <summary>The line color.</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Unlit;
        ShaderNode c = _color;
        ShaderNode a = OpacityUniform;
        if (VertexColors)
        {
            c = c * Sl.VertexColor.Xyz;
            a = a * Sl.VertexColor.W;
        }

        s.Color = c;
        s.Opacity = a;
    }
}

/// <summary>Material of <see cref="Points"/>.</summary>
public class PointsMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.White);
    private readonly UniformNode _size = Sl.Uniform(1f);
    private readonly TextureSlot _map = new();
    private bool _sizeAttenuation = true;
    private bool _round;
    private ShaderNode? _sizeNode;

    /// <summary>The point color.</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    /// <summary>Point size: world units with <see cref="SizeAttenuation"/>, pixels without.</summary>
    public float Size
    {
        get => _size.Float;
        set => _size.Float = value;
    }

    /// <summary>True (default) makes distant points smaller.</summary>
    public bool SizeAttenuation
    {
        get => _sizeAttenuation;
        set => Set(ref _sizeAttenuation, value);
    }

    /// <summary>True draws discs instead of squares.</summary>
    public bool Round
    {
        get => _round;
        set => Set(ref _round, value);
    }

    /// <summary>Texture drawn on each point; its alpha multiplies opacity.</summary>
    public Texture? Map
    {
        get => _map.Texture;
        set
        {
            var had = _map.Texture is not null;
            _map.Texture = value;
            if (had != value is not null)
            {
                Invalidate();
            }
        }
    }

    /// <summary>Replaces the size (float), for example from a vertex attribute.</summary>
    public ShaderNode? SizeNode
    {
        get => _sizeNode;
        set => Set(ref _sizeNode, value);
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Unlit;
        s.Kind = VertexKind.Points;
        s.SizeAttenuation = _sizeAttenuation;
        s.PointSize = _sizeNode ?? _size;
        ShaderNode c = _color;
        ShaderNode a = OpacityUniform;
        if (_map.Texture is not null)
        {
            var t = Sl.Texture(_map, Sl.Vec2(Sl.PointCoord.X, 1f - Sl.PointCoord.Y));
            c = c * t.Xyz;
            a = a * t.W;
        }

        if (VertexColors)
        {
            c = c * Sl.VertexColor.Xyz;
            a = a * Sl.VertexColor.W;
        }

        if (_round)
        {
            var d = Sl.Length(Sl.PointCoord - new Vector2(0.5f, 0.5f));
            a = a * (1f - Sl.SmoothStep(0.45f, 0.5f, d));
        }

        s.Color = c;
        s.Opacity = a;
    }
}

/// <summary>Material of <see cref="Sprite"/>.</summary>
public class SpriteMaterial : Material
{
    private readonly UniformNode _color = Sl.Uniform(ColorF.White);
    private readonly UniformNode _rotation = Sl.Uniform(0f);
    private readonly MaterialMap _map = new();
    private bool _sizeAttenuation = true;

    /// <summary>Creates the material; sprites are transparent by default.</summary>
    public SpriteMaterial()
    {
        Transparent = true;
    }

    /// <summary>The tint.</summary>
    public ColorF Color
    {
        get => _color.Color;
        set => _color.Color = value;
    }

    /// <summary>The image; its alpha multiplies opacity.</summary>
    public Texture? Map
    {
        get => _map.Texture;
        set => SetMap(_map, value);
    }

    /// <summary>Rotation about the view direction, in radians.</summary>
    public float Rotation
    {
        get => _rotation.Float;
        set
        {
            var had = _rotation.Float != 0f;
            _rotation.Float = value;
            if (had != (value != 0f))
            {
                Invalidate();
            }
        }
    }

    /// <summary>False keeps the sprite the same size on screen at any distance.</summary>
    public bool SizeAttenuation
    {
        get => _sizeAttenuation;
        set => Set(ref _sizeAttenuation, value);
    }

    internal override void BuildSurface(Surface s)
    {
        s.Model = LightingModel.Unlit;
        s.Kind = VertexKind.Sprite;
        s.SizeAttenuation = _sizeAttenuation;
        if (_rotation.Float != 0f)
        {
            s.SpriteRotation = _rotation;
        }

        ApplyColor(s, _color, _map);
    }
}
