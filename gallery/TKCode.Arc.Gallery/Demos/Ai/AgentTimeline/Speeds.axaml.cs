using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentTimelineSpeedsDemo : UserControl
{
    public AgentTimelineSpeedsDemo()
    {
        InitializeComponent();
        Timeline.Speeds = [0.5, 1, 2, 4];
    }
}
