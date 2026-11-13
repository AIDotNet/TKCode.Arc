using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The tool dock: geometry, choosing tools and group members, trays, the floating label, the keyboard toolbar model,
/// reordering, badges, accessibility and the measured motion.
/// </summary>
public class ToolDockTests
{
    private static ToolDock Dock(bool reorder = false)
    {
        var dock = new ToolDock { Margin = new Thickness(40, 120, 0, 0), SelectedValue = "select", CanReorder = reorder };
        dock.Items.Add(new ToolDockItem { Value = "select", Label = "Select", Shortcut = "V", Glyph = ArcIcons.Move });
        dock.Items.Add(new ToolDockItem { Value = "pan", Label = "Pan", Shortcut = "H", Glyph = ArcIcons.Hash });
        var shapes = new ToolDockItem { Value = "shapes", Label = "Shapes" };
        shapes.Items.Add(new ToolDockItem { Value = "rectangle", Label = "Rectangle", Shortcut = "R", Glyph = ArcIcons.Square });
        shapes.Items.Add(new ToolDockItem { Value = "ellipse", Label = "Ellipse", Shortcut = "O", Glyph = ArcIcons.Circle });
        shapes.Items.Add(new ToolDockItem { Value = "star", Label = "Star", Glyph = ArcIcons.Star });
        shapes.Items.Add(new ToolDockItem { Value = "box", Label = "Box", Glyph = ArcIcons.Box });
        dock.Items.Add(shapes);
        dock.Items.Add(new ToolDockItem { Value = "text", Label = "Text", Shortcut = "T", Glyph = ArcIcons.Type });
        dock.Items.Add(new ToolDockItem { Value = "pen", Label = "Pen", Shortcut = "P", Glyph = ArcIcons.Pencil });
        dock.Items.Add(new ToolDockItem { Value = "comment", Label = "Comment", Shortcut = "C", Glyph = ArcIcons.MessageCircle, Badge = 2 });
        return dock;
    }

    private static ToolDockItem Slot(ToolDock dock, int index) => (ToolDockItem)dock.ContainerFromIndex(index)!;

    private static Point Center(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root)!.Value;

    private static void Click(Window window, Visual target)
    {
        var point = Center(target, window);
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Key(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static IDisposable InstantLabel()
    {
        var hover = ToolDock.HoverDelay;
        var focus = ToolDock.FocusDelay;
        var leave = ToolDock.LeaveDelay;
        var focusLeave = ToolDock.FocusLeaveDelay;
        ToolDock.HoverDelay = TimeSpan.Zero;
        ToolDock.FocusDelay = TimeSpan.Zero;
        ToolDock.LeaveDelay = TimeSpan.Zero;
        ToolDock.FocusLeaveDelay = TimeSpan.Zero;
        return new Restore(() =>
        {
            ToolDock.HoverDelay = hover;
            ToolDock.FocusDelay = focus;
            ToolDock.LeaveDelay = leave;
            ToolDock.FocusLeaveDelay = focusLeave;
        });
    }

    private sealed class Restore(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    [AvaloniaFact]
    public void Slots_are_40_px_four_apart_inside_a_6_px_padding()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            dock.Bounds.Width.ShouldBe(272);
            dock.Bounds.Height.ShouldBe(52);
            for (var i = 0; i < 6; i++)
            {
                var rect = dock.SlotRect(Slot(dock, i));
                rect.X.ShouldBe(6 + i * 44);
                rect.Y.ShouldBe(6);
                rect.Width.ShouldBe(40);
                rect.Height.ShouldBe(40);
            }
            dock.Surface!.BarCornerRadius.TopLeft.ShouldBe(20);
            dock.Surface.StrokeThickness.ShouldBe(0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_value_selects_a_slot_or_the_group_that_holds_it()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            Slot(dock, 0).IsSelected.ShouldBeTrue();
            Slot(dock, 0).Classes.Contains(":selected").ShouldBeTrue();
            var shapes = Slot(dock, 2);
            shapes.IsGroup.ShouldBeTrue();
            shapes.ShownItem!.Label.ShouldBe("Rectangle");
            shapes.Classes.Contains(":group").ShouldBeTrue();
            dock.SelectedValue = "ellipse";
            Dispatcher.UIThread.RunJobs();
            Slot(dock, 0).IsSelected.ShouldBeFalse();
            shapes.IsSelected.ShouldBeTrue();
            shapes.ShownItem!.Label.ShouldBe("Ellipse");
            shapes.GlyphView!.Data.ShouldBeSameAs(ArcIcons.Circle);
            // Another tool: the group keeps showing the member chosen last.
            dock.SelectedValue = "text";
            Dispatcher.UIThread.RunJobs();
            shapes.IsSelected.ShouldBeFalse();
            shapes.ShownItem!.Label.ShouldBe("Ellipse");
            Slot(dock, 3).IsSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_a_slot_chooses_it_and_reports_the_choice()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            ToolDockItemEventArgs? chosen = null;
            dock.ItemInvoked += (_, e) => chosen = e;
            Click(window, Slot(dock, 4));
            dock.SelectedValue.ShouldBe("pen");
            chosen.ShouldNotBeNull();
            chosen.Item.ShouldBeSameAs(Slot(dock, 4));
            chosen.Value.ShouldBe("pen");
            Slot(dock, 4).IsSelected.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_group_selects_its_member_and_opens_a_tray_centred_above_it()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            var shapes = Slot(dock, 2);
            Click(window, shapes);
            dock.SelectedValue.ShouldBe("rectangle");
            shapes.IsTrayOpen.ShouldBeTrue();
            dock.OpenGroup.ShouldBeSameAs(shapes);
            var tray = dock.Tray!;
            tray.IsVisible.ShouldBeTrue();
            // 4 members of 36, 4 apart, inside a 6 padding: 168 x 48, centred over the slot (centre 114), bottom on the bar.
            tray.Placement.ShouldBe(new Rect(30, -48, 168, 48));
            dock.Surface!.Tab.ShouldBe(new Rect(30, -48, 168, 48));
            tray.Opacity.ShouldBe(1);
            tray.Buttons.Select(b => b.Member.Label).ShouldBe(["Rectangle", "Ellipse", "Star", "Box"]);
            tray.Buttons.First().IsSelected.ShouldBeTrue();
            // Choosing a member selects it and closes the tray.
            Click(window, tray.Buttons.ElementAt(1));
            dock.SelectedValue.ShouldBe("ellipse");
            shapes.IsTrayOpen.ShouldBeFalse();
            dock.OpenGroup.ShouldBeNull();
            dock.Surface.HasTab.ShouldBeFalse();
            tray.IsVisible.ShouldBeFalse();
            shapes.GlyphView!.Data.ShouldBeSameAs(ArcIcons.Circle);
            // Clicking the group again opens it; again closes it.
            Click(window, shapes);
            shapes.IsTrayOpen.ShouldBeTrue();
            Click(window, shapes);
            shapes.IsTrayOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_tray_keeps_30_px_from_the_ends_of_the_bar()
    {
        var dock = Dock();
        var shapes = (ToolDockItem)dock.Items[2]!;
        dock.Items.RemoveAt(2);
        dock.Items.Insert(0, shapes);
        var window = TestHelpers.Show(dock);
        try
        {
            dock.OpenTray(Slot(dock, 0), focusMember: false);
            Dispatcher.UIThread.RunJobs();
            dock.Tray!.Placement.X.ShouldBe(30);
            dock.CloseTray();
            dock.Items.RemoveAt(0);
            dock.Items.Add(shapes);
            Dispatcher.UIThread.RunJobs();
            dock.OpenTray(Slot(dock, 5), focusMember: false);
            Dispatcher.UIThread.RunJobs();
            (272 - dock.Tray.Placement.Right).ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_outside_the_dock_closes_the_tray()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            Click(window, Slot(dock, 2));
            dock.OpenGroup.ShouldNotBeNull();
            window.MouseDown(new Point(380, 280), MouseButton.Left);
            window.MouseUp(new Point(380, 280), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dock.OpenGroup.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_dock_is_a_toolbar_with_one_tab_stop_and_wrapping_arrows()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            dock.Slots.Count(s => s.IsTabStop).ShouldBe(1);
            Slot(dock, 0).IsTabStop.ShouldBeTrue();
            Slot(dock, 0).Focus(NavigationMethod.Tab);
            var focus = window.FocusManager!;
            Key(window, PhysicalKey.ArrowLeft);
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 5));
            Key(window, PhysicalKey.ArrowRight);
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 0));
            Key(window, PhysicalKey.End);
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 5));
            Key(window, PhysicalKey.Home);
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 0));
            Key(window, PhysicalKey.ArrowRight);
            dock.Slots.Single(s => s.IsTabStop).ShouldBeSameAs(Slot(dock, 1));
            Key(window, PhysicalKey.Enter);
            dock.SelectedValue.ShouldBe("pan");
            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.Space);
            dock.SelectedValue.ShouldBe("rectangle");
            Slot(dock, 2).IsTrayOpen.ShouldBeTrue();
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 2));
            Key(window, PhysicalKey.Escape);
            Slot(dock, 2).IsTrayOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(Slot(dock, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Up_opens_the_tray_on_the_chosen_member_and_the_tray_keys_work_inside_it()
    {
        var dock = Dock();
        dock.SelectedValue = "ellipse";
        var window = TestHelpers.Show(dock);
        try
        {
            var shapes = Slot(dock, 2);
            shapes.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowUp);
            shapes.IsTrayOpen.ShouldBeTrue();
            var focus = window.FocusManager!;
            var buttons = dock.Tray!.Buttons.ToArray();
            focus.GetFocusedElement().ShouldBeSameAs(buttons[1]);
            Key(window, PhysicalKey.ArrowRight);
            focus.GetFocusedElement().ShouldBeSameAs(buttons[2]);
            Key(window, PhysicalKey.End);
            Key(window, PhysicalKey.ArrowRight);
            focus.GetFocusedElement().ShouldBeSameAs(buttons[0]);
            Key(window, PhysicalKey.ArrowDown);
            shapes.IsTrayOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(shapes);
            Key(window, PhysicalKey.ArrowUp);
            Key(window, PhysicalKey.ArrowRight);
            Key(window, PhysicalKey.Enter);
            dock.SelectedValue.ShouldBe("star");
            shapes.IsTrayOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(shapes);
            shapes.ShownItem!.Label.ShouldBe("Star");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Names_carry_the_key_hint_the_badge_and_the_shown_member()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            var dockPeer = ControlAutomationPeer.CreatePeerForElement(dock);
            dockPeer.GetAutomationControlType().ShouldBe(AutomationControlType.ToolBar);
            dockPeer.GetName().ShouldBe("Tools");
            var pen = ControlAutomationPeer.CreatePeerForElement(Slot(dock, 4));
            pen.GetName().ShouldBe("Pen, P");
            pen.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            pen.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
            pen.GetProvider<IExpandCollapseProvider>().ShouldBeNull();
            ControlAutomationPeer.CreatePeerForElement(Slot(dock, 5)).GetName().ShouldBe("Comment, C, 2");
            var shapes = ControlAutomationPeer.CreatePeerForElement(Slot(dock, 2));
            shapes.GetName().ShouldBe("Shapes: Rectangle, R");
            shapes.GetProvider<IToggleProvider>().ShouldBeNull();
            var expand = shapes.GetProvider<IExpandCollapseProvider>()!;
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            dock.SelectedValue = "ellipse";
            Dispatcher.UIThread.RunJobs();
            shapes.GetName().ShouldBe("Shapes: Ellipse, O, selected");
            expand.Expand();
            Dispatcher.UIThread.RunJobs();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
            var member = ControlAutomationPeer.CreatePeerForElement(dock.Tray!.Buttons.ElementAt(1));
            member.GetName().ShouldBe("Ellipse, O");
            member.GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.On);
            ControlAutomationPeer.CreatePeerForElement(dock.Tray).GetName().ShouldBe("Shapes");
            ControlAutomationPeer.CreatePeerForElement(Slot(dock, 0)).GetProvider<IToggleProvider>()!.ToggleState.ShouldBe(ToggleState.Off);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_label_shows_name_and_key_for_the_mouse_and_the_keyboard_but_not_for_touch()
    {
        using var _ = InstantLabel();
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            window.MouseMove(Center(Slot(dock, 4), window));
            Dispatcher.UIThread.RunJobs();
            var label = dock.LabelPart!;
            label.IsShown.ShouldBeTrue();
            label.Text.ShouldBe("Pen");
            label.Shortcut.ShouldBe("P");
            // Centred over the slot, its bottom 10 px above the bar.
            label.PillRect.Center.X.ShouldBe(dock.SlotRect(Slot(dock, 4)).Center.X, 0.01);
            label.PillRect.Bottom.ShouldBe(-10, 0.01);
            window.MouseMove(Center(Slot(dock, 2), window));
            Dispatcher.UIThread.RunJobs();
            label.Text.ShouldBe("Shapes: Rectangle");
            label.Shortcut.ShouldBe("R");
            // Leaving hides it; a click hides it too.
            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();
            label.IsShown.ShouldBeFalse();
            Click(window, Slot(dock, 1));
            label.IsShown.ShouldBeFalse();
            // Keyboard focus shows it.
            Slot(dock, 3).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            label.IsShown.ShouldBeTrue();
            label.Text.ShouldBe("Text");
            // Touch never moves it, nor shows the hover fill.
            var slot = Slot(dock, 0);
            var touch = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
            slot.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, slot, touch, window, new Point(5, 5), 0,
                new PointerPointProperties(), KeyModifiers.None));
            Dispatcher.UIThread.RunJobs();
            label.Text.ShouldBe("Text");
            slot.Classes.Contains(":hover-fine").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_label_waits_on_the_first_hover_and_hides_while_the_group_is_open()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            window.MouseMove(Center(Slot(dock, 0), window));
            Dispatcher.UIThread.RunJobs();
            (dock.LabelPart?.IsShown ?? false).ShouldBeFalse();
            TestHelpers.PumpUntil(() => dock.LabelPart?.IsShown == true, 3000).ShouldBeTrue();
            dock.LabelPart!.Text.ShouldBe("Select");
            using (InstantLabel())
            {
                dock.OpenTray(Slot(dock, 2), focusMember: false);
                window.MouseMove(Center(Slot(dock, 2), window));
                Dispatcher.UIThread.RunJobs();
                dock.LabelPart.IsShown.ShouldBeFalse();
                window.MouseMove(Center(dock.Tray!.Buttons.ElementAt(2), window));
                Dispatcher.UIThread.RunJobs();
                dock.LabelPart.IsShown.ShouldBeTrue();
                dock.LabelPart.Text.ShouldBe("Star");
                dock.LabelPart.PillRect.Bottom.ShouldBe(-48 - 10, 0.01);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alt_and_an_arrow_move_the_focused_slot_and_report_it()
    {
        var dock = Dock(reorder: true);
        var window = TestHelpers.Show(dock);
        try
        {
            var pan = Slot(dock, 1);
            ToolDockReorderedEventArgs? moved = null;
            dock.Reordered += (_, e) => moved = e;
            pan.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            dock.Items[2].ShouldBeSameAs(pan);
            moved.ShouldNotBeNull();
            moved.Item.ShouldBeSameAs(pan);
            moved.OldIndex.ShouldBe(1);
            moved.NewIndex.ShouldBe(2);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(pan);
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
            dock.Items[1].ShouldBeSameAs(pan);
            // Without CanReorder Alt + arrow does nothing.
            dock.CanReorder = false;
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            dock.Items[1].ShouldBeSameAs(pan);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_a_slot_past_a_neighbours_centre_swaps_them()
    {
        var dock = Dock(reorder: true);
        var window = TestHelpers.Show(dock);
        try
        {
            var pan = Slot(dock, 1);
            var moves = new List<(int, int)>();
            dock.Reordered += (_, e) => moves.Add((e.OldIndex, e.NewIndex));
            var start = Center(pan, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            for (var dx = 4; dx <= 30; dx += 2)
            {
                window.MouseMove(start + new Point(dx, 0), RawInputModifiers.LeftMouseButton);
            }
            Dispatcher.UIThread.RunJobs();
            dock.IsDragging.ShouldBeTrue();
            // Its right edge passed the next slot's centre after 24 px.
            moves.ShouldBe([(1, 2)]);
            dock.Items[2].ShouldBeSameAs(pan);
            // It follows the pointer: 30 px from where it started.
            (dock.SlotRect(pan).X + pan.SlotOffset).ShouldBe(6 + 44 + 30, 0.5);
            window.MouseUp(start + new Point(30, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            dock.IsDragging.ShouldBeFalse();
            pan.SlotOffset.ShouldBe(0);
            // A drag is not a click.
            dock.SelectedValue.ShouldBe("select");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_read_only_source_cannot_be_reordered()
    {
        var dock = new ToolDock { Margin = new Thickness(40, 120, 0, 0), CanReorder = true };
        dock.ItemsSource = new ReadOnlyCollection<ToolDockItem>(
        [
            new ToolDockItem { Value = "a", Label = "A", Glyph = ArcIcons.Plus },
            new ToolDockItem { Value = "b", Label = "B", Glyph = ArcIcons.Minus }
        ]);
        var window = TestHelpers.Show(dock);
        try
        {
            var first = Slot(dock, 0);
            first.Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
            dock.ContainerFromIndex(0).ShouldBeSameAs(first);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_observable_source_is_reordered_in_place()
    {
        var items = new ObservableCollection<ToolDockItem>
        {
            new() { Value = "a", Label = "A", Glyph = ArcIcons.Plus },
            new() { Value = "b", Label = "B", Glyph = ArcIcons.Minus },
            new() { Value = "c", Label = "C", Glyph = ArcIcons.X }
        };
        var dock = new ToolDock { Margin = new Thickness(40, 120, 0, 0), CanReorder = true, ItemsSource = items };
        var window = TestHelpers.Show(dock);
        try
        {
            Slot(dock, 2).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowLeft, RawInputModifiers.Alt);
            items.Select(i => (string)i.Value!).ShouldBe(["a", "c", "b"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_badge_shows_its_count_and_leaves_at_zero()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            var comment = Slot(dock, 5);
            var badge = comment.BadgePart!;
            badge.IsVisible.ShouldBeTrue();
            badge.Count.ShouldBe(2);
            badge.Bounds.Height.ShouldBe(16);
            badge.Bounds.X.ShouldBe(21);
            badge.Bounds.Y.ShouldBe(3);
            comment.Badge = 12;
            Dispatcher.UIThread.RunJobs();
            badge.DigitsValue.ShouldBe(12);
            comment.Badge = 0;
            Dispatcher.UIThread.RunJobs();
            badge.IsVisible.ShouldBeFalse();
            ControlAutomationPeer.CreatePeerForElement(comment).GetName().ShouldBe("Comment, C");
            comment.Badge = -3;
            comment.Badge.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_tint_slides_from_the_previous_slot_on_a_spring()
    {
        var frames = new ManualFrameClock();
        using var clock = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        try
        {
            dock.SelectedValue = "text";
            Dispatcher.UIThread.RunJobs();
            var text = Slot(dock, 3);
            // It starts where the old tint was, three slots to the left.
            text.TintOffset.ShouldBe(-132, 0.01);
            frames.Advance(TimeSpan.FromMilliseconds(120));
            text.TintOffset.ShouldBeInRange(-110, -20);
            frames.Advance(TimeSpan.FromMilliseconds(900));
            text.TintOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_tray_grows_out_of_its_slot_and_its_members_follow_with_a_stagger()
    {
        var frames = new ManualFrameClock();
        using var clock = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        try
        {
            dock.OpenTray(Slot(dock, 2), focusMember: false);
            Dispatcher.UIThread.RunJobs();
            var surface = dock.Surface!;
            // It starts as the group's slot, flat on the bar.
            surface.Tab.Height.ShouldBe(0, 0.01);
            surface.Tab.Width.ShouldBe(40, 0.01);
            dock.Tray!.Opacity.ShouldBe(0);
            frames.Advance(TimeSpan.FromMilliseconds(100));
            surface.Tab.Height.ShouldBeInRange(10, 40);
            var buttons = dock.Tray.Buttons.ToArray();
            // The first member started at 80 ms, the last waits until 185 ms.
            buttons[0].Opacity.ShouldBeGreaterThan(0);
            buttons[3].Opacity.ShouldBe(0);
            frames.Advance(TimeSpan.FromMilliseconds(1200));
            surface.Tab.ShouldBe(new Rect(30, -48, 168, 48));
            dock.Tray.Opacity.ShouldBe(1);
            buttons.ShouldAllBe(b => b.Opacity == 1);
            dock.CloseTray();
            frames.Advance(TimeSpan.FromMilliseconds(80));
            surface.Tab.Width.ShouldBe(168, 0.01);
            surface.Tab.Height.ShouldBeLessThan(48);
            frames.Advance(TimeSpan.FromMilliseconds(1000));
            Dispatcher.UIThread.RunJobs();
            surface.HasTab.ShouldBeFalse();
            dock.Tray.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_group_swaps_its_glyph_with_a_blur_and_reduced_motion_only_fades()
    {
        var frames = new ManualFrameClock();
        using var clock = frames.Install();
        var dock = Dock();
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        try
        {
            var glyph = Slot(dock, 2).GlyphView!;
            dock.SelectedValue = "ellipse";
            Dispatcher.UIThread.RunJobs();
            glyph.Data.ShouldBeSameAs(ArcIcons.Circle);
            glyph.FrontOpacity.ShouldBe(0);
            glyph.FrontBlur.ShouldBe(2, 0.01);
            frames.Advance(TimeSpan.FromMilliseconds(600));
            glyph.FrontOpacity.ShouldBe(1);
            glyph.FrontBlur.ShouldBe(0);
            glyph.BackOpacity.ShouldBe(0);
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            dock.SelectedValue = "star";
            Dispatcher.UIThread.RunJobs();
            glyph.FrontBlur.ShouldBe(0);
            frames.Advance(TimeSpan.FromMilliseconds(300));
            glyph.Data.ShouldBeSameAs(ArcIcons.Star);
            glyph.FrontOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_dips_the_slot_and_a_drag_lifts_it()
    {
        var frames = new ManualFrameClock();
        using var clock = frames.Install();
        var dock = Dock(reorder: true);
        var window = TestHelpers.Show(dock, ArcMotionLevel.Full);
        try
        {
            var pan = Slot(dock, 1);
            var start = Center(pan, window);
            window.MouseMove(start);
            window.MouseDown(start, MouseButton.Left);
            frames.Advance(TimeSpan.FromMilliseconds(200));
            pan.PressDip.ShouldBe(ToolDockItem.PressScale, 0.001);
            window.MouseMove(start + new Point(8, 0), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            frames.Advance(TimeSpan.FromMilliseconds(1200));
            pan.SlotLift.ShouldBe(ToolDockItem.LiftScale, 0.001);
            pan.ZIndex.ShouldBe(1);
            window.MouseUp(start + new Point(8, 0), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            frames.Advance(TimeSpan.FromMilliseconds(1500));
            Dispatcher.UIThread.RunJobs();
            pan.SlotLift.ShouldBe(1, 0.001);
            pan.PressDip.ShouldBe(1, 0.001);
            pan.SlotOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemInvoked_And_The_Obsolete_ItemChosen_Report_Every_Choice_While_SelectionChanged_Reports_Real_Changes()
    {
        var dock = Dock();
        var window = TestHelpers.Show(dock);
        try
        {
            var invoked = new List<object?>();
            var chosen = new List<object?>();
            var changes = new List<ToolDockSelectionChangedEventArgs>();
            dock.ItemInvoked += (_, e) => invoked.Add(e.Value);
#pragma warning disable CS0618 // the former name must keep working until a major version removes it
            dock.ItemChosen += (_, e) => chosen.Add(e.Value);
#pragma warning restore CS0618
            dock.SelectionChanged += (_, e) => changes.Add(e);

            Click(window, Slot(dock, 4));
            Click(window, Slot(dock, 4));
            invoked.ShouldBe(["pen", "pen"]);
            chosen.ShouldBe(["pen", "pen"]);
            changes.Count.ShouldBe(1);
            changes[0].NewValue.ShouldBe("pen");

            dock.SelectedValue = "text";
            Dispatcher.UIThread.RunJobs();
            changes.Count.ShouldBe(2);
            changes[1].OldValue.ShouldBe("pen");
            changes[1].NewValue.ShouldBe("text");
            // Setting the value in code is not a choice.
            invoked.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }
}
