using Avalonia.Controls;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiChatHostDrivenDemo : UserControl
{
    public AiChatHostDrivenDemo()
    {
        InitializeComponent();

        // No responder: a sent message only appears in the thread and raises MessageSent. Your code fills the reply it gets.
        Chat.MessageSent += (_, e) =>
        {
            var reply = e.Turn.SelectedVersion!.Reply;
            reply.Status = AiChatReplyStatus.Thinking;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                reply.Text = $"You wrote {e.Prompt.Text.Length} characters. This reply was written by the host, not by a responder.";
                reply.Status = AiChatReplyStatus.Done;
            };
            timer.Start();
        };
    }
}
