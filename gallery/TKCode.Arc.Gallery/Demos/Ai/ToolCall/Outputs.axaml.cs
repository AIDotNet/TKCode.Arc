using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolCallOutputsDemo : UserControl
{
    public ToolCallOutputsDemo()
    {
        InitializeComponent();
        var list = ToolCallSamples.TicketSearch();
        var table = ToolCallSamples.JobLogQuery();
        var json = ToolCallSamples.ChecklistRead();
        var text = ToolCallSamples.StatusNote(ToolCallStatus.Success);
        foreach (var item in new[] { list, table, json, text })
        {
            Host.Children.Add(new ToolCall { Item = item, IsExpanded = true });
        }
    }
}
