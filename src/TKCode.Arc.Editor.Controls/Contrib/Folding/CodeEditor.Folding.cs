// The editor's folding option (upstream's EditorOption.folding in src/vs/editor/common/config/editorOptions.ts, commit
// b57d5c9e, MIT license). The FoldingController (FoldingController.cs) follows it.

using Avalonia;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>
    /// Whether regions of the text can be folded (collapsed to their first line), as Monaco's <c>folding</c> option
    /// (default true): a column of chevrons in the gutter, shown while the pointer is over the gutter and always for
    /// folded regions, Monaco's folding keybindings (Ctrl+Shift+[ and ], Ctrl+K Ctrl+0, Ctrl+K Ctrl+J, ...), and an
    /// ellipsis after the first line of a folded region that unfolds it when clicked. Regions come from the
    /// indentation and the language's region markers (<c>#region</c>), or from the language's folding range provider.
    /// </summary>
    public static readonly StyledProperty<bool> FoldingProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(Folding), true);

    /// <summary>Whether regions of the text can be folded (collapsed to their first line), as Monaco's <c>folding</c> option (default true).</summary>
    public bool Folding
    {
        get => GetValue(FoldingProperty);
        set => SetValue(FoldingProperty, value);
    }
}
