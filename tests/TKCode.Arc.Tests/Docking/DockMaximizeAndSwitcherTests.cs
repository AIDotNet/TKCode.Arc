using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Group maximize (spec 5.3, 6) and the pane switcher (spec 10, 11: Ctrl+Tab, most recently used order).</summary>
public class DockMaximizeAndSwitcherTests
{
    private static (DockLayout Layout, Window Window) ShowSpec()
    {
        var layout = DockLayoutViewTests.LoadSpec();
        return (layout, DockLayoutViewTests.Show(layout, 1400, 700));
    }

    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    #region Maximize

    [AvaloniaFact]
    public void A_Maximized_Group_Fills_The_Layout_And_Restoring_Changes_Nothing()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            layout.Activate(readme);
            var before = layout.SaveLayout();
            var group = readme.Group!;
            var view = DockLayoutViewTests.ViewOf(group);
            var root = layout.Host!.Children.Single();
            layout.ToggleMaximize(group);
            Settle();

            layout.Classes.ShouldContain(":maximized");
            layout.Host.Children.Single().ShouldBe(view);
            view.Bounds.Size.ShouldBe(layout.Host.Bounds.Size);
            Pane(layout, "explorer").GetVisualRoot().ShouldBeNull();
            Pane(layout, "terminal").GetVisualRoot().ShouldBeNull();
            readme.GetVisualRoot().ShouldBe(window);
            layout.ActivePane.ShouldBe(readme);
            layout.SaveLayout().ShouldBe(before);

            layout.ToggleMaximize(group);
            Settle();
            layout.Classes.ShouldNotContain(":maximized");
            layout.Host.Children.Single().ShouldBe(root);
            view.GetVisualRoot().ShouldBe(window);
            Pane(layout, "explorer").GetVisualRoot().ShouldBe(window);
            view.GetVisualAncestors().ShouldContain(group.Parent!.View!);
            layout.SaveLayout().ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Double_Clicking_The_Empty_Strip_Toggles_Maximize()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var group = Pane(layout, "terminal").Group!;
            var strip = DockLayoutViewTests.ViewOf(group).Strip!;
            Settle();
            // Right of the only tab, inside the strip.
            var point = strip.TranslatePoint(new Point(strip.Bounds.Width - 8, strip.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            layout.MaximizedGroup.ShouldBe(group);

            point = strip.TranslatePoint(new Point(strip.Bounds.Width - 8, strip.Bounds.Height / 2), window)!.Value;
            Settle();
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            layout.MaximizedGroup.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Maximize_Ends_When_The_Group_Goes_Or_Another_Group_Becomes_Active()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.ToggleMaximize(terminal.Group!);
            Settle();
            layout.Close(terminal).ShouldBeTrue();
            Settle();
            layout.MaximizedGroup.ShouldBeNull();
            layout.Classes.ShouldNotContain(":maximized");
            Pane(layout, "explorer").GetVisualRoot().ShouldBe(window);

            var readme = Pane(layout, "doc:readme");
            layout.ToggleMaximize(readme.Group!);
            Settle();
            layout.Activate(Pane(layout, "explorer"));
            Settle();
            layout.MaximizedGroup.ShouldBeNull();
            DockLayoutViewTests.ViewOf(readme.Group!).GetVisualRoot().ShouldBe(window);

            // Closing a tab of the maximized group keeps it maximized while it still has panes.
            layout.ToggleMaximize(readme.Group!);
            layout.Close(Pane(layout, "doc:app")).ShouldBeTrue();
            Settle();
            layout.MaximizedGroup.ShouldBe(readme.Group);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Menu_And_The_Command_Maximize_And_Restore()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            layout.Activate(readme);
            var row = layout.BuildPaneMenu(readme).Last();
            row.Header.ShouldBe("Maximize");
            row.RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Settle();
            layout.MaximizedGroup.ShouldBe(readme.Group);
            layout.BuildPaneMenu(readme).Last().Header.ShouldBe("Restore");

            layout.ToggleMaximizeCommand.CanExecute(null).ShouldBeTrue();
            layout.ToggleMaximizeCommand.Execute(null);
            Settle();
            layout.MaximizedGroup.ShouldBeNull();
            layout.ToggleMaximizeCommand.Execute(null);
            layout.MaximizedGroup.ShouldBe(readme.Group);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Layout_Of_One_Group_Does_Not_Maximize()
    {
        var pane = DockSample.Document("doc:only", "Only.md");
        var layout = new DockLayout { Root = new DockDocumentArea { Root = new DockGroup(pane) } };
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            // The group fills the layout already (its document area is the root).
            layout.ToggleMaximize(pane.Group!);
            layout.MaximizedGroup.ShouldBeNull();
            layout.BuildPaneMenu(pane).Last().IsEnabled.ShouldBeFalse();
            var bare = new DockLayout { Root = new DockGroup(DockSample.Tool("t", "Tool", DockSide.Left)) };
            bare.ToggleMaximize((DockGroup)bare.Root!);
            bare.MaximizedGroup.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Pane switcher

    [AvaloniaFact]
    public void Ctrl_Tab_Lists_Panes_Most_Recent_First_And_Releasing_Ctrl_Switches()
    {
        var (layout, window) = ShowSpec();
        try
        {
            layout.Activate(Pane(layout, "terminal"));
            layout.Activate(Pane(layout, "explorer"));
            layout.Activate(Pane(layout, "doc:readme"));
            Settle();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            Settle();
            var switcher = layout.Switcher.ShouldNotBeNull();
            switcher.IsOpen.ShouldBeTrue();
            switcher.Panes.Select(p => p.Id).ShouldBe(["doc:readme", "explorer", "terminal", "search", "doc:app"]);
            switcher.Selected!.Id.ShouldBe("explorer");
            switcher.Items[1].Classes.ShouldContain(":selected");
            Focused(window).ShouldBe(switcher);

            // Tab again with Ctrl held moves on; releasing Ctrl activates.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            switcher.Selected!.Id.ShouldBe("terminal");
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            Settle();
            switcher.IsOpen.ShouldBeFalse();
            layout.ActivePane!.Id.ShouldBe("terminal");
            Focused(window).ShouldBe(DockLayoutViewTests.TabOf(layout, "terminal"));

            // The order follows: terminal is now the most recent.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            switcher.Panes.Select(p => p.Id).Take(3).ShouldBe(["terminal", "doc:readme", "explorer"]);
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            Settle();
            layout.ActivePane!.Id.ShouldBe("doc:readme");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ctrl_Shift_Tab_Goes_Backwards_And_Escape_Cancels()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            layout.Activate(readme);
            var tab = DockLayoutViewTests.TabOf(layout, "doc:readme");
            tab.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control | RawInputModifiers.Shift);
            var switcher = layout.Switcher!;
            switcher.Selected.ShouldBe(switcher.Panes[^1]);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Control);
            switcher.Selected.ShouldBe(switcher.Panes[^2]);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.Control);
            Settle();
            switcher.IsOpen.ShouldBeFalse();
            layout.ActivePane.ShouldBe(readme);
            Focused(window).ShouldBe(tab);
            // Releasing Ctrl afterwards does nothing.
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            layout.ActivePane.ShouldBe(readme);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Command_Opens_The_Switcher_And_Enter_Or_A_Click_Chooses()
    {
        var (layout, window) = ShowSpec();
        try
        {
            layout.Activate(Pane(layout, "doc:readme"));
            layout.ShowPaneSwitcherCommand.CanExecute(null).ShouldBeTrue();
            layout.ShowPaneSwitcherCommand.Execute(null);
            Settle();
            var switcher = layout.Switcher!;
            switcher.IsOpen.ShouldBeTrue();
            // Opened without Ctrl held: releasing Ctrl does not choose; Down and Enter do.
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            switcher.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            var chosen = switcher.Selected!;
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            layout.ActivePane.ShouldBe(chosen);

            layout.ShowPaneSwitcherCommand.Execute(null);
            Settle();
            var row = switcher.Items.Single(i => i.Pane.Id == "terminal");
            var point = row.TranslatePoint(new Point(20, row.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Settle();
            switcher.IsOpen.ShouldBeFalse();
            layout.ActivePane!.Id.ShouldBe("terminal");

            // A press outside the card closes without switching.
            layout.ShowPaneSwitcherCommand.Execute(null);
            Settle();
            window.MouseDown(new Point(20, 20), MouseButton.Left);
            window.MouseUp(new Point(20, 20), MouseButton.Left);
            Settle();
            switcher.IsOpen.ShouldBeFalse();
            layout.ActivePane!.Id.ShouldBe("terminal");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Switcher_Lists_Auto_Hidden_Panes_And_Choosing_One_Slides_It_Out()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.Activate(explorer);
            layout.AutoHide(explorer);
            layout.Activate(Pane(layout, "doc:readme"));
            Settle();
            // Auto-hiding handed the active pane to the search: readme, search, explorer.
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            layout.Switcher!.Selected.ShouldBe(explorer);
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            Settle();
            layout.Flyout!.IsOpen.ShouldBeTrue();
            layout.Flyout.Pane.ShouldBe(explorer);
            Focused(window).ShouldBe(explorer.Content);
            // A closed pane leaves the list.
            layout.Close(Pane(layout, "terminal"));
            layout.RecentPanes().ShouldNotContain(p => p.Id == "terminal");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
