using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Layout;

/// <summary>
/// Sticky: translation and IsStuck while scrolling (with Offset), release on scrolling back, the parent's bottom edge
/// (push away), StuckChanged, turning it off, ZIndex raise and restore, reflow, and enabling after layout.
/// </summary>
public class StickyTests
{
    private const double SpacerHeight = 100;
    private const double HeaderHeight = 40;

    private sealed record Page(Window Window, ScrollViewer Scroller, StackPanel Content, Border Spacer, Border Header);

    /// <summary>A 300px tall viewer: a 100px spacer, a 40px header, then 1000px of content.</summary>
    private static Page ShowPage(bool enable = true, Action<Border>? setup = null)
    {
        var spacer = new Border { Height = SpacerHeight };
        var header = new Border { Height = HeaderHeight };
        setup?.Invoke(header);
        if (enable)
        {
            Sticky.SetIsEnabled(header, true);
        }
        var content = new StackPanel { Children = { spacer, header, new Border { Height = 1000 } } };
        var scroller = new ScrollViewer { Content = content };
        var window = TestHelpers.Show(scroller);
        Pump(window);
        return new Page(window, scroller, content, spacer, header);
    }

    private static void Pump(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void ScrollTo(Window window, ScrollViewer scroller, double y)
    {
        scroller.Offset = new Vector(0, y);
        Pump(window);
    }

    private static double Translation(Control element) => element.RenderTransform is TranslateTransform t ? t.Y : 0;

    [AvaloniaFact]
    public void Not_Stuck_Before_The_Element_Reaches_The_Top()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 60);
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
            Translation(page.Header).ShouldBe(0);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolled_Past_The_Element_It_Is_Held_At_The_Top()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            Translation(page.Header).ShouldBe(50);
            page.Header.Bounds.Top.ShouldBe(SpacerHeight);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Offset_Keeps_A_Gap_Under_The_Viewport_Top()
    {
        var page = ShowPage(setup: h => Sticky.SetOffset(h, 8));
        try
        {
            ScrollTo(page.Window, page.Scroller, 95);
            Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            Translation(page.Header).ShouldBe(3);
            ScrollTo(page.Window, page.Scroller, 150);
            Translation(page.Header).ShouldBe(58);
            Sticky.SetOffset(page.Header, 20);
            Translation(page.Header).ShouldBe(70);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_Back_Releases_The_Element()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 300);
            Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            ScrollTo(page.Window, page.Scroller, 0);
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
            Translation(page.Header).ShouldBe(0);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Translation_Does_Not_Feed_Back_Into_The_Viewport()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            for (var i = 0; i < 5; i++)
            {
                page.Content.InvalidateMeasure();
                Pump(page.Window);
                Translation(page.Header).ShouldBe(50);
                Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            }
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Next_Group_Pushes_The_Previous_Header_Away()
    {
        // Two groups of a 30px header and 200px of rows; the header never leaves its group.
        Border Header()
        {
            var header = new Border { Height = 30 };
            Sticky.SetIsEnabled(header, true);
            return header;
        }
        var first = Header();
        var second = Header();
        var firstRows = new Border { Height = 200 };
        var content = new StackPanel
        {
            Children =
            {
                new StackPanel { Children = { first, firstRows } },
                new StackPanel { Children = { second, new Border { Height = 600 } } },
            }
        };
        var scroller = new ScrollViewer { Content = content };
        var window = TestHelpers.Show(scroller);
        try
        {
            Pump(window);
            ScrollTo(window, scroller, 120);
            Translation(first).ShouldBe(120);
            Sticky.GetIsStuck(second).ShouldBeFalse();

            // At 220 the first group ends 10px into the viewport: its header is pushed up by the second.
            ScrollTo(window, scroller, 220);
            Translation(first).ShouldBe(200);
            Sticky.GetIsStuck(first).ShouldBeTrue();
            Sticky.GetIsStuck(second).ShouldBeFalse();

            ScrollTo(window, scroller, 300);
            Translation(first).ShouldBe(200);
            Translation(second).ShouldBe(70);
            Sticky.GetIsStuck(second).ShouldBeTrue();

            // The first group grows: its header can travel further (its own viewport did not change).
            ScrollTo(window, scroller, 220);
            firstRows.Height = 300;
            Pump(window);
            Translation(first).ShouldBe(220);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void StuckChanged_Is_Raised_Once_Per_Change_And_Bubbles()
    {
        var page = ShowPage();
        try
        {
            var seen = new List<bool>();
            page.Content.AddHandler(Sticky.StuckChangedEvent, (_, e) =>
            {
                e.Source.ShouldBe(page.Header);
                seen.Add(e.IsStuck);
            });
            var direct = 0;
            Sticky.AddStuckChangedHandler(page.Header, CountDirect);
            ScrollTo(page.Window, page.Scroller, 150);
            ScrollTo(page.Window, page.Scroller, 250);
            ScrollTo(page.Window, page.Scroller, 0);
            seen.ShouldBe([true, false]);
            direct.ShouldBe(2);

            Sticky.RemoveStuckChangedHandler(page.Header, CountDirect);
            ScrollTo(page.Window, page.Scroller, 150);
            direct.ShouldBe(2);
            seen.Count.ShouldBe(3);

            void CountDirect(object? sender, StickyStuckChangedEventArgs e) => direct++;
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_It_Off_Clears_The_Translation_And_State()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            var events = new List<bool>();
            Sticky.AddStuckChangedHandler(page.Header, (_, e) => events.Add(e.IsStuck));

            Sticky.SetIsEnabled(page.Header, false);
            page.Header.RenderTransform.ShouldBeNull();
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
            page.Header.ZIndex.ShouldBe(0);
            events.ShouldBe([false]);

            ScrollTo(page.Window, page.Scroller, 250);
            page.Header.RenderTransform.ShouldBeNull();
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void ZIndex_Is_Raised_While_Stuck_And_Cleared_After()
    {
        var page = ShowPage(setup: h => Sticky.SetZIndex(h, 5));
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            page.Header.ZIndex.ShouldBe(5);
            ScrollTo(page.Window, page.Scroller, 0);
            page.Header.ZIndex.ShouldBe(0);
            page.Header.GetDiagnostic(Visual.ZIndexProperty).Priority.ShouldNotBe(BindingPriority.LocalValue);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Local_ZIndex_Is_Restored()
    {
        var page = ShowPage(setup: h =>
        {
            h.ZIndex = 3;
            Sticky.SetZIndex(h, 7);
        });
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            page.Header.ZIndex.ShouldBe(7);
            ScrollTo(page.Window, page.Scroller, 0);
            page.Header.ZIndex.ShouldBe(3);
            page.Header.GetDiagnostic(Visual.ZIndexProperty).Priority.ShouldBe(BindingPriority.LocalValue);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Styled_ZIndex_Comes_Back_After_Release()
    {
        var page = ShowPage(setup: h =>
        {
            h.Classes.Add("raised");
            h.Styles.Add(new Style(x => x.OfType<Border>().Class("raised"))
            {
                Setters = { new Setter(Visual.ZIndexProperty, 2) }
            });
            Sticky.SetZIndex(h, 9);
        });
        try
        {
            page.Header.ZIndex.ShouldBe(2);
            ScrollTo(page.Window, page.Scroller, 150);
            page.Header.ZIndex.ShouldBe(9);
            ScrollTo(page.Window, page.Scroller, 0);
            page.Header.ZIndex.ShouldBe(2);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Content_Above_Growing_Is_Picked_Up()
    {
        var page = ShowPage();
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            Translation(page.Header).ShouldBe(50);
            page.Spacer.Height = 130;
            Pump(page.Window);
            Translation(page.Header).ShouldBe(20);
            page.Spacer.Height = 200;
            Pump(page.Window);
            Translation(page.Header).ShouldBe(0);
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Enabled_After_Layout_Starts_In_The_Right_State()
    {
        var page = ShowPage(enable: false);
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            Sticky.SetIsEnabled(page.Header, true);
            Sticky.GetIsStuck(page.Header).ShouldBeTrue();
            Translation(page.Header).ShouldBe(50);
            Pump(page.Window);
            Translation(page.Header).ShouldBe(50);
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Element_With_Its_Own_RenderTransform_Is_Left_Alone()
    {
        var own = new ScaleTransform(1, 1);
        var page = ShowPage(setup: h => h.RenderTransform = own);
        try
        {
            ScrollTo(page.Window, page.Scroller, 150);
            page.Header.RenderTransform.ShouldBeSameAs(own);
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
        }
        finally
        {
            page.Window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Scrolling_Does_Not_Move_It()
    {
        var page = ShowPage(setup: h => h.Width = 1200);
        try
        {
            page.Scroller.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            Pump(page.Window);
            page.Scroller.Offset = new Vector(300, 0);
            Pump(page.Window);
            Sticky.GetIsStuck(page.Header).ShouldBeFalse();
            Translation(page.Header).ShouldBe(0);
            ((TranslateTransform)page.Header.RenderTransform!).X.ShouldBe(0);
        }
        finally
        {
            page.Window.Close();
        }
    }
}
