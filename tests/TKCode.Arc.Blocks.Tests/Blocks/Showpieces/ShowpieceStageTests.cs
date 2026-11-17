using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Showpieces;

/// <summary><see cref="ShowpieceStage"/>: drag threshold, taps, wheel modes, keys, frame loop gating, theme and motion refresh.</summary>
public class ShowpieceStageTests
{
    internal sealed class TestStage : ShowpieceStage
    {
        public TestStage(IShowpieceCamera? camera = null)
        {
            Width = 400;
            Height = 300;
            Camera = camera;
            RoleDescription = "zoomable map";
        }

        public List<string> Log { get; } = new();
        public List<(double Factor, Point? Anchor, ShowpieceZoomSource Source)> Zooms { get; } = new();
        public List<Point?> Hovers { get; } = new();
        public int ThemeChanges { get; private set; }
        public int LoopStops { get; private set; }
        public ArcMotionLevel? LastLevel { get; private set; }
        public string Summary { get; set; } = "A test stage";
        public ShowpieceTextCache Cache => TextCache;
        public IShowpieceCamera? StageCamera => Camera;

        protected override void RenderStage(DrawingContext context)
        {
        }

        protected override object? HitTestItem(Point position) => position.X < 100 ? "item" : null;

        protected override void OnTap(Point position, object? item, KeyModifiers modifiers) => Log.Add("tap " + (item ?? "none"));

        protected override void OnDoubleTap(Point position, object? item) => Log.Add("double " + (item ?? "none"));

        protected override void OnHoverChanged(Point? position, object? item) => Hovers.Add(position);

        protected override void OnDragStarted(Point position)
        {
            Log.Add("drag");
            base.OnDragStarted(position);
        }

        protected override void OnDragCompleted(Vector velocity)
        {
            Log.Add("drop");
            base.OnDragCompleted(velocity);
        }

        protected override void OnZoom(double factor, Point? anchor, ShowpieceZoomSource source)
        {
            Zooms.Add((factor, anchor, source));
            base.OnZoom(factor, anchor, source);
        }

        protected override void OnResetView()
        {
            Log.Add("reset");
            base.OnResetView();
        }

        protected override bool OnStageKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Right)
            {
                Log.Add("right");
                return true;
            }
            return base.OnStageKeyDown(e);
        }

        protected override void OnThemeChanged() => ThemeChanges++;

        protected override void OnLoopStopped() => LoopStops++;

        protected override void OnMotionLevelChanged(ArcMotionLevel level) => LastLevel = level;

        protected override string? GetAccessibleSummary() => Summary;
    }

    private static ShowpieceCamera2D Camera()
    {
        var camera = new ShowpieceCamera2D { MaxZoom = 9 };
        camera.SetContent(new Rect(0, 0, 400, 300), fitScale: 1);
        return camera;
    }

    private static Point At(Window window, Control control, double x, double y) =>
        control.TranslatePoint(new Point(x, y), window)!.Value;

    [AvaloniaFact]
    public void A_Press_Becomes_A_Drag_At_Five_Pixels()
    {
        var camera = Camera();
        var stage = new TestStage(camera);
        var window = TestHelpers.Show(stage);
        try
        {
            var start = At(window, stage, 200, 150);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(4, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(4, 0), MouseButton.Left);
            stage.Log.ShouldBe(new[] { "tap none" });

            var item = At(window, stage, 50, 150);
            window.MouseDown(item, MouseButton.Left);
            window.MouseUp(item, MouseButton.Left);
            stage.Log[^1].ShouldBe("tap item");

            stage.Log.Clear();
            var center = camera.Center;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(5, 0), RawInputModifiers.LeftMouseButton);
            stage.IsDragging.ShouldBeTrue();
            stage.Classes.Contains(ShowpieceStage.DraggingPseudoClass).ShouldBeTrue();
            window.MouseMove(start + new Point(-30, 0), RawInputModifiers.LeftMouseButton);
            // The default drag pans the 2D camera (rubber-banded: everything fits at zoom 1).
            camera.Center.X.ShouldBeGreaterThan(center.X);
            window.MouseUp(start + new Point(-30, 0), MouseButton.Left);
            stage.Log.ShouldBe(new[] { "drag", "drop" });
            stage.IsDragging.ShouldBeFalse();
            // Released past the bounds with motion off: back inside at once.
            camera.Center.X.ShouldBe(center.X, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Clicks_Reach_The_Hook()
    {
        var stage = new TestStage();
        var window = TestHelpers.Show(stage);
        try
        {
            var item = At(window, stage, 50, 150);
            window.MouseDown(item, MouseButton.Left);
            window.MouseUp(item, MouseButton.Left);
            window.MouseDown(item, MouseButton.Left);
            window.MouseUp(item, MouseButton.Left);
            stage.Log.ShouldContain("double item");
            stage.Log.Count(l => l == "tap item").ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Reports_Items_And_Leaving()
    {
        var stage = new TestStage();
        var window = TestHelpers.Show(stage);
        try
        {
            window.MouseMove(At(window, stage, 50, 50));
            stage.HoverItem.ShouldBe("item");
            stage.Cursor.ShouldNotBeNull();
            window.MouseMove(At(window, stage, 300, 50));
            stage.HoverItem.ShouldBeNull();
            stage.Hovers.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Wheel_Zooms_Per_Mode()
    {
        var stage = new TestStage(Camera());
        var bubbled = 0;
        var host = new Border { Child = stage };
        host.AddHandler(InputElement.PointerWheelChangedEvent, (_, _) => bubbled++);
        var window = TestHelpers.Show(host);
        try
        {
            var p = At(window, stage, 200, 150);
            window.MouseWheel(p, new Vector(0, 1));
            stage.Zooms.Count.ShouldBe(1);
            stage.Zooms[0].Factor.ShouldBe(1.2461, 1e-4);
            stage.Zooms[0].Source.ShouldBe(ShowpieceZoomSource.Wheel);
            stage.Zooms[0].Anchor!.Value.X.ShouldBe(200, 1e-6);
            bubbled.ShouldBe(0);

            stage.WheelMode = ShowpieceWheelMode.CtrlZoom;
            window.MouseWheel(p, new Vector(0, 1));
            stage.Zooms.Count.ShouldBe(1);
            bubbled.ShouldBe(1);
            window.MouseWheel(p, new Vector(0, 1), RawInputModifiers.Control);
            stage.Zooms.Count.ShouldBe(2);
            stage.Zooms[1].Factor.ShouldBe(Math.Exp(1.2), 1e-6);
            stage.Zooms[1].Source.ShouldBe(ShowpieceZoomSource.Pinch);

            stage.WheelMode = ShowpieceWheelMode.PassThrough;
            window.MouseWheel(p, new Vector(0, 1), RawInputModifiers.Control);
            stage.Zooms.Count.ShouldBe(2);
            bubbled.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Notched_Mode_Lets_Trackpad_Scrolling_Through()
    {
        var stage = new TestStage(Camera()) { WheelMode = ShowpieceWheelMode.NotchedZoom };
        var window = TestHelpers.Show(stage);
        try
        {
            var p = At(window, stage, 200, 150);
            window.MouseWheel(p, new Vector(0, 1));
            stage.Zooms.Count.ShouldBe(1);
            window.MouseWheel(p, new Vector(0, 0.3));
            stage.Zooms.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Zoom_Reset_And_Route_To_The_Block()
    {
        var camera = Camera();
        var stage = new TestStage(camera) { KeyZoomFactor = 1.5 };
        var window = TestHelpers.Show(stage);
        try
        {
            stage.Focus();
            window.KeyPressQwerty(PhysicalKey.Equal, RawInputModifiers.None);
            stage.Zooms[^1].Factor.ShouldBe(1.5);
            stage.Zooms[^1].Anchor.ShouldBeNull();
            stage.Zooms[^1].Source.ShouldBe(ShowpieceZoomSource.Keyboard);
            camera.Zoom.ShouldBe(1.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.Minus, RawInputModifiers.None);
            stage.Zooms[^1].Factor.ShouldBe(1 / 1.5, 1e-12);
            window.KeyPressQwerty(PhysicalKey.NumPadAdd, RawInputModifiers.None);
            camera.Zoom.ShouldBe(1.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
            stage.Log.ShouldContain("reset");
            camera.Zoom.ShouldBe(1, 1e-9);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            stage.Log[^1].ShouldBe("right");
            stage.Log.Clear();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            stage.Log.ShouldBe(new[] { "reset" });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Idle_Stages_Request_No_Frames()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var camera = Camera();
        var stage = new TestStage(camera);
        var window = TestHelpers.Show(stage, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var requests = clock.RequestCount;
            clock.Advance(TimeSpan.FromMilliseconds(500));
            clock.RequestCount.ShouldBe(requests);
            stage.IsLoopRunning.ShouldBeFalse();

            // A zoom runs frames until the camera rests, then draws its resting frame and stops asking.
            stage.ZoomFromButton(1.6);
            stage.IsLoopRunning.ShouldBeTrue();
            camera.IsMoving.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            camera.IsMoving.ShouldBeFalse();
            camera.Zoom.ShouldBe(1.6, 1e-6);
            stage.IsLoopRunning.ShouldBeFalse();
            stage.LoopStops.ShouldBeGreaterThanOrEqualTo(1);
            var settled = clock.RequestCount;
            clock.Advance(TimeSpan.FromMilliseconds(500));
            clock.RequestCount.ShouldBe(settled);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_Level_Reaches_The_Camera()
    {
        var camera = Camera();
        var stage = new TestStage(camera);
        var window = TestHelpers.Show(stage);
        try
        {
            stage.MotionLevel.ShouldBe(ArcMotionLevel.Off);
            camera.MotionLevel.ShouldBe(ArcMotionLevel.Off);
            stage.ZoomFromButton(2);
            camera.Zoom.ShouldBe(2, 1e-9);
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            camera.MotionLevel.ShouldBe(ArcMotionLevel.Reduced);
            stage.LastLevel.ShouldBe(ArcMotionLevel.Reduced);
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            camera.MotionLevel.ShouldBe(ArcMotionLevel.Full);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Theme_Changes_Clear_The_Text_Cache()
    {
        await TestHelpers.ApplyAsync(null);
        var stage = new TestStage();
        var window = TestHelpers.Show(stage);
        try
        {
            var text = stage.Cache.Get("Platform", Typeface.Default, 13);
            stage.Cache.Brush(Colors.Black, 0.5);
            stage.Cache.Count.ShouldBe(1);
            var changes = stage.ThemeChanges;
            await TestHelpers.ApplyAsync(TestHelpers.Dark);
            stage.ThemeChanges.ShouldBeGreaterThan(changes);
            stage.Cache.Count.ShouldBe(0);
            stage.Cache.BrushCount.ShouldBe(0);
            // Laid out again in the new theme's fonts, not served from before.
            stage.Cache.Get("Platform", Typeface.Default, 13).ShouldNotBeSameAs(text);
        }
        finally
        {
            window.Close();
            await TestHelpers.ApplyAsync(null);
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_A_Named_Custom_Control()
    {
        var stage = new TestStage();
        var window = TestHelpers.Show(stage);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(stage);
            peer.GetName().ShouldBe("A test stage");
            peer.GetLocalizedControlType().ShouldBe("zoomable map");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Custom);
            stage.Focusable.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}
