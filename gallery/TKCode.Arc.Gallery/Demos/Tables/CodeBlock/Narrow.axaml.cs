using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CodeBlockNarrowDemo : UserControl
{
    public CodeBlockNarrowDemo()
    {
        InitializeComponent();
        Block.Code = """
            .card {
              display: grid;
              gap: 12px;
              border-radius: 26px;
              padding: 24px;
            }
            """;
    }
}
