using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LineReplayPreviewDemo : UserControl
{
    public LineReplayPreviewDemo()
    {
        InitializeComponent();
        Replay.Title = LineReplayDemoData.Title;
        Replay.Description = LineReplayDemoData.Description;
        Replay.Points = LineReplayDemoData.ReleaseDay();
        Replay.Moments.AddRange(LineReplayDemoData.ReleaseMoments());
    }
}
