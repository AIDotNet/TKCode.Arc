using TKCode.Arc.Gestures;
using TKCode.Arc.Motion;
using Avalonia;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceCamera2D"/>: fit, anchored zoom, bounds, rubber band, glides, flights, motion levels.</summary>
public class ShowpieceCamera2DTests
{
    private const double Frame = 1 / 60.0;

    // Semantic zoom 4.1: a 663×400 stage, content 1236 × 682.5 wu, insets 24 / 64 / 24 / 60 → k₀ = 276 / 682.5 = 0.4044.
    private static ShowpieceCamera2D Semantic(ArcMotionLevel level = ArcMotionLevel.Full)
    {
        var camera = new ShowpieceCamera2D
        {
            MinZoom = 1,
            MaxZoom = 53.04,
            Insets = new Thickness(24, 64, 24, 60),
            BoundsMode = ShowpieceBoundsMode.Immediate,
            ZoomOverflow = ShowpieceZoomOverflow.RubberBand,
            HasInertia = true,
            FlightMode = ShowpieceFlightMode.VanWijk,
            MotionLevel = level
        };
        camera.SetViewport(new Size(663, 400));
        camera.SetContent(new Rect(0, 0, 1236, 682.5));
        return camera;
    }

    // Customer galaxy 4.7: zoom 1–9 × fit, no glide, bounds after the gesture, flights on the ω 5.7 spring.
    private static ShowpieceCamera2D Galaxy()
    {
        var camera = new ShowpieceCamera2D
        {
            MinZoom = 1,
            MaxZoom = 9,
            Insets = new Thickness(14),
            BoundsMode = ShowpieceBoundsMode.AfterGesture,
            ZoomOverflow = ShowpieceZoomOverflow.Clamp,
            FollowSpring = ShowpieceSprings.ZoomFollow,
            FlySpring = ShowpieceSprings.FlyFollow,
            SettleSpring = ShowpieceSprings.Recenter
        };
        camera.SetViewport(new Size(663, 360));
        // The galaxy decides its own fit scale; the content has room on both axes at fit.
        camera.SetContent(new Rect(-215, -141, 430, 282), fitScale: 1);
        return camera;
    }

    private static void Run(ShowpieceCamera2D camera, double seconds)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += Frame)
        {
            camera.Advance(Frame);
        }
    }

    private static int Settle(ShowpieceCamera2D camera)
    {
        var frames = 0;
        while (camera.Advance(Frame) && frames < 3000)
        {
            frames++;
        }
        return frames;
    }

    [Fact]
    public void Fit_Frames_The_Content_In_The_Safe_Area()
    {
        var camera = Semantic();
        camera.FitScale.ShouldBe(0.4044, 1e-4);
        camera.Zoom.ShouldBe(1, 1e-12);
        camera.IsAtFit.ShouldBeTrue();
        var center = camera.WorldToScreen(new Point(618, 341.25));
        center.X.ShouldBe(24 + (663 - 48) / 2.0, 1e-9);
        center.Y.ShouldBe(64 + (400 - 124) / 2.0, 1e-9);
        camera.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Anchored_Zoom_Keeps_The_Anchor_Fixed_Through_The_Spring()
    {
        var camera = Semantic();
        camera.JumpTo(new Point(600, 300), 4);
        var anchor = new Point(500, 150);
        var world = camera.ScreenToWorld(anchor);
        camera.ZoomBy(ShowpieceWheel.ZoomFactor(1, ShowpieceWheel.WheelSensitivity), anchor);
        camera.TargetZoom.ShouldBe(4 * 1.2461, 1e-3);
        camera.IsMoving.ShouldBeTrue();
        for (var i = 0; i < 40; i++)
        {
            camera.Advance(Frame);
            // The anchored world point stays under the pointer (to a hundredth of a pixel: channels snap to rest separately).
            var screen = camera.WorldToScreen(world);
            screen.X.ShouldBe(anchor.X, 0.01);
            screen.Y.ShouldBe(anchor.Y, 0.01);
        }
        Settle(camera);
        camera.Zoom.ShouldBe(4 * 1.2461, 1e-3);
    }

    [Fact]
    public void Zoom_Limits_Clamp_Or_Rubber_Band()
    {
        var galaxy = Galaxy();
        galaxy.ZoomBy(1 / 1.246);
        galaxy.TargetZoom.ShouldBe(1, 1e-12);
        galaxy.ZoomBy(100);
        galaxy.TargetZoom.ShouldBe(9, 1e-9);

        // Semantic zoom: a request past fit is rubber-banded in ln z (−0.055 → −0.026) and returns after 150ms.
        var semantic = Semantic();
        semantic.ZoomBy(Math.Exp(-0.055));
        Math.Log(semantic.TargetZoom).ShouldBe(-0.026, 0.002);
        Run(semantic, 0.1);
        semantic.TargetZoom.ShouldBeLessThan(1);
        Run(semantic, 0.1);
        semantic.TargetZoom.ShouldBe(1, 1e-9);
        Settle(semantic);
        semantic.Zoom.ShouldBe(1, 1e-6);
        semantic.IsAtFit.ShouldBeTrue();
    }

    [Fact]
    public void Drags_Rubber_Band_Past_The_Bounds_And_Spring_Back()
    {
        var camera = Semantic();
        var probe = new Point(618, 341.25);
        var before = camera.WorldToScreen(probe);
        camera.BeginPan();
        camera.PanBy(new Vector(100, 0));
        // At fit the content cannot move: the whole drag is overflow, Apply(100, 663, 0.55) = 50.8.
        (camera.WorldToScreen(probe).X - before.X).ShouldBe(50.8, 0.05);
        camera.PanBy(new Vector(-100, 0));
        camera.WorldToScreen(probe).X.ShouldBe(before.X, 1e-6);
        camera.PanBy(new Vector(50, 0));
        camera.EndPan(new Vector(300, 0));
        camera.IsGliding.ShouldBeFalse();
        camera.IsMoving.ShouldBeTrue();
        Settle(camera);
        camera.WorldToScreen(probe).X.ShouldBe(before.X, 0.05);
        camera.IsAtFit.ShouldBeTrue();
    }

    [Fact]
    public void Release_Glides_With_The_Measured_Decay()
    {
        var camera = Semantic();
        camera.JumpTo(new Point(600, 300), 4);
        var probe = new Point(600, 300);
        camera.BeginPan();
        camera.PanBy(new Vector(-10, 0));
        var released = camera.WorldToScreen(probe).X;
        camera.EndPan(new Vector(-290, 0));
        camera.IsGliding.ShouldBeTrue();
        Settle(camera);
        // 290px/s × τ 0.274s ≈ 80px more, in the direction of the drag.
        (camera.WorldToScreen(probe).X - released).ShouldBe(-79.5, 3);
    }

    [Fact]
    public void A_Glide_Into_A_Bound_Stops_And_Springs_Back_Inside()
    {
        var camera = Semantic();
        camera.JumpTo(new Point(600, 300), 4);
        var range = camera.CenterRange(camera.Scale);
        camera.BeginPan();
        camera.PanBy(new Vector(-1, 0));
        camera.EndPan(new Vector(-6000, 0));
        var furthest = 0.0;
        for (var i = 0; i < 3000 && camera.Advance(Frame); i++)
        {
            furthest = Math.Max(furthest, camera.Center.X);
        }
        furthest.ShouldBeGreaterThan(range.Right);
        camera.Center.X.ShouldBe(range.Right, 1e-3);
        camera.IsGliding.ShouldBeFalse();
    }

    [Fact]
    public void Retargeting_Keeps_The_Velocity()
    {
        var camera = Semantic();
        camera.JumpTo(new Point(600, 300), 2);
        camera.ZoomBy(2);
        camera.Advance(Frame);
        camera.Advance(Frame);
        var scale = camera.Scale;
        var velocity = camera.ScaleVelocity;
        velocity.ShouldBeGreaterThan(0);
        camera.ZoomBy(1.1);
        camera.Scale.ShouldBe(scale);
        camera.ScaleVelocity.ShouldBe(velocity);
        // A flight interrupted by the wheel hands its speed to the follow spring.
        camera.FlyTo(new Point(200, 200), 8);
        camera.Advance(Frame);
        camera.Advance(Frame);
        camera.IsFlying.ShouldBeTrue();
        camera.ZoomBy(1.01);
        camera.IsFlying.ShouldBeFalse();
        Math.Abs(camera.ScaleVelocity).ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Land_At_Once_Without_Glides(ArcMotionLevel level)
    {
        var camera = Semantic(level);
        camera.ZoomBy(2);
        camera.Zoom.ShouldBe(2, 1e-9);
        camera.IsMoving.ShouldBeFalse();
        camera.FlyTo(new Point(300, 200), 6);
        camera.Zoom.ShouldBe(6, 1e-9);
        camera.IsFlying.ShouldBeFalse();
        camera.BeginPan();
        camera.PanBy(new Vector(-20, 0));
        camera.EndPan(new Vector(-800, 0));
        camera.IsGliding.ShouldBeFalse();
        camera.IsMoving.ShouldBeFalse();
        camera.Fit();
        camera.IsAtFit.ShouldBeTrue();
        camera.Zoom.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Team_Framing_Matches_The_Measured_Zoom()
    {
        // Semantic zoom 6.1: a team panel 0.97·682.5/1.5876 ≈ 417 wu high frames at z 1.5876 (f = 0.97).
        var camera = Semantic();
        var (center, zoom) = camera.FrameRect(new Rect(0, 0, 300, 417), 0.97);
        zoom.ShouldBe(1.5876, 1e-3);
        // The rect center lands on the safe-area center.
        center.X.ShouldBe(150 - (24 - 24) / (2 * zoom * camera.FitScale), 1e-9);
        center.Y.ShouldBe(208.5 - (64 - 60) / (2 * zoom * camera.FitScale), 1e-9);
    }

    [Fact]
    public void Van_Wijk_Flights_Take_460_Plus_380_S()
    {
        var camera = Semantic();
        var target = new Point(300, 220);
        var extent = 663.0;
        var path = VanWijkPath.Create(camera.Center, extent / camera.Scale, target, extent / (camera.FitScale * 4));
        camera.FlyTo(target, 4);
        camera.IsFlying.ShouldBeTrue();
        var frames = 0;
        var minZoom = double.MaxValue;
        while (camera.IsFlying && frames < 600)
        {
            camera.Advance(Frame);
            minZoom = Math.Min(minZoom, camera.Zoom);
            frames++;
        }
        (frames * Frame).ShouldBe(path.Duration.TotalSeconds, Frame + 1e-9);
        camera.Zoom.ShouldBe(4, 1e-9);
        camera.Center.X.ShouldBe(target.X, 1e-6);
        camera.Center.Y.ShouldBe(target.Y, 1e-6);
        // In-out pacing: the first frames barely move.
        minZoom.ShouldBeGreaterThan(0.9);
    }

    [Fact]
    public void Spring_Flights_Settle_On_The_Fly_Spring()
    {
        var camera = Galaxy();
        camera.FlyTo(new Point(40, 20), 2.3);
        camera.IsFlying.ShouldBeFalse();
        Run(camera, 1.0);
        // ω 5.7: 98% of the way after about one second.
        ((camera.Zoom - 1) / 1.3).ShouldBeGreaterThan(0.95);
        Settle(camera);
        camera.Zoom.ShouldBe(2.3, 1e-4);
    }

    [Fact]
    public void Bounds_After_The_Gesture_Recenter_Only_Once_The_Wheel_Rests()
    {
        var camera = Galaxy();
        camera.IsAtFit.ShouldBeTrue();
        // An anchored zoom at the right edge pushes the view off-center while the content still fits.
        camera.ZoomBy(1.1, new Point(600, 180));
        camera.TargetCenter.X.ShouldBeGreaterThan(5);
        Run(camera, 1.0);
        camera.TargetCenter.X.ShouldBeGreaterThan(5);
        camera.IsMoving.ShouldBeTrue();
        Run(camera, 0.2);
        camera.TargetCenter.X.ShouldBe(0, 1e-6);
        Settle(camera);
        camera.Center.X.ShouldBe(0, 0.05);
        camera.IsMoving.ShouldBeFalse();
    }

    [Fact]
    public void Immediate_Bounds_Hold_The_Content_Centered_While_It_Fits()
    {
        var camera = Semantic();
        // Under z ≈ 1.23 the content is narrower than the stage: horizontal zoom anchors stay centered.
        camera.ZoomBy(1.1, new Point(600, 202));
        camera.TargetCenter.X.ShouldBe(618, 1e-9);
    }

    [Fact]
    public void Resizing_Keeps_The_Readout_And_Center()
    {
        var camera = Semantic();
        camera.JumpTo(new Point(500, 300), 3);
        camera.SetViewport(new Size(1046, 580));
        camera.Zoom.ShouldBe(3, 1e-9);
        camera.Center.X.ShouldBe(500, 1e-6);
        // Wide layout fit: k₀ = 0.6681.
        camera.FitScale.ShouldBe(0.6681, 1e-3);
    }

    [Fact]
    public void Changes_Notify_And_Idle_Frames_Do_Not()
    {
        var camera = Semantic();
        var changes = 0;
        camera.Changed += () => changes++;
        camera.Advance(Frame);
        changes.ShouldBe(0);
        var version = camera.Version;
        camera.ZoomBy(1.5);
        changes.ShouldBeGreaterThan(0);
        camera.Version.ShouldBeGreaterThan(version);
        Settle(camera);
        var settled = changes;
        camera.Advance(Frame);
        changes.ShouldBe(settled);
    }
}
