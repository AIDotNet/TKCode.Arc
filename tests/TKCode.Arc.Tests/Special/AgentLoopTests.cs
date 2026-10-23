using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The agent loop card: the sample story, stepping, the end states, data, layout, controls and announcements.</summary>
public class AgentLoopTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (AgentLoop Loop, Window Window) Open(ManualFrameClock clock, AgentLoop? loop = null,
        ArcMotionLevel level = ArcMotionLevel.Full, double width = 800, double height = 760)
    {
        loop ??= new AgentLoop { AutoPlay = false };
        var window = TestHelpers.Show(loop, level);
        window.Width = width;
        window.Height = height;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        Dispatcher.UIThread.RunJobs();
        return (loop, window);
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Dispatcher.UIThread.RunJobs();
    }

    private static AgentLoopStage Stage(AgentLoop loop) => TestHelpers.Descendant<AgentLoopStage>(loop);

    private static AgentLoopIteration Loop(string label, int tokens, bool passed, params AgentLoopCall[] calls)
        => new(new AgentLoopContext(label, "detail", tokens), calls, new AgentLoopCheck(passed, passed ? "Looks right" : "Not yet"));

    private static AgentLoopCall Call(string tool, string action, AgentLoopCallStatus status = AgentLoopCallStatus.Success)
        => new(tool, action, "result of " + action, status);

    [AvaloniaFact]
    public void Defaults_Play_The_Invented_Sample_Run()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.Title.ShouldBe("Agent loop");
            loop.Model!.Name.ShouldBe(AgentLoopDefaults.ModelName);
            loop.Run.Count.ShouldBe(3);
            loop.Run.Select(i => i.Calls.Count).ShouldBe([1, 2, 2]);
            loop.Timeline.End.ShouldBe(17.67, 0.0005);
            loop.Phase.ShouldBe(AgentLoopPhase.Ready);
            loop.CaptionText.ShouldBe(AgentLoopDefaults.Task);
            Stage(loop).Center.LineKey.ShouldBe("text:Ready");
            Stage(loop).TokensText.ShouldBe("0 tokens");
            AutomationProperties.GetName(loop).ShouldBe("Agent loop");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Playing_Gathers_Context_And_Counts_The_Tokens()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.Play();
            Run(clock, 0.3);
            loop.Phase.ShouldBe(AgentLoopPhase.Running);
            loop.Scene.Loop.ShouldBe(0);
            loop.Scene[AgentLoopStation.Gather].ShouldBe(AgentLoopNodeState.Active);
            Stage(loop).Labels[0].Classes.ShouldContain(":active");
            Stage(loop).Center.LineKey.ShouldStartWith("counter:");
            Run(clock, 0.7);
            loop.Scene.Blocks.ShouldBe(1);
            Stage(loop).TokensText.ShouldBe("1.4K tokens");
            Stage(loop).ContextList.Blocks.Single().Context.Label.ShouldBe("Request from Lena");
            loop.Scene[AgentLoopStation.Gather].ShouldBe(AgentLoopNodeState.Active);
            Run(clock, 0.2);
            loop.Scene[AgentLoopStation.Gather].ShouldBe(AgentLoopNodeState.Visited);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Head_Travels_With_A_Trail_And_Rests_Without_One()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.Play();
            Run(clock, 0.6);
            var rest = Stage(loop).Ring.HeadPose;
            rest.Head.ShouldBe(0);
            rest.Tail.ShouldBe(0);
            // Halfway from Gather to Take action (1.07 + 0.35): the head is near 60° and the tail about 72° behind it.
            Run(clock, 1.42 - loop.StoryTime);
            var p = (loop.StoryTime - 1.07) / 0.7;
            var sweep = Stage(loop).Geometry!.LowerAngle;
            var travel = Stage(loop).Ring.HeadPose;
            travel.Head.ShouldBe(sweep * AgentLoopTimeline.EaseInOutCubic(p), 0.01);
            (travel.Head - travel.Tail).ShouldBe(72 * Math.Sin(Math.PI * p), 0.01);
            (travel.Head - travel.Tail).ShouldBeGreaterThan(65);
            travel.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Calls_Drop_Into_Their_Lane_And_Resolve()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.Play();
            Run(clock, 2.0);
            var (checks, calls) = Stage(loop).Lanes;
            calls.Current.ShouldNotBeNull();
            calls.Current!.State.Call!.Action.ShouldBe("Open ticket #418");
            calls.Current.State.IsResolved.ShouldBeFalse();
            calls.Current.Look.ShouldBe(AgentLoopChipLook.Active);
            checks.Current.ShouldBeNull();
            Stage(loop).Ring.PinOf(AgentLoopStation.Act).Has.ShouldBeTrue();
            Run(clock, 0.7);
            calls.Current!.State.IsResolved.ShouldBeTrue();
            Run(clock, 1.2);
            checks.Current.ShouldNotBeNull();
            checks.Current!.State.Check!.Passed.ShouldBeFalse();
            calls.Current!.Look.ShouldBe(AgentLoopChipLook.Dim);
            // Loop 2's first call fails, the second replaces it in the same lane.
            Run(clock, 7.8 - loop.StoryTime);
            calls.Current!.State.Call!.Status.ShouldBe(AgentLoopCallStatus.Error);
            calls.Current.State.IsResolved.ShouldBeTrue();
            Run(clock, 9.0 - loop.StoryTime);
            calls.Current!.State.Call!.Action.ShouldBe("Check last 7 days");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Run_Ends_Done_With_The_Ring_Drawn_In()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            var finished = 0;
            var stations = new List<(AgentLoopStation, int)>();
            loop.Finished += (_, _) => finished++;
            loop.StationChanged += (_, e) => stations.Add((e.Station, e.Loop));
            loop.Play();
            Run(clock, 18.6);
            finished.ShouldBe(1);
            loop.IsFinished.ShouldBeTrue();
            loop.IsPlaying.ShouldBeFalse();
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
            loop.CaptionText.ShouldBe(AgentLoopDefaults.Summary);
            Stage(loop).Center.LineKey.ShouldBe("done:Done in 3 loops");
            Stage(loop).TokensText.ShouldBe("15.8K tokens");
            var done = Stage(loop).Ring.DoneLine;
            done.Progress.ShouldBe(1, 0.001);
            done.Opacity.ShouldBe(1, 0.001);
            Stage(loop).Ring.HeadPose.Opacity.ShouldBe(0, 0.001);
            stations.Count.ShouldBe(9);
            stations[0].ShouldBe((AgentLoopStation.Gather, 0));
            stations[1].ShouldBe((AgentLoopStation.Act, 0));
            stations[2].ShouldBe((AgentLoopStation.Verify, 0));
            stations[^1].ShouldBe((AgentLoopStation.Verify, 2));
            var (checks, calls) = Stage(loop).Lanes;
            checks.Current!.Look.ShouldBe(AgentLoopChipLook.Rest);
            calls.Current!.Look.ShouldBe(AgentLoopChipLook.Rest);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Step_Plays_At_Two_And_A_Half_Times_To_The_Next_Beat()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.StepForward();
            loop.Phase.ShouldBe(AgentLoopPhase.Running);
            // 0.72 s of story at 2.5× takes about 0.29 s.
            Run(clock, 0.25);
            loop.IsStepping.ShouldBeTrue();
            Run(clock, 0.1);
            loop.IsStepping.ShouldBeFalse();
            loop.StoryTime.ShouldBe(0.72, 0.0005);
            loop.Scene.Blocks.ShouldBe(1);
            loop.Phase.ShouldBe(AgentLoopPhase.Paused);
            Stage(loop).Center.LineKey.ShouldStartWith("counter:");
            loop.StepForward();
            Run(clock, 1.5);
            loop.StoryTime.ShouldBe(2.87, 0.0005);
            loop.Scene.CallChip!.IsResolved.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stepping_And_Pausing_Speak_The_Beat_And_The_End_Speaks_The_Summary()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            var announcer = TestHelpers.Descendant<ExplainerAnnouncer>(loop);
            loop.Play();
            Run(clock, 2.0);
            announcer.History.ShouldBeEmpty();
            loop.Pause();
            Run(clock, 0.05);
            announcer.Announcement.ShouldBe("Calling Tracker: Open ticket #418");
            loop.StepForward();
            Run(clock, 1);
            announcer.Announcement.ShouldBe("Tracker returned: Rows missing since Mar 3");
            loop.StepForward();
            Run(clock, 1);
            announcer.Announcement.ShouldBe("Check failed: Cause not found yet. Going round again");
            loop.Play();
            Run(clock, 16);
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
            announcer.Announcement.ShouldBe(AgentLoopDefaults.Summary);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Last_Check_Ends_Stopped_With_A_Neutral_Ring()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var loop = new AgentLoop { AutoPlay = false };
        loop.Iterations.Add(Loop("Build log", 2900, true, Call("CI", "Read pipeline run")));
        loop.Iterations.Add(Loop("Pipeline file", 1700, false, Call("CI", "Clear cache and rerun"), Call("Chat", "Post update")));
        var (_, window) = Open(clock, loop);
        try
        {
            loop.Run.Count.ShouldBe(2);
            loop.CaptionText.ShouldBe(string.Empty);
            loop.JumpToEnd();
            Run(clock, 1);
            loop.Phase.ShouldBe(AgentLoopPhase.Stopped);
            loop.Classes.ShouldContain(AgentLoop.StoppedPseudoClass);
            loop.CaptionText.ShouldBe("Stopped after 2 loops and 3 tool calls: the last check did not pass.");
            Stage(loop).Center.LineKey.ShouldBe("stopped:Stopped after 2 loops");
            Stage(loop).Ring.DoneLine.Opacity.ShouldBe(0);
            Stage(loop).TokensText.ShouldBe("4.6K tokens");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Passed_Run_Without_A_Summary_Counts_Its_Loops_And_Calls()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var loop = new AgentLoop { AutoPlay = false, Task = "Fix the nightly build." };
        loop.Iterations.Add(Loop("Build log", 993, true, Call("CI", "Read run")));
        var (_, window) = Open(clock, loop);
        try
        {
            loop.CaptionText.ShouldBe("Fix the nightly build.");
            loop.JumpToEnd();
            Run(clock, 1);
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
            loop.CaptionText.ShouldBe("Finished in 1 loop with 1 tool call.");
            Stage(loop).Center.LineKey.ShouldBe("done:Done in 1 loop");
            Stage(loop).TokensText.ShouldBe("993 tokens");
            loop.Summary = "All green.";
            loop.CaptionText.ShouldBe("All green.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Run_Rests_With_The_Controls_Off()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var loop = new AgentLoop { AutoPlay = false, IterationsSource = Array.Empty<AgentLoopIteration>() };
        var (_, window) = Open(clock, loop);
        try
        {
            loop.Run.Count.ShouldBe(0);
            loop.Classes.ShouldContain(AgentLoop.EmptyPseudoClass);
            loop.IsPlaying = true;
            loop.IsPlaying.ShouldBeFalse();
            loop.Phase.ShouldBe(AgentLoopPhase.Ready);
            TestHelpers.Descendant<AgentLoopFooterPanel>(loop).IsEnabled.ShouldBeFalse();
            TestHelpers.Descendant<Button>(loop, "PART_PlayButton").IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Iterations_Before_The_Start_Replace_The_Sample_Run()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.Run.Count.ShouldBe(3);
            loop.IterationsSource = new[] { Loop("Ticket", 1200, true, Call("Tracker", "Read ticket")) };
            Dispatcher.UIThread.RunJobs();
            loop.Run.Count.ShouldBe(1);
            loop.Timeline.End.ShouldBe(0.22 + 3.7 + 1.35, 0.0005);
            loop.Phase.ShouldBe(AgentLoopPhase.Ready);
            loop.Play();
            Run(clock, 5.5);
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_On_The_End_And_Steps_In_Place()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var loop = new AgentLoop();
        var (_, window) = Open(clock, loop, ArcMotionLevel.Reduced, height: 1200);
        try
        {
            Run(clock, 0.3);
            loop.IsFinished.ShouldBeTrue();
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
            Stage(loop).Ring.DoneLine.Progress.ShouldBe(1);
            Stage(loop).ContextList.Blocks.Count().ShouldBe(3);
            TestHelpers.Descendant<ExplainerAnnouncer>(loop).Announcement.ShouldBe(AgentLoopDefaults.Summary);
            loop.Restart();
            loop.Pause();
            loop.StepForward();
            loop.StoryTime.ShouldBe(0.72, 0.0005);
            loop.Scene.Blocks.ShouldBe(1);
            // The head waits at its station during a trip and jumps when it ends.
            loop.Play();
            Run(clock, 1.42 - loop.StoryTime);
            var pose = Stage(loop).Ring.HeadPose;
            pose.Head.ShouldBe(0);
            pose.Tail.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Speed_Control_Is_Two_Way()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            var speed = TestHelpers.Descendant<SegmentedControl>(loop, AgentLoop.SpeedPart);
            speed.SelectedIndex.ShouldBe(1);
            AutomationProperties.GetName(speed).ShouldBe("Speed");
            loop.Speed = 2;
            speed.SelectedIndex.ShouldBe(2);
            speed.SelectedIndex = 0;
            loop.Speed.ShouldBe(0.5);
            loop.Speed = 1.5;
            speed.SelectedIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Card_Lays_Out_Wide_Or_Stacked_By_Its_Own_Width()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock, width: 800);
        try
        {
            var stage = Stage(loop);
            stage.Arrangement.ShouldBe(AgentLoopArrangement.Wide);
            // 744 card, 24 padding, 1px border: 694 inside, 406 for the ring (the headless font is wide, so the radius is
            // whatever the measured labels leave).
            stage.Geometry!.Width.ShouldBe(406, 1);
            stage.Geometry.IsCompact.ShouldBeFalse();
            stage.Geometry.Radius.ShouldBe(AgentLoopGeometry.Solve(stage.Geometry.Width, stage.Labels[1].NaturalWidth, stage.Labels[2].NaturalWidth).Radius);
            stage.Lanes.Calls.IsVisible.ShouldBeTrue();
            loop.Bounds.Width.ShouldBe(744, 0.5);

            window.Width = 380;
            Dispatcher.UIThread.RunJobs();
            Run(clock, 0.05);
            stage.Arrangement.ShouldBe(AgentLoopArrangement.StackedOneColumn);
            stage.Geometry!.IsCompact.ShouldBeTrue();
            stage.Lanes.Calls.IsVisible.ShouldBeFalse();
            stage.Labels[1].IsCompact.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(loop, AgentLoop.SpeedLabelPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Single_Lane_Shows_The_Latest_Chip()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock, width: 380, height: 1100);
        try
        {
            loop.Play();
            Run(clock, 2.0);
            var lane = Stage(loop).Lanes.Checks;
            lane.Current!.State.Station.ShouldBe(AgentLoopStation.Act);
            Stage(loop).Ring.PinStation.ShouldBe(AgentLoopStation.Act);
            Run(clock, 1.9);
            lane.Current!.State.Station.ShouldBe(AgentLoopStation.Verify);
            Stage(loop).Ring.PinStation.ShouldBe(AgentLoopStation.Verify);
            Run(clock, 15);
            Stage(loop).Center.LineKey.ShouldBe("done:3 loops");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stepping_To_The_End_Moves_Focus_To_Restart()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            var step = TestHelpers.Descendant<Button>(loop, Explainer.StepButtonPart);
            var restart = TestHelpers.Descendant<Button>(loop, Explainer.RestartButtonPart);
            step.Focus(NavigationMethod.Tab);
            for (var i = 0; i < 12; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Run(clock, 1.5);
            }
            loop.Phase.ShouldBe(AgentLoopPhase.Done);
            step.IsEnabled.ShouldBeFalse();
            restart.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Restart_Clears_The_Scene_And_Plays_Again()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.JumpToEnd();
            Run(clock, 1);
            loop.Restart();
            loop.IsPlaying.ShouldBeTrue();
            loop.Phase.ShouldBe(AgentLoopPhase.Running);
            loop.Scene.Blocks.ShouldBe(0);
            loop.CaptionText.ShouldBe(AgentLoopDefaults.Task);
            Run(clock, 0.1);
            Stage(loop).Center.LineKey.ShouldBe("text:Ready");
            Run(clock, 0.6);
            Stage(loop).Ring.DoneLine.Opacity.ShouldBe(0, 0.001);
            Stage(loop).ContextList.Blocks.Count().ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Rename_The_Stations_And_The_Lanes()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var loop = new AgentLoop
        {
            AutoPlay = false,
            Labels = new AgentLoopLabels { Gather = "Read", Act = "Do", Verify = "Check", ChecksPlaceholder = "No checks yet" }
        };
        var (_, window) = Open(clock, loop);
        try
        {
            var stage = Stage(loop);
            stage.Labels[0].StationName.ShouldBe("Read");
            stage.Labels[1].StationName.ShouldBe("Do");
            stage.Labels[2].StationName.ShouldBe("Check");
            stage.Lanes.Checks.Placeholder.ShouldBe("No checks yet");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Steady_Run_Does_Not_Grow_Its_Visual_Tree()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var (loop, window) = Open(clock);
        try
        {
            loop.JumpToEnd();
            Run(clock, 1);
            var count = loop.GetVisualDescendants().Count();
            for (var i = 0; i < 3; i++)
            {
                loop.Restart();
                Run(clock, 19);
            }
            loop.GetVisualDescendants().Count().ShouldBeLessThanOrEqualTo(count + 4);
        }
        finally
        {
            window.Close();
        }
    }
}
