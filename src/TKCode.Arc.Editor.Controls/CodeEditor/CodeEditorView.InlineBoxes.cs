// Inline class names with a background (upstream: the CSS background of an inline class name, e.g. the color swatch
// of src/vs/editor/contrib/colorPicker/browser/colorDetector.ts, an injected ' ' whose inline class name has the
// color as its background and colorPicker.css draws it as a bordered 0.8em box; commit b57d5c9e, MIT license).
// Drawn in the background layer under the text, before the className backgrounds: the view line's inline decorations
// (injected text included) whose style has a Background, either over the whole cell of the text or as a centered box
// of InlineBoxSize (a fraction of the line height) with the style's border.

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    /// <summary>The inline backgrounds on the visible view lines <paramref name="startLine"/> to <paramref name="endLine"/> (also for tests).</summary>
    internal List<DecorationRect> GetInlineBackgroundRects(int startLine, int endLine)
    {
        var result = new List<DecorationRect>();
        if (_viewModel is not { } viewModel || _editor is not { } editor || endLine < startLine || _textCache is null)
        {
            return result;
        }

        var styles = editor.DecorationStyles;
        var visibleRange = new TextRange(startLine, 1, endLine, viewModel.GetLineMaxColumn(endLine));

        // most frames have none: look at the decorations once before asking each line for its inline decorations
        var any = false;
        foreach (var decoration in viewModel.GetDecorationsInViewport(visibleRange))
        {
            var options = decoration.Options;
            if (HasInlineBackground(styles, options.InlineClassName)
                || HasInlineBackground(styles, options.Before?.InlineClassName)
                || HasInlineBackground(styles, options.After?.InlineClassName))
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            return result;
        }

        for (var line = startLine; line <= endLine; line++)
        {
            var data = viewModel.GetViewportViewLineRenderingData(visibleRange, line);
            foreach (var inline in data.InlineDecorations)
            {
                foreach (var style in styles.GetAll(inline.InlineClassName))
                {
                    if (Resolve(style.Background) is not { } fill)
                    {
                        continue;
                    }

                    var range = inline.Range;
                    var startColumn = range.StartLineNumber < line ? 1 : range.StartColumn;
                    var endColumn = range.EndLineNumber > line ? data.MaxColumn : range.EndColumn;
                    if (endColumn <= startColumn)
                    {
                        continue;
                    }

                    var x1 = GetX(line, startColumn);
                    var x2 = GetX(line, endColumn);
                    var y = GetY(line);
                    Rect bounds;
                    if (style.InlineBoxSize > 0)
                    {
                        var size = Math.Round(_lineHeight * style.InlineBoxSize);
                        var left = Math.Round(x1 + ((x2 - x1 - size) / 2));
                        var top = Math.Round(y + ((_lineHeight - size) / 2));
                        bounds = new Rect(left, top, size, size);
                    }
                    else
                    {
                        bounds = new Rect(x1, y, Math.Max(0, x2 - x1), _lineHeight);
                    }

                    result.Add(new DecorationRect(inline.InlineClassName, DecorationDrawKind.Range, line, bounds, fill, Resolve(style.Border), style.BorderThickness, DecorationUnderlineKind.None));
                }
            }
        }

        return result;

        bool HasInlineBackground(CodeEditorDecorationStyles all, string? className)
        {
            foreach (var style in all.GetAll(className))
            {
                if (!style.Background.IsEmpty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void RenderInlineBackgrounds(DrawingContext context, int startLine, int endLine)
    {
        foreach (var rect in GetInlineBackgroundRects(startLine, endLine))
        {
            context.FillRectangle(rect.Fill!, rect.Bounds);
            if (rect.Stroke is { } stroke && rect.StrokeThickness > 0)
            {
                var half = rect.StrokeThickness / 2;
                context.DrawRectangle(null, new ImmutablePen(stroke.ToImmutable(), rect.StrokeThickness), rect.Bounds.Deflate(half));
            }
        }
    }
}
