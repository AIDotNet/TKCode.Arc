using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class RichTextEditorShortcutsDemo : UserControl
{
    public RichTextEditorShortcutsDemo()
    {
        InitializeComponent();
        Editor.Markdown = """
            # Launch notes

            Type **Markdown** as you go: `#` and a space makes a heading, `-` a list, `>` a quote, and three backticks a code block. Select text for the _floating toolbar_, or type `/` on an empty line for blocks.

            - Shortcuts: ⌘B, ⌘I, ⌘⇧X, ⌘E and ⌘K
            - Tab nests a list item
              - Shift+Tab brings it back

            > Undo is the editor's own: ⌘Z and ⌘⇧Z.
            """;
    }
}
