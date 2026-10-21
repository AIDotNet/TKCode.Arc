using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChatThreadSupportConversationDemo : UserControl
{
    private readonly ObservableCollection<ChatMessage> _messages;
    private readonly ObservableCollection<string> _typing = ["sam"];

    public ChatThreadSupportConversationDemo()
    {
        InitializeComponent();
        var now = DateTimeOffset.Now;
        var yesterday = now.AddDays(-1).Date.AddHours(16);
        var today = now.Date.AddHours(Math.Min(now.Hour, 9));
        _messages =
        [
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
        ];
        Chat.ItemsSource = _messages;
        Chat.TypingParticipantIds = _typing;
        Chat.ReadBy = new Dictionary<string, string> { ["sam"] = "m6", ["lee"] = "m6" };
    }

    private void OnSendRequested(object? sender, ChatSendEventArgs e)
    {
        _messages.Add(new ChatMessage($"s{_messages.Count + 1}", "me", e.Draft.Text, DateTimeOffset.Now)
        {
            Attachments = e.Draft.Attachments.Count > 0 ? e.Draft.Attachments : null,
            Status = ChatMessageStatus.Sent
        });
    }

    private void OnReactionToggled(object? sender, ChatReactionToggledEventArgs e)
    {
        var reactions = new List<ChatReaction>(e.Message.Reactions ?? []);
        var at = reactions.FindIndex(r => r.Emoji == e.Emoji);
        if (at < 0)
        {
            reactions.Add(new ChatReaction(e.Emoji, 1, true));
        }
        else
        {
            reactions[at] = reactions[at] with { Count = reactions[at].Count + (reactions[at].Mine ? -1 : 1), Mine = !reactions[at].Mine };
        }
        e.Message.Reactions = reactions.Where(r => r.Count > 0).ToArray();
    }

    private void OnRetryRequested(object? sender, ChatRetryEventArgs e)
    {
        e.Message.Status = ChatMessageStatus.Sent;
    }

    private void OnReplyClick(object? sender, RoutedEventArgs e)
    {
        _typing.Clear();
        _messages.Add(new ChatMessage($"r{_messages.Count + 1}", "sam", "Yes! Open Settings → Exports and pick Weekly.", DateTimeOffset.Now));
        Chat.ReadBy = new Dictionary<string, string> { ["sam"] = _messages[^2].Id, ["lee"] = "m6" };
    }

    private void OnTypingClick(object? sender, RoutedEventArgs e)
    {
        if (_typing.Count == 0)
        {
            _typing.Add("sam");
        }
        else
        {
            _typing.Clear();
        }
    }
}
