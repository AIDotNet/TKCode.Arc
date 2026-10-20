using System.Collections.ObjectModel;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Pages;

public static class ChatThreadPage
{
    private static readonly ChatParticipant s_me = new("me", "Alex Kim");
    private static readonly ChatParticipant s_sam = new("sam", "Sam Rivera");
    private static readonly ChatParticipant s_lee = new("lee", "Lee Chen");
    private static readonly ChatParticipant s_arc = new("arc", "Arc Assistant");

    public static Control Create() => GalleryUi.Column(20,
        GalleryUi.Card("Support conversation",
            "Messages group by person and time with day separators; reactions, read receipts and the typing indicator follow the conversation. Hover a message to react; the last message failed, so it offers Retry.",
            Support()),
        GalleryUi.Card("Streaming Markdown reply",
            "Bodies render with Markdown. A reply streamed with AppendToMessage feeds only the new text to the bubble's Markdown, so long answers stay incremental.",
            Assistant()),
        GalleryUi.Card("A thousand messages",
            "Rows are virtualized and recycled: only the messages in view (plus a little margin) exist. Scroll up and new messages from others raise the pill.",
            History()),
        GalleryUi.Card("Composer on its own",
            "ChatComposer grows to six lines, takes files from the button, paste or drop, and sends with Enter (Shift+Enter for a new line).",
            ComposerAlone()));

    private static Border Frame(Control content, double height) =>
        Framed(content, height);

    internal static Border Framed(Control content, double height)
    {
        var frame = new Border
        {
            Height = height,
            MaxWidth = 640,
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = content
        };
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return frame;
    }

    private static Control Support()
    {
        var now = DateTimeOffset.Now;
        var yesterday = now.AddDays(-1).Date.AddHours(16);
        var today = now.Date.AddHours(Math.Min(now.Hour, 9));
        var messages = new ObservableCollection<ChatMessage>
        {
            new("m1", "sam", "Hi Alex! Thanks for reaching out about the billing export.", new DateTimeOffset(yesterday.AddMinutes(2))),
            new("m2", "sam", "Could you share the account email so I can look it up?", new DateTimeOffset(yesterday.AddMinutes(3))),
            new("m3", "me", "Sure, it's alex@northwind.dev", new DateTimeOffset(yesterday.AddMinutes(9))) { Status = ChatMessageStatus.Read },
            new("m4", "sam", "Found it. The export failed because of a malformed date in **row 214**. I've attached the cleaned file.", new DateTimeOffset(today.AddMinutes(1)))
            {
                Attachments = [new ChatAttachment("f1", "billing-export-fixed.csv", ChatAttachmentKind.File) { Size = 18_642 }],
                Reactions = [new ChatReaction("🙏", 1, true)]
            },
            new("m5", "lee", "Here's what the new export screen looks like:", new DateTimeOffset(today.AddMinutes(4)))
            {
                Attachments =
                [
                    new ChatAttachment("i1", "export-1.jpg", ChatAttachmentKind.Image, "https://picsum.photos/seed/arc-chat-1/480/480"),
                    new ChatAttachment("i2", "export-2.jpg", ChatAttachmentKind.Image, "https://picsum.photos/seed/arc-chat-2/480/480"),
                    new ChatAttachment("i3", "export-3.jpg", ChatAttachmentKind.Image, "https://picsum.photos/seed/arc-chat-3/480/480")
                ]
            },
            new("m6", "me", "That fixed it, thank you both! 🎉", new DateTimeOffset(today.AddMinutes(6)))
            {
                Status = ChatMessageStatus.Read,
                Reactions = [new ChatReaction("❤️", 2), new ChatReaction("🎉", 1)]
            },
            new("m7", "me", "One more thing: can I schedule it weekly?", new DateTimeOffset(today.AddMinutes(7))) { Status = ChatMessageStatus.Failed }
        };
        var typing = new ObservableCollection<string> { "sam" };
        var thread = new ChatThread
        {
            Participants = [s_me, s_sam, s_lee],
            CurrentUserId = "me",
            ItemsSource = messages,
            TypingParticipantIds = typing,
            ReadBy = new Dictionary<string, string> { ["sam"] = "m6", ["lee"] = "m6" },
            Label = "Support conversation"
        };
        thread.SendRequested += (_, e) =>
        {
            messages.Add(new ChatMessage($"s{messages.Count + 1}", "me", e.Draft.Text, DateTimeOffset.Now)
            {
                Attachments = e.Draft.Attachments.Count > 0 ? e.Draft.Attachments : null,
                Status = ChatMessageStatus.Sent
            });
        };
        thread.ReactionToggled += (_, e) => Toggle(e.Message, e.Emoji);
        thread.RetryRequested += (_, e) => e.Message.Status = ChatMessageStatus.Sent;

        var reply = new Button { Content = "Simulate a reply" };
        reply.Click += (_, _) =>
        {
            typing.Clear();
            messages.Add(new ChatMessage($"r{messages.Count + 1}", "sam", "Yes! Open Settings → Exports and pick Weekly.", DateTimeOffset.Now));
            thread.ReadBy = new Dictionary<string, string> { ["sam"] = messages[^2].Id, ["lee"] = "m6" };
        };
        var type = new Button { Content = "Toggle typing" };
        type.Click += (_, _) =>
        {
            if (typing.Count == 0)
            {
                typing.Add("sam");
            }
            else
            {
                typing.Clear();
            }
        };
        return GalleryUi.Column(12, Frame(thread, 560), GalleryUi.Row(reply, type));
    }

    private static void Toggle(ChatMessage message, string emoji)
    {
        var list = new List<ChatReaction>(message.Reactions ?? []);
        var at = list.FindIndex(r => r.Emoji == emoji);
        if (at < 0)
        {
            list.Add(new ChatReaction(emoji, 1, true));
        }
        else
        {
            list[at] = list[at] with { Count = list[at].Count + (list[at].Mine ? -1 : 1), Mine = !list[at].Mine };
        }
        message.Reactions = list.Where(r => r.Count > 0).ToArray();
    }

    private const string Reply = """
        Here's a small helper that **retries** a request with exponential backoff:

        ```cs
        async Task<T> RetryAsync<T>(Func<Task<T>> run, int attempts = 3)
        {
            for (var i = 1; ; i++)
            {
                try { return await run(); }
                catch when (i < attempts) { await Task.Delay(200 * (1 << i)); }
            }
        }
        ```

        - Keep `attempts` small for user-facing calls.
        - Log the last exception before giving up.
        """;

    private static Control Assistant()
    {
        var now = DateTimeOffset.Now;
        var messages = new ObservableCollection<ChatMessage>
        {
            new("a1", "me", "How do I retry a flaky HTTP call in C#?", now.AddMinutes(-2)) { Status = ChatMessageStatus.Read },
            new("a2", "arc", Reply, now.AddMinutes(-1))
        };
        var thread = new ChatThread
        {
            Participants = [s_me, s_arc],
            CurrentUserId = "me",
            ItemsSource = messages,
            IsAttachmentEnabled = false,
            Placeholder = "Ask anything",
            Label = "Assistant"
        };
        var count = 0;
        thread.SendRequested += (_, e) =>
        {
            messages.Add(new ChatMessage($"q{++count}", "me", e.Draft.Text, DateTimeOffset.Now) { Status = ChatMessageStatus.Sent });
            Stream(thread, messages, $"reply{count}");
        };
        var stream = new Button { Content = "Stream a reply" };
        stream.Click += (_, _) => Stream(thread, messages, $"reply{++count}");
        return GalleryUi.Column(12, Frame(thread, 560), GalleryUi.Row(stream));
    }

    /// <summary>Streams <see cref="Reply"/> in small chunks, as a model would.</summary>
    private static void Stream(ChatThread thread, ObservableCollection<ChatMessage> messages, string id)
    {
        messages.Add(new ChatMessage(id, "arc", null, DateTimeOffset.Now));
        var offset = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            if (offset >= Reply.Length)
            {
                timer.Stop();
                thread.CompleteMessage(id);
                return;
            }
            var length = Math.Min(Reply.Length - offset, 6);
            thread.AppendToMessage(id, Reply.Substring(offset, length));
            offset += length;
        };
        timer.Start();
    }

    private static Control History()
    {
        var start = DateTimeOffset.Now.AddDays(-12);
        var lines = new[]
        {
            "Morning! Did the build go green?", "Yes, all checks passed.", "Shipping the release notes now.",
            "Can you review the migration?", "Looks good, one nit on naming.", "Thanks, fixed.",
            "Lunch?", "In ten minutes.", "Pushed the hotfix.", "Deploy is rolling out."
        };
        var messages = new ObservableCollection<ChatMessage>();
        for (var i = 0; i < 1000; i++)
        {
            var author = (i / 3 % 3) switch { 0 => "sam", 1 => "me", _ => "lee" };
            messages.Add(new ChatMessage($"h{i}", author, lines[i % lines.Length], start.AddMinutes(i * 17))
            {
                Status = author == "me" ? ChatMessageStatus.Delivered : null
            });
        }
        var thread = new ChatThread
        {
            Participants = [s_me, s_sam, s_lee],
            CurrentUserId = "me",
            ItemsSource = messages,
            IsComposerVisible = false,
            Label = "Team history"
        };
        var add = new Button { Content = "New message from Lee" };
        add.Click += (_, _) => messages.Add(new ChatMessage($"n{messages.Count}", "lee", "Heads up: standup moves to 10:30.", DateTimeOffset.Now));
        return GalleryUi.Column(12, Frame(thread, 440), GalleryUi.Row(add));
    }

    private static Control ComposerAlone()
    {
        var status = GalleryUi.Text("Nothing sent yet.", ArcDesignTokenKind.TextSecondary);
        var composer = new ChatComposer { Placeholder = "Write a message" };
        composer.Sent += (_, e) =>
            status.Text = e.Draft.Files.Count > 0 ? $"Sent \"{e.Draft.Text}\" with {e.Draft.Files.Count} file(s)" : $"Sent \"{e.Draft.Text}\"";
        var frame = new Border
        {
            MaxWidth = 560,
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = composer
        };
        ArcTokens.Bind(frame, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(frame, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return GalleryUi.Column(12, frame, status);
    }
}
