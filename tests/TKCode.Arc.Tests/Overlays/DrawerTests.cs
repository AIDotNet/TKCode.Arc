using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class DrawerTests
{
    [AvaloniaFact]
    public void Right_Drawer_Is_A_Full_Height_Side_Panel()
    {
        var drawer = new Drawer { Title = "Filters", Content = "Body" };
        var window = TestHelpers.Show(new StackPanel { Children = { drawer } });
        try
        {
            window.Width = 1000;
            window.Height = 600;
            Dispatcher.UIThread.RunJobs();
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = drawer.Surface.ShouldBeOfType<DrawerSurface>();
            surface.Side.ShouldBe(DrawerSide.Right);
            surface.Bounds.Width.ShouldBe(480, 0.5);
            surface.Bounds.Height.ShouldBe(600, 0.5);
            surface.Bounds.Right.ShouldBe(1000, 0.5);
            surface.CornerRadius.ShouldBe(new CornerRadius(22, 0, 0, 22));
            surface.BorderThickness.ShouldBe(new Thickness(1, 1, 0, 1));
            Avalonia.Automation.AutomationProperties.GetName(surface.CloseButton!).ShouldBe("Close drawer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Viewport_Nearly_Fills_The_Width_And_Tightens()
    {
        var drawer = new Drawer { Title = "Filters", Side = DrawerSide.Left };
        var window = TestHelpers.Show(new StackPanel { Children = { drawer } });
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = (DrawerSurface)drawer.Surface!;
            // 400px window < 40rem: width = viewport − 12.
            surface.Bounds.Width.ShouldBe(388, 0.5);
            surface.Bounds.Left.ShouldBe(0, 0.5);
            surface.Classes.Contains(DrawerSurface.NarrowPseudoClass).ShouldBeTrue();
            surface.CornerRadius.ShouldBe(new CornerRadius(0, 18, 18, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bottom_Drawer_Fits_Its_Content_And_Shows_The_Grabber()
    {
        var drawer = new Drawer { Title = "Share", Side = DrawerSide.Bottom, Content = new Border { Height = 40 } };
        var window = TestHelpers.Show(new StackPanel { Children = { drawer } });
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            var surface = (DrawerSurface)drawer.Surface!;
            surface.Bounds.Bottom.ShouldBe(300, 0.5);
            surface.Bounds.Height.ShouldBeLessThan(300);
            surface.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Grabber").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Header_Past_A_Third_Closes_With_A_Gesture()
    {
        var drawer = new Drawer { Title = "Filters" };
        var window = TestHelpers.Show(new StackPanel { Children = { drawer } }, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            var presence = drawer.Presence!;
            presence.PanelOffset.ShouldBe(0, 0.5);
            var surface = (DrawerSurface)drawer.Surface!;
            var header = surface.Header!;
            var start = header.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(20, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(80, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            drawer.IsDragging.ShouldBeTrue();
            presence.PanelOffset.ShouldBe(80, 0.5);
            // Pulling away from the edge decays with a square root.
            window.MouseMove(start + new Vector(-16, 0), RawInputModifiers.LeftMouseButton);
            presence.PanelOffset.ShouldBe(-4, 0.01);
            window.MouseMove(start + new Vector(200, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Vector(200, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            drawer.IsOpen.ShouldBeFalse();
            drawer.LastCloseReason.ShouldBe(DismissReason.Gesture);
            presence.State.ShouldBe(ModalState.Closing);
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            presence.State.ShouldBe(ModalState.Closed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Short_Drag_Springs_Back()
    {
        var drawer = new Drawer { Title = "Filters" };
        var window = TestHelpers.Show(new StackPanel { Children = { drawer } }, ArcMotionLevel.Full);
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        try
        {
            drawer.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1.5));
            Dispatcher.UIThread.RunJobs();
            var header = ((DrawerSurface)drawer.Surface!).Header!;
            var start = header.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(10, 0), RawInputModifiers.LeftMouseButton);
            window.MouseMove(start + new Vector(30, 0), RawInputModifiers.LeftMouseButton);
            // A pause before release drops the momentum (no flick).
            Thread.Sleep(120);
            window.MouseUp(start + new Vector(30, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            drawer.IsOpen.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            drawer.Presence!.PanelOffset.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
