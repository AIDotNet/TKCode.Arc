using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Layout;

/// <summary>CyclicPanel: slot sizes, seamless wraparound, programmatic and wheel scrolling, visible indices and motion levels.</summary>
public class CyclicPanelTests
{
    private static CyclicPanel Panel(int count, double length = 500, int visible = 5, Orientation orientation = Orientation.Vertical)
    {
        var panel = new CyclicPanel
        {
            Orientation = orientation,
            VisibleItemCount = visible,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (orientation == Orientation.Vertical)
        {
            panel.Width = 200;
            panel.Height = length;
        }
        else
        {
            panel.Width = length;
            panel.Height = 100;
        }
        for (var i = 0; i < count; i++)
        {
            panel.Children.Add(new Border { Tag = i });
        }
        return panel;
    }

    private static Window Show(Control content, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = new Window { Content = content, Width = 800, Height = 700 };
        ArcMotion.SetLevel(window, level);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Theme_Gives_The_Token_Defaults()
    {
        var panel = Panel(3);
        var window = Show(panel);
        try
        {
            panel.Spacing.ShouldBe(0);
            panel.AnimationDuration.ShouldBe(TimeSpan.FromMilliseconds(240));
            panel.ClipToBounds.ShouldBeTrue();
            panel.VisibleItemCount.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slots_Share_The_Length_And_The_Item_Past_The_End_Waits_Before_The_Start()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            panel.Children[0].Bounds.ShouldBe(new Rect(0, 0, 200, 100));
            panel.Children[4].Bounds.ShouldBe(new Rect(0, 400, 200, 100));
            // Exactly one slot before the start is also just past the end: it waits there, out of view.
            panel.Children[7].Bounds.ShouldBe(new Rect(0, 700, 200, 100));
            panel.VisibleItemIndices.ShouldBe([0, 1, 2, 3, 4]);
            panel.Offset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollToIndex_Wraps_Around_The_End()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            panel.ScrollToIndex(6, animated: false).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(6);
            panel.VisibleItemIndices.ShouldBe([6, 7, 0, 1, 2]);
            panel.Children[6].Bounds.Y.ShouldBe(0);
            panel.Children[0].Bounds.Y.ShouldBe(200);
            panel.Children[5].Bounds.Y.ShouldBe(700);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollToIndexCentered_Puts_The_Item_In_The_Middle_Slot()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            panel.ScrollToIndexCentered(5, animated: false).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            panel.VisibleItemIndices.ShouldBe([3, 4, 5, 6, 7]);
            panel.Children[5].Bounds.Y.ShouldBe(200);

            panel.ScrollToIndexCentered(0, animated: false);
            Dispatcher.UIThread.RunJobs();
            panel.VisibleItemIndices.ShouldBe([6, 7, 0, 1, 2]);
            panel.Offset.ShouldBe(6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Invalid_Or_Collapsed_Indexes_Are_Refused()
    {
        var panel = Panel(4);
        panel.Children[2].IsVisible = false;
        var window = Show(panel);
        try
        {
            panel.ScrollToIndex(-1).ShouldBeFalse();
            panel.ScrollToIndex(4).ShouldBeFalse();
            panel.ScrollToIndex(2).ShouldBeFalse();
            panel.Offset.ShouldBe(0);
            // The collapsed child is out of the loop: three items cycle.
            panel.ScrollToIndex(3, animated: false).ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Next_And_Previous_Cycle_Without_Bounds()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                panel.ScrollNext();
            }
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(3);
            for (var i = 0; i < 4; i++)
            {
                panel.ScrollPrevious();
            }
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(7);
            panel.VisibleItemIndices.ShouldBe([7, 0, 1, 2, 3]);
            // Many turns later the offset is still inside the loop.
            for (var i = 0; i < 1000; i++)
            {
                panel.ScrollNext();
            }
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Horizontal_Lays_Out_Left_To_Right()
    {
        var panel = Panel(6, length: 600, visible: 3, orientation: Orientation.Horizontal);
        var window = Show(panel);
        try
        {
            panel.Children[1].Bounds.ShouldBe(new Rect(200, 0, 200, 100));
            panel.ScrollToIndex(5, animated: false);
            Dispatcher.UIThread.RunJobs();
            panel.VisibleItemIndices.ShouldBe([5, 0, 1]);
            panel.Children[0].Bounds.ShouldBe(new Rect(200, 0, 200, 100));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Spacing_Separates_Slots_Within_The_Length()
    {
        var panel = Panel(6, length: 520);
        panel.Spacing = 5;
        var window = Show(panel);
        try
        {
            // (520 - 4 * 5) / 5 = 100 per slot, 105 per step.
            panel.Children[2].Bounds.ShouldBe(new Rect(0, 210, 200, 100));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Wheel_Notch_Scrolls_One_Item()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            window.MouseWheel(new Point(100, 250), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(1);
            window.MouseWheel(new Point(100, 250), new Vector(0, 2));
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Touchpad_Scroll_Follows_The_Fingers_Then_Snaps_To_An_Item()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            // 0.5 units = 25px = a quarter of an item.
            window.MouseWheel(new Point(100, 250), new Vector(0, -0.5));
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(0.25, 1e-9);
            panel.Children[0].Bounds.Y.ShouldBe(-25, 1e-9);
            // Partly visible items at both ends count.
            panel.VisibleItemIndices.ShouldBe([0, 1, 2, 3, 4, 5]);
            TestHelpers.PumpUntil(() => panel.Offset == 0, 3000).ShouldBeTrue();
            panel.VisibleItemIndices.ShouldBe([0, 1, 2, 3, 4]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Wheel_Can_Be_Turned_Off()
    {
        var panel = Panel(8);
        panel.IsWheelEnabled = false;
        var window = Show(panel);
        try
        {
            window.MouseWheel(new Point(100, 250), new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fewer_Items_Than_Slots_Show_Each_Item_Once()
    {
        var panel = Panel(3);
        var window = Show(panel);
        try
        {
            panel.VisibleItemIndices.ShouldBe([0, 1, 2]);
            panel.ScrollNext();
            Dispatcher.UIThread.RunJobs();
            panel.VisibleItemIndices.ShouldBe([1, 2, 0]);
            var rects = panel.Children.Select(c => c.Bounds).ToArray();
            rects.Select(r => r.Y).ShouldBeUnique();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_Items_Keeps_The_Offset_Inside_The_Loop()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            panel.ScrollToIndex(6, animated: false);
            Dispatcher.UIThread.RunJobs();
            panel.Children.RemoveRange(5, 3);
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBeInRange(0, 4.999);
            panel.VisibleItemIndices.ShouldAllBe(i => i >= 0 && i < 5);
            panel.Children.Clear();
            Dispatcher.UIThread.RunJobs();
            panel.Offset.ShouldBe(0);
            panel.VisibleItemIndices.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changing_VisibleItemCount_Resizes_The_Slots()
    {
        var panel = Panel(8);
        var window = Show(panel);
        try
        {
            panel.VisibleItemCount = 2;
            Dispatcher.UIThread.RunJobs();
            panel.Children[1].Bounds.ShouldBe(new Rect(0, 250, 200, 250));
            panel.VisibleItemIndices.ShouldBe([0, 1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Glides_The_Short_Way_Across_The_Seam()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var panel = Panel(8);
        var window = Show(panel, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(0.1));
            var seen = new List<double>();
            panel.PropertyChanged += (_, e) =>
            {
                if (e.Property == CyclicPanel.OffsetProperty)
                {
                    seen.Add(panel.Offset);
                }
            };
            panel.ScrollToIndex(7);
            panel.IsScrolling.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            panel.Offset.ShouldBeInRange(7.0001, 7.9999);
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            panel.IsScrolling.ShouldBeFalse();
            panel.Offset.ShouldBe(7);
            // Backwards from 0 to 7 by way of 7.x, never through 1..6.
            seen.ShouldAllBe(o => o == 0 || o >= 7);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_And_Disabled_Animation_Jump()
    {
        var panel = Panel(8);
        var window = Show(panel, ArcMotionLevel.Reduced);
        try
        {
            panel.ScrollNext();
            panel.IsScrolling.ShouldBeFalse();
            panel.Offset.ShouldBe(1);
            ArcMotion.SetLevel(window, ArcMotionLevel.Full);
            panel.IsAnimationEnabled = false;
            panel.ScrollNext();
            panel.IsScrolling.ShouldBeFalse();
            panel.Offset.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Works_As_An_ItemsPanel()
    {
        var items = new ItemsControl
        {
            Width = 200,
            Height = 300,
            ItemsSource = Enumerable.Range(0, 10).ToArray(),
            ItemsPanel = new FuncTemplate<Panel?>(() => new CyclicPanel { VisibleItemCount = 3 }),
            ItemTemplate = new FuncDataTemplate<int>((i, _) => new TextBlock { Text = $"Item {i}" }),
        };
        var window = Show(items);
        try
        {
            var panel = items.GetVisualDescendants().OfType<CyclicPanel>().Single();
            panel.Children.Count.ShouldBe(10);
            panel.VisibleItemIndices.ShouldBe([0, 1, 2]);
            panel.ScrollToIndexCentered(0, animated: false);
            Dispatcher.UIThread.RunJobs();
            panel.VisibleItemIndices.ShouldBe([9, 0, 1]);
            panel.Children[0].Bounds.ShouldBe(new Rect(0, 100, 200, 100));
        }
        finally
        {
            window.Close();
        }
    }
}
