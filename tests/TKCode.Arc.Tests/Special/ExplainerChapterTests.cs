using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The DNS resolution explainer: the domain, the story through every server, stepping and layout.</summary>
public class DnsResolutionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(ManualFrameClock clock, Control card, double width = 760)
    {
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        window.Width = width;
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

    // ---- DNS resolution -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Dns_Reads_The_Domain_And_Starts_Ready()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            card.Title.ShouldBe("DNS resolution");
            card.Step.ShouldBe(DnsResolutionStep.Ready);
            card.TldText.ShouldBe(".dev");
            card.ZoneText.ShouldBe("lumen-garden.dev");
            card.NameServerText.ShouldBe("ns1.lumen-garden.dev");
            card.StatusText.ShouldBe("Ready");
            card.CaptionText.ShouldBe(card.Labels.ReadyCaption(card.Domain));
            AutomationProperties.GetName(card).ShouldBe("DNS resolution");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Plays_Through_Every_Server_Then_Answers_From_The_Cache()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var steps = new List<DnsResolutionStep>();
        card.StepChanged += (_, e) => steps.Add(e.Step);
        var window = Open(clock, card);
        try
        {
            card.Play();
            Run(clock, 0.8);
            card.Step.ShouldBe(DnsResolutionStep.Query);
            card.Scene.Packet.ShouldNotBeNull().To.ShouldBe(DnsResolutionNode.Resolver);
            card.StatusText.ShouldBe("Resolving");
            Run(clock, 30);
            card.IsFinished.ShouldBeTrue();
            steps.ShouldBe([DnsResolutionStep.Query, DnsResolutionStep.Root, DnsResolutionStep.Tld, DnsResolutionStep.Authoritative,
                DnsResolutionStep.Answer, DnsResolutionStep.Cached, DnsResolutionStep.Done]);
            var scene = card.Scene;
            scene.HasCacheEntry.ShouldBeTrue();
            scene.HasClientAddress.ShouldBeTrue();
            scene.CacheFlash.ShouldBe(DnsResolutionCacheFlash.Hit);
            scene.RoundTrips.ShouldBe(1, "the cached lookup is one round trip");
            scene.ElapsedTarget.ShouldBe(DnsResolution.RoundTripMs[0]);
            scene.Log.Count.ShouldBe(12);
            scene.Log[3].Text.ShouldBe("Ask the .dev servers");
            scene.Log[7].Text.ShouldBe("203.0.113.42, keep it 300 s");
            card.CaptionText.ShouldBe(card.Labels.DoneCaption(97, 4));
            card.StatusText.ShouldBe("Resolved");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Steps_One_Chapter_At_A_Time_Under_Reduced_Motion()
    {
        var card = new DnsResolution { AutoPlay = false, IsRepeatLookupShown = false, Domain = "api.kite.io", Address = "198.51.100.9" };
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.Restart();
            card.Pause();
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Query);
            card.Scene.CacheFlash.ShouldBe(DnsResolutionCacheFlash.Miss);
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Root);
            card.Scene.RoundTrips.ShouldBe(1);
            card.Scene.Log[^1].Text.ShouldBe("Ask the .io servers");
            card.StepForward();
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Authoritative);
            card.Scene.Log[^1].Text.ShouldBe("198.51.100.9, keep it 300 s");
            card.StepForward();
            card.Step.ShouldBe(DnsResolutionStep.Answer);
            card.Scene.ElapsedTarget.ShouldBe(97);
            card.StepForward();
            card.IsFinished.ShouldBeTrue();
            card.CaptionText.ShouldBe(card.Labels.DoneCaption(97, 0));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dns_Stage_Stacks_The_Servers_On_Narrow_Cards()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new DnsResolution { AutoPlay = false };
        var window = Open(clock, card, 760);
        try
        {
            var stage = TestHelpers.Descendant<DnsResolutionStage>(card);
            stage.IsWide.ShouldBeTrue();
            stage.NodeRect(DnsResolutionNode.Root).X.ShouldBeGreaterThan(stage.NodeRect(DnsResolutionNode.Resolver).Right);
            window.Width = 380;
            Dispatcher.UIThread.RunJobs();
            stage.IsWide.ShouldBeFalse();
            stage.NodeRect(DnsResolutionNode.Root).Y.ShouldBeGreaterThan(stage.CacheRect.Bottom);
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>The model training explainer: phases, losses, weights and the story clock.</summary>
public class ModelTrainingTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static Window Open(ManualFrameClock clock, Control card, double width = 760)
    {
        var window = TestHelpers.Show(card, ArcMotionLevel.Full);
        window.Width = width;
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

    // ---- Model training -------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Training_Walks_The_First_Step_Phase_By_Phase()
    {
        var card = new ModelTraining { AutoPlay = false };
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            card.Restart();
            card.Pause();
            var phases = new List<ModelTrainingPhase>();
            for (var i = 0; i < 5; i++)
            {
                card.StepForward();
                phases.Add(card.Phase);
            }
            phases.ShouldBe([ModelTrainingPhase.Batch, ModelTrainingPhase.Forward, ModelTrainingPhase.Loss, ModelTrainingPhase.Backward,
                ModelTrainingPhase.Update]);
            card.Iteration.ShouldBe(1);
            card.CurrentLoss.ShouldBe(2.3, 0.0001);
            card.Scene.Plotted.ShouldBe(1);
            card.CaptionText.ShouldBe(card.Labels.UpdateCaption(0.05));
            card.StepForward();
            card.Iteration.ShouldBe(2, "later steps stop once per step");
            card.Phase.ShouldBe(ModelTrainingPhase.Update);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Training_Ends_With_Every_Loss_On_The_Curve()
    {
        var card = new ModelTraining { AutoPlay = false, StepCount = 4 };
        card.LossValues = [1.9, 1.2, 0.8, 0.5];
        var completed = new List<(int, double)>();
        card.IterationCompleted += (_, e) => completed.Add((e.Iteration, e.Loss));
        var window = TestHelpers.Show(card, ArcMotionLevel.Reduced);
        try
        {
            Dispatcher.UIThread.RunJobs();
            // The data set before the card was shown rebuilt the story; read the events of one run to its end.
            card.Restart();
            card.Pause();
            completed.Clear();
            card.JumpToEnd();
            card.IsFinished.ShouldBeTrue();
            card.Phase.ShouldBe(ModelTrainingPhase.Done);
            card.Losses.ShouldBe([1.9, 1.2, 0.8, 0.5]);
            card.CurrentLoss.ShouldBe(0.5);
            card.Scene.Plotted.ShouldBe(4);
            completed.Select(c => c.Item1).ShouldBe([1, 2, 3, 4]);
            card.CaptionText.ShouldBe(card.Labels.DoneCaption(1.9, 0.5));
            ModelTraining.TargetProbability(0.5).ShouldBe(Math.Exp(-0.5), 1e-12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Training_Weights_Move_Toward_Their_Trained_Values()
    {
        var scene = new ModelTrainingScene();
        scene.Prepare(6);
        var start = scene.WeightsAfter(0);
        var later = scene.WeightsAfter(6);
        var far = scene.WeightsAfter(60);
        start.Length.ShouldBe(ModelTrainingScene.WeightCount);
        ModelTrainingScene.WeightCount.ShouldBe(35);
        double Distance(double[] a, double[] b) => a.Zip(b, (x, y) => Math.Abs(x - y)).Sum();
        Distance(later, far).ShouldBeLessThan(Distance(start, far));
        ModelTraining.SampleLoss(0).ShouldBe(2.3, 0.0001);
        ModelTraining.SampleLoss(5).ShouldBeLessThan(ModelTraining.SampleLoss(1));
    }

    [AvaloniaFact]
    public void Training_Plays_The_Forward_Wave_On_The_Story_Clock()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var card = new ModelTraining { AutoPlay = false };
        var window = Open(clock, card);
        try
        {
            card.Play();
            Run(clock, 1.6);
            card.Phase.ShouldBe(ModelTrainingPhase.Forward);
            card.StatusText.ShouldBe("Training");
            card.Pause();
            var time = card.StoryTime;
            Run(clock, 1);
            card.StoryTime.ShouldBe(time, "pausing freezes the story");
            TestHelpers.Descendant<ModelTrainingStage>(card).Nodes.Count.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }
}
