using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolCallNarrowDemo : UserControl
{
    public ToolCallNarrowDemo()
    {
        InitializeComponent();
        Group.Items.AddRange([ToolCallSamples.TicketSearch(), ToolCallSamples.ChecklistRead(), ToolCallSamples.JobLogQuery(ToolCallStatus.Error)]);
    }
}
