using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Media;

public class ImageCompareTests
{
    private static (ImageCompare Compare, Window Window) Show(ImageCompare compare, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(compare, level);
        return (compare, window);
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static ImageCompareHandle Handle(ImageCompare compare) => TestHelpers.Descendant<ImageCompareHandle>(compare);

    private static Point HandleCenter(ImageCompare compare)
    {
        var handle = Handle(compare);
        var transform = (Avalonia.Media.TranslateTransform)handle.RenderTransform!;
        return new Point(transform.X + ImageCompareHandle.Extent / 2, transform.Y + ImageCompareHandle.Extent / 2);
    }

    [AvaloniaFact]
    public void Defaults_Follow_Arc()
    {
        var (compare, window) = Show(new ImageCompare());
        try
        {
            compare.Position.ShouldBe(50);
            compare.Orientation.ShouldBe(Orientation.Horizontal);
            compare.BeforeLabel.ShouldBe("Before");
            compare.AfterLabel.ShouldBe("After");
            compare.Label.ShouldBe("Before and after");
            // Full width, 3 / 2 frame.
            compare.Bounds.Width.ShouldBe(400);
            compare.Bounds.Height.ShouldBe(400 / 1.5, 0.5);
            var center = HandleCenter(compare);
            center.X.ShouldBe(200, 0.01);
            center.Y.ShouldBe(compare.Bounds.Height / 2, 0.01);
            Handle(compare).Capsule.ShouldBe(new Size(40, 40));
            var divider = TestHelpers.Descendant<ImageCompareDivider>(compare);
            divider.Angle.ShouldBe(0);
            divider.Pivot.X.ShouldBe(200, 0.01);
            var frame = TestHelpers.Descendant<Border>(compare, ImageCompare.FramePart);
            frame.CornerRadius.TopLeft.ShouldBe(26);
            compare.AfterShare.ShouldBe(50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Before_Layer_Is_Clipped_At_The_Divider_Without_Relayout()
    {
        var (compare, window) = Show(new ImageCompare { Position = 25 });
        try
        {
            var layer = TestHelpers.Descendant<Panel>(compare, ImageCompare.BeforeLayerPart);
            layer.Clip.ShouldNotBeNull();
            var bounds = layer.Bounds;
            layer.Clip!.FillContains(new Point(99, 50)).ShouldBeTrue();
            layer.Clip.FillContains(new Point(101, 50)).ShouldBeFalse();
            compare.Position = 75;
            Dispatcher.UIThread.RunJobs();
            layer.Bounds.ShouldBe(bounds);
            layer.Clip.FillContains(new Point(299, 50)).ShouldBeTrue();
            layer.Clip.FillContains(new Point(301, 50)).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_Step_Page_And_Reveal_One_Side()
    {
        var (compare, window) = Show(new ImageCompare());
        var events = new List<double>();
        compare.PositionChanged += (_, e) => events.Add(e.Position);
        try
        {
            Handle(compare).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            Press(window, PhysicalKey.ArrowRight);
            compare.Position.ShouldBe(51);
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            compare.Position.ShouldBe(61);
            Press(window, PhysicalKey.ArrowUp);
            compare.Position.ShouldBe(60);
            Press(window, PhysicalKey.PageDown);
            compare.Position.ShouldBe(70);
            Press(window, PhysicalKey.Home);
            compare.Position.ShouldBe(100);
            Press(window, PhysicalKey.ArrowRight);
            compare.Position.ShouldBe(100);
            Press(window, PhysicalKey.End);
            compare.Position.ShouldBe(0);
            compare.AfterShare.ShouldBe(100);
            events.ShouldBe([51, 61, 60, 70, 100, 0]);
            HandleCenter(compare).X.ShouldBeGreaterThan(20);
            Handle(compare).IsRingVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Vertical_Keys_Follow_The_Axis()
    {
        var (compare, window) = Show(new ImageCompare { Orientation = Orientation.Vertical });
        try
        {
            TestHelpers.Descendant<ImageCompareDivider>(compare).Angle.ShouldBe(90);
            Handle(compare).Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            compare.Position.ShouldBe(51);
            Press(window, PhysicalKey.ArrowLeft);
            compare.Position.ShouldBe(52);
            Press(window, PhysicalKey.ArrowRight);
            compare.Position.ShouldBe(51);
            var center = HandleCenter(compare);
            center.X.ShouldBe(200, 0.01);
            center.Y.ShouldBe(compare.Bounds.Height * 0.51, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_Moves_The_Divider_And_Commits_On_Release()
    {
        var (compare, window) = Show(new ImageCompare());
        var events = new List<double>();
        compare.PositionChanged += (_, e) => events.Add(e.Position);
        try
        {
            var y = 100;
            window.MouseDown(new Point(100, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            // A press on the image moves the divider there at once (no animation with motion off).
            compare.IsDragging.ShouldBeTrue();
            compare.Classes.Contains(ImageCompare.DraggingPseudoClass).ShouldBeTrue();
            compare.Position.ShouldBe(25);
            Handle(compare).Capsule.ShouldBe(new Size(56, 36));
            Handle(compare).IsFocused.ShouldBeTrue();
            Handle(compare).IsRingVisible.ShouldBeFalse();
            window.MouseMove(new Point(160, y), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            compare.Position.ShouldBe(40);
            // Past the edge the position stops at 100.
            window.MouseMove(new Point(460, y), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            compare.Position.ShouldBe(100);
            window.MouseMove(new Point(300, y), RawInputModifiers.LeftMouseButton);
            window.MouseUp(new Point(300, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            compare.IsDragging.ShouldBeFalse();
            compare.Position.ShouldBe(75);
            HandleCenter(compare).X.ShouldBe(300, 0.01);
            Handle(compare).Capsule.ShouldBe(new Size(40, 40));
            events[0].ShouldBe(25);
            events[^1].ShouldBe(75);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Grabbing_The_Handle_Keeps_The_Offset_And_Double_Click_Centers()
    {
        var (compare, window) = Show(new ImageCompare { Position = 30 });
        try
        {
            var center = HandleCenter(compare);
            var grab = center + new Point(6, 0);
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Point(40, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(grab + new Point(40, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            compare.Position.ShouldBe(40);

            var at = HandleCenter(compare);
            window.MouseDown(at, MouseButton.Left);
            window.MouseUp(at, MouseButton.Left);
            window.MouseDown(at, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            compare.Position.ShouldBe(50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Can_Be_Hidden_And_Fade_Near_The_Divider()
    {
        var (compare, window) = Show(new ImageCompare { Position = 50 });
        try
        {
            var before = TestHelpers.Descendant<Border>(compare, ImageCompare.BeforeChipPart);
            var after = TestHelpers.Descendant<Border>(compare, ImageCompare.AfterChipPart);
            before.IsVisible.ShouldBeTrue();
            before.Opacity.ShouldBe(1);
            after.Opacity.ShouldBe(1);
            compare.Position = 5;
            Dispatcher.UIThread.RunJobs();
            before.Opacity.ShouldBe(0);
            after.Opacity.ShouldBe(1);
            compare.ShowLabels = false;
            Dispatcher.UIThread.RunJobs();
            before.IsVisible.ShouldBeFalse();
            after.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handle_Is_A_Slider_For_Assistive_Technology()
    {
        var (compare, window) = Show(new ImageCompare { Position = 60, Label = "Retouch" });
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(Handle(compare));
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            peer.GetName().ShouldBe("Retouch");
            peer.GetItemStatus().ShouldBe("40% after");
            var range = peer.GetProvider<IRangeValueProvider>()!;
            range.Value.ShouldBe(40);
            range.SetValue(10);
            Dispatcher.UIThread.RunJobs();
            compare.Position.ShouldBe(90);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Springs_The_Divider_And_Swings_The_Orientation()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (compare, window) = Show(new ImageCompare(), ArcMotionLevel.Full);
        try
        {
            compare.Position = 80;
            Dispatcher.UIThread.RunJobs();
            HandleCenter(compare).X.ShouldBeLessThan(250);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            HandleCenter(compare).X.ShouldBe(320, 0.5);

            compare.Orientation = Orientation.Vertical;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            Dispatcher.UIThread.RunJobs();
            var mid = TestHelpers.Descendant<ImageCompareDivider>(compare).Angle;
            mid.ShouldBeGreaterThan(5);
            mid.ShouldBeLessThan(90);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<ImageCompareDivider>(compare).Angle.ShouldBe(90, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Math_Matches_Arc()
    {
        ImageCompareMath.Place(50, 400, reduced: false).ShouldBe(50);
        // Past an edge the divider gives at most 9px.
        var placed = ImageCompareMath.Place(150, 400, reduced: false);
        placed.ShouldBeGreaterThan(100);
        (placed - 100).ShouldBeLessThan(9.0 / 400 * 100);
        ImageCompareMath.Place(150, 400, reduced: true).ShouldBe(100);
        // The handle keeps 42px from the edge, giving up to 12px.
        ImageCompareMath.Soften(200, 400).ShouldBe(200);
        ImageCompareMath.Soften(0, 400).ShouldBeGreaterThan(30);
        ImageCompareMath.Soften(0, 400).ShouldBeLessThan(42);
        ImageCompareMath.KeyTarget(Key.Right, shift: false, vertical: false, latest: 49.6).ShouldBe(51);
        ImageCompareMath.KeyTarget(Key.Home, shift: false, vertical: false, latest: 20).ShouldBe(100);
        ImageCompareMath.KeyTarget(Key.A, shift: false, vertical: false, latest: 20).ShouldBeNull();
    }
}
