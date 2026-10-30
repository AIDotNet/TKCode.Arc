using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AiMascot: the size and compact drawing, the accessible label and announcements, the self-playing tour and its event,
/// status blends that start from the frame on screen, reduced motion's still poses, the tones and colors, the pointer and
/// the pure pose math (the seamless writing cycle, the decaying shake, blinks and the orbit's layer switch).
/// </summary>
public class AiMascotTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (AiMascot Mascot, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Off, Action<AiMascot>? setup = null)
    {
        var mascot = new AiMascot();
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

    private static void Run(ManualFrameClock clock, double milliseconds) => clock.Advance(TimeSpan.FromMilliseconds(milliseconds), Frame);

    [AvaloniaFact]
    public void Defaults_To_A_96px_Square_Playing_The_Tour()
    {
        var (mascot, window) = Open();
        try
        {
            mascot.Size.ShouldBe(96);
            mascot.Status.ShouldBeNull();
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Idle);
            mascot.Tone.ShouldBe(AiMascotTone.Accent);
            mascot.FollowPointer.ShouldBe(AiMascotPointerFollow.Hover);
            mascot.ShowCompanion.ShouldBeTrue();
            mascot.ShowGround.ShouldBeTrue();
            mascot.Bounds.Width.ShouldBe(96);
            mascot.Bounds.Height.ShouldBe(96);
            var canvas = mascot.CanvasPartControl!;
            canvas.IsCompact.ShouldBeFalse();
            mascot.Classes.Contains(AiMascot.CompactPseudoClass).ShouldBeFalse();
            mascot.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Below_56px_The_Compact_Drawing_Takes_Over()
    {
        var (mascot, window) = Open(setup: m => m.Size = 24);
        try
        {
            mascot.Bounds.Width.ShouldBe(24);
            mascot.CanvasPartControl!.IsCompact.ShouldBeTrue();
            mascot.Classes.Contains(AiMascot.CompactPseudoClass).ShouldBeTrue();
            mascot.Size = 56;
            Pump();
            mascot.CanvasPartControl!.IsCompact.ShouldBeFalse();
            mascot.Bounds.Width.ShouldBe(56);
            mascot.Classes.Contains(AiMascot.CompactPseudoClass).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Never_Follows_Its_Container_Size()
    {
        var mascot = new AiMascot { Size = 40, Status = AiMascotStatus.Idle };
        var host = new Border { Width = 300, Height = 200, Child = mascot };
        var window = TestHelpers.Show(host);
        try
        {
            Pump();
            mascot.Bounds.Width.ShouldBe(40);
            mascot.Bounds.Height.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Label_Reads_Name_And_Status()
    {
        var (mascot, window) = Open(setup: m => m.Status = AiMascotStatus.Thinking);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            mascot.AccessibleLabel.ShouldBe("Assistant, thinking");
            AutomationProperties.GetName(canvas).ShouldBe("Assistant, thinking");
            AutomationProperties.GetControlTypeOverride(canvas).ShouldBe(Avalonia.Automation.Peers.AutomationControlType.Image);
            mascot.AssistantName = "Atlas";
            AutomationProperties.GetName(canvas).ShouldBe("Atlas, thinking");
            mascot.Status = AiMascotStatus.Writing;
            mascot.AccessibleLabel.ShouldBe("Atlas, writing");
            mascot.Label = "Atlas is replying";
            AutomationProperties.GetName(canvas).ShouldBe("Atlas is replying");
            mascot.Label = null;
            mascot.Labels = new AiMascotLabels { Writing = "schreibt", Label = (name, status) => $"{name} {status}" };
            mascot.AccessibleLabel.ShouldBe("Atlas schreibt");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Announces_Status_Changes_Only_When_Asked()
    {
        var (mascot, window) = Open(setup: m => m.Status = AiMascotStatus.Idle);
        try
        {
            var announcer = mascot.AnnouncerPartControl!;
            mascot.Status = AiMascotStatus.Listening;
            announcer.History.ShouldBeEmpty();
            mascot.Announce = true;
            mascot.AssistantName = "Atlas";
            mascot.Status = AiMascotStatus.Thinking;
            mascot.Status = AiMascotStatus.Thinking;
            mascot.Status = AiMascotStatus.Done;
            announcer.History.ShouldBe(["Atlas, thinking", "Atlas, done"]);
            AutomationProperties.GetLiveSetting(announcer).ShouldBe(AutomationLiveSetting.Polite);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tour_Plays_Every_State_And_Reports_Each_Move()
    {
        var (mascot, window) = Open();
        try
        {
            // Hold the drawing's clock so only these calls move the tour.
            mascot.IsPaused = true;
            mascot.RestartTour();
            var moves = new List<(AiMascotStatus Status, AiMascotStatus Previous)>();
            mascot.StatusChanged += (_, e) => moves.Add((e.Status, e.Previous));
            mascot.AdvanceTour(3.3);
            moves.ShouldBeEmpty();
            mascot.AdvanceTour(0.2);
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Listening);
            moves.ShouldBe([(AiMascotStatus.Listening, AiMascotStatus.Idle)]);
            mascot.AdvanceTour(2.9 + 3.6 + 5.0 + 2.6 + 2.4);
            moves.Select(m => m.Status).ShouldBe([AiMascotStatus.Listening, AiMascotStatus.Thinking, AiMascotStatus.Writing,
                AiMascotStatus.Done, AiMascotStatus.Error, AiMascotStatus.Idle]);
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Idle);
            mascot.CanvasPartControl!.WeightOf(AiMascotStatus.Idle).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Set_Status_Stops_The_Tour_And_Raises_No_Event()
    {
        var (mascot, window) = Open();
        try
        {
            mascot.IsPaused = true;
            var raised = 0;
            mascot.StatusChanged += (_, _) => raised++;
            mascot.Status = AiMascotStatus.Writing;
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Writing);
            mascot.AdvanceTour(30);
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Writing);
            raised.ShouldBe(0);
            // Clearing it starts the tour over.
            mascot.Status = null;
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Idle);
            mascot.AdvanceTour(3.4);
            raised.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tour_Runs_On_The_Drawing_Clock_At_Every_Motion_Level()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            mascot.CanvasPartControl!.IsLooping.ShouldBeTrue();
            Run(clock, 3500);
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Listening);
            // Reduced motion: the drawing clock holds, only the tour keeps time.
            mascot.CanvasPartControl!.Now.ShouldBe(0);
            mascot.IsPaused = true;
            Run(clock, 5000);
            mascot.DisplayedStatus.ShouldBe(AiMascotStatus.Listening);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Status_Change_Blends_From_The_Frame_On_Screen()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = AiMascotStatus.Thinking);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            canvas.WeightOf(AiMascotStatus.Thinking).ShouldBe(1);
            Run(clock, 600);
            canvas.LiveMix.ShouldBe(1);
            mascot.Status = AiMascotStatus.Done;
            Run(clock, 120);
            var done = canvas.WeightOf(AiMascotStatus.Done);
            done.ShouldBeInRange(0.05, 0.95);
            canvas.WeightOf(AiMascotStatus.Thinking).ShouldBeInRange(0.05, 0.95);
            var before = canvas.CurrentPose;
            // A change in the middle of a blend starts from the frame on screen: nothing jumps.
            mascot.Status = AiMascotStatus.Error;
            canvas.WeightOf(AiMascotStatus.Done).ShouldBe(done, 1e-9);
            var after = canvas.CurrentPose;
            after.DotX.ShouldBe(before.DotX, 1e-6);
            after.Squint.ShouldBe(before.Squint, 1e-6);
            // The blend keeps its velocity: done's weight is still rising on the next frame before it turns around.
            Run(clock, 17);
            canvas.WeightOf(AiMascotStatus.Done).ShouldBeGreaterThan(done);
            Run(clock, 1200);
            canvas.WeightOf(AiMascotStatus.Error).ShouldBe(1, 0.01);
            canvas.WeightOf(AiMascotStatus.Done).ShouldBe(0, 0.01);
            canvas.CurrentPose.Brows.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_First_Paint_Is_The_Still_Pose_And_The_Loop_Eases_Out_Of_It()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = AiMascotStatus.Thinking);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            canvas.LiveMix.ShouldBe(0, 0.05);
            var still = canvas.CurrentPose;
            still.DotX.ShouldBe(50, 1e-6);
            Run(clock, 200);
            canvas.LiveMix.ShouldBeInRange(0.3, 0.7);
            Run(clock, 300);
            canvas.LiveMix.ShouldBe(1);
            canvas.IsLooping.ShouldBeTrue();
            mascot.IsPaused = true;
            canvas.IsLooping.ShouldBeFalse();
            var t = canvas.Now;
            Run(clock, 500);
            canvas.Now.ShouldBe(t);
            mascot.IsPaused = false;
            Run(clock, 100);
            canvas.Now.ShouldBeGreaterThan(t);
            // Resuming does not ease in again: the clock keeps its phase.
            canvas.LiveMix.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Switches_In_Place_To_Still_Poses()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Reduced, m => m.Status = AiMascotStatus.Idle);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            // A set status under reduced motion asks for no frames: the loop is decoration.
            canvas.IsLooping.ShouldBeFalse();
            mascot.Status = AiMascotStatus.Thinking;
            canvas.WeightOf(AiMascotStatus.Thinking).ShouldBe(1);
            var thinking = canvas.CurrentPose;
            // Eyes up with the dot over the head.
            thinking.DotX.ShouldBe(50, 1e-6);
            thinking.DotY.ShouldBeLessThan(25);
            thinking.GazeY.ShouldBe(-0.95, 1e-6);
            mascot.Status = AiMascotStatus.Writing;
            var writing = canvas.CurrentPose;
            // Half a written line.
            writing.LineFrom.ShouldBe(AiMascotPose.LineLeft);
            writing.LineTo.ShouldBe((AiMascotPose.LineLeft + AiMascotPose.LineRight) / 2);
            mascot.Status = AiMascotStatus.Done;
            canvas.CurrentPose.Squint.ShouldBe(1);
            canvas.CurrentPose.Sparkle.ShouldBe(1);
            mascot.Status = AiMascotStatus.Error;
            var error = canvas.CurrentPose;
            error.Brows.ShouldBe(1);
            // No shake.
            error.BodyX.ShouldBe(0);
            Run(clock, 100);
            canvas.CurrentPose.BodyX.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tones_And_Colors_Pick_Body_Eyes_And_Dot()
    {
        var (mascot, window) = Open(setup: m => m.Status = AiMascotStatus.Idle);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            var accent = ArcTokens.GetColor(canvas, ArcDesignTokenKind.Accent);
            var accentForeground = ArcTokens.GetColor(canvas, ArcDesignTokenKind.AccentForeground);
            var foreground = ArcTokens.GetColor(canvas, ArcDesignTokenKind.Foreground);
            var background = ArcTokens.GetColor(canvas, ArcDesignTokenKind.Background);
            canvas.Colors.ShouldBe((accent, accentForeground, accent));
            mascot.Tone = AiMascotTone.Ink;
            canvas.Colors.ShouldBe((foreground, background, foreground));
            mascot.Tone = AiMascotTone.Soft;
            canvas.Colors.Body.ShouldBe(ArcTokens.GetColor(canvas, ArcDesignTokenKind.SurfaceMuted));
            canvas.Colors.Eye.ShouldBe(foreground);
            var purple = Color.Parse("#7747FF");
            mascot.BodyColor = purple;
            canvas.Colors.Body.ShouldBe(purple);
            canvas.Colors.Dot.ShouldBe(purple);
            // The eyes become a dark mix of the body.
            var eye = canvas.Colors.Eye;
            (eye.R + eye.G + eye.B).ShouldBeLessThan(purple.R + purple.G + purple.B);
            mascot.EyeColor = Colors.White;
            mascot.DotColor = Colors.Orange;
            canvas.Colors.ShouldBe((purple, Colors.White, Colors.Orange));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Eyes_Follow_The_Pointer_Except_While_Thinking()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (mascot, window) = Open(ArcMotionLevel.Full, m => m.Status = AiMascotStatus.Idle);
        try
        {
            var canvas = mascot.CanvasPartControl!;
            Run(clock, 500);
            canvas.SetPointer(new Point(-200, canvas.Bounds.Height * 0.55));
            Run(clock, 600);
            canvas.Gaze.X.ShouldBeLessThan(-0.6);
            mascot.Status = AiMascotStatus.Thinking;
            Run(clock, 1500);
            // Thinking looks up at the orbit whatever the pointer does.
            canvas.Gaze.Y.ShouldBeLessThan(-0.6);
            mascot.FollowPointer = AiMascotPointerFollow.Never;
            mascot.Status = AiMascotStatus.Idle;
            Run(clock, 800);
            // Back to its own glances, which never reach as far as the pointer did.
            canvas.CurrentPose.GazeX.ShouldBeGreaterThan(-0.96);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Writing_Cycle_Has_No_Seam()
    {
        var penY = AiMascotFrame.Regular.PenY;
        var start = AiMascotPose.Pen(0, penY);
        (start.To - start.From).ShouldBe(0, 1e-9);
        var end = AiMascotPose.Pen(AiMascotPose.WriteCycle - 1e-9, penY);
        (end.To - end.From).ShouldBe(0, 1e-6);
        // Mid-write the line runs from the left edge to the pen.
        var mid = AiMascotPose.Pen(AiMascotPose.WriteCycle * AiMascotPose.WriteShare * 0.5, penY);
        mid.From.ShouldBe(AiMascotPose.LineLeft);
        mid.To.ShouldBe(mid.PenX, 1e-9);
        mid.PenX.ShouldBe((AiMascotPose.LineLeft + AiMascotPose.LineRight) / 2, 1e-9);
        // The pen lifts on the way back.
        var back = AiMascotPose.Pen(AiMascotPose.WriteCycle * (AiMascotPose.WriteShare + (1 - AiMascotPose.WriteShare) / 2), penY);
        back.PenY.ShouldBeLessThan(penY - 5);
        // The pen ends where it starts.
        end.PenX.ShouldBe(start.PenX, 1e-4);
    }

    [Fact]
    public void The_Error_Shake_Plays_Once_And_Dies_Down_In_About_07s()
    {
        AiMascotPose.Shake(0).ShouldBe(0);
        Math.Abs(AiMascotPose.Shake(0.045)).ShouldBeGreaterThan(2);
        Math.Abs(AiMascotPose.Shake(0.7)).ShouldBeLessThan(0.1);
        AiMascotPose.Shake(2).ShouldBe(0);
    }

    [Fact]
    public void A_Blink_Takes_160ms()
    {
        AiMascotCanvas.BlinkOpen(1.0, 1.0, double.NegativeInfinity).ShouldBe(1, 1e-9);
        AiMascotCanvas.BlinkOpen(1.08, 1.0, double.NegativeInfinity).ShouldBe(0.06, 1e-9);
        AiMascotCanvas.BlinkOpen(1.17, 1.0, double.NegativeInfinity).ShouldBe(1);
        // A double blink closes the eyes again.
        AiMascotCanvas.BlinkOpen(1.34, 1.0, 1.26).ShouldBe(0.06, 1e-9);
    }

    [Fact]
    public void The_Orbit_Switches_Layers_Only_At_Its_Ends()
    {
        var frame = AiMascotFrame.Regular;
        for (var i = 0; i < 520; i++)
        {
            var t = i * AiMascotPose.OrbitPeriod / 520;
            var pose = AiMascotPose.Of(AiMascotStatus.Thinking, t, t, true, 0, 0, frame);
            var nextT = (i + 1) * AiMascotPose.OrbitPeriod / 520;
            var next = AiMascotPose.Of(AiMascotStatus.Thinking, nextT, nextT, true, 0, 0, frame);
            if (pose.DotBack != next.DotBack)
            {
                // Clear of the body: at the far left or right of the orbit.
                Math.Abs(pose.DotX - AiMascotPose.AnchorX).ShouldBeGreaterThan(AiMascotPose.OrbitRadiusX - 0.5);
            }
            if (pose.DotBack == 0 && Math.Abs(pose.DotX - AiMascotPose.AnchorX) < 5)
            {
                // Across the crown in front it takes the eye color.
                pose.DotInk.ShouldBe(1);
            }
        }
    }

    [Fact]
    public void Each_State_Has_Its_Own_Face()
    {
        var frame = AiMascotFrame.Regular;
        AiMascotPose Still(AiMascotStatus s) => AiMascotPose.Of(s, 0, 0, false, 0, 0, frame);
        Still(AiMascotStatus.Listening).EyeOpen.ShouldBeGreaterThan(1.1);
        Still(AiMascotStatus.Thinking).GazeY.ShouldBeLessThan(-0.5);
        Still(AiMascotStatus.Writing).GazeY.ShouldBeGreaterThan(0.5);
        Still(AiMascotStatus.Done).Squint.ShouldBe(1);
        Still(AiMascotStatus.Error).Brows.ShouldBe(1);
        Still(AiMascotStatus.Idle).Squint.ShouldBe(0);
        Still(AiMascotStatus.Idle).Brows.ShouldBe(0);
    }
}
