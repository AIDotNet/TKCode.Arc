using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Keyboard (spec 11, without the Ctrl+Tab switcher) and the tab menu with its move commands.</summary>
public class DockKeyboardTests
{
    private static (DockLayout Layout, Window Window) ShowSpec()
    {
        var layout = DockLayoutViewTests.LoadSpec();
        return (layout, DockLayoutViewTests.Show(layout, 1400, 700));
    }

    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static DockTab Tab(DockLayout layout, string id) => DockLayoutViewTests.TabOf(layout, id);

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [AvaloniaFact]
    public void Arrows_Home_And_End_Move_Focus_Between_Tabs_And_Activate()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var doc = Pane(layout, "doc:readme").Group!;
            doc.Panes.Add(DockSample.Document("doc:three", "Three.md"));
            Dispatcher.UIThread.RunJobs();
            Tab(layout, "doc:readme").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Focused(window).ShouldBe(Tab(layout, "doc:app"));
            layout.ActivePane.ShouldBe(Pane(layout, "doc:app"));
            doc.ActivePane.ShouldBe(Pane(layout, "doc:app"));

            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane!.Id.ShouldBe("doc:three");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane!.Id.ShouldBe("doc:readme");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane!.Id.ShouldBe("doc:three");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.ActivePane!.Id.ShouldBe("doc:readme");
            TestHelpers.IsFocusRingVisible(Tab(layout, "doc:readme")).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Shift_Arrows_Reorder_The_Focused_Tab()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var group = Pane(layout, "doc:readme").Group!;
            var tab = Tab(layout, "doc:readme");
            tab.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            group.Panes.Select(p => p.Id).ShouldBe(["doc:app", "doc:readme"]);
            Focused(window).ShouldBe(tab);
            layout.LastAnnouncement.ShouldBe("README.md, tab 2 of 2");
            // At the end nothing moves; the command key on macOS works the same.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Control | RawInputModifiers.Shift);
            group.Panes.Select(p => p.Id).ShouldBe(["doc:app", "doc:readme"]);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Meta | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            group.Panes.Select(p => p.Id).ShouldBe(["doc:readme", "doc:app"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Delete_On_A_Tab_And_Ctrl_F4_In_A_Pane_Close_The_Active_Pane()
    {
        var (layout, window) = ShowSpec();
        try
        {
            Tab(layout, "doc:readme").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.Workspace.FindPane("doc:readme").ShouldBeNull();
            // Focus goes to the tab of the pane that took its place.
            Focused(window).ShouldBe(Tab(layout, "doc:app"));
            layout.ActivePane!.Id.ShouldBe("doc:app");

            var box = Pane(layout, "explorer").Content.ShouldBeOfType<TextBox>();
            box.Focus();
            window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            layout.Workspace.FindPane("explorer").ShouldBeNull();
            layout.ActivePane!.Id.ShouldBe("search");

            Tab(layout, "search").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Meta);
            Dispatcher.UIThread.RunJobs();
            layout.Workspace.FindPane("search").ShouldBeNull();
            // The emptied tool group went; the neighbouring group's pane is active.
            layout.ActivePane.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panes_That_Cannot_Close_Ignore_The_Close_Keys()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            readme.CanClose = false;
            Dispatcher.UIThread.RunJobs();
            Tab(layout, "doc:readme").ShowsClose.ShouldBeFalse();
            Tab(layout, "doc:readme").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            readme.Workspace.ShouldBe(layout.Workspace);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_F10_Opens_The_Tab_Menu_On_Its_First_Row()
    {
        var (layout, window) = ShowSpec();
        try
        {
            Tab(layout, "doc:readme").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            var menu = layout.Menu.ShouldNotBeNull();
            menu.IsOpen.ShouldBeTrue();
            menu.HighlightedEntry.ShouldBe(menu.Items[0]);
            menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Close", "Close others", "Close all", "Float", "Move to", "Maximize"]);
            menu.Close();
            Dispatcher.UIThread.RunJobs();

            // A tool's menu: no Close others / Close all, but Dock to.
            Tab(layout, "terminal").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ContextMenu, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Close", "Float", "Auto hide", "Move to", "Dock to", "Maximize"]);
            // The terminal is alone in its group: it cannot split off itself.
            var moveTo = (MenuEntry)menu.Items[3]!;
            moveTo.IsEnabled.ShouldBeFalse();
            var dockTo = (MenuEntry)menu.Items[4]!;
            dockTo.IsEnabled.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Right_Click_Opens_The_Menu_At_The_Pointer()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var tab = Tab(layout, "doc:app");
            var point = tab.TranslatePoint(new Point(8, 8), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();
            layout.Menu.ShouldNotBeNull().IsOpen.ShouldBeTrue();
            layout.Menu.PlacementTarget.ShouldBe(tab);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Move_To_A_New_Group_Splits_And_Dock_To_Docks_Along_The_Layout()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var app = Pane(layout, "doc:app");
            var rows = layout.BuildPaneMenu(app);
            var moveTo = rows.Single(r => (string?)r.Header == "Move to");
            var right = moveTo.ItemsSource!.Cast<MenuEntry>().Single(r => (string?)r.Header == "New group right");
            right.RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var editors = layout.Workspace.DocumentArea!.Root.ShouldBeOfType<DockSplit>();
            editors.Orientation.ShouldBe(Orientation.Horizontal);
            editors.Children[1].ShouldBe(app.Group);
            layout.ActivePane.ShouldBe(app);
            layout.LastAnnouncement.ShouldBe("App.axaml docked to the right of README.md");
            // The pane's new tab takes the focus the menu gave back.
            TestHelpers.PumpUntil(() => Focused(window) == Tab(layout, "doc:app")).ShouldBeTrue();

            var search = Pane(layout, "search");
            var dockTo = layout.BuildPaneMenu(search).Single(r => (string?)r.Header == "Dock to");
            dockTo.ItemsSource!.Cast<MenuEntry>().Single(r => (string?)r.Header == "Bottom").RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
            root.Orientation.ShouldBe(Orientation.Vertical);
            root.Children[1].ShouldBe(search.Group);
            layout.LastAnnouncement.ShouldBe("Search docked to the bottom edge");
            search.Group!.View.ShouldBeOfType<DockGroupView>().Parent.ShouldBeOfType<ResizablePanel>();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Close_Others_And_Close_All_Act_On_The_Document_Group()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            var group = readme.Group!;
            group.Panes.Add(DockSample.Document("doc:three", "Three.md"));
            var rows = layout.BuildPaneMenu(readme);
            rows.Single(r => (string?)r.Header == "Close others").RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            group.Panes.ShouldBe([readme]);
            layout.BuildPaneMenu(readme).Single(r => (string?)r.Header == "Close others").IsEnabled.ShouldBeFalse();
            layout.BuildPaneMenu(readme).Single(r => (string?)r.Header == "Close all").RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            layout.Workspace.DocumentArea!.Root.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Middle_Click_Closes_A_Tab()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var tab = Tab(layout, "doc:app");
            var point = tab.TranslatePoint(new Point(8, 8), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(point, MouseButton.Middle);
            window.MouseUp(point, MouseButton.Middle);
            Dispatcher.UIThread.RunJobs();
            layout.Workspace.FindPane("doc:app").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Layout_And_Its_Parts_Have_Accessible_Names()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(layout);
            peer.GetName().ShouldBe("Dock layout");
            peer.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Group);
            var tabPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Tab(layout, "terminal"));
            tabPeer.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.TabItem);
            tabPeer.GetName().ShouldBe("Terminal");
            var groupPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Pane(layout, "terminal").Group!.View!);
            groupPeer.GetAutomationControlType().ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Tab);
        }
        finally
        {
            window.Close();
        }
    }
}
