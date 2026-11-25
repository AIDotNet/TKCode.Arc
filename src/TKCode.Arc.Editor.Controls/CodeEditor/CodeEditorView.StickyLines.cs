// Drawing a model line outside the view's own layers: the sticky scroll widget (Contrib/StickyScroll) shows the lines that
// start the scopes around the top of the view as upstream's StickyScrollWidget renders them (src/vs/editor/contrib/
// stickyScroll/browser/stickyScrollWidget.ts: _renderChildNode builds a ViewLine of the line's view model data with its
// tokens, and the line number in the gutter; commit b57d5c9e, MIT license). The line is shaped through the view's text
// cache with the line's token and bracket colors, so it looks as it does in the text.

using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>
    /// Draws the first view line of model line <paramref name="modelLineNumber"/> with its top at <paramref name="top"/>
    /// in a surface as wide as the view: its line number in the gutter (when line numbers show) and its text scrolled as
    /// the view's text is. Returns false when there is nothing to draw (no view model yet).
    /// </summary>
    internal bool RenderStickyLine(DrawingContext context, int modelLineNumber, double top, double width, IBrush? lineNumberBrush = null)
    {
        if (!CanDraw || _editor is not { } editor || _viewModel is not { } viewModel)
        {
            return false;
        }

        var model = editor.Model;
        if (modelLineNumber < 1 || modelLineNumber > model.GetLineCount())
        {
            return false;
        }

        var viewLine = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelLineNumber, 1)).LineNumber;
        var gutterLayout = GetGutterLayout();
        var gutterWidth = gutterLayout.Width;

        // the line number, right-aligned as in the gutter
        if (editor.ShowLineNumbers && gutterWidth > 0)
        {
            var brush = lineNumberBrush ?? LineNumberBrush ?? TextElement.GetForeground(this) ?? Brushes.Gray;
            var cache = GetLineNumberCache(ref _lineNumberCache, brush);
            var number = cache.Get(modelLineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
            var numberWidth = number.GetX(number.Text.Length + 1);
            using (context.PushClip(new Rect(0, top, gutterWidth, _lineHeight)))
            {
                number.Draw(context, brush, gutterLayout.LineNumbersRight - numberWidth, top, 0, numberWidth);
            }
        }

        // the text, with the line's token and bracket colors
        var content = viewModel.GetLineContent(viewLine);
        if (content.Length == 0)
        {
            return true;
        }

        var foreground = TextElement.GetForeground(this) ?? Brushes.Black;
        var maxColumn = viewModel.GetLineMaxColumn(viewLine);
        var data = viewModel.GetViewportViewLineRenderingData(new TextRange(viewLine, 1, viewLine, maxColumn), viewLine);
        var colors = CodeEditorLineColors.From(viewModel.GetViewLineData(viewLine).Tokens, GetBracketColors(viewLine, data));
        var shaped = _textCache!.Get(content, model.GetOptions().TabSize, colors);
        var scrollLeft = ScrollLeft;
        var textWidth = Math.Max(0, width - gutterWidth);
        using (context.PushClip(new Rect(gutterWidth, top, textWidth, _lineHeight)))
        {
            shaped.Draw(context, foreground, gutterWidth - scrollLeft, top, scrollLeft, scrollLeft + textWidth);
        }

        return true;
    }

    /// <summary>The model column at an x relative to the view on the first view line of a model line (for clicks on sticky lines).</summary>
    internal int GetStickyLineColumnAtX(int modelLineNumber, double x)
    {
        if (_viewModel is not { } viewModel || _textCache is null)
        {
            return 1;
        }

        var viewLine = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelLineNumber, 1)).LineNumber;
        var viewColumn = GetColumnAtX(viewLine, x);
        return viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(new TextPosition(viewLine, viewColumn)).Column;
    }
}
