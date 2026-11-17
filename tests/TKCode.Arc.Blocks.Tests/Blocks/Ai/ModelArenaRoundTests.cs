using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>A round of the model arena: phases, statuses, the shared cadence, metrics, stop, failure and retry.</summary>
public class ModelArenaRoundTests
{
    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    [AvaloniaFact]
    public void A_Round_Goes_From_Thinking_To_Writing_To_Finished_First_To_The_Vote()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var started = new List<ArenaRoundEventArgs>();
        arena.RoundStarted += (_, e) => started.Add(e);
        Assert.Equal(ArenaPhase.Idle, arena.Phase);
        Assert.Equal(0, arena.Round);
        Assert.Equal(ArenaVoteKind.Hidden, arena.VoteBar!.Kind);

        var task = rig.Run();

        Assert.Equal(1, arena.Round);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneA.Status);
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneB.Status);
        Assert.Equal(ArenaVoteKind.Pending, arena.VoteBar.Kind);
        Assert.Equal("0 of 2 finished", arena.VoteBar.PendingCountText);
        Assert.True(arena.Form!.IsRunning);
        var round = Assert.Single(started);
        Assert.Equal(1, round.Round);
        Assert.Equal("Lumen 3 Pro", round.ModelA.Name);
        Assert.Equal("Kite 5", round.ModelB.Name);
        Assert.Equal(ModelArenaSamples.Prompts()[0].Text, round.Prompt);
        Assert.Equal(2, rig.Script.Calls.Count);
        Assert.Equal(round.Prompt, rig.A.Prompt);

        rig.Push(rig.A, "The newest row wins. ");
        rig.Frame();
        Assert.Equal(ArenaPaneStatus.Writing, arena.PaneA.Status);
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneB.Status);
        Assert.StartsWith("The newest row", arena.PaneA.Text);

        rig.Complete(rig.A);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(ArenaPaneStatus.FinishedFirst, arena.PaneA.Status);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.Equal("1 of 2 finished", arena.VoteBar.PendingCountText);
        Assert.Equal("Model A finished first.", arena.LastAnnouncement);
        Assert.False(task.IsCompleted);

        rig.Push(rig.B, "A self join works too.");
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(ArenaPaneStatus.Done, arena.PaneB.Status);
        Assert.Equal(ArenaPhase.Voting, arena.Phase);
        Assert.Equal(ArenaVoteKind.Vote, arena.VoteBar.Kind);
        Assert.Equal("Both answers are in. Vote to see the models.", arena.LastAnnouncement);
        Assert.False(arena.Form.IsRunning);
        Assert.True(task.IsCompleted);
        Assert.Equal("A self join works too.", arena.PaneB.Text);
    }

    [AvaloniaFact]
    public void The_Vote_Row_Replaces_The_Waiting_Row_About_116ms_After_The_Last_Answer()
    {
        using var rig = new ModelArenaRig();
        _ = rig.Run();
        rig.Push(rig.A, "One two three.");
        rig.Complete(rig.A);
        rig.Advance(TimeSpan.FromMilliseconds(200));
        rig.Push(rig.B, "Four.");
        rig.Complete(rig.B);
        rig.Frame();
        Assert.Equal(ArenaPhase.Voting, rig.Arena.Phase);
        Assert.True(rig.Arena.IsVoteRowPending);
        Assert.Equal(ArenaVoteKind.Pending, rig.Arena.VoteBar!.Kind);
        Assert.Equal("2 of 2 finished", rig.Arena.VoteBar.PendingCountText);

        rig.Frame(5);
        Assert.Equal(ArenaVoteKind.Pending, rig.Arena.VoteBar.Kind);
        rig.Frame(3);
        Assert.False(rig.Arena.IsVoteRowPending);
        Assert.Equal(ArenaVoteKind.Vote, rig.Arena.VoteBar.Kind);
    }

    [AvaloniaFact]
    public void Both_Answers_Keep_One_Cadence_Whatever_Their_Streams_Do()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        // A gets its whole answer in one piece, B in small pieces now and then: the panes still show the same words per step.
        rig.Push(rig.A, ModelArenaSamples.Words(40, "alpha") + " ");
        rig.Push(rig.B, ModelArenaSamples.Words(12, "beta") + " ");
        var shownA = new List<int>();
        var shownB = new List<int>();
        for (var frame = 0; frame < 18; frame++)
        {
            if (frame % 3 == 2)
            {
                rig.Push(rig.B, ModelArenaSamples.Words(6, "more") + " ");
            }
            rig.Frame();
            shownA.Add(WordCount(arena.PaneA.Text));
            shownB.Add(WordCount(arena.PaneB.Text));
        }
        Assert.Equal(shownA, shownB);
        Assert.True(shownA[^1] >= 20, $"shown {shownA[^1]}");
        // Releases follow the 34ms clock: never two in a row at 60 Hz.
        for (var i = 1; i < shownA.Count; i++)
        {
            if (shownA[i] != shownA[i - 1])
            {
                Assert.True(i < 2 || shownA[i - 1] == shownA[i - 2], $"frames {i - 1} and {i} both released");
            }
        }
    }

    [AvaloniaFact]
    public void A_Late_Frame_Releases_More_Words_On_Both_Sides_Alike()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.Push(rig.A, ModelArenaSamples.Words(60, "a") + " ");
        rig.Push(rig.B, ModelArenaSamples.Words(60, "b") + " ");
        rig.Frame();
        var before = WordCount(arena.PaneA.Text);
        // One frame that took 200ms: the pacers are fed the clock's time, not a frame count.
        rig.Time.Advance(TimeSpan.FromMilliseconds(200));
        rig.Frames.Advance(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
        Dispatcher.UIThread.RunJobs();
        var after = WordCount(arena.PaneA.Text);
        Assert.Equal(after, WordCount(arena.PaneB.Text));
        Assert.True(after - before >= 8, $"released {after - before}");
    }

    [AvaloniaFact]
    public void An_Answer_Is_Complete_Only_When_Its_Stream_Ended_And_Its_Words_Are_Shown()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.Push(rig.A, ModelArenaSamples.Words(30) + " ");
        rig.Frame();
        Assert.Equal(ArenaPaneStatus.Writing, arena.PaneA.Status);
        rig.Advance(TimeSpan.FromMilliseconds(600));
        // Everything shown, but the stream has not ended.
        Assert.Equal(30, WordCount(arena.PaneA.Text));
        Assert.Equal(ArenaPaneStatus.Writing, arena.PaneA.Status);
        rig.Push(rig.A, "last words here");
        rig.Complete(rig.A);
        Assert.Equal(ArenaPaneStatus.Writing, arena.PaneA.Status);
        rig.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(ArenaPaneStatus.FinishedFirst, arena.PaneA.Status);
        Assert.EndsWith("last words here", arena.PaneA.Text);
    }

    [AvaloniaFact]
    public void Finished_First_Goes_To_The_First_Complete_Answer_And_Both_Are_Done_When_They_Finish_Together()
    {
        using var rig = new ModelArenaRig();
        _ = rig.Run();
        rig.Push(rig.B, "Short.");
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(ArenaPaneStatus.FinishedFirst, rig.Arena.PaneB.Status);
        Assert.Equal("Model B finished first.", rig.Arena.LastAnnouncement);

        _ = rig.Run();
        rig.Push(rig.A, "Same.");
        rig.Push(rig.B, "Same.");
        rig.Complete(rig.A);
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(ArenaPaneStatus.Done, rig.Arena.PaneA.Status);
        Assert.Equal(ArenaPaneStatus.Done, rig.Arena.PaneB.Status);
        Assert.Equal(ArenaPhase.Voting, rig.Arena.Phase);
    }

    [AvaloniaFact]
    public void The_First_Token_Timer_Runs_While_Thinking_And_Stops_On_The_Measured_Value()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        // The pane shows the running reading (Elapsed); the metrics have no first token until it arrives.
        Assert.Null(arena.PaneA.Metrics.TimeToFirstToken);
        Assert.Equal(TimeSpan.Zero, arena.PaneViewA!.Elapsed);
        Assert.True(arena.PaneViewA.IsTiming);
        Assert.True(arena.PaneViewB!.IsTiming);
        Assert.Null(arena.PaneA.Metrics.TokensPerSecond);

        rig.Frame(4);
        Assert.Equal(TimeSpan.Zero, arena.PaneViewA.Elapsed);
        rig.Frame(3);
        Assert.InRange(arena.PaneViewA.Elapsed!.Value.TotalMilliseconds, 100, 120);
        rig.Frame(6);
        Assert.InRange(arena.PaneViewA.Elapsed!.Value.TotalMilliseconds, 200, 220);
        Assert.Null(arena.PaneA.Metrics.TimeToFirstToken);

        // 0.5 s after the start the first text arrives.
        rig.Time.Advance(TimeSpan.FromMilliseconds(500) - TimeSpan.FromTicks(ModelArenaRig.FrameStep.Ticks * 13));
        rig.Push(rig.A, "First words. ");
        Assert.Equal(TimeSpan.FromMilliseconds(500), arena.PaneA.Metrics.TimeToFirstToken);
        Assert.False(arena.PaneViewA.IsTiming);
        Assert.Null(arena.PaneViewA.Elapsed);
        Assert.True(arena.PaneViewB.IsTiming);
        rig.Frame(10);
        Assert.Equal(TimeSpan.FromMilliseconds(500), arena.PaneA.Metrics.TimeToFirstToken);
    }

    [AvaloniaFact]
    public void Speed_And_Cost_Are_Measured_On_The_Raw_Stream_And_Sampled_Every_500ms()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        var prompt = rig.A.Prompt;
        var input = (prompt.Length + 3) / 4;
        // Lumen 3 Pro: $4 in and $18 out per million tokens. While thinking only the input costs.
        Assert.Equal(input, arena.PaneA.Metrics.InputTokens);
        Assert.Equal(input * 4m / 1_000_000m, arena.PaneA.Metrics.CostUsd);
        Assert.True(arena.PaneA.Metrics.IsEstimated);

        rig.Push(rig.A, new string('x', 40) + " ");
        rig.Time.Advance(TimeSpan.FromMilliseconds(250));
        rig.Push(rig.A, new string('y', 39) + " ");
        // Not sampled yet: no speed, the input cost only.
        Assert.Null(arena.PaneA.Metrics.TokensPerSecond);
        Assert.Equal(input * 4m / 1_000_000m, arena.PaneA.Metrics.CostUsd);

        rig.Advance(TimeSpan.FromMilliseconds(300));
        var metrics = arena.PaneA.Metrics;
        // 81 characters = 21 tokens over the 250ms from the first text to the last.
        Assert.Equal(21, metrics.OutputTokens);
        Assert.Equal(84, metrics.TokensPerSecond!.Value, 3);
        Assert.Equal((input * 4m + 21 * 18m) / 1_000_000m, metrics.CostUsd);
        Assert.Null(metrics.Total);

        // Counts reported by the runner replace the estimates.
        rig.A.Push(new ArenaStreamEvent(InputTokens: 120, OutputTokens: 30));
        Dispatcher.UIThread.RunJobs();
        rig.Complete(rig.A);
        metrics = arena.PaneA.Metrics;
        Assert.Equal(120, metrics.InputTokens);
        Assert.Equal(30, metrics.OutputTokens);
        Assert.False(metrics.IsEstimated);
        Assert.Equal((120 * 4m + 30 * 18m) / 1_000_000m, metrics.CostUsd);
        Assert.NotNull(metrics.Total);
    }

    [AvaloniaFact]
    public void A_Model_Without_A_Price_Costs_Nothing()
    {
        using var rig = new ModelArenaRig(arena =>
        {
            arena.Contestants[0].InputPrice = null;
            arena.Contestants[0].OutputPrice = null;
        });
        _ = rig.Run();
        rig.Push(rig.A, "Some text here. ");
        rig.Complete(rig.A);
        Assert.Equal(0m, rig.Arena.PaneA.Metrics.CostUsd);
        Assert.True(rig.Arena.PaneB.Metrics.CostUsd > 0);
    }

    [AvaloniaFact]
    public void Stop_Ends_Both_Answers_As_Stopped_And_Keeps_A_Complete_One()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var stopped = new List<ArenaRoundEventArgs>();
        arena.RunStopped += (_, e) => stopped.Add(e);
        var task = rig.Run();
        rig.Push(rig.A, "Half an answer ");
        rig.Frame(3);
        var callA = rig.A;
        var callB = rig.B;

        arena.Stop();
        rig.Pump();

        Assert.Equal(ArenaPhase.Stopped, arena.Phase);
        Assert.Equal(ArenaPaneStatus.Stopped, arena.PaneA.Status);
        Assert.Equal(ArenaPaneStatus.Stopped, arena.PaneB.Status);
        Assert.Equal("Half an answer ", arena.PaneA.Text);
        Assert.Equal(ArenaVoteKind.Stopped, arena.VoteBar!.Kind);
        Assert.Equal("Round stopped.", arena.LastAnnouncement);
        Assert.Single(stopped);
        Assert.True(callA.Token.IsCancellationRequested);
        Assert.True(callB.WasCancelled);
        Assert.True(task.IsCompleted);
        Assert.False(arena.Form!.IsRunning);
        Assert.False(arena.PaneViewA!.IsTiming);
        Assert.False(arena.PaneViewB!.IsTiming);

        // A complete answer stays complete; the round is still stopped.
        _ = rig.Run();
        rig.Push(rig.A, "Done early.");
        rig.Complete(rig.A);
        rig.Advance(TimeSpan.FromMilliseconds(100));
        arena.Stop();
        Assert.Equal(ArenaPaneStatus.FinishedFirst, arena.PaneA.Status);
        Assert.Equal(ArenaPaneStatus.Stopped, arena.PaneB.Status);
        Assert.Equal(ArenaPhase.Stopped, arena.Phase);
    }

    [AvaloniaFact]
    public void Text_That_Arrives_After_Stop_Is_Ignored()
    {
        using var rig = new ModelArenaRig();
        _ = rig.Run();
        var call = rig.A;
        rig.Push(call, "Kept ");
        rig.Frame(2);
        rig.Arena.Stop();
        call.Push("dropped");
        Dispatcher.UIThread.RunJobs();
        rig.Frame(10);
        Assert.Equal("Kept ", rig.Arena.PaneA.Text);
        Assert.Equal(ArenaPaneStatus.Stopped, rig.Arena.PaneA.Status);
    }

    [AvaloniaFact]
    public void A_New_Run_While_Running_Replaces_The_Round_With_The_Same_Pair()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var stopped = 0;
        arena.RunStopped += (_, _) => stopped++;
        var first = rig.Run();
        var oldA = rig.A;
        var pair = arena.CurrentPair;
        _ = rig.Run("Another question");
        Assert.True(first.IsCompleted);
        Assert.True(oldA.Token.IsCancellationRequested);
        Assert.Equal(1, stopped);
        Assert.Equal(2, arena.Round);
        Assert.Equal(pair, arena.CurrentPair);
        Assert.Equal("Another question", arena.PromptText);
        Assert.Equal("Another question", rig.A.Prompt);
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneA.Status);
        Assert.NotEqual("Round stopped.", arena.LastAnnouncement);
    }

    [AvaloniaFact]
    public void A_Failed_Answer_Shows_The_Runner_Error_While_The_Other_Goes_On_And_The_Round_Fails()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var failed = new List<ArenaPaneFailedEventArgs>();
        arena.PaneFailed += (_, e) => failed.Add(e);
        var task = rig.Run();
        rig.Push(rig.A, "Thank you for ");
        rig.Frame(3);
        rig.A.Fail(new ArenaRunException("The connection closed early.", "Stream interrupted"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ArenaPaneStatus.Failed, arena.PaneA.Status);
        Assert.Equal("Stream interrupted", arena.PaneA.ErrorTitle);
        Assert.Equal("The connection closed early.", arena.PaneA.ErrorText);
        Assert.True(arena.PaneA.IsRetryable);
        Assert.Equal("Thank you for ", arena.PaneA.Text);
        Assert.Equal("Model A failed.", arena.LastAnnouncement);
        var args = Assert.Single(failed);
        Assert.Equal(ArenaSlot.A, args.Slot);
        Assert.Equal("Lumen 3 Pro", args.Model.Name);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.False(task.IsCompleted);

        rig.Push(rig.B, "Fine answer.");
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(ArenaPaneStatus.FinishedFirst, arena.PaneB.Status);
        Assert.Equal(ArenaPhase.Failed, arena.Phase);
        Assert.Equal(ArenaVoteKind.Failed, arena.VoteBar!.Kind);
        Assert.Equal("Model A failed. Retry it or move on.", arena.VoteBar.FailedText);
        Assert.True(task.IsCompleted);
    }

    [AvaloniaFact]
    public void Retry_Runs_Only_The_Failed_Answer_Again_And_The_Vote_Opens_After()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.A.Fail(new InvalidOperationException("socket"));
        Dispatcher.UIThread.RunJobs();
        // Not an ArenaRunException: the labels' words, and it can be retried.
        Assert.Equal("Connection dropped", arena.PaneA.ErrorTitle);
        Assert.Equal("The stream ended before this answer was complete.", arena.PaneA.ErrorText);
        Assert.True(arena.PaneA.IsRetryable);
        rig.Push(rig.B, "B answer.");
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(ArenaPhase.Failed, arena.Phase);
        var calls = rig.Script.Calls.Count;
        var pair = arena.CurrentPair;

        arena.PaneViewA!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(ArenaPane.ErrorActionRequestedEvent, arena.PaneViewA));
        rig.Pump();

        Assert.Equal(calls + 1, rig.Script.Calls.Count);
        Assert.Same(arena.PaneA.Model, rig.Script.Calls[^1].Model);
        Assert.Equal(1, arena.Round);
        Assert.Equal(pair, arena.CurrentPair);
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneA.Status);
        Assert.Null(arena.PaneA.ErrorTitle);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.Equal(ArenaVoteKind.Pending, arena.VoteBar!.Kind);
        Assert.Equal("1 of 2 finished", arena.VoteBar.PendingCountText);
        Assert.Equal(ArenaPaneStatus.FinishedFirst, arena.PaneB.Status);

        rig.Push(rig.A, "Second try.");
        rig.Complete(rig.A);
        rig.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(ArenaPaneStatus.Done, arena.PaneA.Status);
        Assert.Equal(ArenaPhase.Voting, arena.Phase);
    }

    [AvaloniaFact]
    public void A_Failure_That_Cannot_Be_Retried_Offers_The_First_Sample_Prompt_Without_Running_It()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run("Something the demo does not know");
        rig.A.Fail(new ArenaRunException("Connect a runner to stream real models.", "No demo answer", isRetryable: false));
        rig.B.Fail(new ArenaRunException("Connect a runner to stream real models.", "No demo answer", isRetryable: false));
        Dispatcher.UIThread.RunJobs();
        Assert.False(arena.PaneA.IsRetryable);
        Assert.Equal(ArenaPhase.Failed, arena.Phase);
        Assert.Equal("Both models failed.", arena.LastAnnouncement);
        Assert.Equal("Both models failed. Retry it or move on.", arena.VoteBar!.FailedText);
        var calls = rig.Script.Calls.Count;

        arena.PaneViewB!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(ArenaPane.ErrorActionRequestedEvent, arena.PaneViewB));
        rig.Pump();

        Assert.Equal(ModelArenaSamples.Prompts()[0].Text, arena.PromptText);
        Assert.Equal(calls, rig.Script.Calls.Count);
        Assert.Equal(1, arena.Round);
        Assert.True(rig.FocusIsIn(arena.Form!.RunButton));
    }

    [AvaloniaFact]
    public void A_Failure_Shows_Every_Word_That_Arrived_Before_It()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.Push(rig.A, ModelArenaSamples.Words(30) + " and half");
        rig.Frame();
        Assert.True(WordCount(arena.PaneA.Text) < 30);
        rig.A.Fail(new ArenaRunException("Gone."));
        Dispatcher.UIThread.RunJobs();
        // The words still in the pacer show before the card (a failed pane takes no more text), the unfinished one too.
        Assert.Equal(ArenaPaneStatus.Failed, arena.PaneA.Status);
        Assert.Equal(ModelArenaSamples.Words(30) + " and half", arena.PaneA.Text);
    }

    [AvaloniaFact]
    public void A_Retry_Keeps_The_Failed_Text_Until_The_New_First_Words()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.Push(rig.A, "Old partial answer");
        rig.A.Fail(new ArenaRunException("Gone."));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Old partial answer", arena.PaneA.Text);

        arena.Retry(ArenaSlot.A);
        rig.Pump();
        Assert.Equal(ArenaPaneStatus.Thinking, arena.PaneA.Status);
        Assert.Equal("Old partial answer", arena.PaneA.Text);
        Assert.True(arena.PaneViewA!.IsTiming);
        Assert.Null(arena.PaneA.Metrics.TimeToFirstToken);
        rig.Frame(6);
        Assert.Equal("Old partial answer", arena.PaneA.Text);

        rig.Push(rig.A, "Fresh words here. ");
        rig.Frame();
        Assert.Equal(ArenaPaneStatus.Writing, arena.PaneA.Status);
        Assert.Equal("Fresh words here. ", arena.PaneA.Text);
    }

    [AvaloniaFact]
    public void A_Runner_That_Cancels_By_Itself_Stops_The_Answer()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run();
        rig.A.Fail(new OperationCanceledException());
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ArenaPaneStatus.Stopped, arena.PaneA.Status);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        rig.Push(rig.B, "B.");
        rig.Complete(rig.B);
        rig.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(ArenaPhase.Stopped, arena.Phase);
        Assert.Equal(ArenaVoteKind.Stopped, arena.VoteBar!.Kind);
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Cancels_Both_Streams_And_Stops_The_Clock()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var task = rig.Run();
        var callA = rig.A;
        var callB = rig.B;
        rig.Push(callA, "Partial ");
        rig.Frame(2);
        Assert.True(arena.Loop.IsActive);

        rig.Host.Child = null;
        rig.Pump();

        Assert.True(callA.WasCancelled || callA.Token.IsCancellationRequested);
        Assert.True(callB.WasCancelled);
        Assert.Equal(ArenaPhase.Stopped, arena.Phase);
        Assert.Equal(ArenaPaneStatus.Stopped, arena.PaneA.Status);
        Assert.False(arena.Loop.IsActive);
        Assert.True(task.IsCompleted);
    }

    [AvaloniaFact]
    public void Nothing_Runs_Without_A_Runner_Two_Contestants_Or_A_Prompt()
    {
        using (var rig = new ModelArenaRig(withRunner: false))
        {
            _ = rig.Run();
            Assert.Equal(0, rig.Arena.Round);
            Assert.False(rig.Arena.Form!.CanRun);
            Assert.False(rig.Arena.Form.RunButton!.IsEnabled);
        }
        using (var rig = new ModelArenaRig(arena =>
               {
                   while (arena.Contestants.Count > 1)
                   {
                       arena.Contestants.RemoveAt(1);
                   }
               }))
        {
            _ = rig.Run();
            Assert.Equal(0, rig.Arena.Round);
            Assert.False(rig.Arena.Form!.CanRun);
        }
        using (var rig = new ModelArenaRig())
        {
            rig.Arena.PromptText = "   ";
            rig.Pump();
            _ = rig.Run();
            Assert.Equal(0, rig.Arena.Round);
            Assert.True(rig.Arena.Form!.CanRun);
            Assert.False(rig.Arena.Form.RunButton!.IsEnabled);
            Assert.Empty(rig.Script.Calls);
        }
    }

    [AvaloniaFact]
    public void The_Box_Is_Prefilled_With_The_First_Sample_Prompt_Once()
    {
        using var rig = new ModelArenaRig();
        Assert.Equal(ModelArenaSamples.Prompts()[0].Text, rig.Arena.PromptText);
        Assert.True(rig.Arena.Form!.Chips[0].IsChecked);
        rig.Arena.PromptText = string.Empty;
        rig.Arena.Prompts.Add(new ArenaPrompt { Id = "x", Label = "X", Text = "Extra" });
        Assert.Equal(string.Empty, rig.Arena.PromptText);

        using var own = new ModelArenaRig(arena => arena.PromptText = "Mine");
        Assert.Equal("Mine", own.Arena.PromptText);
    }

    [AvaloniaFact]
    public void Auto_Start_Runs_The_First_Round_With_The_First_Two_Contestants_Once_Shown()
    {
        using var rig = new ModelArenaRig(arena => arena.AutoStart = true);
        Assert.Equal(1, rig.Arena.Round);
        Assert.Equal(ArenaPhase.Running, rig.Arena.Phase);
        Assert.Equal("Lumen 3 Pro", rig.Arena.PaneA.Model!.Name);
        Assert.Equal("Kite 5", rig.Arena.PaneB.Model!.Name);
        Assert.Equal(2, rig.Script.Calls.Count);

        using var off = new ModelArenaRig();
        Assert.Equal(0, off.Arena.Round);

        using var late = new ModelArenaRig(arena => arena.AutoStart = true, withRunner: false);
        Assert.Equal(0, late.Arena.Round);
        late.Arena.Runner = late.Script;
        late.Pump();
        Assert.Equal(1, late.Arena.Round);
    }

    [AvaloniaFact]
    public void Next_Round_Draws_A_New_Pair_Puts_The_Next_Sample_Prompt_In_The_Box_And_Runs()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        rig.RoundToVote();
        var first = arena.CurrentPair!.Value;
        arena.Vote(ArenaChoice.A);
        rig.Pump();

        arena.NextRound();
        rig.Pump();

        Assert.Equal(2, arena.Round);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        Assert.Equal(ModelArenaSamples.Prompts()[1].Text, arena.PromptText);
        Assert.Equal(arena.PromptText, rig.A.Prompt);
        var second = arena.CurrentPair!.Value;
        Assert.False(ReferenceEquals(first.A, second.A) && ReferenceEquals(first.B, second.B) ||
                     ReferenceEquals(first.A, second.B) && ReferenceEquals(first.B, second.A));
        Assert.NotSame(second.A, second.B);

        // Run keeps the pair.
        _ = rig.Run();
        Assert.Equal(second, arena.CurrentPair);
    }

    [AvaloniaFact]
    public void A_Pair_Picker_Chooses_Every_New_Pair()
    {
        var picker = new FixedPairPicker(3, 5);
        using var rig = new ModelArenaRig(arena => arena.PairPicker = picker);
        _ = rig.Run();
        Assert.Equal("Basalt 2 Ultra", rig.Arena.PaneA.Model!.Name);
        Assert.Equal("Tern 1.2 Flash", rig.Arena.PaneB.Model!.Name);
        Assert.Null(Assert.Single(picker.Previous));
        rig.Arena.NextRound();
        Assert.Equal(2, picker.Previous.Count);
        Assert.Equal("Basalt 2 Ultra", picker.Previous[1]!.Value.A.Name);
    }

    [AvaloniaFact]
    public void The_Round_Counter_Rolls_To_Every_New_Round()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        Assert.False(arena.RoundGroup!.IsVisible);
        _ = rig.Run();
        Assert.True(arena.RoundGroup.IsVisible);
        Assert.Equal(1, arena.RoundDigits!.Value);
        Assert.Equal("Round 1", Avalonia.Automation.AutomationProperties.GetName(arena.RoundWord!));
        _ = rig.Run();
        _ = rig.Run();
        Assert.Equal(3, arena.Round);
        Assert.Equal(3, arena.RoundDigits.Value);
        Assert.Equal("Round 3", Avalonia.Automation.AutomationProperties.GetName(arena.RoundWord!));
    }

    [AvaloniaFact]
    public void Enter_Runs_Escape_Stops_And_The_Focus_Stays_In_The_Box()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        Assert.True(arena.Form!.FocusBox());
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(1, arena.Round);
        Assert.Equal(ArenaPhase.Running, arena.Phase);
        rig.Key(PhysicalKey.Escape);
        Assert.Equal(ArenaPhase.Stopped, arena.Phase);
        Assert.True(rig.FocusIsIn(arena.Form.Box));
    }

    [AvaloniaFact]
    public void A_Chip_Fills_The_Box_And_Runs_And_The_Shortcut_On_A_Chip_Runs_The_Box()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        var chip = arena.Form!.Chips[2];
        rig.Click(chip);
        Assert.Equal(1, arena.Round);
        Assert.Equal(ModelArenaSamples.Prompts()[2].Text, arena.PromptText);
        Assert.Equal(arena.PromptText, arena.RoundPrompt);
        Assert.True(arena.Form.Chips[2].IsChecked);

        arena.Stop();
        arena.PromptText = "Typed by hand";
        rig.Pump();
        arena.Form.Chips[1].Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter, RawInputModifiers.Control);
        Assert.Equal(2, arena.Round);
        Assert.Equal("Typed by hand", arena.RoundPrompt);
    }

    [AvaloniaFact]
    public void Run_Again_Runs_The_Stopped_Prompt_And_Moves_The_Focus_To_Stop()
    {
        using var rig = new ModelArenaRig();
        var arena = rig.Arena;
        _ = rig.Run("First prompt");
        arena.Stop();
        rig.Pump();
        arena.PromptText = "Edited";
        rig.Pump();
        var again = arena.VoteBar!.ActionButton!;
        again.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        Assert.Equal(2, arena.Round);
        Assert.Equal("First prompt", arena.RoundPrompt);
        Assert.Equal("First prompt", arena.PromptText);
        Assert.True(rig.FocusIsIn(arena.Form!.RunButton));
        Assert.True(arena.Form.RunButton!.IsRunning);
    }

    [AvaloniaFact]
    public void Running_At_Reduced_And_Off_Motion_Keeps_The_Cadence()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = new ModelArenaRig(level: level);
            _ = rig.Run();
            rig.Push(rig.A, ModelArenaSamples.Words(20) + " ");
            rig.Frame();
            var first = WordCount(rig.Arena.PaneA.Text);
            Assert.InRange(first, 1, 8);
            rig.Complete(rig.A);
            rig.Advance(TimeSpan.FromMilliseconds(800));
            Assert.Equal(20, WordCount(rig.Arena.PaneA.Text));
            Assert.Equal(ArenaPaneStatus.FinishedFirst, rig.Arena.PaneA.Status);
        }
    }
}
