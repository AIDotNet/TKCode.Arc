using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiChat"/> layout and behavior around it: the width breakpoints and side paddings, motion levels, the keyboard
/// order and focus, long conversations made lazily, and a streamed reply that does not lay out the rest of the thread.
/// </summary>
public class AiChatLayoutTests
{
    private static AiChatConversation Long(int turns)
    {
        var conversation = new AiChatConversation { Id = "long", Title = "Long one" };
        for (var i = 0; i < turns; i++)
        {
            conversation.Turns.Add(new AiChatTurn(new AiChatUserMessage { Text = $"Question {i + 1}" },
                new AiChatReply { ModelId = "orbit-pro", Status = AiChatReplyStatus.Done, ThoughtFor = TimeSpan.FromSeconds(1), Text = $"Answer {i + 1}." }));
        }
        return conversation;
    }

    #region Breakpoints

    [AvaloniaTheory]
    [InlineData(600, 27, 17, 19)]
    [InlineData(390, 17.55, 7.55, 9.55)]
    [InlineData(320, 16, 6, 8)]
    public void The_Side_Paddings_Follow_The_Width(double width, double thread, double header, double dock)
    {
        using var rig = new AiChatRig(history: true, width: width);
        rig.Chat.ThreadHost!.Padding.Left.ShouldBe(thread, 0.01);
        rig.Chat.Header!.Padding.Left.ShouldBe(header, 0.01);
        rig.Chat.Dock!.Padding.Left.ShouldBe(dock, 0.01);
        rig.Chat.ThreadHost.Bounds.Width.ShouldBe(width, 0.5);
    }

    [AvaloniaFact]
    public void At_480_The_Chat_Is_Narrow_Tighter_And_The_Bubbles_Wider()
    {
        using var rig = new AiChatRig(history: true, width: 480);
        rig.Chat.Classes.Contains(AiChat.NarrowPseudoClass).ShouldBeTrue();
        rig.Chat.Classes.Contains(AiChat.CompactPseudoClass).ShouldBeFalse();
        rig.Chat.TurnsPanel!.Spacing.ShouldBe(24);
        rig.Chat.ThreadHost!.Padding.Top.ShouldBe(18);
        rig.Views[0].User!.IsNarrow.ShouldBeTrue();
        rig.Views[0].ReplyView!.Details.ShouldAllBe(d => d.IsNarrow);

        rig.Window.Width = 481;
        rig.Settle();
        rig.Chat.Classes.Contains(AiChat.NarrowPseudoClass).ShouldBeFalse();
        rig.Chat.TurnsPanel.Spacing.ShouldBe(28);
        rig.Views[0].User!.IsNarrow.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void At_360_The_Model_Pill_Tightens_And_The_Title_Truncates_First()
    {
        using var rig = new AiChatRig(history: true, width: 360);
        rig.Chat.Classes.Contains(AiChat.CompactPseudoClass).ShouldBeTrue();
        rig.Chat.ModelPillControl!.Padding.ShouldBe(new Thickness(8, 0, 6, 0));
        var title = rig.Chat.TitlePill!;
        var model = rig.Chat.ModelPillControl;
        (title.Bounds.Right).ShouldBeLessThanOrEqualTo(model.TranslatePoint(default, title.Parent as Visual ?? title)!.Value.X + 0.5);
        rig.Chat.NewChatButton!.Bounds.Width.ShouldBe(34);

        rig.Window.Width = 500;
        rig.Settle();
        rig.Chat.ModelPillControl.Padding.ShouldBe(new Thickness(10, 0, 8, 0));
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void At_Full_Motion_A_New_Turn_Fades_In_Over_160ms_Without_Moving()
    {
        using var rig = new AiChatRig(level: ArcMotionLevel.Full, history: true);
        rig.Send("A new question");
        var view = rig.LastView;
        view.ContentOpacity.ShouldBeLessThan(0.5);
        rig.Run(0.08);
        view.ContentOpacity.ShouldBeGreaterThan(0.5);
        view.ContentOpacity.ShouldBeLessThan(1);
        rig.Run(0.12);
        view.ContentOpacity.ShouldBe(1);
        view.RenderTransform.ShouldBeNull();
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_A_New_Turn_Fades_In_120ms_Linear_And_Off_Shows_It_At_Once()
    {
        using (var rig = new AiChatRig(level: ArcMotionLevel.Reduced, history: true))
        {
            rig.Send("A new question");
            var view = rig.LastView;
            rig.Run(0.06);
            view.ContentOpacity.ShouldBe(0.5, 0.15);
            rig.Run(0.08);
            view.ContentOpacity.ShouldBe(1);
        }
        using (var rig = new AiChatRig(level: ArcMotionLevel.Off, history: true))
        {
            rig.Send("A new question");
            rig.LastView.ContentOpacity.ShouldBe(1);
        }
    }

    [AvaloniaFact]
    public void At_Full_Motion_A_Version_Switch_Fades_The_Turn_In_And_Its_Height_Springs()
    {
        using var rig = new AiChatRig(level: ArcMotionLevel.Full, history: true);
        var first = rig.Views[0];
        var turn = first.Turn!;
        var height = first.Bounds.Height;
        rig.Chat.Retry(turn);
        rig.Run(0.05);
        first.ContentOpacity.ShouldBeLessThan(1);
        first.Sizer!.IsMoving.ShouldBeTrue();
        first.Bounds.Height.ShouldBeLessThan(height);
        first.Bounds.Height.ShouldBeGreaterThan(first.Sizer.Child!.DesiredSize.Height);
        rig.Run(1);
        first.Sizer.IsMoving.ShouldBeFalse();
        first.ContentOpacity.ShouldBe(1);
        first.Bounds.Height.ShouldBe(first.Sizer.Child!.DesiredSize.Height, 0.5);
    }

    [AvaloniaFact]
    public void At_Off_Switching_Conversations_Is_Immediate()
    {
        using var rig = new AiChatRig(history: true);
        rig.Chat.CurrentConversation = rig.Chat.Conversations[3];
        rig.Settle();
        rig.Views.Single().User!.Message!.Text.ShouldStartWith("Summarize");
        rig.Chat.Pane!.Opacity.ShouldBe(1);
    }

    [AvaloniaFact]
    public void At_Reduced_Motion_Streamed_Text_Keeps_Its_Cadence_Without_Fading()
    {
        using var rig = new AiChatRig(level: ArcMotionLevel.Reduced);
        rig.Send("Pace it");
        rig.Call.Text("One two three four five six seven eight nine ");
        rig.Run(0.02);
        string.Concat(rig.LastReply.Text!.AppendedPieces).ShouldBe("One two three ");
        rig.Run(0.05);
        string.Concat(rig.LastReply.Text.AppendedPieces).ShouldBe("One two three four five six ");
        rig.LastReply.Text.Body!.Stream!.State.FadeDuration.ShouldBe(TimeSpan.Zero);
    }

    #endregion

    #region Keyboard and focus

    [AvaloniaFact]
    public void Tab_Goes_Through_The_Header_Then_The_Thread_Then_The_Composer()
    {
        using var rig = new AiChatRig(history: true);
        var chat = rig.Chat;
        chat.TitlePill!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.HasFocus(chat.ModelPillControl!).ShouldBeTrue();
        rig.Key(PhysicalKey.Tab);
        rig.HasFocus(chat.NewChatButton!).ShouldBeTrue();
        rig.Key(PhysicalKey.Tab);
        // The first control of the thread: the reasoning button of the first reply (the person's Copy and Edit show
        // with the focus).
        rig.HasFocus(chat.TurnsPanel!).ShouldBeTrue();

        // From the last thread control the next stops are the paperclip, the text and the send button.
        rig.LastReply.Actions!.BadButton!.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Tab);
        rig.HasFocus(rig.Composer).ShouldBeTrue();
        rig.HasFocus(rig.Composer.PlusButton!).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Focus_Inside_A_Turn_Reveals_Its_Reply_Actions()
    {
        using var rig = new AiChatRig(history: true);
        var first = rig.Views[0].ReplyView!;
        first.Actions!.IsShown.ShouldBeFalse();
        first.Reasoning!.Button!.Focus(NavigationMethod.Tab);
        rig.Settle();
        first.Actions.IsShown.ShouldBeTrue();
        rig.LastReply.Actions!.IsShown.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Hovering_A_Turn_Reveals_Its_Reply_Actions()
    {
        using var rig = new AiChatRig(history: true, height: 2400);
        var first = rig.Views[0];
        rig.Hover(first.ReplyView!.Reasoning!);
        first.ReplyView.Actions!.IsShown.ShouldBeTrue();
        rig.Hover(rig.Chat.Header!);
        first.ReplyView.Actions.IsShown.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void After_A_Touch_The_Message_Actions_Always_Show_Until_A_Mouse_Moves()
    {
        using var rig = new AiChatRig(history: true);
        var first = rig.Views[0];
        first.ReplyView!.Actions!.IsShown.ShouldBeFalse();
        rig.Chat.SetTouch(true);
        first.ReplyView.Actions.IsShown.ShouldBeTrue();
        first.User!.ShowsActions.ShouldBeTrue();
        rig.Send("New turns follow it");
        rig.Settle();
        rig.LastView.User!.ShowsActions.ShouldBeTrue();
        rig.Hover(rig.Chat.Header!);
        first.ReplyView.Actions.IsShown.ShouldBeFalse();
        first.User.ShowsActions.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Cancelling_An_Edit_With_Escape_Gives_The_Focus_Back_To_Edit()
    {
        using var rig = new AiChatRig(history: true);
        var bubble = rig.LastView.User!;
        bubble.IsRevealed = true;
        rig.Settle();
        var edit = bubble.Row!.EditButton!;
        edit.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        bubble.IsEditing.ShouldBeTrue();
        rig.Key(PhysicalKey.Escape);
        bubble.IsEditing.ShouldBeFalse();
        rig.HasFocus(bubble.Row!.EditButton!).ShouldBeTrue();
        rig.LastView.Turn!.Versions.Count.ShouldBe(1);
    }

    #endregion

    #region Long conversations and performance

    [AvaloniaFact]
    public void A_Long_Conversation_Makes_Views_For_Its_Newest_Turns_And_The_Older_Ones_When_Scrolled_To()
    {
        var conversation = Long(100);
        using var rig = new AiChatRig(chat =>
        {
            chat.Conversations.Add(conversation);
            chat.CurrentConversation = conversation;
        }, height: 600);
        rig.Chat.Branch.Count.ShouldBe(100);
        rig.Views.Count.ShouldBe(30);
        rig.Chat.FirstShownIndex.ShouldBe(70);
        AutomationProperties.GetName(rig.Views[0]).ShouldBe("Message 71 of 100");
        rig.Views[^1].User!.Message!.Text.ShouldBe("Question 100");

        var scroll = rig.Chat.Scroller!.Scroll!;
        var first = rig.Views[0];
        var before = first.TranslatePoint(default, rig.Chat)!.Value.Y;
        rig.Chat.ShowEarlier();
        rig.Settle();
        rig.Views.Count.ShouldBe(50);
        rig.Chat.FirstShownIndex.ShouldBe(50);
        AutomationProperties.GetName(rig.Views[0]).ShouldBe("Message 51 of 100");
        // What the person looked at stays in place.
        first.TranslatePoint(default, rig.Chat)!.Value.Y.ShouldBe(before, 1);
        scroll.Offset.Y.ShouldBeGreaterThan(0);

        // A new turn keeps the made views.
        rig.Send("One more");
        rig.Views.Count.ShouldBe(51);
        rig.Chat.FirstShownIndex.ShouldBe(50);
    }

    [AvaloniaFact]
    public void Streaming_Lays_Out_Only_The_Streaming_Turn()
    {
        using var rig = new AiChatRig(history: true);
        rig.Send("Stream this");
        rig.Settle();
        var earlier = rig.Views.Take(2).ToList();
        var counts = earlier.Select(v => v.MeasureCount).ToList();
        rig.Call.Text("A long streamed answer that keeps growing word by word while the reply is shown on the clock, " +
                      "line after line, until the whole text has been released and the reply is done.");
        rig.Call.End();
        rig.RunUntil(() => rig.LastReply.Reply!.Status == AiChatReplyStatus.Done).ShouldBeTrue();
        earlier.Select(v => v.MeasureCount).ShouldBe(counts);
    }

    #endregion
}
