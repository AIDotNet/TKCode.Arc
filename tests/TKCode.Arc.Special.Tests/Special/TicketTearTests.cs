using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The ticket tear: the paper's geometry, the states of a stub, and the control's input, events and automation.</summary>
public class TicketTearTests
{
    private const double Notch = 8;
    private const double Height = 180;

    private const double Inset = 4.5;

    private static TicketTearGeometry Paper(double corner = 0) => new(300, 104, Height, corner, Notch, Inset, 2, 10);

    private static TicketTearMotion Stub()
    {
        var geometry = Paper();
        var motion = new TicketTearMotion();
        motion.Configure(geometry.Height, geometry.Notch, geometry.Holes);
        return motion;
    }

    private static void Run(TicketTearMotion motion, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            motion.Step(1.0 / 60);
        }
    }

    private static TicketTear Ticket() => new()
    {
        Width = 400,
        Height = Height,
        Label = "Check in",
        Content = new TextBlock { Text = "Body" },
        Stub = new TextBlock { Text = "Seat" }
    };

    // --- geometry ----------------------------------------------------------------------------------------------

    [Fact]
    public void Holes_are_spread_evenly_between_the_notches()
    {
        var geometry = Paper();
        geometry.Holes.Count.ShouldBeGreaterThan(5);
        var spacing = geometry.Holes[1] - geometry.Holes[0];
        (geometry.Holes[0] - Notch).ShouldBe(spacing, 1e-9);
        (Height - Notch - geometry.Holes[^1]).ShouldBe(spacing, 1e-9);
    }

    [Fact]
    public void A_torn_stub_and_a_torn_body_share_one_line_through_the_middle_of_each_hole()
    {
        var geometry = Paper();
        var body = new List<Point>();
        var stub = new List<Point>();
        geometry.BuildBody(body, false);
        geometry.BuildStub(stub, Height, false);
        var line = geometry.BodyWidth + Inset;
        foreach (var hole in geometry.Holes)
        {
            // Half a hole bites into each piece, from a centre on the line.
            body.Min(p => Math.Abs(p.X - (line - geometry.HoleRadius)) + Math.Abs(p.Y - hole)).ShouldBeLessThan(1e-6);
            stub.Min(p => Math.Abs(p.X - (Inset + geometry.HoleRadius)) + Math.Abs(p.Y - hole)).ShouldBeLessThan(1e-6);
        }
        // Between the holes the line wanders, and the same way on both sides.
        var past = body.Where(p => p.X > line + 0.01).ToArray();
        past.Length.ShouldBeGreaterThan(0);
        foreach (var point in past)
        {
            var onStub = new Point(point.X - geometry.BodyWidth, point.Y);
            stub.Min(p => Math.Abs(p.X - onStub.X) + Math.Abs(p.Y - onStub.Y)).ShouldBeLessThan(1e-6);
        }
    }

    [Fact]
    public void The_perforation_runs_inside_the_stub_and_its_paper_reaches_the_left_edge()
    {
        var geometry = Paper();
        var whole = new List<Point>();
        geometry.BuildStub(whole, 0, false);
        whole.Min(p => p.X).ShouldBe(0, 1e-9);
        whole.Max(p => p.X).ShouldBe(geometry.StubWidth, 1e-9);
        // Above the front that strip has gone with the tear.
        var torn = new List<Point>();
        geometry.BuildStub(torn, Height / 2, false);
        torn.Where(p => p.Y < Height / 2 - 1).Min(p => p.X).ShouldBeGreaterThan(0);
        torn.Where(p => p.Y > Height / 2 + 1).Min(p => p.X).ShouldBe(0, 1e-9);
    }

    [Fact]
    public void The_tear_line_is_fixed_for_a_ticket()
    {
        var a = new List<Point>();
        var b = new List<Point>();
        Paper().BuildBody(a, true);
        Paper().BuildBody(b, true);
        a.ShouldBe(b);
    }

    [Fact]
    public void Fibers_stand_past_the_edge_on_both_sides()
    {
        var geometry = Paper();
        var plain = new List<Point>();
        var fibers = new List<Point>();
        geometry.BuildBody(plain, false);
        geometry.BuildBody(fibers, true);
        fibers.Max(p => p.X).ShouldBeGreaterThan(plain.Max(p => p.X));
        TicketTearGeometry.Area(fibers).ShouldBeGreaterThan(TicketTearGeometry.Area(plain));

        geometry.BuildStub(plain, Height, false);
        geometry.BuildStub(fibers, Height, true);
        fibers.Min(p => p.X).ShouldBeLessThan(plain.Min(p => p.X));
        TicketTearGeometry.Area(fibers).ShouldBeGreaterThan(TicketTearGeometry.Area(plain));
    }

    [Fact]
    public void The_torn_edge_goes_one_way_so_a_front_can_split_it()
    {
        var geometry = Paper();
        var body = new List<Point>();
        geometry.BuildBody(body, true);
        var line = geometry.BodyWidth + Inset;
        var edge = body.Where(p => p.Y >= Notch && p.Y <= Height - Notch && p.X > line - 8).ToArray();
        for (var i = 1; i < edge.Length; i++)
        {
            edge[i].Y.ShouldBeGreaterThanOrEqualTo(edge[i - 1].Y);
        }
    }

    [Fact]
    public void The_attached_stub_is_what_is_under_the_hinge()
    {
        var geometry = Paper();
        var attached = new List<Point>();
        var whole = new List<Point>();
        geometry.BuildStub(whole, 0, false);
        geometry.BuildAttachedStub(attached, Height / 2);
        attached.Min(p => p.Y).ShouldBe(Height / 2, 1e-9);
        var half = TicketTearGeometry.Area(attached);
        half.ShouldBeLessThan(TicketTearGeometry.Area(whole) * 0.6);
        half.ShouldBeGreaterThan(TicketTearGeometry.Area(whole) * 0.4);
    }

    [Fact]
    public void The_hinge_never_runs_through_a_hole()
    {
        var geometry = Paper();
        var hole = geometry.Holes[3];
        geometry.SnapHinge(hole).ShouldBe(hole - geometry.HoleRadius, 1e-9);
        geometry.SnapHinge(hole + geometry.HoleRadius * 0.9).ShouldBe(hole - geometry.HoleRadius, 1e-9);
        var between = (hole + geometry.Holes[4]) / 2;
        geometry.SnapHinge(between).ShouldBe(between, 1e-9);
    }

    [Fact]
    public void The_flap_is_what_is_above_the_hinge_and_the_whole_stub_once_free()
    {
        var geometry = Paper();
        var hinge = (geometry.Holes[5] + geometry.Holes[6]) / 2;
        var flap = new List<Point>();
        geometry.BuildFlap(flap, hinge, false);
        flap.Max(p => p.Y).ShouldBe(hinge, 1e-9);
        flap.Min(p => p.Y).ShouldBe(0, 1e-9);

        // A flap starts at the torn line: the strip left of it stays with the ticket.
        flap.Min(p => p.X).ShouldBeGreaterThan(0);

        var whole = new List<Point>();
        geometry.BuildStub(whole, Height, false);
        geometry.BuildFlap(flap, Height, false);
        TicketTearGeometry.Area(flap).ShouldBe(TicketTearGeometry.Area(whole), 1e-6);
    }

    [Fact]
    public void The_attached_stub_stays_inside_a_rounded_corner()
    {
        var geometry = Paper(40);
        var attached = new List<Point>();
        geometry.BuildAttachedStub(attached, 12);
        // 12 px down a 40 px corner, the paper's edge is well short of the full width.
        attached.Where(p => Math.Abs(p.Y - 12) < 1e-9).Max(p => p.X).ShouldBeLessThan(geometry.StubWidth - 1);
    }

    [Theory]
    [InlineData(10, 104)] // shorter than two notches
    [InlineData(40, 104)]
    [InlineData(180, 2)] // narrower than a notch
    [InlineData(1, 1)]
    public void Tiny_tickets_still_make_closed_shapes(double height, double stubWidth)
    {
        var geometry = new TicketTearGeometry(0, stubWidth, height, 18, Notch, Inset, 2, 10);
        var points = new List<Point>();
        geometry.BuildBody(points, true);
        TicketTearGeometry.Area(points).ShouldBeGreaterThanOrEqualTo(0);
        geometry.BuildStub(points, height, true);
        TicketTearGeometry.Area(points).ShouldBeGreaterThan(0);
        foreach (var point in points)
        {
            double.IsFinite(point.X).ShouldBeTrue();
            double.IsFinite(point.Y).ShouldBeTrue();
        }
    }

    // --- states ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_tear_follows_the_hand_down_or_away_and_never_goes_back()
    {
        var motion = Stub();
        motion.Grab();
        motion.Drag(new Vector(0, 40), default);
        motion.Front.ShouldBe(Notch + 40, 1e-9);

        // Away from the body counts too, whichever is larger leads.
        motion.Drag(new Vector(60, 10), default);
        motion.Front.ShouldBe(Notch + 60, 1e-9);

        // Going back up does nothing.
        motion.Drag(new Vector(0, 5), default);
        motion.Front.ShouldBe(Notch + 60, 1e-9);

        // A new grab continues from there.
        motion.Release(default);
        motion.Grab();
        motion.Drag(new Vector(0, 10), default);
        motion.Front.ShouldBe(Notch + 70, 1e-9);
    }

    [Fact]
    public void It_tears_hole_by_hole()
    {
        var geometry = Paper();
        var motion = Stub();
        motion.Grab();
        motion.HolesTorn.ShouldBe(0);
        motion.Drag(new Vector(0, geometry.Holes[0] - Notch + 0.5), default);
        motion.HolesTorn.ShouldBe(1);
        motion.Drag(new Vector(0, geometry.Holes[2] - Notch + 0.5), default);
        motion.HolesTorn.ShouldBe(3);
    }

    [Fact]
    public void Let_go_mid_tear_the_flap_springs_most_of_the_way_back()
    {
        var motion = Stub();
        motion.Grab();
        motion.Drag(new Vector(70, 60), default);
        Run(motion, 1);
        var pulled = motion.Angle;
        pulled.ShouldBeGreaterThan(0.2);
        var front = motion.Front;

        motion.Release(default);
        Run(motion, 1);
        motion.Phase.ShouldBe(TicketTearPhase.Attached);
        motion.Angle.ShouldBe(pulled * TicketTearMotion.RestFraction, 0.01);
        motion.Angle.ShouldBeGreaterThan(0);
        motion.Front.ShouldBe(front, 1e-9);
    }

    [Fact]
    public void A_flick_finishes_the_tear_and_a_gentle_release_does_not()
    {
        var gentle = Stub();
        gentle.Grab();
        gentle.Drag(new Vector(0, 40), default);
        gentle.Release(new Vector(0, TicketTearMotion.FlickSpeed - 1));
        gentle.Phase.ShouldBe(TicketTearPhase.Attached);

        var flicked = Stub();
        flicked.Grab();
        flicked.Drag(new Vector(0, 40), default);
        flicked.Release(new Vector(0, TicketTearMotion.FlickSpeed));
        flicked.Phase.ShouldBe(TicketTearPhase.Ripping);
    }

    [Fact]
    public void A_click_is_not_a_flick()
    {
        var motion = Stub();
        motion.Grab();
        motion.Release(new Vector(0, 5000));
        motion.Phase.ShouldBe(TicketTearPhase.Attached);
        motion.Progress.ShouldBe(0);
    }

    [Fact]
    public void A_rip_speeds_up_and_a_hard_flick_is_faster()
    {
        var motion = Stub();
        motion.Rip();
        motion.Step(1.0 / 60);
        var first = motion.Progress;
        motion.Step(1.0 / 60);
        var second = motion.Progress - first;
        motion.Step(1.0 / 60);
        var third = motion.Progress - first - second;
        second.ShouldBeGreaterThan(first * 0.99);
        third.ShouldBeGreaterThan(second);

        var flicked = Stub();
        flicked.Rip(new Vector(0, 2400));
        flicked.Step(1.0 / 60);
        flicked.Progress.ShouldBeGreaterThan(first);
    }

    [Fact]
    public void A_free_stub_pops_up_falls_spins_and_fades_in_half_a_second()
    {
        var motion = Stub();
        var freed = 0;
        var finished = 0;
        motion.Freed += () => freed++;
        motion.Finished += () => finished++;
        motion.Rip(new Vector(300, 0));
        Run(motion, 1);
        freed.ShouldBe(1);
        finished.ShouldBe(1);
        motion.Phase.ShouldBe(TicketTearPhase.Gone);
        motion.FallOpacity.ShouldBe(0);

        // Frame by frame, from the moment it comes free.
        var fall = Stub();
        fall.Rip(new Vector(300, 0));
        while (fall.Phase is not TicketTearPhase.Falling)
        {
            fall.Step(1.0 / 60);
        }
        var top = 0.0;
        var frames = 0;
        while (fall.Phase is TicketTearPhase.Falling)
        {
            fall.Step(1.0 / 60);
            top = Math.Min(top, fall.FallOffset.Y);
            frames++;
            if (fall.Phase is TicketTearPhase.Falling)
            {
                fall.FallOffset.X.ShouldBeGreaterThan(0); // drifts the way it was flung
                fall.FallRotation.ShouldBeGreaterThan(0);
            }
        }
        top.ShouldBeLessThan(0);
        fall.FallOffset.Y.ShouldBeGreaterThan(0);
        ((double)frames / 60).ShouldBe(TicketTearMotion.FallDuration, 0.05);
    }

    [Fact]
    public void A_stub_flung_to_the_left_spins_the_other_way()
    {
        var motion = Stub();
        motion.Rip(new Vector(-400, 0));
        while (motion.Phase is not TicketTearPhase.Falling)
        {
            motion.Step(1.0 / 60);
        }
        motion.Step(1.0 / 60);
        motion.FallRotation.ShouldBeLessThan(0);
    }

    [Fact]
    public void Reduced_motion_has_no_bend_and_no_fall_and_only_fades()
    {
        var motion = Stub();
        motion.CanMove = false;
        motion.Grab();
        motion.Drag(new Vector(70, 60), default);
        Run(motion, 0.2);
        motion.Angle.ShouldBe(0);
        motion.Front.ShouldBe(Notch + 70, 1e-9);

        motion.Release(default);
        motion.Rip();
        motion.Phase.ShouldBe(TicketTearPhase.Falling);
        motion.Step(0.02);
        motion.Step(0.02);
        motion.FallOpacity.ShouldBe(1 - 0.04 / TicketTearMotion.FadeDuration, 0.01);
        motion.FallOffset.ShouldBe(default);
        motion.FallRotation.ShouldBe(0);
        Run(motion, 0.2);
        motion.Phase.ShouldBe(TicketTearPhase.Gone);
    }

    [Fact]
    public void A_resize_keeps_the_tear_share_without_a_jolt()
    {
        var geometry = Paper();
        var motion = Stub();
        motion.Grab();
        motion.Drag(new Vector(0, 80), default);
        Run(motion, 1);
        var share = motion.Progress;
        var angle = motion.Angle;

        var taller = new TicketTearGeometry(300, 104, Height * 2, 0, Notch, Inset, 2, 10);
        motion.Configure(taller.Height, taller.Notch, taller.Holes);
        motion.Progress.ShouldBe(share);
        motion.Front.ShouldBe(Notch + share * (taller.Height - 2 * Notch), 1e-9);
        motion.Step(1.0 / 60);
        motion.Angle.ShouldBe(angle, 1e-3);
        geometry.Holes.Count.ShouldBeLessThan(taller.Holes.Count);
    }

    // --- the control -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void The_stub_keeps_its_width_and_the_body_takes_the_rest()
    {
        var ticket = Ticket();
        TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        ticket.StubPiece.ShouldNotBeNull().Bounds.Width.ShouldBe(104, 0.01);
        ticket.Geometry.ShouldNotBeNull().BodyWidth.ShouldBe(296, 0.01);

        ticket.StubWidth = 80;
        Dispatcher.UIThread.RunJobs();
        ticket.StubPiece!.Bounds.Width.ShouldBe(80, 0.01);
        ticket.Geometry!.BodyWidth.ShouldBe(320, 0.01);
    }

    [AvaloniaFact]
    public void Enter_and_space_tear_the_stub_off_and_tell_the_app_once()
    {
        foreach (var key in new[] { PhysicalKey.Enter, PhysicalKey.Space })
        {
            var ticket = Ticket();
            var window = TestHelpers.Show(ticket);
            Dispatcher.UIThread.RunJobs();
            var torn = 0;
            ticket.Torn += (_, _) => torn++;

            ticket.StubPiece!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(key, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            ticket.IsTorn.ShouldBeTrue();
            torn.ShouldBe(1);
            ticket.StubPiece.IsVisible.ShouldBeFalse();
            ticket.Motion.Phase.ShouldBe(TicketTearPhase.Gone);
            // The focus does not fall out of the ticket, and there is no stop left to tab to.
            ticket.IsFocused.ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void The_stub_has_its_own_paper_and_text_and_neither_has_a_border_by_default()
    {
        var ticket = Ticket();
        TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        ticket.StubBackground.ShouldNotBeNull();
        ticket.Background.ShouldNotBeNull();
        ((ISolidColorBrush)ticket.StubBackground!).Color.ShouldNotBe(((ISolidColorBrush)ticket.Background!).Color);
        ticket.StubPiece!.Foreground.ShouldBe(ticket.StubForeground);
        ticket.EdgePen.ShouldBeNull();
        ticket.CornerRadius.TopLeft.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_stub_is_a_tab_stop_and_the_ticket_is_not()
    {
        var ticket = Ticket();
        var window = TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        ticket.IsTabStop.ShouldBeFalse();
        ticket.StubPiece!.Focusable.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        ticket.StubPiece.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Setting_is_torn_tears_and_clearing_it_puts_a_fresh_stub_on()
    {
        var ticket = Ticket();
        TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        var torn = 0;
        ticket.Torn += (_, _) => torn++;

        ticket.IsTorn = true;
        Dispatcher.UIThread.RunJobs();
        torn.ShouldBe(1);
        ticket.StubPiece!.IsVisible.ShouldBeFalse();

        ticket.IsTorn = false;
        Dispatcher.UIThread.RunJobs();
        torn.ShouldBe(1);
        ticket.StubPiece.IsVisible.ShouldBeTrue();
        ticket.StubPiece.Focusable.ShouldBeTrue();
        ticket.StubPiece.Opacity.ShouldBe(1);
        ticket.Motion.Phase.ShouldBe(TicketTearPhase.Attached);
        ticket.Motion.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_ticket_that_starts_torn_shows_no_tear_and_raises_nothing()
    {
        var ticket = Ticket();
        var torn = 0;
        ticket.Torn += (_, _) => torn++;
        ticket.IsTorn = true;
        TestHelpers.Show(ticket, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        torn.ShouldBe(0);
        ticket.StubPiece!.IsVisible.ShouldBeFalse();
        ticket.Motion.Phase.ShouldBe(TicketTearPhase.Gone);
    }

    [AvaloniaFact]
    public void A_drag_tears_and_a_full_pull_takes_the_stub_off()
    {
        var ticket = Ticket();
        var window = TestHelpers.Show(ticket, ArcMotionLevel.Reduced);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var torn = 0;
        ticket.Torn += (_, _) => torn++;
        var stub = ticket.StubPiece!;
        var start = stub.TranslatePoint(new Point(52, 40), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 50), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        ticket.Motion.Phase.ShouldBe(TicketTearPhase.Held);
        ticket.Motion.Front.ShouldBe(Notch + 50, 0.5);
        ticket.IsTorn.ShouldBeFalse();

        // Capture keeps it going when the hand leaves the stub.
        window.MouseMove(start + new Point(0, Height), RawInputModifiers.LeftMouseButton);
        Dispatcher.UIThread.RunJobs();
        window.MouseUp(start + new Point(0, Height), MouseButton.Left);
        ticket.IsTorn.ShouldBeTrue();
        torn.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_right_button_does_not_tear()
    {
        var ticket = Ticket();
        var window = TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        var start = ticket.StubPiece!.TranslatePoint(new Point(52, 40), window)!.Value;
        window.MouseDown(start, MouseButton.Right);
        window.MouseMove(start + new Point(0, 60), RawInputModifiers.RightMouseButton);
        window.MouseUp(start + new Point(0, 60), MouseButton.Right);
        ticket.Motion.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_full_motion_tear_runs_on_frames_with_a_flap_and_finishes()
    {
        var ticket = Ticket();
        var window = TestHelpers.Show(ticket, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        ticket.IsTorn = true;
        Dispatcher.UIThread.RunJobs();
        ticket.Motion.Phase.ShouldBe(TicketTearPhase.Ripping);

        var sawFlap = false;
        TestHelpers.PumpUntil(() =>
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            sawFlap |= ticket.ShowsFlap;
            return ticket.Motion.Phase is TicketTearPhase.Gone;
        }).ShouldBeTrue();
        sawFlap.ShouldBeTrue();
        ticket.ShowsFlap.ShouldBeFalse();
        ticket.StubSnapshot.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void Leaving_the_window_finishes_a_tear_that_was_running()
    {
        var ticket = Ticket();
        var window = TestHelpers.Show(ticket, ArcMotionLevel.Full);
        Dispatcher.UIThread.RunJobs();
        ticket.IsTorn = true;
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        ticket.Motion.Phase.ShouldBe(TicketTearPhase.Gone);
        ticket.StubSnapshot.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_stub_is_a_named_button_that_can_be_invoked_and_the_flap_is_hidden_from_assistive_technology()
    {
        var ticket = Ticket();
        TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        var peer = ControlAutomationPeer.CreatePeerForElement(ticket.StubPiece!);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        peer.GetName().ShouldBe("Check in");
        ((IInvokeProvider)peer).Invoke();
        Dispatcher.UIThread.RunJobs();
        ticket.IsTorn.ShouldBeTrue();

        var flap = TestHelpers.Descendant<TicketTearFlap>(ticket);
        ControlAutomationPeer.CreatePeerForElement(flap).IsControlElement().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_polite_status_says_the_stub_came_off()
    {
        var ticket = Ticket();
        TestHelpers.Show(ticket);
        Dispatcher.UIThread.RunJobs();
        var status = TestHelpers.Descendant<TextBlock>(ticket, TicketTear.StatusPart);
        (status.Text ?? "").ShouldBeEmpty();
        ticket.IsTorn = true;
        Dispatcher.UIThread.RunJobs();
        status.Text.ShouldNotBeNull().ShouldContain("torn off");
    }
}
