using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests.Raycasting;

public class RaycasterTests
{
    private static Raycaster Down(float x, float z, float height = 10f) => new(new Ray(new Vector3(x, height, z), -Vector3.UnitY));

    private static Mesh Floor(Side side = Side.Front)
    {
        // PlaneGeometry faces +Z; lay it flat so it faces +Y.
        var mesh = new Mesh(new PlaneGeometry(4f, 4f), new BasicMaterial { Side = side });
        mesh.RotateX(-MathF.PI / 2f);
        return mesh;
    }

    [Fact]
    public void Mesh_Hit_Reports_Distance_Point_Normal_Uv_And_Barycentric()
    {
        var scene = new Scene();
        var floor = Floor();
        floor.Position = new Vector3(0f, 1f, 0f);
        scene.Add(floor);

        var hits = Down(1f, -0.5f).Intersect(scene);

        hits.Count.ShouldBe(1);
        var hit = hits[0];
        hit.Object.ShouldBeSameAs(floor);
        hit.Distance.ShouldBe(9f, 1e-4f);
        Vector3.Distance(hit.Point, new Vector3(1f, 1f, -0.5f)).ShouldBeLessThan(1e-4f);
        Vector3.Distance(hit.Normal, Vector3.UnitY).ShouldBeLessThan(1e-4f);
        // The plane's local +Y maps to world -Z after the rotation: u = (x + 2) / 4, v = (-z + 2) / 4.
        hit.Uv.X.ShouldBe(0.75f, 1e-4f);
        hit.Uv.Y.ShouldBe(0.625f, 1e-4f);
        (hit.Barycentric.X + hit.Barycentric.Y + hit.Barycentric.Z).ShouldBe(1f, 1e-5f);
        hit.Barycentric.X.ShouldBeGreaterThanOrEqualTo(0f);
        hit.FaceIndex.ShouldBeInRange(0, 1);
        hit.InstanceId.ShouldBe(-1);
        hit.MaterialIndex.ShouldBe(0);
    }

    [Fact]
    public void Front_Side_Is_Missed_From_Behind_Back_And_Double_Are_Hit()
    {
        var scene = new Scene();
        var floor = Floor();
        scene.Add(floor);
        var fromBelow = new Raycaster(new Ray(new Vector3(0.3f, -5f, 0.2f), Vector3.UnitY));

        fromBelow.Intersect(scene).ShouldBeEmpty();
        floor.Material.Side = Side.Back;
        fromBelow.Intersect(scene).Count.ShouldBe(1);
        Down(0.3f, 0.2f).Intersect(scene).ShouldBeEmpty();
        floor.Material.Side = Side.Double;
        fromBelow.Intersect(scene).Count.ShouldBe(1);
        Down(0.3f, 0.2f).Intersect(scene).Count.ShouldBe(1);
    }

    [Fact]
    public void Hits_Are_Sorted_By_Distance_And_FirstHitOnly_Returns_The_Closest()
    {
        var scene = new Scene();
        for (var i = 0; i < 5; i++)
        {
            var floor = Floor();
            floor.Position = new Vector3(0f, i, 0f);
            floor.Name = "f" + i;
            scene.Add(floor);
        }

        var raycaster = Down(0.5f, 0.5f);
        var hits = raycaster.Intersect(scene);

        hits.Count.ShouldBe(5);
        hits.Select(h => h.Object.Name).ShouldBe(["f4", "f3", "f2", "f1", "f0"]);
        raycaster.FirstHitOnly = true;
        var first = raycaster.Intersect(scene);
        first.Count.ShouldBe(1);
        first[0].Object.Name.ShouldBe("f4");
        raycaster.IntersectFirst(scene, out var hit).ShouldBeTrue();
        hit.Distance.ShouldBe(6f, 1e-4f);
    }

    [Fact]
    public void Near_And_Far_Limit_Hits()
    {
        var scene = new Scene();
        scene.Add(Floor());
        var raycaster = Down(0.3f, 0.1f);

        raycaster.Far = 9.5f;
        raycaster.Intersect(scene).ShouldBeEmpty();
        raycaster.Far = 10.5f;
        raycaster.Near = 10.2f;
        raycaster.Intersect(scene).ShouldBeEmpty();
        raycaster.Near = 1f;
        raycaster.Intersect(scene).Count.ShouldBe(1);
    }

    [Fact]
    public void Layers_And_Invisible_Parents_And_Materials_Are_Skipped()
    {
        var scene = new Scene();
        var group = new Group();
        var floor = Floor();
        group.Add(floor);
        scene.Add(group);
        var raycaster = Down(0.3f, 0.1f);

        raycaster.Intersect(scene).Count.ShouldBe(1);
        group.Visible = false;
        raycaster.Intersect(scene).ShouldBeEmpty();
        raycaster.Intersect(floor).ShouldBeEmpty();
        group.Visible = true;
        floor.Layers = new Layers(1u << 3);
        raycaster.Intersect(scene).ShouldBeEmpty();
        var layers = default(Layers);
        layers.Enable(3);
        raycaster.Layers = layers;
        raycaster.Intersect(scene).Count.ShouldBe(1);
        floor.Material.Visible = false;
        raycaster.Intersect(scene).ShouldBeEmpty();
    }

    [Fact]
    public void Scene_Broad_Phase_And_Graph_Walk_Agree()
    {
        var scene = new Scene();
        var group = new Group { Position = new Vector3(0.2f, 0f, 0f) };
        scene.Add(group);
        var random = new Random(7);
        for (var i = 0; i < 60; i++)
        {
            var box = new Mesh(new BoxGeometry(0.6f, 0.6f, 0.6f), new BasicMaterial())
            {
                Position = new Vector3((random.NextSingle() * 6f) - 3f, (random.NextSingle() * 6f) - 3f, (random.NextSingle() * 6f) - 3f),
            };
            box.RotateY(random.NextSingle());
            group.Add(box);
        }

        for (var n = 0; n < 40; n++)
        {
            var origin = new Vector3((random.NextSingle() * 8f) - 4f, 8f, (random.NextSingle() * 8f) - 4f);
            var raycaster = new Raycaster(new Ray(origin, new Vector3((random.NextSingle() * 0.4f) - 0.2f, -1f, (random.NextSingle() * 0.4f) - 0.2f)));
            var viaScene = raycaster.Intersect(scene);
            var viaWalk = raycaster.Intersect(group);

            viaWalk.Count.ShouldBe(viaScene.Count);
            for (var i = 0; i < viaScene.Count; i++)
            {
                viaWalk[i].Distance.ShouldBe(viaScene[i].Distance, 1e-4f);
            }
        }
    }

    [Fact]
    public void Non_Recursive_Tests_Only_The_Object_Itself()
    {
        var parent = Floor();
        // In the parent's space the plane already faces "up" (local +Z); one unit along it is one unit of world height.
        var child = new Mesh(new PlaneGeometry(4f, 4f), new BasicMaterial()) { Position = new Vector3(0f, 0f, 1f) };
        parent.Add(child);
        var raycaster = Down(0.3f, 0.1f);

        raycaster.Intersect(parent, recursive: false).Count.ShouldBe(1);
        raycaster.Intersect(parent).Count.ShouldBe(2);
    }

    [Fact]
    public void Groups_Select_Materials_And_Report_The_Material_Index()
    {
        var materials = new Material[6];
        for (var i = 0; i < 6; i++)
        {
            materials[i] = new BasicMaterial();
        }

        var box = new Mesh(new BoxGeometry(2f, 2f, 2f), materials[0]) { Materials = materials };
        var raycaster = new Raycaster(new Ray(new Vector3(0.1f, 5f, 0.2f), -Vector3.UnitY));

        // +Y is the third side of BoxGeometry.
        var hits = raycaster.Intersect(box);
        hits.Count.ShouldBe(1);
        hits[0].MaterialIndex.ShouldBe(2);
        Vector3.Distance(hits[0].Normal, Vector3.UnitY).ShouldBeLessThan(1e-4f);

        materials[2].Visible = false;
        raycaster.Intersect(box).ShouldBeEmpty();
        materials[3].Side = Side.Double;
        var through = raycaster.Intersect(box);
        through.Count.ShouldBe(1);
        through[0].MaterialIndex.ShouldBe(3);
        through[0].Distance.ShouldBe(6f, 1e-4f);
    }

    [Fact]
    public void Draw_Range_Limits_The_Tested_Triangles()
    {
        var floor = Floor();
        var geometry = floor.Geometry;
        var raycaster = Down(0f, 0f) ;
        var all = 0;
        for (var z = -1.5f; z < 2f; z += 1f)
        {
            for (var x = -1.5f; x < 2f; x += 1f)
            {
                raycaster.Ray = new Ray(new Vector3(x + 0.1f, 5f, z), -Vector3.UnitY);
                all += raycaster.Intersect(floor).Count;
            }
        }

        geometry.DrawStart = 3;
        geometry.DrawCount = 3;
        var limited = 0;
        for (var z = -1.5f; z < 2f; z += 1f)
        {
            for (var x = -1.5f; x < 2f; x += 1f)
            {
                raycaster.Ray = new Ray(new Vector3(x + 0.1f, 5f, z), -Vector3.UnitY);
                var hits = raycaster.Intersect(floor);
                limited += hits.Count;
                foreach (var hit in hits)
                {
                    hit.FaceIndex.ShouldBe(1);
                }
            }
        }

        all.ShouldBe(16);
        limited.ShouldBeInRange(1, 15);
    }

    [Fact]
    public void Non_Indexed_And_Quantized_Geometry_Is_Supported()
    {
        // One triangle in the XZ plane facing +Y, positions as normalized unsigned shorts (0..1), no index.
        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.UShort([0, 0, 0, 0, 0, 65535, 65535, 0, 0], 3, normalized: true));
        var mesh = new Mesh(geometry, new BasicMaterial()) { Scale = new Vector3(4f), Position = new Vector3(-2f, 0f, -2f) };

        var hits = Down(-1f, -1f).Intersect(mesh);

        hits.Count.ShouldBe(1);
        hits[0].Distance.ShouldBe(10f, 1e-4f);
        hits[0].FaceIndex.ShouldBe(0);
        Down(1.5f, 1.5f).Intersect(mesh).ShouldBeEmpty();
    }

    [Fact]
    public void Transformed_And_Mirrored_Objects_Are_Hit_From_Outside_With_Outward_Normals()
    {
        var scene = new Scene();
        var box = new Mesh(new BoxGeometry(1f, 1f, 1f), new BasicMaterial())
        {
            Position = new Vector3(3f, 0f, 0f),
            Scale = new Vector3(-2f, 1f, 0.5f),
        };
        box.RotateY(0.4f);
        scene.Add(box);
        var raycaster = new Raycaster(new Ray(new Vector3(3f, 0.1f, 10f), -Vector3.UnitZ));

        var hits = raycaster.Intersect(scene);

        hits.Count.ShouldBe(1);
        // The outward normal of the face that was hit points back toward the ray origin.
        Vector3.Dot(hits[0].Normal, raycaster.Ray.Direction).ShouldBeLessThan(0f);
        hits[0].Normal.Length().ShouldBe(1f, 1e-4f);
        Vector3.Distance(hits[0].Point, raycaster.Ray.At(hits[0].Distance)).ShouldBeLessThan(1e-3f);
        // The point is on the box's surface: back in local space one coordinate is ±0.5.
        var local = box.WorldToLocal(hits[0].Point);
        MathF.Max(MathF.Abs(local.X), MathF.Max(MathF.Abs(local.Y), MathF.Abs(local.Z))).ShouldBe(0.5f, 1e-3f);
    }

    [Fact]
    public void Instanced_Mesh_Reports_The_Instance()
    {
        var scene = new Scene();
        var instanced = new InstancedMesh(new BoxGeometry(1f, 1f, 1f), new BasicMaterial(), 10) { Position = new Vector3(0f, 0f, 1f) };
        for (var i = 0; i < 10; i++)
        {
            instanced.SetMatrix(i, Matrix4x4.CreateTranslation(i * 2f, 0f, 0f));
        }

        instanced.Count = 8;
        scene.Add(instanced);

        var hits = Down(6.2f, 1.1f).Intersect(scene);
        hits.Count.ShouldBe(1);
        hits[0].InstanceId.ShouldBe(3);
        hits[0].Distance.ShouldBe(9.5f, 1e-4f);
        // Instance 9 exists in the array but is beyond Count.
        Down(18.2f, 1.1f).Intersect(scene).ShouldBeEmpty();
        Down(1f, 1.1f).Intersect(scene).ShouldBeEmpty();
    }

    [Fact]
    public void Skinned_Mesh_Is_Tested_In_Its_Current_Pose()
    {
        // A strip along +Y made of two quads; the upper half follows the second bone.
        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float(
            [-0.5f, 0f, 0f, 0.5f, 0f, 0f, -0.5f, 1f, 0f, 0.5f, 1f, 0f, -0.5f, 2f, 0f, 0.5f, 2f, 0f], 3));
        geometry.SetAttribute(BufferGeometry.SkinIndex, VertexAttribute.UShort([0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], 4, false));
        geometry.SetAttribute(BufferGeometry.SkinWeight, VertexAttribute.Float(
            [1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f], 4));
        geometry.SetIndex(new ushort[] { 0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4 });
        var scene = new Scene();
        var root = new Bone();
        var tip = new Bone { Position = new Vector3(0f, 1f, 0f) };
        root.Add(tip);
        scene.Add(root);
        var mesh = new SkinnedMesh(geometry, new BasicMaterial { Side = Side.Double }) { Skeleton = new Skeleton([root, tip]) };
        scene.Add(mesh);
        var probe = new Raycaster(new Ray(new Vector3(0f, 1.7f, 5f), -Vector3.UnitZ));

        probe.Intersect(scene).Count.ShouldBe(1);

        // Bend the upper bone 90 degrees toward +Z: the upper quad now lies in the plane y = 1.
        tip.RotateX(MathF.PI / 2f);
        probe.Intersect(scene).ShouldBeEmpty();
        var fromAbove = new Raycaster(new Ray(new Vector3(0f, 5f, 0.6f), -Vector3.UnitY));
        var hits = fromAbove.Intersect(scene);
        hits.Count.ShouldBe(1);
        hits[0].Distance.ShouldBe(4f, 1e-3f);
        hits[0].FaceIndex.ShouldBeInRange(2, 3);

        // As in glTF, the mesh node's own transform does not move skinned vertices.
        mesh.Position = new Vector3(50f, 0f, 0f);
        fromAbove.Intersect(scene).Count.ShouldBe(1);
    }

    [Fact]
    public void Morph_Targets_Move_The_Tested_Surface()
    {
        var floor = Floor();
        var geometry = floor.Geometry;
        var deltas = new float[geometry.VertexCount * 3];
        for (var i = 0; i < geometry.VertexCount; i++)
        {
            // Local +Z is world +Y for the rotated plane.
            deltas[(i * 3) + 2] = 2f;
        }

        geometry.AddMorphTarget(new MorphTarget(deltas));
        var scene = new Scene();
        scene.Add(floor);
        var raycaster = Down(0.4f, 0.4f);

        raycaster.Intersect(scene)[0].Distance.ShouldBe(10f, 1e-4f);
        floor.MorphWeights = [0.5f];
        raycaster.Intersect(scene)[0].Distance.ShouldBe(9f, 1e-4f);
    }

    [Fact]
    public void Lines_Are_Hit_Within_The_Threshold()
    {
        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 4f, 0f, 0f, 4f, 0f, 4f], 3));
        var scene = new Scene();
        var line = new Line(geometry, new LineMaterial());
        scene.Add(line);
        var raycaster = Down(2f, 0.05f);
        raycaster.LineThreshold = 0.1f;

        var hits = raycaster.Intersect(scene);
        hits.Count.ShouldBe(1);
        hits[0].Index.ShouldBe(0);
        hits[0].Distance.ShouldBe(10f, 1e-4f);
        hits[0].DistanceToRay.ShouldBe(0.05f, 1e-4f);
        Vector3.Distance(hits[0].Point, new Vector3(2f, 0f, 0f)).ShouldBeLessThan(1e-4f);
        raycaster.LineThreshold = 0.01f;
        raycaster.Intersect(scene).ShouldBeEmpty();

        // LineSegments joins vertices in pairs: (0,1) only, the third vertex has no partner.
        scene.Remove(line);
        scene.Add(new LineSegments(geometry, new LineMaterial()));
        raycaster.LineThreshold = 0.1f;
        raycaster.Ray = new Ray(new Vector3(4f, 10f, 2f), -Vector3.UnitY);
        raycaster.Intersect(scene).ShouldBeEmpty();
        raycaster.Ray = new Ray(new Vector3(1f, 10f, 0f), -Vector3.UnitY);
        raycaster.Intersect(scene).Count.ShouldBe(1);
    }

    [Fact]
    public void Points_Are_Hit_Within_The_Threshold()
    {
        var geometry = new BufferGeometry();
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 1f, 0f, 0f, 2f, 0f, 0f], 3));
        var points = new Points(geometry, new PointsMaterial()) { Position = new Vector3(0f, 2f, 0f) };
        var scene = new Scene();
        scene.Add(points);
        var raycaster = Down(1.02f, 0f);
        raycaster.PointThreshold = 0.05f;

        var hits = raycaster.Intersect(scene);

        hits.Count.ShouldBe(1);
        hits[0].Index.ShouldBe(1);
        hits[0].Distance.ShouldBe(8f, 1e-4f);
        hits[0].DistanceToRay.ShouldBe(0.02f, 1e-4f);
        raycaster.PointThreshold = 1.5f;
        raycaster.Intersect(scene).Count.ShouldBe(3);
    }

    [Fact]
    public void Sprites_Face_The_Camera_And_Need_One()
    {
        var scene = new Scene();
        var sprite = new Sprite(new SpriteMaterial()) { Position = new Vector3(0f, 0f, 0f), Scale = new Vector3(2f, 1f, 1f) };
        scene.Add(sprite);
        var camera = new PerspectiveCamera(50f, 1f, 0.1f, 100f) { Position = new Vector3(6f, 0f, 6f) };
        camera.LookAt(Vector3.Zero);
        var raycaster = new Raycaster();

        raycaster.Ray = new Ray(camera.Position, -camera.Position);
        raycaster.Intersect(scene).ShouldBeEmpty();

        raycaster.SetFromCamera(Vector2.Zero, camera);
        var hits = raycaster.Intersect(scene);
        hits.Count.ShouldBe(1);
        hits[0].Distance.ShouldBe(camera.Position.Length(), 1e-3f);
        hits[0].Uv.X.ShouldBe(0.5f, 1e-3f);
        hits[0].Uv.Y.ShouldBe(0.5f, 1e-3f);

        // 0.9 units to the camera's right is inside the 2-wide quad, 0.7 units up is outside the 1-high quad.
        var right = Vector3.Normalize(new Vector3(camera.WorldMatrix.M11, camera.WorldMatrix.M12, camera.WorldMatrix.M13));
        raycaster.Ray = new Ray(camera.Position, (right * 0.9f) - camera.Position);
        var side = raycaster.Intersect(scene);
        side.Count.ShouldBe(1);
        side[0].Uv.X.ShouldBe(0.95f, 1e-2f);
        raycaster.Ray = new Ray(camera.Position, new Vector3(0f, 0.7f, 0f) - camera.Position);
        raycaster.Intersect(scene).ShouldBeEmpty();
    }

    [Fact]
    public void SetFromCamera_Perspective_Starts_At_The_Camera_And_Passes_Through_The_Pixel()
    {
        var camera = new PerspectiveCamera(60f, 2f, 0.1f, 100f) { Position = new Vector3(1f, 2f, 5f) };
        camera.LookAt(new Vector3(0f, 0f, 0f));
        var raycaster = new Raycaster();

        raycaster.SetFromCamera(new Vector2(0.5f, -0.25f), camera);

        Vector3.Distance(raycaster.Ray.Origin, camera.Position).ShouldBeLessThan(1e-5f);
        var ndc = camera.Project(raycaster.Ray.At(7f));
        ndc.X.ShouldBe(0.5f, 1e-3f);
        ndc.Y.ShouldBe(-0.25f, 1e-3f);
        raycaster.Camera.ShouldBeSameAs(camera);

        raycaster.SetFromCamera(600f, 100f, 800f, 400f, camera);
        ndc = camera.Project(raycaster.Ray.At(3f));
        ndc.X.ShouldBe(0.5f, 1e-3f);
        ndc.Y.ShouldBe(0.5f, 1e-3f);
    }

    [Fact]
    public void SetFromCamera_Orthographic_Casts_Parallel_Rays()
    {
        var camera = new OrthographicCamera(-4f, 4f, 3f, -3f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 10f) };
        var raycaster = new Raycaster();

        raycaster.SetFromCamera(new Vector2(0.5f, -1f), camera);

        Vector3.Distance(raycaster.Ray.Direction, -Vector3.UnitZ).ShouldBeLessThan(1e-5f);
        raycaster.Ray.Origin.X.ShouldBe(2f, 1e-4f);
        raycaster.Ray.Origin.Y.ShouldBe(-3f, 1e-4f);
        raycaster.Ray.Origin.Z.ShouldBe(9.9f, 1e-3f);
    }

    [Fact]
    public void Triangle_Strips_Are_Tested_With_Alternating_Winding()
    {
        // Four vertices: a quad in the XZ plane facing +Y as a strip.
        var geometry = new BufferGeometry { Topology = Rhi.PrimitiveTopology.TriangleStrip };
        geometry.SetAttribute(BufferGeometry.Position, VertexAttribute.Float([0f, 0f, 0f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 0f, 1f], 3));
        var mesh = new Mesh(geometry, new BasicMaterial());

        Down(0.2f, 0.2f).Intersect(mesh).Count.ShouldBe(1);
        Down(0.8f, 0.8f).Intersect(mesh).Count.ShouldBe(1);
        new Raycaster(new Ray(new Vector3(0.8f, -5f, 0.8f), Vector3.UnitY)).Intersect(mesh).ShouldBeEmpty();
    }

    [Fact]
    public void Reused_List_Does_Not_Allocate_After_Warm_Up()
    {
        var scene = new Scene();
        var sphere = new Mesh(new SphereGeometry(1f, 64, 32), new BasicMaterial { Side = Side.Double });
        var box = new Mesh(new BoxGeometry(1f, 1f, 1f), new BasicMaterial()) { Position = new Vector3(0f, -3f, 0f) };
        scene.Add(sphere, box);
        var raycaster = Down(0.1f, 0.2f);
        var hits = new List<RayHit>(16);
        for (var i = 0; i < 50; i++)
        {
            raycaster.Intersect(scene, hits);
        }

        hits.Count.ShouldBe(3);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            raycaster.Ray = new Ray(new Vector3(0.1f + (i * 0.001f), 10f, 0.2f), -Vector3.UnitY);
            raycaster.Intersect(scene, hits);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
        hits.Count.ShouldBe(3);
    }

    [Fact]
    public void Ray_Segment_Distance_Matches_Hand_Computed_Cases()
    {
        var ray = new Ray(Vector3.Zero, Vector3.UnitX);

        // A segment crossing above the ray.
        Raycaster.RaySegmentDistanceSquared(ray, new Vector3(3f, 2f, -1f), new Vector3(3f, 2f, 1f), out var t, out var point).ShouldBe(4f, 1e-5f);
        t.ShouldBe(3f, 1e-5f);
        Vector3.Distance(point, new Vector3(3f, 2f, 0f)).ShouldBeLessThan(1e-5f);

        // Entirely behind the origin: measured from the origin.
        Raycaster.RaySegmentDistanceSquared(ray, new Vector3(-3f, 0f, 0f), new Vector3(-1f, 0f, 0f), out t, out point).ShouldBe(1f, 1e-5f);
        t.ShouldBe(0f);
        Vector3.Distance(point, new Vector3(-1f, 0f, 0f)).ShouldBeLessThan(1e-5f);

        // Parallel.
        Raycaster.RaySegmentDistanceSquared(ray, new Vector3(2f, 1f, 0f), new Vector3(5f, 1f, 0f), out _, out _).ShouldBe(1f, 1e-5f);
    }
}
