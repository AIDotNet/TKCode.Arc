using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Docking;

/// <summary>Where auto hide, the switcher and maximize (M3, M5) meet drag and drop and floating (M2, M4).</summary>
public class DockIntegrationTests
{
    private static DockLayout Spec(DockFloatingMode mode = DockFloatingMode.Overlay)
    {
        var layout = DockLayoutViewTests.LoadSpec();
        layout.FloatingMode = mode;
        return layout;
    }

    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Dock_Pins_An_Auto_Hidden_Pane_Back_Into_Its_Group()
    {
        var layout = Spec();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            var explorer = Pane(layout, "explorer");
            var group = explorer.Group!;
            layout.AutoHide(explorer).ShouldBeTrue();
            layout.Activate(explorer);
            Pump();
            layout.Dock(explorer);
            Pump();
            explorer.State.ShouldBe(DockPaneState.Docked);
            explorer.Group.ShouldBe(group);
            group.Panes.IndexOf(explorer).ShouldBe(0);
            layout.Flyout!.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Floating_Panes_Do_Not_Auto_Hide_But_Auto_Hidden_Panes_Can_Float()
    {
        var layout = Spec();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.Float(terminal);
            Pump();
            terminal.State.ShouldBe(DockPaneState.Floating);
            layout.AutoHide(terminal).ShouldBeFalse();
            layout.BuildPaneMenu(terminal).ShouldNotContain(r => (string?)r.Header == "Auto hide");

            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            layout.Activate(explorer);
            Pump();
            layout.BuildPaneMenu(explorer).Select(r => (string?)r.Header).ShouldBe(["Close", "Float", "Pin", "Dock to"]);
            layout.Float(explorer);
            Pump();
            explorer.State.ShouldBe(DockPaneState.Floating);
            layout.Workspace.LeftHidden.ShouldBeEmpty();
            layout.Flyout!.IsOpen.ShouldBeFalse();
            layout.FloatsPanel!.Children.OfType<DockFloatingCard>().Count().ShouldBe(2);
            explorer.GetVisualRoot().ShouldBe(window);
            // Without a docked place to go back to, Dock uses its preferred side.
            layout.Dock(explorer);
            explorer.State.ShouldBe(DockPaneState.Docked);
            explorer.Group!.Panes.ShouldContain(Pane(layout, "search"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Drag_Restores_A_Maximized_Layout()
    {
        var layout = Spec();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            var terminal = Pane(layout, "terminal");
            layout.ToggleMaximize(terminal.Group!);
            Pump();
            var tab = DockLayoutViewTests.TabOf(layout, "terminal");
            var from = DockPointer.CenterOf(tab, window);
            DockPointer.Press(window, from);
            DockPointer.Move(window, from, from + new Point(0, 200));
            layout.MaximizedGroup.ShouldBeNull();
            layout.Classes.ShouldContain(":dragging");
            // The whole layout is back to aim at: the explorer's group shows its compass.
            var explorerView = DockLayoutViewTests.ViewOf(Pane(layout, "explorer").Group!);
            explorerView.GetVisualRoot().ShouldBe(window);
            var over = DockPointer.At(explorerView, window, explorerView.Bounds.Width / 2, explorerView.Bounds.Height * 0.6);
            DockPointer.Move(window, from + new Point(0, 200), over);
            var target = DockPointer.ButtonCenter(layout, window, DockCompassSlot.Center);
            DockPointer.Move(window, over, target, 2);
            DockPointer.Release(window, target);
            terminal.Group.ShouldBe(Pane(layout, "explorer").Group);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Switcher_Lists_Floating_Panes_And_Brings_Their_Card_To_The_Front()
    {
        var layout = Spec();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            var terminal = Pane(layout, "terminal");
            var search = Pane(layout, "search");
            layout.Float(terminal);
            layout.Float(search);
            layout.Activate(Pane(layout, "doc:readme"));
            Pump();
            var terminalCard = (DockFloatingCard)layout.HostOf(terminal.Group!.Float!)!;
            var searchCard = (DockFloatingCard)layout.HostOf(search.Group!.Float!)!;
            searchCard.ZIndex.ShouldBeGreaterThan(terminalCard.ZIndex);
            layout.RecentPanes().ShouldContain(terminal);
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            var switcher = layout.Switcher!;
            while (!ReferenceEquals(switcher.Selected, terminal))
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Control);
            }
            window.KeyReleaseQwerty(PhysicalKey.ControlLeft, RawInputModifiers.None);
            Pump();
            layout.ActivePane.ShouldBe(terminal);
            terminalCard.ZIndex.ShouldBeGreaterThan(searchCard.ZIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ShowFloatAndClose(DockFloatingMode mode)
    {
        var layout = Spec(mode);
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        layout.Float(Pane(layout, "terminal"));
        layout.Float(Pane(layout, "search"));
        Pump();
        var hosts = layout.FloatHosts.Select(h => new WeakReference(h)).ToArray();
        layout.Activate(Pane(layout, "terminal"));
        Pump();
        window.Content = null;
        window.Close();
        Pump();
        return [new WeakReference(layout), .. hosts];
    }

    [AvaloniaTheory]
    [InlineData(DockFloatingMode.Overlay)]
    [InlineData(DockFloatingMode.Window)]
    public void A_Closed_Layout_With_Floats_Is_Collected(DockFloatingMode mode)
    {
        var refs = ShowFloatAndClose(mode);
        TestHelpers.Collect(refs);
        refs.Where(r => r.IsAlive).Select(r => r.Target?.GetType().Name).ShouldBeEmpty();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] FloatAndCloseIt(DockLayout layout)
    {
        var terminal = Pane(layout, "terminal");
        layout.Float(terminal);
        Pump();
        var dock = terminal.Group!.Float!;
        var refs = new[] { new WeakReference(layout.HostOf(dock)), new WeakReference(dock), new WeakReference(terminal.Group.View) };
        layout.CloseFloat(dock);
        Pump();
        return refs;
    }

    [AvaloniaTheory]
    [InlineData(DockFloatingMode.Overlay)]
    [InlineData(DockFloatingMode.Window)]
    public void A_Closed_Floating_Window_Or_Card_Is_Collected_While_The_Layout_Lives(DockFloatingMode mode)
    {
        var layout = Spec(mode);
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            var refs = FloatAndCloseIt(layout);
            layout.Workspace.Floats.ShouldBeEmpty();
            TestHelpers.Collect(refs);
            refs.Where(r => r.IsAlive).Select(r => r.Target?.GetType().Name).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_Build_Their_Icon_And_Close_Slot_Only_When_Needed()
    {
        var layout = Spec();
        var window = DockLayoutViewTests.Show(layout, 1200, 700);
        try
        {
            // The search tool has no icon and no close button: nothing but its title.
            var search = DockLayoutViewTests.TabOf(layout, "search");
            search.GetVisualDescendants().OfType<ArcGlyph>().ShouldBeEmpty();
            search.CloseButton.ShouldBeNull();
            search.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().ShouldBeEmpty();
            // The explorer has an icon; setting one later builds it then.
            DockLayoutViewTests.TabOf(layout, "explorer").GetVisualDescendants().OfType<ArcGlyph>().ShouldHaveSingleItem();
            Pane(layout, "search").Icon = ArcIcons.Search;
            Pump();
            search.GetVisualDescendants().OfType<ArcGlyph>().ShouldHaveSingleItem();

            // Documents: the selected one shows its close button, the other only on hover; modified shows the dot instead.
            var readme = DockLayoutViewTests.TabOf(layout, "doc:readme");
            var app = DockLayoutViewTests.TabOf(layout, "doc:app");
            layout.Activate(Pane(layout, "doc:readme"));
            Pump();
            readme.CloseButton.ShouldNotBeNull().IsVisible.ShouldBeTrue();
            app.CloseButton.ShouldNotBeNull().IsVisible.ShouldBeFalse();
            Pane(layout, "doc:readme").IsModified = true;
            Pump();
            readme.CloseButton!.IsVisible.ShouldBeFalse();
            readme.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single().IsVisible.ShouldBeTrue();
            window.MouseMove(DockPointer.CenterOf(readme, window));
            Pump();
            readme.CloseButton.IsVisible.ShouldBeTrue();
            readme.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single().IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
