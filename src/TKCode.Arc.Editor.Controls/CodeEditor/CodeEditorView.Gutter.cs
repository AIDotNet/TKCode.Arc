// The gutter's layout and columns (upstream's EditorLayoutInfo: glyphMarginLeft/Width, lineNumbersLeft/Width,
// decorationsLeft/Width; the margin view parts GlyphMarginWidgets, LinesDecorationsOverlay and
// MarginViewLineDecorationsOverlay; and the gutter cases of MouseTargetFactory in
// src/vs/editor/browser/controller/mouseTarget.ts; commit b57d5c9e, MIT license). The columns are the editor's
// (CodeEditorGutter): before the line numbers, the numbers, after them, then the gap to the text.

using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

internal sealed partial class CodeEditorView
{
    private CodeEditorGutter? _gutter;
    private CodeEditorGutterColumn? _hoveredColumn;

    /// <summary>A gutter column's place.</summary>
    internal readonly record struct GutterColumnLayout(CodeEditorGutterColumn Column, double X, double Width);

    /// <summary>
    /// The gutter's layout: its width (0 without line numbers and columns), where the line numbers are (their right edge
    /// is where they are aligned), and the visible columns.
    /// </summary>
    internal sealed record GutterLayout(double Width, double LineNumbersLeft, double LineNumbersRight, IReadOnlyList<GutterColumnLayout> Columns);

    /// <summary>The gutter's layout now.</summary>
    internal GutterLayout GetGutterLayout()
    {
        var columns = new List<GutterColumnLayout>();
        if (_editor is not { } editor)
        {
            return new GutterLayout(0, 0, 0, columns);
        }

        var x = 0.0;
        foreach (var column in editor.Gutter.Columns)
        {
            if (column.IsBeforeLineNumbers && column.EffectiveWidth > 0)
            {
                columns.Add(new GutterColumnLayout(column, x, column.EffectiveWidth));
                x += column.EffectiveWidth;
            }
        }

        var numbersLeft = x;
        if (editor.ShowLineNumbers)
        {
            var digits = Math.Max(2, CountDigits(editor.Model.GetLineCount()));
            x += GutterLeadingPadding + (digits * _charWidth);
        }

        var numbersRight = x;
        foreach (var column in editor.Gutter.Columns)
        {
            if (!column.IsBeforeLineNumbers && column.EffectiveWidth > 0)
            {
                columns.Add(new GutterColumnLayout(column, x, column.EffectiveWidth));
                x += column.EffectiveWidth;
            }
        }

        if (x > 0)
        {
            x += GutterPadding;
        }

        return new GutterLayout(x, numbersLeft, numbersRight, columns);
    }

    private void AttachGutter()
    {
        DetachGutter();
        _gutter = _editor?.Gutter;
        if (_gutter is not null)
        {
            _gutter.LayoutChanged += OnGutterLayoutChanged;
            _gutter.Invalidated += OnGutterInvalidated;
        }
    }

    private void DetachGutter()
    {
        if (_gutter is not null)
        {
            _gutter.LayoutChanged -= OnGutterLayoutChanged;
            _gutter.Invalidated -= OnGutterInvalidated;
            _gutter = null;
        }
    }

    // a column's width changes the text area: lay out again
    private void OnGutterLayoutChanged()
    {
        UpdateConfiguration();
        _editor?.LayoutWidgets();
    }

    private void OnGutterInvalidated() => InvalidateLayers(CodeEditorLayers.Gutter);

    /// <summary>The cell of a gutter column on a view line (relative to the view).</summary>
    internal Rect GetGutterCell(GutterColumnLayout column, int viewLineNumber) =>
        new(column.X, GetY(viewLineNumber), column.Width, _lineHeight);

    // Margin decorations and the columns' own drawing, for the visible lines (from RenderGutter, before the numbers).
    private void RenderGutterColumns(DrawingContext context, GutterLayout layout, int startLine, int endLine, HashSet<int> cursorLines)
    {
        if (_viewModel is not { } viewModel || _editor is not { } editor)
        {
            return;
        }

        var styles = editor.DecorationStyles;
        var visibleRange = new TextRange(startLine, 1, endLine, viewModel.GetLineMaxColumn(endLine));
        var decorations = viewModel.GetDecorationsInViewport(visibleRange);

        // MarginClassName: the whole gutter row
        foreach (var decoration in decorations)
        {
            foreach (var style in styles.GetAll(decoration.Options.MarginClassName))
            {
                if ((Resolve(style.MarginBackground) ?? Resolve(style.Background)) is not { } fill)
                {
                    continue;
                }

                for (var line = Math.Max(startLine, decoration.Range.StartLineNumber); line <= Math.Min(endLine, decoration.Range.EndLineNumber); line++)
                {
                    context.FillRectangle(fill, new Rect(0, GetY(line), layout.Width, _lineHeight));
                }
            }
        }

        var converter = viewModel.CoordinatesConverter;
        foreach (var column in layout.Columns)
        {
            var isGlyphMargin = column.Column.Id == CodeEditorGutter.GlyphMarginId;
            var isLinesDecorations = column.Column.Id == CodeEditorGutter.LinesDecorationsId;
            if (isGlyphMargin || isLinesDecorations)
            {
                foreach (var decoration in decorations)
                {
                    var className = isGlyphMargin ? decoration.Options.GlyphMarginClassName : decoration.Options.LinesDecorationsClassName;
                    foreach (var style in styles.GetAll(className))
                    {
                        // a glyph shows on the decoration's first line, a lines decoration on each of its lines
                        var from = Math.Max(startLine, decoration.Range.StartLineNumber);
                        var to = isGlyphMargin ? decoration.Range.StartLineNumber : Math.Min(endLine, decoration.Range.EndLineNumber);
                        for (var line = from; line <= to && line <= endLine; line++)
                        {
                            DrawMarginStyle(context, style, GetGutterCell(column, line));
                        }
                    }
                }
            }

            if (column.Column.Draw is { } draw)
            {
                for (var line = startLine; line <= endLine; line++)
                {
                    var model = converter.ConvertViewPositionToModelPosition(new TextPosition(line, 1));
                    var isFirst = model.Column == 1;
                    draw(context, new CodeEditorGutterLine(line, model.LineNumber, isFirst, GetGutterCell(column, line), cursorLines.Contains(line)));
                }
            }
        }
    }

    private void DrawMarginStyle(DrawingContext context, CodeEditorDecorationStyle style, Rect cell)
    {
        if (style.Glyph is { } glyph)
        {
            if ((Resolve(style.GlyphBrush) ?? GutterGlyphBrush ?? LineNumberBrush) is not { } brush)
            {
                return;
            }

            var size = Math.Max(1, Math.Min(cell.Width, cell.Height) - 2);
            var scale = size / 16;
            var x = cell.X + ((cell.Width - size) / 2);
            var y = cell.Y + ((cell.Height - size) / 2);
            using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
            {
                context.DrawGeometry(brush, null, glyph);
            }

            return;
        }

        if ((Resolve(style.MarginBackground) ?? Resolve(style.Background)) is { } fill)
        {
            var width = style.MarginWidth > 0 ? Math.Min(style.MarginWidth, cell.Width) : cell.Width;
            context.FillRectangle(fill, new Rect(cell.X, cell.Y, width, cell.Height));
        }
    }

    // ---- hit testing (upstream's MouseTargetFactory, for what this view has)

    /// <summary>The gutter column at an x of the view, or null.</summary>
    internal GutterColumnLayout? GetGutterColumnAt(double x)
    {
        foreach (var column in GetGutterLayout().Columns)
        {
            if (x >= column.X && x < column.X + column.Width)
            {
                return column;
            }
        }

        return null;
    }

    /// <summary>What lies under a point of the view.</summary>
    internal CodeEditorMouseTarget GetMouseTargetAt(Point point)
    {
        if (_viewModel is not { } viewModel)
        {
            return new CodeEditorMouseTarget(CodeEditorMouseTargetType.Unknown, null, null) { Point = point };
        }

        var layout = GetGutterLayout();
        var y = point.Y + ScrollTop;
        var inGutter = point.X < layout.Width;

        // a view zone between lines
        if (viewModel.ViewLayout.GetWhitespaceAtVerticalOffset(y) is { } whitespace && _editor?.GetZoneIdOfWhitespace(whitespace.Id) is { } zoneId)
        {
            return new CodeEditorMouseTarget(inGutter ? CodeEditorMouseTargetType.GutterViewZone : CodeEditorMouseTargetType.ContentViewZone, null, null)
            {
                Detail = zoneId,
                Point = point,
            };
        }

        var viewPosition = GetViewPositionAt(new Point(Math.Max(point.X, layout.Width), point.Y));
        var position = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(viewPosition);
        if (inGutter)
        {
            var lineRange = new TextRange(position.LineNumber, 1, position.LineNumber, 1);
            if (GetGutterColumnAt(point.X) is { } column)
            {
                var type = column.Column.IsBeforeLineNumbers ? CodeEditorMouseTargetType.GutterGlyphMargin : CodeEditorMouseTargetType.GutterLineDecorations;
                return new CodeEditorMouseTarget(type, position, lineRange) { Detail = column.Column.Id, Point = point };
            }

            var onNumbers = point.X >= layout.LineNumbersLeft && point.X < layout.LineNumbersRight;
            return new CodeEditorMouseTarget(onNumbers ? CodeEditorMouseTargetType.GutterLineNumbers : CodeEditorMouseTargetType.GutterLineDecorations, position, lineRange)
            {
                Point = point,
            };
        }

        var mouseColumn = (int)Math.Round(Math.Max(0, point.X - layout.Width + ScrollLeft) / _charWidth) + 1;
        var lineEnd = GetX(viewPosition.LineNumber, viewModel.GetLineMaxColumn(viewPosition.LineNumber));
        var isEmpty = viewModel.ViewLayout.IsAfterLines(y) || point.X > lineEnd + (_charWidth / 2);
        var range = new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column);
        return new CodeEditorMouseTarget(isEmpty ? CodeEditorMouseTargetType.ContentEmpty : CodeEditorMouseTargetType.ContentText, position, range)
        {
            MouseColumn = mouseColumn,
            Point = point,
        };
    }

    // A press on a gutter column with a handler; true when it handled it.
    private bool DispatchGutterPress(PointerPressedEventArgs e, Point point)
    {
        if (_viewModel is not { } viewModel || GetGutterColumnAt(point.X) is not { } column || column.Column.PointerPressed is not { } handler)
        {
            return false;
        }

        var viewLine = GetViewPositionAt(new Point(GutterWidth, point.Y)).LineNumber;
        var modelLine = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(new TextPosition(viewLine, 1)).LineNumber;
        return handler(new CodeEditorGutterPointerEvent(e, viewLine, modelLine, GetGutterCell(column, viewLine)));
    }

    // The pointer over the gutter columns: tells the column under it, and the one it left; sets the column's cursor.
    private void DispatchGutterMove(PointerEventArgs e, Point point)
    {
        if (_viewModel is not { } viewModel)
        {
            return;
        }

        var column = point.X < GutterWidth ? GetGutterColumnAt(point.X) : null;
        if (_hoveredColumn is { } previous && previous != column?.Column)
        {
            previous.PointerMoved?.Invoke(null);
        }

        _hoveredColumn = column?.Column;
        if (column is { } current)
        {
            var viewLine = GetViewPositionAt(new Point(GutterWidth, point.Y)).LineNumber;
            var modelLine = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(new TextPosition(viewLine, 1)).LineNumber;
            current.Column.PointerMoved?.Invoke(new CodeEditorGutterPointerEvent(e, viewLine, modelLine, GetGutterCell(current, viewLine)));
        }

        Cursor = point.X < GutterWidth ? column?.Column.Cursor ?? s_arrowCursor : s_textCursor;
    }

    private static readonly Cursor s_arrowCursor = new(StandardCursorType.Arrow);

    private static readonly Cursor s_textCursor = new(StandardCursorType.Ibeam);
}
