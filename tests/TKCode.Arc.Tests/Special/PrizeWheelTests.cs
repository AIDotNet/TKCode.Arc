using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Prize wheel: the weighted draw, the spin's path and pegs, and the control's input, events and motion levels.</summary>
public class PrizeWheelTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static PrizeWheelModel Model(bool sizeByWeight = true, params double[] weights)
    {
        var model = new PrizeWheelModel();
        model.Load(weights.Length == 0 ? [1, 1, 2, 0, 4] : weights, sizeByWeight);
        return model;
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
    public void A_spin_turns_at_least_the_given_turns_and_lands_inside_its_slice()
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
            for (var t = 0.0; t < 4.2; t += 1.0 / 60)
            {
                model.Step(1.0 / 60);
            }
            model.IsSpinning.ShouldBeFalse();
            (model.Rotation - before).ShouldBeGreaterThanOrEqualTo(5 * 360);
            model.AtPointer().ShouldBe(target);
        }
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
    public void Every_peg_that_passes_the_pointer_is_counted_once()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1, 1, 1, 1, 1);
        model.Plan(0, 0.5, 3, 3);
        for (var t = 0.0; t < 3.2; t += 1.0 / 60)
        {
            model.Step(1.0 / 60);
        }
        // Each full turn passes all 8 pegs; the rest of the way passes one per 45 degrees.
        var expected = (int)Math.Floor(model.Rotation / 45);
        model.Ticks.ShouldBe(expected);
    }

    [Fact]
    public void A_peg_pushes_the_flapper_and_releases_it()
    {
        var model = Model(sizeByWeight: false, 1, 1, 1, 1);
        // The peg at 90 degrees sits 4 degrees before the pointer after turning 266 degrees.
        model.Plan(0, 0.5, 1, 10);
        var pushed = false;
        for (var t = 0.0; t < 10.5; t += 1.0 / 120)
        {
            model.Step(1.0 / 120);
            pushed |= model.Flap > PrizeWheelModel.MaxFlap * 0.5;
        }
        pushed.ShouldBeTrue();
        for (var t = 0.0; t < 2; t += 1.0 / 120)
        {
            model.Step(1.0 / 120);
        }
        model.IsFlapMoving.ShouldBeFalse();
        model.Flap.ShouldBe(model.Contact(), 1e-6);
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
        Random = () => draw,
        Segments =
        {
            new PrizeWheelSegment("10% off"),
            new PrizeWheelSegment("Free tote", 2),
            new PrizeWheelSegment("Sticker pack", 3),
            new PrizeWheelSegment("Try again", 0)
        }
    };

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
    public void Enter_space_click_and_invoke_spin_the_wheel()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        var spins = 0;
        wheel.SpinCompleted += (_, _) => spins++;
        wheel.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        var center = wheel.TranslatePoint(new Point(150, 160), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
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
    public void A_wheel_for_show_ignores_people()
    {
        var wheel = Wheel();
        wheel.IsInteractive = false;
        var window = TestHelpers.Show(wheel);
        wheel.Focusable.ShouldBeFalse();
        wheel.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
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
        frames.Advance(TimeSpan.FromSeconds(0.3), Frame);
        wheel.Highlight.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Is_a_named_button_that_announces_the_prize()
    {
        var wheel = Wheel();
        var window = TestHelpers.Show(wheel);
        AutomationProperties.GetName(wheel).ShouldBe("Prize wheel");
        var peer = ControlAutomationPeer.CreatePeerForElement(wheel);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
        wheel.Spin();
        peer.GetHelpText().ShouldBe("Sticker pack");
        TestHelpers.Descendant<TextBlock>(wheel, PrizeWheel.StatusPart).Text!.Trim().ShouldBe("Sticker pack");
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
