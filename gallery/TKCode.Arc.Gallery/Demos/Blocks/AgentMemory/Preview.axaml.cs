using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentMemoryPreviewDemo : UserControl
{
    public AgentMemoryPreviewDemo()
    {
        InitializeComponent();
        Memory.Groups.AddRange(AgentMemoryDemoData.Groups());
        Memory.Memories.AddRange(AgentMemoryDemoData.Memories());
        Memory.Captured.AddRange(AgentMemoryDemoData.Captured());
        Memory.Capacity = AgentMemoryDemoData.Capacity;
    }
}
