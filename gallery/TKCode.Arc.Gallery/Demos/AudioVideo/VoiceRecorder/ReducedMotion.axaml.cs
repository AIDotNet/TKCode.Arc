using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceRecorderReducedMotionDemo : UserControl
{
    public VoiceRecorderReducedMotionDemo()
    {
        InitializeComponent();
        Recorder.Messages = VoiceRecorderSamples.Thread();
    }
}
