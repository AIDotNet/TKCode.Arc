using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiChat"/> replies: stop (before and during the text, button and Escape), failure (event and exception) and
/// retry in place, retry and edit as new versions, the version pager, cancellation on unload and on a new reply, and
/// replies your code fills without a responder.
/// </summary>
public class AiChatReplyTests
{
    private static AiChatReply Ask(AiChatRig rig, string text = "Why is the build slow?")
    {
        rig.Send(text);
        return rig.LastReply.Reply!;
    }

    #region Stop

    [AvaloniaFact]
    public void Stop_Before_Any_Text_Says_Stopped_Before_Answering()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Chat.StopReply();
        reply.Status.ShouldBe(AiChatReplyStatus.Stopped);
        reply.Text.ShouldBe(string.Empty);
        rig.Call.IsCancelled.ShouldBeTrue();
        rig.LastReply.Status!.IsVisible.ShouldBeTrue();
        rig.LastReply.Status.Text.ShouldBe("Stopped before answering");
        rig.LastReply.Actions!.IsStreaming.ShouldBeFalse();
        rig.LastReply.Actions.IsVisible.ShouldBeTrue();
        rig.Completed.Single().Status.ShouldBe(AiChatReplyStatus.Stopped);
        rig.Announcement.ShouldBe("Reply stopped");
        rig.Chat.IsBusy.ShouldBeFalse();
        rig.LastReply.Reasoning!.IsThinking.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Stop_Mid_Text_Keeps_What_Was_Shown_And_Drops_The_Backlog()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Call.Text("One two three four five six seven eight nine ten eleven twelve ");
        rig.Run(0.12);
        var shown = string.Concat(rig.LastReply.Text!.AppendedPieces);
        shown.ShouldNotBeEmpty();
        shown.Length.ShouldBeLessThan(reply.Text.Length);
        rig.Chat.StopReply();
        reply.Status.ShouldBe(AiChatReplyStatus.Stopped);
        reply.Text.ShouldBe(shown);
        rig.Run(0.5);
        string.Concat(rig.LastReply.Text.AppendedPieces).ShouldBe(shown);
        rig.LastReply.Status!.Text.ShouldBe("Stopped");
    }

    [AvaloniaFact]
    public void Escape_In_The_Message_Box_Stops_And_Enter_Does_Not_Send_While_Busy()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Composer.FocusInput();
        rig.Settle();
        rig.Window.KeyTextInput("next question");
        rig.Settle();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Count.ShouldBe(1);
        rig.Composer.Text.ShouldBe("next question");
        rig.Key(PhysicalKey.Escape);
        reply.Status.ShouldBe(AiChatReplyStatus.Stopped);
        rig.Composer.IsGenerating.ShouldBeFalse();
        rig.Key(PhysicalKey.Enter);
        rig.Sent.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void A_Tool_Still_Running_When_The_Reply_Stops_Shows_As_Failed()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Call.Push(new AiChatToolUpdate("q", AiChatToolKind.Query, AiChatToolStatus.Running, "Querying build history"));
        rig.Chat.StopReply();
        reply.Tools.Single().Status.ShouldBe(AiChatToolStatus.Error);
    }

    #endregion

    #region Failure and retry

    [AvaloniaFact]
    public void An_Error_Event_Fails_The_Reply_With_The_Box_And_No_Action_Bar()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig, "Check disk usage on ci-runner-7");
        rig.Call.Push(new AiChatToolUpdate("disk", AiChatToolKind.Query, AiChatToolStatus.Error, "Querying ci-runner-7", "Timed out"),
            new AiChatErrorEvent(AiChatSamples.RunnerError));
        reply.Status.ShouldBe(AiChatReplyStatus.Failed);
        reply.ErrorMessage.ShouldBe(AiChatSamples.RunnerError);
        rig.Call.IsCancelled.ShouldBeTrue();
        var note = rig.LastReply.Status!;
        note.IsVisible.ShouldBeTrue();
        note.Tone.ShouldBe(AiStatusNoteTone.Error);
        note.Layout.ShouldBe(AiStatusNoteLayout.Card);
        note.Title.ShouldBe("Reply failed.");
        note.Text.ShouldBe(AiChatSamples.RunnerError);
        note.ActionText.ShouldBe("Retry");
        rig.LastReply.Actions!.IsVisible.ShouldBeFalse();
        rig.Announcement.ShouldBe("Reply failed: " + AiChatSamples.RunnerError);
        rig.Completed.Single().Status.ShouldBe(AiChatReplyStatus.Failed);
        rig.Chat.IsBusy.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void An_Exception_Fails_The_Reply_With_Its_Message()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Call.Throw(new InvalidOperationException("The model is busy."));
        reply.Status.ShouldBe(AiChatReplyStatus.Failed);
        reply.ErrorMessage.ShouldBe("The model is busy.");
    }

    [AvaloniaFact]
    public void Retry_In_The_Error_Box_Runs_Again_In_Place_Without_A_Version()
    {
        using var rig = new AiChatRig();
        var failed = Ask(rig, "Check disk usage on ci-runner-7");
        var turn = rig.LastView.Turn!;
        rig.Call.Push(new AiChatErrorEvent("Timed out"));
        rig.Click(rig.LastReply.Status!.GetVisualDescendants().OfType<Button>().First());
        turn.Versions.Count.ShouldBe(1);
        var again = turn.Versions[0].Reply;
        again.ShouldNotBeSameAs(failed);
        again.Status.ShouldBe(AiChatReplyStatus.Thinking);
        rig.Responder!.Calls.Count.ShouldBe(2);
        rig.Call.Request.Messages.Select(m => m.Content).ShouldBe(["Check disk usage on ci-runner-7"]);
        rig.LastView.ShouldBeSameAs(rig.Views[^1]);
        rig.LastReply.Reply.ShouldBeSameAs(again);
        rig.LastReply.Status!.IsVisible.ShouldBeFalse();
        rig.Call.Text("All mounts are under 60%.");
        rig.Call.End();
        rig.RunUntil(() => again.Status == AiChatReplyStatus.Done).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Retry_On_A_Finished_Reply_Adds_A_Version_And_The_Later_Turns_Stay_With_The_Old_One()
    {
        using var rig = new AiChatRig(history: true);
        var first = rig.Views[0];
        var turn = first.Turn!;
        var oldReply = turn.Versions[0].Reply;
        var retry = first.ReplyView!.Actions!.RetryButton!;
        first.ReplyView.IsRevealed = true;
        rig.Settle();
        retry.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);

        turn.Versions.Count.ShouldBe(2);
        turn.SelectedVersionIndex.ShouldBe(1);
        turn.Versions[1].Prompt.Text.ShouldBe(AiChatSamples.FirstPrompt);
        turn.Versions[1].Prompt.ShouldNotBeSameAs(turn.Versions[0].Prompt);
        turn.Versions[0].Continuation.Count.ShouldBe(1);
        rig.Views.Count.ShouldBe(1);
        rig.Views[0].ShouldBeSameAs(first);
        first.ReplyView.Reply.ShouldBeSameAs(turn.Versions[1].Reply);
        oldReply.Status.ShouldBe(AiChatReplyStatus.Done);
        // The focus stays on Retry.
        rig.HasFocus(retry).ShouldBeTrue();
        rig.Call.Request.Messages.Select(m => m.Content).ShouldBe([AiChatSamples.FirstPrompt]);
        rig.Sent.Last().Turn.ShouldBeSameAs(turn);
        first.Pager.Count.ShouldBe(2);
        first.Pager.Index.ShouldBe(1);
        first.Pager.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Pager_Switches_Versions_And_The_Turns_After_Follow_The_Version()
    {
        using var rig = new AiChatRig(history: true);
        var first = rig.Views[0];
        var turn = first.Turn!;
        rig.Chat.Retry(turn);
        rig.Call.Text("Shorter answer.");
        rig.Call.End();
        rig.RunUntil(() => turn.Versions[1].Reply.Status == AiChatReplyStatus.Done).ShouldBeTrue();
        rig.Views.Count.ShouldBe(1);

        rig.Click(first.Pager.PreviousButton!);
        turn.SelectedVersionIndex.ShouldBe(0);
        rig.Views.Count.ShouldBe(2);
        rig.Views[0].ShouldBeSameAs(first);
        first.ReplyView!.Reply.ShouldBeSameAs(turn.Versions[0].Reply);
        // A finished reply is not replayed.
        first.ReplyView.Text!.AppendedPieces.ShouldBeEmpty();
        rig.Announcement.ShouldBe("Version 1 of 2");
        rig.Views[1].Turn!.Versions[0].Prompt.Text.ShouldBe(AiChatSamples.SecondPrompt);

        rig.Click(first.Pager.NextButton!);
        turn.SelectedVersionIndex.ShouldBe(1);
        rig.Views.Count.ShouldBe(1);
        rig.Announcement.ShouldBe("Version 2 of 2");
    }

    [AvaloniaFact]
    public void A_Host_Version_Switch_Re_Renders_The_Branch()
    {
        using var rig = new AiChatRig(history: true);
        var turn = rig.Views[0].Turn!;
        var version = new AiChatTurnVersion { Prompt = new AiChatUserMessage { Text = "Other question" }, Reply = new AiChatReply { Text = "Other answer" } };
        turn.Versions.Add(version);
        rig.Views[0].Pager.Count.ShouldBe(2);
        turn.SelectedVersionIndex = 1;
        rig.Settle();
        rig.Views[0].User!.Message!.Text.ShouldBe("Other question");
        rig.Views[0].ReplyView!.Reply.ShouldBeSameAs(version.Reply);
        // In a plain list the later turns follow every version; a continuation of their own takes over.
        rig.Views.Count.ShouldBe(2);
        version.Continuation.Add(new AiChatTurn(new AiChatUserMessage { Text = "Follow-up" }, new AiChatReply { Text = "Sure." }));
        rig.Settle();
        rig.Views.Count.ShouldBe(2);
        rig.Views[1].User!.Message!.Text.ShouldBe("Follow-up");
    }

    [AvaloniaFact]
    public void Edit_Sends_A_New_Version_With_The_New_Prompt()
    {
        using var rig = new AiChatRig(history: true);
        var last = rig.Views[1];
        var turn = last.Turn!;
        var bubble = last.User!;
        bubble.BeginEdit();
        rig.Settle();
        bubble.Editor.ShouldNotBeNull();
        bubble.Editor!.Text = "How much time per month?";
        rig.Settle();
        rig.Key(PhysicalKey.Enter);

        turn.Versions.Count.ShouldBe(2);
        turn.SelectedVersion!.Prompt.Text.ShouldBe("How much time per month?");
        turn.SelectedVersion.Reply.Status.ShouldBe(AiChatReplyStatus.Thinking);
        rig.Sent.Last().Prompt.Text.ShouldBe("How much time per month?");
        rig.Call.Request.Messages.Select(m => m.Content).ShouldBe(
            [AiChatSamples.FirstPrompt, AiChatSamples.FirstText, "How much time per month?"]);
        rig.Views[1].ShouldBeSameAs(last);
        bubble.Message!.Text.ShouldBe("How much time per month?");
        bubble.IsEditing.ShouldBeFalse();
    }

    #endregion

    #region Cancellation

    [AvaloniaFact]
    public void A_New_Reply_In_The_Same_Conversation_Stops_The_Running_One()
    {
        using var rig = new AiChatRig(history: true);
        var turn = rig.Views[1].Turn!;
        rig.Chat.Retry(turn);
        var running = turn.SelectedVersion!.Reply;
        var firstCall = rig.Call;
        rig.Chat.Retry(rig.Views[0].Turn!);
        firstCall.IsCancelled.ShouldBeTrue();
        running.Status.ShouldBe(AiChatReplyStatus.Stopped);
    }

    [AvaloniaFact]
    public void Unloading_The_Chat_Stops_Its_Replies()
    {
        using var rig = new AiChatRig();
        var reply = Ask(rig);
        rig.Call.Text("Partial answer ");
        rig.Window.Content = null;
        rig.Settle();
        reply.Status.ShouldBe(AiChatReplyStatus.Stopped);
        rig.Call.IsCancelled.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Switching_Conversations_Does_Not_Stop_A_Reply_And_It_Finishes_Away()
    {
        using var rig = new AiChatRig(history: true);
        var reply = Ask(rig, "One more thing");
        rig.Call.Text("It keeps going while you are away.");
        rig.Chat.CurrentConversation = rig.Chat.Conversations[1];
        rig.Run(0.3);
        rig.Call.IsCancelled.ShouldBeFalse();
        reply.Status.ShouldBe(AiChatReplyStatus.Streaming);
        rig.Chat.IsBusy.ShouldBeFalse();
        rig.Call.End();
        reply.Status.ShouldBe(AiChatReplyStatus.Done);
        reply.Text.ShouldBe("It keeps going while you are away.");
        rig.Chat.CurrentConversation = rig.Chat.Conversations[0];
        rig.Run(0.3);
        rig.LastReply.Reply.ShouldBeSameAs(reply);
        rig.LastReply.Text!.AppendedPieces.ShouldBeEmpty();
    }

    #endregion

    #region Without a responder

    [AvaloniaFact]
    public void Without_A_Responder_A_Send_Adds_The_Turn_And_The_View_Follows_The_Reply_Your_Code_Fills()
    {
        using var rig = new AiChatRig(responder: false);
        rig.Send("Fill this yourself");
        var sent = rig.Sent.Single();
        var reply = sent.Turn.Versions[0].Reply;
        reply.Status.ShouldBe(AiChatReplyStatus.Thinking);
        rig.Chat.IsBusy.ShouldBeTrue();
        var view = rig.LastReply;
        view.Reasoning!.IsThinking.ShouldBeTrue();

        reply.Reasoning = "Thinking it over.";
        view.Reasoning.Reasoning.ShouldBe("Thinking it over.");
        reply.Tools.Add(AiChatSamples.QueryTool());
        view.Chips.Count.ShouldBe(1);
        view.Reasoning.IsThinking.ShouldBeFalse();
        reply.Status = AiChatReplyStatus.Streaming;
        reply.Text = "First words ";
        reply.Text += "and more words.";
        rig.Run(0.05);
        view.Text!.AppendedPieces.ShouldNotBeEmpty();
        reply.Status = AiChatReplyStatus.Done;
        rig.Chat.IsBusy.ShouldBeFalse();
        rig.RunUntil(() => !view.Text.IsRevealing).ShouldBeTrue();
        string.Concat(view.Text.AppendedPieces).ShouldBe("First words and more words.");
        rig.Announcement.ShouldBe("Reply finished");
    }

    [AvaloniaFact]
    public void Without_A_Responder_Stop_Marks_The_Reply_Stopped_And_Tells_Your_Code()
    {
        using var rig = new AiChatRig(responder: false);
        rig.Send("Fill this yourself");
        var reply = rig.Sent.Single().Turn.Versions[0].Reply;
        rig.Chat.StopReply();
        reply.Status.ShouldBe(AiChatReplyStatus.Stopped);
        rig.Completed.Single().Reply.ShouldBeSameAs(reply);
        rig.Chat.IsBusy.ShouldBeFalse();
    }

    #endregion

    #region Away from the window

    [AvaloniaFact]
    public void A_Message_Sent_While_Away_Waits_Thinking_And_Streams_Once_The_Chat_Is_Back()
    {
        using var rig = new AiChatRig(history: true);
        var layers = rig.Window.Content;
        rig.Window.Content = null;
        rig.Settle();
        rig.Chat.Send("Anything else?");
        rig.Settle();
        var reply = rig.Sent.Single().Turn.SelectedVersion!.Reply;
        rig.Responder!.Calls.ShouldBeEmpty();
        reply.Status.ShouldBe(AiChatReplyStatus.Thinking);
        rig.Chat.IsBusy.ShouldBeTrue();

        rig.Window.Content = layers;
        rig.Settle();
        rig.Responder.Calls.Count.ShouldBe(1);
        rig.Chat.IsStreaming(reply).ShouldBeTrue();
        rig.LastReply.Reply.ShouldBeSameAs(reply);
        rig.Call.Text("Nothing else is slow.");
        rig.Call.End();
        rig.RunUntil(() => reply.Status == AiChatReplyStatus.Done).ShouldBeTrue();
        reply.Text.ShouldBe("Nothing else is slow.");
    }

    [AvaloniaFact]
    public void A_Chat_Sent_To_While_Away_Streams_Nothing_And_Is_Collected()
    {
        var responder = new ScriptedResponder();
        var conversation = AiChatSamples.NightlyBuild();
        var weak = TestHelpers.ShowAndClose(() =>
        {
            var chat = AiChatSamples.Chat(responder);
            chat.Conversations.Add(conversation);
            chat.CurrentConversation = conversation;
            return chat;
        }, chat =>
        {
            ((Avalonia.Controls.Window)Avalonia.Controls.TopLevel.GetTopLevel(chat)!).Content = null;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            chat.Send("Anything else?");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        responder.Calls.ShouldBeEmpty();
        conversation.Turns.Count.ShouldBe(3);
        conversation.Turns[2].SelectedVersion!.Reply.Status.ShouldBe(AiChatReplyStatus.Thinking);
    }

    #endregion
}
