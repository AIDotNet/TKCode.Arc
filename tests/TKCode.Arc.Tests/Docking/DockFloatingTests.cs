using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Floating (spec 5.5, 9.3; tests of section 15): cards in overlay mode, windows in window mode, docking back.</summary>
public class DockFloatingTests
{
    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static DockTab Tab(DockLayout layout, string id) => DockLayoutViewTests.TabOf(layout, id);

    private static DockLayout Spec(DockFloatingMode mode)
    {
        var layout = DockLayoutViewTests.LoadSpec();
        layout.FloatingMode = mode;
        return layout;
    }

    /// <summary>Shows the layout with room around it, so a drag can leave it.</summary>
    private static Window ShowWithMargin(DockLayout layout, double margin = 80)
    {
        var host = new Border { Padding = new Thickness(margin), Child = layout };
        var window = TestHelpers.Show(host);
        window.Width = 1200;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Auto_Uses_Cards_Where_There_Is_No_Desktop_Lifetime()
    {
        var layout = Spec(DockFloatingMode.Auto);
        var window = DockLayoutViewTests.Show(layout);
        try
        {
            layout.UsesWindows.ShouldBeFalse();
            layout.FloatingMode = DockFloatingMode.Window;
            layout.UsesWindows.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Float_In_Overlay_Mode_Shows_A_Card_And_Dock_Puts_The_Pane_Back()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var terminal = Pane(layout, "terminal");
            var column = terminal.Group!.Parent.ShouldBeOfType<DockSplit>();
            var changes = new List<DockLayoutChangeKind>();
            layout.LayoutChanged += (_, e) => changes.Add(e.Kind);

            layout.Float(terminal);
            Pump();
            terminal.State.ShouldBe(DockPaneState.Floating);
            terminal.Classes.ShouldContain(":floating");
            var dock = layout.Workspace.Floats.ShouldHaveSingleItem();
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().ShouldHaveSingleItem();
            layout.HostOf(dock).ShouldBe(card);
            card.Title.ShouldBe("Terminal");
            card.Content.ShouldBe(terminal.Group!.View);
            terminal.GetVisualRoot().ShouldBe(window);
            card.Bounds.Width.ShouldBeGreaterThanOrEqualTo(320);
            layout.LastAnnouncement.ShouldBe("Terminal moved to a new window");
            changes.ShouldContain(DockLayoutChangeKind.Float);
            // The document area took the room: the vertical split is gone.
            layout.Workspace.DocumentArea!.Parent.ShouldBe(layout.Workspace.Root);

            layout.Dock(terminal);
            Pump();
            terminal.State.ShouldBe(DockPaneState.Docked);
            layout.Workspace.Floats.ShouldBeEmpty();
            layout.FloatsPanel.Children.ShouldBeEmpty();
            // Back below the documents, with about the share it had.
            var parent = terminal.Group!.Parent.ShouldBeOfType<DockSplit>();
            parent.Orientation.ShouldBe(Orientation.Vertical);
            parent.Children.IndexOf(terminal.Group).ShouldBe(1);
            parent.Children[0].ShouldBe(layout.Workspace.DocumentArea);
            var share = terminal.Group.Size / parent.Children.Sum(c => c.Size);
            share.ShouldBe(0.3, 0.02);
            changes.ShouldContain(DockLayoutChangeKind.Dock);
            column.Workspace.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Float_And_Dock_Return_A_Tab_To_Its_Group_And_Place()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var explorer = Pane(layout, "explorer");
            var tools = explorer.Group!;
            layout.Float(explorer);
            Pump();
            tools.Panes.Select(p => p.Id).ShouldBe(["search"]);
            layout.Dock(explorer);
            Pump();
            tools.Panes.Select(p => p.Id).ShouldBe(["explorer", "search"]);
            layout.ActivePane.ShouldBe(explorer);

            // A document floats and docks back into the document group.
            var app = Pane(layout, "doc:app");
            var documents = app.Group!;
            layout.Float(app);
            Pump();
            app.State.ShouldBe(DockPaneState.Floating);
            layout.Dock(app);
            Pump();
            app.Group.ShouldBe(documents);
            documents.Panes.Select(p => p.Id).ShouldBe(["doc:readme", "doc:app"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Out_Of_The_Layout_Floats_It_Where_It_Was_Dropped()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = ShowWithMargin(layout);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var outside = new Point(window.Bounds.Width - 20, 400);
            DockPointer.Hold(window, from, outside);
            var session = layout.DragSession.ShouldNotBeNull();
            session.Hit.Host.ShouldBeNull();
            layout.IndicatorLayer!.Ghost?.IsVisible.ShouldNotBe(true);
            DockPointer.Release(window, outside);

            var terminal = Pane(layout, "terminal");
            terminal.State.ShouldBe(DockPaneState.Floating);
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().ShouldHaveSingleItem();
            // Opened at the pointer less (24, 16), pulled inside the layout.
            var right = card.Position.X + card.Bounds.Width;
            right.ShouldBe(layout.FloatsPanel.Bounds.Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drop_Outside_With_Floating_Disabled_Puts_The_Pane_Back()
    {
        var layout = Spec(DockFloatingMode.Disabled);
        var window = ShowWithMargin(layout);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var outside = new Point(window.Bounds.Width - 20, 400);
            DockPointer.Hold(window, from, outside);
            DockPointer.Release(window, outside);
            Pane(layout, "terminal").State.ShouldBe(DockPaneState.Docked);
            layout.Workspace.Floats.ShouldBeEmpty();
            layout.BuildPaneMenu(Pane(layout, "terminal")).Select(r => r.Header).ShouldNotContain("Float");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Only_Tab_Of_A_Card_Moves_The_Card_And_Docks_It_On_A_Button()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().Single();
            var start = card.Position;
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var tools = Pane(layout, "explorer").Group!;
            var toolsView = DockLayoutViewTests.ViewOf(tools);
            var over = DockPointer.At(toolsView, window, toolsView.Bounds.Width / 2, toolsView.Bounds.Height * 0.6);
            DockPointer.Hold(window, from, over);

            var session = layout.DragSession.ShouldNotBeNull();
            session.Phase.ShouldBe(DockDragPhase.Move);
            card.Position.ShouldNotBe(start);
            (card.Position - start).X.ShouldBe((over - from).X, 1);
            // Over a group but not on a button: moving the card does not dock it.
            session.Hit.Target.ShouldBeNull();
            var center = DockPointer.ButtonCenter(layout, window, DockCompassSlot.Center);
            DockPointer.Move(window, over, center, 2);
            session.Hit.Target.ShouldBe(DockTarget.Into(tools));
            DockPointer.Release(window, center);

            terminal.Group.ShouldBe(tools);
            layout.Workspace.Floats.ShouldBeEmpty();
            layout.FloatsPanel.Children.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Moving_A_Card_By_Its_Title_Bar_Keeps_It_Floating_And_Escape_Puts_It_Back()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.Float(explorer);
            Pump();
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().Single();
            // Bounds set on the model move the card.
            layout.Workspace.Floats.Single().Bounds = new Rect(200, 150, 400, 300);
            Pump();
            card.Position.ShouldBe(new Point(200, 150));
            card.Bounds.Size.ShouldBe(new Size(400, 300));
            var bar = card.TitleBarPartControl.ShouldNotBeNull();
            var start = card.Position;
            var from = DockPointer.At(bar, window, 30, bar.Bounds.Height / 2);
            var to = from + new Vector(-120, 60);
            DockPointer.Hold(window, from, to);
            card.Position.ShouldBe(start + new Vector(-120, 60));
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            card.Position.ShouldBe(start);
            DockPointer.Release(window, to);

            // Start a little aside, or the press counts as a double click (which docks the card).
            DockPointer.Hold(window, from + new Vector(30, 0), to + new Vector(30, 0));
            DockPointer.Release(window, to + new Vector(30, 0));
            card.Position.ShouldBe(start + new Vector(-120, 60));
            layout.Workspace.Floats.Single().Bounds.Position.ShouldBe(card.Position);
            explorer.State.ShouldBe(DockPaneState.Floating);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Cards_Buttons_Dock_And_Close_It()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.Float(explorer);
            Pump();
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().Single();
            card.DockButton.ShouldNotBeNull().RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            explorer.State.ShouldBe(DockPaneState.Docked);
            layout.Workspace.Floats.ShouldBeEmpty();

            var closed = new List<string>();
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!.Id);
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            card = layout.FloatsPanel.Children.OfType<DockFloatingCard>().Single();
            card.CloseButton.ShouldNotBeNull().RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            closed.ShouldBe(["terminal"]);
            terminal.Workspace.ShouldBeNull();
            layout.FloatsPanel.Children.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Window_Mode_Opens_An_Owned_Window_Bound_To_The_Model()
    {
        var layout = Spec(DockFloatingMode.Window);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        var opened = new List<Window>();
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            var dock = layout.Workspace.Floats.Single();
            var floating = layout.HostOf(dock).ShouldBeOfType<DockFloatingWindow>();
            opened.Add(floating);
            floating.Owner.ShouldBe(window);
            floating.ShowInTaskbar.ShouldBeFalse();
            floating.IsVisible.ShouldBeTrue();
            floating.Title.ShouldBe("Terminal");
            terminal.GetVisualRoot().ShouldBe(floating);
            terminal.Group!.View.ShouldNotBeNull().GetVisualRoot().ShouldBe(floating);
            layout.FloatsPanel!.Children.ShouldBeEmpty();
            floating.Width.ShouldBeGreaterThanOrEqualTo(320);

            // A float added in code opens a window; removing it closes the window.
            var problems = DockSample.Tool("problems", "Problems", DockSide.Bottom);
            var added = new DockFloat(new DockGroup(problems), new Rect(100, 100, 400, 300));
            layout.Workspace.Floats.Add(added);
            Pump();
            var second = layout.HostOf(added).ShouldBeOfType<DockFloatingWindow>();
            opened.Add(second);
            second.Title.ShouldBe("Problems");
            second.Width.ShouldBe(400);
            layout.Workspace.Floats.Remove(added);
            Pump();
            layout.HostOf(added).ShouldBeNull();
            second.IsVisible.ShouldBeFalse();

            // Docking from the window's menu row puts it back and closes the window.
            layout.BuildPaneMenu(terminal).Select(r => r.Header).ShouldContain("Dock");
            layout.BuildPaneMenu(terminal).Select(r => r.Header).ShouldNotContain("Float");
            layout.Dock(terminal);
            Pump();
            floating.IsVisible.ShouldBeFalse();
            terminal.GetVisualRoot().ShouldBe(window);
        }
        finally
        {
            foreach (var w in opened)
            {
                w.Close();
            }
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Closing_A_Floating_Window_Closes_Its_Panes_And_Docks_The_Ones_That_Cannot_Close()
    {
        var layout = Spec(DockFloatingMode.Window);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var closed = new List<string>();
            layout.PaneClosed += (_, e) => closed.Add(e.Pane!.Id);
            var terminal = Pane(layout, "terminal");
            var explorer = Pane(layout, "explorer");
            explorer.CanClose = false;
            layout.Float(terminal);
            Pump();
            var dock = layout.Workspace.Floats.Single();
            // Explorer joins the terminal's window.
            layout.Move(explorer, DockTarget.Into((DockGroup)dock.Root!)).ShouldBeTrue();
            Pump();
            var floating = layout.HostOf(dock).ShouldBeOfType<DockFloatingWindow>();

            floating.Close();
            Pump();
            closed.ShouldBe(["terminal"]);
            explorer.State.ShouldBe(DockPaneState.Docked);
            explorer.Group.ShouldBe(Pane(layout, "search").Group);
            layout.Workspace.Floats.ShouldBeEmpty();
            floating.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Cancelled_Close_Keeps_The_Window()
    {
        var layout = Spec(DockFloatingMode.Window);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        DockFloatingWindow? floating = null;
        try
        {
            layout.PaneClosing += (_, e) => e.Cancel = true;
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            floating = layout.HostOf(layout.Workspace.Floats.Single()).ShouldBeOfType<DockFloatingWindow>();
            floating.Close();
            Pump();
            floating.IsVisible.ShouldBeTrue();
            terminal.State.ShouldBe(DockPaneState.Floating);
        }
        finally
        {
            floating?.Dismiss();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Floats_Round_Trip_Through_A_Saved_Layout()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            var bounds = layout.Workspace.Floats.Single().Bounds;
            var json = layout.SaveLayout();
            json.ShouldContain("\"floats\"");
            layout.Dock(terminal);
            Pump();
            layout.FloatsPanel!.Children.ShouldBeEmpty();

            layout.LoadLayout(json).ShouldBeTrue();
            Pump();
            terminal.State.ShouldBe(DockPaneState.Floating);
            var card = layout.FloatsPanel.Children.OfType<DockFloatingCard>().ShouldHaveSingleItem();
            card.Position.ShouldBe(bounds.Position);
            card.Content.ShouldBe(terminal.Group!.View);

            // The pane still remembers where it was docked.
            layout.Dock(terminal);
            Pump();
            terminal.Group!.Parent.ShouldBeOfType<DockSplit>().Orientation.ShouldBe(Orientation.Vertical);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Menu_And_The_Command_Float_A_Pane()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var search = Pane(layout, "search");
            var row = layout.BuildPaneMenu(search).Single(r => (string?)r.Header == "Float");
            row.IsEnabled.ShouldBeTrue();
            row.RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Pump();
            search.State.ShouldBe(DockPaneState.Floating);

            var terminal = Pane(layout, "terminal");
            layout.Activate(terminal);
            layout.FloatActiveCommand.CanExecute(null).ShouldBeTrue();
            layout.FloatActiveCommand.Execute(null);
            Pump();
            terminal.State.ShouldBe(DockPaneState.Floating);
            layout.Workspace.Floats.Count.ShouldBe(2);
            // Alone in its card already: nothing more to float.
            layout.FloatActiveCommand.CanExecute(null).ShouldBeFalse();

            terminal.CanFloat = false;
            layout.Activate(Pane(layout, "explorer"));
            Pane(layout, "explorer").CanFloat = false;
            layout.BuildPaneMenu(Pane(layout, "explorer")).Single(r => (string?)r.Header == "Float").IsEnabled.ShouldBeFalse();
            layout.IsLocked = true;
            layout.BuildPaneMenu(terminal).Single(r => (string?)r.Header == "Dock").IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Double_Click_On_A_Cards_Title_Docks_It()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            var card = layout.FloatsPanel!.Children.OfType<DockFloatingCard>().Single();
            var point = DockPointer.At(card.TitleBarPartControl!, window, 30, 10);
            DockPointer.Press(window, point);
            window.MouseUp(point, MouseButton.Left);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            terminal.State.ShouldBe(DockPaneState.Docked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Of_A_Two_Pane_Card_Out_Docks_Only_That_Pane()
    {
        var layout = Spec(DockFloatingMode.Overlay);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        try
        {
            var terminal = Pane(layout, "terminal");
            var search = Pane(layout, "search");
            layout.Float(terminal);
            Pump();
            var dock = layout.Workspace.Floats.Single();
            layout.Move(search, DockTarget.Into((DockGroup)dock.Root!)).ShouldBeTrue();
            Pump();
            var from = DockPointer.CenterOf(Tab(layout, "search"), window);
            var docs = DockLayoutViewTests.ViewOf(Pane(layout, "doc:readme").Group!);
            var over = DockPointer.At(docs, window, docs.Bounds.Width / 2, docs.Bounds.Height * 0.6);
            DockPointer.Hold(window, from, over);
            layout.DragSession!.Phase.ShouldBe(DockDragPhase.Dock);
            var right = DockPointer.ButtonCenter(layout, window, DockCompassSlot.AreaRight);
            DockPointer.Move(window, over, right, 2);
            DockPointer.Release(window, right);

            search.State.ShouldBe(DockPaneState.Docked);
            terminal.State.ShouldBe(DockPaneState.Floating);
            layout.Workspace.Floats.ShouldHaveSingleItem();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dropping_Outside_In_Window_Mode_Opens_A_Window_And_Switching_To_Cards_Keeps_The_Float()
    {
        var layout = Spec(DockFloatingMode.Window);
        var window = ShowWithMargin(layout);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var outside = new Point(window.Bounds.Width - 20, 400);
            DockPointer.Hold(window, from, outside);
            DockPointer.Release(window, outside);
            var terminal = Pane(layout, "terminal");
            terminal.State.ShouldBe(DockPaneState.Floating);
            var dock = layout.Workspace.Floats.Single();
            var floating = layout.HostOf(dock).ShouldBeOfType<DockFloatingWindow>();
            floating.IsVisible.ShouldBeTrue();
            dock.Bounds.Width.ShouldBeGreaterThanOrEqualTo(320);
            dock.Bounds.Height.ShouldBeGreaterThanOrEqualTo(240);

            // Cards from now on: the window goes, a card shows the same float.
            layout.FloatingMode = DockFloatingMode.Overlay;
            Pump();
            floating.IsVisible.ShouldBeFalse();
            var card = layout.HostOf(dock).ShouldBeOfType<DockFloatingCard>();
            card.Content.ShouldBe(terminal.Group!.View);
            terminal.GetVisualRoot().ShouldBe(window);
        }
        finally
        {
            foreach (var host in layout.FloatHosts.ToList())
            {
                host.Dismiss();
            }
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Only_Tab_Of_A_Floating_Window_Moves_The_Window_And_The_Layouts_Compass_Docks_It()
    {
        var layout = Spec(DockFloatingMode.Window);
        var window = DockLayoutViewTests.Show(layout, 1200, 800);
        DockFloatingWindow? floating = null;
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            floating = layout.HostOf(layout.Workspace.Floats.Single()).ShouldBeOfType<DockFloatingWindow>();
            Pump();
            var start = floating.Position;
            var tab = Tab(layout, "terminal");
            var p0 = DockPointer.CenterOf(tab, floating);
            var s0 = floating.PointToScreen(p0);
            floating.MouseDown(p0, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            // The pointer travels in screen space; the events reach the moving window in its own (changing) coordinates.
            var tools = DockLayoutViewTests.ViewOf(Pane(layout, "explorer").Group!);
            var s1 = window.PointToScreen(DockPointer.At(tools, window, tools.Bounds.Width / 2, tools.Bounds.Height * 0.6));
            for (var i = 1; i <= 4; i++)
            {
                var t = i / 4.0;
                var s = new PixelPoint((int)(s0.X + (s1.X - s0.X) * t), (int)(s0.Y + (s1.Y - s0.Y) * t));
                floating.MouseMove(floating.PointToClient(s), RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
            }
            var session = layout.DragSession.ShouldNotBeNull();
            session.Phase.ShouldBe(DockDragPhase.Move);
            floating.Position.ShouldBe(start + (s1 - s0));
            layout.IndicatorLayer!.CompassGroup.ShouldNotBeNull();

            var s2 = window.PointToScreen(DockPointer.ButtonCenter(layout, window, DockCompassSlot.Center));
            floating.MouseMove(floating.PointToClient(s2), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            session.Hit.Target.ShouldBe(DockTarget.Into(Pane(layout, "explorer").Group!));
            floating.MouseUp(floating.PointToClient(s2), MouseButton.Left);
            Pump();

            terminal.Group.ShouldBe(Pane(layout, "explorer").Group);
            layout.Workspace.Floats.ShouldBeEmpty();
            floating.IsVisible.ShouldBeFalse();
        }
        finally
        {
            if (floating is { IsVisible: true })
            {
                floating.Dismiss();
            }
            window.Close();
        }
    }
}
