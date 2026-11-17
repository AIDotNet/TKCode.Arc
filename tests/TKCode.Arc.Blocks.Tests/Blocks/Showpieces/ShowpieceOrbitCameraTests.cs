using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceOrbitCamera"/>: drag orbit, pitch limits, glides, steps, poses, drift, motion levels.</summary>
public class ShowpieceOrbitCameraTests
{
    private const double Frame = 1 / 60.0;

    private static double Rad(double degrees) => ShowpieceProjection.Radians(degrees);

    private static double Deg(double radians) => ShowpieceProjection.Degrees(radians);

    // Activity terrain 6.1: yaw 32°, pitch 30° in [10°, 84°], 1/180 rad per px, rubber band d 23° c 0.55, τ 0.31s.
    private static ShowpieceOrbitCamera Terrain(ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var camera = new ShowpieceOrbitCamera
        {
            PitchMin = Rad(10),
            PitchMax = Rad(84),
            PitchRubberBandDimension = Rad(23),
            PitchRubberBandFactor = 0.55,
            ZoomMin = 0.7,
            ZoomMax = 2.8,
            HasPitchInertia = true,
            RestPose = new ShowpieceOrbitPose { Yaw = Rad(32), Pitch = Rad(30), Zoom = 1, Distance = 42, Target = new ShowpieceVec3(0, 0.864, 0) },
            MotionLevel = level
        };
        camera.JumpTo(camera.RestPose);
        return camera;
    }

    // Revenue globe 6.1–6.2: pitch ±65° with a 25° / 0.77 rubber band, home 22°, glide × 0.987 per frame, drift.
    private static ShowpieceOrbitCamera Globe(double radius = 179.36)
    {
        var camera = new ShowpieceOrbitCamera
        {
            PitchMin = Rad(-65),
            PitchMax = Rad(65),
            PitchRubberBandDimension = Rad(25),
            PitchRubberBandFactor = 0.77,
            PitchReturnSpring = ShowpieceSprings.PitchHome,
            YawPerPixel = -1 / radius,
            PitchPerPixel = 1 / radius,
            InertiaTimeConstant = ShowpieceInertia.TimeConstantFromFrameFactor(ShowpieceInertia.GlobeFrameFactor),
            DriftCenter = Rad(-65),
            DriftHalfRange = Rad(43),
            DriftSpeed = Rad(4),
            DriftTimeConstant = 1.4
        };
        camera.JumpTo(new ShowpieceOrbitPose { Yaw = Rad(-65), Pitch = Rad(22), Zoom = 1 });
        camera.PitchRest = Rad(22);
        return camera;
    }

    private static void Run(ShowpieceOrbitCamera camera, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += Frame)
        {
            camera.Advance(Frame);
        }
    }

    private static void Settle(ShowpieceOrbitCamera camera)
    {
        for (var i = 0; i < 3000 && camera.Advance(Frame); i++)
        {
        }
    }

    [Fact]
    public void Drags_Orbit_At_One_Over_180_Radians_Per_Pixel()
    {
        var camera = Terrain();
        camera.BeginDrag();
        camera.DragBy(new Vector(180, 0));
        camera.Yaw.ShouldBe(Rad(32) + 1, 1e-12);
        // 0.318° per px.
        Deg(1 / 180.0).ShouldBe(0.318, 0.001);
        camera.DragBy(new Vector(0, 18));
        camera.Pitch.ShouldBe(Rad(30) + 0.1, 1e-12);
        camera.IsAtRest.ShouldBeFalse();
    }

    [Fact]
    public void Pitch_Rubber_Bands_Past_Its_Limits_And_Returns()
    {
        var camera = Terrain();
        camera.BeginDrag();
        // Raw pitch 84° + 19.6° shows 84° + 7.5°; 84° + 60.7° shows about 84° + 14.4° (d 23°, c 0.55).
        camera.DragBy(new Vector(0, (Rad(84 + 19.6) - Rad(30)) * 180));
        Deg(camera.Pitch).ShouldBe(84 + 7.5, 0.3);
        camera.DragBy(new Vector(0, Rad(60.7 - 19.6) * 180));
        Deg(camera.Pitch).ShouldBe(84 + 14.4, 1.0);
        camera.EndDrag();
        camera.IsMoving.ShouldBeTrue();
        Settle(camera);
        Deg(camera.Pitch).ShouldBe(84, 1e-3);
    }

    [Fact]
    public void Globe_Pitch_Uses_Its_Own_Rubber_Band_And_Goes_Home()
    {
        var radius = 179.36;
        var camera = Globe(radius);
        camera.BeginDrag();
        // Drag down 200px: raw 22° + 63.9° = 85.9° shows 74.9°.
        camera.DragBy(new Vector(0, 200));
        Deg(camera.Pitch).ShouldBe(74.9, 0.3);
        camera.EndDrag();
        Settle(camera);
        Deg(camera.Pitch).ShouldBe(22, 1e-3);

        camera.BeginDrag();
        // Dragging up to a raw −105.8° shows −79° (Arc: 340px up from about 3°).
        camera.DragBy(new Vector(0, Rad(-105.8 - 22) * radius));
        Deg(camera.Pitch).ShouldBe(-79, 0.3);
        camera.EndDrag();
        // Right drags turn the surface right: the center longitude decreases by 1/R per px.
        camera.BeginDrag();
        var yaw = camera.Yaw;
        camera.DragBy(new Vector(radius, 0));
        camera.Yaw.ShouldBe(yaw - 1, 1e-12);
    }

    [Fact]
    public void Release_Glides_About_30_Degrees_At_303_Px_Per_Second()
    {
        var camera = Terrain();
        camera.BeginDrag();
        camera.DragBy(new Vector(10, 0));
        var released = camera.Yaw;
        camera.EndDrag(new Vector(303, 0));
        camera.IsGliding.ShouldBeTrue();
        Settle(camera);
        Deg(camera.Yaw - released).ShouldBe(30, 1.5);
    }

    [Fact]
    public void Keyboard_Steps_Zoom_And_Reset()
    {
        var camera = Terrain();
        camera.StepYaw(Rad(15));
        camera.StepPitch(Rad(8));
        Deg(camera.TargetYaw).ShouldBe(47, 1e-9);
        Deg(camera.TargetPitch).ShouldBe(38, 1e-9);
        // Pitch steps stop at the limit.
        for (var i = 0; i < 10; i++)
        {
            camera.StepPitch(Rad(8));
        }
        Deg(camera.TargetPitch).ShouldBe(84, 1e-9);
        // Zoom ×1.25 steps clamp to [0.7, 2.8].
        for (var i = 0; i < 10; i++)
        {
            camera.ZoomBy(1.25);
        }
        camera.TargetZoom.ShouldBe(2.8, 1e-12);
        for (var i = 0; i < 20; i++)
        {
            camera.ZoomBy(1 / 1.25);
        }
        camera.TargetZoom.ShouldBe(0.7, 1e-12);
        Settle(camera);
        camera.IsAtRest.ShouldBeFalse();

        // Reset turns the short way: from 90.6° back down to 32°, not on to 392°.
        camera.JumpTo(new ShowpieceOrbitPose { Yaw = Rad(90.6 + 360) });
        camera.Reset();
        Deg(camera.TargetYaw).ShouldBe(32 + 360, 1e-9);
        camera.IsAtRest.ShouldBeTrue();
        Settle(camera);
        camera.Zoom.ShouldBe(1, 1e-6);
    }

    [Fact]
    public void Fly_In_Follows_The_Measured_Spring()
    {
        var camera = Terrain();
        var bar = new ShowpieceVec3(-2.5, 1.2, 1.0);
        camera.AnimateTo(new ShowpieceOrbitPose { Target = bar, Zoom = Math.Max(camera.Zoom, 1.5) }, ShowpieceSprings.OrbitFlyIn);
        Run(camera, 0.358);
        ((camera.Zoom - 1) / 0.5).ShouldBe(0.5, 0.03);
        Run(camera, 0.825 - 0.358);
        ((camera.Zoom - 1) / 0.5).ShouldBe(0.9, 0.03);
        Settle(camera);
        (camera.Target - bar).Length.ShouldBeLessThan(1e-4);
        // Yaw, pitch and distance were not part of the pose.
        Deg(camera.Yaw).ShouldBe(32, 1e-9);
        camera.Distance.ShouldBe(42);
    }

    [Fact]
    public void Retargets_Keep_The_Velocity()
    {
        var camera = Terrain();
        camera.AnimateTo(new ShowpieceOrbitPose { Yaw = Rad(80) }, ShowpieceSprings.OrbitReset);
        Run(camera, 0.1);
        var yaw = camera.Yaw;
        var velocity = camera.YawVelocity;
        velocity.ShouldBeGreaterThan(0);
        camera.AnimateTo(new ShowpieceOrbitPose { Yaw = Rad(20) }, ShowpieceSprings.OrbitReset);
        camera.Yaw.ShouldBe(yaw);
        camera.YawVelocity.ShouldBe(velocity);
    }

    [Theory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Jump_Without_Glides(ArcMotionLevel level)
    {
        var camera = Terrain(level);
        camera.AnimateTo(new ShowpieceOrbitPose { Yaw = 0, Pitch = Rad(90), Distance = 160 }, ShowpieceSprings.OrbitTopView);
        camera.Yaw.ShouldBe(0);
        camera.Distance.ShouldBe(160);
        camera.IsMoving.ShouldBeFalse();
        camera.BeginDrag();
        camera.DragBy(new Vector(30, 0));
        camera.EndDrag(new Vector(900, 0));
        camera.IsGliding.ShouldBeFalse();
        camera.ZoomBy(1.25);
        camera.Zoom.ShouldBe(1.25, 1e-12);
        camera.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Drift_Cruises_Back_And_Forth_At_Full_Motion_Only()
    {
        var camera = Globe();
        camera.IsDriftEnabled = true;
        camera.IsMoving.ShouldBeTrue();
        Run(camera, 6);
        // Speeding up toward 4°/s with τ 1.4s.
        Deg(Math.Abs(camera.YawVelocity)).ShouldBe(4 * (1 - Math.Exp(-6 / 1.4)), 0.1);
        var min = double.MaxValue;
        var max = double.MinValue;
        var turns = 0;
        var direction = Math.Sign(camera.YawVelocity);
        for (var t = 0.0; t < 120; t += Frame)
        {
            camera.Advance(Frame);
            var offset = Deg(ShowpieceProjection.WrapAngle(camera.Yaw - Rad(-65)));
            min = Math.Min(min, offset);
            max = Math.Max(max, offset);
            var now = Math.Sign(camera.YawVelocity);
            if (now != 0 && now != direction)
            {
                turns++;
                direction = now;
            }
        }
        turns.ShouldBeGreaterThanOrEqualTo(3);
        // Overshoots past ±43° are a few degrees (the turnaround), never runaway.
        max.ShouldBeInRange(43, 52);
        min.ShouldBeInRange(-52, -43);

        camera.MotionLevel = ArcMotionLevel.Reduced;
        camera.Advance(Frame);
        camera.IsDrifting.ShouldBeFalse();
        camera.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Slow_Glides_Hand_Over_To_The_Drift()
    {
        var camera = Globe();
        camera.IsDriftEnabled = true;
        camera.BeginDrag();
        camera.DragBy(new Vector(-20, 0));
        camera.EndDrag(new Vector(-400, 0));
        camera.IsGliding.ShouldBeTrue();
        Run(camera, 4);
        camera.IsGliding.ShouldBeFalse();
        camera.IsDrifting.ShouldBeTrue();
    }

    [Fact]
    public void Impulses_Add_Up_And_Pitch_Kicks_Spring_Home()
    {
        var camera = Globe();
        camera.Impulse(Rad(55));
        camera.Impulse(Rad(55));
        Deg(camera.YawVelocity).ShouldBe(110, 1e-6);
        camera.Impulse(0, Rad(-45));
        var lowest = double.MaxValue;
        for (var i = 0; i < 300; i++)
        {
            camera.Advance(Frame);
            lowest = Math.Min(lowest, Deg(camera.Pitch));
        }
        // ↑ swings the pitch down to about 16.5° before the ω 3 spring brings it back to 22°.
        lowest.ShouldBe(16.5, 1.0);
        Settle(camera);
        Deg(camera.Pitch).ShouldBe(22, 1e-3);
    }

    [Fact]
    public void Path_Flights_Follow_A_Spring_Progress()
    {
        var camera = Globe();
        camera.PitchRest = null;
        camera.FlyAlong(new LinearPath(Rad(-65), Rad(22), Rad(10), Rad(48)), ShowpieceSprings.GlobeFly);
        camera.IsFlying.ShouldBeTrue();
        Run(camera, 0.268);
        // GlobeFly: 0.51 of the way after 268ms.
        ((camera.Yaw - Rad(-65)) / Rad(75)).ShouldBe(0.51, 0.03);
        camera.BeginDrag();
        camera.IsFlying.ShouldBeFalse();
        camera.EndDrag();
        camera.FlyAlong(new LinearPath(camera.Yaw, camera.Pitch, Rad(10), Rad(48)), ShowpieceSprings.GlobeFly);
        Settle(camera);
        Deg(camera.Yaw).ShouldBe(10, 1e-3);
        Deg(camera.Pitch).ShouldBe(48, 1e-3);
    }

    [Fact]
    public void Views_Scale_With_Zoom()
    {
        var camera = Terrain();
        camera.JumpTo(new ShowpieceOrbitPose { Zoom = 1.5 });
        var view = camera.GetView(new Point(100, 100), 801);
        view.Focal.ShouldBe(1201.5, 1e-9);
        var globe = Globe();
        globe.JumpTo(new ShowpieceOrbitPose { Zoom = 1.3 });
        // Selected city ×1.30: R 179.36 → 233.17.
        globe.GetSphereView(default, 179.36).Radius.ShouldBe(233.17, 0.01);
    }

    private sealed class LinearPath(double yaw0, double pitch0, double yaw1, double pitch1) : IShowpieceOrbitPath
    {
        public ShowpieceOrbitPose Evaluate(double progress) => new()
        {
            Yaw = yaw0 + (yaw1 - yaw0) * progress,
            Pitch = pitch0 + (pitch1 - pitch0) * progress
        };
    }
}
