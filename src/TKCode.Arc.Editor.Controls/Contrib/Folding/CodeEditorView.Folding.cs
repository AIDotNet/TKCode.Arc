// The geometry of the folding column (upstream: the 16 px that EditorLayoutInfo adds to the lines decorations for the
// folding chevrons when the folding option is on; src/vs/editor/common/config/editorOptions.ts, commit b57d5c9e, MIT
// license). The control theme sets it from CodeEditorToken.FoldingWidth.

using Avalonia;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>Defines the <see cref="FoldingWidth"/> property.</summary>
    public static readonly StyledProperty<double> FoldingWidthProperty =
        AvaloniaProperty.Register<CodeEditorView, double>(nameof(FoldingWidth), 18);

    /// <summary>The width of the folding column in the gutter, in pixels.</summary>
    public double FoldingWidth
    {
        get => GetValue(FoldingWidthProperty);
        set => SetValue(FoldingWidthProperty, value);
    }
}
