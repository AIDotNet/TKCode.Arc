using Avalonia;
using Avalonia.Media;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

// Indent guides (upstream's IndentGuidesOverlay, src/vs/editor/browser/viewParts/indentGuides/indentGuides.ts, with
// Monaco's defaults: guides.indentation and guides.highlightActiveIndentation on, bracket pair guides off). For each
// visible line, the view model gives its indent level (the guides part reads blank lines from the lines around them);
// a guide is drawn at the start of each indent level, one indent size of spaces apart, and the guide of the block the
// primary cursor is in is drawn in the active color. Drawn in the background layer, after the selections, as upstream
// draws them in a content overlay below the text.
internal sealed partial class CodeEditorView
{
    /// <summary>An indent guide to draw: its view line, its x relative to the control and whether it is the active one.</summary>
    internal readonly record struct IndentGuideLine(int LineNumber, double X, bool IsActive);

    private void RenderIndentGuides(DrawingContext context, int startLine, int endLine, TextPosition primaryPosition)
    {
        if (IndentGuideBrush is not { } brush)
        {
            return;
        }

        var activeBrush = ActiveIndentGuideBrush ?? brush;
        var width = Math.Max(1, IndentGuideWidth);
        foreach (var guide in GetIndentGuides(startLine, endLine, primaryPosition))
        {
            context.FillRectangle(guide.IsActive ? activeBrush : brush, new Rect(Math.Round(guide.X), GetY(guide.LineNumber), width, _lineHeight));
        }
    }

    /// <summary>The indent guides of the view lines <paramref name="startLine"/> to <paramref name="endLine"/>.</summary>
    internal List<IndentGuideLine> GetIndentGuides(int startLine, int endLine, TextPosition primaryPosition)
    {
        var result = new List<IndentGuideLine>();
        if (_editor is not { ShowIndentGuides: true } || _viewModel is not { } viewModel || endLine < startLine)
        {
            return result;
        }

        var indents = viewModel.GetLinesIndentGuides(startLine, endLine);
        var active = viewModel.GetActiveIndentGuide(primaryPosition.LineNumber, startLine, endLine);
        var indentSize = _editor.Model.GetOptions().IndentSize;
        var left = GutterWidth - ScrollLeft;
        for (var line = startLine; line <= endLine; line++)
        {
            var indent = indents[line - startLine];
            for (var indentLvl = 1; indentLvl <= indent; indentLvl++)
            {
                // the visible column of the guide (1-based), as upstream's (indentLvl - 1) * indentSize + 1
                var visibleColumn = ((indentLvl - 1) * indentSize) + 1;
                var x = left + ((visibleColumn - 1) * _charWidth);
                var isActive = active.StartLineNumber <= line && line <= active.EndLineNumber && indentLvl == active.Indent;
                result.Add(new IndentGuideLine(line, x, isActive));
            }
        }

        return result;
    }
}
