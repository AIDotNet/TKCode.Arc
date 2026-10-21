using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockCollapsibleDemo : UserControl
{
    public CodeBlockCollapsibleDemo()
    {
        InitializeComponent();
        var steps = Enumerable.Range(1, 40).Select(i =>
            i % 5 == 0 ? $"  // step {i}: checkpoint" : $"  const value{i} = compute({i}, \"item-{i}\");");
        Block.Code = "export function pipeline() {\n" + string.Join("\n", steps) + "\n  return value40;\n}";
    }
}
