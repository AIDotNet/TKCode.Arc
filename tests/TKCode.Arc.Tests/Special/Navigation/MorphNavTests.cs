using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>The morph nav: its footprint, the panel's rows and columns, the keyboard model, choosing a link and automation.</summary>
public class MorphNavTests
{
    private static MorphNav Nav()
    {
        var nav = new MorphNav { Title = "Lantern", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var notebook = new MorphNavSection { Header = "Notebook" };
        notebook.Links.Add(new MorphNavLink { Text = "Today", Description = "This morning" });
        notebook.Links.Add(new MorphNavLink { Text = "Archive" });
        notebook.Links.Add(new MorphNavLink { Text = "Map" });
        var help = new MorphNavSection { Header = "Help" };
        help.Links.Add(new MorphNavLink { Text = "Guide" });
        help.Links.Add(new MorphNavLink { Text = "Contact" });
        nav.Sections.Add(notebook);
        nav.Sections.Add(help);
        return nav;
    }

    private static void OpenWithKeyboard(Window window, MorphNav nav)
    {
        nav.Pill!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Sections_sit_side_by_side_while_each_column_keeps_its_minimum_width()
    {
        MorphNavColumns.ColumnsFor(600, 3, 176, 12).ShouldBe(3);
        MorphNavColumns.ColumnsFor(400, 3, 176, 12).ShouldBe(2);
        MorphNavColumns.ColumnsFor(300, 3, 176, 12).ShouldBe(1);
        MorphNavColumns.ColumnsFor(900, 2, 176, 12).ShouldBe(2);
        MorphNavColumns.ColumnsFor(900, 1, 176, 12).ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_nav_takes_only_the_pills_room_also_while_open()
    {
        var nav = Nav();
        var window = TestHelpers.Show(nav);
        try
        {
            var closed = nav.DesiredSize;
            closed.Height.ShouldBeGreaterThan(30);
            closed.Width.ShouldBeLessThan(300);
            nav.Open();
            Dispatcher.UIThread.RunJobs();
            nav.DesiredSize.ShouldBe(closed);
            var surface = TestHelpers.Descendant<Border>(nav, MorphNav.SurfacePart);
            surface.Bounds.Height.ShouldBeGreaterThan(closed.Height);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_link_has_a_row_and_new_links_add_rows()
    {
        var nav = Nav();
        var window = TestHelpers.Show(nav);
        try
        {
            nav.Rows.Select(r => r.Link.Text).ShouldBe(["Today", "Archive", "Map", "Guide", "Contact"]);
            nav.Sections[1].Links.Add(new MorphNavLink { Text = "Status" });
            nav.Rows.Count.ShouldBe(6);
            nav.Rows[^1].Link.Text.ShouldBe("Status");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_keyboard_moves_through_the_links_and_enter_chooses_one()
    {
        var nav = Nav();
        MorphNavLink? chosen = null;
        nav.LinkInvoked += (_, e) => chosen = e.Link;
        var window = TestHelpers.Show(nav);
        try
        {
            OpenWithKeyboard(window, nav);
            nav.IsOpen.ShouldBeTrue();
            var focus = window.FocusManager!;
            focus.GetFocusedElement().ShouldBeSameAs(nav.Rows[0]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(nav.Rows[1]);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(nav.Rows[4]);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            chosen.ShouldBeSameAs(nav.Sections[0].Links[1]);
            nav.CurrentLink.ShouldBeSameAs(chosen);
            nav.IsOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(nav.Pill);
            AutomationProperties.GetName(nav.Pill!).ShouldBe("Open menu, Archive");
            nav.Rows[1].Classes.Contains(MorphNavLinkRow.CurrentPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_again_focuses_the_current_page_and_escape_closes()
    {
        var nav = Nav();
        nav.CurrentLink = nav.Sections[1].Links[0];
        var window = TestHelpers.Show(nav);
        try
        {
            OpenWithKeyboard(window, nav);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(nav.Rows[3]);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            nav.IsOpen.ShouldBeFalse();
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(nav.Pill);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Links_are_named_hyperlinks_that_can_be_invoked()
    {
        var nav = Nav();
        var window = TestHelpers.Show(nav);
        try
        {
            nav.Open();
            Dispatcher.UIThread.RunJobs();
            var peer = ControlAutomationPeer.CreatePeerForElement(nav.Rows[0]);
            peer.GetName().ShouldBe("Today");
            peer.GetHelpText().ShouldBe("This morning");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Hyperlink);
            ((IInvokeProvider)peer).Invoke();
            nav.CurrentLink.ShouldBeSameAs(nav.Rows[0].Link);
            nav.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_panel_never_exceeds_the_window_less_its_gutters()
    {
        var nav = Nav();
        nav.PanelWidth = 900;
        var window = TestHelpers.Show(nav);
        try
        {
            nav.EffectivePanelWidth(double.PositiveInfinity).ShouldBe(window.ClientSize.Width - 32, 0.5);
        }
        finally
        {
            window.Close();
        }
    }
}
