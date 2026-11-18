using System.Diagnostics;
using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests.Raycasting;

public class MeshBvhTests(ITestOutputHelper output)
{
    private static BufferGeometry Terrain(int cells, float size, int seed)
    {
        // A bumpy grid: cells * cells * 2 triangles.
        var random = new Random(seed);
        var n = cells + 1;
        var positions = new float[n * n * 3];
        for (var z = 0; z < n; z++)
        {
            for (var x = 0; x < n; x++)
            {
                var o = ((z * n) + x) * 3;
                positions[o] = ((float)x / cells * size) - (size / 2f);
                positions[o + 1] = (MathF.Sin(x * 0.05f) * MathF.Cos(z * 0.07f) * 2f) + (random.NextSingle() * 0.2f);
                positions[o + 2] = ((float)z / cells * size) - (size / 2f);
            }
        }

        var indices = new uint[cells * cells * 6];
        var i = 0;
        for (var z = 0; z < cells; z++)
        {
            for (var x = 0; x < cells; x++)
            {
                var a = (uint)((z * n) + x);
                var b = a + 1;
                var c = a + (uint)n;
                var d = c + 1;
                indices[i++] = a;
                indices[i++] = c;
                indices[i++] = b;
                indices[i++] = b;
                indices[i++] = c;
                indices[i++] = d;
            }
        }

        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(positions, 3));
        geometry.SetIndex(indices);
        return geometry;
    }

    private static List<MeshBvhHit> BruteForce(BufferGeometry geometry, Ray ray, Side side)
    {
        var hits = new List<MeshBvhHit>();
        var position = geometry.GetAttribute(BufferGeometry.Position)!;
        for (var e = 0; e + 2 < geometry.ElementCount; e += 3)
        {
            var a = position.GetVector3(geometry.GetIndex(e));
            var b = position.GetVector3(geometry.GetIndex(e + 1));
            var c = position.GetVector3(geometry.GetIndex(e + 2));
            if (MeshBvh.IntersectTriangle(ray.Origin, ray.Direction, a, b, c, side, out var t, out var u, out var v))
            {
                hits.Add(new MeshBvhHit { Triangle = e / 3, T = t, U = u, V = v });
            }
        }

        hits.Sort((x, y) => x.T.CompareTo(y.T));
        return hits;
    }

    [Fact]
    public void Bvh_Matches_Brute_Force_On_Random_Rays()
    {
        var sphere = new SphereGeometry(2f, 48, 32);
        var terrain = Terrain(60, 20f, 3);
        var random = new Random(11);
        var all = new List<MeshBvhHit>();
        foreach (var geometry in new BufferGeometry[] { sphere, terrain })
        {
            var bvh = MeshBvh.Build(geometry)!;
            bvh.TriangleCount.ShouldBe(geometry.ElementCount / 3);
            foreach (var side in new[] { Side.Front, Side.Back, Side.Double })
            {
                for (var n = 0; n < 300; n++)
                {
                    var origin = new Vector3((random.NextSingle() * 16f) - 8f, (random.NextSingle() * 12f) - 2f, (random.NextSingle() * 16f) - 8f);
                    var target = new Vector3((random.NextSingle() * 6f) - 3f, (random.NextSingle() * 2f) - 1f, (random.NextSingle() * 6f) - 3f);
                    var ray = new Ray(origin, target - origin);
                    var expected = BruteForce(geometry, ray, side);

                    all.Clear();
                    bvh.RaycastAll(ray, 0f, float.PositiveInfinity, side, all);
                    all.Sort((x, y) => x.T.CompareTo(y.T));
                    all.Count.ShouldBe(expected.Count);
                    for (var i = 0; i < expected.Count; i++)
                    {
                        all[i].T.ShouldBe(expected[i].T, 1e-4f);
                    }

                    var found = bvh.RaycastClosest(ray, 0f, float.PositiveInfinity, side, out var closest);
                    found.ShouldBe(expected.Count > 0);
                    if (found)
                    {
                        closest.T.ShouldBe(expected[0].T, 1e-5f);
                    }

                    // Limits.
                    if (expected.Count > 1)
                    {
                        var between = (expected[0].T + expected[1].T) / 2f;
                        if (between > expected[0].T && between < expected[1].T)
                        {
                            bvh.RaycastClosest(ray, between, float.PositiveInfinity, side, out closest).ShouldBeTrue();
                            closest.T.ShouldBe(expected[1].T, 1e-5f);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Axis_Aligned_Rays_And_Rays_In_Box_Planes_Are_Handled()
    {
        var box = new BoxGeometry(2f, 2f, 2f, 8, 8, 8);
        var bvh = MeshBvh.Build(box)!;
        var hits = new List<MeshBvhHit>();
        foreach (var (origin, direction) in new[]
        {
            (new Vector3(0.3f, 5f, 0.3f), -Vector3.UnitY),
            (new Vector3(5f, 0.25f, -0.5f), -Vector3.UnitX),
            (new Vector3(-0.75f, 0.5f, -5f), Vector3.UnitZ),
            (new Vector3(0f, 5f, 0f), -Vector3.UnitY),
        })
        {
            var ray = new Ray(origin, direction);
            hits.Clear();
            bvh.RaycastAll(ray, 0f, float.PositiveInfinity, Side.Double, hits);
            var expected = BruteForce(box, ray, Side.Double);
            hits.Count.ShouldBe(expected.Count);
            expected.Count.ShouldBeGreaterThanOrEqualTo(2);
            bvh.RaycastClosest(ray, 0f, float.PositiveInfinity, Side.Front, out var closest).ShouldBeTrue();
            closest.T.ShouldBe(4f, 1e-4f);
        }
    }

    [Fact]
    public void Cached_Bvh_Is_Reused_And_Rebuilt_When_Positions_Change()
    {
        var geometry = new SphereGeometry(1f, 32, 16);
        var first = MeshBvh.Get(geometry)!;

        MeshBvh.Get(geometry).ShouldBeSameAs(first);
        var position = geometry.GetAttribute(BufferGeometry.Position)!;
        var data = position.FloatData;
        for (var i = 0; i < data.Length; i++)
        {
            data[i] *= 3f;
        }

        position.NeedsUpdate();
        geometry.InvalidateBounds();
        var second = MeshBvh.Get(geometry)!;
        second.ShouldNotBeSameAs(first);
        second.Bounds.Max.Y.ShouldBe(3f, 1e-4f);

        // The raycaster sees the new shape.
        var mesh = new Mesh(geometry, new BasicMaterial());
        var hits = new Raycaster(new Ray(new Vector3(0f, 10f, 0.01f), -Vector3.UnitY)).Intersect(mesh);
        hits.Count.ShouldBe(1);
        hits[0].Distance.ShouldBe(7f, 1e-2f);
    }

    [Fact]
    public void Degenerate_Geometry_Does_Not_Break_The_Build()
    {
        // Many identical triangles: every centroid coincides.
        var positions = new float[9 * 500];
        for (var i = 0; i < 500; i++)
        {
            new float[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, -1f }.CopyTo(positions, i * 9);
        }

        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(positions, 3));
        var bvh = MeshBvh.Build(geometry)!;
        var hits = new List<MeshBvhHit>();

        bvh.RaycastAll(new Ray(new Vector3(0.2f, 5f, -0.2f), -Vector3.UnitY), 0f, float.PositiveInfinity, Side.Double, hits);

        hits.Count.ShouldBe(500);
        MeshBvh.Build(new BufferGeometry()).ShouldBeNull();
    }

    [Fact]
    public void Picking_A_Million_Triangles_Is_Fast()
    {
        var geometry = Terrain(708, 200f, 5);
        var triangles = geometry.ElementCount / 3;
        triangles.ShouldBeGreaterThan(1_000_000);
        var mesh = new Mesh(geometry, new BasicMaterial { Side = Side.Double });
        var scene = new Scene();
        scene.Add(mesh);

        var build = Stopwatch.StartNew();
        var bvh = MeshBvh.Get(geometry)!;
        build.Stop();

        var random = new Random(1);
        var raycaster = new Raycaster { FirstHitOnly = true };
        var hits = new List<RayHit>(4);
        var rays = new Ray[2000];
        for (var i = 0; i < rays.Length; i++)
        {
            var origin = new Vector3((random.NextSingle() * 160f) - 80f, 40f + (random.NextSingle() * 20f), (random.NextSingle() * 160f) - 80f);
            var target = new Vector3((random.NextSingle() * 160f) - 80f, 0f, (random.NextSingle() * 160f) - 80f);
            rays[i] = new Ray(origin, target - origin);
        }

        // Warm up the JIT, then measure.
        for (var i = 0; i < 200; i++)
        {
            raycaster.Ray = rays[i];
            raycaster.Intersect(scene, hits);
        }

        var hitCount = 0;
        var worst = 0d;
        var total = Stopwatch.StartNew();
        for (var i = 0; i < rays.Length; i++)
        {
            var one = Stopwatch.GetTimestamp();
            raycaster.Ray = rays[i];
            raycaster.Intersect(scene, hits);
            worst = Math.Max(worst, Stopwatch.GetElapsedTime(one).TotalMilliseconds);
            hitCount += hits.Count;
        }

        total.Stop();
        var average = total.Elapsed.TotalMilliseconds / rays.Length;
        output.WriteLine($"{triangles} triangles, {bvh.NodeCount} nodes: build {build.ElapsedMilliseconds} ms, pick average {average * 1000:0.0} µs, worst {worst * 1000:0.0} µs, {hitCount} hits");
        hitCount.ShouldBe(rays.Length);
        average.ShouldBeLessThan(5d);

        // All-hits mode on the same mesh.
        raycaster.FirstHitOnly = false;
        var all = Stopwatch.StartNew();
        for (var i = 0; i < rays.Length; i++)
        {
            raycaster.Ray = rays[i];
            raycaster.Intersect(scene, hits);
        }

        all.Stop();
        output.WriteLine($"all hits: average {all.Elapsed.TotalMilliseconds / rays.Length * 1000:0.0} µs");
        (all.Elapsed.TotalMilliseconds / rays.Length).ShouldBeLessThan(5d);
    }
}
