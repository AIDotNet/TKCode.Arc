using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiChat"/>: structure and geometry, the thread built from the visible branch, the empty state, sending and
/// the streaming state machine with a scripted responder.
/// </summary>
public class AiChatTests
{
    #region Structure

    [AvaloniaFact]
    public void The_Chat_Has_A_Header_A_Thread_And_A_Single_Row_Composer()
    {
        using var rig = new AiChatRig(history: true);
        var chat = rig.Chat;
        chat.Header!.Bounds.Height.ShouldBe(52);
        chat.TitlePill!.Bounds.Height.ShouldBe(34);
        chat.ModelPillControl!.Bounds.Height.ShouldBe(34);
        chat.NewChatButton!.Bounds.Width.ShouldBe(34);
        chat.NewChatButton.Bounds.Height.ShouldBe(34);
        chat.Composer!.Layout.ShouldBe(AiComposerLayout.Inline);
        chat.Composer.MaxLength.ShouldBeNull();
        AutomationProperties.GetName(chat).ShouldBe("AI chat");
        AutomationProperties.GetName(chat.NewChatButton).ShouldBe("New chat");
        AutomationProperties.GetName(chat.TitlePill).ShouldBe("Chats, current: " + AiChatSamples.Title);
        AutomationProperties.GetName(chat.ModelPillControl).ShouldBe("Model: Orbit Pro 3");
    }

    [AvaloniaFact]
    public void The_Header_Puts_The_Title_Left_And_The_Model_And_New_Chat_Right()
    {
        using var rig = new AiChatRig(history: true);
        var chat = rig.Chat;
        var title = chat.TitlePill!.TranslatePoint(default, chat)!.Value;
        title.X.ShouldBe(18, 0.5);
        var newChat = chat.NewChatButton!.TranslatePoint(default, chat)!.Value;
        (1100 - (newChat.X + 34)).ShouldBe(10, 0.5);
        var model = chat.ModelPillControl!.TranslatePoint(default, chat)!.Value;
        (newChat.X - (model.X + chat.ModelPillControl!.Bounds.Width)).ShouldBe(2, 0.5);
    }

    [AvaloniaFact]
    public void The_Thread_Is_A_Centered_Column_Of_760_Plus_28_On_Each_Side()
    {
        using var rig = new AiChatRig(history: true);
        var host = rig.Chat.ThreadHost!;
        host.Bounds.Width.ShouldBe(816);
        host.Padding.ShouldBe(new Thickness(28, 24, 28, 12));
        var x = host.TranslatePoint(default, rig.Chat)!.Value.X;
        x.ShouldBe((1100 - 816) / 2.0, 0.5);
        rig.Chat.TurnsPanel!.Spacing.ShouldBe(28);
        var dock = rig.Chat.Dock!;
        dock.Bounds.Width.ShouldBe(816);
        dock.Padding.ShouldBe(new Thickness(20, 6, 20, 12));
        rig.Composer.Bounds.Width.ShouldBe(776);
    }

    [AvaloniaFact]
    public void The_Settled_Conversation_Shows_Two_Turns_With_Their_Replies_And_No_Replay()
    {
        using var rig = new AiChatRig(history: true);
        rig.Views.Count.ShouldBe(2);
        var first = rig.Views[0];
        first.User!.Message!.Text.ShouldBe(AiChatSamples.FirstPrompt);
        var reply = first.ReplyView!;
        reply.Reply!.Text.ShouldBe(AiChatSamples.FirstText);
        reply.Reasoning!.IsThinking.ShouldBeFalse();
        reply.Reasoning.ThoughtText.ShouldBe("Thought for 3s");
        reply.Chips.Count.ShouldBe(1);
        reply.Text!.IsRevealing.ShouldBeFalse();
        reply.Text.IsFinished.ShouldBeTrue();
        reply.Text.AppendedPieces.ShouldBeEmpty();
        reply.Actions!.IsLast.ShouldBeFalse();
        rig.Views[1].ReplyView!.Actions!.IsLast.ShouldBeTrue();
        rig.Views[1].ReplyView!.Reasoning!.ThoughtText.ShouldBe("Thought for 2s");
        rig.Chat.Classes.Contains(AiChat.EmptyPseudoClass).ShouldBeFalse();
        rig.Chat.EmptyState!.IsShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Reply_Shows_The_Mark_Of_The_Model_That_Wrote_It_In_The_Secondary_Color_When_Monochrome()
    {
        using var rig = new AiChatRig(history: true);
        var mark = rig.Views[0].ReplyView!.Mark;
        mark.Mark.ShouldBeSameAs(AiChatSamples.OrbitMark);
        mark.Size.ShouldBe(16);
        rig.Chat.SelectedModel = rig.Chat.Models.First(m => m.Id == "quill-2");
        rig.Settle();
        // Old replies keep the mark of the model that wrote them.
        rig.Views[0].ReplyView!.Mark.Mark.ShouldBeSameAs(AiChatSamples.OrbitMark);
    }

    [AvaloniaFact]
    public void Turns_Are_A_Named_List_Of_Named_Items()
    {
        using var rig = new AiChatRig(history: true);
        AutomationProperties.GetName(rig.Chat.TurnsPanel!).ShouldBe("Messages");
        AutomationProperties.GetName(rig.Views[0]).ShouldBe("Message 1 of 2");
        AutomationProperties.GetName(rig.Views[1]).ShouldBe("Message 2 of 2");
    }

    #endregion

    #region Empty state

    [AvaloniaFact]
    public void Without_A_Conversation_The_Greeting_And_Three_Suggestions_Show_And_The_Title_Is_New_Chat()
    {
        using var rig = new AiChatRig();
        var chat = rig.Chat;
        chat.Classes.Contains(AiChat.EmptyPseudoClass).ShouldBeTrue();
        chat.EmptyState!.IsShown.ShouldBeTrue();
        chat.EmptyState.Greeting.ShouldBe("What should we look at today?");
        chat.EmptyState.List!.GetVisualDescendants().OfType<AiChatSuggestionButton>().Count().ShouldBe(3);
        chat.TitlePill!.Title.ShouldBe("New chat");
        rig.Views.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void A_Suggestion_Sends_Its_Prompt_And_Creates_A_Conversation()
    {
        using var rig = new AiChatRig();
        var button = rig.Chat.EmptyState!.List!.GetVisualDescendants().OfType<AiChatSuggestionButton>().First();
        rig.Click(button);
        rig.Created.Count.ShouldBe(1);
        rig.Chat.CurrentConversation.ShouldBeSameAs(rig.Created[0]);
        rig.Chat.Conversations[0].ShouldBeSameAs(rig.Created[0]);
        rig.Created[0].Title.ShouldBe(string.Empty);
        rig.Sent.Single().Prompt.Text.ShouldBe("Find the slowest step in the nightly build");
        rig.Call.Request.Messages.Single().Content.ShouldBe("Find the slowest step in the nightly build");
        rig.Views.Count.ShouldBe(1);
        rig.Chat.EmptyState.IsShown.ShouldBeFalse();
        rig.Chat.TitlePill!.Title.ShouldBe("Find the slowest step in the");
    }

    #endregion

    #region Sending and streaming

    [AvaloniaFact]
    public void Enter_Sends_A_Turn_That_Thinks_And_The_Composer_Turns_Into_Stop()
    {
        using var rig = new AiChatRig();
        rig.Send("Why is the build slow?");
        rig.Sent.Count.ShouldBe(1);
        var reply = rig.Sent[0].Turn.Versions[0].Reply;
        reply.Status.ShouldBe(AiChatReplyStatus.Thinking);
        reply.ModelId.ShouldBe("orbit-pro");
        rig.Chat.IsBusy.ShouldBeTrue();
        rig.Chat.Classes.Contains(AiChat.BusyPseudoClass).ShouldBeTrue();
        rig.Composer.IsGenerating.ShouldBeTrue();
        rig.Composer.Text.ShouldBe(string.Empty);
        rig.Announcement.ShouldBe("Orbit Pro 3 is replying");
        rig.LastReply.Reasoning!.IsThinking.ShouldBeTrue();
        rig.LastReply.Actions!.IsStreaming.ShouldBeTrue();
        rig.Call.Request.Model!.Id.ShouldBe("orbit-pro");
        rig.Call.Request.Conversation.ShouldBeSameAs(rig.Chat.CurrentConversation);
    }

    [AvaloniaFact]
    public void Attachments_Go_Into_The_Message_And_A_Message_Of_Only_Attachments_Can_Be_Sent()
    {
        using var rig = new AiChatRig();
        rig.Composer.Attachments.Add(new AiAttachment { Id = "a", Name = "stage-timings.csv", Size = 1229 });
        rig.Settle();
        rig.Composer.SendState.ShouldBe(AiSendState.Ready);
        rig.Composer.FocusInput();
        rig.Settle();
        rig.Key(Avalonia.Input.PhysicalKey.Enter);
        var prompt = rig.Sent.Single().Prompt;
        prompt.Text.ShouldBe(string.Empty);
        prompt.Attachments.Single().Name.ShouldBe("stage-timings.csv");
        rig.Call.Request.Messages.Single().Attachments.Single().Name.ShouldBe("stage-timings.csv");
        rig.LastView.User!.Message.ShouldBeSameAs(prompt);
        rig.Composer.Attachments.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void The_Composer_Takes_The_Chat_Words_And_Settings()
    {
        using var rig = new AiChatRig(chat =>
        {
            chat.PlaceholderText = "Ask about the build";
            chat.IsAttachmentEnabled = false;
            chat.AcceptedFileTypes = ".csv";
        });
        rig.Composer.Placeholder.ShouldBe("Ask about the build");
        rig.Composer.IsAttachmentEnabled.ShouldBeFalse();
        rig.Composer.AcceptedFileTypes.ShouldBe(".csv");
        rig.Composer.Labels.Send.ShouldBe("Send message");
        rig.Composer.Labels.Stop.ShouldBe("Stop reply");
        rig.Composer.Labels.Attach.ShouldBe("Attach files");
        rig.Composer.Labels.KeyHint.ShouldNotContain("/");
        AutomationProperties.GetName(rig.Editor).ShouldBe("Message");
        rig.Chat.Labels = new AiChatLabels { Placeholder = "Frag etwas", StopReply = "Antwort anhalten" };
        rig.Settle();
        rig.Composer.Placeholder.ShouldBe("Ask about the build");
        rig.Composer.Labels.Stop.ShouldBe("Antwort anhalten");
        rig.Chat.PlaceholderText = null;
        rig.Composer.Placeholder.ShouldBe("Frag etwas");
    }

    [AvaloniaFact]
    public void Thinking_Then_Streaming_Then_Done_After_The_Text_Was_Shown()
    {
        using var rig = new AiChatRig();
        rig.Send("Why is the build slow?");
        var reply = rig.LastReply.Reply!;
        rig.Time.Advance(TimeSpan.FromSeconds(2.4));
        rig.Call.Push(new AiChatReasoningDelta("Look at the "), new AiChatReasoningDelta("restore step."));
        reply.Reasoning.ShouldBe("Look at the restore step.");
        rig.LastReply.Reasoning!.IsThinking.ShouldBeTrue();
        rig.Call.Push(new AiChatReasoningEnd());
        reply.ThoughtFor.ShouldBe(TimeSpan.FromSeconds(2.4));
        rig.LastReply.Reasoning.IsThinking.ShouldBeFalse();
        rig.LastReply.Reasoning.ThoughtText.ShouldBe("Thought for 2s");
        reply.Status.ShouldBe(AiChatReplyStatus.Thinking);

        rig.Call.Text("The cache key changed when the lock file did, so ");
        reply.Status.ShouldBe(AiChatReplyStatus.Streaming);
        reply.Text.ShouldBe("The cache key changed when the lock file did, so ");
        // Paced: nothing shows before the next frame.
        rig.LastReply.Text!.AppendedPieces.ShouldBeEmpty();
        rig.Run(0.02);
        string.Concat(rig.LastReply.Text.AppendedPieces).ShouldBe("The cache key ");
        rig.Call.Text("every run compiles from scratch.");
        rig.Call.End();
        reply.Status.ShouldBe(AiChatReplyStatus.Streaming);
        rig.Completed.ShouldBeEmpty();
        rig.Chat.IsBusy.ShouldBeTrue();

        rig.RunUntil(() => reply.Status == AiChatReplyStatus.Done).ShouldBeTrue();
        string.Concat(rig.LastReply.Text.AppendedPieces).ShouldBe(reply.Text);
        reply.Text.ShouldBe("The cache key changed when the lock file did, so every run compiles from scratch.");
        rig.Completed.Single().Status.ShouldBe(AiChatReplyStatus.Done);
        rig.Announcement.ShouldBe("Reply finished");
        rig.Chat.IsBusy.ShouldBeFalse();
        rig.Composer.IsGenerating.ShouldBeFalse();
        rig.LastReply.Actions!.IsStreaming.ShouldBeFalse();
        rig.LastReply.IsDriven.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Tools_Are_Upserted_By_Id_And_End_The_Thinking_Label()
    {
        using var rig = new AiChatRig();
        rig.Send("How much time would we save?");
        var reply = rig.LastReply.Reply!;
        rig.Time.Advance(TimeSpan.FromSeconds(0.6));
        rig.Call.Push(new AiChatToolUpdate("q", AiChatToolKind.Query, AiChatToolStatus.Running, "Querying build history",
            Input: "SELECT 1;"));
        reply.ThoughtFor.ShouldBe(TimeSpan.FromSeconds(0.6));
        rig.LastReply.Reasoning!.ThoughtText.ShouldBe("Thought for 1s");
        var tool = reply.Tools.Single();
        tool.Status.ShouldBe(AiChatToolStatus.Running);
        tool.RunningLabel.ShouldBe("Querying build history");
        tool.Input.ShouldBe("SELECT 1;");
        rig.LastReply.Chips.Single().Tool.ShouldBeSameAs(tool);

        rig.Call.Push(new AiChatToolUpdate("q", AiChatToolKind.Query, AiChatToolStatus.Done, "Queried build history", "212 runs",
            Output: "restore 4.1 s"));
        reply.Tools.Single().ShouldBeSameAs(tool);
        tool.Status.ShouldBe(AiChatToolStatus.Done);
        tool.DoneLabel.ShouldBe("Queried build history");
        tool.RunningLabel.ShouldBe("Querying build history");
        tool.Summary.ShouldBe("212 runs");
        tool.Output.ShouldBe("restore 4.1 s");
        tool.Input.ShouldBe("SELECT 1;");

        rig.Call.Push(new AiChatToolUpdate("s", AiChatToolKind.Search, AiChatToolStatus.Running, "Searching the docs",
            Queries: ["cache key"]));
        reply.Tools.Count.ShouldBe(2);
        reply.Tools[1].Queries.ShouldBe(["cache key"]);
        rig.LastReply.Chips.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Sources_Are_Numbered_From_One_In_Arrival_Order_Unless_They_Carry_A_Number()
    {
        using var rig = new AiChatRig();
        rig.Send("Sources please");
        var reply = rig.LastReply.Reply!;
        rig.Call.Push(new AiChatSourcesEvent([new AiSource { Title = "A" }, new AiSource { Title = "B" }]));
        rig.Call.Push(new AiChatSourcesEvent([new AiSource { Title = "C" }, new AiSource { Title = "D", Index = 9 }]));
        reply.Sources.Select(s => (s.Title, s.Index)).ShouldBe([("A", 1), ("B", 2), ("C", 3), ("D", 9)]);
        rig.LastReply.Text!.Sources.ShouldBeSameAs(reply.Sources);
    }

    #endregion
}
