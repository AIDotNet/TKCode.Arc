// Before / after content of decorations (upstream: the ::before / ::after pseudo elements that ViewLine rendering
// gives the BeforeContentClassName / AfterContentClassName of a decoration, styled by CSS such as folding.css's
// `.inline-folded:after { content: "\22EF" }`; src/vs/editor/common/viewLayout/viewLineRenderer.ts, commit b57d5c9e,
// MIT license). A class name's style gives the text (CodeEditorDecorationStyle.ContentText); the view draws it in the
// decorations layer, above the text.
// Differences from upstream: the content takes no columns and does not move the text after it (upstream's pseudo
// element is inline and pushes it); the content is not part of the text's hit testing, a feature asks
// GetContentDecorationRects for where it is.

using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>Where the view draws the content text of a decoration (relative to the view).</summary>
    internal readonly record struct ContentDecorationRect(
        string ClassName,
        bool IsAfter,
        int ViewLineNumber,
        int Column,
        Rect Bounds,
        string Text,
        IBrush? Foreground,
        IBrush? Background);

    /// <summary>The content texts of the decorations on the visible view lines <paramref name="startLine"/> to <paramref name="endLine"/>.</summary>
    internal List<ContentDecorationRect> GetContentDecorationRects(int startLine, int endLine)
    {
        var result = new List<ContentDecorationRect>();
        if (_viewModel is not { } viewModel || _editor is not { } editor || endLine < startLine)
        {
            return result;
        }

        var styles = editor.DecorationStyles;
        var visibleRange = new TextRange(startLine, 1, endLine, viewModel.GetLineMaxColumn(endLine));
        for (var line = startLine; line <= endLine; line++)
        {
            var data = viewModel.GetViewportViewLineRenderingData(visibleRange, line);
            if (data.InlineDecorations.Count == 0)
            {
                continue;
            }

            // texts at the same column follow each other (upstream: pseudo elements in document order)
            var lastColumn = -1;
            var nextX = 0.0;
            foreach (var decoration in data.InlineDecorations)
            {
                if (decoration.Type is not (InlineDecorationType.Before or InlineDecorationType.After))
                {
                    continue;
                }

                foreach (var style in styles.GetAll(decoration.InlineClassName))
                {
                    if (string.IsNullOrEmpty(style.ContentText))
                    {
                        continue;
                    }

                    var column = Math.Clamp(decoration.Range.StartColumn, 1, data.MaxColumn);
                    var x = GetX(line, column);
                    if (column == lastColumn)
                    {
                        x = Math.Max(x, nextX);
                    }

                    var margin = style.ContentMarginEm * _fontSize;
                    var width = MeasureContentText(style.ContentText) + (2 * margin);
                    var bounds = new Rect(x, GetY(line), width, _lineHeight);
                    result.Add(new ContentDecorationRect(
                        decoration.InlineClassName,
                        decoration.Type == InlineDecorationType.After,
                        line,
                        column,
                        bounds,
                        style.ContentText,
                        Resolve(style.Foreground),
                        Resolve(style.Background)));
                    lastColumn = column;
                    nextX = bounds.Right;
                }
            }
        }

        return result;
    }

    private double MeasureContentText(string text)
    {
        using var layout = new TextLayout(text, _typeface, _fontSize, null);
        return layout.WidthIncludingTrailingWhitespace;
    }

    // decorations layer (inside the text area's clip): the content texts above the text
    private void RenderContentDecorations(DrawingContext context, int startLine, int endLine)
    {
        foreach (var rect in GetContentDecorationRects(startLine, endLine))
        {
            if (rect.Background is { } background)
            {
                context.FillRectangle(background, rect.Bounds);
            }

            var foreground = rect.Foreground ?? TextElement.GetForeground(this) ?? Brushes.Gray;
            using var layout = new TextLayout(rect.Text, _typeface, _fontSize, foreground);
            var x = rect.Bounds.X + ((rect.Bounds.Width - layout.WidthIncludingTrailingWhitespace) / 2);
            var y = rect.Bounds.Y + ((rect.Bounds.Height - layout.Height) / 2);
            layout.Draw(context, new Point(x, y));
        }
    }
}
