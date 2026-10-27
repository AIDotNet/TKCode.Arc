using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class JourneyFlowTicketsDemo : UserControl
{
    public JourneyFlowTicketsDemo()
    {
        InitializeComponent();
        Journey.ValueFormatter = value => value >= 1000
            ? (value / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k"
            : value.ToString("0", CultureInfo.InvariantCulture);
        Journey.Stages.AddRange(JourneyFlowDemoData.TicketStages());
        Journey.Nodes.AddRange(JourneyFlowDemoData.TicketNodes());
        Journey.Segments.Add(JourneyFlowDemoData.TicketSegment());
    }
}
