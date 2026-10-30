using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// BuildingBlocks: the schedule (three even phases, the sample's hold and fade, re-assembling app builds, the 2.4s
/// floor), the statuses an app sets (idle, building, a done that fast forwards), the width rules, Replay, the labels and
/// events, and the measured motion (0.52s critically damped flights 40ms apart, 320ms fills, 400ms styling, reduced
/// motion as crossfades that play once).
/// </summary>
public class BuildingBlocksTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (BuildingBlocks Blocks, Window Window) Open(BuildingBlocks blocks, ArcMotionLevel level = ArcMotionLevel.Off, double width = 800)
    {
        var window = TestHelpers.Show(blocks, level);
        window.Width = width;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return (blocks, window);
    }

    private static string? LabelOf(BuildingBlocks blocks) =>
        TestHelpers.Descendant<ArcContentSwap>(blocks, BuildingBlocks.LabelPart).Content as string;

    // ---- the schedule ----

    [Fact]
    public void A_Build_Splits_Its_Duration_Evenly_Over_Three_Phases()
    {
        const double d = 4.8;
        BuildingBlocksSchedule.StepAt(0, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Layout);
        BuildingBlocksSchedule.StepAt(1.59, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Layout);
        BuildingBlocksSchedule.StepAt(1.61, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Components);
        BuildingBlocksSchedule.StepAt(3.21, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Styling);
        BuildingBlocksSchedule.StepAt(4.8, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Ready);
        BuildingBlocksSchedule.StepAt(4.8 + BuildingBlocksSchedule.Hold, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Clearing);
        BuildingBlocksSchedule.StepAt(100, d, BuildingBlocksMode.Sample, false).ShouldBe(BuildingBlocksStep.Ready);
        BuildingBlocksSchedule.StepAt(-0.1, d, BuildingBlocksMode.Sample, true).ShouldBe(BuildingBlocksStep.Clearing);
    }

    [Fact]
    public void App_Builds_Reassemble_While_Looping_And_Hold_On_Styling_Otherwise()
    {
        BuildingBlocksSchedule.StepAt(4.9, 4.8, BuildingBlocksMode.Building, true).ShouldBe(BuildingBlocksStep.Clearing);
        BuildingBlocksSchedule.StepAt(60, 4.8, BuildingBlocksMode.Building, false).ShouldBe(BuildingBlocksStep.Styling);
        BuildingBlocksSchedule.StepAt(1, 4.8, BuildingBlocksMode.Idle, true).ShouldBe(BuildingBlocksStep.Empty);
        BuildingBlocksSchedule.StepAt(1, 4.8, BuildingBlocksMode.Done, true).ShouldBe(BuildingBlocksStep.Ready);
    }

    [Fact]
    public void The_Duration_Is_Never_Under_24_Seconds()
    {
        var schedule = new BuildingBlocksSchedule { Duration = 1 };
        schedule.Duration.ShouldBe(BuildingBlocksSchedule.MinimumDuration);
        schedule.Duration = double.NaN;
        schedule.Duration.ShouldBe(BuildingBlocksSchedule.DefaultDuration);
    }

    [Fact]
    public void The_Sample_Holds_Ready_Fades_Out_And_Builds_Again()
    {
        var schedule = new BuildingBlocksSchedule { Loops = true, Duration = 3 };
        schedule.SetMode(BuildingBlocksMode.Sample, blocksShown: false);
        var cycle = schedule.Cycle;
        schedule.Step.ShouldBe(BuildingBlocksStep.Layout);
        schedule.Advance(3.05);
        schedule.Step.ShouldBe(BuildingBlocksStep.Ready);
        schedule.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        schedule.Advance(BuildingBlocksSchedule.Hold);
        schedule.Step.ShouldBe(BuildingBlocksStep.Clearing);
        // A clearing layout keeps the phase it had.
        schedule.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        schedule.Advance(BuildingBlocksSchedule.Fade);
        schedule.Step.ShouldBe(BuildingBlocksStep.Layout);
        schedule.Cycle.ShouldBe(cycle + 1);
    }

    [Fact]
    public void A_Build_That_Does_Not_Loop_Stops_The_Clock_At_Its_End()
    {
        var schedule = new BuildingBlocksSchedule { Loops = false, Duration = 3 };
        schedule.SetMode(BuildingBlocksMode.Building, blocksShown: false);
        schedule.IsRunning.ShouldBeTrue();
        schedule.Advance(10);
        schedule.Step.ShouldBe(BuildingBlocksStep.Styling);
        schedule.IsRunning.ShouldBeFalse();
        schedule.Time.ShouldBe(3);
    }

    [Fact]
    public void A_Restart_Over_Visible_Blocks_Clears_Them_First()
    {
        var schedule = new BuildingBlocksSchedule();
        schedule.SetMode(BuildingBlocksMode.Building, blocksShown: true);
        schedule.Step.ShouldBe(BuildingBlocksStep.Clearing);
        schedule.Advance(BuildingBlocksSchedule.Fade + 0.01);
        schedule.Step.ShouldBe(BuildingBlocksStep.Layout);
    }

    // ---- the control ----

    [AvaloniaFact]
    public void With_No_Status_It_Plays_The_Simulated_Build()
    {
        var (blocks, _) = Open(new BuildingBlocks());
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Layout);
        LabelOf(blocks).ShouldBe("Laying out");
        blocks.Advance(1.7);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Components);
        LabelOf(blocks).ShouldBe("Adding components");
        blocks.Advance(1.6);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Styling);
        LabelOf(blocks).ShouldBe("Styling");
        blocks.Advance(1.6);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        LabelOf(blocks).ShouldBe("Ready");
        Avalonia.Automation.AutomationProperties.GetHelpText(blocks).ShouldBe("Simulated build, nothing was generated");
    }

    [AvaloniaFact]
    public void PhaseChanged_Reports_Every_Phase_With_The_One_Before()
    {
        var blocks = new BuildingBlocks();
        var seen = new List<(BuildingBlocksPhase Old, BuildingBlocksPhase New)>();
        blocks.PhaseChanged += (_, e) => seen.Add((e.OldPhase, e.Phase));
        Open(blocks);
        blocks.Advance(5);
        seen.ShouldBe([
            (BuildingBlocksPhase.Idle, BuildingBlocksPhase.Layout),
            (BuildingBlocksPhase.Layout, BuildingBlocksPhase.Ready),
        ]);
        blocks.Status = BuildingBlocksStatus.Building;
        blocks.Advance(BuildingBlocksSchedule.Fade + 0.01);
        blocks.Advance(1.7);
        seen[^2].ShouldBe((BuildingBlocksPhase.Ready, BuildingBlocksPhase.Layout));
        seen[^1].ShouldBe((BuildingBlocksPhase.Layout, BuildingBlocksPhase.Components));
    }

    [AvaloniaFact]
    public void Idle_Clears_The_Frame_To_Its_Slots()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Idle });
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Idle);
        LabelOf(blocks).ShouldBe("Waiting for a prompt");
        blocks.Stage!.HasVisibleBlocks.ShouldBeFalse();
        blocks.Mark!.Phase.ShouldBe(BuildingBlocksPhase.Idle);
        Avalonia.Automation.AutomationProperties.GetHelpText(blocks).ShouldBeNull();
    }

    [AvaloniaFact]
    public void Done_Mid_Build_Fast_Forwards_To_Ready()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building });
        blocks.Advance(0.5);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Layout);
        blocks.Stage!.ContentOf(0).ShouldBe(0);
        blocks.Status = BuildingBlocksStatus.Done;
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        for (var i = 0; i < BuildingBlocksStage.BlockCount; i++)
        {
            blocks.Stage.ShownOf(i).ShouldBe(1);
            blocks.Stage.ContentOf(i).ShouldBe(1);
            blocks.Stage.StyledOf(i).ShouldBe(1);
        }
        blocks.Stage.KnobOf().ShouldBe(1);
        blocks.Mark!.Phase.ShouldBe(BuildingBlocksPhase.Ready);
    }

    [AvaloniaFact]
    public void A_Building_Status_Without_Full_Motion_Plays_Once_And_Holds_On_Styling()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building });
        blocks.Schedule.Loops.ShouldBeFalse();
        blocks.Advance(30);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Styling);
        blocks.Schedule.IsRunning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Setting_Building_Again_Does_Not_Restart_A_Running_Build()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building });
        blocks.Advance(2);
        var t = blocks.Schedule.Time;
        blocks.SetCurrentValue(BuildingBlocks.StatusProperty, BuildingBlocksStatus.Building);
        blocks.Schedule.Time.ShouldBe(t);
    }

    [AvaloniaFact]
    public void Duration_Paces_The_Phases()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building, Duration = TimeSpan.FromSeconds(9) });
        blocks.Advance(2.9);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Layout);
        blocks.Advance(0.2);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Components);
    }

    [AvaloniaFact]
    public void The_Frame_Grows_To_Its_Size_And_Never_Past_The_Container()
    {
        var (medium, window) = Open(new BuildingBlocks());
        medium.Stage!.Bounds.Width.ShouldBe(400);
        medium.Stage.Bounds.Height.ShouldBe(280, 0.01);

        medium.Size = BuildingBlocksSize.Large;
        Dispatcher.UIThread.RunJobs();
        medium.Stage.Bounds.Width.ShouldBe(560);

        medium.Size = BuildingBlocksSize.Small;
        Dispatcher.UIThread.RunJobs();
        medium.Stage.Bounds.Width.ShouldBe(280);
        medium.Classes.Contains(BuildingBlocks.NarrowPseudoClass).ShouldBeTrue();

        medium.StageWidth = 480;
        Dispatcher.UIThread.RunJobs();
        medium.Stage.Bounds.Width.ShouldBe(480);
        medium.Classes.Contains(BuildingBlocks.NarrowPseudoClass).ShouldBeFalse();

        window.Width = 300;
        Dispatcher.UIThread.RunJobs();
        medium.Stage.Bounds.Width.ShouldBeLessThanOrEqualTo(300);
        medium.Classes.Contains(BuildingBlocks.NarrowPseudoClass).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Width_Does_Not_Follow_The_Label()
    {
        var (blocks, _) = Open(new BuildingBlocks { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left });
        var width = blocks.Bounds.Width;
        blocks.Labels = new BuildingBlocksLabels { Layout = "Laying out a very long and detailed customer relationship manager with many parts" };
        Dispatcher.UIThread.RunJobs();
        blocks.Bounds.Width.ShouldBe(width);
    }

    [AvaloniaFact]
    public void Replay_Shows_On_Ready_When_The_Sample_Plays_Once_And_Builds_Again()
    {
        var (blocks, window) = Open(new BuildingBlocks { IsLooping = false });
        blocks.ShowsReplay.ShouldBeFalse();
        blocks.Advance(5);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        blocks.ShowsReplay.ShouldBeTrue();
        TestHelpers.Descendant<PresenceHost>(blocks, BuildingBlocks.ReplayHostPart).IsPresent.ShouldBeTrue();
        var raised = 0;
        blocks.ReplayRequested += (_, _) => raised++;
        var replay = TestHelpers.Descendant<AvaloniaButton>(blocks, BuildingBlocks.ReplayPart);
        replay.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        raised.ShouldBe(1);
        // The sample builds again (the old layout clears first).
        blocks.Advance(BuildingBlocksSchedule.Fade + 0.01);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Layout);
        blocks.ShowsReplay.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void AllowsReplay_Shows_Replay_On_An_App_Driven_Ready()
    {
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Done });
        blocks.ShowsReplay.ShouldBeFalse();
        blocks.AllowsReplay = true;
        Dispatcher.UIThread.RunJobs();
        blocks.ShowsReplay.ShouldBeTrue();
        TestHelpers.Descendant<PresenceHost>(blocks, BuildingBlocks.ReplayHostPart).IsPresent.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(blocks, BuildingBlocks.ReplayTextPart).Text.ShouldBe("Replay");
        // An app-driven Ready is the app's to change: Replay() leaves it alone.
        blocks.Replay();
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Ready);
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var (blocks, _) = Open(new BuildingBlocks
        {
            Status = BuildingBlocksStatus.Done,
            AllowsReplay = true,
            Labels = new BuildingBlocksLabels { Ready = "Your CRM is ready", Replay = "Build again" },
        });
        LabelOf(blocks).ShouldBe("Your CRM is ready");
        blocks.DisplayedLabel.ShouldBe("Your CRM is ready");
        TestHelpers.Descendant<TextBlock>(blocks, BuildingBlocks.ReplayTextPart).Text.ShouldBe("Build again");
        var label = TestHelpers.Descendant<ArcContentSwap>(blocks, BuildingBlocks.LabelPart);
        Avalonia.Automation.AutomationProperties.GetName(label).ShouldBe("Your CRM is ready");
        Avalonia.Automation.AutomationProperties.GetLiveSetting(label).ShouldBe(Avalonia.Automation.AutomationLiveSetting.Polite);
    }

    [AvaloniaFact]
    public void IsLabelVisible_Hides_The_Status_Line()
    {
        var (blocks, _) = Open(new BuildingBlocks { IsLabelVisible = false });
        TestHelpers.Descendant<Grid>(blocks, "PART_Status").IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_Frame_Takes_No_Focus_And_Is_Hidden_From_Assistive_Technology()
    {
        var (blocks, _) = Open(new BuildingBlocks());
        blocks.Focusable.ShouldBeFalse();
        blocks.Stage!.Focusable.ShouldBeFalse();
        Avalonia.Automation.AutomationProperties.GetAccessibilityView(blocks.Stage)
            .ShouldBe(Avalonia.Automation.AccessibilityView.Raw);
    }

    [AvaloniaFact]
    public void Tone_And_Tint_Pick_The_Blocks_Color()
    {
        var (blocks, _) = Open(new BuildingBlocks { Tone = BuildingBlocksTone.Neutral });
        var foreground = TestHelpers.ColorOf(blocks.Stage!.ForegroundInk);
        blocks.Stage.TintColor.ShouldBe(foreground);
        blocks.Tint = Color.Parse("#16A34A");
        blocks.Stage.TintColor.ShouldBe(Color.Parse("#16A34A"));
        BuildingBlocksPalette.OnTint(Color.Parse("#16A34A"), BuildingBlocksTone.Accent, null, null).ShouldBe(Colors.Black);
        BuildingBlocksPalette.OnTint(Color.Parse("#1E3A8A"), BuildingBlocksTone.Accent, null, null).ShouldBe(Colors.White);
    }

    // ---- motion ----

    [AvaloniaFact]
    public void Blocks_Fly_In_40ms_Apart_And_Land_Without_Overshoot()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building }, ArcMotionLevel.Full);
        var stage = blocks.Stage!;
        stage.FlightOf(0).ShouldBe(0);
        blocks.Advance(0.1);
        stage.FlightOf(0).ShouldBeGreaterThan(0);
        // The last block leaves 200ms after the first one.
        stage.FlightOf(5).ShouldBe(0);
        var last = 0.0;
        for (var t = 0; t < 60; t++)
        {
            blocks.Advance(1.0 / 60);
            var flight = stage.FlightOf(5);
            flight.ShouldBeGreaterThanOrEqualTo(last);
            flight.ShouldBeLessThanOrEqualTo(1);
            last = flight;
        }
        // 0.52s spring after a 0.2s delay: landed within about a second of the first block.
        for (var i = 0; i < BuildingBlocksStage.BlockCount; i++)
        {
            stage.FlightOf(i).ShouldBe(1, 0.01);
        }
        stage.ContentOf(0).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Placeholders_Fill_Then_Take_The_Styling_Pass()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building, IsLooping = false }, ArcMotionLevel.Full);
        var stage = blocks.Stage!;
        blocks.Advance(1.6);
        blocks.Advance(0.01);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Components);
        blocks.Advance(BuildingBlocksStage.ContentDuration / 2);
        stage.ContentOf(0).ShouldBeInRange(0.05, 0.99);
        // Block 5 waits 5 × 60ms.
        stage.ContentOf(5).ShouldBe(0);
        blocks.Advance(1);
        stage.ContentOf(5).ShouldBe(1);
        stage.DrawnOf(2).ShouldBe(1, 0.01);
        stage.StyledOf(0).ShouldBe(0);
        blocks.Advance(0.5);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Styling);
        // The switch (block 4) starts its slide 4 × 50ms into the pass.
        blocks.Advance(4 * BuildingBlocksStage.StyleStagger + BuildingBlocksStage.StyleDuration / 2);
        stage.KnobOf().ShouldBeInRange(0.05, 0.99);
        blocks.Advance(1);
        stage.StyledOf(5).ShouldBe(1);
        stage.KnobOf().ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Clock_Runs_On_Frames_And_Pauses_When_Hidden()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building }, ArcMotionLevel.Full);
        clock.Advance(TimeSpan.FromSeconds(1.8), Frame);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Components);
        blocks.Mark!.IsLightRunning.ShouldBeTrue();
        var t = blocks.Schedule.Time;
        blocks.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(3), Frame);
        blocks.Schedule.Time.ShouldBe(t, 0.05);
        blocks.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromSeconds(1.6), Frame);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Styling);
    }

    [AvaloniaFact]
    public void A_Looping_App_Build_Reassembles_With_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (blocks, _) = Open(new BuildingBlocks { Status = BuildingBlocksStatus.Building, Duration = TimeSpan.FromSeconds(3) }, ArcMotionLevel.Full);
        blocks.Schedule.Loops.ShouldBeTrue();
        var cycle = blocks.Schedule.Cycle;
        blocks.Advance(2.5);
        blocks.Advance(0.6);
        blocks.Schedule.Step.ShouldBe(BuildingBlocksStep.Clearing);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Styling);
        blocks.Advance(BuildingBlocksSchedule.Fade);
        blocks.Schedule.Cycle.ShouldBe(cycle + 1);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Layout);
        // The new build flies in again from out of sight.
        blocks.Stage!.ContentOf(0).ShouldBe(0);
    }

    [AvaloniaFact]
    public void Reduced_Motion_Crossfades_Without_Travel_And_Plays_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (blocks, _) = Open(new BuildingBlocks(), ArcMotionLevel.Reduced);
        blocks.Schedule.Loops.ShouldBeFalse();
        var stage = blocks.Stage!;
        blocks.Advance(0.12);
        stage.FlightOf(5).ShouldBe(1);
        stage.ShownOf(5).ShouldBeInRange(0.05, 0.99);
        blocks.Advance(10);
        blocks.Phase.ShouldBe(BuildingBlocksPhase.Ready);
        blocks.Advance(BuildingBlocksStage.ReducedDuration);
        stage.KnobOf().ShouldBe(1);
        blocks.Mark!.IsLightRunning.ShouldBeFalse();
        blocks.Mark.BlockOpacity(0).ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Mark_Fills_One_Block_Per_Phase_Without_Its_Light()
    {
        var mark = new BuildingBlocksMark();
        mark.Show(BuildingBlocksPhase.Layout, ArcMotionLevel.Reduced);
        mark.BlockOpacity(0).ShouldBe(1);
        mark.BlockOpacity(1).ShouldBe(BuildingBlocksMark.RestOpacity);
        mark.Show(BuildingBlocksPhase.Styling, ArcMotionLevel.Reduced);
        mark.BlockOpacity(2).ShouldBe(1);
        mark.BlockOpacity(3).ShouldBe(BuildingBlocksMark.RestOpacity);
        mark.Show(BuildingBlocksPhase.Styling, ArcMotionLevel.Full);
        mark.BlockOpacity(0).ShouldBe(1);
        mark.Advance(BuildingBlocksMark.BeatSeconds);
        mark.BlockOpacity(1).ShouldBe(1, 1e-9);
        mark.BlockOpacity(3).ShouldBe(BuildingBlocksMark.RestOpacity, 1e-9);
    }
}
