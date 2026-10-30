using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolCallOutputsDemo : UserControl
{
    public ToolCallOutputsDemo()
    {
        InitializeComponent();
        var list = ToolCallDemoData.TicketSearch();
        var table = ToolCallDemoData.JobLogQuery();
        var json = ToolCallDemoData.ChecklistRead();
        var text = ToolCallDemoData.StatusNote(ToolCallStatus.Success);
        foreach (var item in new[] { list, table, json, text })
        {
            Host.Children.Add(new ToolCall { Item = item, IsExpanded = true });
        }
    }
}
