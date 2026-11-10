using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Publishing;

/// <summary>
/// The mega menu morph: placement and swap rules, triggers and plain links, opening and closing, the morph between items
/// (one panel, the leaving page, the spring), the keyboard model, link activation, motion levels and accessibility.
/// </summary>
public class MegaMenuMorphTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static MegaMenuMorph Menu()
    {
        var menu = new MegaMenuMorph { OpenDelay = TimeSpan.Zero, CloseDelay = TimeSpan.Zero };
        menu.Items.Add(new MegaMenuMorphItem
        {
            Header = "Product",
            Content = LinkPanel(260, "Boards", "Docs")
        });
        menu.Items.Add(new MegaMenuMorphItem
        {
            Header = "Teams",
            Content = LinkPanel(400, "Design", "Engineering", "Operations")
        });
        menu.Items.Add(new MegaMenuMorphItem { Header = "Pricing" });
        return menu;
    }

    private static StackPanel LinkPanel(double width, params string[] titles)
    {
        var panel = new StackPanel { Width = width, Spacing = 4 };
        foreach (var title in titles)
        {
            panel.Children.Add(new MegaMenuMorphLink { Title = title, Description = title + " description" });
        }
        return panel;
    }

    private static (MegaMenuMorph Menu, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var menu = Menu();
        var host = new Panel { Children = { menu } };
        menu.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        menu.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = TestHelpers.Show(host, level);
        window.Width = 1000;
        window.Height = 700;
        Pump();
        return (menu, window);
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        var frames = (int)Math.Round(seconds * 60);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
        Pump(1);
    }

    // ---- Pure rules ----

    [Fact]
    public void Panel_is_centred_under_its_trigger_and_slides_to_stay_in_range()
    {
        MegaMenuMorphMath.PanelRect(200, new Size(100, 80), 0, 1000).ShouldBe(new Rect(150, 0, 100, 80));
        // Near the right edge it slides left; near the left edge it slides right.
        MegaMenuMorphMath.PanelRect(980, new Size(100, 80), 0, 1000).ShouldBe(new Rect(900, 0, 100, 80));
        MegaMenuMorphMath.PanelRect(10, new Size(100, 80), -20, 1000).ShouldBe(new Rect(-20, 0, 100, 80));
        // Wider than the range: it starts at the left of the range.
        MegaMenuMorphMath.PanelRect(100, new Size(500, 80), 0, 300).X.ShouldBe(0);
    }

    [Fact]
    public void Direction_follows_the_item_order()
    {
        MegaMenuMorphMath.Direction(0, 2).ShouldBe(1);
        MegaMenuMorphMath.Direction(2, 1).ShouldBe(-1);
        MegaMenuMorphMath.Direction(-1, 1).ShouldBe(0);
        MegaMenuMorphMath.Direction(1, 1).ShouldBe(0);
    }

    [Fact]
    public void Content_comes_in_from_the_direction_of_travel_and_leaves_the_other_way()
    {
        MegaMenuMorphMath.Incoming(0, 1, 36).ShouldBe((36.0, 0.0));
        MegaMenuMorphMath.Incoming(1, 1, 36).ShouldBe((0.0, 1.0));
        MegaMenuMorphMath.Incoming(0.15, -1, 36).Opacity.ShouldBe(0, 1e-9);
        MegaMenuMorphMath.Outgoing(0, 1, 36).ShouldBe((0.0, 1.0));
        MegaMenuMorphMath.Outgoing(0.55, 1, 36).Opacity.ShouldBe(0, 1e-9);
        MegaMenuMorphMath.Outgoing(1, -1, 36).Offset.ShouldBe(36);
        // Without travel only the opacity changes.
        MegaMenuMorphMath.Incoming(0.5, 1, 0).Offset.ShouldBe(0);
    }

    // ---- Triggers ----

    [AvaloniaFact]
    public void Builds_one_trigger_per_item_and_plain_items_have_no_chevron()
    {
        var (menu, window) = Open();
        menu.Triggers.Count.ShouldBe(3);
        menu.Triggers[0].HasPanel.ShouldBeTrue();
        menu.Triggers[2].HasPanel.ShouldBeFalse();
        menu.Triggers[0].Classes.Contains(":has-panel").ShouldBeTrue();
        menu.Triggers[2].Classes.Contains(":has-panel").ShouldBeFalse();

        menu.Items.Add(new MegaMenuMorphItem { Header = "Blog" });
        menu.Triggers.Count.ShouldBe(4);
        menu.Items[0].Header = "Products";
        menu.Triggers[0].Header.ShouldBe("Products");
        window.Close();
    }

    [AvaloniaFact]
    public void Triggers_report_expanded_and_collapsed_and_plain_items_are_links()
    {
        var (menu, window) = Open();
        var peer = ControlAutomationPeer.CreatePeerForElement(menu.Triggers[0]);
        var expand = (IExpandCollapseProvider)peer;
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
        expand.Expand();
        Pump();
        menu.OpenIndex.ShouldBe(0);
        expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
        ((IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(menu.Triggers[2])).ExpandCollapseState
            .ShouldBe(ExpandCollapseState.LeafNode);
        ControlAutomationPeer.CreatePeerForElement(menu.Triggers[2]).GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
        window.Close();
    }

    // ---- Open and close ----

    [AvaloniaFact]
    public void Opening_shows_the_items_page_in_the_popup_centred_under_its_trigger()
    {
        var (menu, window) = Open();
        menu.OpenIndex = 0;
        Pump();
        var stage = menu.Stage!;
        stage.IsShown.ShouldBeTrue();
        stage.CurrentItem.ShouldBe(menu.Items[0]);
        menu.Classes.Contains(":open").ShouldBeTrue();
        menu.Triggers[0].IsOpen.ShouldBeTrue();
        // 260 content + 2 × 12 padding.
        stage.TargetRect.Width.ShouldBe(284, 0.5);
        var trigger = menu.Triggers[0];
        var center = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, 0), menu)!.Value.X;
        stage.TargetRect.Center.X.ShouldBe(center, 0.5);
        stage.PanelRect.ShouldBe(stage.TargetRect);

        menu.Close();
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        stage.IsShown.ShouldBeFalse();
        menu.Triggers[0].IsOpen.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void A_plain_item_or_an_index_out_of_range_cannot_be_open()
    {
        var (menu, window) = Open();
        menu.OpenIndex = 2;
        menu.OpenIndex.ShouldBe(-1);
        menu.OpenIndex = 9;
        menu.OpenIndex.ShouldBe(-1);
        menu.Open(2);
        menu.OpenIndex.ShouldBe(-1);
        window.Close();
    }

    [AvaloniaFact]
    public void An_index_set_before_the_items_opens_once_the_menu_is_shown()
    {
        // XAML sets attributes before it adds the children.
        var menu = new MegaMenuMorph { OpenIndex = 1 };
        foreach (var item in Menu().Items.ToArray())
        {
            item.Owner = null;
            menu.Items.Add(item);
        }
        var window = TestHelpers.Show(new Panel { Children = { menu } });
        Pump();
        menu.OpenIndex.ShouldBe(1);
        menu.Stage!.IsShown.ShouldBeTrue();
        menu.Triggers[1].IsOpen.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Removing_the_open_item_closes_the_panel()
    {
        var (menu, window) = Open();
        menu.Open(1);
        Pump();
        menu.Items.RemoveAt(1);
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        window.Close();
    }

    // ---- Morph ----

    [AvaloniaFact]
    public void Moving_to_a_neighbour_morphs_one_panel_and_swaps_the_pages()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (menu, window) = Open(ArcMotionLevel.Full);
        menu.OpenIndex = 0;
        Pump();
        Run(clock, 0.5);
        var stage = menu.Stage!;
        var first = stage.PanelRect;

        menu.OpenIndex = 1;
        Pump();
        stage.LeavingPage.ShouldNotBeNull();
        stage.LeavingPage!.Item.ShouldBe(menu.Items[0]);
        stage.CurrentPage!.Item.ShouldBe(menu.Items[1]);
        var target = stage.TargetRect;
        target.Width.ShouldBe(424, 0.5);

        Run(clock, 0.12);
        // Mid-morph: the panel is between the two items, wider than the first and narrower than the second.
        stage.PanelRect.Width.ShouldBeGreaterThan(first.Width);
        stage.PanelRect.Width.ShouldBeLessThan(target.Width);
        // The new page comes in from the right (later item), the old one leaves to the left.
        stage.CurrentPage.Shift.X.ShouldBeGreaterThan(0);
        stage.LeavingPage!.Shift.X.ShouldBeLessThan(0);

        Run(clock, 1);
        stage.LeavingPage.ShouldBeNull();
        stage.PanelRect.X.ShouldBe(target.X, 0.5);
        stage.PanelRect.Width.ShouldBe(target.Width, 0.5);
        stage.CurrentPage.Shift.X.ShouldBe(0, 0.01);
        stage.CurrentPage.Opacity.ShouldBe(1, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_jumps_the_panel_and_only_fades_the_pages()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (menu, window) = Open(ArcMotionLevel.Reduced);
        menu.OpenIndex = 0;
        Pump();
        Run(clock, 0.5);
        menu.OpenIndex = 1;
        Pump();
        var stage = menu.Stage!;
        stage.PanelRect.ShouldBe(stage.TargetRect);
        Run(clock, 0.05);
        stage.CurrentPage!.Shift.X.ShouldBe(0);
        stage.LeavingPage?.Shift.X.ShouldBe(0);
        Run(clock, 0.5);
        stage.LeavingPage.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Closing_fades_out_then_closes_the_popup_and_reopening_mid_fade_keeps_it()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (menu, window) = Open(ArcMotionLevel.Full);
        menu.OpenIndex = 0;
        Pump();
        Run(clock, 0.5);
        var stage = menu.Stage!;
        stage.Opacity.ShouldBe(1, 0.01);

        menu.Close();
        Pump();
        Run(clock, 0.05);
        stage.IsShown.ShouldBeTrue();
        stage.Opacity.ShouldBeLessThan(1);
        menu.OpenIndex = 0;
        Pump();
        Run(clock, 0.5);
        stage.IsShown.ShouldBeTrue();
        stage.Opacity.ShouldBe(1, 0.01);

        menu.Close();
        Pump();
        Run(clock, 0.5);
        stage.IsShown.ShouldBeFalse();
        window.Close();
    }

    // ---- Keyboard ----

    [AvaloniaFact]
    public void Arrow_keys_move_between_triggers_and_carry_an_open_panel()
    {
        var (menu, window) = Open();
        menu.Triggers[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump();
        menu.Triggers[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        menu.Triggers[2].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        menu.Triggers[0].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        menu.Triggers[2].IsFocused.ShouldBeTrue();

        menu.Open(0);
        Pump();
        menu.Triggers[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(1);
        // A plain link closes it.
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        window.Close();
    }

    [AvaloniaFact]
    public void Down_moves_into_the_panel_Up_and_Down_walk_the_links_and_Escape_returns()
    {
        var (menu, window) = Open();
        menu.Triggers[1].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(1);
        var links = menu.Stage!.CurrentPage!.GetVisualDescendants().OfType<MegaMenuMorphLink>().ToArray();
        links.Length.ShouldBe(3);
        links[0].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        links[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        links[2].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        links[1].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        menu.Triggers[1].IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_toggles_a_panel()
    {
        var (menu, window) = Open();
        menu.Triggers[0].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        window.Close();
    }

    // ---- Links ----

    [AvaloniaFact]
    public void Choosing_a_link_raises_LinkInvoked_with_its_item_and_closes()
    {
        var (menu, window) = Open();
        MegaMenuMorphLinkInvokedEventArgs? args = null;
        menu.LinkInvoked += (_, e) => args = e;
        menu.Open(1, focusPanel: true);
        Pump();
        var link = menu.Stage!.CurrentPage!.GetVisualDescendants().OfType<MegaMenuMorphLink>().First();
        link.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        args.ShouldNotBeNull();
        args!.Item.ShouldBe(menu.Items[1]);
        args.Link.ShouldBe(link);
        args.Title.ShouldBe("Design");
        menu.OpenIndex.ShouldBe(-1);
        menu.Triggers[1].IsFocused.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_plain_item_raises_LinkInvoked_without_a_link()
    {
        var (menu, window) = Open();
        MegaMenuMorphLinkInvokedEventArgs? args = null;
        menu.LinkInvoked += (_, e) => args = e;
        menu.Triggers[2].Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
        args.ShouldNotBeNull();
        args!.Link.ShouldBeNull();
        args.Title.ShouldBe("Pricing");
        window.Close();
    }

    [AvaloniaFact]
    public void Link_names_its_title_and_describes_itself()
    {
        var link = new MegaMenuMorphLink { Title = "Boards", Description = "Plan work", Icon = ArcIcons.LayoutGrid };
        var window = TestHelpers.Show(link);
        Avalonia.Automation.AutomationProperties.GetName(link).ShouldBe("Boards");
        Avalonia.Automation.AutomationProperties.GetHelpText(link).ShouldBe("Plan work");
        link.Classes.Contains(":has-icon").ShouldBeTrue();
        link.Classes.Contains(":has-description").ShouldBeTrue();
        ControlAutomationPeer.CreatePeerForElement(link).GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
        window.Close();
    }

    [AvaloniaFact]
    public void Menu_is_a_named_navigation_landmark()
    {
        var (menu, window) = Open();
        menu.Label = "Site";
        Avalonia.Automation.AutomationProperties.GetName(menu).ShouldBe("Site");
        Avalonia.Automation.AutomationProperties.GetLandmarkType(menu).ShouldBe(AutomationLandmarkType.Navigation);
        window.Close();
    }

    [AvaloniaFact]
    public void Detaching_closes_the_panel()
    {
        var (menu, window) = Open();
        menu.Open(0);
        Pump();
        ((Panel)menu.Parent!).Children.Remove(menu);
        Pump();
        menu.OpenIndex.ShouldBe(-1);
        menu.Stage!.IsShown.ShouldBeFalse();
        window.Close();
    }
}
