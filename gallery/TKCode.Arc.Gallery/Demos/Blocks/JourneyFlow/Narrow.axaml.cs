using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JourneyFlowNarrowDemo : UserControl
{
    public JourneyFlowNarrowDemo()
    {
        InitializeComponent();
        Journey.Stages.AddRange(JourneyFlowDemoData.Stages());
        Journey.Nodes.AddRange(JourneyFlowDemoData.Nodes());
        Journey.Segments.AddRange(JourneyFlowDemoData.Segments());
    }
}
