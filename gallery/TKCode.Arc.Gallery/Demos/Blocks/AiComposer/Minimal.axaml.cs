using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiComposerMinimalDemo : UserControl
{
    public AiComposerMinimalDemo()
    {
        InitializeComponent();
        Composer.Sent += (_, e) => Sent.Text = $"Sent: “{e.Message.Text.Trim()}”";
    }
}
