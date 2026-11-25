using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The drawing layers of a <see cref="CodeEditorView"/>, bottom to top.</summary>
[Flags]
internal enum CodeEditorLayers
{
    None = 0,

    /// <summary>The current line, decoration backgrounds and the selections (upstream's CurrentLineHighlightOverlay, DecorationsOverlay and SelectionsOverlay).</summary>
    Background = 1,

    /// <summary>The text (upstream's ViewLines).</summary>
    Text = 2,

    /// <summary>The cursors (upstream's ViewCursors); the only layer a blink redraws.</summary>
    Cursors = 4,

    /// <summary>The gutter: margin decorations, the columns and the line numbers (upstream's margin overlays).</summary>
    Gutter = 8,

    /// <summary>Decorations drawn above the text: underlines such as squiggles (upstream's DecorationsOverlay parts above the text).</summary>
    Decorations = 16,

    All = Background | Text | Cursors | Gutter | Decorations,
}

/// <summary>
/// One layer of a <see cref="CodeEditorView"/>: a visual of its own, so that invalidating it re-records only what it
/// draws (a cursor blink does not touch the text). The view draws into it; the layer takes no input.
/// </summary>
internal sealed class CodeEditorLayer : Control
{
    private readonly Action<DrawingContext> _render;

    internal CodeEditorLayer(CodeEditorLayers kind, Action<DrawingContext> render)
    {
        Kind = kind;
        _render = render;
        IsHitTestVisible = false;
    }

    internal CodeEditorLayers Kind { get; }

    /// <summary>How many times the layer has been drawn (for tests).</summary>
    internal int RenderCount { get; private set; }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        RenderCount++;
        _render(context);
    }
}
