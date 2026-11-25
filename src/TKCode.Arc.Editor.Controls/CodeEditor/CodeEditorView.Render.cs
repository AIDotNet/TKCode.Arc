using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

// Drawing, in four layers (CodeEditorLayer), bottom to top: the current line (upstream's CurrentLineHighlightOverlay)
// and the selections (SelectionsOverlay); the text (ViewLines); the cursors (ViewCursors); the line numbers
// (LineNumbersOverlay). Each is a visual of its own and is invalidated on its own (InvalidateLayers), so a cursor blink
// re-records the cursor layer only and a cursor move leaves the text layer alone. The text comes from a cache of shaped
// lines (CodeEditorTextCache), so scrolling re-records the text layer without shaping the lines it already had. The x of
// a column is what the shaped line says (CodeEditorShapedLine.GetX): the column grid for ASCII lines, the layout's hit
// testing for the others, so cursors and selections meet the glyphs as drawn.
internal sealed partial class CodeEditorView
{
    private readonly CodeEditorLayer _backgroundLayer;
    private readonly CodeEditorLayer _textLayer;
    private readonly CodeEditorLayer _decorationsLayer;
    private readonly CodeEditorLayer _cursorLayer;
    private readonly CodeEditorLayer _gutterLayer;
    private CodeEditorTextMetrics? _metrics;
    private CodeEditorTextCache? _textCache;
    private CodeEditorTextCache? _lineNumberCache;
    private CodeEditorTextCache? _activeLineNumberCache;
    private double _maxLineWidth;

    /// <summary>The width of the gutter: the columns, the line numbers and the gap to the text (0 with neither; CodeEditorView.Gutter.cs).</summary>
    internal double GutterWidth => GetGutterLayout().Width;

    /// <summary>The x of a view position, relative to the control.</summary>
    internal double GetX(int viewLineNumber, int column) =>
        GutterWidth + GetShapedLine(viewLineNumber).GetX(column) - ScrollLeft;

    /// <summary>The column of a view line nearest to an x relative to the control (the inverse of <see cref="GetX"/>).</summary>
    internal int GetColumnAtX(int viewLineNumber, double x)
    {
        var column = GetShapedLine(viewLineNumber).GetColumn(x - GutterWidth + ScrollLeft);
        return Math.Clamp(column, 1, _viewModel!.GetLineMaxColumn(viewLineNumber));
    }

    /// <summary>The y of the top of a view line, relative to the control.</summary>
    internal double GetY(int viewLineNumber) =>
        _viewModel!.ViewLayout.GetVerticalOffsetForLineNumber(viewLineNumber) - ScrollTop;

    internal double ScrollTop => _viewModel?.ViewLayout.GetCurrentScrollTop() ?? 0;

    internal double ScrollLeft => _viewModel?.ViewLayout.GetCurrentScrollLeft() ?? 0;

    internal CodeEditorLayer BackgroundLayer => _backgroundLayer;

    internal CodeEditorLayer TextLayer => _textLayer;

    internal CodeEditorLayer DecorationsLayer => _decorationsLayer;

    internal CodeEditorLayer CursorLayer => _cursorLayer;

    internal CodeEditorLayer GutterLayer => _gutterLayer;

    /// <summary>The shaped lines of the text layer (for tests).</summary>
    internal CodeEditorTextCache? TextCache => _textCache;

    /// <summary>Repaints every layer (hides <see cref="Visual.InvalidateVisual"/>, which repaints only the view's own, empty, drawing).</summary>
    internal new void InvalidateVisual()
    {
        base.InvalidateVisual();
        InvalidateLayers(CodeEditorLayers.All);
    }

    /// <summary>Repaints the cursor layer only: what a cursor blink needs.</summary>
    internal void InvalidateCursors() => InvalidateLayers(CodeEditorLayers.Cursors);

    /// <summary>Repaints the given layers.</summary>
    internal void InvalidateLayers(CodeEditorLayers layers)
    {
        if (layers.HasFlag(CodeEditorLayers.Background))
        {
            _backgroundLayer.InvalidateVisual();
        }

        if (layers.HasFlag(CodeEditorLayers.Text))
        {
            _textLayer.InvalidateVisual();
        }

        if (layers.HasFlag(CodeEditorLayers.Decorations))
        {
            _decorationsLayer.InvalidateVisual();
        }

        if (layers.HasFlag(CodeEditorLayers.Cursors))
        {
            _cursorLayer.InvalidateVisual();
        }

        if (layers.HasFlag(CodeEditorLayers.Gutter))
        {
            _gutterLayer.InvalidateVisual();
        }
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        // the whole surface takes the pointer, not only where text is drawn (the layers take no input)
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
    }

    private bool CanDraw => _viewModel is not null && _editor is not null && _textCache is not null && Bounds.Width > 0 && Bounds.Height > 0;

    private (int Start, int End) GetVisibleLines()
    {
        var viewport = _viewModel!.ViewLayout.GetLinesViewportData();
        return (Math.Max(1, viewport.StartLineNumber), Math.Min(_viewModel.GetLineCount(), viewport.EndLineNumber));
    }

    private CodeEditorShapedLine GetShapedLine(int viewLineNumber) =>
        _textCache!.Get(_viewModel!.GetLineContent(viewLineNumber), _editor!.Model.GetOptions().TabSize);

    private Rect TextArea(double gutterWidth) => new(gutterWidth, 0, Math.Max(0, TextAreaRight - gutterWidth), Bounds.Height);

    // ---- background: the current line and the selections

    private void RenderBackground(DrawingContext context)
    {
        if (!CanDraw)
        {
            return;
        }

        var gutterWidth = GutterWidth;
        var primary = _viewModel!.GetPrimaryCursorState().ViewState;

        // the current line, when the primary cursor selects nothing
        if (CurrentLineBrush is { } currentLine && primary.Selection.IsEmpty)
        {
            var y = GetY(primary.Position.LineNumber);
            context.FillRectangle(currentLine, new Rect(gutterWidth, y, Math.Max(0, TextAreaRight - gutterWidth), _lineHeight));
        }

        var (startLine, endLine) = GetVisibleLines();
        using (context.PushClip(TextArea(gutterWidth)))
        {
            RenderDecorationBackgrounds(context, startLine, endLine);
            RenderSelections(context, startLine, endLine);
            RenderIndentGuides(context, startLine, endLine, primary.Position);
        }
    }

    private void RenderSelections(DrawingContext context, int startLine, int endLine)
    {
        if (SelectionBrush is not { } brush)
        {
            return;
        }

        {
            foreach (var state in _viewModel!.GetCursorStates())
            {
                var selection = state.ViewState.Selection;
                if (selection.IsEmpty)
                {
                    continue;
                }

                var from = Math.Max(startLine, selection.StartLineNumber);
                var to = Math.Min(endLine, selection.EndLineNumber);
                for (var line = from; line <= to; line++)
                {
                    var startColumn = line == selection.StartLineNumber ? selection.StartColumn : 1;
                    var endColumn = line == selection.EndLineNumber ? selection.EndColumn : _viewModel.GetLineMaxColumn(line);
                    var x1 = GetX(line, startColumn);
                    var x2 = GetX(line, endColumn);
                    if (line != selection.EndLineNumber)
                    {
                        // the line break is selected too
                        x2 += _charWidth / 2;
                    }

                    context.FillRectangle(brush, new Rect(x1, GetY(line), Math.Max(0, x2 - x1), _lineHeight));
                }
            }
        }
    }

    // ---- text

    private void RenderText(DrawingContext context)
    {
        if (!CanDraw)
        {
            return;
        }

        var foreground = TextElement.GetForeground(this) ?? Brushes.Black;
        if (!ReferenceEquals(_textCache!.Metrics.Foreground, foreground))
        {
            // the fallback layouts carry their brush: a new foreground (a theme change) shapes them again
            _textCache.Reset(_metrics!.WithForeground(foreground));
        }

        var gutterWidth = GutterWidth;
        var scrollLeft = ScrollLeft;
        var textWidth = Math.Max(0, TextAreaRight - gutterWidth);
        var tabSize = _editor!.Model.GetOptions().TabSize;
        var (startLine, endLine) = GetVisibleLines();
        var maxLineWidth = 0.0;
        var visibleRange = new TextRange(startLine, 1, endLine, _viewModel!.GetLineMaxColumn(endLine));

        // the inline decorations' colors join the palette before any line is drawn (a new palette starts the cache over)
        EnsureDecorationPalette(visibleRange);
        var cache = _textCache;
        using (context.PushClip(TextArea(gutterWidth)))
        {
            for (var line = startLine; line <= endLine; line++)
            {
                var content = _viewModel!.GetLineContent(line);
                if (content.Length == 0)
                {
                    continue;
                }

                var shaped = cache.Get(content, tabSize, GetLineColors(line, visibleRange));
                shaped.Draw(context, foreground, gutterWidth - scrollLeft, GetY(line), scrollLeft, scrollLeft + textWidth);
                maxLineWidth = Math.Max(maxLineWidth, shaped.Width);
            }
        }

        cache.EndFrame(endLine - startLine + 1);
        EnsureMaxLineWidth(maxLineWidth);
        ReportVisibleLines(startLine, endLine);
    }

    // Upstream's ViewLines._ensureMaxLineWidth: the scroll width grows to the widest line drawn so far (it never
    // shrinks while drawing, so the scroll position is not clamped from inside a frame; a flush starts over).
    private void EnsureMaxLineWidth(double lineWidth)
    {
        var width = Math.Ceiling(lineWidth + _charWidth);
        if (width > _maxLineWidth)
        {
            _maxLineWidth = width;
            _viewModel!.ViewLayout.SetMaxLineWidth(width);
        }
    }

    private void ResetMaxLineWidth()
    {
        _maxLineWidth = 0;
        _viewModel?.ViewLayout.SetMaxLineWidth(0);
    }

    // ---- cursors

    private void RenderCursors(DrawingContext context)
    {
        if (!CanDraw || CursorBrush is not { } brush || _editor is not { IsKeyboardFocusWithin: true } || !_cursorVisible)
        {
            return;
        }

        using (context.PushClip(TextArea(GutterWidth)))
        {
            foreach (var state in _viewModel!.GetCursorStates())
            {
                var position = state.ViewState.Position;
                var x = GetX(position.LineNumber, position.Column);
                context.FillRectangle(brush, new Rect(Math.Round(x), GetY(position.LineNumber), CursorWidth, _lineHeight));
            }
        }
    }

    // ---- line numbers

    private void RenderGutter(DrawingContext context)
    {
        var gutterLayout = GetGutterLayout();
        var gutterWidth = gutterLayout.Width;
        if (!CanDraw || gutterWidth <= 0)
        {
            return;
        }

        var cursorLines = new HashSet<int>();
        foreach (var state in _viewModel!.GetCursorStates())
        {
            cursorLines.Add(state.ViewState.Position.LineNumber);
        }

        var brush = LineNumberBrush ?? TextElement.GetForeground(this) ?? Brushes.Gray;
        var active = ActiveLineNumberBrush ?? brush;
        var normalCache = GetLineNumberCache(ref _lineNumberCache, brush);
        var activeCache = GetLineNumberCache(ref _activeLineNumberCache, active);
        var converter = _viewModel.CoordinatesConverter;
        var right = gutterLayout.LineNumbersRight;
        var (startLine, endLine) = GetVisibleLines();
        using (context.PushClip(new Rect(0, 0, gutterWidth, Bounds.Height)))
        {
            RenderGutterColumns(context, gutterLayout, startLine, endLine, cursorLines);
            for (var line = startLine; _editor!.ShowLineNumbers && line <= endLine; line++)
            {
                var modelPosition = converter.ConvertViewPositionToModelPosition(new TextPosition(line, 1));
                if (modelPosition.Column != 1)
                {
                    // a wrapped continuation of the line above has no number
                    continue;
                }

                var isActive = cursorLines.Contains(line);
                var cache = isActive ? activeCache : normalCache;
                var number = cache.Get(modelPosition.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), 1);
                var width = number.GetX(number.Text.Length + 1);
                number.Draw(context, isActive ? active : brush, right - width, GetY(line), 0, width);
            }
        }

        normalCache.EndFrame(endLine - startLine + 1);
        activeCache.EndFrame(endLine - startLine + 1);
    }

    // The line numbers in one brush: a cache per brush, since a fallback layout carries the brush it was built with.
    private CodeEditorTextCache GetLineNumberCache(ref CodeEditorTextCache? cache, IBrush brush)
    {
        if (cache is null || !ReferenceEquals(cache.Metrics.Foreground, brush))
        {
            cache?.Clear();
            cache = new CodeEditorTextCache(_metrics!.WithForeground(brush));
        }

        return cache;
    }

    private static int CountDigits(int value)
    {
        var digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }
}
