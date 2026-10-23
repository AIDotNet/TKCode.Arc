using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentMemoryPausedDemo : UserControl
{
    public AgentMemoryPausedDemo()
    {
        InitializeComponent();
        Memory.Groups.AddRange(AgentMemoryDemoData.Groups());

        // Only two memories: the Projects and People groups are empty, which the All view hides and their own filter explains.
        Memory.Memories.AddRange(AgentMemoryDemoData.Memories().Take(5));
        Memory.Captured.AddRange(AgentMemoryDemoData.Captured());
        Memory.Capacity = AgentMemoryDemoData.Capacity;
    }
}
