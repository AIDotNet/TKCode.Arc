using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChatThreadThousandMessagesDemo : UserControl
{
    private static readonly string[] Lines =
    [
        "Morning! Did the build go green?", "Yes, all checks passed.", "Shipping the release notes now.",
        "Can you review the migration?", "Looks good, one nit on naming.", "Thanks, fixed.",
        "Lunch?", "In ten minutes.", "Pushed the hotfix.", "Deploy is rolling out."
    ];

    private readonly ObservableCollection<ChatMessage> _messages = [];

    public ChatThreadThousandMessagesDemo()
    {
        InitializeComponent();
        var start = DateTimeOffset.Now.AddDays(-12);
        for (var i = 0; i < 1000; i++)
        {
            var author = (i / 3 % 3) switch { 0 => "sam", 1 => "me", _ => "lee" };
            _messages.Add(new ChatMessage($"h{i}", author, Lines[i % Lines.Length], start.AddMinutes(i * 17))
            {
                Status = author == "me" ? ChatMessageStatus.Delivered : null
            });
        }
        Chat.ItemsSource = _messages;
    }

    private void OnNewMessageClick(object? sender, RoutedEventArgs e)
    {
        _messages.Add(new ChatMessage($"n{_messages.Count}", "lee", "Heads up: standup moves to 10:30.", DateTimeOffset.Now));
    }
}
