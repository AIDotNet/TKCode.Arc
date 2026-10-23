using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Overlays;

public class PopupZoomTests
{
    private static (Window Window, StackPanel Root, Popover Popover, Border Probe) Open(double scale)
    {
        var probe = new Border { Name = "Probe", Width = 120, Height = 40, Background = Avalonia.Media.Brushes.Gray };
        var popover = new Popover { Trigger = new Button { Content = "Open" }, Content = probe };
        var root = new StackPanel { Margin = new Thickness(40), Children = { popover } };
        ArcZoom.SetScale(root, scale);
        var window = TestHelpers.Show(root);
        popover.IsOpen = true;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, root, popover, probe);
    }

    private static double WidthInWindow(Control probe, Window window)
    {
        var left = probe.TranslatePoint(new Point(0, 0), window)!.Value.X;
        var right = probe.TranslatePoint(new Point(probe.Bounds.Width, 0), window)!.Value.X;
        return right - left;
    }

    [AvaloniaTheory]
    [InlineData(1.0, 120)]
    [InlineData(1.5, 180)]
    [InlineData(2.0, 240)]
    public void Popover_Surface_Is_Laid_Out_At_The_Inherited_Scale(double scale, double expectedWidth)
    {
        var (window, _, popover, probe) = Open(scale);
        try
        {
            popover.Popup!.IsOpen.ShouldBeTrue();
            WidthInWindow(probe, window).ShouldBe(expectedWidth, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scale_Change_While_Open_Resizes_The_Surface()
    {
        var (window, root, _, probe) = Open(1.0);
        try
        {
            WidthInWindow(probe, window).ShouldBe(120, 0.5);
            ArcZoom.SetScale(root, 2.0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            WidthInWindow(probe, window).ShouldBe(240, 0.5);
            ArcZoom.SetScale(root, 1.0);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            WidthInWindow(probe, window).ShouldBe(120, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    // AtomUI's popup takes the shadow's corner radius from its direct child (the zoom host): a square shadow shows past
    // the surface's rounded corners.
    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void Shadow_Follows_The_Surface_Corner_Radius_At_The_Scale(double scale)
    {
        var (window, _, popover, _) = Open(scale);
        try
        {
            var surface = (TemplatedControl)popover.Popup!.Surface!;
            surface.CornerRadius.TopLeft.ShouldBeGreaterThan(0);
            var shadowRadius = ShadowContainerRadius(surface);
            shadowRadius.TopLeft.ShouldBe(surface.CornerRadius.TopLeft * scale, 0.01);
            shadowRadius.BottomRight.ShouldBe(surface.CornerRadius.BottomRight * scale, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shadow_Radius_Tracks_A_Surface_Radius_Change()
    {
        var (window, _, popover, _) = Open(1.0);
        try
        {
            var surface = (TemplatedControl)popover.Popup!.Surface!;
            surface.CornerRadius = new CornerRadius(3);
            ShadowContainerRadius(surface).ShouldBe(new CornerRadius(3));
        }
        finally
        {
            window.Close();
        }
    }

    private static CornerRadius ShadowContainerRadius(Control surface) =>
        surface.GetVisualAncestors().OfType<AvaloniaObject>()
               .First(v => v.GetType().Name == "ShadowsAwareContainer")
               .GetValue(Border.CornerRadiusProperty);

    [AvaloniaFact]
    public void Surface_Returns_The_Assigned_Child_And_Reassigning_Releases_It()
    {
        var first = new Border { Width = 10, Height = 10 };
        var popup = new ArcPopup { Child = first };
        popup.Surface.ShouldBeSameAs(first);
        popup.Child.ShouldNotBeSameAs(first);
        popup.Child.ShouldBeOfType<ArcZoomHost>();

        // The same surface can move to another popup (MorphSelect, TimePicker and PhoneInput reassign theirs).
        popup.Child = null;
        popup.Surface.ShouldBeNull();
        first.GetVisualParent().ShouldBeNull();
        var other = new ArcPopup { Child = first };
        other.Surface.ShouldBeSameAs(first);
    }
}
