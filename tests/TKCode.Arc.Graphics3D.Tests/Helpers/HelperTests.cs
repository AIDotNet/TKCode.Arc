using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class HelperTests
{
    private static Vector3 Point(BufferGeometry g, int i) => g.GetAttribute(BufferGeometry.Position)!.GetVector3(i);

    [Fact]
    public void Grid_Helper_Lies_In_The_XZ_Plane()
    {
        var grid = new GridHelper(10f, 10, ColorF.FromHex(0xff0000), ColorF.FromHex(0x0000ff));
        grid.Geometry.VertexCount.ShouldBe(11 * 4);
        grid.Geometry.BoundingBox.Min.ShouldBeNear(new Vector3(-5f, 0f, -5f));
        grid.Geometry.BoundingBox.Max.ShouldBeNear(new Vector3(5f, 0f, 5f));
        grid.Topology.ShouldBe(Rhi.PrimitiveTopology.Lines);
        grid.Material.VertexColors.ShouldBeTrue();
        grid.CastShadow.ShouldBeFalse();

        // The lines through the center have the center color (red), the others the grid color (blue).
        var colors = grid.Geometry.GetAttribute(BufferGeometry.Color)!;
        colors.GetVector3(5 * 4).X.ShouldBe(1f);
        colors.GetVector3(0).Z.ShouldBe(1f);
        var polar = new PolarGridHelper(5f, 8, 4, 32);
        polar.Geometry.VertexCount.ShouldBe((8 * 2) + (4 * 32 * 2));
        polar.Geometry.BoundingSphere.Radius.ShouldBe(5f, 0.2f);
    }

    [Fact]
    public void Axes_Helper_Colors_The_Axes()
    {
        var axes = new AxesHelper(2f);
        axes.Geometry.VertexCount.ShouldBe(6);
        Point(axes.Geometry, 1).ShouldBe(new Vector3(2f, 0f, 0f));
        Point(axes.Geometry, 3).ShouldBe(new Vector3(0f, 2f, 0f));
        Point(axes.Geometry, 5).ShouldBe(new Vector3(0f, 0f, 2f));
        var colors = axes.Geometry.GetAttribute(BufferGeometry.Color)!;
        colors.GetVector3(0).ShouldBe(new Vector3(1f, 0f, 0f));
        colors.GetVector3(2).ShouldBe(new Vector3(0f, 1f, 0f));
        colors.GetVector3(4).ShouldBe(new Vector3(0f, 0f, 1f));
        var version = colors.Version;
        axes.SetColors(ColorF.White, ColorF.White, ColorF.Black);
        colors.GetVector3(0).ShouldBe(Vector3.One);
        colors.GetVector3(5).ShouldBe(Vector3.Zero);
        colors.Version.ShouldBeGreaterThan(version);
    }

    [Fact]
    public void Arrow_Helper_Points_Along_Its_Direction()
    {
        foreach (var direction in new[] { Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, Vector3.Normalize(new Vector3(1f, -2f, 3f)) })
        {
            var arrow = new ArrowHelper(direction, new Vector3(1f, 2f, 3f), 2f);

            // The tip of the head (local y = length) is at origin + direction * length.
            arrow.Cone.WorldPosition.ShouldBeNear(new Vector3(1f, 2f, 3f) + (direction * 2f), 1e-4f);
            arrow.Line.Scale.Y.ShouldBe(1.6f, 1e-5f);
            arrow.Cone.Scale.ShouldBeNear(new Vector3(0.08f, 0.4f, 0.08f));
        }

        var a = new ArrowHelper();
        a.Cone.WorldPosition.ShouldBeNear(Vector3.UnitZ);
        a.SetLength(4f, 1f, 0.5f);
        a.Cone.Scale.ShouldBeNear(new Vector3(0.5f, 1f, 0.5f));
        a.SetColor(ColorF.FromHex(0xff0000));
        ((BasicMaterial)a.Cone.Material).Color.ShouldBe(ColorF.FromHex(0xff0000));
        ((LineMaterial)a.Line.Material).Color.ShouldBe(ColorF.FromHex(0xff0000));
    }

    [Fact]
    public void Box_Helper_Follows_Its_Object()
    {
        var scene = new Scene();
        var group = new Group { Position = new Vector3(10f, 0f, 0f) };
        var box = new Mesh(new BoxGeometry(2f, 2f, 2f), new BasicMaterial());
        var sphere = new Mesh(new SphereGeometry(1f), new BasicMaterial()) { Position = new Vector3(0f, 5f, 0f) };
        group.Add(box, sphere);
        scene.Add(group);
        var helper = new BoxHelper(group);
        helper.Box.Min.ShouldBeNear(new Vector3(9f, -1f, -1f), 1e-3f);
        helper.Box.Max.ShouldBeNear(new Vector3(11f, 6f, 1f), 1e-3f);
        helper.Geometry.IndexCount.ShouldBe(24);
        group.Position = new Vector3(0f, 0f, -3f);
        helper.Update();
        helper.Geometry.BoundingBox.Min.ShouldBeNear(new Vector3(-1f, -1f, -4f), 1e-3f);
        helper.Geometry.BoundingBox.Max.ShouldBeNear(new Vector3(1f, 6f, -2f), 1e-3f);
        var fixedBox = new Box3Helper(new BoundingBox(new Vector3(-1f), new Vector3(2f)));
        fixedBox.Geometry.BoundingBox.Max.ShouldBeNear(new Vector3(2f));
        fixedBox.Box = new BoundingBox(Vector3.Zero, Vector3.One);
        fixedBox.Geometry.BoundingBox.Min.ShouldBeNear(Vector3.Zero);
        new BoxHelper(new Group()).Box.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Plane_Helper_Sits_On_Its_Plane()
    {
        var plane = new Plane(Vector3.Normalize(new Vector3(1f, 1f, 0f)), -2f);
        var helper = new PlaneHelper(plane, 4f);
        Plane.DotCoordinate(plane, helper.Position).ShouldBe(0f, 1e-4f);
        helper.WorldDirection.ShouldBeNear(plane.Normal, 1e-4f);
        helper.Scale.ShouldBeNear(new Vector3(2f, 2f, 1f));

        // Every corner of the square is on the plane.
        foreach (var corner in new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, 1f, 0f) })
        {
            Plane.DotCoordinate(plane, helper.LocalToWorld(corner)).ShouldBe(0f, 1e-3f);
        }
    }

    [Fact]
    public void Light_Helpers_Follow_Their_Lights()
    {
        var sun = new DirectionalLight(ColorF.FromHex(0xff8800), 2f) { Position = new Vector3(0f, 10f, 0f), Target = new Vector3(3f, 6f, 0f) };
        var sunHelper = new DirectionalLightHelper(sun, 2f);
        sunHelper.Position.ShouldBe(sun.Position);
        sunHelper.WorldDirection.ShouldBeNear(sun.Direction, 1e-4f);
        sunHelper.Children[1].Scale.Z.ShouldBe(5f, 1e-4f);
        ((LineMaterial)((Line)sunHelper.Children[0]).Material).Color.ShouldBe(sun.Color);
        sun.Target = new Vector3(0f, 0f, 0f);
        sunHelper.Color = ColorF.White;
        sunHelper.Update();
        sunHelper.WorldDirection.ShouldBeNear(-Vector3.UnitY, 1e-3f);
        ((LineMaterial)((Line)sunHelper.Children[0]).Material).Color.ShouldBe(ColorF.White);

        var point = new PointLight(ColorF.FromHex(0x00ff00)) { Position = new Vector3(1f, 2f, 3f) };
        var pointHelper = new PointLightHelper(point, 0.5f);
        pointHelper.Position.ShouldBe(point.Position);
        pointHelper.Geometry.BoundingSphere.Radius.ShouldBe(0.5f, 1e-3f);
        ((LineMaterial)pointHelper.Material).Color.ShouldBe(point.Color);

        var spot = new SpotLight(ColorF.White, 1f, 10f, MathF.PI / 4f) { Position = new Vector3(0f, 5f, 0f), Target = new Vector3(0f, 0f, 5f) };
        var spotHelper = new SpotLightHelper(spot);
        spotHelper.WorldDirection.ShouldBeNear(spot.Direction, 1e-4f);
        spotHelper.Children[0].Scale.ShouldBeNear(new Vector3(10f, 10f, 10f), 1e-3f);
        spot.Distance = 0f;
        spotHelper.Update();
        spotHelper.Children[0].Scale.Z.ShouldBe(1000f);

        var hemisphere = new HemisphereLight(ColorF.FromHex(0x0000ff), ColorF.FromHex(0x00ff00)) { Position = new Vector3(0f, 0f, 4f) };
        var hemisphereHelper = new HemisphereLightHelper(hemisphere, 1f);

        // The sky half (local +Y) points away from the origin, toward the light's "up".
        Vector3.Transform(Vector3.UnitY, hemisphereHelper.Rotation).ShouldBeNear(Vector3.UnitZ, 1e-4f);
        var positions = hemisphereHelper.Geometry.GetAttribute(BufferGeometry.Position)!;
        var colors = hemisphereHelper.Geometry.GetAttribute(BufferGeometry.Color)!;
        for (var i = 0; i < positions.Count; i++)
        {
            var y = positions.GetVector3(i).Y;
            if (y > 0.5f)
            {
                colors.GetVector3(i).ShouldBe(new Vector3(0f, 0f, 1f));
            }
            else if (y < -0.5f)
            {
                colors.GetVector3(i).ShouldBe(new Vector3(0f, 1f, 0f));
            }
        }
    }

    [Fact]
    public void Camera_Helper_Outlines_The_View_Volume()
    {
        foreach (var camera in new Camera[] { new PerspectiveCamera(60f, 2f, 1f, 20f), new OrthographicCamera(-3f, 3f, 2f, -2f, 1f, 20f) })
        {
            camera.Position = new Vector3(1f, 2f, 3f);
            camera.LookAt(new Vector3(0f, 0f, -5f));
            var helper = new CameraHelper(camera);
            helper.Geometry.VertexCount.ShouldBe(50);
            helper.FrustumCulled.ShouldBeFalse();

            // The first four segments are the near rectangle: its corners project onto the corners of the view.
            foreach (var (index, x, y) in new[] { (0, -1f, -1f), (1, 1f, -1f), (3, 1f, 1f), (5, -1f, 1f) })
            {
                var ndc = camera.Project(Point(helper.Geometry, index));
                ndc.X.ShouldBe(x, 1e-3f);
                ndc.Y.ShouldBe(y, 1e-3f);
                ndc.Z.ShouldBe(0f, 1e-3f);
            }

            // The far rectangle is at the far distance.
            var far = Point(helper.Geometry, 8);
            Vector3.Dot(far - camera.WorldPosition, Vector3.Normalize(new Vector3(0f, 0f, -5f) - camera.WorldPosition)).ShouldBe(20f, 1e-2f);
            var before = Point(helper.Geometry, 0);
            camera.Position = new Vector3(5f, 5f, 5f);
            helper.Update();
            Point(helper.Geometry, 0).ShouldNotBe(before);
        }
    }

    [Fact]
    public void Skeleton_Helper_Draws_A_Line_Per_Joint()
    {
        var root = new Bone { Position = new Vector3(0f, 1f, 0f) };
        var spine = new Bone { Position = new Vector3(0f, 1f, 0f) };
        var head = new Bone { Position = new Vector3(0f, 0.5f, 0f) };
        var arm = new Bone { Position = new Vector3(1f, 0f, 0f) };
        root.Add(spine);
        spine.Add(head, arm);
        var model = new Group();
        model.Add(root);
        var helper = new SkeletonHelper(model);
        helper.Bones.Count.ShouldBe(4);

        // Three joints have a bone parent.
        helper.Geometry.VertexCount.ShouldBe(6);
        Point(helper.Geometry, 0).ShouldBeNear(new Vector3(0f, 1f, 0f));
        Point(helper.Geometry, 1).ShouldBeNear(new Vector3(0f, 2f, 0f));
        Point(helper.Geometry, 5).ShouldBeNear(new Vector3(1f, 2f, 0f));
        spine.Position = new Vector3(0f, 3f, 0f);
        helper.Update();
        Point(helper.Geometry, 1).ShouldBeNear(new Vector3(0f, 4f, 0f));
        helper.Material.DepthTest.ShouldBeFalse();
        var fromSkeleton = new SkeletonHelper(new Skeleton([root, spine, head, arm]));
        fromSkeleton.Geometry.VertexCount.ShouldBe(6);
        new SkeletonHelper(new Group()).Geometry.VertexCount.ShouldBe(0);
    }
}
