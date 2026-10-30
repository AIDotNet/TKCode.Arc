using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// TrainingRun: the simulated run it plays with no status (queue, start up, training, completion, the OOM failure and the
/// rollback on retry), app-driven states and their buttons, the two step cancel, deploy (success, failure, abort), the
/// chart's fixed axes, easing, rollback and keyboard model, the event log's slide, the narrow layout, announcements and
/// the pure formats.
/// </summary>
public class TrainingRunTests
{
    private static (TrainingRun Card, Window Window) Open(Action<TrainingRun>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off,
        double width = 620)
    {
        var card = new TrainingRun { Simulation = TrainingRunTestData.Simulation(), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (card, window);
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

    private static void Click(Avalonia.Controls.Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static void AddCurve(TrainingRun card, int upTo, int every = 10)
    {
        for (var step = every; step <= upTo; step += every)
        {
            card.Loss.Add(new TrainingRunLossPoint(step, 2.0 - step / 2000.0, step % 100 == 0 ? 2.1 - step / 2000.0 : null));
        }
    }

    private static void Running(TrainingRun card)
    {
        card.TotalSteps = 1000;
        card.Epochs = 2;
        card.Step = 300;
        card.Status = TrainingRunStatus.Running;
    }

    [AvaloniaFact]
    public void Plays_The_Sample_Run_Queued_First()
    {
        var (card, window) = Open();
        try
        {
            card.IsSimulated.ShouldBeTrue();
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Queued);
            card.StatusText.ShouldBe("Queued");
            card.DisplayName.ShouldBe("support-v4");
            TestHelpers.Descendant<TextBlock>(card, "PART_Subtitle").Text.ShouldBe("Model A · LoRA r16 · 8× H200");
            card.ActivityText.ShouldBe("Waiting for 8× H200 · 2nd in queue");
            card.StatCells[3].ValueText.ShouldBe("About 18m 40s");
            card.StatCells[0].ValueText.ShouldBe("–");
            card.FooterKey.ShouldStartWith("dataset:contoso/support-tickets-oct");
            card.CancelButton!.IsVisible.ShouldBeTrue();
            card.PauseButton!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Starts_Up_Trains_And_Completes()
    {
        var (card, window) = Open();
        try
        {
            card.AdvanceSimulation(3);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Running);
            card.IsStartingUp.ShouldBeTrue();
            card.StatusText.ShouldBe("Starting");
            card.ActivityText.ShouldBe("Loading the base model onto 8× H200");

            card.AdvanceSimulation(4);
            card.IsStartingUp.ShouldBeFalse();
            card.StatusText.ShouldBe("Training");
            card.ActivityText.ShouldBeNull();
            card.EffectiveLoss.Count.ShouldBeGreaterThan(0);
            card.LegendTrain.ShouldNotBe("–");
            card.LegendTail.ShouldBe("Epoch 1 of 3");
            card.StatCells[0].ValueText.ShouldEndWith(" / 2,400");
            card.StatCells[1].ValueText.ShouldContain("e-");
            card.PauseButton!.IsVisible.ShouldBeTrue();
            card.AnnouncerPartControl!.History.ShouldContain("Training");

            card.AdvanceSimulation(60);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Completed);
            card.EffectiveStep.ShouldBe(2400);
            card.ShowsMetrics.ShouldBeTrue();
            card.MetricCells.Select(m => m.Label).ShouldBe(["Train loss", "Validation loss", "Steps", "Duration"]);
            card.MetricCells[2].ValueText.ShouldBe("2,400");
            card.MetricCells[2].Hint.ShouldBe("3 epochs");
            card.MetricCells[3].ValueText.ShouldBe("17m 26s");
            card.DeployButton!.IsVisible.ShouldBeTrue();
            card.RestartButton!.IsVisible.ShouldBeTrue();
            card.CancelButton!.IsVisible.ShouldBeFalse();
            card.EventListPart!.RowKind(0).ShouldBe(TrainingRunEventKind.Done);
            card.AnnouncerPartControl.History.ShouldContain("Completed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Oom_Fails_At_940_And_Retry_Rolls_Back_To_The_Checkpoint()
    {
        var (card, window) = Open(c => c.Scenario = TrainingRunScenario.Oom);
        try
        {
            card.AdvanceSimulation(30);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Failed);
            card.EffectiveStep.ShouldBe(TrainingRunPlayer.OomStep);
            card.FooterKey.ShouldStartWith("error:CUDA out of memory");
            card.RetryButton!.IsVisible.ShouldBeTrue();
            card.RetryButton.Content.ShouldBe("Retry from checkpoint");
            card.EventListPart!.RowKind(0).ShouldBe(TrainingRunEventKind.Error);

            var retried = 0;
            card.RetryRequested += (_, _) => retried++;
            Click(card.RetryButton);
            retried.ShouldBe(1);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Running);
            card.EffectiveStep.ShouldBe(800);
            card.EffectiveLoss[^1].Step.ShouldBe(800);
            card.ChartPartControl!.Head.ShouldBe(800);
            card.EventListPart.RowText(0).ShouldBe("Resumed from checkpoint at step 800");

            // It fails only once.
            card.AdvanceSimulation(60);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Completed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulation_Pauses_Resumes_And_Cancels()
    {
        var (card, window) = Open();
        try
        {
            card.AdvanceSimulation(6);
            Click(card.PauseButton!);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Paused);
            var step = card.EffectiveStep;
            card.AdvanceSimulation(5);
            card.EffectiveStep.ShouldBe(step);
            card.ResumeButton!.IsVisible.ShouldBeTrue();
            card.AnnouncerPartControl!.History.ShouldContain("Paused");

            Click(card.ResumeButton);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Running);
            Click(card.CancelButton!);
            Click(card.CancelButton!);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Cancelled);
            card.RestartButton!.IsVisible.ShouldBeTrue();

            Click(card.RestartButton);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Queued);
            card.EffectiveLoss.Count.ShouldBe(0);
            card.ChartPartControl!.Head.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cancel_Needs_A_Second_Press_Within_Three_Seconds()
    {
        var clock = new AgentStatusTestClock();
        var (card, window) = Open(c =>
        {
            c.Clock = clock;
            Running(c);
        });
        try
        {
            var cancelled = 0;
            card.CancelRequested += (_, _) => cancelled++;
            Click(card.CancelButton!);
            cancelled.ShouldBe(0);
            card.IsConfirmingCancel.ShouldBeTrue();
            card.CancelButton!.Content.ShouldBe("Confirm cancel");
            card.CancelButton.Variant.ShouldBe(ButtonVariant.Danger);
            card.AnnouncerPartControl!.History.ShouldContain("Press Confirm cancel to stop the run");

            clock.Advance(3100);
            Pump();
            card.IsConfirmingCancel.ShouldBeFalse();
            card.CancelButton.Content.ShouldBe("Cancel");
            card.CancelButton.Variant.ShouldBe(ButtonVariant.Secondary);

            Click(card.CancelButton);
            clock.Advance(1000);
            Pump();
            Click(card.CancelButton);
            cancelled.ShouldBe(1);
            card.IsConfirmingCancel.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Buttons_Follow_The_Status_And_The_Allowed_Actions()
    {
        var (card, window) = Open(Running);
        try
        {
            card.PauseButton!.IsVisible.ShouldBeTrue();
            card.CancelButton!.IsVisible.ShouldBeTrue();
            card.ResumeButton!.IsVisible.ShouldBeFalse();

            card.Status = TrainingRunStatus.Paused;
            Pump();
            card.ResumeButton.IsVisible.ShouldBeTrue();
            card.ResumeButton.Variant.ShouldBe(ButtonVariant.Primary);
            card.PauseButton.IsVisible.ShouldBeFalse();

            card.Status = TrainingRunStatus.Failed;
            Pump();
            card.RetryButton!.IsVisible.ShouldBeTrue();
            card.RestartButton!.IsVisible.ShouldBeTrue();
            card.CancelButton.IsVisible.ShouldBeFalse();

            card.Status = TrainingRunStatus.Completed;
            Pump();
            card.DeployButton!.IsVisible.ShouldBeFalse(); // no handler
            card.DeployHandler = _ => Task.FromResult<string?>(null);
            Pump();
            card.DeployButton.IsVisible.ShouldBeTrue();

            card.Actions = TrainingRunActions.None;
            Pump();
            card.DeployButton.IsVisible.ShouldBeFalse();
            card.RestartButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Deploy_Loads_Then_Shows_The_Endpoint()
    {
        var pending = new TaskCompletionSource<string?>();
        var (card, window) = Open(c =>
        {
            Running(c);
            AddCurve(c, 300);
            c.Status = TrainingRunStatus.Completed;
            c.DeployHandler = _ => pending.Task;
        });
        try
        {
            Click(card.DeployButton!);
            card.Deploy.ShouldBe(TrainingRun.DeployState.Deploying);
            card.DeployButton!.IsLoading.ShouldBeTrue();
            AutomationProperties.GetName(card.DeployButton).ShouldBe("Deploying");

            pending.SetResult("https://inference.example.com/v1/run");
            TestHelpers.PumpUntil(() => card.Deploy == TrainingRun.DeployState.Deployed).ShouldBeTrue();
            Pump();
            card.Endpoint.ShouldBe("https://inference.example.com/v1/run");
            card.FooterKey.ShouldBe("deployed:https://inference.example.com/v1/run");
            card.CopyEndpointButton!.Value.ShouldBe("https://inference.example.com/v1/run");
            card.DeployButton.IsVisible.ShouldBeFalse();
            card.AnnouncerPartControl!.History.ShouldContain("Deployed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Deploy_Shows_The_Message_And_Retry_Deploy()
    {
        var (card, window) = Open(c =>
        {
            Running(c);
            c.Status = TrainingRunStatus.Completed;
            c.DeployHandler = _ => Task.FromException<string?>(new InvalidOperationException("Quota exceeded"));
        });
        try
        {
            Click(card.DeployButton!);
            TestHelpers.PumpUntil(() => card.Deploy == TrainingRun.DeployState.Failed).ShouldBeTrue();
            Pump();
            card.FooterKey.ShouldBe("deploy-failed:Quota exceeded");
            card.DeployButton!.Content.ShouldBe("Retry deploy");
            card.DeployButton.IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Deploy_Is_Aborted_When_The_Run_Leaves_Completed()
    {
        CancellationToken seen = default;
        var (card, window) = Open(c =>
        {
            Running(c);
            c.Status = TrainingRunStatus.Completed;
            c.DeployHandler = token =>
            {
                seen = token;
                return new TaskCompletionSource<string?>().Task;
            };
        });
        try
        {
            Click(card.DeployButton!);
            seen.IsCancellationRequested.ShouldBeFalse();
            card.Status = TrainingRunStatus.Running;
            Pump();
            seen.IsCancellationRequested.ShouldBeTrue();
            card.Deploy.ShouldBe(TrainingRun.DeployState.Idle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Axes_Are_Fixed_And_A_Rollback_Jumps()
    {
        var (card, window) = Open(Running);
        try
        {
            var chart = card.ChartPartControl!;
            chart.AxisTop.ShouldBeNull();
            AddCurve(card, 300);
            Pump();
            // The first train loss is 1.995: with 8% headroom, rounded up to 0.2.
            chart.AxisTop!.Value.ShouldBe(2.2, 1e-9);
            card.Loss.Add(new TrainingRunLossPoint(310, 9.0));
            Pump();
            chart.AxisTop!.Value.ShouldBe(2.2, 1e-9);
            chart.Head.ShouldBe(310);
            chart.Progress.ShouldBe(300);

            card.Loss.RemoveAt(card.Loss.Count - 1);
            card.Loss.RemoveAt(card.Loss.Count - 1);
            Pump();
            chart.Head.ShouldBe(290);

            card.Loss.Clear();
            Pump();
            chart.Head.ShouldBe(0);
            chart.AxisTop.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Eases_Toward_New_Samples_In_Full_Motion()
    {
        var (card, window) = Open(Running, ArcMotionLevel.Full);
        try
        {
            AddCurve(card, 200);
            Dispatcher.UIThread.RunJobs();
            var chart = card.ChartPartControl!;
            chart.HeadTarget.ShouldBe(200);
            chart.Head.ShouldBeLessThan(200);
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return chart.Head == 200;
            }).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Keyboard_Inspects_Samples()
    {
        var (card, window) = Open(c =>
        {
            Running(c);
            AddCurve(c, 300);
        });
        try
        {
            var chart = card.ChartPartControl!;
            chart.Focus(NavigationMethod.Tab);
            Pump();
            AutomationProperties.GetName(chart)!.ShouldStartWith("Loss chart, step 300, train loss ");

            Press(window, PhysicalKey.ArrowLeft);
            chart.InspectedIndex.ShouldBe(29);
            card.LegendTail.ShouldBe("step 300");
            Press(window, PhysicalKey.ArrowLeft);
            chart.InspectedIndex.ShouldBe(28);
            card.LegendTail.ShouldBe("step 290");
            card.LegendValidation.ShouldBe("2.000");
            Press(window, PhysicalKey.Home);
            chart.InspectedIndex.ShouldBe(0);
            card.LegendValidation.ShouldBe("–");
            Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            chart.InspectedIndex.ShouldBe(10);
            AutomationProperties.GetName(chart)!.ShouldStartWith("Loss chart, step 110, train loss ");
            Press(window, PhysicalKey.End);
            chart.InspectedIndex.ShouldBe(29);
            Press(window, PhysicalKey.ArrowRight);
            chart.InspectedIndex.ShouldBe(29);
            Press(window, PhysicalKey.Escape);
            chart.InspectedIndex.ShouldBeNull();
            card.LegendTail.ShouldBe("Epoch 1 of 2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Event_Log_Shows_The_Newest_Three_And_Slides_In_Full_Motion()
    {
        var (card, window) = Open(Running, ArcMotionLevel.Full);
        try
        {
            var list = card.EventListPart!;
            card.Events.Add(new TrainingRunEvent("a", TrainingRunEventKind.Start, "Started", 0));
            card.Events.Add(new TrainingRunEvent("b", TrainingRunEventKind.Checkpoint, "Checkpoint saved", 400));
            card.Events.Add(new TrainingRunEvent("c", TrainingRunEventKind.Eval, "Eval", 800));
            card.Events.Add(new TrainingRunEvent("d", TrainingRunEventKind.Warning, "Spike", 900));
            list.ShownCount.ShouldBe(3);
            list.RowText(0).ShouldBe("Spike");
            list.RowStep(0).ShouldBe("step 900");
            list.RowText(2).ShouldBe("Checkpoint saved");
            list.RowKind(1).ShouldBe(TrainingRunEventKind.Eval);
            list.SlideProgress.ShouldBeLessThan(1);
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return list.SlideProgress >= 1;
            }).ShouldBeTrue();

            // A reset changes at once.
            card.Events.Clear();
            list.ShownCount.ShouldBe(0);
            list.SlideProgress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Card_Shortens_The_Legend_And_Uses_Two_Columns()
    {
        var (card, window) = Open(Running, width: 400);
        try
        {
            card.Classes.Contains(TrainingRun.NarrowPseudoClass).ShouldBeTrue();
            card.LegendValidationLabel.ShouldBe("Val");
            TestHelpers.Descendant<Avalonia.Controls.Primitives.UniformGrid>(card, "PART_Stats").Columns.ShouldBe(2);
            window.Width = 620;
            Pump();
            card.Classes.Contains(TrainingRun.NarrowPseudoClass).ShouldBeFalse();
            card.LegendValidationLabel.ShouldBe("Validation");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void App_Driven_Card_Uses_Its_Own_Data_Labels_And_Notices()
    {
        var (card, window) = Open(c =>
        {
            Running(c);
            c.JobName = "my-job";
            c.BaseModel = new AgentModel("Model B");
            c.Details = ["LoRA r8"];
            c.Dataset = new TrainingRunDataset("fabrikam/faq", 1200);
            c.LearningRate = 0.000184;
            c.Elapsed = TimeSpan.FromSeconds(724);
            c.Eta = TimeSpan.FromSeconds(376);
            c.Labels = new TrainingRunLabels { Training = "Entrenando" };
            AddCurve(c, 100);
        });
        try
        {
            card.DisplayName.ShouldBe("my-job");
            TestHelpers.Descendant<TextBlock>(card, "PART_Subtitle").Text.ShouldBe("Model B · LoRA r8");
            TestHelpers.Descendant<TextBlock>(card, "PART_MonogramText").Text.ShouldBe("M");
            card.StatusText.ShouldBe("Entrenando");
            card.StatCells[0].ValueText.ShouldBe("300 / 1,000");
            card.StatCells[1].ValueText.ShouldBe("1.84e-4");
            card.StatCells[2].ValueText.ShouldBe("12m 4s");
            card.StatCells[3].ValueText.ShouldBe("6m 16s");
            AutomationProperties.GetName(card.StatCells[2]).ShouldBe("Elapsed: 12m 4s");
            card.FooterKey.ShouldStartWith("dataset:fabrikam/faq|1200");

            card.Notice = "Checkpoint restored";
            Pump();
            card.AnnouncerPartControl!.History.ShouldContain("Checkpoint restored");

            card.Status = TrainingRunStatus.Failed;
            card.ErrorText = "Disk full";
            Pump();
            card.FooterKey.ShouldBe("error:Disk full");
            card.AnnouncerPartControl.History.ShouldContain("Failed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Apply_Sets_Values_And_Appends_Samples_And_Events()
    {
        var (card, window) = Open(Running);
        try
        {
            card.Apply(new TrainingRunProgress
            {
                Step = 420,
                Loss = new TrainingRunLossPoint(420, 1.2, 1.3),
                Event = new TrainingRunEvent("x", TrainingRunEventKind.Checkpoint, "Checkpoint saved", 400),
                LearningRate = 1e-4,
                Eta = TimeSpan.FromSeconds(42),
            });
            Pump();
            card.Step.ShouldBe(420);
            card.Loss.Count.ShouldBe(1);
            card.Events.Count.ShouldBe(1);
            card.StatCells[3].ValueText.ShouldBe("42s");
            card.LegendTrain.ShouldBe("1.200");
            card.LegendValidation.ShouldBe("1.300");
            card.Apply(new TrainingRunProgress { Status = TrainingRunStatus.Completed });
            card.Status.ShouldBe(TrainingRunStatus.Completed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FollowAsync_Applies_Every_Update()
    {
        var (card, window) = Open(Running);
        try
        {
            await card.FollowAsync(Updates());
            card.Loss.Count.ShouldBe(3);
            card.Step.ShouldBe(30);
        }
        finally
        {
            window.Close();
        }

        static async IAsyncEnumerable<TrainingRunProgress> Updates()
        {
            for (var i = 1; i <= 3; i++)
            {
                await Task.Yield();
                yield return new TrainingRunProgress { Step = i * 10, Loss = new TrainingRunLossPoint(i * 10, 2.0 / i) };
            }
        }
    }

    [Fact]
    public void Formats_Numbers_And_Times()
    {
        TrainingRunFormat.Scientific(0.000184).ShouldBe((1.84, -4));
        TrainingRunFormat.Scientific(0.0009999).ShouldBe((1.0, -3));
        TrainingRunFormat.LearningRate(2e-5).ShouldBe("2.00e-5");
        TrainingRunFormat.Loss(0.81234).ShouldBe("0.812");
        var labels = TrainingRunLabels.Default;
        TrainingRunFormat.Time(TimeSpan.FromSeconds(42), labels).ShouldBe("42s");
        TrainingRunFormat.Time(TimeSpan.FromSeconds(1120), labels).ShouldBe("18m 40s");
        TrainingRunFormat.Time(TimeSpan.FromSeconds(3725), labels).ShouldBe("1h 2m");
        TrainingRunFormat.AxisTop(2.35).ShouldBe(2.6, 1e-9);
        TrainingRunFormat.AxisTop(0.5).ShouldBe(0.6, 1e-9);
        TrainingRunFormat.EpochOf(799, 2400, 3).ShouldBe(1);
        TrainingRunFormat.EpochOf(800, 2400, 3).ShouldBe(2);
        TrainingRunFormat.EpochOf(2400, 2400, 3).ShouldBe(3);
        var smooth = new List<double>();
        TrainingRunFormat.Smooth([2.0, 2.0, 2.0], smooth);
        smooth.ShouldAllBe(v => Math.Abs(v - 2.0) < 1e-9);
    }

    [AvaloniaFact]
    public void With_No_Status_And_No_Simulation_It_Shows_A_Queued_Job_With_No_Data()
    {
        var (card, window) = Open(c => c.Simulation = null);
        try
        {
            card.IsSimulated.ShouldBeFalse();
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Queued);
            card.DisplayName.ShouldBe("Fine-tuning job");
            TestHelpers.Descendant<TextBlock>(card, "PART_Subtitle").Text.ShouldBe(string.Empty);
            card.EffectiveLoss.ShouldBeEmpty();
            card.EffectiveEvents.ShouldBeEmpty();
            card.ActivityText.ShouldBe(TrainingRunLabels.Default.WaitingInQueue);
            card.AdvanceSimulation(10);
            card.EffectiveStep.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Apps_Status_Wins_Over_The_Simulation()
    {
        var (card, window) = Open(c =>
        {
            c.Status = TrainingRunStatus.Running;
            c.Step = 120;
            c.TotalSteps = 600;
        });
        try
        {
            card.IsSimulated.ShouldBeFalse();
            card.EffectiveStep.ShouldBe(120);
            card.DisplayName.ShouldBe("Fine-tuning job");
            card.AdvanceSimulation(10);
            card.EffectiveStep.ShouldBe(120);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_And_Setting_It_To_Null_Stops_It()
    {
        var (card, window) = Open();
        try
        {
            card.AdvanceSimulation(TrainingRunPlayer.QueueSeconds + TrainingRunPlayer.StartupSeconds + 2);
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Running);
            card.EffectiveStep.ShouldBeGreaterThan(0);
            card.Simulation = null;
            Pump();
            card.IsSimulated.ShouldBeFalse();
            card.EffectiveStep.ShouldBe(0);
            card.EffectiveLoss.ShouldBeEmpty();
            card.EffectiveStatus.ShouldBe(TrainingRunStatus.Queued);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Simulation_Is_Deterministic_And_Its_Curves_Fall()
    {
        TrainingRunPlayer.TrainLoss(10).ShouldBeGreaterThan(TrainingRunPlayer.TrainLoss(2000));
        TrainingRunPlayer.ValidationLoss(100).ShouldBeGreaterThan(TrainingRunPlayer.ValidationLoss(2400));
        TrainingRunPlayer.LearningRateAt(100).ShouldBe(2e-4, 1e-12);
        TrainingRunPlayer.LearningRateAt(2400).ShouldBe(2e-5, 1e-12);
        var a = new TrainingRunPlayer();
        var b = new TrainingRunPlayer();
        for (var i = 0; i < 400; i++)
        {
            a.Advance(1 / 60.0);
            b.Advance(1 / 60.0);
        }
        a.Loss.Select(p => p.Train).ShouldBe(b.Loss.Select(p => p.Train));
        a.Events.Select(e => e.Label).ShouldBe(b.Events.Select(e => e.Label));
    }
}

/// <summary>The invented job these tests play (the gallery's demo job has its own data).</summary>
internal static class TrainingRunTestData
{
    public static TrainingRunSimulation Simulation() => new()
    {
        JobName = "support-v4",
        BaseModel = new AgentModel("Model A"),
        Details = ["LoRA r16", "8× H200"],
        Dataset = new TrainingRunDataset("contoso/support-tickets-oct", 48210, null),
        Hardware = "8× H200",
        Endpoint = "https://inference.example.com/v1/support-v4",
    };
}
