using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LineReplayEventsDemo : UserControl
{
    public LineReplayEventsDemo()
    {
        InitializeComponent();
        Replay.Title = LineReplayDemoData.Title;
        Replay.Description = LineReplayDemoData.Description;
        Replay.Points = LineReplayDemoData.ReleaseDay();
        Replay.Moments.AddRange(LineReplayDemoData.ReleaseMoments());

        Replay.MomentReached += (_, e) => Log.Text = $"Reached: {e.Moment.Title}";
        PlayButton.Click += (_, _) => Replay.Replay();
        NoonButton.Click += (_, _) => Replay.Seek(12 * 60);
    }
}
