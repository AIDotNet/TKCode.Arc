namespace TKCode.Arc.Graphics3D;

/// <summary>A node the renderer draws: a geometry with one or more materials.</summary>
public abstract class Renderable : Object3D
{
    private BufferGeometry _geometry;
    private Material[] _materials;
    private int _proxy = -1;

    private protected Renderable(BufferGeometry geometry, Material material)
    {
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _materials = [material ?? throw new ArgumentNullException(nameof(material))];
    }

    /// <summary>The vertex data.</summary>
    public BufferGeometry Geometry
    {
        get => _geometry;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_geometry, value))
            {
                return;
            }

            if (_proxy >= 0)
            {
                _geometry.RemoveUser(this);
                value.AddUser(this);
            }

            _geometry = value;
            NotifyRenderStateChanged();
        }
    }

    /// <summary>The material (the first one of <see cref="Materials"/>).</summary>
    public Material Material
    {
        get => _materials[0];
        set => _materials = [value ?? throw new ArgumentNullException(nameof(value))];
    }

    /// <summary>One material per <see cref="BufferGeometry.Groups"/> material index.</summary>
    public Material[] Materials
    {
        get => _materials;
        set
        {
            if (value is null || value.Length == 0)
            {
                throw new ArgumentException("At least one material is required.", nameof(value));
            }

            _materials = value;
        }
    }

    /// <summary>The slot in the scene's render world, or -1.</summary>
    internal int ProxyIndex => _proxy;

    /// <summary>How the vertices form primitives.</summary>
    internal virtual Rhi.PrimitiveTopology Topology => _geometry.Topology;

    /// <summary>Bounds in local space used for culling.</summary>
    internal virtual BoundingSphere LocalBoundingSphere => _geometry.BoundingSphere;

    /// <summary>Bounds in local space.</summary>
    internal virtual BoundingBox LocalBoundingBox => _geometry.BoundingBox;

    internal void NotifyBoundsChanged() => NotifyRenderStateChanged();

    private protected override void OnAttached(Scene scene)
    {
        _proxy = scene.World.Add(this);
        _geometry.AddUser(this);
    }

    private protected override void OnDetached(Scene scene)
    {
        if (_proxy >= 0)
        {
            scene.World.Remove(_proxy);
            _geometry.RemoveUser(this);
            _proxy = -1;
        }
    }

    private protected override void OnWorldChanged(Scene scene)
    {
        if (_proxy >= 0)
        {
            scene.World.MarkDirty(_proxy);
        }
    }

    private protected override void OnRenderStateChanged(Scene scene)
    {
        if (_proxy >= 0)
        {
            scene.World.MarkDirty(_proxy);
        }
    }
}

/// <summary>Triangles (or the geometry's own topology) drawn with surface materials.</summary>
public class Mesh : Renderable
{
    /// <summary>Creates a mesh.</summary>
    public Mesh(BufferGeometry geometry, Material material)
        : base(geometry, material)
    {
    }

    /// <summary>One weight per <see cref="BufferGeometry.MorphTargets"/> entry; null means all zero.</summary>
    public float[]? MorphWeights { get; set; }

    // Where this frame's bone matrices and morph weights are in the renderer's frame data.
    internal Renderer? AuxOwner;
    internal int AuxFrame;
    internal int BoneBase;
    internal int MorphBase;
}
