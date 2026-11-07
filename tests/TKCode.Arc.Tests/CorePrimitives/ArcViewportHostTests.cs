using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.CorePrimitives;

/// <summary>
/// <see cref="ArcViewportHost"/> and the window-sized half of <see cref="ArcViewport"/>: what a control inside a
/// narrow-viewport preview treats as its window (design/engineering/gallery-mobile-preview.md, section 4).
/// </summary>
public class ArcViewportHostTests
{
    private static Window Show(Control content, double width = 800, double height = 700)
    {
        var window = TestHelpers.Show(content);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Without_A_Host_The_Viewport_Is_The_Window()
    {
        var probe = new Border();
        var window = Show(probe);
        try
        {
            ArcViewport.FindHost(probe).ShouldBeNull();
            ArcViewport.GetSize(probe).ShouldBe(window.ClientSize);
            ArcViewport.GetBounds(probe).ShouldBe(new Rect(window.ClientSize));
            ArcViewport.IsCompact(probe).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
        var detached = new Border();
        ArcViewport.GetSize(detached).ShouldBeNull();
        ArcViewport.GetBounds(detached).ShouldBeNull();
        ArcViewport.IsCompact(detached).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Nearest_Host_Is_The_Viewport_And_Narrow_Ones_Are_Compact()
    {
        var probe = new Border();
        var inner = new ArcViewportHost { Width = 390, Height = 500, Children = { probe } };
        var outer = new ArcViewportHost { Width = 700, Height = 600, Children = { inner } };
        var window = Show(outer);
        try
        {
            ArcViewport.FindHost(probe).ShouldBeSameAs(inner);
            ArcViewport.FindHost(inner).ShouldBeSameAs(inner);
            ArcViewport.GetSize(probe).ShouldBe(new Size(390, 500));
            ArcViewport.IsCompact(probe).ShouldBeTrue();
            ArcViewport.IsCompact(outer).ShouldBeFalse();
            // A modal opened inside mounts on the host's own layer, as for any ArcOverlayHost.
            ArcOverlayLayer.Find(probe).ShouldBeSameAs(inner.Layer);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compact_Width_Is_Configurable()
    {
        var probe = new Border();
        var window = Show(new ArcViewportHost { Width = 700, Height = 400, Children = { probe } });
        try
        {
            ArcViewport.CompactWidth.ShouldBe(ArcViewport.DefaultCompactWidth);
            ArcViewport.IsCompact(probe).ShouldBeFalse();
            ArcViewport.CompactWidth = 768;
            ArcViewport.IsCompact(probe).ShouldBeTrue();
        }
        finally
        {
            ArcViewport.CompactWidth = ArcViewport.DefaultCompactWidth;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bounds_Are_In_Window_Coordinates_After_A_Scale()
    {
        var host = new ArcViewportHost { Width = 390, Height = 844, Children = { new Border() } };
        var scaled = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(0.5, 0.5),
            Margin = new Thickness(40, 20, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = host
        };
        var window = Show(scaled);
        try
        {
            var bounds = ArcViewport.GetBounds(host.Children[0]).ShouldNotBeNull();
            bounds.X.ShouldBe(40, 0.5);
            bounds.Y.ShouldBe(20, 0.5);
            bounds.Width.ShouldBe(195, 0.5);
            bounds.Height.ShouldBe(422, 0.5);
            // The size is the viewport's own (unscaled) size: what the content lays out in.
            ArcViewport.GetSize(host).ShouldBe(new Size(390, 844));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Observe_Reports_The_Size_On_Attach_On_Resize_And_Under_Another_Host()
    {
        var probe = new Border();
        var first = new ArcViewportHost { Width = 390, Height = 500 };
        var second = new ArcViewportHost { Width = 600, Height = 400 };
        var window = Show(new StackPanel { Children = { first, second } });
        var seen = new List<Size>();
        using var subscription = ArcViewport.Observe(probe, seen.Add);
        try
        {
            seen.ShouldBeEmpty();
            first.Children.Add(probe);
            Dispatcher.UIThread.RunJobs();
            seen.ShouldBe([new Size(390, 500)]);

            first.Width = 360;
            Dispatcher.UIThread.RunJobs();
            seen.Last().ShouldBe(new Size(360, 500));

            first.Children.Remove(probe);
            second.Children.Add(probe);
            Dispatcher.UIThread.RunJobs();
            seen.Last().ShouldBe(new Size(600, 400));

            var count = seen.Count;
            subscription.Dispose();
            second.Width = 500;
            Dispatcher.UIThread.RunJobs();
            seen.Count.ShouldBe(count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Select_Near_The_Bottom_Of_A_Host_Opens_Its_Menu_Inside_The_Host()
    {
        var select = new Select
        {
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(12),
            Items =
            {
                new SelectItem { Content = "North America", Value = "na" },
                new SelectItem { Content = "Europe", Value = "eu" },
                new SelectItem { Content = "Asia Pacific", Value = "apac" },
                new SelectItem { Content = "Africa", Value = "af" }
            }
        };
        // The window has plenty of room below the host; the host does not, so the menu must flip above the field.
        var host = new ArcViewportHost { Width = 390, Height = 260, VerticalAlignment = VerticalAlignment.Top, Children = { select } };
        var window = Show(host, 800, 900);
        try
        {
            select.Open();
            Dispatcher.UIThread.RunJobs();
            var popup = TestHelpers.Descendant<ArcPopup>(select);
            var surface = popup.Surface.ShouldNotBeNull();
            var menu = new Rect(surface.Bounds.Size).TransformToAABB(surface.TransformToVisual(window)!.Value);
            var viewport = ArcViewport.GetBounds(select).ShouldNotBeNull();
            menu.Bottom.ShouldBeLessThanOrEqualTo(viewport.Bottom + 0.5);
            menu.Top.ShouldBeGreaterThanOrEqualTo(viewport.Top - 0.5);
            var field = new Rect(select.Bounds.Size).TransformToAABB(select.TransformToVisual(window)!.Value);
            menu.Bottom.ShouldBeLessThanOrEqualTo(field.Top + 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
