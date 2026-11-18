using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>CelebrateButton: geometry, the press / pending / success / error cycle, the confetti system, motion levels and accessibility.</summary>
public class CelebrateButtonTests
{
    private sealed class Fixture
    {
        public CelebrateButton Button = null!;
        public Window Window = null!;
        public readonly List<CelebrateButtonStatusChangedEventArgs> Changes = new();
        public int Clicks;

        public static Fixture Create(ArcMotionLevel level = ArcMotionLevel.Off, Action<CelebrateButton>? setup = null)
        {
            var f = new Fixture();
            f.Button = new CelebrateButton { Content = "Ship release" };
            setup?.Invoke(f.Button);
            f.Button.StatusChanged += (_, e) => f.Changes.Add(e);
            f.Button.Click += (_, _) => f.Clicks++;
            f.Window = TestHelpers.Show(f.Button, level);
            f.Pump();
            return f;
        }

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        public void Click()
        {
            var center = Button.TranslatePoint(new Point(Button.Bounds.Width / 2, Button.Bounds.Height / 2), Window)!.Value;
            Window.MouseMove(center, RawInputModifiers.None);
            Window.MouseDown(center, MouseButton.Left);
            Window.MouseUp(center, MouseButton.Left);
            Pump();
        }
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Arc_Button()
    {
        var f = Fixture.Create();
        var b = f.Button;
        b.Status.ShouldBe(CelebrateButtonStatus.Idle);
        b.SuccessLabel.ShouldBe("Done");
        b.ErrorLabel.ShouldBe("Try again");
        b.PendingLabel.ShouldBeNull();
        b.ResetAfter.ShouldBe(TimeSpan.FromMilliseconds(2400));
        b.Cooldown.ShouldBe(TimeSpan.FromMilliseconds(1200));
        b.ParticleCount.ShouldBe(44);
        b.Bounds.Height.ShouldBe(40);
        b.FontSize.ShouldBe(13);
        b.FontWeight.ShouldBe(FontWeight.Medium);
        // The capsule is the label plus 20 px on each side.
        b.Bounds.Width.ShouldBe(b.Labels.ShownWidth + 40, 0.5);
        b.Labels.PendingText.ShouldBe("Ship release");
    }

    [AvaloniaFact]
    public void Press_Without_Action_Succeeds_And_Resets()
    {
        var f = Fixture.Create();
        f.Click();
        f.Clicks.ShouldBe(1);
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        f.Button.Labels.Active.ShouldBe(CelebrateButtonStatus.Success);
        f.Button.Classes.Contains(":success").ShouldBeTrue();
        f.Button.Announcer.History.ShouldBe(["Done"]);
        f.Button.IsResetPending.ShouldBeTrue();
        // Presses while done are ignored.
        f.Click();
        f.Clicks.ShouldBe(1);
        f.Button.FireResetTimer();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Idle);
        f.Button.Announcer.Announcement.ShouldBeNull();
        f.Changes.Select(c => c.NewStatus).ShouldBe([CelebrateButtonStatus.Success, CelebrateButtonStatus.Idle]);
    }

    [AvaloniaFact]
    public void Pending_Ignores_Presses_Then_Succeeds()
    {
        var work = new TaskCompletionSource();
        var runs = 0;
        var f = Fixture.Create(setup: b =>
        {
            b.PendingLabel = "Shipping";
            b.SuccessLabel = "Shipped";
            b.Action = () =>
            {
                runs++;
                return work.Task;
            };
        });
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Pending);
        f.Button.Labels.Active.ShouldBe(CelebrateButtonStatus.Pending);
        f.Button.Classes.Contains(":pending").ShouldBeTrue();
        f.Click();
        runs.ShouldBe(1);
        f.Clicks.ShouldBe(1);
        work.SetResult();
        f.Pump();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        f.Button.Announcer.History.ShouldBe(["Shipped"]);
        f.Changes.Select(c => (c.OldStatus, c.NewStatus)).ShouldBe([
            (CelebrateButtonStatus.Idle, CelebrateButtonStatus.Pending),
            (CelebrateButtonStatus.Pending, CelebrateButtonStatus.Success)]);
    }

    [AvaloniaFact]
    public void Failure_Shows_The_Error_Without_A_Burst_And_A_Press_Retries()
    {
        var fail = true;
        var f = Fixture.Create(ArcMotionLevel.Full, b => b.Action = () => fail ? Task.FromException(new InvalidOperationException("offline")) : Task.CompletedTask);
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Error);
        f.Changes[^1].Error.ShouldBeOfType<InvalidOperationException>();
        f.Button.Confetti.LiveCount.ShouldBe(0);
        f.Button.Announcer.History.ShouldBe(["Try again"]);
        fail = false;
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        f.Changes[^1].Error.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_Throwing_Action_Fails_Like_A_Faulted_Task()
    {
        var f = Fixture.Create(setup: b => b.Action = () => throw new InvalidOperationException("sync"));
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Error);
        f.Changes[^1].Error!.Message.ShouldBe("sync");
    }

    [AvaloniaFact]
    public void Setting_Success_From_Outside_Bursts_At_Full_Motion()
    {
        var f = Fixture.Create(ArcMotionLevel.Full);
        f.Button.Confetti.Random = new Random(7);
        f.Button.Status = CelebrateButtonStatus.Success;
        f.Pump();
        f.Button.Confetti.LiveCount.ShouldBe(44);
        f.Button.Labels.Active.ShouldBe(CelebrateButtonStatus.Success);
        // The kick peaks at 1.07 about 130 ms in and is back at rest by 460 ms.
        var peak = 1.0;
        for (var i = 0; i < 20; i++)
        {
            f.Button.Confetti.AdvanceFrames(1000.0 / 60);
            peak = Math.Max(peak, f.Button.KickScale);
        }
        peak.ShouldBe(1.07, 0.005);
        f.Button.Confetti.AdvanceFrames(400);
        f.Button.KickScale.ShouldBe(1);
        // Every piece has faded by 1.45 s; the loop stops with them.
        f.Button.Confetti.AdvanceFrames(1000);
        f.Button.Confetti.LiveCount.ShouldBe(0);
        f.Button.Confetti.IsActive.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Bursts_Respect_The_Cooldown()
    {
        var f = Fixture.Create(ArcMotionLevel.Full, b => b.Cooldown = TimeSpan.FromHours(1));
        f.Button.Celebrate();
        f.Button.Confetti.LiveCount.ShouldBe(44);
        f.Button.Celebrate();
        f.Button.Confetti.LiveCount.ShouldBe(44);
    }

    [AvaloniaFact]
    public void Live_Pieces_Are_Capped_At_180()
    {
        var f = Fixture.Create(ArcMotionLevel.Full, b =>
        {
            b.Cooldown = TimeSpan.Zero;
            b.ParticleCount = 100;
        });
        f.Button.Celebrate();
        f.Button.Celebrate();
        f.Button.Celebrate();
        f.Button.Confetti.LiveCount.ShouldBe(180);
        f.Button.ParticleCount = 500;
        f.Button.ParticleCount.ShouldBe(180);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Skips_The_Confetti_And_Kick()
    {
        var f = Fixture.Create(ArcMotionLevel.Reduced);
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        f.Button.Confetti.LiveCount.ShouldBe(0);
        f.Button.KickScale.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Label_Swap_Fades_Blurs_And_Drifts_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var work = new TaskCompletionSource();
        var f = Fixture.Create(ArcMotionLevel.Full, b =>
        {
            b.PendingLabel = "Shipping";
            b.SuccessLabel = "Shipped";
            b.Action = () => work.Task;
        });
        var idleWidth = f.Button.Labels.ShownWidth;
        f.Click();
        var labels = f.Button.Labels;
        clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
        // The pending text drops in from above while the resting text drifts down and fades.
        labels.DriftOf(CelebrateButtonStatus.Pending).ShouldBeLessThan(0);
        labels.DriftOf(CelebrateButtonStatus.Idle).ShouldBeGreaterThan(0);
        labels.ViewOf(CelebrateButtonStatus.Idle).Effect.ShouldNotBeNull();
        clock.Advance(TimeSpan.FromMilliseconds(600));
        f.Pump();
        labels.ViewOf(CelebrateButtonStatus.Pending).Opacity.ShouldBe(1);
        labels.DriftOf(CelebrateButtonStatus.Pending).ShouldBe(0, 0.05);
        labels.ViewOf(CelebrateButtonStatus.Idle).IsVisible.ShouldBeFalse();
        work.SetResult();
        f.Pump();
        clock.Advance(TimeSpan.FromMilliseconds(1000.0 / 60));
        // The success text rises from below.
        labels.DriftOf(CelebrateButtonStatus.Success).ShouldBeGreaterThan(0);
        clock.Advance(TimeSpan.FromMilliseconds(600));
        f.Pump();
        labels.ShownWidth.ShouldNotBe(idleWidth);
        labels.ViewOf(CelebrateButtonStatus.Success).Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Enter_Runs_The_Action()
    {
        var runs = 0;
        var f = Fixture.Create(setup: b => b.Action = () =>
        {
            runs++;
            return Task.CompletedTask;
        });
        f.Button.Focus(NavigationMethod.Tab);
        f.Window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        f.Pump();
        runs.ShouldBe(1);
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        TestHelpers.IsFocusRingVisible(f.Button).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Accessible_Name_Follows_The_Label_And_Pending_Is_Not_Enabled()
    {
        var work = new TaskCompletionSource();
        var f = Fixture.Create(setup: b =>
        {
            b.PendingLabel = "Shipping";
            b.Action = () => work.Task;
        });
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Button);
        peer.GetName().ShouldBe("Ship release");
        f.Click();
        peer.GetName().ShouldBe("Shipping");
        peer.IsEnabled().ShouldBeFalse();
        work.SetResult();
        f.Pump();
        peer.GetName().ShouldBe("Done");
        peer.IsEnabled().ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Zero_Reset_After_Stays_Done()
    {
        var f = Fixture.Create(setup: b => b.ResetAfter = TimeSpan.Zero);
        f.Click();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Success);
        f.Button.IsResetPending.ShouldBeFalse();
        f.Button.Reset();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Idle);
    }

    [AvaloniaFact]
    public void Status_Moved_On_From_Outside_Ignores_The_Running_Action()
    {
        var work = new TaskCompletionSource();
        var f = Fixture.Create(setup: b => b.Action = () => work.Task);
        f.Click();
        f.Button.Status = CelebrateButtonStatus.Idle;
        work.SetResult();
        f.Pump();
        f.Button.Status.ShouldBe(CelebrateButtonStatus.Idle);
    }

    [AvaloniaFact]
    public void Custom_Colors_Are_Used_Evenly()
    {
        var f = Fixture.Create(ArcMotionLevel.Full, b => b.ConfettiColors = [Colors.Red, Colors.Blue]);
        f.Button.Confetti.Random = new Random(3);
        f.Button.Celebrate();
        var pieces = f.Button.Confetti.Pieces.ToArray();
        pieces.Select(p => (int)p.Color).Distinct().OrderBy(c => c).ShouldBe([0, 1]);
    }

    [Fact]
    public void Pieces_Leave_From_The_Capsule_Outline_Outward()
    {
        var pieces = new ConfettiPiece[CelebrateConfettiMath.MaxLive];
        var live = CelebrateConfettiMath.Emit(pieces, 0, 44, 125, 40, 5, true, new Random(11));
        live.ShouldBe(44);
        var top = 0;
        for (var i = 0; i < live; i++)
        {
            var p = pieces[i];
            // Distance from the capsule's centre segment is the radius (20).
            var cx = Math.Clamp(p.X, 20, 105);
            Math.Sqrt(((p.X - cx) * (p.X - cx)) + ((p.Y - 20) * (p.Y - 20))).ShouldBe(20, 0.01);
            var speed = Math.Sqrt((p.Vx * p.Vx) + (p.Vy * p.Vy));
            speed.ShouldBeInRange(CelebrateConfettiMath.MinSpeed - 0.01, CelebrateConfettiMath.MaxSpeed + 0.01);
            // Outward: the velocity points away from the centre segment.
            (((p.X - cx) * p.Vx) + ((p.Y - 20) * p.Vy)).ShouldBeGreaterThan(0);
            if (p.Y < 0.5)
            {
                top++;
            }
            p.Life.ShouldBeInRange(CelebrateConfettiMath.MinLife, CelebrateConfettiMath.MaxLife);
        }
        top.ShouldBeGreaterThan(8);
    }

    [Fact]
    public void Pieces_Fall_Fade_And_Leave()
    {
        var pieces = new ConfettiPiece[CelebrateConfettiMath.MaxLive];
        var live = CelebrateConfettiMath.Emit(pieces, 0, 44, 125, 40, 5, true, new Random(5));
        CelebrateConfettiMath.Grow(pieces[0]).ShouldBe(0);
        live = CelebrateConfettiMath.Step(pieces, live, 0.05);
        CelebrateConfettiMath.Grow(pieces[0]).ShouldBe(0.5, 0.001);
        for (var i = 0; i < 60; i++)
        {
            live = CelebrateConfettiMath.Step(pieces, live, 1.0 / 60);
        }
        // About a second in, gravity has turned every piece downward and the oldest pieces are fading.
        for (var i = 0; i < live; i++)
        {
            pieces[i].Vy.ShouldBeGreaterThan(0);
        }
        for (var i = 0; i < 40; i++)
        {
            live = CelebrateConfettiMath.Step(pieces, live, 1.0 / 60);
        }
        live.ShouldBe(0);
    }

    [Fact]
    public void Kick_Peaks_At_1_07()
    {
        CelebrateConfettiMath.Kick(0).ShouldBe(1);
        CelebrateConfettiMath.Kick(0.13).ShouldBe(1.07, 1e-6);
        CelebrateConfettiMath.Kick(0.46).ShouldBe(1);
        CelebrateConfettiMath.Kick(0.3).ShouldBeInRange(1.0, 1.07);
    }
}
