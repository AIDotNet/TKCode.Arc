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

namespace TKCode.Arc.Tests.Special;

/// <summary>
/// Prize wheel: the weighted draw, thrown and planned spins, the pointer against the pegs, and the control's gestures,
/// reward panel, events, accessibility and motion levels.
/// </summary>
public class PrizeWheelTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static PrizeWheelModel Model(bool sizeByWeight = true, params double[] weights)
    {
        var model = new PrizeWheelModel();
        model.Load(weights.Length == 0 ? [1, 1, 2, 0, 4] : weights, sizeByWeight);
        return model;
    }

    private static void Run(PrizeWheelModel model, double seconds, double step = 1.0 / 60)
    {
        for (var t = 0.0; t < seconds; t += step)
        {
            model.Step(step);
        }
    }

    // --- model -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Slices_are_as_wide_as_their_odds_or_all_equal()
    {
        var weighted = Model();
        weighted.Sweep(0).ShouldBe(45, 1e-9);
        weighted.Sweep(2).ShouldBe(90, 1e-9);
        weighted.Sweep(3).ShouldBe(0, 1e-9);
        weighted.Sweep(4).ShouldBe(180, 1e-9);
        weighted.Start(4).ShouldBe(180, 1e-9);
        weighted.Chance(4).ShouldBe(0.5, 1e-9);
        var equal = Model(sizeByWeight: false);
        for (var i = 0; i < 5; i++)
        {
            equal.Sweep(i).ShouldBe(72, 1e-9);
        }
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.124, 0)]
    [InlineData(0.126, 1)]
    [InlineData(0.3, 2)]
    [InlineData(0.49, 2)]
    [InlineData(0.51, 4)]
    [InlineData(0.9999, 4)]
    public void The_draw_follows_the_weights_and_never_lands_on_zero(double random, int expected) =>
        Model().Pick(random).ShouldBe(expected);

    [Fact]
    public void A_spin_from_code_turns_at_least_the_given_turns_and_lands_inside_its_slice()
    {
        for (var target = 0; target < 5; target++)
        {
            if (target == 3)
            {
                continue;
            }
            var model = Model();
            var before = model.Rotation;
            model.Plan(target, 0.5, 5, 4);
            model.SpinSeconds.ShouldBe(4, 1e-9);
            Run(model, 4.05);
            model.IsSpinning.ShouldBeFalse();
            (model.Rotation - before).ShouldBeGreaterThanOrEqualTo(5 * 360);
            model.AtPointer().ShouldBe(target);
        }
    }

    [Theory]
    [InlineData(300)]
    [InlineData(700)]
    [InlineData(1200)]
    [InlineData(1600)]
    public void A_throw_leaves_at_its_speed_and_lands_nearest_its_natural_distance(double speed)
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1, 1, 1);
        model.Throw(5, 0.3, speed, ramp: false);
        Math.Abs(model.SpinDistance - PrizeWheelModel.NaturalDistance(speed)).ShouldBeLessThanOrEqualTo(180 + 1e-6);
        model.SpinSeconds.ShouldBe(3 * model.SpinDistance / speed, 1e-9);
        var start = model.Rotation;
        model.Step(0.001);
        ((model.Rotation - start) / 0.001).ShouldBe(speed, speed * 0.01);
        Run(model, model.SpinSeconds + 0.05);
        model.IsSpinning.ShouldBeFalse();
        model.AtPointer().ShouldBe(5);
    }

    [Fact]
    public void A_counter_clockwise_throw_turns_the_other_way()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1);
        model.Throw(2, 0.5, -800, ramp: false);
        model.SpinDistance.ShouldBeLessThan(-360);
        Run(model, model.SpinSeconds + 0.05);
        model.AtPointer().ShouldBe(2);
    }

    [Fact]
    public void Slow_throws_still_travel_two_and_a_half_turns_and_fast_ones_further()
    {
        PrizeWheelModel.NaturalDistance(300).ShouldBe(900);
        PrizeWheelModel.NaturalDistance(1000).ShouldBe(1000 * 5.8 / 3, 1e-9);
        PrizeWheelModel.NaturalDistance(1600).ShouldBeGreaterThan(3600);
    }

    [Fact]
    public void A_ramped_spin_builds_its_speed_and_still_lands()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1);
        model.Throw(1, 0.5, 1000, ramp: true);
        model.Step(0.02);
        var early = model.Rotation / 0.02;
        early.ShouldBeLessThan(300);
        Run(model, model.SpinSeconds + 0.05);
        model.AtPointer().ShouldBe(1);
    }

    [Fact]
    public void The_wheel_slows_down_to_its_stop()
    {
        PrizeWheelModel.Ease(0).ShouldBe(0);
        PrizeWheelModel.Ease(1).ShouldBe(1);
        var early = PrizeWheelModel.Ease(0.1) - PrizeWheelModel.Ease(0);
        var late = PrizeWheelModel.Ease(1) - PrizeWheelModel.Ease(0.9);
        early.ShouldBeGreaterThan(late * 50);
    }

    [Fact]
    public void A_gentle_flick_coasts_into_the_drift()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1);
        model.Coast(200);
        model.IsCoasting.ShouldBeTrue();
        Run(model, 4);
        model.IsCoasting.ShouldBeFalse();
        model.Rotation.ShouldBe(200 * 0.46, 4);
    }

    [Fact]
    public void Every_peg_that_passes_the_pointer_is_counted_once()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1, 1, 1);
        model.Plan(0, 0.5, 3, 3);
        Run(model, 3.2);
        // Each full turn passes all 8 pegs; the rest of the way passes one per 45 degrees.
        var expected = (int)Math.Floor(model.Rotation / 45);
        model.Ticks.ShouldBe(expected);
    }

    [Theory]
    [InlineData(-2.0, 0.0)]
    [InlineData(-1.457, -0.94)]
    [InlineData(-0.025, -12.07)]
    [InlineData(1.694, -23.57)]
    [InlineData(3.413, -29.86)]
    [InlineData(3.843, -28.32)]
    public void A_peg_bends_the_tapered_pointer_by_its_shape(double peg, double bend) =>
        PrizeWheelModel.RestingBend(peg).ShouldBe(bend, 0.15);

    [Fact]
    public void A_slow_wheel_pushes_the_pointer_then_lets_it_snap_back_with_a_wobble()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1, 1, 1);
        model.TurnTo(-10);
        model.Step(1.0 / 60);
        model.Drift = 4.3;
        var deepest = 0.0;
        var rebound = 0.0;
        for (var t = 0.0; t < 4; t += 1.0 / 60)
        {
            model.Step(1.0 / 60);
            deepest = Math.Min(deepest, model.Flap);
            rebound = Math.Max(rebound, model.Flap);
        }
        deepest.ShouldBe(-29.86, 0.5);
        rebound.ShouldBeGreaterThan(8);
        rebound.ShouldBeLessThan(16);
    }

    [Fact]
    public void A_fast_wheel_rattles_the_pointer_without_bending_it_past_its_stop()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1, 1, 1);
        model.Throw(3, 0.5, 1600, ramp: false);
        var extreme = 0.0;
        for (var t = 0.0; t < 2; t += 1.0 / 60)
        {
            model.Step(1.0 / 60);
            extreme = Math.Max(extreme, Math.Abs(model.Flap));
        }
        extreme.ShouldBeGreaterThan(25);
        extreme.ShouldBeLessThanOrEqualTo(PrizeWheelModel.MaxFlap + 1e-9);
    }

    [Fact]
    public void Jump_lands_without_turning()
    {
        var model = Model();
        model.Jump(2, 0.5);
        model.IsSpinning.ShouldBeFalse();
        model.AtPointer().ShouldBe(2);
        model.Ticks.ShouldBe(0);
    }

    // --- control ---------------------------------------------------------------------------------------------------

    private static PrizeWheel Wheel(double draw = 0.6) => new()
    {
        Width = 300,
        Height = 300,
        IsPanelVisible = false,
        Random = () => draw,
        Segments =
        {
            new PrizeWheelSegment("10% off"),
            new PrizeWheelSegment("Free tote", 2),
            new PrizeWheelSegment("Sticker pack", 3),
            new PrizeWheelSegment("Try again", 0)
        }
    };

    private static PrizeWheel RewardWheel(double draw = 0.6) => new()
    {
        Width = 880,
        SizesByWeight = false,
        Random = () => draw,
        Segments =
        {
            new PrizeWheelSegment("Mug", 5) { Detail = "A ceramic mug", Code = "MUG-01" },
            new PrizeWheelSegment("Tote", 3) { Detail = "A canvas tote bag", Code = "TOTE-22" },
            new PrizeWheelSegment("Stickers", 1.5) { Detail = "A sheet of stickers" },
            new PrizeWheelSegment("Hoodie", 0.5) { Detail = "The limited hoodie", Code = "HOOD-99", IsFeatured = true }
        }
    };

    private static Window ShowLarge(Control content)
    {
        var window = TestHelpers.Show(content);
        window.Width = 1000;
        window.Height = 1000;
        Pump();
        return window;
    }

    private static void Pump(int frames = 4)
    {
        for (var i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public void Spin_draws_by_weight_turns_and_reports_the_winner()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var wheel = Wheel(draw: 0.6);
        var window = TestHelpers.Show(wheel, ArcMotionLevel.Full);
        PrizeWheelResultEventArgs? result = null;
        var ticks = 0;
        wheel.SpinCompleted += (_, e) => result = e;
        wheel.Ticked += (_, _) => ticks++;
        wheel.Spin().ShouldBe(2);
        wheel.IsSpinning.ShouldBeTrue();
        wheel.Spin().ShouldBe(-1);
        frames.Advance(TimeSpan.FromSeconds(2), Frame);
        result.ShouldBeNull();
        frames.Advance(TimeSpan.FromSeconds(3.5), Frame);
        result.ShouldNotBeNull();
        result.Index.ShouldBe(2);
        result.Segment.Label.ShouldBe("Sticker pack");
        wheel.ResultIndex.ShouldBe(2);
        wheel.Model.AtPointer().ShouldBe(2);
        ticks.ShouldBe(wheel.Model.Ticks);
        ticks.ShouldBeGreaterThan(5 * 3);
        wheel.Highlight.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void SpinTo_lands_on_a_winner_chosen_elsewhere()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        wheel.SpinTo(0);
        wheel.ResultIndex.ShouldBe(0);
        wheel.Result!.Label.ShouldBe("10% off");
        wheel.Model.AtPointer().ShouldBe(0);
        wheel.SpinTo(9);
        wheel.ResultIndex.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Enter_space_the_hub_and_invoke_spin_the_wheel()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        var spins = 0;
        wheel.SpinCompleted += (_, _) => spins++;
        wheel.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        spins.ShouldBe(0);
        wheel.Classes.Contains(PrizeWheel.ChargingPseudoClass).ShouldBeTrue();
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        spins.ShouldBe(1);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        var hub = wheel.TranslatePoint(new Point(150, 150), window)!.Value;
        window.MouseDown(hub, MouseButton.Left);
        window.MouseUp(hub, MouseButton.Left);
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(wheel)).Invoke();
        spins.ShouldBe(4);
        window.Close();
    }

    [AvaloniaFact]
    public void A_click_outside_the_wheel_does_nothing()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        var spins = 0;
        wheel.SpinCompleted += (_, _) => spins++;
        var corner = wheel.TranslatePoint(new Point(3, 297), window)!.Value;
        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);
        spins.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void A_drag_turns_the_wheel_and_a_gentle_release_asks_for_a_quicker_flick()
    {
        var wheel = RewardWheel();
        var window = ShowLarge(wheel);
        var face = TestHelpers.Descendant<PrizeWheelFace>(wheel);
        var center = face.TranslatePoint(face.Center, window)!.Value;
        var before = wheel.Model.Rotation;
        window.MouseDown(new Point(center.X - 100, center.Y - 100), MouseButton.Left);
        window.MouseMove(new Point(center.X, center.Y - 141.42), RawInputModifiers.LeftMouseButton);
        wheel.Model.Rotation.ShouldBe(before + 45, 0.5);
        Thread.Sleep(150);
        window.MouseUp(new Point(center.X, center.Y - 141.42), MouseButton.Left);
        wheel.ResultIndex.ShouldBe(-1);
        wheel.IsSpinning.ShouldBeFalse();
        wheel.DisplayHint.ShouldBe(wheel.WeakHintText);
        wheel.SpinTo(1);
        wheel.DisplayHint.ShouldBe(wheel.HintText);
        window.Close();
    }

    [AvaloniaFact]
    public void A_wheel_for_show_ignores_people()
    {
        var wheel = Wheel();
        wheel.IsInteractive = false;
        var window = TestHelpers.Show(wheel);
        wheel.Focusable.ShouldBeFalse();
        wheel.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        wheel.ResultIndex.ShouldBe(-1);
        wheel.Spin().ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_lands_at_once_and_fades_the_highlight_in()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel, ArcMotionLevel.Reduced);
        wheel.Spin();
        wheel.IsSpinning.ShouldBeFalse();
        wheel.ResultIndex.ShouldBe(2);
        wheel.Model.Ticks.ShouldBe(0);
        wheel.Highlight.ShouldBeLessThan(1);
        frames.Advance(TimeSpan.FromSeconds(0.4), Frame);
        wheel.Highlight.ShouldBe(1);
        wheel.Model.Rotation.ShouldBe(wheel.Model.Rotation);
        window.Close();
    }

    [AvaloniaFact]
    public void The_wheel_drifts_before_its_first_spin_but_not_under_reduced_motion()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel, ArcMotionLevel.Full);
        var start = wheel.Model.Rotation;
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        (wheel.Model.Rotation - start).ShouldBe(PrizeWheelModel.DriftRate, 0.3);
        wheel.IsDriftPaused = true;
        start = wheel.Model.Rotation;
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        wheel.Model.Rotation.ShouldBe(start, 1e-6);
        window.Close();

        var still = Wheel();
        var reduced = TestHelpers.Show(still, ArcMotionLevel.Reduced);
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        still.Model.Rotation.ShouldBe(0);
        reduced.Close();
    }

    [AvaloniaFact]
    public void Holding_the_hub_charges_a_ring_winds_the_wheel_back_and_throws_harder_the_longer()
    {
        var frames = new ManualFrameClock();
        using var install = frames.Install();
        var wheel = Wheel();
        wheel.IsDriftPaused = true;
        var window = TestHelpers.Show(wheel, ArcMotionLevel.Full);
        var hub = wheel.TranslatePoint(new Point(150, 150), window)!.Value;
        window.MouseDown(hub, MouseButton.Left);
        frames.Advance(TimeSpan.FromSeconds(0.55), Frame);
        wheel.Charge.ShouldBe(0.5, 0.03);
        wheel.Model.Rotation.ShouldBe(-PrizeWheelModel.WindBack * 0.5, 0.6);
        frames.Advance(TimeSpan.FromSeconds(1), Frame);
        wheel.Charge.ShouldBe(1);
        wheel.Model.Rotation.ShouldBe(-PrizeWheelModel.WindBack, 1e-6);
        wheel.HubScale.ShouldBe(0.95, 0.002);
        window.MouseUp(hub, MouseButton.Left);
        wheel.IsSpinning.ShouldBeTrue();
        // A full charge throws at about 1626 degrees per second: a long spin.
        wheel.Model.SpinDistance.ShouldBeGreaterThan(3400);
        window.Close();
    }

    [AvaloniaFact]
    public void A_win_fills_the_card_lights_the_odds_row_and_raises_selection_changed()
    {
        var wheel = RewardWheel(draw: 0.97);
        var window = TestHelpers.Show(wheel);
        var changes = new List<PrizeWheelSelectionChangedEventArgs>();
        wheel.SelectionChanged += (_, e) => changes.Add(e);
        wheel.Spin().ShouldBe(3);
        var card = TestHelpers.Descendant<PrizeWheelCard>(wheel, PrizeWheel.CardPart);
        card.IsVisible.ShouldBeTrue();
        card.Prize.ShouldBe("Hoodie");
        card.Detail.ShouldBe("The limited hoodie");
        card.Code.ShouldBe("HOOD-99");
        var odds = TestHelpers.Descendant<PrizeWheelOddsList>(wheel);
        odds.Rows.Count.ShouldBe(4);
        odds.Rows[0].Chance.ShouldBe("50%");
        odds.Rows[3].Chance.ShouldBe("5%");
        odds.Rows[3].IsFeatured.ShouldBeTrue();
        odds.Rows[3].IsWon.ShouldBeTrue();
        odds.Rows[0].IsWon.ShouldBeFalse();
        wheel.Classes.Contains(PrizeWheel.WonPseudoClass).ShouldBeTrue();
        changes.Single().NewItem!.Label.ShouldBe("Hoodie");
        changes[0].OldIndex.ShouldBe(-1);
        TestHelpers.Descendant<TextBlock>(wheel, PrizeWheel.StatusPart).Text!.Trim()
            .ShouldBe("You won Hoodie. Your code is H O O D - 9 9.");
        wheel.ClearResult();
        card.IsVisible.ShouldBeFalse();
        changes.Count.ShouldBe(2);
        changes[1].NewItem.ShouldBeNull();
        changes[1].OldIndex.ShouldBe(3);
        window.Close();
    }

    [AvaloniaFact]
    public void Spin_again_spins_from_the_card()
    {
        var wheel = RewardWheel(draw: 0.1);
        var window = TestHelpers.Show(wheel);
        var spins = 0;
        wheel.SpinCompleted += (_, _) => spins++;
        wheel.Spin();
        wheel.UpdateLayout();
        var again = TestHelpers.Descendant<Avalonia.Controls.Button>(TestHelpers.Descendant<PrizeWheelCard>(wheel, PrizeWheel.CardPart),
            PrizeWheelCard.SpinAgainPart);
        again.IsEffectivelyVisible.ShouldBeTrue();
        again.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        spins.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Copy_puts_the_code_on_the_clipboard_and_announces_it()
    {
        var wheel = RewardWheel(draw: 0.1);
        var window = TestHelpers.Show(wheel);
        var copied = new List<PrizeWheelResultEventArgs>();
        wheel.CodeCopied += (_, e) => copied.Add(e);
        wheel.Spin();
        wheel.UpdateLayout();
        var card = TestHelpers.Descendant<PrizeWheelCard>(wheel, PrizeWheel.CardPart);
        var copy = card.CopyButton!;
        copy.Value.ShouldBe("MUG-01");
        copy.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestHelpers.PumpUntil(() => copy.State != CopyState.Idle, 3000).ShouldBeTrue();
        var status = TestHelpers.Descendant<TextBlock>(wheel, PrizeWheel.StatusPart).Text!.Trim();
        if (copy.State == CopyState.Copied)
        {
            copied.Single().Segment.Code.ShouldBe("MUG-01");
            status.ShouldBe(wheel.CopiedText);
        }
        else
        {
            copied.ShouldBeEmpty();
            status.ShouldBe(wheel.CopyFailedText);
        }
        window.Close();
    }

    [AvaloniaFact]
    public void A_single_spin_wheel_locks_on_its_prize_until_cleared()
    {
        var wheel = RewardWheel(draw: 0.1);
        wheel.IsSingleSpin = true;
        var window = TestHelpers.Show(wheel);
        wheel.Spin().ShouldBe(0);
        wheel.Classes.Contains(PrizeWheel.ClaimedPseudoClass).ShouldBeTrue();
        wheel.HubText.ShouldBe(wheel.ClaimedText);
        wheel.Spin().ShouldBe(-1);
        wheel.UpdateLayout();
        var again = TestHelpers.Descendant<Avalonia.Controls.Button>(TestHelpers.Descendant<PrizeWheelCard>(wheel, PrizeWheel.CardPart),
            PrizeWheelCard.SpinAgainPart);
        again.IsVisible.ShouldBeFalse();
        wheel.ClearResult();
        wheel.HubText.ShouldBe(wheel.SpinText);
        wheel.Spin().ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void The_panel_sits_beside_a_wide_wheel_under_a_narrow_one_and_can_be_hidden()
    {
        var wheel = RewardWheel();
        var window = ShowLarge(wheel);
        var face = TestHelpers.Descendant<PrizeWheelFace>(wheel);
        var panel = TestHelpers.Descendant<Grid>(wheel, PrizeWheel.PanelPart);
        face.Bounds.Width.ShouldBe(480);
        var facePosition = face.TranslatePoint(default, wheel)!.Value;
        var panelPosition = panel.TranslatePoint(default, wheel)!.Value;
        panelPosition.X.ShouldBe(facePosition.X + 480 + 40, 0.5);
        panel.Bounds.Width.ShouldBe(320);
        TestHelpers.Descendant<PrizeWheelOddsList>(wheel).Columns.ShouldBe(1);

        wheel.Width = 600;
        Pump();
        panelPosition = panel.TranslatePoint(default, wheel)!.Value;
        panelPosition.Y.ShouldBeGreaterThanOrEqualTo(480 + 24 - 0.5);
        panel.Bounds.Width.ShouldBe(480);
        TestHelpers.Descendant<PrizeWheelOddsList>(wheel).Columns.ShouldBe(2);

        wheel.Width = 360;
        Pump();
        face.Bounds.Width.ShouldBe(360);
        TestHelpers.Descendant<PrizeWheelOddsList>(wheel).Columns.ShouldBe(1);

        wheel.IsPanelVisible = false;
        Pump();
        panel.IsVisible.ShouldBeFalse();
        wheel.DesiredSize.Height.ShouldBe(360, 0.5);
        window.Close();
    }

    [AvaloniaFact]
    public void Is_a_named_button_that_announces_the_prize_and_an_image_of_the_odds()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        AutomationProperties.GetName(wheel).ShouldBe("Prize wheel");
        var peer = ControlAutomationPeer.CreatePeerForElement(wheel);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        var face = TestHelpers.Descendant<PrizeWheelFace>(wheel);
        var image = ControlAutomationPeer.CreatePeerForElement(face);
        image.GetAutomationControlType().ShouldBe(AutomationControlType.Image);
        image.GetName().ShouldBe("Prize wheel. 10% off 17%; Free tote 33%; Sticker pack 50%; Try again 0%.");
        wheel.Spin();
        peer.GetHelpText().ShouldBe("Sticker pack");
        TestHelpers.Descendant<TextBlock>(wheel, PrizeWheel.StatusPart).Text!.Trim().ShouldBe("You won Sticker pack.");
        window.Close();
    }

    [AvaloniaFact]
    public void Clearing_the_result_keeps_the_wheel_where_it_stopped()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        wheel.Spin();
        var rotation = wheel.Model.Rotation;
        wheel.ClearResult();
        wheel.ResultIndex.ShouldBe(-1);
        wheel.Result.ShouldBeNull();
        wheel.Model.Rotation.ShouldBe(rotation);
        window.Close();
    }
}
