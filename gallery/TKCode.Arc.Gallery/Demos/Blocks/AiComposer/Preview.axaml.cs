using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AiComposerPreviewDemo : UserControl
{
    private IDisposable? _reply;

    public AiComposerPreviewDemo()
    {
        InitializeComponent();
        AiComposerDemoData.Fill(Composer);
        Composer.Placeholder = AiComposerDemoData.Placeholder;

        // Your code starts the reply; the composer only shows that one is running and lets the person stop it.
        Composer.Sent += (_, e) => _reply = AiComposerDemoData.SimulateReply(Composer, Reply, e.Message);
        Composer.StopRequested += (_, _) => _reply?.Dispose();
    }
}
