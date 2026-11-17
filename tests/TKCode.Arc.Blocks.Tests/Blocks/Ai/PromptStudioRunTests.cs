using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// Prompt studio's runs: the request (raw and rendered text, values, model, effort, case, version), the states from
/// Thinking to Done / Stopped / Failed, the paced reveal, results and cost, one run at a time, the shortcut and the
/// commands, the built-in simulator, Retry, Copy, announcements and the busy state.
/// </summary>
public class PromptStudioRunTests
{
    private static PromptStudioRig Rig(StudioScript script, ManualFrameClock? frames = null, Action<PromptStudio>? configure = null,
                                       ArcMotionLevel level = ArcMotionLevel.Off) =>
        new(studio =>
        {
            studio.RunHandler = script.Handler;
            configure?.Invoke(studio);
        }, level: level, frames: frames ?? new ManualFrameClock());

    #region Request and states

    [AvaloniaFact]
    public void Run_Asks_The_Handler_With_The_Rendered_Prompt()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        PromptRunRequest? started = null;
        studio.RunStarted += (_, e) => started = e.Request;
        studio.Values["agent_sign"] = "Kai";
        studio.Run();
        rig.Pump();
        script.Calls.Count.ShouldBe(1);
        var request = script.Last.Request;
        request.ShouldBeSameAs(started);
        request.Raw.ShouldBe(new PromptTemplate(PromptStudioSamples.SystemV4, PromptStudioSamples.UserV4));
        request.Rendered.User.ShouldBe("Write the reply to the last message from Mira Okafor about ticket NK-3307.");
        request.Rendered.System.ShouldContain("Apply this rule: Damaged parts are replaced free within 60 days.");
        request.Rendered.System.ShouldContain("- Sign as Kai.");
        request.Values["ticket_id"].ShouldBe("NK-3307");
        request.Model!.Id.ShouldBe("atlas-medium");
        request.Effort.ShouldBe(ModelEffort.High);
        request.TestCaseId.ShouldBe("spar");
        request.VersionId.ShouldBe("v4");
    }

    [AvaloniaFact]
    public void An_Empty_Value_Renders_As_Nothing_And_Is_Still_In_The_Values()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        rig.Studio.Run();
        rig.Pump();
        var request = script.Last.Request;
        request.Values["agent_sign"].ShouldBe(string.Empty);
        request.Rendered.System.ShouldEndWith("- Sign as .");
    }

    [AvaloniaFact]
    public void A_Run_Goes_From_Thinking_To_Writing_To_Done()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        var completed = new List<(PromptRunState State, PromptRunResult? Result)>();
        studio.RunCompleted += (_, e) => completed.Add((e.State, e.Result));
        studio.Run();
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        studio.Classes.Contains(PromptStudio.ThinkingPseudoClass).ShouldBeTrue();
        studio.Classes.Contains(PromptStudio.BusyPseudoClass).ShouldBeTrue();
        studio.RunButton!.IsRunning.ShouldBeTrue();
        studio.Output!.State.ShouldBe(PromptRunState.Thinking);
        studio.Output.StatusText.ShouldBe("Thinking");
        studio.Output.CaseText.ShouldBe("Broken spar");
        studio.Output.FooterText.ShouldBe("Atlas Medium · High effort");
        AutomationProperties.GetItemStatus(studio).ShouldBe("Thinking");
        studio.LastAnnouncement.ShouldBe("Running");

        rig.Time.Advance(TimeSpan.FromSeconds(0.8));
        script.Last.Push("Hi Mira,\n\nThanks for the photos. ");
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Streaming);
        studio.Output.StatusText.ShouldBe("Writing");
        AutomationProperties.GetItemStatus(studio).ShouldBe("Writing");

        script.Last.Push("A replacement leaves today.");
        rig.Time.Advance(TimeSpan.FromSeconds(0.7));
        script.Last.Complete();
        rig.Pump();
        // Done only once the reply is all on screen.
        studio.RunState.ShouldBe(PromptRunState.Streaming);
        rig.Reveal();
        studio.RunState.ShouldBe(PromptRunState.Done);
        studio.RunButton.IsRunning.ShouldBeFalse();
        studio.Output.Text.ShouldBe("Hi Mira,\n\nThanks for the photos. A replacement leaves today.");
        studio.Output.StatusText.ShouldBe("Reply");
        studio.LastAnnouncement.ShouldBe("Reply ready");
        AutomationProperties.GetItemStatus(studio).ShouldBeNull();

        var result = studio.LastResult!;
        result.Text.ShouldBe("Hi Mira,\n\nThanks for the photos. A replacement leaves today.");
        result.Elapsed.ShouldBe(TimeSpan.FromSeconds(1.5));
        result.IsSimulated.ShouldBeFalse();
        var rendered = script.Last.Request.Rendered;
        result.InputTokens.ShouldBe(PromptStudioText.EstimateTokens(rendered.System) + PromptStudioText.EstimateTokens(rendered.User));
        result.OutputTokens.ShouldBe(PromptStudioText.EstimateTokens(result.Text));
        result.Cost.ShouldBe(Math.Round((result.InputTokens * 1.5m + result.OutputTokens * 7m) / 1_000_000m, 4));
        studio.Output.FooterText.ShouldBe($"1.50 s · {result.OutputTokens} tokens · ${result.Cost!.Value:0.0000}");
        completed.Count.ShouldBe(1);
        completed[0].State.ShouldBe(PromptRunState.Done);
        completed[0].Result.ShouldBeSameAs(result);
    }

    [AvaloniaFact]
    public void A_Model_Without_Prices_Has_No_Cost()
    {
        var script = new StudioScript();
        using var rig = Rig(script, configure: studio =>
        {
            studio.SelectedModel = new AiModel { Id = "free", Name = "Free" };
        });
        var studio = rig.Studio;
        studio.Run();
        rig.Pump();
        script.Last.Push("Done.");
        script.Last.Complete();
        rig.Pump();
        rig.Reveal();
        studio.LastResult!.Cost.ShouldBeNull();
        studio.Output!.FooterText.ShouldBe("0.00 s · 2 tokens");
        // A model without effort levels has no effort in the footer.
        script.Last.Request.Model!.Id.ShouldBe("free");
    }

    [AvaloniaFact]
    public void Stop_Keeps_What_Was_Shown()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        PromptRunCompletedEventArgs? completed = null;
        studio.RunCompleted += (_, e) => completed = e;
        studio.Run();
        rig.Pump();
        script.Last.Push(PromptStudioSamples.ReplyFor("spar"));
        rig.Pump();
        rig.Frame(6);
        var shown = studio.Output!.Text;
        shown.Length.ShouldBeGreaterThan(0);
        shown.Length.ShouldBeLessThan(PromptStudioSamples.ReplyFor("spar").Length);
        studio.Stop();
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Stopped);
        script.Last.Token.IsCancellationRequested.ShouldBeTrue();
        studio.Output.State.ShouldBe(PromptRunState.Stopped);
        studio.Output.StatusText.ShouldBe("Stopped");
        studio.Output.Text.ShouldBe(shown);
        // The footer keeps the model line of the run.
        studio.Output.FooterText.ShouldBe("Atlas Medium · High effort");
        completed!.State.ShouldBe(PromptRunState.Stopped);
        completed.Result!.Text.ShouldBe(shown);
        studio.LastAnnouncement.ShouldBe("Run stopped");
        rig.Frame(60);
        studio.Output.Text.ShouldBe(shown);
    }

    [AvaloniaFact]
    public void The_Run_Button_Runs_And_Stops()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        rig.Click(studio.RunButton!);
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        AutomationProperties.GetName(studio.RunButton!).ShouldBe("Stop run");
        rig.Click(studio.RunButton!);
        studio.RunState.ShouldBe(PromptRunState.Stopped);
        script.Calls.Count.ShouldBe(1);
        AutomationProperties.GetName(studio.RunButton!).ShouldBe("Run prompt");
    }

    [AvaloniaFact]
    public void A_Failure_Shows_The_Message_The_Host_Words_And_Retry()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        Exception? thrown = null;
        studio.RunFailed += (_, e) =>
        {
            thrown = e.Exception;
            e.Message = "The model is busy. Try again.";
        };
        PromptRunCompletedEventArgs? completed = null;
        studio.RunCompleted += (_, e) => completed = e;
        studio.Run();
        rig.Pump();
        var error = new InvalidOperationException("503");
        script.Last.Fail(error);
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Failed);
        thrown.ShouldBeSameAs(error);
        studio.Output!.State.ShouldBe(PromptRunState.Failed);
        studio.Output.StatusText.ShouldBe("Run failed");
        studio.Output.ErrorText.ShouldBe("The model is busy. Try again.");
        studio.Output.FooterText.ShouldBe("Atlas Medium");
        studio.LastAnnouncement.ShouldBe("Run failed: The model is busy. Try again.");
        completed!.State.ShouldBe(PromptRunState.Failed);
        completed.Result.ShouldBeNull();
        studio.LastResult.ShouldBeNull();
        studio.Classes.Contains(PromptStudio.FailedPseudoClass).ShouldBeTrue();

        // Retry runs again and the focus moves to the Run button (now Stop), not to the page.
        var retry = studio.Output.RetryButton!;
        retry.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        script.Calls.Count.ShouldBe(2);
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        rig.FocusIsIn(studio.RunButton!).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Failure_After_Some_Text_Keeps_It()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        studio.Run();
        rig.Pump();
        script.Last.Push("Hi Mira, thanks for writing. ");
        rig.Pump();
        rig.Frame(20);
        script.Last.Fail(new Exception("lost"));
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Failed);
        studio.Output!.Text.TrimEnd().ShouldBe("Hi Mira, thanks for writing.");
        studio.LastResult!.Text.TrimEnd().ShouldBe("Hi Mira, thanks for writing.");
        studio.Output.ErrorText.ShouldBe("lost");
    }

    [AvaloniaFact]
    public void Only_One_Run_At_A_Time()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        var completed = new List<PromptRunState>();
        studio.RunCompleted += (_, e) => completed.Add(e.State);
        studio.Run();
        rig.Pump();
        var first = script.Last;
        script.Last.Push("First ");
        rig.Pump();
        studio.Run();
        rig.Pump();
        script.Calls.Count.ShouldBe(2);
        first.Token.IsCancellationRequested.ShouldBeTrue();
        completed.ShouldBe([PromptRunState.Stopped]);
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        // The old run's late text goes nowhere.
        first.Push("late ");
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        script.Last.Push("Second.");
        script.Last.Complete();
        rig.Pump();
        rig.Reveal();
        studio.Output!.Text.ShouldBe("Second.");
        completed.ShouldBe([PromptRunState.Stopped, PromptRunState.Done]);
    }

    #endregion

    #region Shortcut and commands

    [AvaloniaTheory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Meta)]
    public void Mod_Enter_Runs_From_The_Editor_And_A_Field_But_Does_Not_Restart(RawInputModifiers modifier)
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        var editor = studio.SystemEditor!;
        editor.Focus();
        var text = editor.Text;
        rig.Key(PhysicalKey.Enter, modifier);
        script.Calls.Count.ShouldBe(1);
        editor.Text.ShouldBe(text);
        studio.Fields!.FocusField("ticket_id");
        rig.Key(PhysicalKey.Enter, modifier);
        script.Calls.Count.ShouldBe(1);
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        studio.Stop();
        rig.Key(PhysicalKey.Enter, modifier);
        script.Calls.Count.ShouldBe(2);
        studio.Cases!.FocusSelected();
        studio.Stop();
        rig.Key(PhysicalKey.Enter, modifier);
        script.Calls.Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public void Plain_Enter_In_The_Editor_Adds_A_Line()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var editor = rig.Studio.SystemEditor!;
        editor.Focus();
        editor.Select(0, 0);
        rig.Key(PhysicalKey.Enter);
        script.Calls.ShouldBeEmpty();
        editor.Text.ShouldStartWith("\n");
    }

    [AvaloniaFact]
    public void The_Commands_Run_And_Stop()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        studio.RunCommand.CanExecute(null).ShouldBeTrue();
        studio.StopCommand.CanExecute(null).ShouldBeFalse();
        studio.RunCommand.Execute(null);
        rig.Pump();
        script.Calls.Count.ShouldBe(1);
        studio.RunCommand.CanExecute(null).ShouldBeFalse();
        studio.StopCommand.CanExecute(null).ShouldBeTrue();
        // Run does not restart a run from a toolbar.
        studio.RunCommand.Execute(null);
        script.Calls.Count.ShouldBe(1);
        studio.StopCommand.Execute(null);
        studio.RunState.ShouldBe(PromptRunState.Stopped);
        studio.RunCommand.CanExecute(null).ShouldBeTrue();
    }

    #endregion

    #region Simulator

    [AvaloniaFact]
    public void Without_A_Handler_The_Simulator_Thinks_By_Effort_And_Says_So()
    {
        var waits = new List<TimeSpan>();
        var frames = new ManualFrameClock();
        using var rig = new PromptStudioRig(frames: frames);
        var studio = rig.Studio;
        var gates = new List<TaskCompletionSource>();
        studio.Delay = (time, _) =>
        {
            waits.Add(time);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            gates.Add(gate);
            return gate.Task;
        };
        studio.Output!.IdleText.ShouldBe("Run Broken spar to see the reply here. Runs are simulated.");
        studio.Run();
        rig.Pump();
        waits.ShouldBe([TimeSpan.FromMilliseconds(799)]);
        studio.Output.FooterText.ShouldBe("Simulated · Atlas Medium · High effort");
        studio.RunState.ShouldBe(PromptRunState.Thinking);
        // Let every wait pass at once from here.
        studio.Delay = (time, _) =>
        {
            waits.Add(time);
            return Task.CompletedTask;
        };
        gates[0].SetResult();
        rig.Pump();
        rig.Reveal();
        studio.RunState.ShouldBe(PromptRunState.Done);
        studio.Output.Text.ShouldBe(studio.Labels.SimulatedReply);
        studio.LastResult!.IsSimulated.ShouldBeTrue();
        studio.Output.FooterText!.ShouldStartWith("Simulated · ");
        waits.Skip(1).ShouldAllBe(wait => wait == TimeSpan.FromMilliseconds(50));
    }

    [AvaloniaTheory]
    [InlineData(ModelEffort.Low, 500)]
    [InlineData(ModelEffort.Medium, 634)]
    [InlineData(ModelEffort.ExtraHigh, 982)]
    [InlineData(ModelEffort.Max, 1166)]
    public void The_Simulator_Thinks_Longer_At_Higher_Effort(ModelEffort effort, int milliseconds)
    {
        var waits = new List<TimeSpan>();
        using var rig = new PromptStudioRig(frames: new ManualFrameClock());
        var studio = rig.Studio;
        studio.Delay = (time, _) =>
        {
            waits.Add(time);
            return new TaskCompletionSource().Task;
        };
        studio.Effort = effort;
        studio.Run();
        rig.Pump();
        waits.ShouldBe([TimeSpan.FromMilliseconds(milliseconds)]);
    }

    [AvaloniaFact]
    public void A_Handler_Removes_The_Simulated_Words()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        rig.Studio.Output!.IdleText.ShouldBe("Run Broken spar to see the reply here.");
        rig.Studio.SelectedTestCase = rig.Studio.TestCases[2];
        rig.Studio.Output.IdleText.ShouldBe("Run Out of policy to see the reply here.");
        rig.Studio.Output.CaseText.ShouldBe("Out of policy");
    }

    [AvaloniaFact]
    public void The_Simulated_Reply_Is_Cut_In_Whole_Words()
    {
        PromptStudio.Chunks("one two  three\n\nfour five", 2).ShouldBe(["one two  ", "three\n\nfour ", "five"]);
        string.Concat(PromptStudio.Chunks(new PromptStudioLabels().SimulatedReply, 5)).ShouldBe(new PromptStudioLabels().SimulatedReply);
    }

    [AvaloniaFact]
    public void The_Sample_Handler_Fails_Once_On_The_Out_Of_Policy_Case()
    {
        using var rig = new PromptStudioRig(studio =>
        {
            studio.RunHandler = PromptStudioSamples.ScriptedHandler(static (_, _) => Task.CompletedTask);
        }, frames: new ManualFrameClock());
        var studio = rig.Studio;
        studio.SelectedTestCase = studio.TestCases[2];
        studio.Run();
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Failed);
        studio.Output!.ErrorText.ShouldBe(PromptStudioSamples.BusyError);
        studio.Retry();
        rig.Pump();
        rig.Reveal();
        studio.RunState.ShouldBe(PromptRunState.Done);
        studio.Output.Text.ShouldBe(PromptStudioSamples.ReplyFor("policy"));
    }

    #endregion

    #region Copy, lifetime

    [AvaloniaFact]
    public void A_Copied_Reply_Is_Announced()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        rig.Studio.Output!.RaiseEvent(new RoutedEventArgs(PromptRunPanel.CopyRequestedEvent, rig.Studio.Output));
        rig.Studio.LastAnnouncement.ShouldBe("Reply copied");
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Stops_The_Run()
    {
        var script = new StudioScript();
        using var rig = Rig(script);
        var studio = rig.Studio;
        studio.Run();
        rig.Pump();
        rig.Root.Child = null;
        rig.Pump();
        studio.RunState.ShouldBe(PromptRunState.Stopped);
        script.Last.Token.IsCancellationRequested.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_Run_Without_A_Window_Finishes_When_The_Text_Ends()
    {
        var script = new StudioScript();
        var studio = PromptStudioSamples.Studio(s => s.RunHandler = script.Handler);
        studio.Run();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        script.Last.Push("Short.");
        script.Last.Complete();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        studio.RunState.ShouldBe(PromptRunState.Done);
        studio.LastResult!.Text.ShouldBe("Short.");
    }

    #endregion
}
