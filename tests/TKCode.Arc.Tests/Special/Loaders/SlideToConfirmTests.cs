using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>SlideToConfirm: geometry, the slide and confirm cycle, the action states, keyboard, accessibility and motion.</summary>
public class SlideToConfirmTests
{
    private sealed class Fixture
    {
        public SlideToConfirm Slider = null!;
        public Window Window = null!;
        public readonly List<SlideToConfirmStateChangedEventArgs> Changes = new();
        public int Confirmed;

        public static Fixture Create(double width = 350, ArcMotionLevel level = ArcMotionLevel.Off, Action<SlideToConfirm>? setup = null)
        {
            var f = new Fixture();
            f.Slider = new SlideToConfirm { Width = width, HorizontalAlignment = HorizontalAlignment.Left, IsHintEnabled = false };
            setup?.Invoke(f.Slider);
            f.Slider.StateChanged += (_, e) => f.Changes.Add(e);
            f.Slider.Confirmed += (_, _) => f.Confirmed++;
            f.Window = TestHelpers.Show(f.Slider, level);
            f.Pump();
            return f;
        }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>A window point on the track's centre line, <paramref name="x"/> from the slider's left edge.</summary>
        public Point At(double x) => Slider.TranslatePoint(new Point(x, Slider.Bounds.Height / 2), Window)!.Value;

        public double ThumbX => (Slider.TrackSize.Height / 2) + Slider.ThumbOffset;

        public void Press(double x)
        {
            Window.MouseMove(At(x), RawInputModifiers.None);
            Window.MouseDown(At(x), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        public void Move(double x) => Window.MouseMove(At(x), RawInputModifiers.LeftMouseButton);

        public void Release(double x)
        {
            Window.MouseUp(At(x), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Drags the thumb by <paramref name="dx"/> in <paramref name="steps"/> moves and lets go.</summary>
        public void Slide(double dx, int steps = 10, bool release = true)
        {
            var from = ThumbX;
            Press(from);
            for (var i = 1; i <= steps; i++)
            {
                Move(from + (dx * i / steps));
                Slider.AdvanceFrames(16);
            }
            Slider.AdvanceFrames(300);
            if (release)
            {
                Release(from + dx);
            }
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Arc_Demo()
    {
        var f = Fixture.Create();
        var s = f.Slider;
        s.Label.ShouldBe("Slide to confirm");
        s.PendingLabel.ShouldBe("Processing");
        s.ConfirmedLabel.ShouldBe("Confirmed");
        s.ErrorLabel.ShouldNotBeNullOrWhiteSpace();
        s.Threshold.ShouldBe(0.9);
        s.ResetAfter.ShouldBeNull();
        s.State.ShouldBe(SlideToConfirmState.Idle);
        s.Progress.ShouldBe(0);
        s.Bounds.Height.ShouldBe(56);
        s.TrackSize.Travel.ShouldBe(294);
        s.ThumbShape.Rx.ShouldBe(23);
        s.Focusable.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Track_Is_64_Tall_From_360_Wide_And_At_Least_200_Wide()
    {
        Fixture.Create(width: 359).Slider.Bounds.Height.ShouldBe(56);
        var tall = Fixture.Create(width: 360).Slider;
        tall.Bounds.Height.ShouldBe(64);
        tall.ThumbShape.Rx.ShouldBe(27);
        tall.TrackSize.Travel.ShouldBe(296);
        Fixture.Create(width: 120).Slider.Bounds.Width.ShouldBe(200);
    }

    [AvaloniaFact]
    public void Threshold_Is_Kept_Between_A_Half_And_One()
    {
        var s = new SlideToConfirm { Threshold = 0.2 };
        s.Threshold.ShouldBe(0.5);
        s.Threshold = 3;
        s.Threshold.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Letting_Go_Before_The_Threshold_Springs_Back_Without_Confirming()
    {
        var f = Fixture.Create();
        f.Slide(200, release: false);
        f.Slider.State.ShouldBe(SlideToConfirmState.Dragging);
        f.Slider.Progress.ShouldBe(200.0 / 294, 0.01);
        f.Release(f.ThumbX);
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
        f.Slider.Progress.ShouldBe(0);
        f.Confirmed.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Letting_Go_Past_The_Threshold_Confirms_And_Without_An_Action_Shows_Success()
    {
        var f = Fixture.Create();
        f.Slide(270);
        f.Confirmed.ShouldBe(1);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
        f.Slider.Progress.ShouldBe(1);
        f.Slider.RetreatProgress.ShouldBe(1);
        f.Changes.Select(c => c.NewState).ShouldBe([SlideToConfirmState.Dragging, SlideToConfirmState.Done]);
        var v = f.Slider.Visuals;
        v.Check.ShouldBe(1);
        v.Success.ShouldBe(1);
        v.Disc.ShouldBe(0);
        v.Label.ShouldBe(0);
        v.Status.ShouldBe(1);
        f.Slider.Announcer.History.ShouldBe(["Confirmed"]);
    }

    [AvaloniaFact]
    public void Pulling_Past_The_End_Stretches_On_A_Rubber_Band()
    {
        var f = Fixture.Create();
        f.Slide(294 + 56, release: false);
        f.Slider.ThumbOffset.ShouldBe(294 + SlideToConfirmMath.RubberThumb(56, 1), 0.01);
        f.Slider.ThumbOffset.ShouldBeInRange(294 + 12, 294 + 16);
        f.Slider.Progress.ShouldBe(1);
        f.Release(f.ThumbX);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
    }

    [AvaloniaFact]
    public void A_Pending_Action_Shows_The_Spinner_Then_The_Check()
    {
        var work = new TaskCompletionSource();
        var f = Fixture.Create(setup: s =>
        {
            s.PendingLabel = "Sending";
            s.ConfirmedLabel = "Sent";
            s.ConfirmAction = () => work.Task;
        });
        f.Slide(280);
        f.Slider.State.ShouldBe(SlideToConfirmState.Pending);
        f.Slider.Visuals.Spinner.ShouldBe(1);
        f.Slider.Visuals.Ink.ShouldBe(0);
        f.Slider.AutomationValueText.ShouldBe("Sending");

        // Input is ignored while pending.
        f.Key(PhysicalKey.Home);
        f.Slider.Progress.ShouldBe(1);

        work.SetResult();
        TestHelpers.PumpUntil(() => f.Slider.State == SlideToConfirmState.Done).ShouldBeTrue();
        f.Slider.Visuals.Check.ShouldBe(1);
        f.Slider.Announcer.History.ShouldBe(["Sending", "Sent"]);
        f.Changes.Select(c => c.NewState).ShouldBe([SlideToConfirmState.Dragging, SlideToConfirmState.Pending, SlideToConfirmState.Done]);
    }

    [AvaloniaFact]
    public void A_Failing_Action_Returns_With_The_Error_Label_Until_The_Next_Slide()
    {
        var failure = new InvalidOperationException("declined");
        var f = Fixture.Create(setup: s => s.ConfirmAction = () => Task.FromException(failure));
        f.Slide(280);
        TestHelpers.PumpUntil(() => f.Slider.State == SlideToConfirmState.Error).ShouldBeTrue();
        f.Changes[^1].Error.ShouldBeSameAs(failure);
        f.Slider.ShowsError.ShouldBeTrue();
        f.Slider.Progress.ShouldBe(0);
        f.Slider.RetreatProgress.ShouldBe(0);
        f.Slider.Announcer.History[^1].ShouldBe(f.Slider.ErrorLabel);

        f.Slide(40, release: false);
        f.Slider.ShowsError.ShouldBeFalse();
        f.Slider.State.ShouldBe(SlideToConfirmState.Dragging);
    }

    [AvaloniaFact]
    public void Reset_And_ResetAfter_Return_To_Idle()
    {
        var f = Fixture.Create();
        f.Slide(280);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
        f.Slider.Reset();
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
        f.Slider.Progress.ShouldBe(0);
        f.Slider.RetreatProgress.ShouldBe(0);
        f.Slider.Visuals.Check.ShouldBe(0);
        f.Slider.Visuals.Disc.ShouldBe(1);

        f.Slider.ResetAfter = TimeSpan.FromMilliseconds(50);
        f.Slide(280);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
        TestHelpers.PumpUntil(() => f.Slider.State == SlideToConfirmState.Idle).ShouldBeTrue();
        f.Slider.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Keys_Step_In_Tenths_And_Quarters_Home_Returns_End_Confirms()
    {
        var f = Fixture.Create();
        f.Slider.Focus(NavigationMethod.Tab);
        f.Key(PhysicalKey.ArrowRight);
        f.Slider.Progress.ShouldBe(0.1, 1e-9);
        f.Key(PhysicalKey.ArrowUp);
        f.Slider.Progress.ShouldBe(0.2, 1e-9);
        f.Key(PhysicalKey.ArrowLeft);
        f.Slider.Progress.ShouldBe(0.1, 1e-9);
        f.Key(PhysicalKey.Home);
        f.Slider.Progress.ShouldBe(0);
        f.Key(PhysicalKey.PageUp);
        f.Slider.Progress.ShouldBe(0.3, 1e-9);
        f.Key(PhysicalKey.PageUp);
        f.Slider.Progress.ShouldBe(0.6, 1e-9);
        f.Key(PhysicalKey.PageDown);
        f.Slider.Progress.ShouldBe(0.4, 1e-9);
        f.Confirmed.ShouldBe(0);
        f.Key(PhysicalKey.End);
        f.Confirmed.ShouldBe(1);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Never_Confirm()
    {
        var f = Fixture.Create();
        f.Slider.Focus(NavigationMethod.Tab);
        for (var i = 0; i < 12; i++)
        {
            f.Key(PhysicalKey.ArrowRight);
        }
        f.Slider.Progress.ShouldBe(1);
        f.Confirmed.ShouldBe(0);
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
    }

    [AvaloniaFact]
    public void Space_Without_Motion_Confirms_At_Once()
    {
        var f = Fixture.Create();
        f.Slider.Focus(NavigationMethod.Tab);
        f.Key(PhysicalKey.Space);
        f.Confirmed.ShouldBe(1);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
    }

    [AvaloniaFact]
    public void Disabled_Ignores_Pointer_And_Keys()
    {
        var f = Fixture.Create(setup: s => s.IsEnabled = false);
        f.Slide(280);
        f.Slider.Focus(NavigationMethod.Tab);
        f.Key(PhysicalKey.End);
        f.Confirmed.ShouldBe(0);
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
        f.Slider.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Press_Away_From_The_Thumb_Does_Not_Grab_It()
    {
        var f = Fixture.Create();
        f.Press(f.ThumbX + 40);
        f.Move(f.ThumbX + 200);
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
        f.Slider.Progress.ShouldBe(0);
        f.Release(f.ThumbX + 200);

        f.Press(f.ThumbX + 34);
        f.Slider.State.ShouldBe(SlideToConfirmState.Dragging);
        f.Release(f.ThumbX);
    }

    [AvaloniaFact]
    public void Confirm_From_Code_Completes_The_Slide()
    {
        var f = Fixture.Create();
        f.Slider.Confirm();
        f.Confirmed.ShouldBe(1);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
        f.Slider.Confirm();
        f.Confirmed.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Automation_Peer_Is_A_Named_Slider_In_Percent()
    {
        var work = new TaskCompletionSource();
        var f = Fixture.Create(setup: s =>
        {
            s.Label = "Slide to send";
            s.ConfirmAction = () => work.Task;
        });
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Slider);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
        peer.GetName().ShouldBe("Slide to send");
        var range = peer.GetProvider<IRangeValueProvider>().ShouldNotBeNull();
        range.Minimum.ShouldBe(0);
        range.Maximum.ShouldBe(100);
        range.Value.ShouldBe(0);
        range.IsReadOnly.ShouldBeFalse();
        range.SetValue(40);
        f.Slider.Progress.ShouldBe(0.4, 1e-9);
        range.Value.ShouldBe(40);
        f.Slider.AutomationValueText.ShouldBe("40 percent");
        range.SetValue(100);
        f.Slider.State.ShouldBe(SlideToConfirmState.Pending);
        range.IsReadOnly.ShouldBeTrue();
        work.SetResult();
    }

    [AvaloniaFact]
    public void Full_Motion_Swells_On_Press_Lags_The_Liquid_And_Springs_Back()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Full);
        f.Press(f.ThumbX);
        f.Slider.AdvanceFrames(600);
        var pressed = f.Slider.ThumbShape;
        pressed.Rx.ShouldBe(23.9, 0.05);
        pressed.Head.ShouldBe(24.9, 0.05);
        pressed.Swell.ShouldBe(2.4, 0.1);

        // A fast drag: the thumb leads, the tail lags, the thumb stretches along the drag.
        var from = f.ThumbX;
        for (var i = 1; i <= 6; i++)
        {
            f.Move(from + (20 * i));
            f.Slider.AdvanceFrames(16);
        }
        (f.Slider.ThumbOffset - f.Slider.TailOffset).ShouldBeGreaterThan(20);
        (f.Slider.ThumbShape.Rx / f.Slider.ThumbShape.Ry).ShouldBeGreaterThan(1.1);

        f.Release(from + 120);
        f.Slider.AdvanceFrames(1500);
        f.Slider.ThumbOffset.ShouldBe(0, 0.05);
        f.Slider.TailOffset.ShouldBe(0, 0.1);
        f.Slider.ThumbShape.Swell.ShouldBe(0, 0.02);
        f.Slider.State.ShouldBe(SlideToConfirmState.Idle);
    }

    [AvaloniaFact]
    public void Full_Motion_Confirm_Draws_The_Track_Into_The_Thumb_Then_Morphs_To_The_Check()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Full);
        f.Slide(300);
        f.Slider.State.ShouldBe(SlideToConfirmState.Done);
        f.Slider.RetreatProgress.ShouldBeLessThan(0.5);
        f.Slider.AdvanceFrames(1500);
        f.Slider.RetreatProgress.ShouldBe(1, 0.01);
        var v = f.Slider.Visuals;
        v.Morph.ShouldBe(1, 0.01);
        v.Check.ShouldBe(1, 0.01);
        v.Disc.ShouldBe(0, 0.01);
        f.Slider.ThumbOffset.ShouldBe(294, 0.1);

        f.Slider.Reset();
        f.Slider.AdvanceFrames(2000);
        f.Slider.RetreatProgress.ShouldBe(0, 0.01);
        f.Slider.ThumbOffset.ShouldBe(0, 0.1);
        f.Slider.Visuals.Disc.ShouldBe(1, 0.01);
        f.Slider.Visuals.Label.ShouldBe(1, 0.01);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Drops_The_Liquid_Stretch_And_Swell()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Reduced);
        f.Press(f.ThumbX);
        var from = f.ThumbX;
        f.Move(from + 120);
        f.Slider.AdvanceFrames(16);
        f.Slider.ThumbOffset.ShouldBe(120, 0.01);
        f.Slider.TailOffset.ShouldBe(120, 0.01);
        f.Slider.ThumbShape.Rx.ShouldBe(23);
        f.Slider.ThumbShape.Swell.ShouldBe(0);
        f.Release(from + 120);
        f.Slider.AdvanceFrames(16);
        f.Slider.ThumbOffset.ShouldBe(0);

        // After a confirm and a reset the thumb is back at the start.
        f.Slide(300);
        f.Slider.AdvanceFrames(50);
        f.Slider.Visuals.Check.ShouldBe(1);
        f.Slider.Reset();
        f.Slider.AdvanceFrames(50);
        f.Slider.ThumbOffset.ShouldBe(0);
        f.Slider.Progress.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Hint_Is_Scheduled_Only_With_Full_Motion_And_Stops_After_A_Touch()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Full, setup: s => s.IsHintEnabled = true);
        f.Slider.IsHintScheduled.ShouldBeTrue();
        f.Press(f.ThumbX);
        f.Release(f.ThumbX);
        f.Slider.AdvanceFrames(1000);
        f.Slider.IsHintScheduled.ShouldBeFalse();

        Fixture.Create(level: ArcMotionLevel.Reduced, setup: s => s.IsHintEnabled = true).Slider.IsHintScheduled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Swell_Pushes_The_Track_Outline_Out_Around_The_Thumb()
    {
        var s = new SlideToConfirm();
        s.BuildOutline(0, 350, 0, 28, 28, 2.4, 22);
        s.OutlineCount.ShouldBeGreaterThan(100);
        s.OutlineTopNear(30, 2).ShouldBe(-2.4, 0.15);
        s.OutlineTopNear(200, 2).ShouldBe(0, 0.01);
    }

    [Fact]
    public void Math_Matches_The_Measured_Curves()
    {
        SlideToConfirmMath.PageStep(0, 1).ShouldBe(0.3, 1e-9);
        SlideToConfirmMath.PageStep(0.3, 1).ShouldBe(0.6, 1e-9);
        SlideToConfirmMath.PageStep(0.6, -1).ShouldBe(0.4, 1e-9);
        SlideToConfirmMath.PageStep(0.9, 1).ShouldBe(1);
        SlideToConfirmMath.ArrowStep(0.37, 1).ShouldBe(0.5, 1e-9);
        SlideToConfirmMath.ArrowStep(0, -1).ShouldBe(0);
        SlideToConfirmMath.LabelOpacity(1).ShouldBe(0.3, 1e-9);
        SlideToConfirmMath.LabelDrift(0.5).ShouldBe(6);
        SlideToConfirmMath.StretchRatio(150).ShouldBe(1.051, 1e-9);
        SlideToConfirmMath.StretchRatio(5000).ShouldBe(1.364, 1e-9);
        SlideToConfirmMath.NeckWaist(12, 23).ShouldBe(21.2, 0.1);
        SlideToConfirmMath.NeckWaist(82, 23).ShouldBe(10.7, 0.3);
        SlideToConfirmMath.RubberThumb(20, 1).ShouldBe(7.1, 0.3);
        SlideToConfirmMath.RubberThumb(200, 1).ShouldBe(21.1, 0.3);
        SlideToConfirmMath.StatusOpacity(0.55).ShouldBe(0);
        SlideToConfirmMath.StatusOpacity(0.95).ShouldBe(1, 1e-9);
        SlideToConfirmMath.SpinnerOpacity(0.65).ShouldBe(0);
        SlideToConfirmMath.HintOffset(450).ShouldBe(18.2, 0.15);
        SlideToConfirmMath.HintOffset(SlideToConfirmMath.HintDuration + 1).ShouldBe(0);
    }
}
