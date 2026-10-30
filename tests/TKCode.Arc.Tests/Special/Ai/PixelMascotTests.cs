using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// PixelMascot: the fixed square size per scale, the resting pose of every status, the 40ms step clock and its
/// one-pixel-per-step morphs (lids before eye shapes, the bubble and keyboard stepping in and out, reversal mid-change),
/// the done jump and error shake curves, the built-in loop, pausing, reduced motion, labels and announcements.
/// </summary>
public class PixelMascotTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (PixelMascot Mascot, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<PixelMascot>? setup = null)
    {
        var mascot = new PixelMascot();
        setup?.Invoke(mascot);
        var window = TestHelpers.Show(mascot, level);
        Pump();
        return (mascot, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static string Ascii(PixelMascot mascot)
    {
        const string chars = ".#sh=EA_xe12";
        var sb = new StringBuilder();
        for (var y = 0; y < PixelMascotSprite.Size; y++)
        {
            for (var x = 0; x < PixelMascotSprite.Size; x++)
            {
                sb.Append(chars[mascot.PixelAt(x, y)]);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static int Count(PixelMascot mascot, byte index)
    {
        var n = 0;
        for (var y = 0; y < PixelMascotSprite.Size; y++)
        {
            for (var x = 0; x < PixelMascotSprite.Size; x++)
            {
                if (mascot.PixelAt(x, y) == index)
                {
                    n++;
                }
            }
        }
        return n;
    }

    [AvaloniaTheory]
    [InlineData(1, 24)]
    [InlineData(2, 48)]
    [InlineData(4, 96)]
    [InlineData(8, 192)]
    [InlineData(0, 24)]
    [InlineData(12, 192)]
    public void Renders_A_Fixed_Square_Of_24_Times_The_Scale(int scale, double side)
    {
        var (mascot, window) = Open(setup: m => m.Scale = scale);
        try
        {
            mascot.Bounds.Width.ShouldBe(side);
            mascot.Bounds.Height.ShouldBe(side);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Defaults_Match_The_Spec()
    {
        var mascot = new PixelMascot();
        mascot.Scale.ShouldBe(4);
        mascot.Status.ShouldBeNull();
        mascot.AssistantName.ShouldBe("Assistant");
        mascot.IsPaused.ShouldBeFalse();
        mascot.Announce.ShouldBeNull();
        mascot.ShownStatus.ShouldBe(PixelMascotStatus.Idle);
        mascot.Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Pixel_Edges_Land_On_Whole_Device_Pixels()
    {
        var (mascot, window) = Open(setup: m => m.Scale = 3);
        try
        {
            mascot.InvalidateVisual();
            Pump();
            var edges = mascot.EdgesX.ToArray();
            edges.Length.ShouldBe(25);
            for (var i = 0; i < edges.Length; i++)
            {
                edges[i].ShouldBe(i * 3.0, 1e-9);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Each_Status_Has_Its_Own_Resting_Shape()
    {
        var (mascot, window) = Open(setup: m => m.Status = PixelMascotStatus.Idle);
        try
        {
            var idle = Ascii(mascot);
            // Two open 2 × 2 eyes and the chest light.
            Count(mascot, PixelMascotSprite.Eye).ShouldBe(8);
            mascot.PixelAt(11, 18).ShouldBe(PixelMascotSprite.Accent);

            mascot.Status = PixelMascotStatus.Thinking;
            mascot.Pose.Bubble.ShouldBe(3);
            mascot.Pose.Dots.ShouldBe(3);
            mascot.PixelAt(17, 2).ShouldBe(PixelMascotSprite.Screen);
            mascot.PixelAt(21, 2).ShouldBe(PixelMascotSprite.Screen);

            mascot.Status = PixelMascotStatus.Writing;
            mascot.Pose.Keyboard.ShouldBe(PixelMascotPose.KeyboardIn);
            mascot.PixelAt(4, 20).ShouldBe(PixelMascotSprite.Outline);
            mascot.PixelAt(8, 18).ShouldBe(PixelMascotSprite.Accent);

            mascot.Status = PixelMascotStatus.Done;
            mascot.Pose.Eyes.ShouldBe(PixelMascotEyes.Happy);
            Count(mascot, PixelMascotSprite.Eye).ShouldBe(6);

            mascot.Status = PixelMascotStatus.Error;
            mascot.Pose.Eyes.ShouldBe(PixelMascotEyes.Crossed);
            // Two crossed eyes of five pixels and the 2 × 2 antenna light.
            Count(mascot, PixelMascotSprite.Danger).ShouldBe(10 + 4);
            Count(mascot, PixelMascotSprite.ScreenTintFull).ShouldBeGreaterThan(0);
            Count(mascot, PixelMascotSprite.Screen).ShouldBe(0);

            mascot.Status = PixelMascotStatus.Idle;
            Ascii(mascot).ShouldBe(idle);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Thinking_Cycles_Three_Dots_Every_1120ms()
    {
        var lit = Enumerable.Range(0, 28).Select(t => PixelMascotMotion.Target(PixelMascotStatus.Thinking, t).Dots).ToArray();
        lit[0].ShouldBe(1);
        lit[6].ShouldBe(1);
        lit[7].ShouldBe(2);
        lit[14].ShouldBe(3);
        lit[21].ShouldBe(0);
        PixelMascotMotion.Target(PixelMascotStatus.Thinking, 28).Dots.ShouldBe(1);
    }

    [Fact]
    public void Writing_Types_Eight_Strokes_Every_1600ms_While_The_Eyes_Scan()
    {
        var typed = Enumerable.Range(0, 40).Select(t => PixelMascotMotion.Target(PixelMascotStatus.Writing, t)).ToArray();
        typed[0].Typed.ShouldBe(0);
        typed[4].Typed.ShouldBe(1);
        typed[31].Typed.ShouldBe(7);
        typed[32].Typed.ShouldBe(8);
        typed[39].Typed.ShouldBe(8);
        PixelMascotMotion.Target(PixelMascotStatus.Writing, 40).Typed.ShouldBe(0);
        typed[0].EyeX.ShouldBe(-1);
        typed[16].EyeX.ShouldBe(0);
        typed[32].EyeX.ShouldBe(1);
        // Hands alternate.
        typed[0].TapLeft.ShouldBeTrue();
        typed[4].TapRight.ShouldBeTrue();
        typed[2].TapLeft.ShouldBeFalse();
    }

    [Fact]
    public void Idle_Bobs_On_A_2s_Cycle_And_Blinks_On_An_8s_Cycle()
    {
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 0).Y.ShouldBe(0);
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 25).Y.ShouldBe(-1);
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 50).Y.ShouldBe(0);
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 60).Lid.ShouldBe(2);
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 260).Lid.ShouldBe(2);
        PixelMascotMotion.Target(PixelMascotStatus.Idle, 110).EyeX.ShouldBe(-1);
    }

    [Fact]
    public void Done_Jumps_Fast_Holds_120ms_And_Settles_Without_A_Rebound()
    {
        var pose = PixelMascotPose.Rest;
        var heights = new List<int>();
        for (var t = 0; t < 14; t++)
        {
            PixelMascotMotion.Step(ref pose, PixelMascotMotion.Target(PixelMascotStatus.Done, t), writing: false);
            heights.Add(pose.Y);
        }
        heights.Take(6).ShouldBe([-2, -2, -2, -1, -1, 0]);
        heights.ShouldAllBe(y => y <= 0);
        heights.Skip(6).ShouldAllBe(y => y == 0);
    }

    [Fact]
    public void Eye_Shapes_Change_Behind_Closed_Lids()
    {
        var pose = PixelMascotPose.Rest;
        var target = PixelMascotMotion.Target(PixelMascotStatus.Error, 20);
        PixelMascotMotion.Step(ref pose, target, false);
        pose.Lid.ShouldBe(1);
        pose.Eyes.ShouldBe(PixelMascotEyes.Open);
        PixelMascotMotion.Step(ref pose, target, false);
        pose.Lid.ShouldBe(2);
        pose.Eyes.ShouldBe(PixelMascotEyes.Open);
        PixelMascotMotion.Step(ref pose, target, false);
        pose.Eyes.ShouldBe(PixelMascotEyes.Crossed);
        PixelMascotMotion.Step(ref pose, target, false);
        PixelMascotMotion.Step(ref pose, target, false);
        pose.Lid.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Status_Changes_Step_In_And_Reverse_From_The_Current_Frame()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = PixelMascotStatus.Idle);
        try
        {
            mascot.Loop.IsRunning.ShouldBeTrue();
            mascot.Status = PixelMascotStatus.Writing;
            // The keyboard steps in a row at a time, 40ms each.
            clock.Advance(TimeSpan.FromMilliseconds(81), Frame);
            mascot.Pose.Keyboard.ShouldBeInRange(2, 3);
            var reached = mascot.Pose.Keyboard;
            mascot.Status = PixelMascotStatus.Idle;
            clock.Advance(TimeSpan.FromMilliseconds(41), Frame);
            mascot.Pose.Keyboard.ShouldBe(reached - 1);
            clock.Advance(TimeSpan.FromMilliseconds(400), Frame);
            mascot.Pose.Keyboard.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Bubble_Steps_In_Before_Its_Dots_Light()
    {
        var (mascot, window) = Open(setup: m => m.Status = PixelMascotStatus.Idle);
        try
        {
            mascot.Status = PixelMascotStatus.Thinking;
            // Motion is off here, so drive the clock by hand from the idle pose.
            mascot.Status = PixelMascotStatus.Idle;
            mascot.ShownStatus.ShouldBe(PixelMascotStatus.Idle);
            mascot.Pose.Bubble.ShouldBe(0);
            var pose = PixelMascotPose.Rest;
            PixelMascotMotion.Step(ref pose, PixelMascotMotion.Target(PixelMascotStatus.Thinking, 0), false);
            pose.Bubble.ShouldBe(1);
            pose.Dots.ShouldBe(0);
            PixelMascotMotion.Step(ref pose, PixelMascotMotion.Target(PixelMascotStatus.Thinking, 1), false);
            pose.Bubble.ShouldBe(2);
            PixelMascotMotion.Step(ref pose, PixelMascotMotion.Target(PixelMascotStatus.Thinking, 2), false);
            pose.Bubble.ShouldBe(3);
            pose.Dots.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Error_Shakes_Once()
    {
        var pose = PixelMascotPose.Rest;
        var xs = new List<int>();
        for (var t = 0; t < 12; t++)
        {
            PixelMascotMotion.Step(ref pose, PixelMascotMotion.Target(PixelMascotStatus.Error, t), false);
            xs.Add(pose.X);
        }
        xs.ShouldContain(1);
        xs.ShouldContain(-1);
        xs.Skip(8).ShouldAllBe(x => x == 0);
    }

    [Fact]
    public void The_Built_In_Loop_Runs_Through_Every_State()
    {
        PixelMascotLoop.Steps.Select(s => s.Status).ShouldBe(
        [
            PixelMascotStatus.Idle, PixelMascotStatus.Thinking, PixelMascotStatus.Writing, PixelMascotStatus.Done,
            PixelMascotStatus.Idle, PixelMascotStatus.Thinking, PixelMascotStatus.Error
        ]);
        PixelMascotLoop.StatusAt(TimeSpan.Zero).ShouldBe(PixelMascotStatus.Idle);
        PixelMascotLoop.StatusAt(TimeSpan.FromSeconds(3.3)).ShouldBe(PixelMascotStatus.Thinking);
        PixelMascotLoop.StatusAt(TimeSpan.FromSeconds(7)).ShouldBe(PixelMascotStatus.Writing);
        PixelMascotLoop.StatusAt(PixelMascotLoop.CycleDuration - TimeSpan.FromMilliseconds(1)).ShouldBe(PixelMascotStatus.Error);
        PixelMascotLoop.StatusAt(PixelMascotLoop.CycleDuration).ShouldBe(PixelMascotStatus.Idle);
    }

    [AvaloniaFact]
    public void A_Shared_Loop_Steps_And_Holds()
    {
        var loop = new PixelMascotLoop();
        loop.Status.ShouldBe(PixelMascotStatus.Idle);
        loop.Advance();
        loop.Status.ShouldBe(PixelMascotStatus.Thinking);
        loop.StepIndex.ShouldBe(1);
        loop.IsEnabled = false;
        loop.Advance();
        loop.Status.ShouldBe(PixelMascotStatus.Writing);
        loop.Reset();
        loop.Status.ShouldBe(PixelMascotStatus.Idle);
    }

    [AvaloniaFact]
    public void An_Uncontrolled_Mascot_Plays_The_Loop_And_Raises_ShownStatusChanged()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full);
        try
        {
            var seen = new List<(PixelMascotStatus Old, PixelMascotStatus New)>();
            mascot.ShownStatusChanged += (_, e) => seen.Add((e.OldStatus, e.NewStatus));
            clock.Advance(TimeSpan.FromSeconds(3.3), Frame);
            mascot.ShownStatus.ShouldBe(PixelMascotStatus.Thinking);
            seen.ShouldBe([(PixelMascotStatus.Idle, PixelMascotStatus.Thinking)]);
            AutomationProperties.GetName(mascot).ShouldBe("Assistant is thinking");
            // The built-in loop is quiet by default.
            AutomationProperties.GetLiveSetting(mascot).ShouldBe(AutomationLiveSetting.Off);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Built_In_Loop_Keeps_Cycling_Under_Reduced_Motion_With_Resting_Poses()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            mascot.Loop.IsRunning.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(7), Frame);
            mascot.ShownStatus.ShouldBe(PixelMascotStatus.Writing);
            mascot.Pose.ShouldBe(PixelMascotMotion.Rest(PixelMascotStatus.Writing));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Controlled_Mascot_Stops_Its_Loop_Under_Reduced_Motion()
    {
        var (mascot, window) = Open(ArcMotionLevel.Reduced, m => m.Status = PixelMascotStatus.Thinking);
        try
        {
            mascot.Loop.IsRunning.ShouldBeFalse();
            mascot.Pose.ShouldBe(PixelMascotMotion.Rest(PixelMascotStatus.Thinking));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paused_Freezes_The_Current_Frame()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = PixelMascotStatus.Writing);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
            var tick = mascot.Tick;
            tick.ShouldBeGreaterThan(10);
            mascot.IsPaused = true;
            mascot.Loop.IsRunning.ShouldBeFalse();
            var frozen = mascot.Pose;
            clock.Advance(TimeSpan.FromMilliseconds(500), Frame);
            mascot.Tick.ShouldBe(tick);
            mascot.Pose.ShouldBe(frozen);
            mascot.IsPaused = false;
            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            mascot.Tick.ShouldBeGreaterThan(tick);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_Are_40ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = PixelMascotStatus.Idle);
        try
        {
            var start = mascot.Tick;
            clock.Advance(TimeSpan.FromMilliseconds(1000), Frame);
            (mascot.Tick - start).ShouldBeInRange(24, 26);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Follow_The_Status_And_Name()
    {
        var (mascot, window) = Open(setup: m =>
        {
            m.Status = PixelMascotStatus.Thinking;
            m.AssistantName = "Atlas";
        });
        try
        {
            AutomationProperties.GetName(mascot).ShouldBe("Atlas is thinking");
            AutomationProperties.GetLiveSetting(mascot).ShouldBe(AutomationLiveSetting.Polite);
            mascot.Status = PixelMascotStatus.Error;
            AutomationProperties.GetName(mascot).ShouldBe("Atlas ran into an error");
            mascot.Labels = new PixelMascotLabels { Error = "{0}: something went wrong" };
            AutomationProperties.GetName(mascot).ShouldBe("Atlas: something went wrong");
            mascot.Label = "Robot";
            AutomationProperties.GetName(mascot).ShouldBe("Robot");
            mascot.Announce = false;
            AutomationProperties.GetLiveSetting(mascot).ShouldBe(AutomationLiveSetting.Off);
            var peer = ControlAutomationPeer.CreatePeerForElement(mascot);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Image);
            peer.GetName().ShouldBe("Robot");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Labels_Describe_Every_Status()
    {
        var labels = PixelMascotLabels.Default;
        labels.Describe("Arc", PixelMascotStatus.Idle).ShouldBe("Arc is idle");
        labels.Describe("Arc", PixelMascotStatus.Thinking).ShouldBe("Arc is thinking");
        labels.Describe("Arc", PixelMascotStatus.Writing).ShouldBe("Arc is writing");
        labels.Describe("Arc", PixelMascotStatus.Done).ShouldBe("Arc is done");
        labels.Describe("Arc", PixelMascotStatus.Error).ShouldBe("Arc ran into an error");
    }

    [AvaloniaFact]
    public void The_Accent_Colors_The_Antenna_And_Lifts_The_Eyes()
    {
        var (mascot, window) = Open(setup: m =>
        {
            m.Status = PixelMascotStatus.Idle;
            m.AccentBrush = new SolidColorBrush(Color.Parse("#2F6BFF"));
        });
        try
        {
            mascot.PixelAt(11, 2).ShouldBe(PixelMascotSprite.Accent);
            mascot.Status = PixelMascotStatus.Error;
            mascot.PixelAt(11, 2).ShouldBe(PixelMascotSprite.Danger);
        }
        finally
        {
            window.Close();
        }
    }
}
