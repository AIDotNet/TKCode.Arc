using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The orbit menu: places on the arc, targeting by angle, press-drag-release, tap to keep open, the confirmation, the
/// keyboard, the lens and the magnet, and automation.
/// </summary>
public class OrbitMenuTests
{
    private static OrbitMenu Menu(int count = 5)
    {
        var menu = new OrbitMenu { Label = "Photo actions" };
        for (var i = 0; i < count; i++)
        {
            menu.Items.Add(new OrbitMenuAction { Glyph = ArcIcons.Plus, Label = $"Action {i}", DoneText = $"Done {i}" });
        }
        return menu;
    }

    private static OrbitMenuAction Action(OrbitMenu menu, int index) => (OrbitMenuAction)menu.ContainerFromIndex(index)!;

    private static void OpenWithKeyboard(Window window, OrbitMenu menu)
    {
        menu.Trigger!.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The trigger's center in window coordinates.</summary>
    private static Point Center(OrbitMenu menu, Window window)
        => menu.TranslatePoint(new Point(menu.Bounds.Width / 2, menu.Bounds.Height / 2), window)!.Value;

    private static Point Toward(Point center, double degrees, double distance)
    {
        var radians = degrees * Math.PI / 180;
        return new Point(center.X + Math.Cos(radians) * distance, center.Y + Math.Sin(radians) * distance);
    }

    private static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void Five_actions_spread_over_the_upper_arc_by_default()
    {
        var angles = Enumerable.Range(0, 5).Select(i => OrbitMenuGeometry.AngleOf(i, 5, -90, 150)).ToArray();
        angles.ShouldBe([-165, -127.5, -90, -52.5, -15]);
        var first = OrbitMenu.PlaceOnArc(0, 5, 104, -90, 150);
        first.X.ShouldBe(-100.46, 0.01);
        first.Y.ShouldBe(-26.92, 0.01);
        var single = OrbitMenu.PlaceOnArc(0, 1, 104, 0, 150);
        single.X.ShouldBe(104, 1e-9);
        single.Y.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void A_full_ring_does_not_put_the_last_action_on_the_first()
    {
        var places = Enumerable.Range(0, 4).Select(i => OrbitMenu.PlaceOnArc(i, 4, 50, -90, 360)).ToArray();
        places[0].Y.ShouldBe(-50, 1e-9);
        places[1].X.ShouldBe(50, 1e-9);
        places[2].Y.ShouldBe(50, 1e-9);
        places[3].X.ShouldBe(-50, 1e-9);
    }

    [Fact]
    public void Targeting_goes_by_angle_outside_the_dead_zone_and_within_reach()
    {
        int At(double degrees, double distance)
        {
            var radians = degrees * Math.PI / 180;
            return OrbitMenuGeometry.TargetAt(new Vector(Math.Cos(radians) * distance, Math.Sin(radians) * distance), 5, 104,
                -90, 150, 34, 76, 30, _ => true);
        }

        At(-90, 33.9).ShouldBe(-1);
        At(-90, 34).ShouldBe(2);
        At(-90, 180).ShouldBe(2);
        At(-90, 181).ShouldBe(-1);
        // Halfway between two actions the boundary is the bisector.
        At(-145, 100).ShouldBe(1);
        At(-150, 100).ShouldBe(0);
        // Thirty degrees past either end still targets it, a degree more does not.
        At(15, 100).ShouldBe(4);
        At(16, 100).ShouldBe(-1);
        At(-195, 100).ShouldBe(0);
        At(-196, 100).ShouldBe(-1);
        // Straight down, away from the arc: nothing.
        At(90, 60).ShouldBe(-1);
    }

    [Fact]
    public void The_magnet_reaches_88_px_and_is_full_on_the_action()
    {
        OrbitMenuGeometry.Weight(0).ShouldBe(1);
        OrbitMenuGeometry.Weight(44).ShouldBe(0.5625, 1e-9);
        OrbitMenuGeometry.Weight(88).ShouldBe(0);
        OrbitMenuGeometry.Weight(120).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Folded_actions_are_hidden_and_open_ones_rest_on_the_arc()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            Action(menu, 0).IsVisible.ShouldBeFalse();
            menu.Bounds.Width.ShouldBe(56);
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < 3; i++)
            {
                var action = Action(menu, i);
                action.IsVisible.ShouldBeTrue();
                action.Opacity.ShouldBe(1);
                action.Bounds.Width.ShouldBe(46);
                action.Rest.ShouldBe(OrbitMenu.PlaceOnArc(i, 3, 104, -90, 150));
                // Each action sits on the trigger's centre; its transform carries it out to its place.
                var center = action.TranslatePoint(new Point(action.Bounds.Width / 2, action.Bounds.Height / 2), menu)!.Value;
                (center.X - menu.Bounds.Width / 2).ShouldBe(action.Rest.X, 0.5);
                (center.Y - menu.Bounds.Height / 2).ShouldBe(action.Rest.Y, 0.5);
            }
            menu.Classes.Contains(OrbitMenu.OpenPseudoClass).ShouldBeTrue();
            menu.Trigger!.Classes.Contains(OrbitMenuTrigger.OpenPseudoClass).ShouldBeTrue();
            menu.Trigger.Glyph!.State.ShouldBe(OrbitMenuGlyphState.Close);
            menu.Pill!.Text.ShouldBe("Photo actions");
            menu.Close();
            Dispatcher.UIThread.RunJobs();
            Action(menu, 1).IsVisible.ShouldBeFalse();
            menu.Trigger.Glyph.State.ShouldBe(OrbitMenuGlyphState.Dots);
            menu.Pill.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Direction_spread_and_radius_place_the_arc()
    {
        var menu = Menu(3);
        menu.Direction = 0;
        menu.Spread = 90;
        menu.Radius = 80;
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            Action(menu, 1).Rest.X.ShouldBe(80, 1e-9);
            Action(menu, 1).Rest.Y.ShouldBe(0, 1e-9);
            Action(menu, 0).Rest.Y.ShouldBeLessThan(0);
            Action(menu, 2).Rest.Y.ShouldBeGreaterThan(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_pill_sits_between_the_trigger_and_an_upward_arc_and_beyond_a_sideways_one()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            // Up: 8 px clear of the trigger, half the pill's height further (52 from the center).
            menu.Pill!.Anchor.X.ShouldBe(0, 0.01);
            menu.Pill.Anchor.Y.ShouldBe(-52, 0.01);
            // Right: no room between the trigger and the arc, so the pill goes 8 px past the actions.
            menu.Direction = 0;
            Dispatcher.UIThread.RunJobs();
            menu.Pill.Anchor.X.ShouldBe(104 + 23 + 8 + menu.Pill.Bounds.Width / 2, 0.5);
            menu.Pill.Anchor.Y.ShouldBe(0, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Press_drag_and_release_fires_the_action_the_pointer_heads_toward()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        try
        {
            Render();
            OrbitMenuActionEventArgs? fired = null;
            menu.ActionInvoked += (_, e) => fired = e;
            var center = Center(menu, window);
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Trigger!.Classes.Contains(OrbitMenuTrigger.HoldingPseudoClass).ShouldBeTrue();
            menu.Pill!.Text.ShouldBe("Photo actions");

            // Only heading toward the action matters: halfway out, between the trigger and the arc, already targets it.
            window.MouseMove(Toward(center, -52.5, 60), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            menu.TargetIndex.ShouldBe(3);
            Action(menu, 3).IsTargeted.ShouldBeTrue();
            menu.Pill.Text.ShouldBe("Action 3");

            window.MouseUp(Toward(center, -52.5, 60), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            fired.ShouldNotBeNull();
            fired!.Action.ShouldBeSameAs(Action(menu, 3));
            fired.Item.ShouldBeSameAs(Action(menu, 3));
            menu.IsOpen.ShouldBeFalse();
            menu.DoneText.ShouldBe("Done 3");
            menu.Pill.Text.ShouldBe("Done 3");
            menu.Pill.IsDone.ShouldBeTrue();
            menu.Trigger.Glyph!.State.ShouldBe(OrbitMenuGlyphState.Check);
            menu.Announcement.ShouldBe("Done 3");

            menu.ExpireDone();
            menu.Pill.IsShown.ShouldBeFalse();
            menu.Trigger.Glyph.State.ShouldBe(OrbitMenuGlyphState.Dots);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_drag_away_from_every_action_says_release_to_cancel_and_fires_nothing()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        try
        {
            Render();
            var fired = 0;
            menu.ActionInvoked += (_, _) => fired++;
            var center = Center(menu, window);
            window.MouseDown(center, MouseButton.Left);
            // Down, away from the arc, past the 8 px drag threshold.
            window.MouseMove(Toward(center, 90, 40), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            menu.TargetIndex.ShouldBe(-1);
            menu.Pill!.Text.ShouldBe("Release to cancel");
            window.MouseUp(Toward(center, 90, 40), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            fired.ShouldBe(0);
            menu.IsOpen.ShouldBeFalse();
            menu.DoneText.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_tap_keeps_the_arc_open_and_a_second_tap_closes_it()
    {
        var menu = Menu();
        long now = 1000;
        menu.Clock = () => now;
        var window = TestHelpers.Show(menu);
        try
        {
            Render();
            var center = Center(menu, window);
            window.MouseDown(center, MouseButton.Left);
            now += 120;
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            menu.Trigger!.Classes.Contains(OrbitMenuTrigger.HoldingPseudoClass).ShouldBeFalse();

            window.MouseDown(center, MouseButton.Left);
            now += 120;
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_hold_without_a_drag_folds_on_release()
    {
        var menu = Menu();
        long now = 1000;
        menu.Clock = () => now;
        var window = TestHelpers.Show(menu);
        try
        {
            Render();
            var center = Center(menu, window);
            window.MouseDown(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            now += 400;
            window.MouseUp(center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_open_arc_follows_a_hovering_pointer_and_a_click_on_an_action_fires_it()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            Render();
            OrbitMenuAction? fired = null;
            menu.ActionInvoked += (_, e) => fired = e.Action;
            var center = Center(menu, window);
            // Between the trigger and the arc: the hover field brings the pointer to the menu.
            window.MouseMove(Toward(center, -90, 60), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.TargetIndex.ShouldBe(2);
            menu.Pill!.Text.ShouldBe("Action 2");
            menu.Lens!.Classes.Contains(OrbitMenuLens.DangerPseudoClass).ShouldBeFalse();

            var on = Toward(center, -165, 104);
            window.MouseMove(on, RawInputModifiers.None);
            Render();
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            fired.ShouldBeSameAs(Action(menu, 0));
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_press_on_the_field_between_actions_folds_the_arc_without_firing()
    {
        var menu = Menu();
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            Render();
            var fired = 0;
            menu.ActionInvoked += (_, _) => fired++;
            var spot = Toward(Center(menu, window), -90, 60);
            window.MouseDown(spot, MouseButton.Left);
            window.MouseUp(spot, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            fired.ShouldBe(0);
            menu.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nearby_actions_lean_toward_the_pointer_and_the_lens_settles_under_the_target()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu();
        var window = TestHelpers.Show(menu, ArcMotionLevel.Full);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            Render();
            var center = Center(menu, window);
            // On the middle action: it grows to about 1.42 (1.29 from the magnet, 1.1 as the target), its neighbours lean in.
            window.MouseMove(Toward(center, -90, 104), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromSeconds(1));
            Dispatcher.UIThread.RunJobs();
            var target = Action(menu, 2);
            target.VisualScale.ShouldBe(1.29 * 1.1, 0.01);
            var neighbour = Action(menu, 1);
            neighbour.VisualScale.ShouldBeGreaterThan(1.03);
            neighbour.VisualOffset.X.ShouldBeGreaterThan(neighbour.Rest.X);
            Action(menu, 0).VisualScale.ShouldBe(1, 0.001);
            var lens = menu.Lens!;
            lens.VisualScale.ShouldBe(target.VisualScale, 0.01);
            lens.VisualOffset.X.ShouldBe(target.VisualOffset.X, 0.5);
            lens.VisualOffset.Y.ShouldBe(target.VisualOffset.Y, 0.5);
            lens.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_danger_action_tints_the_lens()
    {
        var menu = Menu();
        ((OrbitMenuAction)menu.Items[4]!).Tone = OrbitMenuActionTone.Danger;
        var window = TestHelpers.Show(menu);
        try
        {
            Action(menu, 4).Classes.Contains(OrbitMenuAction.DangerPseudoClass).ShouldBeTrue();
            OpenWithKeyboard(window, menu);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.TargetIndex.ShouldBe(4);
            menu.Lens!.Classes.Contains(OrbitMenuLens.DangerPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_arrow_keys_walk_round_the_arc_and_escape_returns_to_the_trigger()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            OpenWithKeyboard(window, menu);
            menu.IsOpen.ShouldBeTrue();
            var focus = window.FocusManager!;
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 0));
            // The focused action is the target: the lens sits under it and the pill names it.
            menu.TargetIndex.ShouldBe(0);
            menu.Pill!.Text.ShouldBe("Action 0");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 1));
            menu.TargetIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 0));
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            focus.GetFocusedElement().ShouldBeSameAs(Action(menu, 0));
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            focus.GetFocusedElement().ShouldBeSameAs(menu.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrow_up_on_the_trigger_opens_onto_the_last_action()
    {
        var menu = Menu(4);
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Trigger!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(Action(menu, 3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_on_an_action_fires_it_folds_the_arc_and_confirms()
    {
        var menu = Menu(3);
        var executed = 0;
        var window = TestHelpers.Show(menu);
        try
        {
            var target = Action(menu, 1);
            target.Command = new RelayCommandProbe(() => executed++);
            OrbitMenuAction? invoked = null;
            menu.ActionInvoked += (_, e) => invoked = e.Action;
            OpenWithKeyboard(window, menu);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            invoked.ShouldBeSameAs(target);
            executed.ShouldBe(1);
            menu.IsOpen.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(menu.Trigger);
            menu.Pill!.Text.ShouldBe("Done 1");
            menu.Classes.Contains(OrbitMenu.DonePseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_confirmation_falls_back_to_the_label_and_can_stay_silent()
    {
        var menu = Menu(2);
        menu.Announces = false;
        ((OrbitMenuAction)menu.Items[0]!).DoneText = null;
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            Action(menu, 0).Invoke();
            Dispatcher.UIThread.RunJobs();
            menu.DoneText.ShouldBe("Action 0");
            menu.Announcement.ShouldBeNull();
            // Opening again ends the confirmation at once.
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.DoneText.ShouldBeNull();
            menu.Trigger!.Glyph!.State.ShouldBe(OrbitMenuGlyphState.Close);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_disabled_action_is_skipped_and_never_targeted()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            Action(menu, 1).IsEnabled = false;
            OpenWithKeyboard(window, menu);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(Action(menu, 2));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabling_the_menu_folds_it()
    {
        var menu = Menu(3);
        var window = TestHelpers.Show(menu);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            menu.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeFalse();
            menu.Trigger!.IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_trigger_and_actions_are_named_for_assistive_technology()
    {
        var menu = Menu(2);
        var window = TestHelpers.Show(menu);
        try
        {
            AutomationProperties.GetName(menu.Trigger!).ShouldBe("Photo actions");
            AutomationProperties.GetItemStatus(menu.Trigger!).ShouldBe("Collapsed");
            var trigger = ControlAutomationPeer.CreatePeerForElement(menu.Trigger!);
            var expand = (IExpandCollapseProvider)trigger;
            expand.ShowsMenu.ShouldBeTrue();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);
            expand.Expand();
            Dispatcher.UIThread.RunJobs();
            menu.IsOpen.ShouldBeTrue();
            expand.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);

            var peer = ControlAutomationPeer.CreatePeerForElement(Action(menu, 1));
            peer.GetName().ShouldBe("Action 1");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.MenuItem);
            OrbitMenuAction? invoked = null;
            menu.ActionInvoked += (_, e) => invoked = e.Action;
            ((IInvokeProvider)peer).Invoke();
            invoked.ShouldBeSameAs(Action(menu, 1));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void At_full_motion_the_actions_leave_one_after_another()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu(4);
        var window = TestHelpers.Show(menu, ArcMotionLevel.Full);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            var first = Action(menu, 0).Travel!.Value;
            var last = Action(menu, 3).Travel!.Value;
            first.ShouldBeGreaterThan(last);
            // The first one swirls in: it starts behind its place (counterclockwise) and turns into it.
            var head = Action(menu, 0);
            var angle = Math.Atan2(head.VisualOffset.Y, head.VisualOffset.X);
            OrbitMenuGeometry.Wrap((angle - head.RestAngle) * 180 / Math.PI).ShouldBeLessThan(-5);
            clock.Advance(TimeSpan.FromSeconds(2));
            Dispatcher.UIThread.RunJobs();
            Action(menu, 3).Travel!.Value.ShouldBe(1, 0.01);
            Action(menu, 3).Opacity.ShouldBe(1, 0.01);
            menu.Track!.Progress.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_first_opening_starts_every_action_inside_the_trigger()
    {
        // The first opening used to place the actions all the way out until each one's spring started, so the ring
        // showed open, folded back into the trigger and fanned out again.
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu(4);
        var window = TestHelpers.Show(menu, ArcMotionLevel.Full);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            for (var i = 0; i < 4; i++)
            {
                Action(menu, i).Opacity.ShouldBe(0);
                var center = Action(menu, i).TranslatePoint(new Point(23, 23), menu)!.Value;
                center.X.ShouldBe(menu.Bounds.Width / 2, 0.5);
                center.Y.ShouldBe(menu.Bounds.Height / 2, 0.5);
            }
            // The last action waits three stagger steps (105 ms) before it leaves.
            clock.Advance(TimeSpan.FromMilliseconds(60));
            Dispatcher.UIThread.RunJobs();
            var last = Action(menu, 3);
            last.Opacity.ShouldBe(0);
            var at = last.TranslatePoint(new Point(last.Bounds.Width / 2, last.Bounds.Height / 2), menu)!.Value;
            at.X.ShouldBe(menu.Bounds.Width / 2, 0.5);
            at.Y.ShouldBe(menu.Bounds.Height / 2, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_fades_the_actions_in_place()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var menu = Menu(3);
        var window = TestHelpers.Show(menu, ArcMotionLevel.Reduced);
        try
        {
            menu.Open();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(40));
            Dispatcher.UIThread.RunJobs();
            var action = Action(menu, 2);
            action.Opacity.ShouldBeGreaterThan(0);
            action.Opacity.ShouldBeLessThan(1);
            // Already at its place, never swirling or blurred.
            action.VisualOffset.X.ShouldBe(action.Rest.X, 0.01);
            action.VisualOffset.Y.ShouldBe(action.Rest.Y, 0.01);
            action.Effect.ShouldBeNull();
            menu.Trigger!.Glyph!.Rotation.ShouldBe(90);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class RelayCommandProbe : System.Windows.Input.ICommand
    {
        private readonly System.Action _run;

        public RelayCommandProbe(System.Action run) => _run = run;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _run();
    }
}
