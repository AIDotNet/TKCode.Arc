using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The sticker peel: the fold geometry, the springs and states of one sticker, and the surface's input and automation.</summary>
public class StickerPeelTests
{
    private static StickerPeelMotion Sticker(StickerPeelShape shape = StickerPeelShape.Rounded, double w = 120, double h = 90)
    {
        var motion = new StickerPeelMotion { MinOffset = new Vector(-400, -300), MaxOffset = new Vector(400, 300) };
        motion.Configure(w, h, shape, 14);
        return motion;
    }

    private static void Run(StickerPeelMotion motion, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            motion.Step(1.0 / 60);
        }
    }

    // --- geometry ----------------------------------------------------------------------------------------------

    [Fact]
    public void Rounded_shapes_peel_from_the_nearest_corner_along_its_diagonal()
    {
        StickerPeelGeometry.Anchor(StickerPeelShape.Rounded, 120, 90, 14, new Point(110, 10), false, out var anchor, out var inward);
        // Top right: on the corner arc, running down and to the left.
        anchor.X.ShouldBeGreaterThan(100);
        anchor.Y.ShouldBeLessThan(20);
        inward.X.ShouldBeLessThan(0);
        inward.Y.ShouldBeGreaterThan(0);
        inward.Length.ShouldBe(1, 1e-9);
    }

    [Fact]
    public void Circles_peel_from_the_rim_point_nearest_the_pointer()
    {
        StickerPeelGeometry.Anchor(StickerPeelShape.Circle, 100, 100, 14, new Point(95, 50), false, out var anchor, out var inward);
        anchor.X.ShouldBe(100, 1e-9);
        anchor.Y.ShouldBe(50, 1e-9);
        inward.X.ShouldBe(-1, 1e-9);

        // Hover snaps to eight directions.
        StickerPeelGeometry.Anchor(StickerPeelShape.Circle, 100, 100, 14, new Point(90, 40), true, out var snapped, out _);
        var angle = Math.Atan2(snapped.Y - 50, snapped.X - 50) / (Math.PI / 4);
        angle.ShouldBe(Math.Round(angle), 1e-9);
    }

    [Fact]
    public void The_fold_leans_at_most_35_degrees()
    {
        var inward = new Vector(-Math.Sqrt(0.5), Math.Sqrt(0.5));
        // A pull 80 degrees off the diagonal folds along the 35 degree limit.
        var pull = StickerPeelGeometry.Rotate(inward, 80 * Math.PI / 180) * 60;
        var fold = StickerPeelGeometry.Constrain(inward, pull);
        var angle = Math.Abs(Math.Atan2(StickerPeelGeometry.Cross(inward, fold), StickerPeelGeometry.Dot(inward, fold)));
        angle.ShouldBe(35 * Math.PI / 180, 1e-6);
        // Pulling outward does not fold.
        StickerPeelGeometry.Constrain(inward, -inward * 30).ShouldBe(default);
        // A pull along the diagonal keeps its length.
        StickerPeelGeometry.Constrain(inward, inward * 30).Length.ShouldBe(30, 1e-9);
    }

    [Fact]
    public void Clipping_splits_the_outline_into_two_parts_that_add_up()
    {
        var motion = Sticker();
        var outline = motion.Outline.ToArray();
        var a = new Point[outline.Length + 4];
        var b = new Point[outline.Length + 4];
        var origin = new Point(60, 45);
        var normal = new Vector(0.6, 0.8);
        var na = StickerPeelGeometry.Clip(outline, a, origin, normal, true);
        var nb = StickerPeelGeometry.Clip(outline, b, origin, normal, false);
        var total = StickerPeelGeometry.Area(outline);
        (StickerPeelGeometry.Area(a.AsSpan(0, na)) + StickerPeelGeometry.Area(b.AsSpan(0, nb))).ShouldBe(total, 1e-6);
    }

    [Fact]
    public void The_flap_is_the_pulled_part_mirrored_onto_the_stuck_side()
    {
        var motion = Sticker();
        motion.Grab(new Vector(50, -35)); // top right
        motion.Drag(new Vector(50, -35) + motion.Inward * 40, 1.0 / 60);
        Run(motion, 1);
        motion.Phase.ShouldBe(StickerPeelPhase.Peeling);
        motion.HasFold.ShouldBeTrue();
        // The corner lands at the pointer: a 40 px pull.
        motion.Fold.Length.ShouldBe(40, 0.5);
        motion.FlapPoints.Length.ShouldBeGreaterThan(2);
        foreach (var p in motion.FlapPoints)
        {
            // Canonical y points to the stuck side: the whole flap lies on it.
            p.Y.ShouldBeGreaterThanOrEqualTo(-1e-9);
        }
        // The farthest flap point is the mirrored anchor: its distance from the line is half the pull.
        motion.FlapPoints.ToArray().Max(p => p.Y).ShouldBe(20, 1.5);
        motion.StuckPoints.Length.ShouldBeGreaterThan(2);
    }

    // --- springs and states ----------------------------------------------------------------------------------------------

    [Fact]
    public void Springs_are_critically_damped_and_never_overshoot()
    {
        double x = 0, v = 0;
        var peak = 0.0;
        for (var i = 0; i < 240; i++)
        {
            StickerPeelMotion.Spring(ref x, ref v, 100, 26, 1.0 / 60);
            peak = Math.Max(peak, x);
        }
        peak.ShouldBeLessThanOrEqualTo(100.0000001);
        x.ShouldBe(100, 1e-3);
    }

    [Fact]
    public void Hovering_lifts_the_nearest_corner_a_few_pixels_and_moving_lays_it_flat_first()
    {
        var motion = Sticker();
        motion.Hover(new Vector(50, -35));
        Run(motion, 0.6);
        motion.Fold.Length.ShouldBe(StickerPeelMotion.HoverPull, 0.3);
        var first = motion.Anchor;

        motion.Hover(new Vector(-50, 35)); // the opposite corner
        motion.Step(1.0 / 60);
        motion.Anchor.ShouldBe(first); // still the first corner, now laying flat
        Run(motion, 0.6);
        motion.Anchor.ShouldNotBe(first);
        motion.Fold.Length.ShouldBe(StickerPeelMotion.HoverPull, 0.3);

        motion.Unhover();
        Run(motion, 0.8);
        motion.HasFold.ShouldBeFalse();
    }

    [Fact]
    public void Letting_go_before_the_middle_lays_the_sticker_back_down()
    {
        var motion = Sticker();
        var down = new Vector(50, -35);
        motion.Grab(down);
        motion.Drag(down + motion.Inward * 30, 0.05);
        Run(motion, 0.5);
        motion.Phase.ShouldBe(StickerPeelPhase.Peeling);
        motion.Release();
        var settled = false;
        motion.Settled += _ => settled = true;
        Run(motion, 1);
        settled.ShouldBeTrue();
        motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        motion.HasFold.ShouldBeFalse();
        motion.Offset.ShouldBe(default);
    }

    [Fact]
    public void A_peel_comes_off_once_the_fold_passes_the_middle()
    {
        var motion = Sticker();
        var down = new Vector(50, -35);
        var peeled = 0;
        motion.PeeledOff += () => peeled++;
        motion.Grab(down);
        var pulled = 0.0;
        while (motion.Phase == StickerPeelPhase.Peeling && pulled < 400)
        {
            pulled += 4;
            motion.Drag(down + motion.Inward * pulled, 0.1); // slow, so it is not a tug
        }
        peeled.ShouldBe(1);
        motion.Phase.ShouldBe(StickerPeelPhase.Carried);
        // The fold passed the middle: the pull reached about the sticker's extent.
        pulled.ShouldBeGreaterThan(motion.Extent * 0.9);
        motion.LiftTarget.ShouldBe(1);
        motion.ShadowTarget.ShouldBe(1);
    }

    [Fact]
    public void A_quick_tug_peels_sooner()
    {
        var slow = Sticker();
        var quick = Sticker();
        var down = new Vector(50, -35);
        slow.Grab(down);
        quick.Grab(down);
        slow.Drag(down + slow.Inward * 70, 0.5);
        quick.Drag(down + quick.Inward * 70, 0.01);
        slow.Phase.ShouldBe(StickerPeelPhase.Peeling);
        quick.Phase.ShouldBe(StickerPeelPhase.Carried);
    }

    [Fact]
    public void A_carried_sticker_follows_the_hand_trailing_a_little_and_lands_where_it_is()
    {
        var motion = Sticker();
        var down = new Vector(50, -35);
        motion.Grab(down);
        motion.Drag(down + motion.Inward * 70, 0.01);
        motion.Phase.ShouldBe(StickerPeelPhase.Carried);
        Run(motion, 0.5);
        motion.Lift.ShouldBe(1, 0.01);
        motion.Shadow.ShouldBe(1, 1e-9);

        var hand = down + new Vector(120, 60);
        motion.Drag(hand, 0.016);
        motion.Step(1.0 / 60);
        var early = motion.Offset;
        early.Length.ShouldBeLessThan(motion.OffsetTarget.Length); // trails the pointer
        Run(motion, 0.6);
        (motion.Offset - motion.OffsetTarget).Length.ShouldBeLessThan(0.1);

        motion.Release();
        var landed = default(bool?);
        motion.Settled += d => landed = d;
        Run(motion, 1);
        landed.ShouldBe(true);
        motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        motion.Lift.ShouldBe(0, 1e-6);
        // The shadow fades over 200 ms.
        motion.Shadow.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void The_shadow_fades_out_in_200_ms()
    {
        var motion = Sticker();
        motion.PickUp();
        Run(motion, 0.5);
        motion.Shadow.ShouldBe(1, 1e-9);
        motion.Drop();
        for (var i = 0; i < 6; i++)
        {
            motion.Step(1.0 / 60);
        }
        motion.Shadow.ShouldBeGreaterThan(0);
        for (var i = 0; i < 8; i++)
        {
            motion.Step(1.0 / 60);
        }
        motion.Shadow.ShouldBe(0, 1e-9);
    }

    [Fact]
    public void Grabbing_a_landing_sticker_catches_it_in_place()
    {
        var motion = Sticker();
        motion.PickUp();
        motion.MoveBy(new Vector(100, 0));
        motion.Drop();
        Run(motion, 0.05);
        motion.Phase.ShouldBe(StickerPeelPhase.Settling);
        var at = motion.Offset;
        motion.Grab(new Vector(100, 0));
        motion.Phase.ShouldBe(StickerPeelPhase.Carried);
        motion.OffsetTarget.ShouldBe(at);
    }

    [Fact]
    public void A_carried_sticker_stretches_past_the_edges_and_is_clamped_on_drop()
    {
        var motion = Sticker();
        motion.MinOffset = new Vector(-100, -100);
        motion.MaxOffset = new Vector(100, 100);
        motion.Grab(Vector.Zero);
        motion.Drag(motion.Inward * 500, 0.001); // a tug
        motion.Phase.ShouldBe(StickerPeelPhase.Carried);
        var hand = new Vector(400, 0);
        motion.Drag(hand, 0.016);
        motion.OffsetTarget.X.ShouldBeGreaterThan(100); // stretched
        motion.OffsetTarget.X.ShouldBeLessThan(400); // with resistance
        motion.Release();
        motion.OffsetTarget.X.ShouldBe(100, 1e-9); // clamped
    }

    [Fact]
    public void Rubber_banding_is_monotone_and_bounded()
    {
        StickerPeelMotion.Rubber(50, 0, 100).ShouldBe(50);
        var a = StickerPeelMotion.Rubber(120, 0, 100);
        var b = StickerPeelMotion.Rubber(300, 0, 100);
        a.ShouldBeGreaterThan(100);
        b.ShouldBeGreaterThan(a);
        b.ShouldBeLessThan(100 + 90);
    }

    [Fact]
    public void Keyboard_pick_up_move_and_stick_down()
    {
        var motion = Sticker();
        motion.PickUp();
        motion.IsCarried.ShouldBeTrue();
        motion.MoveBy(new Vector(10, 0));
        motion.MoveBy(new Vector(40, -40));
        motion.OffsetTarget.ShouldBe(new Vector(50, -40));
        motion.Drop();
        Run(motion, 1);
        motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        motion.Offset.ShouldBe(new Vector(50, -40));
    }

    [Fact]
    public void Escape_puts_a_carried_sticker_back()
    {
        var motion = Sticker();
        motion.PickUp();
        motion.MoveBy(new Vector(80, 20));
        var dropped = default(bool?);
        motion.Settled += d => dropped = d;
        motion.Cancel();
        Run(motion, 1);
        dropped.ShouldBe(false);
        motion.Offset.ShouldBe(default);
    }

    [Fact]
    public void Reduced_motion_picks_up_directly_and_follows_one_to_one()
    {
        var motion = Sticker();
        motion.CanMove = false;
        var down = new Vector(50, -35);
        motion.Hover(down);
        motion.Fold.ShouldBe(default);
        motion.Grab(down);
        motion.Phase.ShouldBe(StickerPeelPhase.Carried);
        motion.Drag(down + new Vector(30, 10), 0.016);
        motion.Step(1.0 / 60);
        motion.Offset.ShouldBe(new Vector(30, 10));
        motion.HasFold.ShouldBeFalse();
    }

    [Fact]
    public void An_outside_change_of_place_glides_from_where_the_sticker_is()
    {
        var motion = Sticker();
        motion.Glide(new Vector(200, 0)); // the place moved 200 px to the left of where it was
        motion.Offset.X.ShouldBe(200);
        motion.OffsetTarget.ShouldBe(default);
        Run(motion, 0.1);
        motion.Offset.X.ShouldBeLessThan(200);
        motion.Offset.X.ShouldBeGreaterThan(0);
        Run(motion, 1);
        motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        motion.Offset.ShouldBe(default);
    }

    // --- the control ---------------------------------------------------------------------------------------------------

    private static StickerPeel Surface(params StickerPeelItem[] items)
    {
        var surface = new StickerPeel { Width = 500, Height = 360 };
        foreach (var item in items)
        {
            surface.Stickers.Add(item);
        }
        return surface;
    }

    private static StickerPeelItem Item(string id, double x, double y, StickerPeelShape shape = StickerPeelShape.Rounded, double rotate = 0)
        => new()
        {
            Id = id,
            Label = id + " sticker",
            X = x,
            Y = y,
            Rotate = rotate,
            Shape = shape,
            Content = new Border { Width = 80, Height = 60 }
        };

    [AvaloniaFact]
    public void Stickers_are_arranged_at_their_fractions_of_the_surface()
    {
        var surface = Surface(Item("a", 0.25, 0.5), Item("b", 0.75, 0.25));
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var layer = surface.Layer.ShouldNotBeNull();
        layer.Stickers.Count.ShouldBe(2);
        var a = layer.Stickers[0];
        a.HomeCenter.X.ShouldBe(layer.Bounds.Width * 0.25, 0.01);
        a.HomeCenter.Y.ShouldBe(layer.Bounds.Height * 0.5, 0.01);
        (a.Bounds.Center.X).ShouldBe(a.HomeCenter.X, 0.5);
        a.Bounds.Width.ShouldBe(92, 0.5); // 80 + the 6 px die cut border on both sides

        window.Width = 400;
        Dispatcher.UIThread.RunJobs();
        a.HomeCenter.X.ShouldBe(layer.Bounds.Width * 0.25, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void Positions_override_the_starting_places_and_a_change_glides()
    {
        var surface = Surface(Item("a", 0.25, 0.5));
        var window = TestHelpers.Show(surface, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        var before = a.HomeCenter;

        surface.Positions = new Dictionary<string, StickerPlacement> { ["a"] = new(0.75, 0.5, 10) };
        Dispatcher.UIThread.RunJobs();
        a.HomeCenter.X.ShouldBe(surface.Layer.Bounds.Width * 0.75, 0.01);
        a.RestRotation.ShouldBe(10);
        // It has not jumped: the offset carries it from where it was, toward the new place (frames may already have run).
        var start = before.X - a.HomeCenter.X;
        a.Motion.Offset.X.ShouldBeInRange(start - 0.5, 0);
        a.Motion.Offset.X.ShouldBeLessThan(0);
        a.Motion.Phase.ShouldBe(StickerPeelPhase.Settling);

        surface.Reset();
        Dispatcher.UIThread.RunJobs();
        a.HomeCenter.X.ShouldBe(surface.Layer.Bounds.Width * 0.25, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void The_keyboard_carries_a_sticker_and_reports_where_it_landed()
    {
        var surface = Surface(Item("a", 0.25, 0.5), Item("b", 0.75, 0.5));
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        StickerPeelPositionsChangedEventArgs? seen = null;
        surface.PositionsChanged += (_, e) => seen = e;
        var startX = a.HomeCenter.X;

        a.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        a.IsCarried.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        a.IsCarried.ShouldBeFalse();
        Dispatcher.UIThread.RunJobs();

        seen.ShouldNotBeNull();
        seen.Id.ShouldBe("a");
        seen.Positions.Count.ShouldBe(2);
        (seen.Positions["a"].X * surface.Layer.Bounds.Width).ShouldBe(startX + 50, 0.5);
        seen.Positions["b"].X.ShouldBe(0.75);
        surface.Positions.ShouldBeSameAs(seen.Positions);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_restores_a_carried_sticker_without_reporting_a_change()
    {
        var surface = Surface(Item("a", 0.5, 0.5));
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        var raised = 0;
        surface.PositionsChanged += (_, _) => raised++;
        a.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        raised.ShouldBe(0);
        a.Motion.Offset.ShouldBe(default);
        window.Close();
    }

    [AvaloniaFact]
    public void A_carried_sticker_stays_inside_the_surface()
    {
        var surface = Surface(Item("a", 0.9, 0.5));
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        a.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        for (var i = 0; i < 20; i++)
        {
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
        }
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        var right = a.HomeCenter.X + a.Bounds.Width / 2;
        right.ShouldBeLessThanOrEqualTo(surface.Layer.Bounds.Width + 0.01);
        right.ShouldBeGreaterThan(surface.Layer.Bounds.Width - 1);
        window.Close();
    }

    [AvaloniaFact]
    public void A_press_under_reduced_motion_picks_the_sticker_up_and_it_follows_the_pointer()
    {
        var surface = Surface(Item("a", 0.5, 0.5));
        var window = TestHelpers.Show(surface, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        var start = surface.Layer.TranslatePoint(a.HomeCenter, window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        a.IsCarried.ShouldBeTrue();
        window.MouseMove(start + new Point(60, 20), RawInputModifiers.LeftMouseButton);
        a.Motion.Step(1.0 / 60);
        a.Motion.Offset.X.ShouldBe(60, 0.5);
        a.Motion.Offset.Y.ShouldBe(20, 0.5);
        window.MouseUp(start + new Point(60, 20), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        a.Motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        a.HomeCenter.X.ShouldBe(surface.Layer.Bounds.Width * 0.5 + 60, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Dragging_a_corner_folds_the_sticker_and_clips_its_face()
    {
        var surface = Surface(Item("a", 0.5, 0.5));
        var window = TestHelpers.Show(surface, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        var corner = surface.Layer.TranslatePoint(new Point(a.HomeCenter.X + 40, a.HomeCenter.Y - 30), window)!.Value;
        window.MouseDown(corner, MouseButton.Left);
        window.MouseMove(corner + new Point(-20, 20), RawInputModifiers.LeftMouseButton);
        for (var i = 0; i < 30; i++)
        {
            a.Step(1.0 / 60, true);
        }
        a.Motion.Phase.ShouldBe(StickerPeelPhase.Peeling);
        a.Motion.HasFold.ShouldBeTrue();
        var face = TestHelpers.Descendant<Border>(a, StickerPeelSticker.FacePart);
        face.Clip.ShouldNotBeNull();
        window.MouseUp(corner + new Point(-20, 20), MouseButton.Left);
        for (var i = 0; i < 90; i++)
        {
            a.Step(1.0 / 60, true);
        }
        a.Motion.Phase.ShouldBe(StickerPeelPhase.Rest);
        face.Clip.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void The_surface_is_a_labelled_group_and_each_sticker_a_toggle_button()
    {
        var surface = Surface(Item("a", 0.5, 0.5));
        surface.Label = "Laptop lid";
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var peer = ControlAutomationPeer.CreatePeerForElement(surface);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetName().ShouldBe("Laptop lid");

        var sticker = surface.Layer!.Stickers[0];
        var stickerPeer = ControlAutomationPeer.CreatePeerForElement(sticker);
        stickerPeer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        stickerPeer.GetName().ShouldBe("a sticker");
        stickerPeer.GetLocalizedControlType().ShouldBe("sticker");
        sticker.Focusable.ShouldBeTrue();

        var toggle = stickerPeer.GetProvider<Avalonia.Automation.Provider.IToggleProvider>().ShouldNotBeNull();
        toggle.ToggleState.ShouldBe(Avalonia.Automation.Provider.ToggleState.Off);
        toggle.Toggle();
        toggle.ToggleState.ShouldBe(Avalonia.Automation.Provider.ToggleState.On);
        window.Close();
    }

    [AvaloniaFact]
    public void Leaving_a_carried_sticker_with_tab_sticks_it_down()
    {
        var surface = Surface(Item("a", 0.3, 0.5), Item("b", 0.7, 0.5));
        var window = TestHelpers.Show(surface);
        Dispatcher.UIThread.RunJobs();
        var a = surface.Layer!.Stickers[0];
        var b = surface.Layer.Stickers[1];
        a.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        a.IsCarried.ShouldBeTrue();
        b.Focus(NavigationMethod.Tab);
        a.IsCarried.ShouldBeFalse();
        window.Close();
    }
}
