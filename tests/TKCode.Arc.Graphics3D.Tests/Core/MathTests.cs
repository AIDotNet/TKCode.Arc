using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests;

public class MathTests
{
    [Fact]
    public void Color_Converts_Between_Srgb_And_Linear()
    {
        var c = ColorF.FromHex(0xFF8000);
        c.R.ShouldBe(1f, 1e-6f);
        c.G.ShouldBe(0.2158605f, 1e-4f);
        c.B.ShouldBe(0f, 1e-6f);
        var back = c.ToSrgb();
        back.X.ShouldBe(1f, 1e-5f);
        back.Y.ShouldBe(128f / 255f, 1e-4f);
        var red = ColorF.FromHsl(0f, 1f, 0.5f);
        red.R.ShouldBe(1f, 1e-5f);
        red.G.ShouldBe(0f, 1e-5f);
        red.B.ShouldBe(0f, 1e-5f);
        ColorF.FromHsl(1f / 3f, 1f, 0.5f).G.ShouldBe(1f, 1e-5f);
    }

    [Theory]
    [InlineData(0.3f, -0.7f, 1.1f)]
    [InlineData(-1.2f, 0.4f, 2.9f)]
    [InlineData(0f, 0f, 0f)]
    public void Euler_Angles_Round_Trip(float x, float y, float z)
    {
        var q = MathUtil.QuaternionFromEuler(new Vector3(x, y, z));
        var e = MathUtil.EulerFromQuaternion(q);
        var q2 = MathUtil.QuaternionFromEuler(e);
        MathF.Abs(Quaternion.Dot(q, q2)).ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Euler_Order_Is_Intrinsic_Xyz()
    {
        // Rotate about X by 90 degrees, then about the new Y by 90 degrees: local +Z ends up on world +X... followed
        // through by hand: Rx maps Z to -Y; the composed rotation applies Ry first to the vector.
        var q = MathUtil.QuaternionFromEuler(new Vector3(MathF.PI / 2f, MathF.PI / 2f, 0f));
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f);
        MathF.Abs(Quaternion.Dot(q, expected)).ShouldBe(1f, 1e-5f);
    }

    [Fact]
    public void Compose_Matches_Scale_Rotate_Translate()
    {
        var p = new Vector3(1f, 2f, 3f);
        var r = Quaternion.CreateFromYawPitchRoll(0.4f, -0.2f, 0.9f);
        var s = new Vector3(2f, 0.5f, 3f);
        var m = MathUtil.Compose(p, r, s);
        var expected = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(p);
        for (var i = 0; i < 16; i++)
        {
            m[i / 4, i % 4].ShouldBe(expected[i / 4, i % 4], 1e-5f);
        }

        MathUtil.MaxScale(m).ShouldBe(3f, 1e-5f);
    }

    [Fact]
    public void Bounding_Box_Transforms_And_Tests()
    {
        var box = new BoundingBox(new Vector3(-1f), new Vector3(1f));
        var moved = box.Transform(Matrix4x4.CreateRotationY(MathF.PI / 4f) * Matrix4x4.CreateTranslation(5f, 0f, 0f));
        moved.Min.X.ShouldBe(5f - MathF.Sqrt(2f), 1e-5f);
        moved.Max.X.ShouldBe(5f + MathF.Sqrt(2f), 1e-5f);
        moved.Max.Y.ShouldBe(1f, 1e-5f);
        BoundingBox.Empty.IsEmpty.ShouldBeTrue();
        box.Contains(new Vector3(0.5f, -1f, 1f)).ShouldBeTrue();
        box.Intersects(new BoundingBox(new Vector3(1f), new Vector3(2f))).ShouldBeTrue();
        box.Intersects(new BoundingBox(new Vector3(1.01f), new Vector3(2f))).ShouldBeFalse();
        box.SurfaceArea.ShouldBe(24f);
        BoundingSphere.FromBox(box).Radius.ShouldBe(MathF.Sqrt(3f), 1e-5f);
    }

    [Fact]
    public void Ray_Intersections()
    {
        var ray = new Ray(new Vector3(0f, 0f, 5f), new Vector3(0f, 0f, -2f));
        ray.Direction.ShouldBe(-Vector3.UnitZ);
        ray.Intersect(new BoundingSphere(Vector3.Zero, 1f)).ShouldBe(4f);
        ray.Intersect(new BoundingSphere(new Vector3(3f, 0f, 0f), 1f)).ShouldBeNull();
        ray.Intersect(new BoundingBox(new Vector3(-1f), new Vector3(1f))).ShouldBe(4f);
        new Ray(Vector3.Zero, Vector3.UnitX).Intersect(new BoundingBox(new Vector3(-1f), new Vector3(1f))).ShouldBe(0f);
        ray.IntersectTriangle(new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(0f, 1f, 0f), true, out var t, out var u, out var v).ShouldBeTrue();
        t.ShouldBe(5f, 1e-5f);
        (u + v).ShouldBeLessThanOrEqualTo(1f);

        // The same triangle seen from behind is culled when back faces are.
        var back = new Ray(new Vector3(0f, 0f, -5f), Vector3.UnitZ);
        back.IntersectTriangle(new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(0f, 1f, 0f), true, out _, out _, out _).ShouldBeFalse();
        back.IntersectTriangle(new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(0f, 1f, 0f), false, out _, out _, out _).ShouldBeTrue();
        ray.Intersect(new Plane(Vector3.UnitZ, 0f)).ShouldBe(5f);
    }

    [Fact]
    public void Frustum_Culls_What_The_Camera_Cannot_See()
    {
        var camera = new PerspectiveCamera(60f, 1f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 5f) };
        var frustum = camera.GetFrustum();
        frustum.Intersects(new BoundingSphere(Vector3.Zero, 1f)).ShouldBeTrue();
        frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, 10f), 1f)).ShouldBeFalse();
        frustum.Intersects(new BoundingSphere(new Vector3(0f, 0f, -200f), 1f)).ShouldBeFalse();
        frustum.Intersects(new BoundingSphere(new Vector3(50f, 0f, 0f), 1f)).ShouldBeFalse();
        frustum.Intersects(new BoundingSphere(new Vector3(3.5f, 0f, 0f), 1f)).ShouldBeTrue();
        frustum.Intersects(new BoundingBox(new Vector3(-1f), new Vector3(1f))).ShouldBeTrue();
        frustum.Intersects(new BoundingBox(new Vector3(40f, -1f, -1f), new Vector3(42f, 1f, 1f))).ShouldBeFalse();
    }

    [Fact]
    public void Camera_Projects_And_Unprojects()
    {
        var camera = new PerspectiveCamera(50f, 2f, 1f, 100f) { Position = new Vector3(1f, 2f, 10f) };
        camera.LookAt(new Vector3(1f, 2f, 0f));
        var ndc = camera.Project(new Vector3(1f, 2f, 0f));
        ndc.X.ShouldBe(0f, 1e-5f);
        ndc.Y.ShouldBe(0f, 1e-5f);
        ndc.Z.ShouldBeInRange(0f, 1f);
        camera.Project(new Vector3(1f, 2f, 9f)).Z.ShouldBe(0f, 1e-5f);
        var world = camera.Unproject(ndc);
        Vector3.Distance(world, new Vector3(1f, 2f, 0f)).ShouldBeLessThan(1e-3f);
        var ray = camera.ScreenPointToRay(400f, 200f, 800f, 400f);
        Vector3.Distance(ray.Direction, -Vector3.UnitZ).ShouldBeLessThan(1e-5f);

        var ortho = new OrthographicCamera(-4f, 4f, 2f, -2f, 0.1f, 50f) { Position = new Vector3(0f, 0f, 5f) };
        ortho.Project(new Vector3(4f, 2f, 0f)).X.ShouldBe(1f, 1e-5f);
        ortho.Zoom = 2f;
        ortho.Project(new Vector3(2f, 1f, 0f)).Y.ShouldBe(1f, 1e-5f);
        ortho.ScreenPointToRay(new Vector2(1f, 0f)).Origin.X.ShouldBe(2f, 1e-4f);
    }
}
