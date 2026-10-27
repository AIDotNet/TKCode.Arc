using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JourneyFlowPreviewDemo : UserControl
{
    public JourneyFlowPreviewDemo()
    {
        InitializeComponent();
        Journey.TableColumnHeaders = ["From", "To", "Learners", "Share of step"];
        Journey.Stages.AddRange(JourneyFlowDemoData.Stages());
        Journey.Nodes.AddRange(JourneyFlowDemoData.Nodes());
        Journey.Segments.AddRange(JourneyFlowDemoData.Segments());
    }
}
