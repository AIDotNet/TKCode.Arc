using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>The liquid tab bar: selection, the keyboard model, tab stops, automation and the drop's stretch and settle.</summary>
public class LiquidTabBarTests
{
    private static LiquidTabBar Bar(int count = 4)
    {
        var bar = new LiquidTabBar();
        for (var i = 0; i < count; i++)
        {
            bar.Items.Add(new LiquidTabBarItem { Content = $"Tab {i}" });
        }
        return bar;
    }

    private static LiquidTabBarItem Item(LiquidTabBar bar, int index) => (LiquidTabBarItem)bar.ContainerFromIndex(index)!;

    private static Rect RectOf(LiquidTabBar bar, int index)
    {
        var blob = bar.Blob!;
        var item = Item(bar, index);
        var origin = item.TranslatePoint(default, blob.GetVisualParent()!)!.Value;
        return new Rect(origin, item.Bounds.Size);
    }

    [AvaloniaFact]
    public void The_first_tab_is_selected_and_the_drop_sits_under_it()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedIndex.ShouldBe(0);
            Item(bar, 0).IsSelected.ShouldBeTrue();
            var (left, right) = bar.Blob!.Edges;
            var rect = RectOf(bar, 0);
            left.ShouldBe(rect.Left, 0.01);
            right.ShouldBe(rect.Right, 0.01);
            bar.Blob.Stretch.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_keys_select_as_they_move_and_wrap_round_the_ends()
    {
        var bar = Bar(3);
        var window = TestHelpers.Show(bar);
        try
        {
            Item(bar, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(2);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(2);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(Item(bar, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_disabled_tab_is_skipped_and_cannot_be_pressed()
    {
        var bar = Bar(3);
        ((LiquidTabBarItem)bar.Items[1]!).IsEnabled = false;
        var window = TestHelpers.Show(bar);
        try
        {
            Item(bar, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(2);
            bar.OnItemPressed(Item(bar, 1));
            bar.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Only_the_selected_tab_is_a_tab_stop()
    {
        var bar = Bar(3);
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Item(bar, 0).IsTabStop.ShouldBeFalse();
            Item(bar, 1).IsTabStop.ShouldBeTrue();
            Item(bar, 2).IsTabStop.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_a_tab_selects_it_and_the_drop_jumps_there_without_motion()
    {
        var bar = Bar(3);
        var window = TestHelpers.Show(bar);
        try
        {
            bar.OnItemPressed(Item(bar, 2));
            Dispatcher.UIThread.RunJobs();
            bar.SelectedIndex.ShouldBe(2);
            var (left, right) = bar.Blob!.Edges;
            var rect = RectOf(bar, 2);
            left.ShouldBe(rect.Left, 0.01);
            right.ShouldBe(rect.Right, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_bar_is_a_tab_list_of_named_tabs()
    {
        var bar = Bar(2);
        var window = TestHelpers.Show(bar);
        try
        {
            ControlAutomationPeer.CreatePeerForElement(bar).GetAutomationControlType().ShouldBe(AutomationControlType.Tab);
            var peer = ControlAutomationPeer.CreatePeerForElement(Item(bar, 1));
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
            peer.GetName().ShouldBe("Tab 1");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_drop_stretches_toward_the_new_tab_and_settles_under_it()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar(4);
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            var start = RectOf(bar, 0);
            var target = RectOf(bar, 3);
            bar.SelectedIndex = 3;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(90));
            Dispatcher.UIThread.RunJobs();

            // The front has gone further than the back: the drop is longer than either tab.
            var (left, right) = bar.Blob!.Edges;
            (right - start.Right).ShouldBeGreaterThan(left - start.Left);
            (right - left).ShouldBeGreaterThan(Math.Max(start.Width, target.Width));
            bar.Blob.Stretch.ShouldBeGreaterThan(0);
            bar.Blob.IsMoving.ShouldBeTrue();

            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            (left, right) = bar.Blob.Edges;
            left.ShouldBe(target.Left, 0.5);
            right.ShouldBe(target.Right, 0.5);
            bar.Blob.Stretch.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_bar_with_no_tabs_draws_no_drop()
    {
        var bar = new LiquidTabBar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedIndex.ShouldBe(-1);
            bar.Blob!.IsPlaced.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
