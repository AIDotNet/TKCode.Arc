using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GanttTimelinePreviewDemo : UserControl
{
    public GanttTimelinePreviewDemo()
    {
        InitializeComponent();
        GanttTimelineDemoData.Fill(Timeline);
    }
}
