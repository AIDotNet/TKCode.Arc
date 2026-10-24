using System.Collections.ObjectModel;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Activity;

public class ChatThreadTests
{
    private static readonly TimeZoneInfo s_utc = TimeZoneInfo.Utc;
    private static readonly DateTimeOffset s_now = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private static ChatParticipant[] People() =>
    [
        new("you", "You"),
        new("sam", "Sam Rivera"),
        new("lee", "Lee Chen")
    ];

    private static ChatMessage At(string id, string author, string text, DateTimeOffset at) => new(id, author, text, at);

    /// <summary>Yesterday one message; today Sam twice within five minutes, then Lee, then the current user.</summary>
    private static List<ChatMessage> Conversation() =>
    [
        At("m1", "sam", "Morning! Any news on the export?", s_now.AddDays(-1).AddHours(-3)),
        At("m2", "sam", "The CSV still drops the last row.", s_now.AddMinutes(-30)),
        At("m3", "sam", "Repro attached.", s_now.AddMinutes(-28)),
        At("m4", "lee", "Looking at it now.", s_now.AddMinutes(-20)),
        At("m5", "you", "Thanks both!", s_now.AddMinutes(-10))
    ];

    private static ChatThread NewThread() => new()
    {
        Participants = People(),
        CurrentUserId = "you",
        TimeZone = s_utc,
        Culture = CultureInfo.GetCultureInfo("en-US"),
        Width = 520,
        Height = 560,
        Now = static () => s_now
    };

    private static (ChatThread Thread, Window Window) Show(IEnumerable<ChatMessage>? messages = null, ArcMotionLevel level = ArcMotionLevel.Off,
                                                         bool bound = false)
    {
        var thread = NewThread();
        var list = messages?.ToList() ?? Conversation();
        if (bound)
        {
            thread.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<ChatMessage>(list);
        }
        else
        {
            foreach (var message in list)
            {
                thread.Items.Add(message);
            }
        }
        var window = TestHelpers.Show(thread, level);
        window.Width = 700;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return (thread, window);
    }

    private static ChatThreadMessageRow Row(ChatThread thread, string id) =>
        thread.Rows.OfType<ChatThreadMessageRow>().Single(r => r.Message.Id == id);

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var thread = new ChatThread();
        thread.Placeholder.ShouldBe("Message");
        thread.Label.ShouldBe("Conversation");
        thread.GroupWindow.ShouldBe(TimeSpan.FromMinutes(5));
        thread.Reactions.ShouldBe(["👍", "❤️", "😂", "🎉", "👀", "🙏"]);
        thread.IsComposerVisible.ShouldBeTrue();
        thread.IsAttachmentEnabled.ShouldBeTrue();
        thread.Labels.Retry.ShouldBe("Retry");
        thread.Labels.Typing(["Sam"]).ShouldContain("Sam");
    }

    [AvaloniaFact]
    public void Messages_Group_By_Author_And_Time_With_Day_Separators()
    {
        var (thread, window) = Show();
        try
        {
            var rows = thread.Rows;
            rows.OfType<ChatThreadDayRow>().Select(d => d.Label).ShouldBe(["Yesterday", "Today"]);
            rows[0].ShouldBeOfType<ChatThreadDayRow>();

            // Sam's two messages two minutes apart are one group: the first opens it, the second closes it.
            Row(thread, "m2").First.ShouldBeTrue();
            Row(thread, "m2").Last.ShouldBeFalse();
            Row(thread, "m3").First.ShouldBeFalse();
            Row(thread, "m3").Last.ShouldBeTrue();
            Row(thread, "m2").AfterDay.ShouldBeTrue();
            // Another author starts a new group; the current user's messages are "mine".
            Row(thread, "m4").First.ShouldBeTrue();
            Row(thread, "m5").Mine.ShouldBeTrue();
            Row(thread, "m4").Mine.ShouldBeFalse();
            // Only the current user's last message shows its delivery status.
            Row(thread, "m5").ShowsStatus.ShouldBeTrue();
            Row(thread, "m3").ShowsStatus.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Gap_Longer_Than_The_Group_Window_Starts_A_New_Group()
    {
        var messages = Conversation();
        messages[2].CreatedAt = messages[1].CreatedAt.AddMinutes(6);
        var (thread, window) = Show(messages);
        try
        {
            Row(thread, "m3").First.ShouldBeTrue();
            Row(thread, "m2").Last.ShouldBeTrue();
            thread.GroupWindow = TimeSpan.FromMinutes(10);
            Dispatcher.UIThread.RunJobs();
            Row(thread, "m3").First.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Sends_Shift_Enter_Does_Not_And_An_Unbound_Thread_Appends_The_Message()
    {
        var (thread, window) = Show();
        ChatDraft? sent = null;
        thread.SendRequested += (_, e) => sent = e.Draft;
        try
        {
            var box = thread.Composer!.TextBox!;
            box.Focus();
            window.KeyTextInput("Ship it");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            sent.ShouldBeNull();

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sent.ShouldNotBeNull();
            sent!.Text.ShouldBe("Ship it");
            var last = thread.Items.OfType<ChatMessage>().Last();
            last.Text.ShouldBe("Ship it");
            last.AuthorId.ShouldBe("you");
            last.CreatedAt.ShouldBe(s_now);
            box.Text.ShouldBeNullOrEmpty();
            thread.Rows.OfType<ChatThreadMessageRow>().Last().Message.ShouldBeSameAs(last);
            // The log region announces only the new message.
            thread.LogRegion!.LastAnnouncement.ShouldBe("You: Ship it");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bound_Thread_Raises_Events_And_Leaves_Its_Messages_To_The_App()
    {
        var (thread, window) = Show(bound: true);
        var reacted = new List<string>();
        thread.ReactionToggled += (_, e) => reacted.Add($"{e.Message.Id}:{e.Emoji}");
        try
        {
            var message = ((IEnumerable<ChatMessage>)thread.ItemsSource!).First(m => m.Id == "m4");
            thread.React(message, "👍");
            reacted.ShouldBe(["m4:👍"]);
            message.Reactions.ShouldBeNull();

            thread.Send(new ChatDraft("hello", [], []));
            ((IEnumerable<ChatMessage>)thread.ItemsSource!).Count().ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reactions_Toggle_In_Place_When_Unbound()
    {
        var (thread, window) = Show();
        try
        {
            var message = thread.Items.OfType<ChatMessage>().First(m => m.Id == "m4");
            message.Reactions = [new ChatReaction("👍", 2)];
            thread.React(message, "👍");
            message.Reactions!.Single().ShouldBe(new ChatReaction("👍", 3, true));
            thread.React(message, "👍");
            message.Reactions!.Single().ShouldBe(new ChatReaction("👍", 2, false));
            thread.React(message, "🎉");
            message.Reactions!.Select(r => r.Emoji).ShouldBe(["👍", "🎉"]);
            thread.React(message, "🎉");
            message.Reactions!.Select(r => r.Emoji).ShouldBe(["👍"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Failed_Last_Message_Offers_Retry()
    {
        var messages = Conversation();
        messages[^1].Status = ChatMessageStatus.Failed;
        var (thread, window) = Show(messages);
        ChatMessage? retried = null;
        thread.RetryRequested += (_, e) => retried = e.Message;
        try
        {
            Row(thread, "m5").Status.ShouldBe(ChatMessageStatus.Failed);
            thread.HasRetryHandler.ShouldBeTrue();
            var view = thread.ViewFor(messages[^1])!;
            view.Classes.ShouldContain(ChatThreadMessageView.FailedPseudoClass);
            var retry = view.GetVisualDescendants().OfType<Avalonia.Controls.Button>()
                            .First(b => b.Name == ChatThreadMessageView.RetryPart);
            retry.IsVisible.ShouldBeTrue();
            retry.Command?.Execute(null);
            if (retried is null)
            {
                retry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
            }
            retried.ShouldBeSameAs(messages[^1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Read_Receipts_Sit_Under_Each_Readers_Last_Read_Message()
    {
        var (thread, window) = Show();
        try
        {
            thread.ReadBy = new Dictionary<string, string> { ["sam"] = "m5", ["lee"] = "m3" };
            Dispatcher.UIThread.RunJobs();
            Row(thread, "m5").Readers.Select(p => p.Id).ShouldBe(["sam"]);
            Row(thread, "m3").Readers.Select(p => p.Id).ShouldBe(["lee"]);
            // A receipt on the current user's last message reads as read.
            Row(thread, "m5").Status.ShouldBe(ChatMessageStatus.Read);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_Adds_A_Row_And_Is_Announced_In_Its_Own_Region()
    {
        var (thread, window) = Show();
        try
        {
            thread.TypingParticipantIds = ["sam"];
            Dispatcher.UIThread.RunJobs();
            thread.Rows[^1].ShouldBeOfType<ChatThreadTypingRow>();
            thread.TypingRegion!.LastAnnouncement.ShouldBe(thread.Labels.Typing(["Sam Rivera"]));
            thread.LogRegion!.LastAnnouncement.ShouldBeNullOrEmpty();

            thread.TypingParticipantIds = [];
            Dispatcher.UIThread.RunJobs();
            thread.Rows.OfType<ChatThreadTypingRow>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Message_List_Is_A_Polite_Log()
    {
        var (thread, window) = Show();
        try
        {
            var list = thread.List!;
            AutomationProperties.GetLiveSetting(list).ShouldBe(AutomationLiveSetting.Polite);
            ControlAutomationPeer.CreatePeerForElement(list).GetAutomationControlType().ShouldBe(AutomationControlType.List);
            AutomationProperties.GetName(list).ShouldBe("Conversation");
            thread.Label = "Support chat";
            AutomationProperties.GetName(list).ShouldBe("Support chat");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Streamed_Reply_Feeds_The_Bubble_Markdown_Incrementally()
    {
        var reply = At("r1", "sam", "", s_now.AddMinutes(-1));
        var messages = Conversation();
        messages.Add(reply);
        var (thread, window) = Show(messages);
        try
        {
            thread.AppendToMessage("missing", "x").ShouldBeFalse();
            thread.AppendToMessage("r1", "Here is the **fix**").ShouldBeTrue();
            thread.IsMessageStreaming("r1").ShouldBeTrue();
            var markdown = thread.Bodies.Peek(reply)!;
            markdown.IsStreaming.ShouldBeTrue();
            thread.AppendToMessage("r1", ":\n\n```csharp\nvar x = 1;\n```\n").ShouldBeTrue();
            reply.Text.ShouldBe("Here is the **fix**:\n\n```csharp\nvar x = 1;\n```\n");
            // The same Markdown keeps streaming: nothing was rebuilt from scratch.
            thread.Bodies.Peek(reply).ShouldBeSameAs(markdown);

            thread.CompleteMessage("r1").ShouldBeTrue();
            thread.IsMessageStreaming("r1").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_Messages_Render_Without_Markdown_When_Asked()
    {
        var (thread, window) = Show();
        try
        {
            thread.UsesMarkdown(thread.Items.OfType<ChatMessage>().First()).ShouldBeTrue();
            thread.MessageFormat = ChatMessageFormat.PlainText;
            Dispatcher.UIThread.RunJobs();
            thread.UsesMarkdown(thread.Items.OfType<ChatMessage>().First()).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Thousand_Messages_Realize_Only_The_Rows_In_View()
    {
        var authors = new[] { "you", "sam", "lee" };
        var messages = Enumerable.Range(0, 1000)
                                 .Select(i => At($"n{i}", authors[i % 3], $"Message number {i}", s_now.AddMinutes(-1000 + i)))
                                 .ToList();
        var (thread, window) = Show(messages);
        try
        {
            thread.Rows.Count.ShouldBeGreaterThan(1000);
            var realized = thread.List!.GetRealizedContainers().Count();
            realized.ShouldBeGreaterThan(0);
            realized.ShouldBeLessThan(60);
            thread.Bodies.Count.ShouldBeLessThanOrEqualTo(64);
            // Pinned to the newest message.
            thread.IsPinned.ShouldBeTrue();
            thread.ViewFor(messages[^1]).ShouldNotBeNull();
            thread.ViewFor(messages[0]).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Messages_Out_Of_View_Raise_The_Pill_And_ScrollToBottom_Clears_It()
    {
        var messages = Enumerable.Range(0, 60)
                                 .Select(i => At($"n{i}", i % 2 == 0 ? "sam" : "you", $"Line {i}", s_now.AddMinutes(-120 + i)))
                                 .ToList();
        var (thread, window) = Show(messages);
        try
        {
            // The reader scrolls up: rows come into view and re-estimate the extent, which must not snap back.
            thread.Scroller!.Offset = new Vector(0, 0);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            thread.IsPinned.ShouldBeFalse();
            thread.Scroller.Offset.Y.ShouldBeLessThan(1);

            thread.Items.Add(At("late", "lee", "Did anyone see this?", s_now));
            Dispatcher.UIThread.RunJobs();
            thread.UnreadCount.ShouldBe(1);
            thread.PillHost!.IsPresent.ShouldBeTrue();

            thread.ScrollToBottom();
            Dispatcher.UIThread.RunJobs();
            thread.IsPinned.ShouldBeTrue();
            thread.UnreadCount.ShouldBe(0);
            var scroller = thread.Scroller!;
            (scroller.Extent.Height - scroller.Offset.Y - scroller.Viewport.Height).ShouldBeLessThan(1);

            // Pinned again: later messages (several lines tall) keep the newest message in view.
            for (var i = 0; i < 3; i++)
            {
                thread.Items.Add(At($"more{i}", "sam", "A longer reply\nthat spans\nthree lines", s_now));
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
            thread.IsPinned.ShouldBeTrue();
            (scroller.Extent.Height - scroller.Offset.Y - scroller.Viewport.Height).ShouldBeLessThan(1);
            thread.UnreadCount.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picker_Opens_From_The_Add_Button_And_Picks_An_Emoji()
    {
        var (thread, window) = Show();
        var reacted = new List<string>();
        thread.ReactionToggled += (_, e) => reacted.Add(e.Emoji);
        try
        {
            var message = thread.Items.OfType<ChatMessage>().First(m => m.Id == "m4");
            var view = thread.ViewFor(message)!;
            thread.TogglePicker(view, message);
            Dispatcher.UIThread.RunJobs();
            thread.IsPickerOpenFor(message).ShouldBeTrue();
            var buttons = thread.Picker!.Buttons;
            buttons.Count.ShouldBe(6);
            buttons[0].IsFocused.ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            buttons[^1].IsFocused.ShouldBeTrue(); // wraps

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            thread.IsPickerOpenFor(message).ShouldBeFalse();

            thread.React(message, "🎉");
            reacted.ShouldBe(["🎉"]);
            message.Reactions!.Single().Emoji.ShouldBe("🎉");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrivals_Animate_At_Full_Motion_And_Settle()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (thread, window) = Show(level: ArcMotionLevel.Full);
        try
        {
            var message = At("fresh", "sam", "Just landed", s_now);
            thread.Items.Add(message);
            Dispatcher.UIThread.RunJobs();
            var view = thread.ViewFor(message);
            view.ShouldNotBeNull();
            clock.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(1000.0 / 60));
            Dispatcher.UIThread.RunJobs();
            view!.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Thread_Is_Collected_While_Its_Messages_Live_On()
    {
        var messages = new ObservableCollection<ChatMessage>(Conversation());
        var typing = new ObservableCollection<string> { "sam" };
        var withMessages = TestHelpers.ShowAndClose(() =>
        {
            var thread = NewThread();
            thread.ItemsSource = messages;
            return thread;
        });
        var withTyping = TestHelpers.ShowAndClose(() =>
        {
            var thread = NewThread();
            thread.TypingParticipantIds = typing;
            return thread;
        });
        TestHelpers.Collect(withMessages, withTyping);
        withMessages.IsAlive.ShouldBeFalse();
        withTyping.IsAlive.ShouldBeFalse();
        messages[0].Text = "Edited";
        messages.Add(At("m6", "lee", "Shipped.", s_now));
        typing.Add("lee");
    }

    [AvaloniaFact]
    public void A_Thread_Taken_Out_And_Put_Back_Shows_What_Changed_Meanwhile()
    {
        var reply = At("r1", "sam", "", s_now.AddMinutes(-1));
        var list = Conversation();
        list.Add(reply);
        var lee = list[3];
        lee.Text = "Looking at **it** now.";
        var typing = new ObservableCollection<string>();
        var thread = NewThread();
        thread.ItemsSource = new ObservableCollection<ChatMessage>(list);
        thread.TypingParticipantIds = typing;
        var host = new Border { Child = thread };
        var window = TestHelpers.Show(host);
        window.Width = 700;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        try
        {
            thread.AppendToMessage("r1", "Part one").ShouldBeTrue();
            thread.Bodies.Peek(lee).ShouldNotBeNull();
            Row(thread, "m4").First.ShouldBeTrue();

            host.Child = null;
            Dispatcher.UIThread.RunJobs();
            lee.AuthorId = "sam";
            lee.CreatedAt = s_now.AddMinutes(-27);
            lee.Text = "**Fixed** it.";
            typing.Add("lee");
            thread.AppendToMessage("r1", " and two").ShouldBeTrue();
            Dispatcher.UIThread.RunJobs();

            host.Child = thread;
            Dispatcher.UIThread.RunJobs();
            // m4 now joins Sam's group, shows its new text, and Lee is typing; the streamed reply is still streaming.
            Row(thread, "m4").Author.Id.ShouldBe("sam");
            Row(thread, "m4").First.ShouldBeFalse();
            thread.Bodies.Peek(lee)!.Text.ShouldBe("**Fixed** it.");
            thread.Rows[^1].ShouldBeOfType<ChatThreadTypingRow>();
            thread.IsMessageStreaming("r1").ShouldBeTrue();
            reply.Text.ShouldBe("Part one and two");

            lee.AuthorId = "lee";
            lee.Text = "Looking at **it** now.";
            typing.Clear();
            Dispatcher.UIThread.RunJobs();
            Row(thread, "m4").Author.Id.ShouldBe("lee");
            Row(thread, "m4").First.ShouldBeTrue();
            thread.Bodies.Peek(lee)!.Text.ShouldBe("Looking at **it** now.");
            thread.Rows.OfType<ChatThreadTypingRow>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }
}
