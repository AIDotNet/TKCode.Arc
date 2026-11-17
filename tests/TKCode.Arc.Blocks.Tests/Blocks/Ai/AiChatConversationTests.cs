using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiChat"/> around the thread: conversations (menu, titles, offsets, crossfade), models, new chat, ratings,
/// copying, citations and following the newest message.
/// </summary>
public class AiChatConversationTests
{
    #region Conversations

    [AvaloniaFact]
    public void Choosing_A_Conversation_Opens_It_Announces_And_Keeps_The_Focus_On_The_Title()
    {
        using var rig = new AiChatRig(history: true);
        var pill = rig.Chat.TitlePill!;
        pill.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        pill.IsMenuOpen.ShouldBeTrue();
        // New chat, then the conversations: the second one of "Today".
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        var bucket = rig.Chat.Conversations[1];
        rig.Chat.CurrentConversation.ShouldBeSameAs(bucket);
        rig.Opened2.Single().ShouldBeSameAs(bucket);
        rig.Announcement.ShouldBe("Opened Rename the staging bucket");
        rig.HasFocus(pill).ShouldBeTrue();
        pill.Title.ShouldBe("Rename the staging bucket");
        rig.Views.Count.ShouldBe(1);
        rig.Views[0].User!.Message!.Text.ShouldBe("What breaks if we rename the staging bucket?");
        // Finished replies are not replayed.
        rig.Views[0].ReplyView!.Text!.AppendedPieces.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Each_Conversation_Keeps_Its_Scroll_Offset()
    {
        using var rig = new AiChatRig(history: true, height: 600);
        var scroller = rig.Chat.Scroller!;
        var scroll = scroller.Scroll!;
        scroller.DistanceToBottom.ShouldBeLessThan(1);
        // The person scrolls to the top.
        var point = rig.CenterOf(scroller);
        for (var i = 0; i < 80; i++)
        {
            rig.Window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.None);
        }
        rig.Settle();
        scroll.Offset.Y.ShouldBe(0);
        rig.Chat.IsFollowingLatest.ShouldBeFalse();
        var nightly = rig.Chat.CurrentConversation!;

        rig.Chat.CurrentConversation = rig.Chat.Conversations[4];
        rig.Run(0.2);
        rig.Chat.CurrentConversation = nightly;
        rig.Run(0.2);
        scroll.Offset.Y.ShouldBe(0);
        rig.Chat.IsFollowingLatest.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Switching_At_Full_Motion_Fades_The_Thread_Out_Then_The_Other_In_Rising()
    {
        using var rig = new AiChatRig(history: true, level: ArcMotionLevel.Full);
        var pane = rig.Chat.Pane!;
        var fade = rig.Chat.PaneFade!;
        var before = rig.Views[0];
        rig.Chat.CurrentConversation = rig.Chat.Conversations[2];
        rig.Run(0.05);
        fade.Progress.ShouldBeLessThan(1);
        fade.Progress.ShouldBeGreaterThan(0);
        // Still the old thread while it fades out.
        rig.Views[0].ShouldBeSameAs(before);
        rig.Run(0.1);
        rig.Views[0].User!.Message!.Text.ShouldStartWith("Draft the 4.2");
        rig.Run(0.3);
        fade.Progress.ShouldBe(1);
        pane.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void The_Title_Is_The_Title_Or_The_First_Six_Words_Or_Untitled()
    {
        using var rig = new AiChatRig();
        var pill = rig.Chat.TitlePill!;
        pill.Title.ShouldBe("New chat");
        rig.Send("Draft a short note to the team about the outage");
        pill.Title.ShouldBe("Draft a short note to the");
        AutomationProperties.GetName(pill).ShouldBe("Chats, current: Draft a short note to the");
        rig.Chat.CurrentConversation!.Title = "Outage note";
        rig.Settle();
        pill.Title.ShouldBe("Outage note");
        var untitled = new AiChatConversation { Id = "x" };
        rig.Chat.Conversations.Add(untitled);
        rig.Chat.CurrentConversation = untitled;
        rig.Settle();
        pill.Title.ShouldBe("Untitled chat");
    }

    #endregion

    #region New chat

    [AvaloniaFact]
    public void New_Chat_Unhandled_Shows_The_Empty_State_Focuses_The_Box_And_Announces()
    {
        using var rig = new AiChatRig(history: true);
        rig.Click(rig.Chat.NewChatButton!);
        rig.NewChats.ShouldBe(1);
        rig.Chat.CurrentConversation.ShouldBeNull();
        rig.Chat.TitlePill!.Title.ShouldBe("New chat");
        rig.Chat.EmptyState!.IsShown.ShouldBeTrue();
        rig.Views.ShouldBeEmpty();
        rig.HasFocus(rig.Editor).ShouldBeTrue();
        rig.Announcement.ShouldBe("New chat");
        rig.Chat.Conversations.Count.ShouldBe(5);
    }

    [AvaloniaFact]
    public void New_Chat_Handled_Leaves_It_To_The_Host_And_Runs_The_Command()
    {
        var ran = 0;
        using var rig = new AiChatRig(chat =>
        {
            chat.NewChatRequested += (_, e) => e.Handled = true;
            chat.NewChatCommand = new RelayCommand(() => ran++);
        }, history: true);
        var current = rig.Chat.CurrentConversation;
        rig.Click(rig.Chat.NewChatButton!);
        rig.Chat.CurrentConversation.ShouldBeSameAs(current);
        ran.ShouldBe(1);
    }

    [AvaloniaFact]
    public void New_Chat_From_The_Menu_Does_The_Same()
    {
        using var rig = new AiChatRig(history: true);
        var pill = rig.Chat.TitlePill!;
        pill.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Key(PhysicalKey.Enter);
        rig.NewChats.ShouldBe(1);
        rig.Chat.CurrentConversation.ShouldBeNull();
        rig.HasFocus(rig.Editor).ShouldBeTrue();
    }

    #endregion

    #region Models

    [AvaloniaFact]
    public void Picking_A_Model_Selects_It_Raises_ModelChanged_And_Announces()
    {
        using var rig = new AiChatRig(history: true);
        var pill = rig.Chat.ModelPillControl!;
        pill.Appearance.ShouldBe(ModelPillAppearance.Header);
        var menu = rig.Chat.ModelMenuControl!;
        menu.Layout.ShouldBe(ModelMenuLayout.Simple);
        pill.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        menu.IsOpen.ShouldBeTrue();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.ArrowDown);
        rig.Key(PhysicalKey.Enter);
        rig.Chat.SelectedModel!.Id.ShouldBe("quill-2");
        rig.ModelChanges.Single().NewModel!.Id.ShouldBe("quill-2");
        rig.ModelChanges[0].OldModel!.Id.ShouldBe("orbit-pro");
        rig.Announcement.ShouldBe("Quill 2.4 selected. New replies use it.");
        rig.HasFocus(pill).ShouldBeTrue();
        AutomationProperties.GetName(pill).ShouldBe("Model: Quill 2.4");

        rig.Send("Which one wrote this?");
        rig.LastReply.Reply!.ModelId.ShouldBe("quill-2");
        rig.LastReply.Mark.Mark.ShouldBeSameAs(AiChatSamples.QuillMark);
        rig.Views[0].ReplyView!.Mark.Mark.ShouldBeSameAs(AiChatSamples.OrbitMark);
        rig.Call.Request.Model!.Id.ShouldBe("quill-2");
    }

    [AvaloniaFact]
    public void Without_Models_The_Model_Button_Is_Hidden()
    {
        using var rig = new AiChatRig(chat => chat.Models.Clear());
        rig.Chat.ModelPillControl!.IsVisible.ShouldBeFalse();
        rig.Chat.SelectedModel.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_First_Model_Is_Selected_Once()
    {
        using var rig = new AiChatRig();
        rig.Chat.SelectedModel!.Id.ShouldBe("orbit-pro");
    }

    #endregion

    #region Ratings and copying

    [AvaloniaFact]
    public void Ratings_Are_Exclusive_Take_Back_And_Announce()
    {
        using var rig = new AiChatRig(history: true);
        var actions = rig.LastReply.Actions!;
        var reply = rig.LastReply.Reply!;
        rig.Click(actions.GoodButton!);
        reply.Feedback.ShouldBe(AiChatFeedback.Up);
        rig.Feedback.Last().Value.ShouldBe(AiChatFeedback.Up);
        rig.Announcement.ShouldBe("Feedback sent: good response");
        rig.Click(actions.BadButton!);
        reply.Feedback.ShouldBe(AiChatFeedback.Down);
        actions.GoodButton!.IsPressedOn.ShouldBeFalse();
        rig.Announcement.ShouldBe("Feedback sent: bad response");
        rig.Click(actions.BadButton!);
        reply.Feedback.ShouldBe(AiChatFeedback.None);
        rig.Announcement.ShouldBe("Feedback removed");
        rig.Feedback.Count.ShouldBe(3);
        // Data set by the host shows too.
        reply.Feedback = AiChatFeedback.Up;
        actions.GoodButton.IsPressedOn.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Copy_Writes_The_Reply_Or_The_Message_And_Announces()
    {
        using var rig = new AiChatRig(history: true);
        rig.Click(rig.LastReply.Actions!.CopyButton!);
        rig.Copied.Single().ShouldBe(AiChatSamples.SecondText);
        rig.Announcement.ShouldBe("Copied to clipboard");
        rig.LastReply.Actions.IsCopied.ShouldBeTrue();

        rig.LastView.User!.Copy();
        rig.Copied.Last().ShouldBe(AiChatSamples.SecondPrompt);
    }

    #endregion

    #region Citations

    [AvaloniaFact]
    public void A_Citation_Raises_CitationInvoked_And_Opens_Its_Address_Unless_Handled()
    {
        using var rig = new AiChatRig(history: true);
        var source = AiChatSamples.Sources()[0];
        rig.Views[0].ReplyView!.Text!.RaiseEvent(new AiReplyTextSourceEventArgs(AiReplyText.CitationInvokedEvent, rig.Views[0].ReplyView!.Text!, source, 1));
        rig.Citations.Single().ShouldBeSameAs(source);
        rig.Opened.Single().ShouldBe(source.Url);

        rig.Chat.CitationInvoked += (_, e) => e.Handled = true;
        var detail = new AiChatSourceEventArgs(AiToolDetail.SourceInvokedEvent, rig.Views[0], source);
        rig.Views[0].RaiseEvent(detail);
        rig.Citations.Count.ShouldBe(2);
        rig.Opened.Count.ShouldBe(1);
    }

    #endregion

    #region Following

    [AvaloniaFact]
    public void Sending_Follows_The_Newest_Message_Again()
    {
        using var rig = new AiChatRig(history: true, height: 600);
        var scroller = rig.Chat.Scroller!;
        var point = rig.CenterOf(scroller);
        for (var i = 0; i < 10; i++)
        {
            rig.Window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.None);
        }
        rig.Settle();
        rig.Chat.IsFollowingLatest.ShouldBeFalse();
        scroller.IsJumpButtonShown.ShouldBeTrue();
        rig.Send("Back to the bottom");
        rig.Chat.IsFollowingLatest.ShouldBeTrue();
        rig.Run(1);
        scroller.DistanceToBottom.ShouldBeLessThan(1);
    }

    [AvaloniaFact]
    public void ScrollToLatest_Follows_Again()
    {
        using var rig = new AiChatRig(history: true, height: 600);
        var scroller = rig.Chat.Scroller!;
        var point = rig.CenterOf(scroller);
        for (var i = 0; i < 10; i++)
        {
            rig.Window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.None);
        }
        rig.Settle();
        rig.Chat.ScrollToLatest();
        rig.Run(1);
        rig.Chat.IsFollowingLatest.ShouldBeTrue();
        scroller.DistanceToBottom.ShouldBeLessThan(1);
        AutomationProperties.GetName(scroller.JumpButton!).ShouldBe("Scroll to latest message");
    }

    #endregion

    private sealed class RelayCommand(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }

    #region Away from the window

    /// <summary>Takes <paramref name="control"/> out of its window, as a closed tab or a page navigated away from would.</summary>
    private static void Away(Avalonia.Controls.Control control)
    {
        ((Avalonia.Controls.Window)Avalonia.Controls.TopLevel.GetTopLevel(control)!).Content = null;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void A_Chat_That_Switched_Conversations_While_Away_Is_Collected_While_They_Live_On()
    {
        var history = AiChatSamples.History();
        var weak = TestHelpers.ShowAndClose(() =>
        {
            var chat = AiChatSamples.Chat();
            chat.Conversations.AddRange(history);
            chat.CurrentConversation = history[0];
            return chat;
        }, chat =>
        {
            Away(chat);
            // A bound current conversation that changes while the chat is out of the window.
            chat.CurrentConversation = history[1];
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        history[1].Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = "And the CDN?" },
            new AiChatReply { Status = AiChatReplyStatus.Done, Text = "Update its origin." }));
    }

    [AvaloniaFact]
    public void A_Chat_Put_Back_Shows_The_Conversation_Chosen_While_Away_And_Follows_It()
    {
        using var rig = new AiChatRig(history: true);
        var layers = rig.Window.Content;
        rig.Window.Content = null;
        rig.Settle();
        var bucket = rig.Chat.Conversations[1];
        rig.Chat.CurrentConversation = bucket;
        rig.Window.Content = layers;
        rig.Settle();
        rig.Views.Count.ShouldBe(1);
        rig.Views[0].User!.Message!.Text.ShouldBe("What breaks if we rename the staging bucket?");

        bucket.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = "And the CDN?" },
            new AiChatReply { Status = AiChatReplyStatus.Done, Text = "Update its origin." }));
        rig.Settle();
        rig.Views.Count.ShouldBe(2);
        rig.Views[1].User!.Message!.Text.ShouldBe("And the CDN?");
    }

    #endregion
}
