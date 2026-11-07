using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceRecorderNarrowDemo : UserControl
{
    public VoiceRecorderNarrowDemo()
    {
        InitializeComponent();
        Recorder.Messages = VoiceRecorderSamples.Thread();
    }
}
