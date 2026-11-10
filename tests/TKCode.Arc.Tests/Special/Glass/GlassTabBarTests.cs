using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Glass;

/// <summary>The glass tab bar: selection by tap, drag and keyboard, labels and badges, and the automation tree.</summary>
public class GlassTabBarTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static GlassTabBar Bar() => new()
    {
        Items =
        {
            new GlassTab { Content = "Home", Icon = ArcIcons.House },
            new GlassTab { Content = "Search", Icon = ArcIcons.Search },
            new GlassTab { Content = "Saved", Icon = ArcIcons.Bookmark, Badge = "" },
            new GlassTab { Content = "Inbox", Icon = ArcIcons.Inbox, Badge = "3" },
        }
    };

    private static GlassTab Tab(GlassTabBar bar, int index) => (GlassTab)bar.ContainerFromIndex(index)!;

    private static Point Center(Visual target, Window window) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;

    private static Rect TabRect(GlassTabBar bar, int index)
    {
        var tab = Tab(bar, index);
        return new Rect(tab.TranslatePoint(default, bar.Lens!)!.Value, tab.Bounds.Size);
    }

    [AvaloniaFact]
    public void Tabs_share_the_width_and_the_lens_starts_on_the_first()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        Pump();
        bar.SelectedIndex.ShouldBe(0);
        Tab(bar, 0).Bounds.Width.ShouldBe(Tab(bar, 3).Bounds.Width);
        Tab(bar, 0).Bounds.Width.ShouldBeGreaterThanOrEqualTo(64);
        bar.Lens!.CurrentRect.ShouldBe(TabRect(bar, 0));
        window.Close();
    }

    [AvaloniaFact]
    public void Tapping_a_tab_selects_it_and_arrows_move_between_tabs()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        Pump();
        var point = Center(Tab(bar, 2), window);
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
        bar.SelectedIndex.ShouldBe(2);
        bar.Lens!.CurrentRect.ShouldBe(TabRect(bar, 2));
        Tab(bar, 2).Classes.ShouldContain(":selected");

        Tab(bar, 2).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        bar.SelectedIndex.ShouldBe(3);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        bar.SelectedIndex.ShouldBe(0);
        Tab(bar, 0).IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Right_to_left_flips_the_arrow_keys()
    {
        var bar = Bar();
        bar.FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        var window = TestHelpers.Show(bar);
        Pump();
        Tab(bar, 0).Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        bar.SelectedIndex.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Dragging_the_lens_scrubs_across_the_tabs()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        Pump();
        var start = Center(Tab(bar, 0), window);
        var end = Center(Tab(bar, 2), window);
        window.MouseMove(start, RawInputModifiers.None);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X + 20, start.Y), RawInputModifiers.LeftMouseButton);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        Pump();
        Tab(bar, 2).IsUnderLens.ShouldBeTrue();
        Tab(bar, 0).IsUnderLens.ShouldBeFalse();
        window.MouseUp(end, MouseButton.Left);
        Pump();
        bar.SelectedIndex.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Badges_and_icon_only_bars()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        Pump();
        Tab(bar, 2).Classes.ShouldContain(":badge-dot");
        Tab(bar, 3).Classes.ShouldContain(":badge-count");
        Tab(bar, 0).Classes.ShouldNotContain(":badge-dot");
        var label = TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(Tab(bar, 0), "PART_Label");
        label.IsVisible.ShouldBeTrue();
        var tall = Tab(bar, 0).Bounds.Height;

        bar.ShowLabels = false;
        Pump();
        Tab(bar, 0).Classes.ShouldContain(":compact");
        label.IsVisible.ShouldBeFalse();
        Tab(bar, 0).Bounds.Height.ShouldBeLessThan(tall);
        window.Close();
    }

    [AvaloniaFact]
    public void Automation_names_tabs_with_their_badges()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        Pump();
        ControlAutomationPeer.CreatePeerForElement(bar).GetAutomationControlType().ShouldBe(AutomationControlType.Tab);
        var saved = ControlAutomationPeer.CreatePeerForElement(Tab(bar, 2));
        saved.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
        saved.GetName().ShouldBe("Saved, new");
        ControlAutomationPeer.CreatePeerForElement(Tab(bar, 3)).GetName().ShouldBe("Inbox, 3");
        ControlAutomationPeer.CreatePeerForElement(Tab(bar, 0)).GetName().ShouldBe("Home");
        window.Close();
    }

    [AvaloniaFact]
    public void With_motion_off_the_lens_moves_at_once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Off);
        Pump();
        bar.SelectedIndex = 3;
        bar.Lens!.CurrentRect.ShouldBe(TabRect(bar, 3));
        bar.Lens.IsMoving.ShouldBeFalse();
        window.Close();
    }
}
