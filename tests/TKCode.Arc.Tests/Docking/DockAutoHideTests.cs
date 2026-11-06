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

/// <summary>Auto hide (spec 5.4, 7, 8, 11, 12, 15): unpinning, the strips, the flyout, retracting and pinning back.</summary>
public class DockAutoHideTests
{
    private static (DockLayout Layout, Window Window) ShowSpec(double width = 1400, double height = 700)
    {
        var layout = DockLayoutViewTests.LoadSpec();
        return (layout, DockLayoutViewTests.Show(layout, width, height));
    }

    private static DockPane Pane(DockLayout layout, string id) => DockLayoutViewTests.Pane(layout, id);

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Visual target, Point? at = null)
    {
        Settle();
        var point = target.TranslatePoint(at ?? new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static IInputElement? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [AvaloniaFact]
    public void Auto_Hide_Moves_A_Tool_To_The_Strip_Nearest_Its_Group()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var changes = new List<DockLayoutChangedEventArgs>();
            layout.LayoutChanged += (_, e) => changes.Add(e);
            var explorer = Pane(layout, "explorer");
            var group = explorer.Group!;
            layout.AutoHide(explorer).ShouldBeTrue();
            Settle();

            explorer.State.ShouldBe(DockPaneState.AutoHidden);
            explorer.IsPinned.ShouldBeFalse();
            explorer.Group.ShouldBeNull();
            explorer.Classes.ShouldContain(":auto-hidden");
            layout.Workspace.LeftHidden.ShouldBe([explorer]);
            group.Panes.Select(p => p.Id).ShouldBe(["search"]);
            changes.ShouldContain(c => c.Kind == DockLayoutChangeKind.AutoHide && c.Pane == explorer);
            layout.LastAnnouncement.ShouldBe("Explorer auto hidden");

            // The left strip shows one 26px button; the others take no room.
            var strip = layout.StripOf(DockSide.Left).ShouldNotBeNull();
            strip.IsVisible.ShouldBeTrue();
            strip.Bounds.Width.ShouldBe(32);
            var button = strip.Buttons.Single();
            button.Pane.ShouldBe(explorer);
            button.Bounds.Width.ShouldBe(26);
            button.Bounds.Height.ShouldBeGreaterThan(button.Bounds.Width);
            button.Classes.ShouldContain(":vertical");
            Avalonia.Automation.AutomationProperties.GetName(button).ShouldBe("Explorer");
            Avalonia.Automation.AutomationProperties.GetHelpText(button).ShouldBe("Auto hidden, press Enter to show");
            layout.StripOf(DockSide.Right)!.IsVisible.ShouldBeFalse();
            layout.StripOf(DockSide.Bottom)!.IsVisible.ShouldBeFalse();
            // The host moved right to make room for the strip.
            layout.Host!.TranslatePoint(new Point(), layout)!.Value.X.ShouldBe(6 + 32);

            // The terminal's group sits at the bottom: its strip is the bottom one, a row.
            var terminal = Pane(layout, "terminal");
            layout.AutoHide(terminal).ShouldBeTrue();
            Settle();
            layout.Workspace.BottomHidden.ShouldBe([terminal]);
            var bottom = layout.StripOf(DockSide.Bottom)!;
            bottom.IsVisible.ShouldBeTrue();
            bottom.Bounds.Height.ShouldBe(32);
            bottom.Buttons.Single().Bounds.Height.ShouldBe(26);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Documents_Floating_Panes_And_Panes_That_Cannot_Auto_Hide_Stay()
    {
        var (layout, window) = ShowSpec();
        try
        {
            layout.AutoHide(Pane(layout, "doc:readme")).ShouldBeFalse();
            var search = Pane(layout, "search");
            search.CanAutoHide = false;
            layout.AutoHide(search).ShouldBeFalse();
            search.State.ShouldBe(DockPaneState.Docked);
            // An auto-hidden pane cannot auto-hide again.
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer).ShouldBeTrue();
            layout.AutoHide(explorer).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Strip_Button_Slides_The_Pane_Out_With_Focus_Inside()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            var dockedWidth = explorer.Group!.View!.Bounds.Width;
            layout.AutoHide(explorer);
            Settle();
            explorer.GetVisualRoot().ShouldBeNull();
            var button = layout.StripOf(DockSide.Left)!.Buttons.Single();
            Click(window, button);

            var flyout = layout.Flyout.ShouldNotBeNull();
            flyout.IsOpen.ShouldBeTrue();
            flyout.Pane.ShouldBe(explorer);
            flyout.Card.IsVisible.ShouldBeTrue();
            // As wide as the pane was docked, along the left edge, over the content.
            flyout.Card.Bounds.Width.ShouldBe(dockedWidth, 0.5);
            flyout.Card.Bounds.X.ShouldBe(0);
            flyout.Card.Bounds.Height.ShouldBe(layout.Host!.Bounds.Height);
            explorer.GetVisualRoot().ShouldBe(window);
            layout.ActivePane.ShouldBe(explorer);
            button.Classes.ShouldContain(":selected");
            // Focus moved into the pane's content.
            Focused(window).ShouldBe(explorer.Content);
            // The title and the pin button are in the header.
            flyout.Card.Title.ShouldBe("Explorer");
            flyout.Card.PinButton.ShouldNotBeNull().IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Click_Outside_Retracts_The_Flyout_And_Gives_Back_The_Active_Pane()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var readme = Pane(layout, "doc:readme");
            layout.Activate(readme);
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            Settle();
            Click(window, layout.StripOf(DockSide.Left)!.Buttons.Single());
            layout.Flyout!.IsOpen.ShouldBeTrue();

            // A press on the documents (to the right of the card).
            var documents = readme.Group!.View!;
            Click(window, documents, new Point(documents.Bounds.Width - 40, documents.Bounds.Height - 40));
            layout.Flyout.IsOpen.ShouldBeFalse();
            layout.Flyout.Card.IsVisible.ShouldBeFalse();
            explorer.GetVisualRoot().ShouldBeNull();
            layout.ActivePane.ShouldBe(readme);
            layout.StripOf(DockSide.Left)!.Buttons.Single().Classes.ShouldNotContain(":selected");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_Retracts_And_Returns_Focus_To_The_Strip_Button()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            Settle();
            var button = layout.StripOf(DockSide.Left)!.Buttons.Single();
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            layout.Flyout!.IsOpen.ShouldBeTrue();
            Focused(window).ShouldBe(explorer.Content);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle();
            layout.Flyout.IsOpen.ShouldBeFalse();
            Focused(window).ShouldBe(button);
            TestHelpers.IsFocusRingVisible(button).ShouldBeTrue();

            // Enter again shows it; clicking the button while it is out retracts it.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle();
            layout.Flyout.IsOpen.ShouldBeTrue();
            Click(window, button);
            layout.Flyout.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Leaving_The_Flyout_Retracts_It()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            Settle();
            layout.Activate(explorer);
            Settle();
            layout.Flyout!.IsOpen.ShouldBeTrue();
            DockLayoutViewTests.TabOf(layout, "terminal").Focus(NavigationMethod.Tab);
            Settle();
            layout.Flyout.IsOpen.ShouldBeFalse();
            layout.ActivePane.ShouldBe(Pane(layout, "terminal"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pin_Returns_The_Pane_To_Its_Group_At_Its_Old_Place()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var changes = new List<DockLayoutChangeKind>();
            layout.LayoutChanged += (_, e) => changes.Add(e.Kind);
            var explorer = Pane(layout, "explorer");
            var group = explorer.Group!;
            layout.AutoHide(explorer);
            Settle();
            layout.Activate(explorer);
            Settle();
            // The pin button in the flyout's header.
            var pin = layout.Flyout!.Card.PinButton!;
            Click(window, pin);
            explorer.State.ShouldBe(DockPaneState.Docked);
            explorer.IsPinned.ShouldBeTrue();
            explorer.Group.ShouldBe(group);
            group.Panes.Select(p => p.Id).ShouldBe(["explorer", "search"]);
            group.ActivePane.ShouldBe(explorer);
            layout.Workspace.LeftHidden.ShouldBeEmpty();
            layout.StripOf(DockSide.Left)!.IsVisible.ShouldBeFalse();
            layout.Flyout.IsOpen.ShouldBeFalse();
            changes.ShouldContain(DockLayoutChangeKind.Pin);
            explorer.GetVisualRoot().ShouldBe(window);
            // Focus was in the flyout: it follows the pane.
            Focused(window).ShouldBe(explorer.Content);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pin_Makes_A_New_Group_On_The_Strip_Side_When_The_Old_Group_Is_Gone()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var terminal = Pane(layout, "terminal");
            var height = terminal.Group!.View!.Bounds.Height;
            layout.AutoHide(terminal).ShouldBeTrue();
            Settle();
            // The terminal was alone: its group went away and the documents took the room.
            layout.Workspace.Groups().Count().ShouldBe(2);
            layout.Pin(terminal).ShouldBeTrue();
            Settle();
            terminal.State.ShouldBe(DockPaneState.Docked);
            var root = layout.Workspace.Root.ShouldBeOfType<DockSplit>();
            root.Orientation.ShouldBe(Orientation.Vertical);
            root.Children[^1].ShouldBe(terminal.Group);
            terminal.Group!.View!.Bounds.Height.ShouldBe(height, 12);
            layout.LastAnnouncement.ShouldBe("Terminal docked to the bottom edge");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Flyout_Covers_At_Most_Seventy_Percent_And_Its_Inner_Edge_Resizes_It()
    {
        var (layout, window) = ShowSpec(1000, 600);
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            explorer.AutoHideExtent = 5000;
            Settle();
            layout.Activate(explorer);
            Settle();
            var flyout = layout.Flyout!;
            var host = layout.Host!.Bounds.Width;
            flyout.Card.Bounds.Width.ShouldBe(host * 0.7, 1);

            explorer.AutoHideExtent = 240;
            layout.Activate(Pane(layout, "doc:readme"));
            Settle();
            layout.Activate(explorer);
            Settle();
            flyout.Card.Bounds.Width.ShouldBe(240);
            // Drag the grip on the inner edge 60px further in.
            var grip = flyout.Grip;
            grip.IsVisible.ShouldBeTrue();
            var start = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, 100), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(60, 0), RawInputModifiers.LeftMouseButton);
            window.MouseUp(start + new Point(60, 0), MouseButton.Left);
            Settle();
            flyout.Card.Bounds.Width.ShouldBe(300, 0.5);
            explorer.AutoHideExtent.ShouldBe(300, 0.5);
            flyout.IsOpen.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Escape_Auto_Hides_A_Tool_And_Retracts_An_Auto_Hidden_One()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            var box = explorer.Content.ShouldBeOfType<TextBox>();
            box.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.Shift);
            Settle();
            explorer.State.ShouldBe(DockPaneState.AutoHidden);
            // Focus went to the pane that took over in the group.
            layout.ActivePane.ShouldBe(Pane(layout, "search"));
            Focused(window).ShouldBe(DockLayoutViewTests.TabOf(layout, "search"));

            layout.Activate(explorer);
            Settle();
            Focused(window).ShouldBe(box);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.Shift);
            Settle();
            layout.Flyout!.IsOpen.ShouldBeFalse();
            explorer.State.ShouldBe(DockPaneState.AutoHidden);
            Focused(window).ShouldBe(layout.StripOf(DockSide.Left)!.Buttons.Single());

            // Documents do nothing.
            DockLayoutViewTests.TabOf(layout, "doc:readme").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.Shift);
            Settle();
            Pane(layout, "doc:readme").State.ShouldBe(DockPaneState.Docked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Menu_Offers_Auto_Hide_For_Tools_And_Pin_For_Auto_Hidden_Panes()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.BuildPaneMenu(Pane(layout, "doc:readme")).ShouldNotContain(r => (string?)r.Header == "Auto hide");
            var autoHide = layout.BuildPaneMenu(explorer).Single(r => (string?)r.Header == "Auto hide");
            autoHide.IsEnabled.ShouldBeTrue();
            autoHide.RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Settle();
            explorer.State.ShouldBe(DockPaneState.AutoHidden);

            // The strip button's menu: Close, Float, Pin, Dock to.
            var button = layout.StripOf(DockSide.Left)!.Buttons.Single();
            button.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.F10, RawInputModifiers.Shift);
            Settle();
            var menu = layout.Menu.ShouldNotBeNull();
            menu.IsOpen.ShouldBeTrue();
            menu.Items.Cast<MenuEntry>().Select(e => e.Header).ShouldBe(["Close", "Float", "Pin", "Dock to"]);
            menu.Close();
            layout.BuildPaneMenu(explorer).Single(r => (string?)r.Header == "Pin").RaiseEvent(new RoutedEventArgs(MenuEntry.ClickEvent));
            Settle();
            explorer.State.ShouldBe(DockPaneState.Docked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Locked_Layout_Shows_Auto_Hidden_Panes_But_Does_Not_Unpin_Or_Pin()
    {
        var (layout, window) = ShowSpec();
        try
        {
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            layout.IsLocked = true;
            Settle();
            layout.BuildPaneMenu(Pane(layout, "terminal")).Single(r => (string?)r.Header == "Auto hide").IsEnabled.ShouldBeFalse();
            layout.BuildPaneMenu(explorer).Single(r => (string?)r.Header == "Pin").IsEnabled.ShouldBeFalse();
            layout.AutoHideActiveCommand.CanExecute(null).ShouldBeFalse();
            Click(window, layout.StripOf(DockSide.Left)!.Buttons.Single());
            layout.Flyout!.IsOpen.ShouldBeTrue();
            layout.Flyout.Card.PinButton!.IsVisible.ShouldBeFalse();
            layout.Flyout.Grip.IsVisible.ShouldBeFalse();
            // Shift+Escape on a docked tool does nothing while locked.
            DockLayoutViewTests.TabOf(layout, "terminal").Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.Shift);
            Settle();
            Pane(layout, "terminal").State.ShouldBe(DockPaneState.Docked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Auto_Hidden_Panes_Survive_Save_And_Restore()
    {
        var (layout, window) = ShowSpec();
        string json;
        try
        {
            layout.AutoHide(Pane(layout, "explorer"));
            layout.AutoHide(Pane(layout, "terminal"));
            json = layout.SaveLayout();
        }
        finally
        {
            window.Close();
        }
        var (other, otherWindow) = ShowSpec();
        try
        {
            other.LoadLayout(json).ShouldBeTrue();
            Settle();
            other.Workspace.LeftHidden.Select(p => p.Id).ShouldBe(["explorer"]);
            other.Workspace.BottomHidden.Select(p => p.Id).ShouldBe(["terminal"]);
            other.StripOf(DockSide.Left)!.Buttons.Single().Pane.Id.ShouldBe("explorer");
            other.StripOf(DockSide.Bottom)!.IsVisible.ShouldBeTrue();
            // Pinning after a restore docks along the strip's edge.
            other.Pin(Pane(other, "explorer")).ShouldBeTrue();
            Pane(other, "explorer").State.ShouldBe(DockPaneState.Docked);
        }
        finally
        {
            otherWindow.Close();
        }
    }

    [AvaloniaFact]
    public void The_Flyout_Slides_In_On_A_Spring_And_Out_In_A_Tween()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var layout = DockLayoutViewTests.LoadSpec();
        var window = TestHelpers.Show(layout, Motion.ArcMotionLevel.Full);
        window.Width = 1400;
        window.Height = 700;
        try
        {
            Settle();
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            Settle();
            layout.Activate(explorer);
            Settle();
            var flyout = layout.Flyout!;
            // It starts past the left edge (shadow included) and springs to its place.
            var shift = (Avalonia.Media.TranslateTransform)flyout.Card.RenderTransform!;
            shift.X.ShouldBeLessThan(-flyout.Card.Bounds.Width);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            shift.X.ShouldBeInRange(-flyout.Card.Bounds.Width, -1);
            clock.Advance(TimeSpan.FromSeconds(1));
            Math.Abs(shift.X).ShouldBeLessThan(0.5);

            layout.Activate(Pane(layout, "doc:readme"));
            Settle();
            flyout.IsOpen.ShouldBeFalse();
            // Still on screen while it slides out (180ms), gone afterwards.
            clock.Advance(TimeSpan.FromMilliseconds(100));
            flyout.Card.IsVisible.ShouldBeTrue();
            shift.X.ShouldBeLessThan(-1);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            flyout.Card.IsVisible.ShouldBeFalse();
            explorer.GetVisualRoot().ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Reduced_Motion_The_Flyout_Fades()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var layout = DockLayoutViewTests.LoadSpec();
        var window = TestHelpers.Show(layout, Motion.ArcMotionLevel.Reduced);
        window.Width = 1400;
        window.Height = 700;
        try
        {
            Settle();
            var explorer = Pane(layout, "explorer");
            layout.AutoHide(explorer);
            layout.Activate(explorer);
            Settle();
            var card = layout.Flyout!.Card;
            ((Avalonia.Media.TranslateTransform)card.RenderTransform!).X.ShouldBe(0);
            card.Opacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            card.Opacity.ShouldBe(1);
            layout.Activate(Pane(layout, "doc:readme"));
            clock.Advance(TimeSpan.FromMilliseconds(60));
            card.IsVisible.ShouldBeTrue();
            card.Opacity.ShouldBeInRange(0.01, 0.99);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            card.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
