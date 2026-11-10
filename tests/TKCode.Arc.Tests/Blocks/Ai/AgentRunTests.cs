using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AgentRun"/>: the sample run, app-driven steps, Stop and Retry, the clock, keyboard and screen readers.</summary>
public class AgentRunTests
{
    /// <summary>A manual scheduler: the callbacks wait until <see cref="Fire"/>.</summary>
    private sealed class ManualSchedule
    {
        public readonly List<Action> Pending = new();

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            Pending.Add(callback);
            return new Handle(() => Pending.Remove(callback));
        }

        public void Fire()
        {
            var next = Pending[0];
            Pending.RemoveAt(0);
            next();
        }

        private sealed class Handle(Action dispose) : IDisposable
        {
            public void Dispose() => dispose();
        }
    }

    private static (AgentRun Run, Window Window, ManualSchedule Clock) ShowSample()
    {
        var clock = new ManualSchedule();
        var run = new AgentRun { Width = 640 };
        run.Schedule = clock.Schedule;
        var window = TestHelpers.Show(run);
        Settle(window);
        return (run, window, clock);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static List<AgentRunStepView> Views(AgentRun run) => run.GetVisualDescendants().OfType<AgentRunStepView>().ToList();

    [AvaloniaFact]
    public void Sample_Run_Starts_When_Shown_And_Plays_Every_Step_To_Completed()
    {
        var (run, window, clock) = ShowSample();
        run.IsSimulated.ShouldBeTrue();
        run.State.ShouldBe(AgentRunState.Running);
        run.ShownSteps.Count.ShouldBe(1);
        run.ShownSteps[0].Status.ShouldBe(AgentRunStepStatus.Running);
        while (clock.Pending.Count > 0)
        {
            clock.Fire();
        }
        Settle(window);
        run.State.ShouldBe(AgentRunState.Completed);
        run.ShownSteps.Count.ShouldBe(6);
        run.ShownSteps.ShouldAllBe(s => s.Status == AgentRunStepStatus.Done);
        run.Cost.ShouldBe(AgentRunSamples.TotalCost);
        run.Tokens.ShouldBe(AgentRunSamples.TotalTokens);
        run.ActionButton!.Content.ShouldBe("Run again");
        run.Announcer!.Announcement!.ShouldStartWith("Completed in");
    }

    [AvaloniaFact]
    public void Stop_Raises_The_Event_And_Marks_The_Running_Step_Stopped()
    {
        var (run, window, clock) = ShowSample();
        var stops = 0;
        run.StopRequested += (_, _) => stops++;
        run.ActionButton!.Content.ShouldBe("Stop");
        run.Stop();
        Settle(window);
        stops.ShouldBe(1);
        run.State.ShouldBe(AgentRunState.Stopped);
        run.ShownSteps[^1].Status.ShouldBe(AgentRunStepStatus.Stopped);
        clock.Pending.ShouldBeEmpty();
        run.NoteLine.ShouldBe(new AgentRunLabels().StoppedNote);
        run.ActionButton.Content.ShouldBe("Retry");

        var retries = 0;
        run.RetryRequested += (_, _) => retries++;
        run.Retry();
        retries.ShouldBe(1);
        run.State.ShouldBe(AgentRunState.Running);
        run.ShownSteps.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void App_Steps_Are_Followed_As_They_Arrive_And_Change()
    {
        var steps = new ObservableCollection<AgentRunStep>();
        var run = new AgentRun { Width = 640, Steps = steps, State = AgentRunState.Running, Goal = "Tidy the docs" };
        var window = TestHelpers.Show(run);
        Settle(window);
        run.IsSimulated.ShouldBeFalse();
        run.ProgressLine.ShouldBe(new AgentRunLabels().Waiting);

        var step = new AgentRunStep { Kind = AgentRunStepKind.ToolCall, Title = "List the pages", ToolName = "list_files", Status = AgentRunStepStatus.Running };
        steps.Add(step);
        Settle(window);
        Views(run).Count.ShouldBe(1);
        run.Announcer!.Announcement.ShouldBe("Running: List the pages");
        run.ProgressLine.ShouldBe("0 of 1 steps");

        step.Output = "12 pages";
        step.Duration = TimeSpan.FromSeconds(0.8);
        step.Status = AgentRunStepStatus.Done;
        Settle(window);
        run.ProgressLine.ShouldBe("1 of 1 steps");
        run.ProgressBar!.Statuses.ToArray().ShouldBe([AgentRunStepStatus.Done]);
        var header = Views(run)[0].Header!;
        header.Meta.ShouldBe("0.8s");
        AutomationProperties.GetName(header).ShouldBe("Tool call: List the pages, done, 0.8s");
    }

    [AvaloniaFact]
    public void Pressing_A_Step_Opens_Its_Detail_With_Input_And_Result()
    {
        var run = new AgentRun { Width = 640, Steps = AgentRunSamples.CompletedSteps(), State = AgentRunState.Completed };
        var window = TestHelpers.Show(run);
        Settle(window);
        var tool = run.ShownSteps[1];
        AgentRunStepEventArgs? toggled = null;
        run.StepToggled += (_, e) => toggled = e;
        run.SetStepExpanded(tool, true);
        Settle(window);
        toggled!.Step.ShouldBe(tool);
        toggled.IsExpanded.ShouldBeTrue();
        var view = run.FindView(tool)!;
        view.Header!.IsExpanded.ShouldBeTrue();
        var texts = view.DetailContent!.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        texts.ShouldContain("Input");
        texts.ShouldContain("Result");
        texts.ShouldContain(tool.Output);
        run.IsStepExpanded(tool).ShouldBeTrue();
        // The message is always open.
        run.IsStepExpanded(run.ShownSteps[^1]).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Down_And_Up_Move_Between_Step_Headers()
    {
        var run = new AgentRun { Width = 640, Steps = AgentRunSamples.CompletedSteps(), State = AgentRunState.Completed };
        var window = TestHelpers.Show(run);
        Settle(window);
        var first = run.FindView(run.ShownSteps[0])!.Header!;
        first.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Settle(window);
        run.FindView(run.ShownSteps[1])!.Header!.IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        Settle(window);
        run.FindView(run.ShownSteps[^1])!.Header!.IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clock_Counts_Whole_Seconds_While_Running()
    {
        var now = TimeSpan.Zero;
        var run = new AgentRun { Width = 640, Steps = new ObservableCollection<AgentRunStep>(), Now = () => now };
        var window = TestHelpers.Show(run);
        run.State = AgentRunState.Running;
        now = TimeSpan.FromSeconds(65.4);
        run.Tick();
        run.Elapsed.ShouldBe(TimeSpan.FromSeconds(65));
        run.ElapsedText.ShouldBe("1m 05s");
        run.State = AgentRunState.Failed;
        now = TimeSpan.FromSeconds(200);
        run.Tick();
        run.Elapsed.ShouldBe(TimeSpan.FromSeconds(65));
        window.Close();
    }

    [AvaloniaFact]
    public void Failed_Run_Shows_The_Reason_And_Offers_Retry()
    {
        var run = new AgentRun { Width = 640, Steps = AgentRunSamples.CompletedSteps(), State = AgentRunState.Failed, FailureMessage = "Disk full." };
        var window = TestHelpers.Show(run);
        Settle(window);
        run.NoteLine.ShouldBe("Run failed. Disk full.");
        run.ActionButton!.Content.ShouldBe("Retry");
        run.Classes.ShouldContain(":failed");
    }

    [AvaloniaFact]
    public void Narrow_Run_Is_Compact()
    {
        var run = new AgentRun { Width = 400, Steps = AgentRunSamples.CompletedSteps() };
        var window = TestHelpers.Show(run);
        Settle(window);
        run.IsCompact.ShouldBeTrue();
    }
}
