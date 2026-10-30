using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolCallNarrowDemo : UserControl
{
    public ToolCallNarrowDemo()
    {
        InitializeComponent();
        Group.Items.AddRange([ToolCallDemoData.TicketSearch(), ToolCallDemoData.ChecklistRead(), ToolCallDemoData.JobLogQuery(ToolCallStatus.Error)]);
    }
}
