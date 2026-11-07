using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceRecorderPreviewDemo : UserControl
{
    public VoiceRecorderPreviewDemo()
    {
        InitializeComponent();
        Recorder.Messages = VoiceRecorderSamples.Thread();
    }

    // Back to the three messages; sent takes are dropped.
    private void OnReset(object? sender, RoutedEventArgs e) => Recorder.ResetMessages(VoiceRecorderSamples.Thread());
}
