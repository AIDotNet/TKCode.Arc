using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Blocks;

/// <summary>Focus rings built on demand and pseudo-class styling look the same as the template parts and property selectors they replaced.</summary>
public class BlocksLazyPartsTests
{
    private static Border? Ring(Visual owner) =>
        owner.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Name == "PART_FocusRing");

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_bar_button_builds_its_ring_on_keyboard_focus_above_the_face()
    {
        var button = new SiteBrandButton();
        var other = new SiteMenuButton();
        var window = TestHelpers.Show(new StackPanel { Children = { button, other } });
        try
        {
            Settle();
            Ring(button).ShouldBeNull();

            button.Focus(NavigationMethod.Tab);
            Settle();
            var ring = Ring(button).ShouldNotBeNull();
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(new Thickness(-2));
            ring.CornerRadius.ShouldBe(new CornerRadius(14));
            ring.BorderThickness.ShouldBe(new Thickness(2));
            ((Panel)ring.Parent!).Children[^1].ShouldBeSameAs(ring);

            other.Focus(NavigationMethod.Tab);
            Settle();
            ring.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_capsule_nav_button_ring_follows_the_capsule_radius()
    {
        var button = new SiteNavButton();
        var window = TestHelpers.Show(button);
        try
        {
            button.Focus(NavigationMethod.Tab);
            Settle();
            Ring(button).ShouldNotBeNull().CornerRadius.ShouldBe(new CornerRadius(14));

            button.IsCapsule = true;
            Settle();
            var ring = button.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_FocusRing");
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(new Thickness(-2));
            ring.CornerRadius.TopLeft.ShouldBeGreaterThan(14);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_board_button_ring_sits_under_the_face_at_the_board_outset()
    {
        var button = new KanbanIconButton();
        var window = TestHelpers.Show(button);
        try
        {
            Settle();
            Ring(button).ShouldBeNull();
            button.Focus(NavigationMethod.Tab);
            Settle();
            var ring = Ring(button).ShouldNotBeNull();
            ring.IsVisible.ShouldBeTrue();
            ring.Margin.ShouldBe(new Thickness(-4));
            ((Panel)ring.Parent!).Children[0].ShouldBeSameAs(ring);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_drag_proxy_shows_its_cards_ring_without_focus()
    {
        var proxy = new KanbanCardView(new KanbanCard("FLD-1", "Card", "queued"), new KanbanBoard()) { IsProxy = true };
        var window = TestHelpers.Show(proxy);
        try
        {
            Settle();
            Ring(proxy).ShouldBeNull();
            proxy.SetFocusRing(true);
            Settle();
            Ring(proxy).ShouldNotBeNull().IsVisible.ShouldBeTrue();
            proxy.IsFocusRingVisible.ShouldBeTrue();
            proxy.SetFocusRing(false);
            Settle();
            Ring(proxy)!.IsVisible.ShouldBeFalse();
            proxy.IsFocusRingVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_footer_label_is_styled_by_kind_and_tone()
    {
        var label = new SiteFooterLabel { Text = "x", Kind = SiteFooterLabelKind.Message, Tone = SiteFooterMessageTone.Error };
        var window = TestHelpers.Show(label);
        try
        {
            Settle();
            TestHelpers.ColorOf(label.Foreground).ShouldBe(ArcTokens.GetColor(label, ArcDesignTokenKind.Danger));
            label.Tone = SiteFooterMessageTone.Success;
            Settle();
            TestHelpers.ColorOf(label.Foreground).ShouldBe(ArcTokens.GetColor(label, ArcDesignTokenKind.Success));
            label.Kind = SiteFooterLabelKind.Copyright;
            Settle();
            TestHelpers.ColorOf(label.Foreground).ShouldBe(ArcTokens.GetColor(label, ArcDesignTokenKind.TextMuted));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_notification_button_is_styled_by_kind_and_state()
    {
        var button = new NotificationCenterButton { Kind = NotificationCenterButtonKind.Trigger };
        var window = TestHelpers.Show(button);
        try
        {
            Settle();
            TestHelpers.ColorOf(button.Background).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.Surface));
            button.IsOn = true;
            Settle();
            TestHelpers.ColorOf(button.Background).ShouldBe(ArcTokens.GetColor(button, ArcDesignTokenKind.SurfaceMuted));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_sticky_summary_follows_the_scroll_without_moving_in_layout()
    {
        var block = new LiveCheckout();
        var scroll = new ScrollViewer { Content = block };
        var window = TestHelpers.Show(scroll);
        try
        {
            window.Width = 1100;
            window.Height = 300;
            Settle();
            var summary = TestHelpers.Descendant<Border>(block, "PART_Summary");
            var shell = TestHelpers.Descendant<LiveCheckoutShell>(block);

            scroll.Offset = new Vector(0, 100);
            Settle();
            window.UpdateLayout();
            Settle();

            // 24 below the top of the view, as the arranged position used to be; the layout slot stays at the shell's top.
            shell.StickyOffset.ShouldBe(124, 1);
            summary.Bounds.Y.ShouldBe(0, 0.01);
            summary.TranslatePoint(default, scroll)!.Value.Y.ShouldBe(24, 1);
        }
        finally
        {
            window.Close();
        }
    }
}
