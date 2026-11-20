using Avalonia;
using Avalonia.Automation;
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

/// <summary>ArmingSwitch: the lift, arm, hold and run ritual, its events, keyboard, accessibility, layout and motion.</summary>
public class ArmingSwitchTests
{
    private sealed class Fixture
    {
        public ArmingSwitch Switch = null!;
        public Window Window = null!;
        public readonly List<ArmingSwitchStatusChangedEventArgs> Changes = new();
        public int CoverChanges;
        public int ArmedChanges;
        public int Confirmed;

        public static Fixture Create(double width = 840, ArcMotionLevel level = ArcMotionLevel.Off, Action<ArmingSwitch>? setup = null)
        {
            var f = new Fixture();
            f.Switch = new ArmingSwitch { Width = width, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            setup?.Invoke(f.Switch);
            f.Switch.StatusChanged += (_, e) => f.Changes.Add(e);
            f.Switch.CoverOpenChanged += (_, _) => f.CoverChanges++;
            f.Switch.ArmedChanged += (_, _) => f.ArmedChanges++;
            f.Switch.Confirmed += (_, _) => f.Confirmed++;
            f.Window = TestHelpers.Show(f.Switch, level);
            f.Window.Width = width + 40;
            f.Window.Height = 900;
            f.Pump();
            f.Stage.IsManualClock = true;
            return f;
        }

        public ArmingSwitchStage Stage => Switch.Stage!;

        public void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        public void Advance(double ms) => Stage.AdvanceFrames(ms);

        public Point Centre(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), Window)!.Value;

        public void PressLamp()
        {
            var p = Centre(Stage.LampHit);
            Window.MouseMove(p, RawInputModifiers.None);
            Window.MouseDown(p, MouseButton.Left);
        }

        public void ReleaseLamp() => Window.MouseUp(Centre(Stage.LampHit), MouseButton.Left);

        public void Click(Control c)
        {
            var p = Centre(c);
            Window.MouseMove(p, RawInputModifiers.None);
            Window.MouseDown(p, MouseButton.Left);
            Window.MouseUp(p, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        public void Key(PhysicalKey key)
        {
            Window.KeyPressQwerty(key, RawInputModifiers.None);
            Window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        public void Armed()
        {
            Switch.OpenCover();
            Switch.Arm();
            Advance(500);
        }
    }

    [AvaloniaFact]
    public void Starts_Safe_With_Its_Own_Defaults()
    {
        var f = Fixture.Create();
        var s = f.Switch;
        s.Status.ShouldBe(ArmingSwitchStatus.Safe);
        s.IsCoverOpen.ShouldBeFalse();
        s.IsArmed.ShouldBeFalse();
        s.HoldDuration.ShouldBe(TimeSpan.FromMilliseconds(1200));
        s.IsAutoSafeEnabled.ShouldBeTrue();
        s.ArmLabel.ShouldBe("Arm");
        s.SafeLabel.ShouldBe("Safe");
        s.CurrentHint.ShouldBe(s.SafeHint);
        f.Stage.CoverAngle.ShouldBe(0);
        f.Stage.BatAngle.ShouldBe(ArmingSwitchStage.BatThrow);
    }

    [AvaloniaFact]
    public void Hold_Duration_Is_Kept_Between_300_Ms_And_6_S()
    {
        var s = new ArmingSwitch { HoldDuration = TimeSpan.FromMilliseconds(50) };
        s.HoldDuration.ShouldBe(TimeSpan.FromMilliseconds(300));
        s.HoldDuration = TimeSpan.FromSeconds(20);
        s.HoldDuration.ShouldBe(TimeSpan.FromSeconds(6));
    }

    [AvaloniaFact]
    public void Lifting_The_Cover_Opens_It_And_Announces_The_Next_Step()
    {
        var f = Fixture.Create();
        f.Switch.OpenCover();
        f.Switch.IsCoverOpen.ShouldBeTrue();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Open);
        f.CoverChanges.ShouldBe(1);
        f.Stage.CoverAngle.ShouldBe(ArmingSwitchStage.CoverOpenAngle);
        f.Switch.Announcer.Announcement.ShouldBe(f.Switch.OpenStatusText + ". " + f.Switch.OpenHint);
        f.Changes.Single().NewStatus.ShouldBe(ArmingSwitchStatus.Open);
    }

    [AvaloniaFact]
    public void The_Switch_Refuses_To_Arm_Under_A_Closed_Cover()
    {
        var f = Fixture.Create();
        f.Stage.Activate(ArmingSwitchPart.Switch);
        f.Switch.IsArmed.ShouldBeFalse();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Safe);
        f.Switch.CurrentHint.ShouldBe(ArmingSwitch.CoverFirstText);
        f.Switch.Announcer.Announcement.ShouldBe(ArmingSwitch.CoverFirstText);
        f.ArmedChanges.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Arming_Lights_The_Lamp_And_Closing_The_Cover_Makes_It_Safe()
    {
        var f = Fixture.Create();
        f.Armed();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Armed);
        f.ArmedChanges.ShouldBe(1);
        f.Stage.BatAngle.ShouldBe(-ArmingSwitchStage.BatThrow);
        f.Stage.LampLevel.ShouldBeGreaterThan(0.5);

        f.Switch.CloseCover();
        f.Switch.IsCoverOpen.ShouldBeFalse();
        f.Switch.IsArmed.ShouldBeFalse();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Safe);
        f.ArmedChanges.ShouldBe(2);
        f.Advance(300);
        f.Stage.BatAngle.ShouldBe(ArmingSwitchStage.BatThrow);
        f.Stage.LampLevel.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_Lamp_Refuses_Until_The_Switch_Is_Armed()
    {
        var f = Fixture.Create();
        f.PressLamp();
        f.Stage.IsHolding.ShouldBeFalse();
        f.Switch.CurrentHint.ShouldBe(ArmingSwitch.CoverFirstText);
        f.ReleaseLamp();
        f.Switch.OpenCover();
        f.PressLamp();
        f.Stage.IsHolding.ShouldBeFalse();
        f.Switch.CurrentHint.ShouldBe(ArmingSwitch.ArmFirstText);
        f.ReleaseLamp();
    }

    [AvaloniaFact]
    public void Holding_The_Lamp_For_The_Hold_Duration_Runs_The_Action()
    {
        var runs = 0;
        var f = Fixture.Create(setup: s => s.ConfirmAction = () =>
        {
            runs++;
            return Task.FromResult(true);
        });
        f.Armed();
        f.PressLamp();
        f.Stage.IsHolding.ShouldBeTrue();
        f.Advance(1100);
        f.Confirmed.ShouldBe(0);
        f.Stage.HoldProgress.ShouldBe(1100.0 / 1200, 0.03);
        f.Advance(150);
        f.Confirmed.ShouldBe(1);
        runs.ShouldBe(1);
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Done);
        f.ReleaseLamp();
    }

    [AvaloniaFact]
    public void Letting_Go_Early_Drains_The_Ring_And_Pressing_Again_Picks_Up()
    {
        var f = Fixture.Create();
        f.Armed();
        f.PressLamp();
        f.Advance(600);
        f.ReleaseLamp();
        var released = f.Stage.HoldProgress;
        released.ShouldBe(0.5, 0.03);
        f.Advance(100);
        var drained = f.Stage.HoldProgress;
        drained.ShouldBeLessThan(released);
        drained.ShouldBeGreaterThan(0);
        f.PressLamp();
        f.Advance(16);
        f.Stage.HoldProgress.ShouldBeGreaterThanOrEqualTo(drained);
        f.ReleaseLamp();
        f.Advance(2000);
        f.Stage.HoldProgress.ShouldBe(0);
        f.Confirmed.ShouldBe(0);
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Armed);
    }

    [AvaloniaFact]
    public void A_Pending_Action_Shows_Running_Then_Done()
    {
        var pending = new TaskCompletionSource<bool>();
        var f = Fixture.Create(setup: s => s.ConfirmAction = () => pending.Task);
        f.Armed();
        f.PressLamp();
        f.Advance(1300);
        f.ReleaseLamp();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Running);
        f.Switch.CurrentHint.ShouldBe(f.Switch.RunningHint);
        // The cover and the switch ignore input while the action runs; Escape too.
        f.Switch.CloseCover();
        f.Switch.MakeSafe();
        f.Switch.IsCoverOpen.ShouldBeTrue();
        pending.SetResult(true);
        f.Pump();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Done);
        f.Changes.Select(c => c.NewStatus).ShouldBe(new[]
        {
            ArmingSwitchStatus.Open, ArmingSwitchStatus.Armed, ArmingSwitchStatus.Running, ArmingSwitchStatus.Done
        });
    }

    [AvaloniaFact]
    public void Done_Stays_After_The_Cover_Closes_Until_It_Is_Lifted_Again()
    {
        var f = Fixture.Create(setup: s => s.ConfirmAction = () => Task.FromResult(true));
        f.Armed();
        f.PressLamp();
        f.Advance(1300);
        f.ReleaseLamp();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Done);
        f.Switch.CloseCover();
        f.Switch.IsArmed.ShouldBeFalse();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Done);
        f.Switch.OpenCover();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Open);
    }

    [AvaloniaFact]
    public void Without_Auto_Safe_The_Action_Can_Run_Again()
    {
        var runs = 0;
        var f = Fixture.Create(setup: s =>
        {
            s.IsAutoSafeEnabled = false;
            s.ConfirmAction = () =>
            {
                runs++;
                return Task.FromResult(true);
            };
        });
        f.Armed();
        f.PressLamp();
        f.Advance(1300);
        f.ReleaseLamp();
        f.Advance(200);
        f.PressLamp();
        f.Advance(1300);
        f.ReleaseLamp();
        runs.ShouldBe(2);
        f.Switch.IsCoverOpen.ShouldBeTrue();
        f.Switch.IsArmed.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void False_Or_An_Exception_Fails_And_Keeps_The_Switch_Armed()
    {
        var f = Fixture.Create(setup: s => s.ConfirmAction = () => Task.FromResult(false));
        f.Armed();
        f.PressLamp();
        f.Advance(1300);
        f.ReleaseLamp();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Failed);
        f.Switch.IsArmed.ShouldBeTrue();
        f.Changes.Last().Error.ShouldBeNull();

        var boom = new InvalidOperationException("offline");
        var g = Fixture.Create(setup: s => s.ConfirmAction = () => Task.FromException<bool>(boom));
        g.Armed();
        g.PressLamp();
        g.Advance(1300);
        g.ReleaseLamp();
        g.Switch.Status.ShouldBe(ArmingSwitchStatus.Failed);
        g.Changes.Last().Error.ShouldBe(boom);
        g.Switch.CurrentHint.ShouldBe(g.Switch.FailedHint);

        // Throwing the switch down clears the failure.
        g.Stage.Activate(ArmingSwitchPart.Switch);
        g.Switch.Status.ShouldBe(ArmingSwitchStatus.Open);
    }

    [AvaloniaFact]
    public void Keyboard_Tabs_Cover_Switch_Lamp_And_Escape_Makes_Everything_Safe()
    {
        var f = Fixture.Create();
        f.Stage.CoverHit.Focus(NavigationMethod.Tab);
        f.Key(PhysicalKey.Enter);
        f.Switch.IsCoverOpen.ShouldBeTrue();
        f.Key(PhysicalKey.Tab);
        f.Stage.SwitchHit.IsFocused.ShouldBeTrue();
        f.Key(PhysicalKey.ArrowUp);
        f.Switch.IsArmed.ShouldBeTrue();
        f.Key(PhysicalKey.Tab);
        f.Stage.LampHit.IsFocused.ShouldBeTrue();
        f.Window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Stage.IsHolding.ShouldBeTrue();
        f.Advance(300);
        f.Window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        f.Stage.IsHolding.ShouldBeFalse();
        f.Key(PhysicalKey.Escape);
        f.Switch.IsCoverOpen.ShouldBeFalse();
        f.Switch.IsArmed.ShouldBeFalse();
        f.Switch.Status.ShouldBe(ArmingSwitchStatus.Safe);
    }

    [AvaloniaFact]
    public void Arrow_Keys_Open_And_Close_The_Cover()
    {
        var f = Fixture.Create();
        f.Stage.CoverHit.Focus(NavigationMethod.Tab);
        f.Key(PhysicalKey.ArrowUp);
        f.Switch.IsCoverOpen.ShouldBeTrue();
        f.Key(PhysicalKey.ArrowDown);
        f.Switch.IsCoverOpen.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Clicking_The_Cover_And_The_Switch_Toggles_Them()
    {
        var f = Fixture.Create();
        f.Click(f.Stage.CoverHit);
        f.Switch.IsCoverOpen.ShouldBeTrue();
        f.Click(f.Stage.SwitchHit);
        f.Switch.IsArmed.ShouldBeTrue();
        f.Click(f.Stage.SwitchHit);
        f.Switch.IsArmed.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Locked_Switch_Refuses_Everything_And_Says_So()
    {
        var f = Fixture.Create(setup: s => s.IsLocked = true);
        f.Switch.OpenCover();
        f.Switch.IsCoverOpen.ShouldBeFalse();
        f.Switch.CurrentHint.ShouldBe(ArmingSwitch.LockedText);
        f.PressLamp();
        f.Stage.IsHolding.ShouldBeFalse();
        f.ReleaseLamp();
        f.Stage.CoverHit.Focusable.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Parts_Are_Named_For_Assistive_Technology()
    {
        var f = Fixture.Create(setup: s => s.Title = "Delete the archive");
        var peer = ControlAutomationPeer.CreatePeerForElement(f.Switch);
        peer.GetName().ShouldBe("Delete the archive");
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);

        ControlAutomationPeer.CreatePeerForElement(f.Stage.CoverHit).GetName().ShouldBe(ArmingSwitch.LiftCoverName);
        var sw = ControlAutomationPeer.CreatePeerForElement(f.Stage.SwitchHit);
        sw.GetName().ShouldBe("Arm switch");
        ControlAutomationPeer.CreatePeerForElement(f.Stage.LampHit).GetName().ShouldBe(f.Switch.HoldLabel);
        var toggle = sw.GetProvider<IToggleProvider>();
        toggle.ShouldNotBeNull();
        toggle!.ToggleState.ShouldBe(ToggleState.Off);

        f.Switch.OpenCover();
        AutomationProperties.GetName(f.Stage.CoverHit).ShouldBe(ArmingSwitch.CloseCoverName);
        AutomationProperties.GetHelpText(f.Stage.CoverHit).ShouldBe(f.Switch.OpenHint);
        toggle.Toggle();
        f.Switch.IsArmed.ShouldBeTrue();
        toggle.ToggleState.ShouldBe(ToggleState.On);
    }

    [AvaloniaFact]
    public void Wide_Cards_Sit_Beside_The_Plate_And_Narrow_Ones_Stack()
    {
        var wide = Fixture.Create(840);
        wide.Switch.Classes.Contains(":narrow").ShouldBeFalse();
        var stage = wide.Stage;
        stage.PlateWidth.ShouldBeLessThanOrEqualTo(560);
        stage.Bounds.X.ShouldBeGreaterThan(200);

        var narrow = Fixture.Create(552);
        narrow.Switch.Classes.Contains(":narrow").ShouldBeTrue();
        narrow.Switch.Classes.Contains(":tight").ShouldBeFalse();
        narrow.Stage.Bounds.X.ShouldBeLessThan(1);

        var phone = Fixture.Create(342);
        phone.Switch.Classes.Contains(":tight").ShouldBeTrue();
        phone.Stage.PlateWidth.ShouldBeGreaterThan(280);
    }

    [AvaloniaFact]
    public void Full_Motion_Swings_The_Cover_Open_On_Its_Hinge()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Full);
        f.Switch.OpenCover();
        f.Advance(80);
        f.Stage.CoverAngle.ShouldBeInRange(5, 80);
        f.Advance(500);
        f.Stage.CoverAngle.ShouldBe(ArmingSwitchStage.CoverOpenAngle, 0.1);
        f.Switch.Arm();
        f.Advance(30);
        f.Stage.BatAngle.ShouldBeInRange(-ArmingSwitchStage.BatThrow, ArmingSwitchStage.BatThrow - 1);
        f.Advance(400);
        f.Stage.BatAngle.ShouldBe(-ArmingSwitchStage.BatThrow, 0.1);
        f.Stage.LampLevel.ShouldBe(0.92, 0.02);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Jumps_The_Cover_But_Still_Fills_The_Ring()
    {
        var f = Fixture.Create(level: ArcMotionLevel.Reduced);
        f.Switch.OpenCover();
        f.Stage.CoverAngle.ShouldBe(ArmingSwitchStage.CoverOpenAngle);
        f.Switch.Arm();
        f.Stage.BatAngle.ShouldBe(-ArmingSwitchStage.BatThrow);
        f.PressLamp();
        f.Advance(600);
        f.Stage.HoldProgress.ShouldBe(0.5, 0.02);
        f.Advance(700);
        f.Confirmed.ShouldBe(1);
        f.ReleaseLamp();
    }
}
