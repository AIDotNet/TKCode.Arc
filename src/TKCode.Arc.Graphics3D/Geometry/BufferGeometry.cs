using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>Vertex data: named attributes, an optional index, and how they form primitives. Shared between any number
/// of objects. Follows the vertex conventions of glTF and three.js: counter-clockwise front faces, tangents as xyzw
/// with the handedness in w.</summary>
public class BufferGeometry : IDisposable
{
    /// <summary>Attribute name of positions (3 floats).</summary>
    public const string Position = "position";

    /// <summary>Attribute name of normals (3 components).</summary>
    public const string Normal = "normal";

    /// <summary>Attribute name of tangents (4 components, w is the bitangent sign).</summary>
    public const string Tangent = "tangent";

    /// <summary>Attribute name of the first texture coordinate set.</summary>
    public const string Uv = "uv";

    /// <summary>Attribute name of the second texture coordinate set.</summary>
    public const string Uv1 = "uv1";

    /// <summary>Attribute name of vertex colors (3 or 4 components, linear).</summary>
    public const string Color = "color";

    /// <summary>Attribute name of the four bone indices of a skinned vertex.</summary>
    public const string SkinIndex = "skinIndex";

    /// <summary>Attribute name of the four bone weights of a skinned vertex.</summary>
    public const string SkinWeight = "skinWeight";

    private static int s_nextId;
    private readonly List<KeyValuePair<string, VertexAttribute>> _attributes = [];
    private readonly List<GeometryGroup> _groups = [];
    private readonly List<MorphTarget> _morphTargets = [];
    private Array? _index;
    private BoundingBox _box;
    private BoundingSphere _sphere;
    private bool _boundsValid;
    private List<Renderable>? _users;

    /// <summary>A process-wide unique number (used in sort keys).</summary>
    public int Id { get; } = Interlocked.Increment(ref s_nextId);

    /// <summary>A name for debugging.</summary>
    public string Name { get; set; } = "";

    /// <summary>How vertices form primitives.</summary>
    public PrimitiveTopology Topology { get; set; } = PrimitiveTopology.Triangles;

    /// <summary>First index (or vertex) to draw.</summary>
    public int DrawStart { get; set; }

    /// <summary>Number of indices (or vertices) to draw; negative draws everything.</summary>
    public int DrawCount { get; set; } = -1;

    /// <summary>Ranges drawn with different materials. Empty means the whole geometry uses material 0.</summary>
    public IReadOnlyList<GeometryGroup> Groups => _groups;

    /// <summary>The attributes, by name.</summary>
    public IReadOnlyList<KeyValuePair<string, VertexAttribute>> Attributes => _attributes;

    /// <summary>Morph targets, blended by <see cref="Mesh.MorphWeights"/>.</summary>
    public IReadOnlyList<MorphTarget> MorphTargets => _morphTargets;

    /// <summary>Changes when attributes or the index are added, removed or replaced.</summary>
    public int StructureVersion { get; private set; }

    /// <summary>Changes when index contents change.</summary>
    public int IndexVersion { get; private set; }

    /// <summary>Changes when morph targets change.</summary>
    public int MorphVersion { get; private set; }

    /// <summary>True after <see cref="Dispose"/>.</summary>
    public bool IsDisposed { get; private set; }

    /// <summary>The index array (<c>ushort[]</c> or <c>uint[]</c>), or null for non-indexed geometry.</summary>
    public Array? Index => _index;

    /// <summary>Number of indices, or 0.</summary>
    public int IndexCount => _index?.Length ?? 0;

    /// <summary>Number of vertices (of the position attribute).</summary>
    public int VertexCount => GetAttribute(Position)?.Count ?? 0;

    /// <summary>Renderer state attached to this geometry.</summary>
    internal ResourceBinding? Binding;

    /// <summary>The ray casting acceleration structure built for this geometry, with the versions it was built from
    /// (<see cref="StructureVersion"/>, <see cref="IndexVersion"/> and the position attribute's version).</summary>
    internal object? RaycastCache;
    internal long RaycastCacheStamp = -1;

    /// <summary>The attribute with a name, or null.</summary>
    public VertexAttribute? GetAttribute(string name)
    {
        for (var i = 0; i < _attributes.Count; i++)
        {
            if (_attributes[i].Key == name)
            {
                return _attributes[i].Value;
            }
        }

        return null;
    }

    /// <summary>True when an attribute exists.</summary>
    public bool HasAttribute(string name) => GetAttribute(name) is not null;

    /// <summary>Adds or replaces an attribute.</summary>
    public BufferGeometry SetAttribute(string name, VertexAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        for (var i = 0; i < _attributes.Count; i++)
        {
            if (_attributes[i].Key == name)
            {
                _attributes[i] = new(name, attribute);
                Changed(name);
                return this;
            }
        }

        _attributes.Add(new(name, attribute));
        Changed(name);
        return this;
    }

    /// <summary>Removes an attribute.</summary>
    public bool RemoveAttribute(string name)
    {
        for (var i = 0; i < _attributes.Count; i++)
        {
            if (_attributes[i].Key == name)
            {
                _attributes.RemoveAt(i);
                Changed(name);
                return true;
            }
        }

        return false;
    }

    private void Changed(string name)
    {
        StructureVersion++;
        if (name == Position)
        {
            InvalidateBounds();
        }
    }

    /// <summary>Sets 32-bit indices (null removes the index).</summary>
    public BufferGeometry SetIndex(uint[]? indices)
    {
        _index = indices;
        StructureVersion++;
        return this;
    }

    /// <summary>Sets 16-bit indices.</summary>
    public BufferGeometry SetIndex(ushort[] indices)
    {
        _index = indices;
        StructureVersion++;
        return this;
    }

    /// <summary>Sets indices, choosing 16 bits when every vertex fits.</summary>
    public BufferGeometry SetIndex(ReadOnlySpan<int> indices, int vertexCount)
    {
        if (vertexCount <= 65535)
        {
            var a = new ushort[indices.Length];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = (ushort)indices[i];
            }

            return SetIndex(a);
        }

        return SetIndex(MemoryMarshal.Cast<int, uint>(indices).ToArray());
    }

    /// <summary>Tells the renderer that the index array contents changed.</summary>
    public void IndexNeedsUpdate() => IndexVersion++;

    /// <summary>The index element size.</summary>
    public IndexFormat IndexFormat => _index is ushort[] ? IndexFormat.UInt16 : IndexFormat.UInt32;

    /// <summary>One index value.</summary>
    public int GetIndex(int i) => _index switch
    {
        ushort[] s => s[i],
        uint[] u => (int)u[i],
        _ => i,
    };

    /// <summary>The index bytes.</summary>
    internal ReadOnlySpan<byte> IndexBytes => _index switch
    {
        ushort[] s => MemoryMarshal.AsBytes<ushort>(s),
        uint[] u => MemoryMarshal.AsBytes<uint>(u),
        _ => default,
    };

    /// <summary>Adds a material range.</summary>
    public void AddGroup(int start, int count, int materialIndex = 0) => _groups.Add(new GeometryGroup(start, count, materialIndex));

    /// <summary>Removes all material ranges.</summary>
    public void ClearGroups() => _groups.Clear();

    /// <summary>Adds a morph target.</summary>
    public void AddMorphTarget(MorphTarget target)
    {
        _morphTargets.Add(target);
        MorphVersion++;
    }

    /// <summary>The number of indices or vertices one draw of the whole geometry covers.</summary>
    public int ElementCount => _index is not null ? _index.Length : VertexCount;

    /// <summary>The box around all positions (morph targets and skinning are not considered).</summary>
    public BoundingBox BoundingBox
    {
        get
        {
            EnsureBounds();
            return _box;
        }

        set
        {
            _box = value;
            _sphere = BoundingSphere.FromBox(value);
            _boundsValid = true;
            NotifyUsers();
        }
    }

    /// <summary>The sphere around all positions.</summary>
    public BoundingSphere BoundingSphere
    {
        get
        {
            EnsureBounds();
            return _sphere;
        }
    }

    /// <summary>Forces bounds to be computed again, after position data changed.</summary>
    public void InvalidateBounds()
    {
        _boundsValid = false;
        NotifyUsers();
    }

    private void EnsureBounds()
    {
        if (_boundsValid)
        {
            return;
        }

        var box = BoundingBox.Empty;
        var position = GetAttribute(Position);
        var radius2 = 0f;
        if (position is not null)
        {
            var n = position.Count;
            for (var i = 0; i < n; i++)
            {
                box.Expand(position.GetVector3(i));
            }

            var center = box.Center;
            for (var i = 0; i < n; i++)
            {
                radius2 = MathF.Max(radius2, Vector3.DistanceSquared(center, position.GetVector3(i)));
            }
        }

        _box = box;
        _sphere = box.IsEmpty ? BoundingSphere.Empty : new BoundingSphere(box.Center, MathF.Sqrt(radius2));
        _boundsValid = true;
    }

    internal void AddUser(Renderable user) => (_users ??= []).Add(user);

    internal void RemoveUser(Renderable user) => _users?.Remove(user);

    private void NotifyUsers()
    {
        if (_users is null)
        {
            return;
        }

        for (var i = 0; i < _users.Count; i++)
        {
            _users[i].NotifyBoundsChanged();
        }
    }

    /// <summary>Computes smooth vertex normals from the triangles (area weighted).</summary>
    public void ComputeVertexNormals()
    {
        var position = GetAttribute(Position);
        if (position is null || Topology != PrimitiveTopology.Triangles)
        {
            return;
        }

        var count = position.Count;
        var normals = new Vector3[count];
        var elements = ElementCount;
        for (var i = 0; i + 2 < elements; i += 3)
        {
            int a = GetIndex(i), b = GetIndex(i + 1), c = GetIndex(i + 2);
            var pa = position.GetVector3(a);
            var n = Vector3.Cross(position.GetVector3(b) - pa, position.GetVector3(c) - pa);
            normals[a] += n;
            normals[b] += n;
            normals[c] += n;
        }

        for (var i = 0; i < count; i++)
        {
            var l = normals[i].Length();
            normals[i] = l > 0f ? normals[i] / l : Vector3.UnitY;
        }

        SetAttribute(Normal, VertexAttribute.Float(normals));
    }

    /// <summary>Computes tangents from positions, normals and the first UV set (needs all three).</summary>
    public void ComputeTangents()
    {
        var position = GetAttribute(Position);
        var normal = GetAttribute(Normal);
        var uv = GetAttribute(Uv);
        if (position is null || normal is null || uv is null || Topology != PrimitiveTopology.Triangles)
        {
            return;
        }

        var count = position.Count;
        var tan1 = new Vector3[count];
        var tan2 = new Vector3[count];
        var elements = ElementCount;
        for (var i = 0; i + 2 < elements; i += 3)
        {
            int a = GetIndex(i), b = GetIndex(i + 1), c = GetIndex(i + 2);
            Vector3 pa = position.GetVector3(a), pb = position.GetVector3(b), pc = position.GetVector3(c);
            Vector2 ua = uv.GetVector2(a), ub = uv.GetVector2(b), uc = uv.GetVector2(c);
            var e1 = pb - pa;
            var e2 = pc - pa;
            var d1 = ub - ua;
            var d2 = uc - ua;
            var det = (d1.X * d2.Y) - (d2.X * d1.Y);
            if (MathF.Abs(det) < 1e-20f)
            {
                continue;
            }

            var r = 1f / det;
            var s = ((e1 * d2.Y) - (e2 * d1.Y)) * r;
            var t = ((e2 * d1.X) - (e1 * d2.X)) * r;
            tan1[a] += s;
            tan1[b] += s;
            tan1[c] += s;
            tan2[a] += t;
            tan2[b] += t;
            tan2[c] += t;
        }

        var tangents = new float[count * 4];
        for (var i = 0; i < count; i++)
        {
            var n = normal.GetVector3(i);
            var t = tan1[i];
            var ortho = t - (n * Vector3.Dot(n, t));
            if (ortho.LengthSquared() < 1e-20f)
            {
                ortho = MathF.Abs(n.X) > 0.9f ? Vector3.Cross(n, Vector3.UnitY) : Vector3.Cross(n, Vector3.UnitX);
            }

            ortho = Vector3.Normalize(ortho);
            var w = Vector3.Dot(Vector3.Cross(n, t), tan2[i]) < 0f ? -1f : 1f;
            tangents[i * 4] = ortho.X;
            tangents[(i * 4) + 1] = ortho.Y;
            tangents[(i * 4) + 2] = ortho.Z;
            tangents[(i * 4) + 3] = w;
        }

        SetAttribute(Tangent, VertexAttribute.Float(tangents, 4));
    }

    /// <summary>Applies an affine transform to positions, normals and tangents (float attributes only).</summary>
    public BufferGeometry Transform(in Matrix4x4 matrix)
    {
        if (GetAttribute(Position)?.Data is float[] p)
        {
            for (var i = 0; i + 2 < p.Length; i += 3)
            {
                var v = Vector3.Transform(new Vector3(p[i], p[i + 1], p[i + 2]), matrix);
                p[i] = v.X;
                p[i + 1] = v.Y;
                p[i + 2] = v.Z;
            }

            GetAttribute(Position)!.NeedsUpdate();
        }

        if (GetAttribute(Normal)?.Data is float[] n && Matrix4x4.Invert(matrix, out var inverse))
        {
            var normalMatrix = Matrix4x4.Transpose(inverse);
            for (var i = 0; i + 2 < n.Length; i += 3)
            {
                var v = Vector3.Normalize(Vector3.TransformNormal(new Vector3(n[i], n[i + 1], n[i + 2]), normalMatrix));
                n[i] = v.X;
                n[i + 1] = v.Y;
                n[i + 2] = v.Z;
            }

            GetAttribute(Normal)!.NeedsUpdate();
        }

        if (GetAttribute(Tangent) is { ItemSize: 4, Data: float[] t })
        {
            for (var i = 0; i + 3 < t.Length; i += 4)
            {
                var v = Vector3.Normalize(Vector3.TransformNormal(new Vector3(t[i], t[i + 1], t[i + 2]), matrix));
                t[i] = v.X;
                t[i + 1] = v.Y;
                t[i + 2] = v.Z;
            }

            GetAttribute(Tangent)!.NeedsUpdate();
        }

        InvalidateBounds();
        return this;
    }

    /// <summary>Moves the geometry so that the center of its box is at the origin.</summary>
    public BufferGeometry Center() => Transform(Matrix4x4.CreateTranslation(-BoundingBox.Center));

    /// <summary>Frees the GPU buffers of this geometry in every renderer that drew it.</summary>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        ResourceBinding.ReleaseAll(ref Binding);
        GC.SuppressFinalize(this);
    }
}
