using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class MarkdownStreamedReplyDemo : UserControl
{
    private const string Reply = """
        Sure — here is the short version.

        **Streaming** keeps the document stable while text arrives: finished blocks never re-render, the last block grows,
        and new words fade in.

        ```cs
        markdown.Append(chunk);   // as tokens arrive
        markdown.Complete();      // when the reply ends
        ```

        | Step | Call |
        |------|------|
        | Start | `Append("...")` |
        | Finish | `Complete()` |

        Anything else?
        """;

    private const string Prompt = "_Press **Stream reply** to simulate a model answering._";

    private DispatcherTimer? _timer;

    public MarkdownStreamedReplyDemo()
    {
        InitializeComponent();
        Message.Text = Prompt;
        Message.PropertyChanged += (_, e) =>
        {
            if (e.Property == Markdown.IsStreamingProperty)
            {
                Status.Text = Message.IsStreaming ? "Streaming…" : "Complete";
                StreamButton.IsEnabled = !Message.IsStreaming;
            }
        };
    }

    private void OnStreamClick(object? sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        var random = new Random(7);
        var position = 0;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Normal, (_, _) =>
        {
            if (position >= Reply.Length)
            {
                _timer?.Stop();
                Message.Complete();
                return;
            }
            var length = Math.Min(Reply.Length - position, random.Next(2, 9));
            Message.Append(Reply.Substring(position, length));
            position += length;
        });
        _timer.Start();
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _timer?.Stop();
        Message.Text = Prompt;
        Status.Text = "Idle";
    }
}
