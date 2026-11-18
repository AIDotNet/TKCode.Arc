using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class SceneGraphTests
{
    [Fact]
    public void World_Matrices_Are_Always_Current()
    {
        var parent = new Group { Position = new Vector3(10f, 0f, 0f) };
        var child = new Group { Position = new Vector3(0f, 2f, 0f) };
        var grandchild = new Group { Position = new Vector3(0f, 0f, 3f) };
        parent.Add(child);
        child.Add(grandchild);
        grandchild.WorldPosition.ShouldBe(new Vector3(10f, 2f, 3f));

        parent.Position = new Vector3(-5f, 0f, 0f);
        grandchild.WorldPosition.ShouldBe(new Vector3(-5f, 2f, 3f));

        // Reading a deep node first must not leave a shallower one stale later.
        child.Position = new Vector3(0f, 7f, 0f);
        grandchild.WorldPosition.ShouldBe(new Vector3(-5f, 7f, 3f));
        parent.RotateY(MathF.PI / 2f);
        Vector3.Distance(grandchild.WorldPosition, new Vector3(-5f + 3f, 7f, 0f)).ShouldBeLessThan(1e-5f);
        Vector3.Distance(grandchild.WorldToLocal(grandchild.LocalToWorld(new Vector3(1f, 2f, 3f))), new Vector3(1f, 2f, 3f)).ShouldBeLessThan(1e-4f);
    }

    [Fact]
    public void Reparenting_Keeps_The_Tree_Consistent()
    {
        var a = new Group { Name = "a", Position = new Vector3(1f, 0f, 0f) };
        var b = new Group { Name = "b", Position = new Vector3(0f, 1f, 0f) };
        var leaf = new Group { Name = "leaf" };
        a.Add(leaf);
        leaf.WorldPosition.ShouldBe(new Vector3(1f, 0f, 0f));
        b.Add(leaf);
        a.Children.ShouldBeEmpty();
        leaf.Parent.ShouldBe(b);
        leaf.WorldPosition.ShouldBe(new Vector3(0f, 1f, 0f));
        Should.Throw<ArgumentException>(() => leaf.Add(b));
        Should.Throw<ArgumentException>(() => leaf.Add(leaf));
        b.Find("leaf").ShouldBe(leaf);
        b.Find<Group>().ShouldBe(b);
        leaf.RemoveFromParent();
        leaf.Parent.ShouldBeNull();
        leaf.WorldPosition.ShouldBe(Vector3.Zero);
    }

    [Fact]
    public void LookAt_Points_The_Right_Axis_At_The_Target()
    {
        var obj = new Group { Position = new Vector3(0f, 0f, 5f) };
        obj.LookAt(Vector3.Zero);
        Vector3.Distance(obj.WorldDirection, -Vector3.UnitZ).ShouldBeLessThan(1e-5f);

        var camera = new PerspectiveCamera { Position = new Vector3(0f, 0f, 5f) };
        camera.LookAt(Vector3.Zero);
        // A camera looks down its local -Z, so its +Z keeps pointing away from the target.
        Vector3.Distance(camera.WorldDirection, Vector3.UnitZ).ShouldBeLessThan(1e-5f);

        // Straight down, where "up" is parallel to the view direction: still a valid rotation, with the target in the
        // center up to the small nudge that breaks the tie (as in three.js).
        camera.Position = new Vector3(0f, 10f, 0f);
        camera.LookAt(Vector3.Zero);
        var center = camera.Project(Vector3.Zero);
        center.X.ShouldBe(0f, 1e-3f);
        center.Y.ShouldBe(0f, 1e-3f);
        float.IsNaN(camera.WorldMatrix.M11).ShouldBeFalse();
    }

    [Fact]
    public void LookAt_Works_Under_A_Rotated_Parent()
    {
        var parent = new Group();
        parent.RotateY(1.1f);
        parent.RotateX(-0.4f);
        var child = new Group { Position = new Vector3(2f, 1f, 0f) };
        parent.Add(child);
        var target = new Vector3(-3f, 4f, 7f);
        child.LookAt(target);
        var expected = Vector3.Normalize(target - child.WorldPosition);
        Vector3.Distance(child.WorldDirection, expected).ShouldBeLessThan(1e-4f);
    }

    [Fact]
    public void Scene_Tracks_Renderables_And_Lights()
    {
        var scene = new Scene();
        var group = new Group();
        var mesh = new Mesh(new BoxGeometry(), new BasicMaterial());
        var light = new PointLight(ColorF.White);
        group.Add(mesh, light);
        mesh.Scene.ShouldBeNull();
        scene.World.Count.ShouldBe(0);

        scene.Add(group);
        mesh.Scene.ShouldBe(scene);
        scene.World.Count.ShouldBe(1);
        scene.Lights.ShouldBe([light]);

        scene.Remove(group);
        mesh.Scene.ShouldBeNull();
        scene.World.Count.ShouldBe(0);
        scene.Lights.ShouldBeEmpty();
    }

    [Fact]
    public void Render_World_Follows_Transforms_Visibility_And_Bounds()
    {
        var scene = new Scene();
        var group = new Group();
        var mesh = new Mesh(new SphereGeometry(1f), new BasicMaterial()) { CastShadow = true };
        group.Add(mesh);
        scene.Add(group);
        var world = scene.World;
        world.Sync();
        var slot = mesh.ProxyIndex;
        world.Spheres[slot].Radius.ShouldBe(1f, 1e-3f);
        (world.Flags[slot] & ProxyFlags.Visible).ShouldBe(ProxyFlags.Visible);
        (world.Flags[slot] & ProxyFlags.CastShadow).ShouldBe(ProxyFlags.CastShadow);

        group.Position = new Vector3(4f, 0f, 0f);
        group.Scale = new Vector3(2f);
        world.Sync();
        world.Spheres[slot].Center.ShouldBe(new Vector3(4f, 0f, 0f));
        world.Spheres[slot].Radius.ShouldBe(2f, 1e-3f);

        // Hiding an ancestor hides the mesh.
        group.Visible = false;
        world.Sync();
        (world.Flags[slot] & ProxyFlags.Visible).ShouldBe(ProxyFlags.None);
        group.Visible = true;

        // A mirrored transform is flagged so that the renderer flips the winding.
        mesh.Scale = new Vector3(-1f, 1f, 1f);
        world.Sync();
        (world.Flags[slot] & ProxyFlags.Mirrored).ShouldBe(ProxyFlags.Mirrored);

        // New geometry bounds reach the slot.
        mesh.Scale = Vector3.One;
        mesh.Geometry = new SphereGeometry(3f);
        world.Sync();
        world.Spheres[slot].Radius.ShouldBe(6f, 1e-2f);
        var box = scene.ComputeBounds();
        box.Min.X.ShouldBe(4f - 6f, 0.05f);
    }

    [Fact]
    public void Scene_Reports_A_Change_Once_Until_Acknowledged()
    {
        var scene = new Scene();
        var count = 0;
        scene.Changed += () => count++;
        var mesh = new Mesh(new BoxGeometry(), new BasicMaterial());
        scene.Add(mesh);
        count.ShouldBe(1);
        mesh.Position = new Vector3(1f, 0f, 0f);
        mesh.Visible = false;
        count.ShouldBe(1);

        scene.World.Sync();
        scene.AcknowledgeChanges();
        mesh.Position = new Vector3(2f, 0f, 0f);
        count.ShouldBe(2);
        mesh.Position = new Vector3(3f, 0f, 0f);
        count.ShouldBe(2);

        scene.World.Sync();
        scene.AcknowledgeChanges();
        var light = new DirectionalLight();
        scene.Add(light);
        count.ShouldBe(3);
    }

    [Fact]
    public void Removed_Slots_Are_Reused()
    {
        var scene = new Scene();
        var geometry = new BoxGeometry();
        var material = new BasicMaterial();
        var meshes = Enumerable.Range(0, 100).Select(_ => new Mesh(geometry, material)).ToArray();
        scene.Add(meshes);
        scene.World.Sync();
        scene.World.HighWater.ShouldBe(100);
        for (var i = 0; i < 50; i++)
        {
            scene.Remove(meshes[i]);
        }

        scene.World.Count.ShouldBe(50);
        for (var i = 0; i < 50; i++)
        {
            scene.Add(new Mesh(geometry, material));
        }

        scene.World.Sync();
        scene.World.HighWater.ShouldBe(100);
        scene.World.Count.ShouldBe(100);
    }
}
