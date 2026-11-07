using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>
/// The holo card: the measured pointer, light, shadow and flip rules, the perspective projection against the measured
/// foreshortening, the fitted springs, input (click halves, keyboard, touch), motion levels and accessibility.
/// </summary>
public class HoloCardTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    // ---- Pure rules ----

    [Fact]
    public void Card_is_five_by_seven_and_at_most_its_width_limit()
    {
        HoloCardMath.CardSize(290, double.PositiveInfinity, double.PositiveInfinity).ShouldBe(new Size(290, 406));
        HoloCardMath.CardSize(double.PositiveInfinity, double.PositiveInfinity, 300).ShouldBe(new Size(300, 420));
        HoloCardMath.CardSize(240, 1000, 300).ShouldBe(new Size(240, 336));
        // Too short: the height limits the width.
        HoloCardMath.CardSize(300, 280, 300).ShouldBe(new Size(200, 280));
    }

    [Fact]
    public void Tilt_maps_the_pointer_linearly_and_the_corner_under_it_sinks()
    {
        HoloCardMath.Tilt(new Vector(0.5, 0.5), 14).ShouldBe(new Vector(0, 0));
        // Top right: rotateX +14 (top recedes), rotateY +14 (right recedes).
        HoloCardMath.Tilt(new Vector(1, 0), 14).ShouldBe(new Vector(14, 14));
        HoloCardMath.Tilt(new Vector(0, 1), 14).ShouldBe(new Vector(-14, -14));
        // Measured: (864, 500) on the 290-wide card → px 0.9966 → rotateY 13.9.
        HoloCardMath.Tilt(new Vector(0.9966, 0.5), 14).Y.ShouldBe(13.905, 0.001);
        HoloCardMath.Tilt(new Vector(1, 0.5), 8).Y.ShouldBe(8);
    }

    [Fact]
    public void Pointer_position_is_normalized_and_clamped_to_the_layout_box()
    {
        HoloCardMath.Normalize(new Point(145, 203), new Size(290, 406)).ShouldBe(new Vector(0.5, 0.5));
        HoloCardMath.Normalize(new Point(-10, 500), new Size(290, 406)).ShouldBe(new Vector(0, 1));
    }

    [Fact]
    public void Shadow_leans_against_the_tilt_and_hover_strengthens_it()
    {
        HoloCardMath.ShadowOffset(default).ShouldBe(new Vector(0, 18));
        var offset = HoloCardMath.ShadowOffset(new Vector(14, 14));
        offset.X.ShouldBe(-12.6, 1e-9);
        offset.Y.ShouldBe(30.6, 1e-9);
        HoloCardMath.ShadowAlpha(0).ShouldBe(0.28);
        HoloCardMath.ShadowAlpha(1).ShouldBe(0.40, 1e-9);
        HoloCardMath.Scale(1).ShouldBe(1.035);
    }

    [Fact]
    public void Light_turns_the_foil_hue_and_a_flip_sweeps_it()
    {
        HoloCardMath.Hue(new Vector(0.5, 0.5), 0).ShouldBe(0);
        HoloCardMath.Hue(new Vector(1, 1), 0).ShouldBe(90, 1e-9);
        HoloCardMath.Hue(new Vector(0, 0), 0).ShouldBe(-90, 1e-9);
        // Measured at θ = 273.8°: hue +89.8, foil +59.9 %.
        HoloCardMath.Hue(new Vector(0.5, 0.5), 273.8).ShouldBe(89.8, 0.05);
        HoloCardMath.FoilPosition(new Vector(0.5, 0.5), 273.8).X.ShouldBe(0.5 + 0.599, 0.001);
        HoloCardMath.GlitterDrift(new Vector(1, 0)).ShouldBe(new Vector(-18, 18));
    }

    [Fact]
    public void Shine_rests_at_a_third_rises_on_hover_and_mid_flip()
    {
        HoloCardMath.Shine(0, 0).ShouldBe(0.35);
        HoloCardMath.Shine(1, 0).ShouldBe(1);
        HoloCardMath.Shine(0, 90).ShouldBe(1, 1e-9);
        HoloCardMath.Shine(0, 180).ShouldBe(0.35, 1e-9);
    }

    [Fact]
    public void Flips_accumulate_without_wrapping()
    {
        var target = 0.0;
        target = HoloCardMath.NextFlipTarget(target, HoloCardFlipSide.Right);
        target.ShouldBe(180);
        HoloCardMath.IsFlippedAt(target).ShouldBeTrue();
        target = HoloCardMath.NextFlipTarget(target, HoloCardFlipSide.Keyboard);
        target.ShouldBe(360);
        HoloCardMath.IsFlippedAt(target).ShouldBeFalse();
        target = HoloCardMath.NextFlipTarget(target, HoloCardFlipSide.Left);
        target.ShouldBe(180);
        HoloCardMath.IsFlippedAt(-180).ShouldBeTrue();
    }

    [Theory]
    [InlineData(30, 252.8, 441.6)]
    [InlineData(60, 147.9, 471.8)]
    [InlineData(80, 51.7, 482.6)]
    [InlineData(89, 5.2, 484.0)]
    public void Flip_foreshortening_matches_the_measured_boxes(double angle, double width, double height)
    {
        var m = HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, angle);
        var bounds = HoloCardProjection.ProjectedBounds(m, 290, 406);
        bounds.Width.ShouldBe(width, 0.6);
        bounds.Height.ShouldBe(height, 0.6);
    }

    [Fact]
    public void A_thirty_degree_flip_brings_the_left_edge_near_and_shifts_the_box_left()
    {
        var m = HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, 30);
        var bounds = HoloCardProjection.ProjectedBounds(m, 290, 406);
        (bounds.Center.X - 145).ShouldBe(-10.2, 0.3);
        var leftTop = HoloCardProjection.Apply(m, new Point(0, 0));
        var leftBottom = HoloCardProjection.Apply(m, new Point(0, 406));
        var rightTop = HoloCardProjection.Apply(m, new Point(290, 0));
        var rightBottom = HoloCardProjection.Apply(m, new Point(290, 406));
        (leftBottom.Y - leftTop.Y).ShouldBe(441.6, 0.3);
        // The far edge is 406·900/972.5 = 375.7 (the record's 371.6 was read off the rounded corners).
        (rightBottom.Y - rightTop.Y).ShouldBe(375.7, 0.3);
    }

    [Fact]
    public void Projection_is_identity_at_rest_and_scales_about_the_centre()
    {
        var rest = HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, 0);
        HoloCardProjection.Apply(rest, new Point(10, 20)).X.ShouldBe(10, 1e-9);
        HoloCardProjection.Apply(rest, new Point(10, 20)).Y.ShouldBe(20, 1e-9);
        var lifted = HoloCardProjection.PlaneMatrix(290, 406, 900, 1.035, 0, 0);
        var corner = HoloCardProjection.Apply(lifted, new Point(0, 0));
        corner.X.ShouldBe(145 - 145 * 1.035, 1e-9);
        corner.Y.ShouldBe(203 - 203 * 1.035, 1e-9);
    }

    [Fact]
    public void Front_faces_the_viewer_until_ninety_degrees_and_the_back_after()
    {
        HoloCardProjection.FacesViewer(HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, 89), 290, 406).ShouldBeTrue();
        HoloCardProjection.FacesViewer(HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, 91), 290, 406).ShouldBeFalse();
        // The back carries its own half turn.
        HoloCardProjection.FacesViewer(HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 0, 91 + 180), 290, 406).ShouldBeTrue();
        // Tilted, the back at 180 still reads the right way round: its left edge stays on the left.
        var back = HoloCardProjection.PlaneMatrix(290, 406, 900, 1, 5, 180 + 180 + 7);
        HoloCardProjection.Apply(back, new Point(0, 203)).X.ShouldBeLessThan(HoloCardProjection.Apply(back, new Point(290, 203)).X);
    }

    [Theory]
    // Tilt step 0 → 12.414°: 100 ms 5.4°, 200 ms 9.8°, 300 ms 11.5°.
    [InlineData(230, 31, 0.1, 0.435)]
    [InlineData(230, 31, 0.2, 0.789)]
    [InlineData(230, 31, 0.3, 0.926)]
    // Light step 50 → 98.28 %: 100 ms 65.3 %, 200 ms 82.9 %, 300 ms 92.6 %.
    [InlineData(122, 20, 0.1, 0.317)]
    [InlineData(122, 20, 0.2, 0.681)]
    [InlineData(122, 20, 0.3, 0.882)]
    // Flip 180 → 360: 100 ms 34.8°, 200 ms 93.8°, 300 ms 137.7°, 500 ms 173.5°.
    [InlineData(77, 15.8, 0.1, 0.193)]
    [InlineData(77, 15.8, 0.2, 0.521)]
    [InlineData(77, 15.8, 0.3, 0.765)]
    [InlineData(77, 15.8, 0.5, 0.964)]
    public void Springs_follow_the_measured_steps(double stiffness, double damping, double seconds, double progress)
    {
        var spec = SpringSpec.FromPhysical(stiffness, damping);
        var (x, _) = SpringSolver.Evaluate(spec, -1, 0, seconds);
        (1 + x).ShouldBe(progress, 0.06);
    }

    [Fact]
    public void Glitter_tile_is_our_own_scatter_of_twenty_dots_in_three_sizes()
    {
        var dots = HoloCardSheen.GlitterDots;
        dots.Length.ShouldBe(20);
        dots.Count(d => d.Radius == 0.75).ShouldBe(4);
        dots.All(d => d.X is >= 0 and < 120 && d.Y is >= 0 and < 120).ShouldBeTrue();
        dots.Select(d => d.Radius).Distinct().Count().ShouldBe(3);
    }

    // ---- The control ----

    private static HoloCard Card(bool back = true, double width = 200) => new()
    {
        Width = width,
        Front = new Border { Child = new TextBlock { Text = "Front side" } },
        Back = back ? new Border { Child = new TextBlock { Text = "Back side" } } : null,
    };

    private static Point At(HoloCard card, Window window, double fx, double fy)
    {
        var size = card.CardSize;
        return card.TranslatePoint(new Point((card.Bounds.Width - size.Width) / 2 + size.Width * fx,
            (card.Bounds.Height - size.Height) / 2 + size.Height * fy), window) ?? default;
    }

    private static void Pump()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void ClickAt(Window window, Point point)
    {
        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    [AvaloniaFact]
    public void Defaults_to_a_300_by_420_card_and_follows_a_set_width()
    {
        var card = new HoloCard { Front = new Border() };
        var window = TestHelpers.Show(new Border { Width = 600, Height = 600, Child = card });
        card.CardSize.ShouldBe(new Size(300, 420));
        card.Bounds.Size.ShouldBe(new Size(300, 420));
        card.Width = 290;
        Pump();
        card.CardSize.ShouldBe(new Size(290, 406));
        card.MaxTilt.ShouldBe(14);
        card.Intensity.ShouldBe(1);
        card.Intensity = 3;
        card.Intensity.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Clicks_flip_by_half_and_accumulate()
    {
        var card = Card();
        var window = TestHelpers.Show(card);
        var events = new List<HoloCardFlippedEventArgs>();
        card.FlippedChanged += (_, e) => events.Add(e);

        ClickAt(window, At(card, window, 0.75, 0.5));
        card.IsFlipped.ShouldBeTrue();
        card.FlipTarget.ShouldBe(180);
        ClickAt(window, At(card, window, 0.8, 0.5));
        card.IsFlipped.ShouldBeFalse();
        card.FlipTarget.ShouldBe(360);
        ClickAt(window, At(card, window, 0.2, 0.5));
        card.IsFlipped.ShouldBeTrue();
        card.FlipTarget.ShouldBe(180);

        events.Select(e => e.Side).ShouldBe([HoloCardFlipSide.Right, HoloCardFlipSide.Right, HoloCardFlipSide.Left]);
        events.Select(e => e.IsFlipped).ShouldBe([true, false, true]);
        card.Classes.Contains(HoloCard.FlippedPseudoClass).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_and_space_flip_to_the_right()
    {
        var card = Card();
        var window = TestHelpers.Show(card);
        card.Focusable.ShouldBeTrue();
        card.Focus(NavigationMethod.Tab);
        var sides = new List<HoloCardFlipSide>();
        card.FlippedChanged += (_, e) => sides.Add(e.Side);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        card.FlipTarget.ShouldBe(180);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        card.FlipTarget.ShouldBe(360);
        card.IsFlipped.ShouldBeFalse();
        sides.ShouldBe([HoloCardFlipSide.Keyboard, HoloCardFlipSide.Keyboard]);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        card.FlipTarget.ShouldBe(360);
        window.Close();
    }

    [AvaloniaFact]
    public void Without_a_back_the_card_only_tilts()
    {
        var card = Card(back: false);
        var window = TestHelpers.Show(card);
        card.Focusable.ShouldBeFalse();
        card.Classes.Contains(HoloCard.TiltOnlyPseudoClass).ShouldBeTrue();
        var raised = 0;
        card.FlippedChanged += (_, _) => raised++;
        ClickAt(window, At(card, window, 0.75, 0.5));
        card.Flip();
        card.IsFlipped = true;
        Pump();
        raised.ShouldBe(0);
        card.FlipTarget.ShouldBe(0);
        card.FrontFace!.IsVisible.ShouldBeTrue();
        card.BackFace!.IsVisible.ShouldBeFalse();
        var peer = ControlAutomationPeer.CreatePeerForElement(card);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        window.Close();
    }

    [AvaloniaFact]
    public void Setting_is_flipped_turns_the_card_quietly_and_flip_raises_programmatic()
    {
        var card = Card();
        card.IsFlipped = true;
        var window = TestHelpers.Show(card);
        card.FlipTarget.ShouldBe(180);
        card.FlipAngle.ShouldBe(180);
        card.FrontFace!.IsVisible.ShouldBeFalse();
        card.BackFace!.IsVisible.ShouldBeTrue();
        var events = new List<HoloCardFlippedEventArgs>();
        card.FlippedChanged += (_, e) => events.Add(e);
        card.IsFlipped = false;
        events.ShouldBeEmpty();
        card.FlipTarget.ShouldBe(360);
        card.Flip();
        events.Single().Side.ShouldBe(HoloCardFlipSide.Programmatic);
        events.Single().IsFlipped.ShouldBeTrue();
        card.IsFlipped.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Pointer_tilts_lifts_and_lights_the_card_then_settles_on_leave()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = Card();
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        Pump();
        window.MouseMove(At(card, window, 0.5, 0.5), RawInputModifiers.None);
        window.MouseMove(At(card, window, 0.99, 0.01), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        card.TiltValue.X.ShouldBe(13.72, 0.6);
        card.TiltValue.Y.ShouldBe(13.72, 0.6);
        card.LightValue.X.ShouldBe(0.99, 0.02);
        card.HoverValue.ShouldBe(1, 0.01);
        card.Shadow!.Offset.X.ShouldBe(-12.35, 0.6);
        card.Shadow.Alpha.ShouldBe(0.40, 0.01);
        card.FrontSheen!.Shine.ShouldBe(1, 0.01);
        // The top right corner sinks: it is drawn smaller than the bottom left one.
        var m = card.FrontMatrix;
        var topRight = HoloCardProjection.Apply(m, new Point(card.CardSize.Width, 0));
        topRight.X.ShouldBeLessThan(card.CardSize.Width);

        window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1.5), Frame);
        card.TiltValue.ShouldBe(default);
        card.LightValue.ShouldBe(new Vector(0.5, 0.5));
        card.HoverValue.ShouldBe(0);
        card.FrontSheen.Shine.ShouldBe(0.35, 1e-9);
        window.Close();
    }

    [AvaloniaFact]
    public void Flip_spins_with_a_spring_swaps_faces_at_ninety_and_keeps_velocity_when_re_aimed()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = Card();
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        Pump();
        card.Flip(HoloCardFlipSide.Right);
        card.Classes.Contains(HoloCard.FlippingPseudoClass).ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        // Measured 34.8° at 100 ms after the click; the spring itself starts a frame or so later there.
        card.FlipAngle.ShouldBeInRange(20, 60);
        card.FrontFace!.IsVisible.ShouldBeTrue();
        card.BackFace!.IsVisible.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(150), Frame);
        card.FlipAngle.ShouldBeGreaterThan(90);
        card.FrontFace.IsVisible.ShouldBeFalse();
        card.BackFace.IsVisible.ShouldBeTrue();
        // A second press mid-spin aims at 360 and carries on from the current speed.
        var before = card.FlipAngle;
        card.Flip(HoloCardFlipSide.Right);
        clock.Advance(Frame, Frame);
        (card.FlipAngle - before).ShouldBeGreaterThan(3);
        clock.Advance(TimeSpan.FromSeconds(2), Frame);
        card.FlipAngle.ShouldBe(360);
        card.Classes.Contains(HoloCard.FlippingPseudoClass).ShouldBeFalse();
        card.FrontFace.IsVisible.ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_does_not_tilt_and_cross_fades_the_faces()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = Card();
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        Pump();
        window.MouseMove(At(card, window, 0.9, 0.1), RawInputModifiers.None);
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        card.TiltValue.ShouldBe(default);
        card.HoverValue.ShouldBe(0);
        ClickAt(window, At(card, window, 0.9, 0.1));
        card.IsFlipped.ShouldBeTrue();
        clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
        card.FadeValue.ShouldBeInRange(0.2, 0.99);
        card.FrontFace!.IsVisible.ShouldBeTrue();
        card.BackFace!.IsVisible.ShouldBeTrue();
        card.FrontMatrix.ShouldBe(Matrix.Identity);
        card.BackMatrix.ShouldBe(Matrix.Identity);
        clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
        card.FadeValue.ShouldBe(1);
        card.FrontFace.IsVisible.ShouldBeFalse();
        card.BackFace.Opacity.ShouldBe(1);
        card.FrontSheen!.ShowsGlitter.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Touch_drag_tilts_without_lifting_and_a_tap_flips()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var card = Card();
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        Pump();
        // A drag across the card tilts it (no hover lift for a finger) and does not flip it.
        var drag = window.TouchBegin(At(card, window, 0.5, 0.5));
        window.TouchMove(drag, At(card, window, 0.95, 0.1));
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        card.TiltValue.Y.ShouldBeGreaterThan(10);
        card.HoverValue.ShouldBe(0);
        window.TouchEnd(drag, At(card, window, 0.95, 0.1));
        card.IsFlipped.ShouldBeFalse();
        clock.Advance(TimeSpan.FromSeconds(1), Frame);
        card.TiltValue.ShouldBe(default);
        // A tap flips by the half it lands on.
        var tap = window.TouchBegin(At(card, window, 0.25, 0.5));
        window.TouchEnd(tap, At(card, window, 0.25, 0.5));
        card.IsFlipped.ShouldBeTrue();
        card.FlipTarget.ShouldBe(-180);
        window.Close();
    }

    [AvaloniaFact]
    public void Automation_is_a_toggle_button_named_by_its_label_or_face_text()
    {
        var card = Card();
        var window = TestHelpers.Show(card);
        var peer = ControlAutomationPeer.CreatePeerForElement(card);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        peer.GetLocalizedControlType().ShouldBe("flip card");
        peer.GetName().ShouldBe("Front side");
        card.Label = "Mira Okafor, Lumen Society patron pass";
        peer.GetName().ShouldBe("Mira Okafor, Lumen Society patron pass");
        var toggle = peer.GetProvider<IToggleProvider>()!;
        toggle.ToggleState.ShouldBe(ToggleState.Off);
        toggle.Toggle();
        card.IsFlipped.ShouldBeTrue();
        toggle.ToggleState.ShouldBe(ToggleState.On);
        card.Label = null;
        peer.GetName().ShouldBe("Back side");
        window.Close();
    }
}
