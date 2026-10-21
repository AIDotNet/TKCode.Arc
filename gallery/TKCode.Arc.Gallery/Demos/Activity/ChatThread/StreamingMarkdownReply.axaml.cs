using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChatThreadStreamingMarkdownReplyDemo : UserControl
{
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

    private readonly ObservableCollection<ChatMessage> _messages;
    private int _count;

    public ChatThreadStreamingMarkdownReplyDemo()
    {
        InitializeComponent();
        var now = DateTimeOffset.Now;
        _messages =
        [
            new("a1", "me", "How do I retry a flaky HTTP call in C#?", now.AddMinutes(-2)) { Status = ChatMessageStatus.Read },
            new("a2", "arc", Reply, now.AddMinutes(-1))
        ];
        Chat.ItemsSource = _messages;
    }

    private void OnSendRequested(object? sender, ChatSendEventArgs e)
    {
        _messages.Add(new ChatMessage($"q{++_count}", "me", e.Draft.Text, DateTimeOffset.Now) { Status = ChatMessageStatus.Sent });
        Stream($"reply{_count}");
    }

    private void OnStreamClick(object? sender, RoutedEventArgs e)
    {
        Stream($"reply{++_count}");
    }

    /// <summary>Streams <see cref="Reply"/> in small chunks, as a model would.</summary>
    private void Stream(string id)
    {
        _messages.Add(new ChatMessage(id, "arc", null, DateTimeOffset.Now));
        var offset = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) =>
        {
            if (offset >= Reply.Length)
            {
                timer.Stop();
                Chat.CompleteMessage(id);
                return;
            }
            var length = Math.Min(Reply.Length - offset, 6);
            Chat.AppendToMessage(id, Reply.Substring(offset, length));
            offset += length;
        };
        timer.Start();
    }
}
