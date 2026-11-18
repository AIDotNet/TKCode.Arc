using System.Numerics;

namespace TKCode.Arc.Graphics3D.Tests.Controls;

public class OrbitControlsTests
{
    private static (PerspectiveCamera Camera, OrbitControls Controls) Create(float fov = 50f, float aspect = 4f / 3f)
    {
        var camera = new PerspectiveCamera(fov, aspect, 0.1f, 1000f) { Position = new Vector3(0f, 0f, 5f) };
        var controls = new OrbitControls(camera) { ViewportWidth = 800f, ViewportHeight = 600f };
        controls.Update(0f);
        return (camera, controls);
    }

    private static void ShouldBeNear(Vector3 actual, Vector3 expected, float tolerance = 1e-3f) =>
        Vector3.Distance(actual, expected).ShouldBeLessThan(tolerance, $"{actual} vs {expected}");

    [Fact]
    public void Dragging_Left_Button_Orbits_By_Two_Pi_Per_Viewport_Height()
    {
        var (camera, controls) = Create();

        controls.PointerDown(1, 100f, 300f, PointerButton.Left);
        controls.IsInteracting.ShouldBeTrue();
        controls.PointerMove(1, 250f, 300f);
        controls.Update(1f / 60f).ShouldBeTrue();

        // 150 px of 600 is a quarter turn; dragging right moves the camera to its left.
        ShouldBeNear(camera.Position, new Vector3(-5f, 0f, 0f));
        controls.AzimuthAngle.ShouldBe(-MathF.PI / 2f, 1e-4f);
        // The camera keeps looking at the target.
        ShouldBeNear(Vector3.Transform(-Vector3.UnitZ, camera.Rotation), Vector3.UnitX);

        controls.PointerMove(1, 250f, 200f);
        controls.Update(1f / 60f);
        // 100 px up: the polar angle shrinks by 2π / 6... dragging down raises the camera, dragging up lowers it.
        controls.PolarAngle.ShouldBe((MathF.PI / 2f) + (MathF.PI / 3f), 1e-4f);
        controls.Distance.ShouldBe(5f, 1e-4f);
        controls.PointerUp(1);
        controls.IsInteracting.ShouldBeFalse();
        controls.Update(1f / 60f).ShouldBeFalse();
    }

    [Fact]
    public void Wheel_Scales_The_Distance_By_095_Per_Notch()
    {
        var (camera, controls) = Create();

        controls.Wheel(-100f, 400f, 300f);
        controls.Update(1f / 60f).ShouldBeTrue();
        camera.Position.Z.ShouldBe(4.75f, 1e-4f);

        controls.Wheel(200f, 400f, 300f);
        controls.Update(1f / 60f);
        camera.Position.Z.ShouldBe(4.75f / (0.95f * 0.95f), 1e-4f);

        controls.ZoomSpeed = 2f;
        controls.Dolly(0.5f);
        controls.Update(0f);
        controls.Distance.ShouldBe(4.75f / (0.95f * 0.95f) / 2f, 1e-4f);
    }

    [Fact]
    public void Right_Button_Pans_By_The_World_Size_Of_The_Drag()
    {
        var (camera, controls) = Create();

        controls.PointerDown(1, 400f, 300f, PointerButton.Right);
        controls.PointerMove(1, 460f, 300f);
        controls.Update(1f / 60f);

        // 60 px at distance 5 with a 50° lens over 600 px.
        var expected = 2f * 60f * 5f * MathF.Tan(MathUtil.ToRadians(25f)) / 600f;
        ShouldBeNear(controls.Target, new Vector3(-expected, 0f, 0f));
        ShouldBeNear(camera.Position, new Vector3(-expected, 0f, 5f));

        controls.PointerMove(1, 460f, 360f);
        controls.Update(1f / 60f);
        ShouldBeNear(controls.Target, new Vector3(-expected, expected, 0f));
        controls.PointerUp(1);
    }

    [Fact]
    public void A_Point_Under_The_Cursor_Follows_A_Pan_Drag()
    {
        var (camera, controls) = Create();
        var world = new Vector3(0.5f, -0.3f, 0f);
        var before = camera.Project(world);
        var pixel = new Vector2((before.X + 1f) * 400f, (1f - before.Y) * 300f);

        controls.PointerDown(1, pixel.X, pixel.Y, PointerButton.Right);
        controls.PointerMove(1, pixel.X + 80f, pixel.Y - 45f);
        controls.Update(1f / 60f);

        var after = camera.Project(world);
        ((after.X + 1f) * 400f).ShouldBe(pixel.X + 80f, 0.5f);
        ((1f - after.Y) * 300f).ShouldBe(pixel.Y - 45f, 0.5f);
    }

    [Fact]
    public void Modifiers_Swap_Rotate_And_Pan_And_Buttons_Can_Be_Remapped()
    {
        var (camera, controls) = Create();

        controls.PointerDown(1, 400f, 300f, PointerButton.Left, ControlModifiers.Shift);
        controls.PointerMove(1, 430f, 300f);
        controls.Update(1f / 60f);
        controls.PointerUp(1);
        controls.Target.X.ShouldBeLessThan(0f);
        controls.AzimuthAngle.ShouldBe(0f, 1e-4f);

        controls.PointerDown(1, 400f, 300f, PointerButton.Right, ControlModifiers.Control);
        controls.PointerMove(1, 430f, 300f);
        controls.Update(1f / 60f);
        controls.PointerUp(1);
        controls.AzimuthAngle.ShouldBeLessThan(0f);

        var map = new MapControls(new PerspectiveCamera { Position = new Vector3(0f, 5f, 5f) }) { ViewportHeight = 600f };
        map.Update(0f);
        map.PointerDown(1, 400f, 300f, PointerButton.Left);
        map.PointerMove(1, 400f, 350f);
        map.Update(1f / 60f);
        // Map panning stays in the horizontal plane.
        map.Target.Y.ShouldBe(0f, 1e-5f);
        map.Target.Z.ShouldBeLessThan(0f);
        _ = camera;
    }

    [Fact]
    public void Middle_Button_Drag_Dollies()
    {
        var (_, controls) = Create();

        controls.PointerDown(1, 400f, 300f, PointerButton.Middle);
        controls.PointerMove(1, 400f, 400f);
        controls.Update(1f / 60f);
        controls.Distance.ShouldBe(5f / 0.95f, 1e-4f);
        controls.PointerMove(1, 400f, 200f);
        controls.Update(1f / 60f);
        controls.Distance.ShouldBe(5f / 0.95f * 0.95f * 0.95f, 1e-4f);
        controls.PointerUp(1);
    }

    [Fact]
    public void Limits_Clamp_Distance_And_Angles()
    {
        var (_, controls) = Create();
        controls.MinDistance = 2f;
        controls.MaxDistance = 8f;
        controls.MinPolarAngle = MathF.PI / 4f;
        controls.MaxPolarAngle = MathF.PI / 2f;
        controls.MinAzimuthAngle = -MathF.PI / 6f;
        controls.MaxAzimuthAngle = MathF.PI / 6f;

        controls.Dolly(0.01f);
        controls.Update(0f);
        controls.Distance.ShouldBe(2f, 1e-4f);
        controls.Dolly(1000f);
        controls.Update(0f);
        controls.Distance.ShouldBe(8f, 1e-4f);

        controls.RotateUp(10f);
        controls.Update(0f);
        controls.PolarAngle.ShouldBe(MathF.PI / 4f, 1e-4f);
        controls.RotateUp(-10f);
        controls.Update(0f);
        controls.PolarAngle.ShouldBe(MathF.PI / 2f, 1e-4f);

        controls.RotateLeft(2f);
        controls.Update(0f);
        controls.AzimuthAngle.ShouldBe(-MathF.PI / 6f, 1e-4f);
        controls.RotateLeft(-4f);
        controls.Update(0f);
        controls.AzimuthAngle.ShouldBe(MathF.PI / 6f, 1e-4f);
    }

    [Fact]
    public void The_Poles_Are_Reachable_Without_Flipping()
    {
        var (camera, controls) = Create();

        controls.RotateUp(10f);
        controls.Update(0f);
        controls.PolarAngle.ShouldBeLessThan(1e-3f);
        camera.Position.Y.ShouldBe(5f, 1e-3f);
        ShouldBeNear(Vector3.Transform(-Vector3.UnitZ, camera.Rotation), -Vector3.UnitY, 1e-2f);
        controls.RotateUp(-20f);
        controls.Update(0f);
        camera.Position.Y.ShouldBe(-5f, 1e-3f);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(144)]
    public void Damping_Reaches_The_Same_Pose_At_Any_Frame_Rate(int framesPerSecond)
    {
        var (camera, controls) = Create();
        controls.EnableDamping = true;
        controls.DampingFactor = 0.1f;
        controls.RotateLeft(1f);
        controls.RotateUp(0.4f);
        controls.Pan(40f, 0f);

        // Half a second in: part of the movement is applied.
        for (var i = 0; i < framesPerSecond / 2; i++)
        {
            controls.Update(1f / framesPerSecond).ShouldBeTrue();
        }

        var remaining = MathF.Pow(0.9f, 30f);
        controls.AzimuthAngle.ShouldBe(-(1f - remaining), 2e-3f);

        var frames = 0;
        while (controls.Update(1f / framesPerSecond) && frames < 100_000)
        {
            frames++;
        }

        frames.ShouldBeLessThan(framesPerSecond * 10);
        controls.AzimuthAngle.ShouldBe(-1f, 2e-3f);
        controls.PolarAngle.ShouldBe((MathF.PI / 2f) - 0.4f, 2e-3f);
        controls.Update(1f / framesPerSecond).ShouldBeFalse();
        _ = camera;
    }

    [Fact]
    public void Without_Damping_Input_Applies_At_Once_And_Idle_Updates_Return_False()
    {
        var (_, controls) = Create();
        var changed = 0;
        controls.Changed += (_, _) => changed++;

        controls.Update(1f / 60f).ShouldBeFalse();
        controls.RotateLeft(0.3f);
        controls.Update(1f / 60f).ShouldBeTrue();
        controls.Update(1f / 60f).ShouldBeFalse();
        controls.Update(1f / 60f).ShouldBeFalse();
        changed.ShouldBe(1);
    }

    [Fact]
    public void Auto_Rotate_Turns_Once_In_Thirty_Seconds_At_Speed_Two_And_Pauses_While_Dragging()
    {
        var (_, controls) = Create();
        controls.AutoRotate = true;

        for (var i = 0; i < 450; i++)
        {
            controls.Update(1f / 60f).ShouldBeTrue();
        }

        // 7.5 s of 30 s: a quarter turn.
        controls.AzimuthAngle.ShouldBe(-MathF.PI / 2f, 1e-2f);

        controls.PointerDown(1, 10f, 10f, PointerButton.Left);
        var angle = controls.AzimuthAngle;
        controls.Update(1f).ShouldBeFalse();
        controls.AzimuthAngle.ShouldBe(angle, 1e-5f);
        controls.PointerUp(1);
    }

    [Fact]
    public void One_Finger_Rotates_And_A_Pinch_Dollies_And_Pans()
    {
        var (camera, controls) = Create();

        controls.PointerDown(10, 300f, 300f, PointerButton.Touch);
        controls.PointerMove(10, 375f, 300f);
        controls.Update(1f / 60f);
        controls.AzimuthAngle.ShouldBe(-MathF.PI / 4f, 1e-4f);
        controls.PointerUp(10);
        controls.Reset();
        controls.AzimuthAngle.ShouldBe(0f, 1e-5f);

        // Two fingers 100 px apart spread to 200 px around the same center: the distance halves.
        controls.PointerDown(10, 350f, 300f, PointerButton.Touch);
        controls.PointerDown(11, 450f, 300f, PointerButton.Touch);
        controls.PointerMove(10, 300f, 300f);
        controls.PointerMove(11, 500f, 300f);
        controls.Update(1f / 60f);
        controls.Distance.ShouldBe(2.5f, 1e-3f);
        ShouldBeNear(controls.Target, Vector3.Zero);

        // Moving both fingers together pans.
        controls.PointerMove(10, 300f, 360f);
        controls.PointerMove(11, 500f, 360f);
        controls.Update(1f / 60f);
        controls.Distance.ShouldBe(2.5f, 1e-3f);
        controls.Target.Y.ShouldBeGreaterThan(0.1f);

        // Lifting one finger continues with a one-finger rotate, without a jump.
        var azimuth = controls.AzimuthAngle;
        controls.PointerUp(10);
        controls.IsInteracting.ShouldBeTrue();
        controls.Update(1f / 60f);
        controls.AzimuthAngle.ShouldBe(azimuth, 1e-5f);
        controls.PointerMove(11, 530f, 360f);
        controls.Update(1f / 60f);
        controls.AzimuthAngle.ShouldBeLessThan(azimuth);
        controls.PointerUp(11);
        controls.IsInteracting.ShouldBeFalse();
        _ = camera;
    }

    [Fact]
    public void Orthographic_Cameras_Zoom_Instead_Of_Moving_And_Pan_By_View_Size()
    {
        var camera = new OrthographicCamera(-4f, 4f, 3f, -3f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 10f) };
        var controls = new OrbitControls(camera) { ViewportWidth = 800f, ViewportHeight = 600f, MinZoom = 0.5f, MaxZoom = 4f };
        controls.Update(0f);

        controls.Wheel(-100f, 400f, 300f);
        controls.Update(1f / 60f).ShouldBeTrue();
        camera.Zoom.ShouldBe(1f / 0.95f, 1e-4f);
        controls.Distance.ShouldBe(10f, 1e-4f);

        controls.Dolly(0.001f);
        controls.Update(0f);
        camera.Zoom.ShouldBe(4f, 1e-4f);
        controls.Dolly(1000f);
        controls.Update(0f);
        camera.Zoom.ShouldBe(0.5f, 1e-4f);

        // At zoom 0.5 the view is 16 units wide: 100 px of 800 is 2 units.
        controls.PointerDown(1, 400f, 300f, PointerButton.Right);
        controls.PointerMove(1, 500f, 300f);
        controls.Update(1f / 60f);
        controls.PointerUp(1);
        controls.Target.X.ShouldBe(-2f, 1e-3f);
    }

    [Fact]
    public void Zoom_To_Cursor_Keeps_The_Point_Under_The_Cursor_Fixed()
    {
        var (camera, controls) = Create();
        controls.ZoomToCursor = true;
        var cursor = new Vector2(620f, 180f);
        var ndc = new Vector2((cursor.X / 400f) - 1f, 1f - (cursor.Y / 300f));
        var ray = camera.ScreenPointToRay(ndc);
        var world = ray.At(5f);

        for (var i = 0; i < 6; i++)
        {
            controls.Wheel(-240f, cursor.X, cursor.Y);
            controls.Update(1f / 60f).ShouldBeTrue();
            var projected = camera.Project(world);
            projected.X.ShouldBe(ndc.X, 2e-3f);
            projected.Y.ShouldBe(ndc.Y, 2e-3f);
        }

        Vector3.Distance(camera.Position, new Vector3(0f, 0f, 5f)).ShouldBeGreaterThan(1f);
        // The target stays in front of the camera at the new distance.
        ShouldBeNear(Vector3.Normalize(controls.Target - camera.Position), Vector3.Transform(-Vector3.UnitZ, camera.Rotation), 1e-3f);

        var ortho = new OrthographicCamera(-4f, 4f, 3f, -3f, 0.1f, 100f) { Position = new Vector3(0f, 0f, 10f) };
        var orthoControls = new OrbitControls(ortho) { ViewportWidth = 800f, ViewportHeight = 600f, ZoomToCursor = true };
        orthoControls.Update(0f);
        var point = ortho.Unproject(new Vector3(ndc, 0.5f));
        orthoControls.Wheel(-300f, cursor.X, cursor.Y);
        orthoControls.Update(1f / 60f);
        ortho.Zoom.ShouldBeGreaterThan(1.1f);
        var after = ortho.Project(point);
        after.X.ShouldBe(ndc.X, 2e-3f);
        after.Y.ShouldBe(ndc.Y, 2e-3f);
    }

    [Theory]
    [InlineData(4f / 3f)]
    [InlineData(0.5f)]
    [InlineData(2.5f)]
    public void FitToBounds_Puts_The_Whole_Box_In_View(float aspect)
    {
        var camera = new PerspectiveCamera(40f, aspect, 0.1f, 50f) { Position = new Vector3(3f, 2f, 4f) };
        var controls = new OrbitControls(camera);
        controls.Update(0f);
        var direction = Vector3.Normalize(camera.Position);
        var box = new BoundingBox(new Vector3(10f, -30f, 5f), new Vector3(90f, 20f, 45f));

        controls.FitToBounds(box, 1f);

        ShouldBeNear(controls.Target, box.Center);
        ShouldBeNear(Vector3.Normalize(camera.Position - controls.Target), direction, 1e-3f);
        var extreme = 0f;
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z);
            var ndc = camera.Project(corner);
            MathF.Abs(ndc.X).ShouldBeLessThanOrEqualTo(1.0001f);
            MathF.Abs(ndc.Y).ShouldBeLessThanOrEqualTo(1.0001f);
            ndc.Z.ShouldBeInRange(0f, 1f);
            extreme = MathF.Max(extreme, MathF.Max(MathF.Abs(ndc.X), MathF.Abs(ndc.Y)));
        }

        // Not wastefully far either: some corner comes reasonably close to an edge.
        extreme.ShouldBeGreaterThan(0.4f);
        camera.Far.ShouldBeGreaterThan(50f);

        var ortho = new OrthographicCamera(-2f * aspect, 2f * aspect, 2f, -2f, 0.1f, 500f) { Position = new Vector3(3f, 2f, 4f) };
        var orthoControls = new OrbitControls(ortho);
        orthoControls.FitToBounds(box);
        for (var i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? box.Min.X : box.Max.X, (i & 2) == 0 ? box.Min.Y : box.Max.Y, (i & 4) == 0 ? box.Min.Z : box.Max.Z);
            var ndc = ortho.Project(corner);
            MathF.Abs(ndc.X).ShouldBeLessThanOrEqualTo(1.0001f);
            MathF.Abs(ndc.Y).ShouldBeLessThanOrEqualTo(1.0001f);
            ndc.Z.ShouldBeInRange(0f, 1f);
        }
    }

    [Fact]
    public void Keys_Pan_And_With_A_Modifier_Rotate()
    {
        var (_, controls) = Create();

        controls.KeyDown(ControlKey.Left).ShouldBeTrue();
        controls.Update(0f);
        var expected = 2f * 7f * 5f * MathF.Tan(MathUtil.ToRadians(25f)) / 600f;
        controls.Target.X.ShouldBe(-expected, 1e-4f);

        controls.KeyDown(ControlKey.Up, ControlModifiers.Shift).ShouldBeTrue();
        controls.Update(0f);
        controls.PolarAngle.ShouldBe((MathF.PI / 2f) - (MathF.PI * 2f / 600f), 1e-4f);

        controls.EnablePan = false;
        controls.KeyDown(ControlKey.Right).ShouldBeFalse();
    }

    [Fact]
    public void Disabled_Controls_Ignore_Input_And_Enable_Flags_Gate_Each_Action()
    {
        var (camera, controls) = Create();
        controls.Enabled = false;
        controls.PointerDown(1, 0f, 0f, PointerButton.Left);
        controls.PointerMove(1, 200f, 100f);
        controls.Wheel(-500f, 0f, 0f);
        controls.Update(1f / 60f).ShouldBeFalse();
        ShouldBeNear(camera.Position, new Vector3(0f, 0f, 5f));

        controls.Enabled = true;
        controls.EnableRotate = false;
        controls.EnableZoom = false;
        controls.EnablePan = false;
        controls.PointerDown(1, 0f, 0f, PointerButton.Left);
        controls.IsInteracting.ShouldBeFalse();
        controls.PointerDown(1, 0f, 0f, PointerButton.Right);
        controls.IsInteracting.ShouldBeFalse();
        controls.Wheel(-500f, 0f, 0f);
        controls.Update(1f / 60f).ShouldBeFalse();
    }

    [Fact]
    public void Start_And_End_Bracket_Interactions_And_Reset_Restores_The_Saved_State()
    {
        var (camera, controls) = Create();
        var log = new List<string>();
        controls.Start += (_, _) => log.Add("start");
        controls.End += (_, _) => log.Add("end");
        controls.Changed += (_, _) => log.Add("changed");

        controls.PointerDown(1, 0f, 0f, PointerButton.Left);
        controls.PointerMove(1, 40f, 25f);
        controls.Update(1f / 60f);
        controls.PointerUp(1);
        controls.PointerUp(1);
        log.ShouldBe(["start", "changed", "end"]);

        controls.Target = new Vector3(1f, 2f, 3f);
        camera.Position = new Vector3(4f, 5f, 6f);
        controls.SaveState();
        controls.RotateLeft(1f);
        controls.Dolly(3f);
        controls.Update(0f);
        controls.Reset();
        ShouldBeNear(controls.Target, new Vector3(1f, 2f, 3f));
        ShouldBeNear(camera.Position, new Vector3(4f, 5f, 6f));
    }
}
