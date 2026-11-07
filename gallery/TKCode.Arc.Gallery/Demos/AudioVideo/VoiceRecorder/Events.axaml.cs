using Avalonia.Collections;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class VoiceRecorderEventsDemo : UserControl
{
    private readonly AvaloniaList<string> _log = new();

    public VoiceRecorderEventsDemo()
    {
        InitializeComponent();
        Log.ItemsSource = _log;
        Recorder.Messages = VoiceRecorderSamples.LongThread();
        Recorder.MessageSent += (_, e) =>
            Add($"Sent {e.Message.Id}: {e.Message.Duration.TotalSeconds:0.0} s, {e.Message.Peaks.Count} bars");
        Recorder.Cancelled += (_, e) => Add($"Discarded a {e.Message.Duration.TotalSeconds:0.0} s take");
    }

    private void Add(string line)
    {
        _log.Insert(0, line);
        if (_log.Count > 8)
        {
            _log.RemoveAt(_log.Count - 1);
        }
    }
}
