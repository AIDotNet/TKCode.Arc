using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RichTextEditorReadOnlyDemo : UserControl
{
    public RichTextEditorReadOnlyDemo()
    {
        InitializeComponent();
        Editor.Markdown = """
            ## Release checklist

            1. Freeze the branch
            2. Run the **full** test suite
            3. Tag and ship

            ```
            git tag v1.4.0
            ```
            """;
    }
}
