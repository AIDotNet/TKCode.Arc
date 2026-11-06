using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Pointer helpers for dock drags in a headless window.</summary>
internal static class DockPointer
{
    public static Point CenterOf(Visual visual, Visual window)
        => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    public static Point At(Visual visual, Visual window, double x, double y) => visual.TranslatePoint(new Point(x, y), window)!.Value;

    public static void Press(Window window, Point point)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Moves with the left button held, in a few steps (the first step crosses the drag threshold).</summary>
    public static void Move(Window window, Point from, Point to, int steps = 4)
    {
        for (var i = 1; i <= steps; i++)
        {
            var t = (double)i / steps;
            window.MouseMove(new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
        }
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    public static void Release(Window window, Point point)
    {
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Presses at <paramref name="from"/>, moves to <paramref name="to"/> and keeps the button down.</summary>
    public static void Hold(Window window, Point from, Point to)
    {
        Press(window, from);
        Move(window, from, to);
    }

    /// <summary>The centre of a visible compass or edge button, in window coordinates.</summary>
    public static Point ButtonCenter(DockLayout layout, Window window, DockCompassSlot slot, DockIndicatorLayer? layer = null)
    {
        var button = (layer ?? layout.IndicatorLayer!).ButtonOf(slot).ShouldNotBeNull($"no {slot} button");
        return CenterOf(button, window);
    }
}

/// <summary>Drag and drop inside a window (spec 9.1, 9.2; tests of section 15).</summary>
public class DockDragTests
{
    private static DockLayout Spec() => DockLayoutViewTests.LoadSpec();

    private static Window Show(DockLayout layout, double width = 900, double height = 600) => DockLayoutViewTests.Show(layout, width, height);

    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static DockTab Tab(DockLayout layout, string id) => DockLayoutViewTests.TabOf(layout, id);

    private static DockGroupView View(DockLayout layout, string id) => DockLayoutViewTests.ViewOf(Pane(layout, id).Group!);

    /// <summary>A point in the content of a pane's group (below the strip), at fractions of its size.</summary>
    private static Point InGroup(DockLayout layout, Window window, string id, double fx = 0.5, double fy = 0.6)
    {
        var view = View(layout, id);
        return DockPointer.At(view, window, view.Bounds.Width * fx, view.Bounds.Height * fy);
    }

    [AvaloniaFact]
    public void Dragging_A_Tab_Along_Its_Strip_Reorders_It_And_The_Neighbour_Makes_Room()
    {
        var layout = Spec();
        var window = Show(layout);
        try
        {
            var readme = Tab(layout, "doc:readme");
            var app = Tab(layout, "doc:app");
            var from = DockPointer.CenterOf(readme, window);
            var to = DockPointer.At(app, window, app.Bounds.Width * 0.8, app.Bounds.Height / 2);
            DockPointer.Hold(window, from, to);

            var session = layout.DragSession.ShouldNotBeNull();
            session.Phase.ShouldBe(DockDragPhase.Reorder);
            layout.Classes.ShouldNotContain(":dragging");
            session.Index.ShouldBe(1);
            // Motion is off in tests: the neighbour jumps to its new place, one tab width (plus the gap) to the left.
            DockTabShift.Current(app).ShouldBe(-(readme.Bounds.Width + 2), 0.01);
            DockTabShift.Current(readme).ShouldBeGreaterThan(0);

            DockPointer.Release(window, to);
            layout.DragSession.ShouldBeNull();
            Pane(layout, "doc:app").Group!.Panes.Select(p => p.Id).ShouldBe(["doc:app", "doc:readme"]);
            layout.ActivePane.ShouldBe(Pane(layout, "doc:readme"));
            layout.LastAnnouncement.ShouldBe("README.md, tab 2 of 2");
            DockTabShift.Current(app).ShouldBe(0);
            DockTabShift.Current(readme).ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Strip_Turns_The_Drag_Into_Docking_And_Escape_Cancels_It()
    {
        var layout = Spec();
        var window = Show(layout);
        try
        {
            var readme = Tab(layout, "doc:readme");
            var from = DockPointer.CenterOf(readme, window);
            DockPointer.Hold(window, from, from + new Vector(0, 60));

            var session = layout.DragSession.ShouldNotBeNull();
            session.Phase.ShouldBe(DockDragPhase.Dock);
            layout.Classes.ShouldContain(":dragging");
            readme.Classes.ShouldContain(":dragging");
            var layer = layout.IndicatorLayer.ShouldNotBeNull();
            layer.CompassGroup.ShouldNotBeNull();
            layer.ButtonOf(DockCompassSlot.Center).ShouldNotBeNull();
            layer.ButtonOf(DockCompassSlot.EdgeLeft).ShouldNotBeNull();
            // The pointer is over the document group: the document area's ring shows too.
            layer.ButtonOf(DockCompassSlot.AreaLeft).ShouldNotBeNull();
            layer.Ghost.ShouldNotBeNull().IsVisible.ShouldBeTrue();
            layer.Ghost!.Title.ShouldBe("README.md");
            View(layout, "doc:readme").Classes.ShouldContain(":drop-target");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            layout.DragSession.ShouldBeNull();
            layout.Classes.ShouldNotContain(":dragging");
            readme.Classes.ShouldNotContain(":dragging");
            layer.IsShowing.ShouldBeFalse();
            View(layout, "doc:readme").Classes.ShouldNotContain(":drop-target");

            // The release that follows does nothing.
            DockPointer.Release(window, from + new Vector(0, 60));
            Pane(layout, "doc:readme").Group!.Panes.Select(p => p.Id).ShouldBe(["doc:readme", "doc:app"]);
            layout.Workspace.Floats.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Center")]
    [InlineData("Left")]
    [InlineData("Right")]
    [InlineData("Top")]
    [InlineData("Bottom")]
    public void Dropping_On_A_Compass_Button_Docks_There(string name)
    {
        var slot = Enum.Parse<DockCompassSlot>(name);
        var layout = Spec();
        var window = Show(layout, 1200, 700);
        try
        {
            var terminal = Pane(layout, "terminal");
            var tools = Pane(layout, "explorer").Group!;
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var over = InGroup(layout, window, "explorer");
            DockPointer.Hold(window, from, over);
            var target = DockPointer.ButtonCenter(layout, window, slot);
            DockPointer.Move(window, over, target, 2);
            layout.IndicatorLayer!.HotSlot.ShouldBe(slot);
            layout.IndicatorLayer.Preview.HasTarget.ShouldBeTrue();
            DockPointer.Release(window, target);

            layout.DragSession.ShouldBeNull();
            if (slot == DockCompassSlot.Center)
            {
                tools.Panes.Select(p => p.Id).ShouldBe(["explorer", "search", "terminal"]);
                layout.LastAnnouncement.ShouldBe("Terminal moved next to Explorer");
                return;
            }
            var group = terminal.Group.ShouldNotBeNull();
            group.ShouldNotBe(tools);
            var parent = group.Parent.ShouldBeOfType<DockSplit>();
            parent.ShouldBe(tools.Parent);
            var horizontal = slot is DockCompassSlot.Left or DockCompassSlot.Right;
            parent.Orientation.ShouldBe(horizontal ? Orientation.Horizontal : Orientation.Vertical);
            var before = slot is DockCompassSlot.Left or DockCompassSlot.Top;
            (parent.Children.IndexOf(group) < parent.Children.IndexOf(tools)).ShouldBe(before);
            layout.ActivePane.ShouldBe(terminal);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("EdgeRight")]
    [InlineData("EdgeTop")]
    public void Dropping_On_An_Edge_Button_Docks_Along_The_Whole_Layout(string name)
    {
        var slot = Enum.Parse<DockCompassSlot>(name);
        var layout = Spec();
        var window = Show(layout, 1200, 700);
        try
        {
            var explorer = Pane(layout, "explorer");
            var from = DockPointer.CenterOf(Tab(layout, "explorer"), window);
            var over = InGroup(layout, window, "doc:readme");
            DockPointer.Hold(window, from, over);
            var target = DockPointer.ButtonCenter(layout, window, slot);
            DockPointer.Move(window, over, target, 2);
            layout.IndicatorLayer!.HotSlot.ShouldBe(slot);
            DockPointer.Release(window, target);

            var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
            if (slot == DockCompassSlot.EdgeRight)
            {
                root.Orientation.ShouldBe(Orientation.Horizontal);
                root.Children[^1].ShouldBe(explorer.Group);
                layout.LastAnnouncement.ShouldBe("Explorer docked to the right edge");
            }
            else
            {
                root.Orientation.ShouldBe(Orientation.Vertical);
                root.Children[0].ShouldBe(explorer.Group);
                layout.LastAnnouncement.ShouldBe("Explorer docked to the top edge");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dropping_On_A_Strip_Inserts_The_Tab_Where_The_Line_Shows()
    {
        var layout = Spec();
        var window = Show(layout, 1400, 700);
        try
        {
            var explorer = Tab(layout, "explorer");
            var search = Tab(layout, "search");
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            // Between Explorer and Search: just right of Explorer's centre.
            var between = DockPointer.At(explorer, window, explorer.Bounds.Width * 0.9, explorer.Bounds.Height / 2);
            DockPointer.Hold(window, from, between);
            var layer = layout.IndicatorLayer!;
            var line = layer.InsertionLine.ShouldNotBeNull();
            var searchLeft = DockPointer.At(search, layer, 0, 0).X;
            var explorerRight = DockPointer.At(explorer, layer, explorer.Bounds.Width, 0).X;
            line.Center.X.ShouldBeInRange(explorerRight - 1, searchLeft + 1);
            DockPointer.Release(window, between);

            Pane(layout, "explorer").Group!.Panes.Select(p => p.Id).ShouldBe(["explorer", "terminal", "search"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Documents_Stay_In_The_Document_Area_And_Tools_Enter_It_Only_When_Allowed()
    {
        var layout = Spec();
        layout.FloatingMode = DockFloatingMode.Disabled;
        var window = Show(layout, 1200, 700);
        try
        {
            // A document over a tool group: nothing accepts it.
            var from = DockPointer.CenterOf(Tab(layout, "doc:readme"), window);
            var over = InGroup(layout, window, "explorer");
            DockPointer.Hold(window, from, over);
            var layer = layout.IndicatorLayer!;
            layout.DragSession!.Hit.Target.ShouldBeNull();
            layer.ButtonOf(DockCompassSlot.Center).ShouldNotBeNull().IsEnabled.ShouldBeFalse();
            layer.ButtonOf(DockCompassSlot.EdgeLeft).ShouldNotBeNull().IsEnabled.ShouldBeFalse();
            layer.Preview.HasTarget.ShouldBeFalse();
            DockPointer.Release(window, over);
            Pane(layout, "doc:readme").Group!.IsDocumentGroup.ShouldBeTrue();

            // A tool over the document group: refused until it may dock as a document.
            from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            over = InGroup(layout, window, "doc:readme");
            DockPointer.Hold(window, from, over);
            layer.ButtonOf(DockCompassSlot.Center).ShouldNotBeNull().IsEnabled.ShouldBeFalse();
            // ... but beside the whole document area is fine.
            layer.ButtonOf(DockCompassSlot.AreaLeft).ShouldNotBeNull().IsEnabled.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            DockPointer.Release(window, over);

            Pane(layout, "terminal").CanDockAsDocument = true;
            DockPointer.Hold(window, DockPointer.CenterOf(Tab(layout, "terminal"), window), over);
            var center = DockPointer.ButtonCenter(layout, window, DockCompassSlot.Center);
            layer.ButtonOf(DockCompassSlot.Center)!.IsEnabled.ShouldBeTrue();
            DockPointer.Move(window, over, center, 2);
            DockPointer.Release(window, center);
            Pane(layout, "terminal").Group.ShouldBe(Pane(layout, "doc:readme").Group);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Locked_Layout_Does_Not_Start_A_Drag()
    {
        var layout = Spec();
        layout.IsLocked = true;
        var window = Show(layout);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "doc:readme"), window);
            DockPointer.Hold(window, from, from + new Vector(40, 80));
            layout.DragSession.ShouldBeNull();
            layout.IndicatorLayer!.IsShowing.ShouldBeFalse();
            DockPointer.Release(window, from + new Vector(40, 80));
            Pane(layout, "doc:readme").Group!.Panes.Select(p => p.Id).ShouldBe(["doc:readme", "doc:app"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Empty_Part_Of_A_Strip_Moves_The_Whole_Group()
    {
        var layout = Spec();
        var window = Show(layout, 1400, 700);
        try
        {
            var tools = Pane(layout, "explorer").Group!;
            var strip = View(layout, "explorer").Strip!;
            var search = Tab(layout, "search");
            var empty = DockPointer.At(strip, window, (search.Bounds.Right + strip.Bounds.Width) / 2, strip.Bounds.Height / 2);
            var over = InGroup(layout, window, "terminal");
            DockPointer.Hold(window, empty, over);
            var session = layout.DragSession.ShouldNotBeNull();
            session.IsGroupDrag.ShouldBeTrue();
            layout.IndicatorLayer!.Ghost!.Extra.ShouldBe("+1");
            var center = DockPointer.ButtonCenter(layout, window, DockCompassSlot.Center);
            DockPointer.Move(window, over, center, 2);
            DockPointer.Release(window, center);

            var terminal = Pane(layout, "terminal").Group!;
            terminal.Panes.Select(p => p.Id).ShouldBe(["terminal", "explorer", "search"]);
            tools.Workspace.ShouldBeNull();
            terminal.ActivePane!.Id.ShouldBe("explorer");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Group_Cannot_Drop_Onto_Itself()
    {
        var layout = Spec();
        var window = Show(layout, 1400, 700);
        try
        {
            var strip = View(layout, "explorer").Strip!;
            var search = Tab(layout, "search");
            var empty = DockPointer.At(strip, window, (search.Bounds.Right + strip.Bounds.Width) / 2, strip.Bounds.Height / 2);
            var own = InGroup(layout, window, "explorer", 0.1, 0.6);
            DockPointer.Hold(window, empty, own);
            var session = layout.DragSession.ShouldNotBeNull();
            session.Hit.Target.ShouldBeNull();
            session.Hit.NoOp.ShouldBeTrue();
            layout.IndicatorLayer!.ButtonOf(DockCompassSlot.Left)!.IsEnabled.ShouldBeFalse();
            DockPointer.Release(window, own);
            // Nothing moved, nothing floated.
            Pane(layout, "explorer").Group!.Panes.Select(p => p.Id).ShouldBe(["explorer", "search"]);
            layout.Workspace.Floats.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Indicators_The_Pointer_Position_In_A_Group_Picks_The_Side()
    {
        var layout = Spec();
        layout.ShowDockIndicators = false;
        var window = Show(layout, 1200, 700);
        try
        {
            var tools = Pane(layout, "explorer").Group!;
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var nearLeft = InGroup(layout, window, "explorer", 0.1, 0.6);
            DockPointer.Hold(window, from, nearLeft);
            var layer = layout.IndicatorLayer!;
            layer.ButtonOf(DockCompassSlot.Center).ShouldBeNull();
            layer.ButtonOf(DockCompassSlot.EdgeLeft).ShouldBeNull();
            layer.Preview.HasTarget.ShouldBeTrue();
            layout.DragSession!.Hit.Target.ShouldBe(DockTarget.Split(tools, DockPosition.Left));
            // The middle joins as a tab.
            var middle = InGroup(layout, window, "explorer", 0.5, 0.6);
            DockPointer.Move(window, nearLeft, middle, 2);
            layout.DragSession!.Hit.Target.ShouldBe(DockTarget.Into(tools));
            DockPointer.Move(window, middle, nearLeft, 2);
            DockPointer.Release(window, nearLeft);

            var group = Pane(layout, "terminal").Group!;
            var parent = group.Parent.ShouldBeOfType<DockSplit>();
            parent.Orientation.ShouldBe(Orientation.Horizontal);
            parent.Children.IndexOf(group).ShouldBe(parent.Children.IndexOf(tools) - 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Document_Area_Ring_Docks_Beside_All_Documents()
    {
        var layout = Spec();
        var window = Show(layout, 1200, 700);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var over = InGroup(layout, window, "doc:readme");
            DockPointer.Hold(window, from, over);
            var ring = DockPointer.ButtonCenter(layout, window, DockCompassSlot.AreaRight);
            DockPointer.Move(window, over, ring, 2);
            DockPointer.Release(window, ring);

            var area = layout.Workspace.DocumentArea!;
            var group = Pane(layout, "terminal").Group!;
            group.IsDocumentGroup.ShouldBeFalse();
            var parent = group.Parent.ShouldBeOfType<DockSplit>();
            parent.Orientation.ShouldBe(Orientation.Horizontal);
            parent.Children.IndexOf(group).ShouldBe(parent.Children.IndexOf(area) + 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Preview_Covers_The_Half_A_Split_Takes()
    {
        var layout = Spec();
        var window = Show(layout, 1200, 700);
        try
        {
            var from = DockPointer.CenterOf(Tab(layout, "terminal"), window);
            var over = InGroup(layout, window, "explorer");
            DockPointer.Hold(window, from, over);
            var bottom = DockPointer.ButtonCenter(layout, window, DockCompassSlot.Bottom);
            DockPointer.Move(window, over, bottom, 2);
            var layer = layout.IndicatorLayer!;
            var view = View(layout, "explorer");
            var rect = new Rect(DockPointer.At(view, layer, 0, 0), view.Bounds.Size);
            layer.Preview.CurrentRect.ShouldBe(new Rect(rect.X, rect.Y + rect.Height / 2, rect.Width, rect.Height / 2));
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            DockPointer.Release(window, bottom);
        }
        finally
        {
            window.Close();
        }
    }
}
