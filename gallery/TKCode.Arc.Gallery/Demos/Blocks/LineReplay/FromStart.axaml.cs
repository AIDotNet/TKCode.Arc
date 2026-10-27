using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class LineReplayFromStartDemo : UserControl
{
    public LineReplayFromStartDemo()
    {
        InitializeComponent();
        Replay.Points = LineReplayDemoData.SaleMorning();
        Replay.Moments.AddRange(LineReplayDemoData.SaleMoments());
    }
}
