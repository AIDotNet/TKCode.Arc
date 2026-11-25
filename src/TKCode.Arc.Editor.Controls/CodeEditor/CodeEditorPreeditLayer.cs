using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The layer that draws the input method's composition text over the editor's text (CodeEditorView.Ime.cs).</summary>
internal sealed class CodeEditorPreeditLayer : Control
{
    private readonly CodeEditorView _view;

    public CodeEditorPreeditLayer(CodeEditorView view)
    {
        _view = view;
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context) => _view.RenderPreedit(context);
}
