using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The liquid tab bar: layout (44px slots plus the open label), the lens sliding as one shape, label timing, the lean
/// toward a hovered tab, dragging and flicking the lens, the label modes, badges, the keyboard model and automation.
/// </summary>
public class LiquidTabBarTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static LiquidTabBar Bar(int count = 4, LiquidTabBarLabels labels = LiquidTabBarLabels.Active)
    {
        var bar = new LiquidTabBar { Labels = labels };
        Geometry[] glyphs = [ArcIcons.House, ArcIcons.Bookmark, ArcIcons.MessageCircle, ArcIcons.User, ArcIcons.Bell];
        for (var i = 0; i < count; i++)
        {
            bar.Items.Add(new LiquidTabBarItem { Value = $"v{i}", Label = $"Tab {i}", Glyph = glyphs[i % glyphs.Length] });
        }
        return bar;
    }

    private static LiquidTabBarItem Item(LiquidTabBar bar, int index) => (LiquidTabBarItem)bar.ContainerFromIndex(index)!;

    private static Rect RectOf(LiquidTabBar bar, int index)
    {
        var item = Item(bar, index);
        return new Rect(item.TranslatePoint(default, bar)!.Value, item.Bounds.Size);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static void Advance(ManualFrameClock clock, Window window, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Settle(window);
    }

    [AvaloniaFact]
    public void Tabs_are_44px_slots_and_the_selected_one_opens_to_fit_its_label()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedIndex.ShouldBe(0);
            var open = Item(bar, 0);
            open.IsSelected.ShouldBeTrue();
            open.Bounds.Height.ShouldBe(40);
            // Label at 40px, 14px after it: the open width is the rounded label width plus 54.
            open.Bounds.Width.ShouldBe(open.OpenWidth, 0.01);
            (open.OpenWidth - 54).ShouldBe(Math.Round(open.OpenWidth - 54), 1e-9);
            open.OpenWidth.ShouldBeGreaterThan(54);
            for (var i = 1; i < 4; i++)
            {
                Item(bar, i).Bounds.Width.ShouldBe(44);
            }
            // 4px padding, 2px gaps, a 48px pill track that is exactly as wide as the tabs.
            RectOf(bar, 0).X.ShouldBe(4, 0.01);
            RectOf(bar, 1).X.ShouldBe(4 + open.Bounds.Width + 2, 0.01);
            bar.Bounds.Height.ShouldBe(48);
            bar.Bounds.Width.ShouldBe(8 + open.Bounds.Width + 3 * 44 + 3 * 2, 0.01);
            bar.TrackRect.Width.ShouldBe(bar.Bounds.Width, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_lens_covers_the_selected_tab_exactly()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.LensRect.ShouldBe(RectOf(bar, 0));
            bar.SelectedIndex = 2;
            Settle(window);
            bar.LensRect.ShouldBe(RectOf(bar, 2));
            Item(bar, 0).Bounds.Width.ShouldBe(44);
            Item(bar, 2).Bounds.Width.ShouldBeGreaterThan(44);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_lens_slides_as_one_shape_on_the_spring_and_lands_on_the_new_tab()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            bar.SelectedIndex = 3;
            Settle(window);
            Advance(clock, window, 0.12);
            var (left, right) = bar.LensEdges;
            // Both edges sit at the same fractional tab: the lens is one tab wide wherever it is, never stretched.
            left.ShouldBe(right, 1e-9);
            left.ShouldBeInRange(0.2, 2.8);
            var lens = bar.LensRect;
            lens.Height.ShouldBe(40);
            lens.Width.ShouldBeLessThanOrEqualTo(Math.Max(Item(bar, 0).Bounds.Width, Item(bar, 3).Bounds.Width) + 0.01);

            Advance(clock, window, 1.5);
            bar.LensEdges.Left.ShouldBe(3, 0.001);
            bar.LensRect.Left.ShouldBe(RectOf(bar, 3).Left, 0.05);
            bar.LensRect.Width.ShouldBe(RectOf(bar, 3).Width, 0.05);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_old_label_goes_at_once_and_the_new_one_shows_only_as_its_tab_finishes_opening()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            Item(bar, 0).LabelOpacity.ShouldBe(1);
            bar.SelectedIndex = 1;
            Settle(window);
            Advance(clock, window, 0.07);
            Item(bar, 0).LabelOpacity.ShouldBe(0);
            Item(bar, 1).Openness.ShouldBeLessThan(0.9);
            Item(bar, 1).LabelOpacity.ShouldBe(0);
            Advance(clock, window, 1.5);
            Item(bar, 1).Openness.ShouldBe(1, 0.001);
            Item(bar, 1).LabelOpacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_retarget_keeps_each_tab_moving_with_its_speed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            bar.SelectedIndex = 1;
            Settle(window);
            Advance(clock, window, 0.1);
            var before = Item(bar, 1).Openness;
            bar.SelectedIndex = 2;
            Settle(window);
            Advance(clock, window, 1.0 / 60);
            // Tab 1 was opening: it carries on a little before it turns back.
            Item(bar, 1).Openness.ShouldBeGreaterThan(before);
            Advance(clock, window, 1.5);
            Item(bar, 1).Openness.ShouldBe(0, 0.001);
            Item(bar, 2).Openness.ShouldBe(1, 0.001);
            bar.LensEdges.Left.ShouldBe(2, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hovering_a_tab_leans_the_lens_toward_it_with_the_mouse_only()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            var rest = bar.LensRect;
            window.MouseMove(Item(bar, 3).TranslatePoint(new Point(22, 20), window)!.Value, RawInputModifiers.None);
            Advance(clock, window, 1);
            bar.Lean.ShouldBe(0.14, 0.001);
            // The facing edge reaches 14% of the way to the neighbour's edge, however far the hovered tab is.
            var lens = bar.LensRect;
            lens.Left.ShouldBe(rest.Left, 0.01);
            lens.Right.ShouldBe(rest.Right + 0.14 * (RectOf(bar, 1).Right - rest.Right), 0.05);
            window.MouseMove(Item(bar, 0).TranslatePoint(new Point(22, 20), window)!.Value, RawInputModifiers.None);
            Advance(clock, window, 1);
            bar.Lean.ShouldBe(0, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_jumps_and_never_leans()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar();
        var window = TestHelpers.Show(bar, ArcMotionLevel.Reduced);
        try
        {
            window.MouseMove(Item(bar, 2).TranslatePoint(new Point(22, 20), window)!.Value, RawInputModifiers.None);
            Advance(clock, window, 0.5);
            bar.Lean.ShouldBe(0);
            bar.SelectedIndex = 2;
            Settle(window);
            bar.LensEdges.Left.ShouldBe(2);
            Item(bar, 2).Openness.ShouldBe(1);
            Item(bar, 0).LabelOpacity.ShouldBe(0);
            bar.LensRect.ShouldBe(RectOf(bar, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_click_selects_on_release()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            var point = Item(bar, 2).TranslatePoint(new Point(22, 20), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            bar.SelectedIndex.ShouldBe(0);
            window.MouseUp(point, MouseButton.Left);
            bar.SelectedIndex.ShouldBe(2);
            bar.SelectedValue.ShouldBe("v2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_the_lens_follows_the_pointer_and_lands_on_the_nearest_tab()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar(4, LiquidTabBarLabels.None);
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            var start = Item(bar, 0).TranslatePoint(new Point(22, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            Advance(clock, window, 0.4);
            bar.Squeeze.ShouldBe(1, 0.01);
            bar.LensRect.Height.ShouldBe(36, 0.05);
            // 44px tabs, 46px apart: 92px is two tabs along.
            for (var dx = 10; dx <= 92; dx += 10)
            {
                window.MouseMove(start + new Vector(dx, 0), RawInputModifiers.LeftMouseButton);
                Advance(clock, window, 0.05);
            }
            window.MouseMove(start + new Vector(92, 0), RawInputModifiers.LeftMouseButton);
            bar.IsDragging.ShouldBeTrue();
            Advance(clock, window, 0.5);
            var (left, right) = bar.LensEdges;
            left.ShouldBe(2, 0.01);
            right.ShouldBe(2, 0.01);
            bar.SelectedIndex.ShouldBe(0);
            // A pause before the release drops the momentum: it lands where it is.
            Advance(clock, window, 0.2);
            Thread.Sleep(120);
            window.MouseUp(start + new Vector(92, 0), MouseButton.Left);
            bar.IsDragging.ShouldBeFalse();
            bar.SelectedIndex.ShouldBe(2);
            Advance(clock, window, 1.5);
            bar.Squeeze.ShouldBe(0, 0.01);
            bar.LensRect.ShouldBe(RectOf(bar, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_lens_resists_past_the_ends()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var bar = Bar(3, LiquidTabBarLabels.None);
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            var start = Item(bar, 0).TranslatePoint(new Point(22, 20), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            for (var dx = -10; dx >= -80; dx -= 10)
            {
                window.MouseMove(start + new Vector(dx, 0), RawInputModifiers.LeftMouseButton);
                Advance(clock, window, 0.03);
            }
            Advance(clock, window, 0.5);
            var left = bar.LensEdges.Left;
            // 80px past the start would be 1.74 tabs unresisted; the band holds it within half a tab.
            left.ShouldBeLessThan(0);
            left.ShouldBeGreaterThan(-0.45);
            window.MouseUp(start + new Vector(-80, 0), MouseButton.Left);
            bar.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_release_lands_where_its_momentum_reaches_and_a_flick_moves_at_least_one_tab()
    {
        var bar = Bar(5, LiquidTabBarLabels.None);
        var window = TestHelpers.Show(bar);
        try
        {
            // 44px tabs: the centers are 46px apart.
            bar.LandingIndex(0.3, 0).ShouldBe(0);
            bar.LandingIndex(0.6, 0).ShouldBe(1);
            bar.LandingIndex(0.39, 380).ShouldBe(2);
            bar.LandingIndex(0.2, 1500).ShouldBe(4);
            bar.LandingIndex(0.05, 100).ShouldBe(0);
            // Fast enough to count as a flick: at least the next tab, even when the projection falls short.
            bar.LandingIndex(-0.3, 210).ShouldBe(1);
            bar.LandingIndex(-0.3, 150).ShouldBe(0);
            bar.LandingIndex(0, -400).ShouldBe(0);
            ((LiquidTabBarItem)bar.Items[1]!).IsEnabled = false;
            Settle(window);
            bar.LandingIndex(-0.3, 210).ShouldBe(2);
            bar.LandingIndex(0.9, 0).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Always_opens_every_tab_and_None_closes_them_all()
    {
        var bar = Bar(3, LiquidTabBarLabels.Always);
        var window = TestHelpers.Show(bar);
        try
        {
            for (var i = 0; i < 3; i++)
            {
                Item(bar, i).Bounds.Width.ShouldBeGreaterThan(44);
                Item(bar, i).LabelOpacity.ShouldBe(1);
            }
            bar.Labels = LiquidTabBarLabels.None;
            Settle(window);
            for (var i = 0; i < 3; i++)
            {
                Item(bar, i).Bounds.Width.ShouldBe(44);
                Item(bar, i).LabelOpacity.ShouldBe(0);
            }
            bar.Bounds.Width.ShouldBe(8 + 3 * 44 + 2 * 2, 0.01);
            bar.LensRect.ShouldBe(RectOf(bar, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_tab_without_a_glyph_always_shows_its_label()
    {
        var bar = new LiquidTabBar();
        bar.Items.Add(new LiquidTabBarItem { Content = "Day" });
        bar.Items.Add(new LiquidTabBarItem { Content = "Week" });
        var window = TestHelpers.Show(bar);
        try
        {
            Item(bar, 1).IsTextTab.ShouldBeTrue();
            Item(bar, 1).LabelText.ShouldBe("Week");
            Item(bar, 1).LabelOpacity.ShouldBe(1);
            Item(bar, 1).Bounds.Width.ShouldBe(Item(bar, 1).OpenWidth, 0.01);
            Item(bar, 1).Bounds.Width.ShouldBeGreaterThan(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selected_value_selects_the_tab_with_that_value_both_ways()
    {
        var bar = Bar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedValue = "v3";
            bar.SelectedIndex.ShouldBe(3);
            bar.SelectedIndex = 1;
            bar.SelectedValue.ShouldBe("v1");
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
    public void A_disabled_tab_is_skipped_and_cannot_be_clicked()
    {
        var bar = Bar(3);
        ((LiquidTabBarItem)bar.Items[1]!).IsEnabled = false;
        var window = TestHelpers.Show(bar);
        try
        {
            Item(bar, 0).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            bar.SelectedIndex.ShouldBe(2);
            bar.OnItemClicked(Item(bar, 1));
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
    public void The_bar_is_a_named_tab_list_and_a_tab_is_named_with_its_badge()
    {
        var bar = Bar(3);
        Item0(bar).Badge = 2;
        Item0(bar).BadgeLabel = "2 unread";
        ((LiquidTabBarItem)bar.Items[1]!).Badge = 5;
        var window = TestHelpers.Show(bar);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(bar);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Tab);
            peer.GetName().ShouldBe("Sections");
            var tab = ControlAutomationPeer.CreatePeerForElement(Item(bar, 0));
            tab.GetAutomationControlType().ShouldBe(AutomationControlType.TabItem);
            tab.GetName().ShouldBe("Tab 0, 2 unread");
            ControlAutomationPeer.CreatePeerForElement(Item(bar, 1)).GetName().ShouldBe("Tab 1, 5");
            ControlAutomationPeer.CreatePeerForElement(Item(bar, 2)).GetName().ShouldBe("Tab 2");
            AutomationProperties.SetName(bar, "App sections");
            peer.GetName().ShouldBe("App sections");
        }
        finally
        {
            window.Close();
        }

        static LiquidTabBarItem Item0(LiquidTabBar b) => (LiquidTabBarItem)b.Items[0]!;
    }

    [AvaloniaFact]
    public void A_badged_tab_opens_wider_to_clear_its_badge()
    {
        var bar = Bar(2, LiquidTabBarLabels.Always);
        var window = TestHelpers.Show(bar);
        try
        {
            var plain = Item(bar, 1).Bounds.Width;
            Item(bar, 1).Badge = 4;
            Settle(window);
            Item(bar, 1).Bounds.Width.ShouldBe(plain + 6, 0.01);
            Item(bar, 1).HasBadge.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Badge_digits_roll_to_a_new_count_and_the_badge_shrinks_away_at_zero()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var roll = new LiquidTabBarBadgeRoll();
        roll.SetValue(9, animate: false).ShouldBeFalse();
        roll.PositionOf(0).ShouldBe(9);
        roll.SetValue(12, animate: true).ShouldBeTrue();
        roll.Count.ShouldBe(2);
        roll.Progress = 0.5;
        // The ones column keeps its place value: it turns from 9 down to 2; the new tens column turns up from 0.
        roll.PositionOf(1).ShouldBe(5.5);
        roll.PositionOf(0).ShouldBe(0.5);

        var bar = Bar(2);
        var window = TestHelpers.Show(bar, ArcMotionLevel.Full);
        try
        {
            var tab = Item(bar, 1);
            tab.Badge = 3;
            Advance(clock, window, 1);
            tab.HasBadge.ShouldBeTrue();
            tab.Badge = 0;
            Advance(clock, window, 0.05);
            tab.HasBadge.ShouldBeTrue();
            Advance(clock, window, 1);
            tab.HasBadge.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_bar_with_no_tabs_draws_no_lens()
    {
        var bar = new LiquidTabBar();
        var window = TestHelpers.Show(bar);
        try
        {
            bar.SelectedIndex.ShouldBe(-1);
            bar.LensRect.ShouldBe(default);
            bar.Bounds.Width.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Edges_interpolate_between_tabs_and_pointer_positions_map_through_the_centers()
    {
        double[] lefts = [4, 99, 145, 191];
        LiquidTabBarMetrics.EdgeAt(lefts, 0).ShouldBe(4);
        LiquidTabBarMetrics.EdgeAt(lefts, 0.5).ShouldBe(51.5);
        LiquidTabBarMetrics.EdgeAt(lefts, 2.25).ShouldBe(156.5);
        LiquidTabBarMetrics.EdgeAt(lefts, 3.5).ShouldBe(214);
        LiquidTabBarMetrics.EdgeAt(lefts, -0.5).ShouldBe(-43.5);
        // Centers of a 93px open tab and three 44px tabs (Arc's preview layout).
        double[] centers = [50.5, 121, 167, 213];
        LiquidTabBarMetrics.IndexAt(centers, 50.5 + 10).ShouldBe(10 / 70.5, 1e-9);
        LiquidTabBarMetrics.IndexAt(centers, 121 + 23).ShouldBe(1.5, 1e-9);
        LiquidTabBarMetrics.IndexAt(centers, 213 + 46).ShouldBe(4, 1e-9);
        LiquidTabBarMetrics.PitchAt(centers, 0.4).ShouldBe(70.5);
        LiquidTabBarMetrics.PitchAt(centers, 2.4).ShouldBe(46);
        LiquidTabBarMetrics.Resist(1.5, 4, centers).ShouldBe(1.5);
        // 8.28px past the last tab of a 46px pitch: about 3.7px shown (Arc's measured band).
        ((LiquidTabBarMetrics.Resist(3 + 8.28 / 46, 4, centers) - 3) * 46).ShouldBe(3.74, 0.05);
        LiquidTabBarMetrics.LabelOpacity(0.95, 1).ShouldBe(0.5, 1e-9);
        LiquidTabBarMetrics.LabelOpacity(1, 0.2).ShouldBe(0.2);
    }
}
