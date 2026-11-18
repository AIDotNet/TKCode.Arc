using System.Numerics;
using System.Runtime.InteropServices;
using TKCode.Arc.Graphics3D.Rhi;

namespace TKCode.Arc.Graphics3D;

/// <summary>Finds what a ray hits in a scene: the basis of mouse picking. Meshes are tested triangle by triangle
/// (through a cached <see cref="MeshBvh"/> when they are large), lines and points by distance to the ray. Hidden
/// objects, objects outside <see cref="Layers"/> and materials with <see cref="Material.Visible"/> false are skipped.
/// A raycaster keeps scratch buffers, so reuse one instance; it is not thread-safe.</summary>
public sealed class Raycaster
{
    private static readonly Comparison<RayHit> s_byDistance = static (a, b) => a.Distance.CompareTo(b.Distance);
    private readonly List<MeshBvhHit> _bvhHits = [];
    private Vector3[] _deformed = [];
    private Matrix4x4[] _bones = [];
    private RayHit _best;
    private bool _hasBest;

    /// <summary>Creates a raycaster looking down -Z from the origin.</summary>
    public Raycaster()
        : this(new Ray(Vector3.Zero, -Vector3.UnitZ))
    {
    }

    /// <summary>Creates a raycaster.</summary>
    /// <param name="ray">The ray in world space.</param>
    /// <param name="near">Hits closer than this are ignored.</param>
    /// <param name="far">Hits farther than this are ignored.</param>
    public Raycaster(Ray ray, float near = 0f, float far = float.PositiveInfinity)
    {
        Ray = ray;
        Near = near;
        Far = far;
    }

    /// <summary>The ray in world space.</summary>
    public Ray Ray { get; set; }

    /// <summary>Hits closer than this are ignored.</summary>
    public float Near { get; set; }

    /// <summary>Hits farther than this are ignored.</summary>
    public float Far { get; set; }

    /// <summary>Only objects that share a layer with this set are tested. Default: layer 0.</summary>
    public Layers Layers { get; set; } = Layers.Default;

    /// <summary>How close, in world units, the ray must pass to a line to hit it.</summary>
    public float LineThreshold { get; set; } = 1f;

    /// <summary>How close, in world units, the ray must pass to a point to hit it.</summary>
    public float PointThreshold { get; set; } = 1f;

    /// <summary>The camera the ray was built from. Sprites face it, so they are only tested when it is set.</summary>
    public Camera? Camera { get; set; }

    /// <summary>True returns only the closest hit, which is faster.</summary>
    public bool FirstHitOnly { get; set; }

    /// <summary>Points the ray through a position of a camera's view.</summary>
    /// <param name="ndc">Normalized device coordinates: x and y in -1..1, y up.</param>
    /// <param name="camera">The camera; it becomes <see cref="Camera"/>.</param>
    public void SetFromCamera(Vector2 ndc, Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        Camera = camera;
        if (camera.IsOrthographic)
        {
            // Parallel rays: start on the near plane and go along the view direction.
            var world = camera.WorldMatrix;
            Ray = new Ray(camera.Unproject(new Vector3(ndc, 0f)), new Vector3(-world.M31, -world.M32, -world.M33));
        }
        else
        {
            var origin = camera.WorldPosition;
            Ray = new Ray(origin, camera.Unproject(new Vector3(ndc, 0.5f)) - origin);
        }
    }

    /// <summary>Points the ray through a pixel of a viewport (origin top-left, y down).</summary>
    public void SetFromCamera(float x, float y, float viewportWidth, float viewportHeight, Camera camera) =>
        SetFromCamera(new Vector2(((x / viewportWidth) * 2f) - 1f, 1f - ((y / viewportHeight) * 2f)), camera);

    /// <summary>Returns the hits on an object (and, when <paramref name="recursive"/>, everything below it), closest first.</summary>
    public List<RayHit> Intersect(Object3D root, bool recursive = true)
    {
        var hits = new List<RayHit>();
        Intersect(root, hits, recursive);
        return hits;
    }

    /// <summary>Replaces the contents of <paramref name="hits"/> with the hits on an object (and, when
    /// <paramref name="recursive"/>, everything below it), closest first. Reusing the list avoids allocations.</summary>
    public void Intersect(Object3D root, List<RayHit> hits, bool recursive = true)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(hits);
        hits.Clear();
        _hasBest = false;
        if (!root.IsEffectivelyVisible())
        {
            return;
        }

        if (recursive && root is Scene scene)
        {
            IntersectScene(scene, hits);
        }
        else
        {
            Walk(root, hits, recursive);
        }

        if (FirstHitOnly)
        {
            if (_hasBest)
            {
                hits.Add(_best);
            }

            _best = default;
            return;
        }

        if (hits.Count > 1)
        {
            CollectionsMarshal.AsSpan(hits).Sort(s_byDistance);
        }
    }

    /// <summary>The closest hit on an object and everything below it.</summary>
    public bool IntersectFirst(Object3D root, out RayHit hit)
    {
        var all = FirstHitOnly;
        FirstHitOnly = true;
        try
        {
            _bestList.Clear();
            Intersect(root, _bestList);
            if (_bestList.Count > 0)
            {
                hit = _bestList[0];
                return true;
            }

            hit = default;
            return false;
        }
        finally
        {
            FirstHitOnly = all;
            _bestList.Clear();
        }
    }

    private readonly List<RayHit> _bestList = new(1);

    // ---- Traversal ----

    private void IntersectScene(Scene scene, List<RayHit> hits)
    {
        // Broad phase over the scene's flat arrays: no graph walk, and the bounding spheres are already in world space.
        var world = scene.World;
        world.Sync();
        var n = world.HighWater;
        var flags = world.Flags;
        var layers = world.LayerMasks;
        var spheres = world.Spheres;
        var mask = Layers.Mask;
        const ProxyFlags Need = ProxyFlags.Alive | ProxyFlags.Visible;
        for (var i = 0; i < n; i++)
        {
            if ((flags[i] & Need) != Need || (layers[i] & mask) == 0)
            {
                continue;
            }

            var obj = world.Objects[i]!;
            if (HasReliableBounds(obj) && !SphereMayHit(spheres[i], Padding(obj)))
            {
                continue;
            }

            Test(obj, hits);
        }
    }

    private void Walk(Object3D node, List<RayHit> hits, bool recursive)
    {
        if (!node.Visible)
        {
            return;
        }

        if (node is Renderable renderable && Layers.Test(node.Layers))
        {
            if (!HasReliableBounds(renderable) || SphereMayHit(renderable.LocalBoundingSphere.Transform(renderable.WorldMatrix), Padding(renderable)))
            {
                Test(renderable, hits);
            }
        }

        if (!recursive)
        {
            return;
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            Walk(children[i], hits, true);
        }
    }

    /// <summary>False when the static bounds do not contain what is drawn: posed skins, morphed meshes, fixed-size sprites.</summary>
    private static bool HasReliableBounds(Renderable obj)
    {
        if (obj is SkinnedMesh { Skeleton: not null })
        {
            return false;
        }

        // A sprite that keeps its size on screen grows with distance: its node's bounds say nothing.
        if (obj is Sprite { Material: SpriteMaterial { SizeAttenuation: false } })
        {
            return false;
        }

        return obj is not Mesh mesh || !HasActiveMorph(mesh);
    }

    private float Padding(Renderable obj) => obj switch
    {
        Line => LineThreshold,
        Points => PointThreshold,
        _ => 0f,
    };

    private static bool HasActiveMorph(Mesh mesh)
    {
        var weights = mesh.MorphWeights;
        if (weights is null || mesh.Geometry.MorphTargets.Count == 0)
        {
            return false;
        }

        foreach (var w in weights)
        {
            if (w != 0f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The largest distance still worth testing: <see cref="Far"/>, or the best hit so far.</summary>
    private float Limit => FirstHitOnly && _hasBest ? MathF.Min(Far, _best.Distance) : Far;

    private bool SphereMayHit(in BoundingSphere sphere, float padding)
    {
        if (sphere.IsEmpty)
        {
            return false;
        }

        var ray = Ray;
        var r = sphere.Radius + padding;
        var oc = sphere.Center - ray.Origin;
        var tca = Vector3.Dot(oc, ray.Direction);
        var d2 = Vector3.Dot(oc, oc) - (tca * tca);
        var r2 = r * r;
        if (d2 > r2)
        {
            return false;
        }

        var thc = MathF.Sqrt(r2 - d2);
        return tca + thc >= Near && tca - thc <= Limit;
    }

    private void Add(List<RayHit> hits, in RayHit hit)
    {
        if (!FirstHitOnly)
        {
            hits.Add(hit);
        }
        else if (!_hasBest || hit.Distance < _best.Distance)
        {
            _best = hit;
            _hasBest = true;
        }
    }

    private void Test(Renderable obj, List<RayHit> hits)
    {
        switch (obj)
        {
            case InstancedMesh instanced:
                TestInstanced(instanced, hits);
                break;
            case Mesh mesh:
                TestMesh(mesh, hits);
                break;
            case Line line:
                TestLine(line, hits);
                break;
            case Points points:
                TestPoints(points, hits);
                break;
            case Sprite sprite:
                TestSprite(sprite, hits);
                break;
        }
    }

    // ---- Meshes ----

    private void TestMesh(Mesh mesh, List<RayHit> hits)
    {
        var geometry = mesh.Geometry;
        var skinned = mesh as SkinnedMesh;
        var skin = skinned?.Skeleton is not null && geometry.HasAttribute(BufferGeometry.SkinIndex) && geometry.HasAttribute(BufferGeometry.SkinWeight);
        var morph = HasActiveMorph(mesh);
        if (!skin && !morph)
        {
            TestTriangles(mesh, geometry, mesh.WorldMatrix, new TriangleSource(geometry), useBvh: true, -1, hits);
            return;
        }

        var deformed = Deform(mesh, geometry, skin ? skinned : null, morph);
        if (deformed is null)
        {
            return;
        }

        // Skinned vertices are already in world space: the mesh's own transform does not apply (as in glTF).
        TestTriangles(mesh, geometry, skin ? Matrix4x4.Identity : mesh.WorldMatrix, new TriangleSource(geometry, deformed), useBvh: false, -1, hits);
    }

    private void TestInstanced(InstancedMesh mesh, List<RayHit> hits)
    {
        var geometry = mesh.Geometry;
        var sphere = geometry.BoundingSphere;
        if (sphere.IsEmpty)
        {
            return;
        }

        var world = mesh.WorldMatrix;
        var source = new TriangleSource(geometry);
        var matrices = mesh.Matrices;
        var count = Math.Min(mesh.Count, matrices.Length);
        for (var i = 0; i < count; i++)
        {
            var instanceWorld = matrices[i] * world;
            if (SphereMayHit(sphere.Transform(instanceWorld), 0f))
            {
                TestTriangles(mesh, geometry, instanceWorld, source, useBvh: true, i, hits);
            }
        }
    }

    /// <summary>Applies morph targets and skinning on the CPU. Returns one position per vertex: world space with a
    /// skin, local space otherwise.</summary>
    private Vector3[]? Deform(Mesh mesh, BufferGeometry geometry, SkinnedMesh? skinned, bool morph)
    {
        var position = geometry.GetAttribute(BufferGeometry.Position);
        if (position is null)
        {
            return null;
        }

        var count = position.Count;
        if (_deformed.Length < count)
        {
            _deformed = new Vector3[Math.Max(count, _deformed.Length * 2)];
        }

        var result = _deformed;
        for (var i = 0; i < count; i++)
        {
            result[i] = position.GetVector3(i);
        }

        if (morph)
        {
            var weights = mesh.MorphWeights!;
            var targets = geometry.MorphTargets;
            for (var t = 0; t < targets.Count && t < weights.Length; t++)
            {
                var w = weights[t];
                if (w == 0f)
                {
                    continue;
                }

                var deltas = targets[t].PositionDeltas;
                var n = Math.Min(count, deltas.Length / 3);
                for (var i = 0; i < n; i++)
                {
                    result[i] += new Vector3(deltas[i * 3], deltas[(i * 3) + 1], deltas[(i * 3) + 2]) * w;
                }
            }
        }

        if (skinned?.Skeleton is { } skeleton)
        {
            var bones = skeleton.Bones;
            if (_bones.Length < bones.Length)
            {
                _bones = new Matrix4x4[Math.Max(bones.Length, _bones.Length * 2)];
            }

            var matrices = _bones;
            var bind = skinned.BindMatrix;
            for (var i = 0; i < bones.Length; i++)
            {
                matrices[i] = bind * skeleton.InverseBindMatrices[i] * bones[i].WorldMatrix;
            }

            var joints = geometry.GetAttribute(BufferGeometry.SkinIndex)!;
            var jointWeights = geometry.GetAttribute(BufferGeometry.SkinWeight)!;
            var n = Math.Min(count, Math.Min(joints.Count, jointWeights.Count));
            for (var i = 0; i < n; i++)
            {
                var w = jointWeights.GetVector4(i);
                if (jointWeights.ItemSize < 4)
                {
                    w.W = 0f;
                }

                var sum = w.X + w.Y + w.Z + w.W;
                w /= MathF.Max(sum, 1e-6f);
                var p = result[i];
                var q = Vector3.Zero;
                for (var k = 0; k < 4; k++)
                {
                    var wk = k == 0 ? w.X : k == 1 ? w.Y : k == 2 ? w.Z : w.W;
                    if (wk == 0f || k >= joints.ItemSize)
                    {
                        continue;
                    }

                    var joint = (int)joints.Get(i, k);
                    if ((uint)joint < (uint)bones.Length)
                    {
                        q += Vector3.Transform(p, matrices[joint]) * wk;
                    }
                }

                result[i] = q;
            }
        }

        return result;
    }

    private void TestTriangles(
        Mesh mesh, BufferGeometry geometry, in Matrix4x4 world, in TriangleSource source, bool useBvh, int instanceId, List<RayHit> hits)
    {
        var topology = mesh.Topology;
        var strip = topology == PrimitiveTopology.TriangleStrip;
        if (!strip && topology != PrimitiveTopology.Triangles)
        {
            return;
        }

        var elements = source.ElementCount;
        if (elements < 3 || !Matrix4x4.Invert(world, out var inverse))
        {
            return;
        }

        // Not normalized: a parameter along the local ray is then a distance along the world ray.
        var local = Ray.TransformUnnormalized(inverse);
        var start = Math.Clamp(geometry.DrawStart, 0, elements);
        var count = geometry.DrawCount < 0 ? elements - start : Math.Min(geometry.DrawCount, elements - start);
        var bvh = useBvh && !strip && elements / 3 >= MeshBvh.MinimumTriangles ? MeshBvh.Get(geometry) : null;
        var groups = geometry.Groups;
        var materials = mesh.Materials;
        if (groups.Count == 0)
        {
            TestRange(mesh, geometry, world, inverse, source, local, bvh, strip, start, start + count, materials[0], 0, instanceId, hits);
            return;
        }

        for (var g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var s = Math.Max(group.Start, start);
            var e = Math.Min(group.Start + group.Count, start + count);
            if (e > s)
            {
                var material = materials[Math.Min(group.MaterialIndex, materials.Length - 1)];
                TestRange(mesh, geometry, world, inverse, source, local, bvh, strip, s, e, material, group.MaterialIndex, instanceId, hits);
            }
        }
    }

    private void TestRange(
        Mesh mesh, BufferGeometry geometry, in Matrix4x4 world, in Matrix4x4 inverse, in TriangleSource source, Ray local, MeshBvh? bvh, bool strip,
        int start, int end, Material material, int materialIndex, int instanceId, List<RayHit> hits)
    {
        if (!material.Visible)
        {
            return;
        }

        var side = material.Side;
        var origin = local.Origin;
        var direction = local.Direction;
        if (bvh is not null)
        {
            if (FirstHitOnly)
            {
                if (bvh.RaycastClosest(source, origin, direction, Near, Limit, start, end, side, out var h))
                {
                    AddMeshHit(mesh, geometry, world, inverse, source, h.Triangle * 3, false, h.T, h.U, h.V, materialIndex, instanceId, hits);
                }

                return;
            }

            _bvhHits.Clear();
            bvh.RaycastAll(source, origin, direction, Near, Far, start, end, side, _bvhHits);
            for (var i = 0; i < _bvhHits.Count; i++)
            {
                var h = _bvhHits[i];
                AddMeshHit(mesh, geometry, world, inverse, source, h.Triangle * 3, false, h.T, h.U, h.V, materialIndex, instanceId, hits);
            }

            return;
        }

        var step = strip ? 1 : 3;
        for (var element = start; element + 3 <= end; element += step)
        {
            // Every other triangle of a strip has the opposite winding.
            var flip = strip && ((element - start) & 1) == 1;
            Corners(source, element, flip, out var a, out var b, out var c);
            if (MeshBvh.IntersectTriangle(origin, direction, a, b, c, side, out var t, out var u, out var v) && t >= Near && t <= Limit)
            {
                AddMeshHit(mesh, geometry, world, inverse, source, element, flip, t, u, v, materialIndex, instanceId, hits);
            }
        }
    }

    private static void Corners(in TriangleSource source, int element, bool flip, out Vector3 a, out Vector3 b, out Vector3 c)
    {
        source.Triangle(element, out a, out b, out c);
        if (flip)
        {
            (a, b) = (b, a);
        }
    }

    private void AddMeshHit(
        Mesh mesh, BufferGeometry geometry, in Matrix4x4 world, in Matrix4x4 inverse, in TriangleSource source, int element, bool flip,
        float t, float u, float v, int materialIndex, int instanceId, List<RayHit> hits)
    {
        if (FirstHitOnly && _hasBest && t >= _best.Distance)
        {
            return;
        }

        Corners(source, element, flip, out var a, out var b, out var c);
        int ia = source.Index(element), ib = source.Index(element + 1), ic = source.Index(element + 2);
        if (flip)
        {
            (ia, ib) = (ib, ia);
        }

        var w = 1f - u - v;
        var localPoint = (a * w) + (b * u) + (c * v);
        // Normals transform with the inverse transpose, which also keeps them outward under mirroring.
        var normal = Vector3.TransformNormal(Vector3.Cross(b - a, c - a), Matrix4x4.Transpose(inverse));
        var length = normal.Length();
        var hit = new RayHit
        {
            Distance = t,
            Point = Vector3.Transform(localPoint, world),
            Object = mesh,
            FaceIndex = element / 3,
            Normal = length > 0f ? normal / length : Vector3.Zero,
            Barycentric = new Vector3(w, u, v),
            InstanceId = instanceId,
            Index = -1,
            MaterialIndex = materialIndex,
        };
        if (geometry.GetAttribute(BufferGeometry.Uv) is { } uv)
        {
            hit.Uv = (uv.GetVector2(ia) * w) + (uv.GetVector2(ib) * u) + (uv.GetVector2(ic) * v);
        }

        if (geometry.GetAttribute(BufferGeometry.Uv1) is { } uv1)
        {
            hit.Uv1 = (uv1.GetVector2(ia) * w) + (uv1.GetVector2(ib) * u) + (uv1.GetVector2(ic) * v);
        }

        Add(hits, hit);
    }

    // ---- Lines, points, sprites ----

    private void TestLine(Line line, List<RayHit> hits)
    {
        var geometry = line.Geometry;
        var material = line.Material;
        var source = new TriangleSource(geometry);
        var elements = source.ElementCount;
        if (!material.Visible || elements < 2)
        {
            return;
        }

        var world = line.WorldMatrix;
        var ray = Ray;
        var threshold2 = LineThreshold * LineThreshold;
        var start = Math.Clamp(geometry.DrawStart, 0, elements);
        var end = start + (geometry.DrawCount < 0 ? elements - start : Math.Min(geometry.DrawCount, elements - start));
        var step = line.Topology == PrimitiveTopology.Lines ? 2 : 1;
        for (var element = start; element + 1 < end; element += step)
        {
            var a = Vector3.Transform(source.Vertex(source.Index(element)), world);
            var b = Vector3.Transform(source.Vertex(source.Index(element + 1)), world);
            var d2 = RaySegmentDistanceSquared(ray, a, b, out var t, out var onSegment);
            if (d2 > threshold2 || t < Near || t > Limit)
            {
                continue;
            }

            Add(hits, new RayHit
            {
                Distance = t,
                Point = onSegment,
                Object = line,
                FaceIndex = -1,
                InstanceId = -1,
                Index = element,
                DistanceToRay = MathF.Sqrt(d2),
            });
        }
    }

    /// <summary>Squared distance between a ray and a segment, with the ray parameter and the segment point of the
    /// closest approach.</summary>
    internal static float RaySegmentDistanceSquared(Ray ray, Vector3 a, Vector3 b, out float rayT, out Vector3 onSegment)
    {
        var d = ray.Direction;
        var seg = b - a;
        var r = ray.Origin - a;
        var segLen2 = Vector3.Dot(seg, seg);
        var dSeg = Vector3.Dot(d, seg);
        var dR = Vector3.Dot(d, r);
        var segR = Vector3.Dot(seg, r);
        float s;
        var denominator = segLen2 - (dSeg * dSeg);
        if (segLen2 <= 1e-20f)
        {
            s = 0f;
        }
        else if (denominator > 1e-12f * segLen2)
        {
            // Closest point of the infinite lines, clamped to the segment.
            s = Math.Clamp((segR - (dSeg * dR)) / denominator, 0f, 1f);
        }
        else
        {
            // Parallel: any point of the segment is as close as another; take the one nearest to the origin.
            s = Math.Clamp(segR / segLen2, 0f, 1f);
        }

        onSegment = a + (seg * s);
        rayT = Vector3.Dot(onSegment - ray.Origin, d);
        if (rayT < 0f)
        {
            // The closest approach is behind the origin: measure from the origin instead.
            rayT = 0f;
            s = segLen2 <= 1e-20f ? 0f : Math.Clamp(segR / segLen2, 0f, 1f);
            onSegment = a + (seg * s);
        }

        return Vector3.DistanceSquared(ray.Origin + (d * rayT), onSegment);
    }

    private void TestPoints(Points points, List<RayHit> hits)
    {
        var geometry = points.Geometry;
        var material = points.Material;
        var source = new TriangleSource(geometry);
        var elements = source.ElementCount;
        if (!material.Visible || elements == 0)
        {
            return;
        }

        var world = points.WorldMatrix;
        var ray = Ray;
        var threshold2 = PointThreshold * PointThreshold;
        var start = Math.Clamp(geometry.DrawStart, 0, elements);
        var end = start + (geometry.DrawCount < 0 ? elements - start : Math.Min(geometry.DrawCount, elements - start));
        for (var element = start; element < end; element++)
        {
            var vertex = source.Index(element);
            var p = Vector3.Transform(source.Vertex(vertex), world);
            var t = Vector3.Dot(p - ray.Origin, ray.Direction);
            if (t < 0f)
            {
                continue;
            }

            var closest = ray.Origin + (ray.Direction * t);
            var d2 = Vector3.DistanceSquared(closest, p);
            if (d2 > threshold2 || t < Near || t > Limit)
            {
                continue;
            }

            Add(hits, new RayHit
            {
                Distance = t,
                Point = closest,
                Object = points,
                FaceIndex = -1,
                InstanceId = -1,
                Index = vertex,
                DistanceToRay = MathF.Sqrt(d2),
            });
        }
    }

    private void TestSprite(Sprite sprite, List<RayHit> hits)
    {
        var camera = Camera;
        var material = sprite.Material;
        if (camera is null || !material.Visible)
        {
            return;
        }

        // Rebuild the quad the vertex stage draws: the node's origin in view space, plus corners scaled by the node.
        var world = sprite.WorldMatrix;
        var cameraWorld = camera.WorldMatrix;
        var view = camera.ViewMatrix;
        var center = Vector3.Transform(world.Translation, view);
        var scale = new Vector2(new Vector3(world.M11, world.M12, world.M13).Length(), new Vector3(world.M21, world.M22, world.M23).Length());
        var rotation = 0f;
        if (material is SpriteMaterial spriteMaterial)
        {
            rotation = spriteMaterial.Rotation;
            if (!spriteMaterial.SizeAttenuation && !camera.IsOrthographic)
            {
                scale *= -center.Z;
            }
        }

        var (sin, cos) = MathF.SinCos(rotation);
        Vector3 Corner(float x, float y)
        {
            var px = x * scale.X;
            var py = y * scale.Y;
            var v = new Vector3(center.X + (cos * px) - (sin * py), center.Y + (sin * px) + (cos * py), center.Z);
            return Vector3.Transform(v, cameraWorld);
        }

        var a = Corner(-0.5f, -0.5f);
        var b = Corner(0.5f, -0.5f);
        var c = Corner(0.5f, 0.5f);
        var d = Corner(-0.5f, 0.5f);
        var ray = Ray;
        Vector2 uv;
        if (MeshBvh.IntersectTriangle(ray.Origin, ray.Direction, a, b, c, Side.Double, out var t, out var u, out var v))
        {
            uv = new Vector2(u + v, v);
        }
        else if (MeshBvh.IntersectTriangle(ray.Origin, ray.Direction, a, c, d, Side.Double, out t, out u, out v))
        {
            uv = new Vector2(u, u + v);
        }
        else
        {
            return;
        }

        if (t < Near || t > Limit)
        {
            return;
        }

        Add(hits, new RayHit
        {
            Distance = t,
            Point = ray.At(t),
            Object = sprite,
            FaceIndex = -1,
            Normal = Vector3.Normalize(new Vector3(cameraWorld.M31, cameraWorld.M32, cameraWorld.M33)),
            Uv = uv,
            InstanceId = -1,
            Index = -1,
        });
    }
}
