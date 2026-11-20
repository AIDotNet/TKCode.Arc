using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Illustrations;

/// <summary>The model training illustration: the run's schedule, the five steps, picking a step, the loop, hover, layout and the kept API.</summary>
public class ModelTrainingTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(ManualFrameClock clock, Control card, double width = 904)
    {
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        window.Width = width + 40;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        clock.Advance(TimeSpan.FromMilliseconds(50), Frame);
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Run(ManualFrameClock clock, double seconds)
    {
        clock.Advance(TimeSpan.FromSeconds(seconds), Frame);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Timeline_Tells_The_Default_Run_In_About_21_Seconds()
    {
        var timeline = new ModelTrainingTimeline();
        timeline.Build(6, 12, 0.69, 0.08, null, 94.2, 90, 0.05);
        timeline.Length.ShouldBe(21.46, 0.05);
        timeline.StageStart(1).ShouldBe(3.2);
        timeline.StageStart(2).ShouldBe(6.2);
        timeline.StageStart(3).ShouldBe(9.2);
        timeline.EvaluateStart.ShouldBe(16.36, 0.02);
        timeline.StageAt(0.1).ShouldBe(0);
        timeline.StageAt(7).ShouldBe(2);
        timeline.StageAt(20).ShouldBe(4);
        timeline.LossAt(0).ShouldBe(0.69, 1e-9);
        timeline.LossAt(1).ShouldBe(0.08, 1e-9);
        timeline.LossAt(0.5).ShouldBeLessThan(0.3);
        timeline.ValidationLoss.ShouldBe(0.11, 0.005);
        timeline.ChartMax.ShouldBeGreaterThan(0.69);
        // Before the first forward pass lands the readouts are untrained; the evaluation counts up to the accuracy.
        timeline.IsMeasured(4).ShouldBeFalse();
        timeline.EpochAt(4).ShouldBe(0);
        timeline.WinnerProbability(4).ShouldBe(0.5);
        timeline.EpochAt(timeline.Length).ShouldBe(12);
        timeline.ScoreAt(timeline.Length).ShouldBe(94.2, 1e-9);
        timeline.EvalStateAt(timeline.Length).ShouldBe(2);
        timeline.WinnerProbability(timeline.Length).ShouldBe(0.97, 0.005);
        // The first 0.35 s still show the finished run (the loop is seamless), then the untrained model.
        timeline.EvalStateAt(0.1).ShouldBe(2);
        timeline.EvalStateAt(0.5).ShouldBe(0);
        timeline.UpdatesAt(0, 0).ShouldBe(6, 1e-9);
        timeline.UpdatesAt(0, 1).ShouldBe(0);
        timeline.LearnedShare(6).ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void More_Steps_Stretch_The_Epochs_Step()
    {
        var timeline = new ModelTrainingTimeline();
        timeline.Build(10, 30, 1.2, 0.2, null, 80, 85, 0.05);
        timeline.EpochsLength.ShouldBe(8 * ModelTrainingTimeline.CyclePeriod + ModelTrainingTimeline.CycleLength, 1e-9);
        timeline.PassesTarget.ShouldBeFalse();
        timeline.Passes.Count(p => p.Kind == ModelTrainingPassKind.Backward).ShouldBe(30);
    }

    [AvaloniaFact]
    public void Stepping_Walks_The_Five_Steps()
    {
        var card = new ModelTraining { AutoPlay = false };
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.Restart();
            card.Pause();
            card.CurrentStep.ShouldBe(0);
            var phases = new List<ModelTrainingPhase>();
            var steps = new List<int>();
            for (var i = 0; i < 5; i++)
            {
                card.StepForward();
                phases.Add(card.Phase);
                steps.Add(card.CurrentStep);
            }
            phases.ShouldBe([ModelTrainingPhase.Forward, ModelTrainingPhase.Backward, ModelTrainingPhase.Epochs, ModelTrainingPhase.Evaluate,
                ModelTrainingPhase.Done]);
            steps.ShouldBe([1, 2, 3, 4, 4]);
            card.IsFinished.ShouldBeTrue();
            card.CaptionText.ShouldBe(card.Labels.EvaluateCaption(94.2, 90, true));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Training_Step_Raises_Its_Update_With_A_Falling_Loss()
    {
        var card = new ModelTraining { AutoPlay = false, StepCount = 4, LossValues = [1.9, 1.2, 0.8, 0.5] };
        var completed = new List<(int Step, double Loss)>();
        card.IterationCompleted += (_, e) => completed.Add((e.Iteration, e.Loss));
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.Restart();
            card.Pause();
            completed.Clear();
            card.JumpToEnd();
            completed.Select(c => c.Step).ShouldBe([1, 2, 3, 4]);
            completed.Select(c => c.Loss).ShouldBeInOrder(SortDirection.Descending);
            completed[^1].Loss.ShouldBe(0.5, 1e-9);
            card.CurrentLoss.ShouldBe(0.5, 1e-9);
            card.Iteration.ShouldBe(4);
            card.Timeline.LossFrom.ShouldBe(1.9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Step_While_Paused_Shows_Its_Still()
    {
        var card = new ModelTraining { AutoPlay = false };
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        try
        {
            Dispatcher.UIThread.RunJobs();
            // Before it plays it shows the finished model.
            card.CurrentStep.ShouldBe(4);
            card.RenderTime.ShouldBe(card.Timeline.Length);
            card.GoToStep(1);
            card.IsPlaying.ShouldBeFalse();
            card.CurrentStep.ShouldBe(1);
            card.StoryTime.ShouldBe(card.Timeline.StillTime(1), 1e-9);
            card.StepButtons[1].Progress.ShouldBe(1);
            card.StepButtons[0].Classes.ShouldNotContain(":current");
            TestHelpers.Descendant<TextBlock>(card, "PART_EpochValue").Text.ShouldBe("0");
            card.GoToStep(3);
            card.CurrentStep.ShouldBe(3);
            int.Parse(TestHelpers.Descendant<TextBlock>(card, "PART_EpochValue").Text!).ShouldBeGreaterThan(1);
            card.GoToStep(0);
            card.StoryTime.ShouldBe(card.Timeline.StillTime(0), 1e-9, "going back starts the run over");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_A_Step_While_Playing_Plays_On_From_It()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new ModelTraining { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            card.Play();
            Run(clock, 1);
            card.GoToStep(3);
            card.IsPlaying.ShouldBeTrue();
            card.StoryTime.ShouldBe(ModelTrainingTimeline.EpochsStart, 0.05);
            Run(clock, 1);
            card.StoryTime.ShouldBeGreaterThan(ModelTrainingTimeline.EpochsStart + 0.5);
            card.CurrentStep.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Playing_Run_Loops()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new ModelTraining { AutoPlay = false };
        var finished = 0;
        card.Finished += (_, _) => finished++;
        var window = Open(clock, card);
        try
        {
            card.Play();
            Run(clock, card.Timeline.Length + 2);
            finished.ShouldBe(1);
            card.IsPlaying.ShouldBeTrue("the run starts over on its own");
            card.StoryTime.ShouldBeLessThan(5);
            card.CurrentStep.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Resting_Mouse_Shows_A_Detail_And_Slows_The_Run()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new ModelTraining { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            var stage = TestHelpers.Descendant<ModelTrainingStage>(card);
            card.Play();
            Run(clock, 0.5);
            var layout = stage.Layout;
            stage.HitTestRegion(layout.Node(1, 0)).ShouldBe(ModelTrainingRegion.Layer1);
            stage.HitTestRegion(layout.Sheet(false, 0)).ShouldBe(ModelTrainingRegion.TrainingSet);
            stage.SetHover(ModelTrainingRegion.Layer1);
            card.DetailOf(ModelTrainingRegion.Layer1).ShouldBe(card.Labels.HiddenDetail(4, 12));
            Run(clock, 1.5);
            card.PaceRate.ShouldBe(ModelTraining.HoverRate, 0.02);
            var before = card.StoryTime;
            Run(clock, 1);
            (card.StoryTime - before).ShouldBe(ModelTraining.HoverRate, 0.05);
            stage.SetHover(ModelTrainingRegion.None);
            Run(clock, 1.5);
            card.PaceRate.ShouldBe(1, 0.02);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Width()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new ModelTraining { AutoPlay = false, Width = 904 };
        var window = Open(clock, card);
        try
        {
            var stage = TestHelpers.Descendant<ModelTrainingStage>(card);
            var panel = TestHelpers.Descendant<StackPanel>(card, "PART_Panel");
            stage.Bounds.Width.ShouldBe(640, 0.5);
            panel.Bounds.Width.ShouldBe(232, 0.5);
            stage.Layout.LayerX(0).ShouldBe(177, 0.5);
            stage.Layout.LayerX(3).ShouldBe(528, 0.5);
            card.IsCompact.ShouldBeFalse();

            card.Width = 552;
            Run(clock, 0.1);
            stage.Bounds.Width.ShouldBe(552, 0.5);
            panel.Bounds.Top.ShouldBeGreaterThan(stage.Bounds.Bottom);
            card.IsCompact.ShouldBeTrue();
            card.StepButtons[0].Classes.ShouldContain(":compact");
            stage.Layout.IsNarrow.ShouldBeFalse();

            card.Width = 342;
            Run(clock, 0.1);
            stage.Layout.IsNarrow.ShouldBeTrue();
            stage.Layout.Height.ShouldBe(301);
            stage.Layout.TrainingFront.Y.ShouldBeLessThan(stage.Layout.Node(0, 0).Y);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Opens_On_The_Finished_Run_Without_Play()
    {
        var card = new ModelTraining();
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.CurrentStep.ShouldBe(4);
            TestHelpers.Descendant<Control>(card, Explainer.PlayButtonPart).IsVisible.ShouldBeFalse();
            card.GoToStep(2);
            card.IsPlaying.ShouldBeFalse();
            card.CurrentStep.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Steps_And_Diagram_Are_Named_For_Screen_Readers()
    {
        var card = new ModelTraining { AutoPlay = false, ClassNames = ["Spam", "Mail"], Epochs = 8, Accuracy = 97.5, Target = 95 };
        var window = TestHelpers.Show(card, ArcMotionLevel.Off);
        try
        {
            Dispatcher.UIThread.RunJobs();
            AutomationProperties.GetName(card.StepButtons[2]).ShouldBe("Step 3 of 5: Backward pass");
            AutomationProperties.GetName(TestHelpers.Descendant<Grid>(card, ModelTraining.StepsPart)).ShouldBe("Training steps");
            var description = AutomationProperties.GetName(TestHelpers.Descendant<ModelTrainingStage>(card));
            description.ShouldNotBeNull();
            description.ShouldContain("Spam");
            description.ShouldContain("8 epochs");
            description.ShouldContain("97.5%");
            card.Description = "Custom";
            AutomationProperties.GetName(TestHelpers.Descendant<ModelTrainingStage>(card)).ShouldBe("Custom");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kept_Members_Still_Work()
    {
#pragma warning disable CS0618
        var labels = new ModelTrainingLabels { PhaseNames = ["A", "B", "C", "D", "E"] };
        labels.Steps.ShouldBe(["A", "B", "C", "D", "E"], "PhaseNames forwards to Steps");
        labels.Steps = ["1", "2", "3", "4", "5"];
        labels.PhaseNames[4].ShouldBe("5");
        labels.Ready = "x";
        labels.Speed.ShouldBe("Speed");
        labels.DoneCaption(1, 0.5).ShouldContain("0.50");
#pragma warning restore CS0618
        var card = new ModelTraining { AutoPlay = false, Labels = labels, LearningRate = 0.2, BatchSize = 64 };
        var window = TestHelpers.Show(card, ArcMotionLevel.Off);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.StepButtons[4].Text.ShouldBe("5");
            card.Timeline.Rate.ShouldBeGreaterThan(new ModelTrainingTimeline().Rate, "a larger learning rate moves the weights further");
            card.DetailOf(ModelTrainingRegion.TrainingSet).ShouldBe(labels.TrainingDetail(12800, labels.Unit, 64));
            card.Phase.ShouldBe(ModelTrainingPhase.Ready);
            double.IsNaN(card.CurrentLoss).ShouldBeTrue();
            card.Iteration.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }
}
