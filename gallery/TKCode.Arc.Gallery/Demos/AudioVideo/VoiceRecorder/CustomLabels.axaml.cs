using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceRecorderCustomLabelsDemo : UserControl
{
    public VoiceRecorderCustomLabelsDemo()
    {
        InitializeComponent();
        Recorder.Labels = new VoiceRecorderLabels
        {
            Hint = "Tap to talk to the crew",
            Record = "Start a voice note",
            Send = "Post voice note",
            Discard = "Throw away",
            StopAt = time => $"Finish at {time}",
            AnnounceSent = "Posted",
        };
        // Any function of the take and the 90ms sample index; return 0 to 1.
        Recorder.LevelSource = (take, sample) =>
            0.3 + 0.25 * Math.Sin(sample * 0.7 + take) + (sample % 9 == 0 ? 0.35 : 0);
    }
}
