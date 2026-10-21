using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ChatThreadComposerOnItsOwnDemo : UserControl
{
    public ChatThreadComposerOnItsOwnDemo()
    {
        InitializeComponent();
    }

    private void OnSent(object? sender, ChatSendEventArgs e)
    {
        Status.Text = e.Draft.Files.Count > 0
            ? $"Sent \"{e.Draft.Text}\" with {e.Draft.Files.Count} file(s)"
            : $"Sent \"{e.Draft.Text}\"";
    }
}
