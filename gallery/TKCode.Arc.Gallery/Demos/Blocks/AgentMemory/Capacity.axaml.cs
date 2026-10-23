using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentMemoryCapacityDemo : UserControl
{
    public AgentMemoryCapacityDemo()
    {
        InitializeComponent();
        Memory.Groups.AddRange(AgentMemoryDemoData.Groups());
        Memory.Memories.AddRange(AgentMemoryDemoData.Memories());

        // 91 percent used: the meter already warns, and one more sentence of average length fills it.
        Memory.Capacity = (int)(Memory.Memories.Sum(m => m.Text.Length) / 0.91);
        Memory.WarningRatio = 0.9;
    }
}
