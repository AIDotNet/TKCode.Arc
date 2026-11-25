using System.Numerics;

namespace TKCode.Arc.Graphics3D;

/// <summary>A set of up to 32 layers. An object is drawn by a camera when their layer sets share a layer.</summary>
public struct Layers : IEquatable<Layers>
{
    /// <summary>The bit mask; bit n is layer n.</summary>
    public uint Mask;

    /// <summary>Creates a set from a mask.</summary>
    public Layers(uint mask) => Mask = mask;

    /// <summary>Layer 0 only: the default of every object and camera.</summary>
    public static Layers Default => new(1u);

    /// <summary>Every layer.</summary>
    public static Layers All => new(uint.MaxValue);

    /// <summary>Makes this set contain exactly one layer.</summary>
    public void Set(int layer) => Mask = 1u << layer;

    /// <summary>Adds a layer.</summary>
    public void Enable(int layer) => Mask |= 1u << layer;

    /// <summary>Removes a layer.</summary>
    public void Disable(int layer) => Mask &= ~(1u << layer);

    /// <summary>True when the two sets share a layer.</summary>
    public readonly bool Test(Layers other) => (Mask & other.Mask) != 0;

    /// <inheritdoc/>
    public readonly bool Equals(Layers other) => Mask == other.Mask;

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) => obj is Layers l && Equals(l);

    /// <inheritdoc/>
    public override readonly int GetHashCode() => (int)Mask;
}

/// <summary>A node of the scene graph: a transform relative to its parent, and children. World matrices are computed
/// on demand and cached, so <see cref="WorldMatrix"/> is always current; there is no "update matrices" step.</summary>
public class Object3D
{
    private static int s_nextId;
    private Vector3 _position;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _scale = Vector3.One;
    private Matrix4x4 _local = Matrix4x4.Identity;
    private Matrix4x4 _world = Matrix4x4.Identity;
    private bool _localDirty;
    private bool _worldDirty;
    private bool _visible = true;
    private List<Object3D>? _children;
    private Object3D? _parent;
    private Scene? _scene;

    /// <summary>A process-wide unique number.</summary>
    public int Id { get; } = Interlocked.Increment(ref s_nextId);

    /// <summary>A name for lookups and debugging.</summary>
    public string Name { get; set; } = "";

    /// <summary>Anything the application wants to attach.</summary>
    public object? Tag { get; set; }

    /// <summary>Objects with a higher value draw later within the opaque and within the transparent group.</summary>
    public int RenderOrder { get; set; }

    /// <summary>The parent node, or null for a root.</summary>
    public Object3D? Parent => _parent;

    /// <summary>The scene this node is in, or null.</summary>
    public Scene? Scene => _scene;

    /// <summary>The child nodes.</summary>
    public IReadOnlyList<Object3D> Children => (IReadOnlyList<Object3D>?)_children ?? [];

    /// <summary>Position relative to the parent.</summary>
    public Vector3 Position
    {
        get => _position;
        set
        {
            if (_position != value)
            {
                _position = value;
                MarkLocalDirty();
            }
        }
    }

    /// <summary>Rotation relative to the parent.</summary>
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation != value)
            {
                _rotation = value;
                MarkLocalDirty();
            }
        }
    }

    /// <summary>Scale relative to the parent.</summary>
    public Vector3 Scale
    {
        get => _scale;
        set
        {
            if (_scale != value)
            {
                _scale = value;
                MarkLocalDirty();
            }
        }
    }

    /// <summary>The rotation as intrinsic XYZ Euler angles in radians.</summary>
    public Vector3 EulerAngles
    {
        get => MathUtil.EulerFromQuaternion(_rotation);
        set => Rotation = MathUtil.QuaternionFromEuler(value);
    }

    /// <summary>False hides this node and everything below it.</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible != value)
            {
                _visible = value;
                NotifySubtreeChanged();
            }
        }
    }

    /// <summary>The layers this node is in (not inherited by children).</summary>
    public Layers Layers
    {
        get;
        set
        {
            field = value;
            NotifyRenderStateChanged();
        }
    } = Layers.Default;

    /// <summary>True when the object is drawn into shadow maps.</summary>
    public bool CastShadow
    {
        get;
        set
        {
            field = value;
            NotifyRenderStateChanged();
        }
    }

    /// <summary>True when shadows darken the object.</summary>
    public bool ReceiveShadow
    {
        get;
        set
        {
            field = value;
            NotifyRenderStateChanged();
        }
    }

    /// <summary>False draws the object even when its bounds are outside the camera's view.</summary>
    public bool FrustumCulled
    {
        get;
        set
        {
            field = value;
            NotifyRenderStateChanged();
        }
    } = true;

    /// <summary>The transform relative to the parent.</summary>
    public Matrix4x4 LocalMatrix
    {
        get
        {
            if (_localDirty)
            {
                _local = MathUtil.Compose(_position, _rotation, _scale);
                _localDirty = false;
            }

            return _local;
        }
    }

    /// <summary>The transform to world space. Always current.</summary>
    public Matrix4x4 WorldMatrix
    {
        get
        {
            if (_worldDirty)
            {
                _world = _parent is null ? LocalMatrix : LocalMatrix * _parent.WorldMatrix;
                _worldDirty = false;
            }

            return _world;
        }
    }

    /// <summary>The position in world space.</summary>
    public Vector3 WorldPosition => WorldMatrix.Translation;

    /// <summary>The rotation in world space (assuming no skew).</summary>
    public Quaternion WorldRotation
    {
        get
        {
            Matrix4x4.Decompose(WorldMatrix, out _, out var rotation, out _);
            return rotation;
        }
    }

    /// <summary>The direction of the local +Z axis in world space.</summary>
    public Vector3 WorldDirection
    {
        get
        {
            var m = WorldMatrix;
            return Vector3.Normalize(new Vector3(m.M31, m.M32, m.M33));
        }
    }

    /// <summary>Sets position, rotation and scale from a local matrix.</summary>
    public void SetLocalMatrix(in Matrix4x4 matrix)
    {
        if (Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
        {
            _position = translation;
            _rotation = rotation;
            _scale = scale;
            MarkLocalDirty();
        }
    }

    /// <summary>Adds a child, removing it from its current parent first.</summary>
    public Object3D Add(Object3D child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (ReferenceEquals(child, this))
        {
            throw new ArgumentException("An object cannot be its own child.", nameof(child));
        }

        for (var p = _parent; p is not null; p = p._parent)
        {
            if (ReferenceEquals(p, child))
            {
                throw new ArgumentException("An object cannot be added below itself.", nameof(child));
            }
        }

        child._parent?.Remove(child);
        (_children ??= []).Add(child);
        child._parent = this;
        child.MarkWorldDirty(force: true);
        var scene = this as Scene ?? _scene;
        if (scene is not null)
        {
            child.AttachSubtree(scene);
        }

        return this;
    }

    /// <summary>Adds several children.</summary>
    public Object3D Add(params Object3D[] children)
    {
        foreach (var child in children)
        {
            Add(child);
        }

        return this;
    }

    /// <summary>Removes a child.</summary>
    public bool Remove(Object3D child)
    {
        if (_children is null || !_children.Remove(child))
        {
            return false;
        }

        if (child._scene is not null)
        {
            child.DetachSubtree();
        }

        child._parent = null;
        child.MarkWorldDirty(force: true);
        return true;
    }

    /// <summary>Removes this node from its parent.</summary>
    public void RemoveFromParent() => _parent?.Remove(this);

    /// <summary>Removes every child.</summary>
    public void Clear()
    {
        if (_children is null)
        {
            return;
        }

        for (var i = _children.Count - 1; i >= 0; i--)
        {
            Remove(_children[i]);
        }
    }

    /// <summary>Calls an action on this node and every node below it.</summary>
    public void Traverse(Action<Object3D> action)
    {
        action(this);
        if (_children is null)
        {
            return;
        }

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].Traverse(action);
        }
    }

    /// <summary>Like <see cref="Traverse"/>, skipping hidden subtrees.</summary>
    public void TraverseVisible(Action<Object3D> action)
    {
        if (!_visible)
        {
            return;
        }

        action(this);
        if (_children is null)
        {
            return;
        }

        for (var i = 0; i < _children.Count; i++)
        {
            _children[i].TraverseVisible(action);
        }
    }

    /// <summary>The first node with a name in this subtree, or null.</summary>
    public Object3D? Find(string name)
    {
        if (Name == name)
        {
            return this;
        }

        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                if (_children[i].Find(name) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>The first node of a type in this subtree, or null.</summary>
    public T? Find<T>()
        where T : Object3D
    {
        if (this is T match)
        {
            return match;
        }

        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                if (_children[i].Find<T>() is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>Rotates the node so that its local +Z axis (for cameras and lights: -Z) points at a world position.</summary>
    public void LookAt(Vector3 target) => LookAt(target, Vector3.UnitY);

    /// <summary>Rotates the node so that its local +Z axis (for cameras and lights: -Z) points at a world position.</summary>
    public void LookAt(Vector3 target, Vector3 up)
    {
        var position = WorldPosition;
        var z = LooksDownNegativeZ ? position - target : target - position;
        if (z.LengthSquared() < 1e-12f)
        {
            z = Vector3.UnitZ;
        }

        z = Vector3.Normalize(z);
        var x = Vector3.Cross(up, z);
        if (x.LengthSquared() < 1e-12f)
        {
            // up and z are parallel: nudge z.
            z = Vector3.Normalize(z + (MathF.Abs(up.Z) > 0.999f ? new Vector3(1e-4f, 0f, 0f) : new Vector3(0f, 0f, 1e-4f)));
            x = Vector3.Cross(up, z);
        }

        x = Vector3.Normalize(x);
        var y = Vector3.Cross(z, x);
        var m = new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, 0f, 0f, 0f, 1f);
        var world = Quaternion.CreateFromRotationMatrix(m);
        Rotation = _parent is null ? world : Quaternion.Normalize(Quaternion.Inverse(_parent.WorldRotation) * world);
    }

    /// <summary>True for nodes whose "forward" is local -Z (cameras, lights).</summary>
    protected virtual bool LooksDownNegativeZ => false;

    /// <summary>Rotates about a local axis (normalized) by an angle in radians.</summary>
    public void RotateOnAxis(Vector3 axis, float angle) => Rotation = Quaternion.Normalize(_rotation * Quaternion.CreateFromAxisAngle(axis, angle));

    /// <summary>Rotates about a world axis (normalized) by an angle in radians. Assumes an unrotated parent.</summary>
    public void RotateOnWorldAxis(Vector3 axis, float angle) => Rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(axis, angle) * _rotation);

    /// <summary>Rotates about the local X axis.</summary>
    public void RotateX(float angle) => RotateOnAxis(Vector3.UnitX, angle);

    /// <summary>Rotates about the local Y axis.</summary>
    public void RotateY(float angle) => RotateOnAxis(Vector3.UnitY, angle);

    /// <summary>Rotates about the local Z axis.</summary>
    public void RotateZ(float angle) => RotateOnAxis(Vector3.UnitZ, angle);

    /// <summary>Moves along a local axis (normalized).</summary>
    public void TranslateOnAxis(Vector3 axis, float distance) => Position = _position + (Vector3.Transform(axis, _rotation) * distance);

    /// <summary>Converts a point from this node's local space to world space.</summary>
    public Vector3 LocalToWorld(Vector3 point) => Vector3.Transform(point, WorldMatrix);

    /// <summary>Converts a point from world space to this node's local space.</summary>
    public Vector3 WorldToLocal(Vector3 point) => Matrix4x4.Invert(WorldMatrix, out var inv) ? Vector3.Transform(point, inv) : point;

    private void MarkLocalDirty()
    {
        _localDirty = true;
        MarkWorldDirty(force: false);
    }

    private void MarkWorldDirty(bool force)
    {
        // Invariant: when a node's world matrix is stale, every node below it is stale too. So an already stale node
        // ends the walk.
        if (_worldDirty && !force)
        {
            return;
        }

        _worldDirty = true;
        if (_scene is not null)
        {
            OnWorldChanged(_scene);
        }

        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].MarkWorldDirty(force);
            }
        }
    }

    private void AttachSubtree(Scene scene)
    {
        _scene = scene;
        OnAttached(scene);
        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].AttachSubtree(scene);
            }
        }
    }

    private void DetachSubtree()
    {
        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].DetachSubtree();
            }
        }

        var scene = _scene;
        _scene = null;
        if (scene is not null)
        {
            OnDetached(scene);
        }
    }

    private void NotifySubtreeChanged()
    {
        if (_scene is null)
        {
            return;
        }

        OnRenderStateChanged(_scene);
        if (_children is not null)
        {
            for (var i = 0; i < _children.Count; i++)
            {
                _children[i].NotifySubtreeChanged();
            }
        }
    }

    /// <summary>Tells the scene that something the renderer caches about this node changed (visibility, layers, bounds).</summary>
    protected void NotifyRenderStateChanged()
    {
        if (_scene is not null)
        {
            OnRenderStateChanged(_scene);
        }
    }

    /// <summary>True when this node and all its ancestors are visible.</summary>
    internal bool IsEffectivelyVisible()
    {
        for (var n = this; n is not null; n = n._parent)
        {
            if (!n._visible)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Called when the node enters a scene.</summary>
    private protected virtual void OnAttached(Scene scene)
    {
    }

    /// <summary>Called when the node leaves a scene.</summary>
    private protected virtual void OnDetached(Scene scene)
    {
    }

    /// <summary>Called when the node's world matrix became stale while it is in a scene.</summary>
    private protected virtual void OnWorldChanged(Scene scene)
    {
    }

    /// <summary>Called when visibility, layers or shadow flags changed while the node is in a scene.</summary>
    private protected virtual void OnRenderStateChanged(Scene scene)
    {
    }
}

/// <summary>A node with no content of its own, for grouping.</summary>
public class Group : Object3D
{
}
